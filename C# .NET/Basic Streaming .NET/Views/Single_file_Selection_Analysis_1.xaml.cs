using MathNet.Numerics.LinearAlgebra;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.Wpf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MathNet.Filtering;
using MathNet.Filtering.IIR;
using System.Threading.Tasks;


namespace Basic_Streaming_NET.Views
{
    /// <summary>
    /// Single_file_Selection_Analysis_1.xaml 的互動邏輯
    /// </summary>
    public partial class Single_file_Selection_Analysis_1 : Window
    {
        private string _patientFilePath;
        private List<List<double>> globalEmgData;

        // 取樣頻率，設置為每秒 1926 點
        private const int SamplingRate = 1926;

        // 用於存儲每個通道正則化後的數據
        private List<List<double>> normalizedEmgData;

        // 用於存儲每個通道的 RMS 計算結果
        private List<List<double>> rmsValues;

        // 用於存儲所有標記位置的全局變量
        private List<int> markerPositions;

        //// 在類的頂部定義 filteredEmgData 作爲全局變量 濾完波的
        private List<List<double>> filteredEmgData;

        int k = 3; // 基向量數量設為 3

        public Single_file_Selection_Analysis_1(string patient_name)
        {
            InitializeComponent();

            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            _patientFilePath = Path.Combine(baseDirectory, "sensor_data", patient_name);

            // 初始化 TreeView
            PopulateTreeView(_patientFilePath);

            // 展開根目錄下一層
            ExpandRootLevel();
        }

        private void PopulateTreeView(string rootPath)
        {
            if (!Directory.Exists(rootPath))
            {
                MessageBox.Show($"目錄不存在: {rootPath}");
                return;
            }

            TreeViewItem rootItem = CreateTreeViewItem(rootPath);
            TreeView.Items.Add(rootItem);
        }

        private TreeViewItem CreateTreeViewItem(string path)
        {
            TreeViewItem item = new TreeViewItem
            {
                Header = Path.GetFileName(path),
                Tag = path
            };

            // 添加佔位符以支持延遲加載
            item.Items.Add(null);
            item.Expanded += TreeViewItem_Expanded;

            return item;
        }

        private void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
        {
            TreeViewItem item = (TreeViewItem)sender;

            // 如果已經加載過子項，則不再加載
            if (item.Items.Count == 1 && item.Items[0] == null)
            {
                item.Items.Clear();

                string path = (string)item.Tag;
                try
                {
                    // 加載子目錄
                    foreach (string directory in Directory.GetDirectories(path))
                    {
                        item.Items.Add(CreateTreeViewItem(directory));
                    }

                    // 加載 .csv 文件
                    foreach (string file in Directory.GetFiles(path, "*.csv"))
                    {
                        var radioButton = new RadioButton
                        {
                            Content = Path.GetFileName(file),
                            GroupName = "CsvFiles", // 確保所有 .csv 文件是同一組的選項
                            Tag = file
                        };

                        var fileItem = new TreeViewItem
                        {
                            Header = radioButton,
                            Tag = file
                        };

                        // 訂閱 RadioButton 的 Checked 事件
                        radioButton.Checked += RadioButton_Checked;

                        item.Items.Add(fileItem);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"無法加載目錄: {ex.Message}");
                }
            }
        }

        private void ExpandRootLevel()
        {
            // 展開根目錄下的第一層子目錄
            foreach (TreeViewItem rootItem in TreeView.Items)
            {
                rootItem.IsExpanded = true;
            }
        }

        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radioButton)
            {
                // 記錄選中的 RadioButton 路徑
                SelectedCsvFilePath = radioButton.Tag as string;
            }
        }

        private string SelectedCsvFilePath { get; set; }

        private void LoadMarkerPositionsFromCsv(string filePath)
        {
            // 初始化 markerPositions 全局變量
            markerPositions = new List<int>();

            // 讀取 CSV 文件的所有行
            var lines = File.ReadAllLines(filePath);

            // 假設最後一列是標記列，跳過第一行標題行
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                var parts = line.Split(',');

                // 獲取最後一列的值
                if (double.TryParse(parts[parts.Length - 1], out double markerValue) && markerValue > 0)
                {
                    // 如果標記值大於 0，則保存這個標記的位置（行號或者時間點）
                    markerPositions.Add(i);

                }
            }

            Console.WriteLine("標記位置已成功讀取並存儲到全局變量中！");
        }

        private async void ConfirmButton_Click_DiffParams(object sender, RoutedEventArgs e)
        {
            // 檢查是否有選擇的 CSV 文件路徑
            if (KCHECK.Text == "")
            {
                    CustomMessageBox _CustomMessageBox = new CustomMessageBox(10);
                    _CustomMessageBox.Show();
            }
            else
            {
                if (!string.IsNullOrEmpty(SelectedCsvFilePath))
                {
                    //MessageBox.Show($"已選擇的文件: {SelectedCsvFilePath}");

                    // 創建並顯示進度條窗口
                    var progressWindow = new Window
                    {
                        Title = "數據處理中，請稍後",
                        Width = 400,
                        Height = 100,
                        Content = new ProgressBar
                        {
                            Name = "ProgressBar",
                            Minimum = 0,
                            Maximum = 100,
                            Value = 0,
                            Height = 30,
                            Margin = new Thickness(10)
                        }
                    };
                    progressWindow.Show();

                    // 獲取進度條對象
                    ProgressBar progressBar = (ProgressBar)progressWindow.Content;

                    await Task.Run(async () =>
                    {
                        // 調用函數加載 CSV 數據到全局 List 中
                        LoadDataToGlobalList(SelectedCsvFilePath);
                        // 加載 CSV 文件中的標記位置
                        LoadMarkerPositionsFromCsv(SelectedCsvFilePath);

                        //濾波
                        //ApplyButterworthFilter();
                        filteredEmgData = globalEmgData;

                        // Z 正則化
                        ZNormalizeFilteredEmgData();

                        // 計算 1/10 秒的 RMS
                        CalculateRMSForAllChannels();

                        // 對所有通道的數據進行插值
                        rmsValues = InterpolateToFixedPoints(rmsValues, 100);

                        // 提取區間內的 RMS 值
                        var V = Matrix<double>.Build.DenseOfArray(GetRMSValuesInMarkRange());

                        
                        int maxIter = 5000;
                        double tolerance = 1e-4;

                        // NNMF 分解
                        var (W, H) = await NNMFDecompositionAsync(V, k, maxIter, tolerance, progressBar);

                        // 檢查 W 和 H 矩陣是否有有效數據
                        if ((W == null || W.RowCount == 0 || W.ColumnCount == 0) ||
                            (H == null || H.RowCount == 0 || H.ColumnCount == 0))
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show("W 或 H 矩陣無有效數據。");
                                progressWindow.Close();
                            });
                            return;
                        }

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            var ResultsStackPanel = (StackPanel)this.FindName("ResultsStackPanel");
                            if (ResultsStackPanel != null)
                            {
                                ResultsStackPanel.Children.Clear(); // 清空現有內容
                                VisualizeWandHInStackPanel(W, H, ResultsStackPanel);
                            }
                            else
                            {
                                MessageBox.Show("找不到 ResultsStackPanel。");
                            }

                            progressBar.Value = 100; // 完成
                            progressWindow.Close(); // 關閉進度條窗口
                        });
                    });
                }
                else
                {
                    MessageBox.Show("請選擇一個 .csv 文件！");
                }
            }
            
        }
        private void LoadDataToGlobalList(string filePath)
        {
            // 初始化全局變量
            globalEmgData = new List<List<double>>();

            // 讀取 CSV 文件的所有行
            var lines = File.ReadAllLines(filePath);

            // 設置 EMG 通道數量
            int emgCount = lines[0].Split(',').Length - 1;

            // 初始化每個 EMG 通道的 List
            for (int i = 0; i < emgCount; i++)
            {
                globalEmgData.Add(new List<double>());
            }

            // 從第 1 行開始遍歷 CSV 數據，跳過標題行
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                var parts = line.Split(',');

                // 解析每個通道的數據，並添加到全局變量中
                for (int j = 0; j < emgCount; j++)
                {
                    if (double.TryParse(parts[j], out double emgValue))
                    {
                        globalEmgData[j].Add(emgValue);
                    }
                }
            }
            Console.WriteLine("CSV 數據已成功讀取並存儲到全局變量中！");

        }
        //濾波
        private void ApplyButterworthFilter()
        {
            double sampleRate = 1926.0; // 假設採樣率爲 1926 Hz
            double lowCutoff = 10.0;
            double highCutoff = 300.0;

            // 創建一個新列表來保存濾波後的數據
            filteredEmgData = new List<List<double>>();

            foreach (var channelData in globalEmgData)
            {
                // 初始化濾波後的通道數據
                var filteredChannelData = new List<double>();

                // 每次濾波新通道時，重新創建帶通 Butterworth 濾波器，確保濾波器狀態獨立
                var bandPassFilter = OnlineIirFilter.CreateBandpass(ImpulseResponse.Infinite, lowCutoff / (sampleRate / 2), highCutoff / (sampleRate / 2), 6);

                // 對每個樣本進行濾波並添加調試信息
                for (int j = 0; j < channelData.Count; j++)
                {
                    double filteredSample = bandPassFilter.ProcessSample(channelData[j]);

                    // 添加調試信息以確認濾波後的樣本值
                    if (j < 5) // 僅打印前 5 個樣本
                    {
                        Console.WriteLine($"原始樣本值: {channelData[j]}, 濾波後樣本值: {filteredSample}");
                    }

                    filteredChannelData.Add(filteredSample);
                }

                // 添加濾波後的通道數據到列表中
                filteredEmgData.Add(filteredChannelData);
            }

            Console.WriteLine("EMG 數據已成功進行濾波並保存！");

            // 跳出窗口並顯示濾波前後的對比
            for (int i = 0; i < globalEmgData.Count; i++)
            {

                var originalData = globalEmgData[i];
                var filteredData = filteredEmgData[i];

                var comparisonWindow = new Window
                {
                    Title = $"通道 {i + 1} 的濾波前後對比",
                    Width = 800,
                    Height = 600
                };

                var comparisonGrid = new Grid();
                comparisonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                comparisonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                // 原始數據折線圖
                var modelOriginal = new PlotModel { Title = $"通道 {i + 1} 的原始數據" };
                var lineSeriesOriginal = new LineSeries
                {
                    Title = "原始數據",
                    Color = OxyColors.Red
                };

                for (int j = 0; j < originalData.Count; j++)
                {
                    lineSeriesOriginal.Points.Add(new DataPoint(j, originalData[j]));
                }
                modelOriginal.Series.Add(lineSeriesOriginal);
                modelOriginal.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "時間點" });
                modelOriginal.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "原始數據值" });

                var plotViewOriginal = new PlotView
                {
                    Model = modelOriginal,
                    Width = 400,
                    Height = 300,
                    Margin = new Thickness(10)
                };
                Grid.SetColumn(plotViewOriginal, 0);
                comparisonGrid.Children.Add(plotViewOriginal);

                // 濾波後數據折線圖
                var modelFiltered = new PlotModel { Title = $"通道 {i + 1} 的濾波後數據" };
                var lineSeriesFiltered = new LineSeries
                {
                    Title = "濾波後數據",
                    Color = OxyColors.Green
                };

                for (int j = 0; j < filteredData.Count; j++)
                {
                    lineSeriesFiltered.Points.Add(new DataPoint(j, filteredData[j]));
                }
                modelFiltered.Series.Add(lineSeriesFiltered);
                modelFiltered.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "時間點" });
                modelFiltered.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "濾波後數據值" });

                var plotViewFiltered = new PlotView
                {
                    Model = modelFiltered,
                    Width = 400,
                    Height = 300,
                    Margin = new Thickness(10)
                };
                Grid.SetColumn(plotViewFiltered, 1);
                comparisonGrid.Children.Add(plotViewFiltered);

                comparisonWindow.Content = comparisonGrid;
                comparisonWindow.Show();
            }
        }


        // 簡單實現快速傅里葉變換（FFT）進行頻譜分析
        private double[] FFT(List<double> data)
        {
            // 使用 FFTW 或類似的庫來計算 FFT（此處爲僞代碼，需要替換爲實際的 FFT 實現）
            return new double[data.Count / 2]; // 僅返回一半的頻譜數據
        }


        private void ZNormalizeFilteredEmgData()
        {
            foreach (var channelData in filteredEmgData)
            {
                if (channelData.Count > 0)
                {
                    double mean = channelData.Average();
                    double variance = channelData.Sum(value => Math.Pow(value - mean, 2)) / channelData.Count;
                    double stdDev = Math.Sqrt(variance);

                    for (int i = 0; i < channelData.Count; i++)
                    {
                        channelData[i] = (channelData[i] - mean) / stdDev;
                    }
                }
            }

            Console.WriteLine("所有通道的濾波數據已完成 Z 正則化！");
        }


        // 計算 1/10 秒的 RMS
        private void CalculateRMSForAllChannels()
        {
            // 設置取樣頻率爲 1926 點每秒
            const int SamplingRate = 1926;

            // 設置每 100 毫秒的數據點數
            int windowSize = SamplingRate / 10; // 100 毫秒窗口

            // 初始化全局變量 rmsValues
            rmsValues = new List<List<double>>();

            // 遍歷每個通道的數據（使用濾波後的數據）
            foreach (var channelData in filteredEmgData)
            {
                List<double> channelRmsValues = new List<double>();

                // 分割數據並計算每個窗口的 RMS 值
                for (int i = 0; i + windowSize <= channelData.Count; i += windowSize)
                {
                    double sumOfSquares = 0.0;

                    // 計算當前窗口的平方和
                    for (int j = i; j < i + windowSize; j++)
                    {
                        sumOfSquares += channelData[j] * channelData[j];
                    }

                    // 計算 RMS 值
                    double rms = Math.Sqrt(sumOfSquares / windowSize);
                    channelRmsValues.Add(rms);
                }

                // 將每個通道的 RMS 值添加到全局列表
                rmsValues.Add(channelRmsValues);
            }

            Console.WriteLine("所有通道的 RMS 已完成計算！");
        }


        // 對 RMS 進行正則化處理
        private List<List<double>> InterpolateToFixedPoints(List<List<double>> originalValues, int targetPoints)
        {
            List<List<double>> interpolatedValues = new List<List<double>>();

            foreach (var channelValues in originalValues)
            {
                List<double> channelInterpolated = new List<double>();
                double step = (double)(channelValues.Count - 1) / (targetPoints - 1);

                for (int i = 0; i < targetPoints; i++)
                {
                    double index = i * step;
                    int lowerIndex = (int)Math.Floor(index);
                    int upperIndex = (int)Math.Ceiling(index);

                    if (lowerIndex == upperIndex)
                    {
                        channelInterpolated.Add(channelValues[lowerIndex]);
                    }
                    else
                    {
                        double lowerValue = channelValues[lowerIndex];
                        double upperValue = channelValues[upperIndex];
                        double weight = index - lowerIndex;
                        channelInterpolated.Add(lowerValue * (1 - weight) + upperValue * weight);
                    }
                }

                interpolatedValues.Add(channelInterpolated);
            }

            return interpolatedValues;
        }


        // 初始化矩陣方法
        private static Random rand = new Random();

        // 初始化矩陣
        private static Matrix<double> InitializeMatrix(int rows, int cols)
        {
            var matrix = Matrix<double>.Build.Dense(rows, cols, (i, j) => rand.NextDouble() * 0.1);
            return matrix;
        }

        // NNMF 分解方法，多次進行分解以尋找最佳結果
        public static async Task<(Matrix<double>, Matrix<double>)> NNMFDecompositionAsync(Matrix<double> V, int k, int maxIter, double tolerance, ProgressBar progressBar)
        {
            Matrix<double> bestW = null;
            Matrix<double> bestH = null;
            double bestError = double.MaxValue;

            int numRuns = 100; // 多次初始化以避免局部最小值

            for (int run = 0; run < numRuns; run++)
            {
                // 初始化 W 和 H
                Matrix<double> W = InitializeMatrix(V.RowCount, k);
                Matrix<double> H = InitializeMatrix(k, V.ColumnCount);

                double previousError = double.MaxValue;
                int stagnationCount = 0;
                int stagnationThreshold = 50;

                for (int iter = 0; iter < maxIter; iter++)
                {
                    // 更新 H 矩陣
                    var numeratorH = W.TransposeThisAndMultiply(V);
                    var denominatorH = W.TransposeThisAndMultiply(W).Multiply(H) + 1e-9;
                    H = H.PointwiseMultiply(numeratorH).PointwiseDivide(denominatorH);

                    // 更新 W 矩陣
                    var numeratorW = V.Multiply(H.Transpose());
                    var denominatorW = W.Multiply(H).Multiply(H.Transpose()) + 1e-9;
                    W = W.PointwiseMultiply(numeratorW).PointwiseDivide(denominatorW);

                    // 正則化 W 和 H 矩陣
                    H = H.Map(v => Math.Max(v, 1e-5));
                    W = W.Map(v => Math.Max(v, 1e-5));

                    // 計算誤差
                    var WH = W.Multiply(H);
                    double error = (V - WH).FrobeniusNorm();

                    // 若誤差變化很小，增加 stagnationCount
                    if (Math.Abs(previousError - error) < 1e-6)
                    {
                        stagnationCount++;
                    }
                    else
                    {
                        stagnationCount = 0;
                    }

                    if (stagnationCount >= stagnationThreshold)
                    {
                        System.Diagnostics.Debug.WriteLine($"Run {run + 1}, stopped early at iteration {iter + 1} due to stagnation with error: {error}");
                        break;
                    }

                    previousError = error;
                    if (error < tolerance)
                    {
                        System.Diagnostics.Debug.WriteLine($"Run {run + 1}, converged at iteration {iter + 1} with error: {error}");
                        break;
                    }

                    System.Diagnostics.Debug.WriteLine($"Run {run + 1}, Iteration {iter + 1}, Error: {error}");
                }

                // 如果本次運行的誤差更小，則更新最佳結果
                if (previousError < bestError)
                {
                    bestW = W;
                    bestH = H;
                    bestError = previousError;
                }

                // 更新進度條
                await Application.Current.Dispatcher.InvokeAsync(() => progressBar.Value = (run + 1) * 100.0 / numRuns);
            }

            System.Diagnostics.Debug.WriteLine($"Best error after {numRuns} runs: {bestError}");
            return (bestW, bestH);
        }



        private double[,] GetRMSValuesInMarkRange()
        {


            // 使用 startMarkNumber 和 endMarkNumber 來設置範圍
            double startTime = markerPositions[0];
            double endTime = markerPositions[1];

            // 原始數據的長度和 RMS 計算後數據的長度
            int originalLength = filteredEmgData[0].Count;
            int rmsLength = rmsValues[0].Count;

            // 將原始索引轉換為 RMS 索引
            int startIndexRMS = (int)((startTime * rmsLength) / originalLength);
            int endIndexRMS = (int)((endTime * rmsLength) / originalLength);

            List<List<double>> allRmsValuesInRange = new List<List<double>>();

            for (int i = 0; i < rmsValues.Count; i++) // 遍歷每個通道的 RMS 值
            {
                List<double> rmsValuesList = rmsValues[i];

                if (rmsValuesList != null && rmsValuesList.Count > 0)
                {
                    // 確保索引範圍有效
                    if (startIndexRMS >= 0 && endIndexRMS <= rmsValuesList.Count && startIndexRMS < endIndexRMS)
                    {
                        // 提取該區間的 RMS 值並保存到單獨的列表
                        List<double> rmsValuesInRange = rmsValuesList.Skip(startIndexRMS).Take(endIndexRMS - startIndexRMS).ToList();
                        allRmsValuesInRange.Add(rmsValuesInRange);
                    }
                    else
                    {
                        // 如果索引無效，則添加一個空的列表
                        allRmsValuesInRange.Add(new List<double>());
                    }
                }
                else
                {
                    // 如果沒有找到有效 RMS 值，則添加一個空的列表
                    allRmsValuesInRange.Add(new List<double>());
                }
            }

            // 確保列表中有有效數據
            int rowCount = allRmsValuesInRange.Count;
            int colCount = allRmsValuesInRange.Any(row => row.Count > 0) ? allRmsValuesInRange.Max(row => row.Count) : 0;

            double[,] data = new double[rowCount, colCount];

            for (int i = 0; i < rowCount; i++)
            {
                for (int j = 0; j < allRmsValuesInRange[i].Count; j++)
                {
                    data[i, j] = allRmsValuesInRange[i][j];
                }
            }

            return data;
        }


        // 可視化 W 和 H 矩陣
        private void VisualizeWandHInStackPanel(Matrix<double> W, Matrix<double> H, StackPanel stackPanel)
        {
            int k = W.ColumnCount; // 基向量數量

            stackPanel.Children.Clear(); // 清空 StackPanel 內容

            // 創建左右兩側 StackPanel
            var leftPanel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(10) };
            var rightPanel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(10) };
            //var rmsPanel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(10) }; // 新增 RMS Panel

            var mainGrid = new Grid();
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            //mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // 新增第三列

            Grid.SetColumn(leftPanel, 0);
            Grid.SetColumn(rightPanel, 1);
            //Grid.SetColumn(rmsPanel, 2); // 設置 RMS Panel 的列位置
            mainGrid.Children.Add(leftPanel);
            mainGrid.Children.Add(rightPanel);
            //mainGrid.Children.Add(rmsPanel);

            double hAxisMax = H.Enumerate().Max(); // 獲取 H 矩陣中的最大值
            double hAxisMin = H.Enumerate().Min(); // 獲取 H 矩陣中的最小值

            for (int j = 0; j < k; j++)
            {
                // 檢查 W 矩陣中的數據有效性
                if (W.Column(j).Any(value => double.IsNaN(value) || value < 0))
                {
                    Console.WriteLine($"W 矩陣的基向量 {j + 1} 包含無效數據，無法繪製。");
                    continue;
                }

                // W 矩陣的柱狀圖 - 顯示基向量的所有貢獻
                var modelW = new PlotModel { Title = $"W Muscle Synergy {j + 1} Contributions" };
                var contributions = W.Column(j).Select((value, index) => new { Value = value, Index = index + 1 }).ToList();

                var barSeriesW = new BarSeries
                {
                    Title = $"基向量 {j + 1}",
                    ItemsSource = contributions.Select(c => new BarItem { Value = c.Value }).ToList(),
                    BaseValue = 0,
                    FillColor = OxyColors.SkyBlue // 使用固定的顏色
                };
                modelW.Series.Add(barSeriesW);

                // 爲 W 矩陣的圖設置 X 軸和 Y 軸
                var categoryAxis = new CategoryAxis { Position = AxisPosition.Left, Title = "sensor" };
                categoryAxis.Labels.AddRange(contributions.Select(c => c.Index.ToString()));
                modelW.Axes.Add(categoryAxis);
                modelW.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "Contribution" });
                var plotViewW = new PlotView
                {
                    Model = modelW,
                    Width = 400, // 調整寬度以增加可視化空間
                    Height = 300, // 調整高度以增加可視化空間
                    Margin = new Thickness(10)
                };

                // 將 W 的圖加入到左側 StackPanel
                leftPanel.Children.Add(plotViewW);

                var modelH = new PlotModel { Title = $"H Activation coefficient {j + 1}" };
                var lineSeriesH = new LineSeries
                {
                    Title = $"基向量 {j + 1}",
                    Color = OxyColors.SkyBlue // 使用固定的顏色
                };

                for (int i = 0; i < H.ColumnCount; i++)
                {
                    lineSeriesH.Points.Add(new DataPoint(i, H[j, i]));
                }
                modelH.Series.Add(lineSeriesH);
                modelH.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "time" });
                modelH.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "Activation Coefficient", Minimum = hAxisMin, Maximum = hAxisMax });

                var plotViewH = new PlotView
                {
                    Model = modelH,
                    Width = 400, // 調整寬度以增加可視化空間
                    Height = 300, // 調整高度以增加可視化空間
                    Margin = new Thickness(10)
                };

                // 將 H 的圖加入到右側 StackPanel
                rightPanel.Children.Add(plotViewH);
            }

            // RMS 值的折線圖 - 顯示每個通道的 RMS 值
            //for (int i = 0; i < rmsValues.Count; i++)
            //{
            //    var modelRMS = new PlotModel { Title = $"通道 {i + 1} 的 RMS 值" };
            //    var lineSeriesRMS = new LineSeries
            //    {
            //        Title = $"通道 {i + 1}",
            //        Color = OxyColors.Coral // 使用固定的顏色
            //    };

            //    for (int j = 0; j < rmsValues[i].Count; j++)
            //    {
            //        lineSeriesRMS.Points.Add(new DataPoint(j, rmsValues[i][j]));
            //    }
            //    modelRMS.Series.Add(lineSeriesRMS);

            //     爲 RMS 圖設置 X 軸和 Y 軸
            //    modelRMS.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "時間" });
            //    modelRMS.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "RMS 值" });

            //    var plotViewRMS = new PlotView
            //    {
            //        Model = modelRMS,
            //        Width = 400, // 調整寬度以增加可視化空間
            //        Height = 300, // 調整高度以增加可視化空間
            //        Margin = new Thickness(10)
            //    };

            //     將 RMS 的圖加入到 RMS Panel
            //    rmsPanel.Children.Add(plotViewRMS);
            //}

            // 將主 Grid 加入到 StackPanel
            stackPanel.Children.Add(mainGrid);
        }

        private void btn_KCHECK_Click(object sender, RoutedEventArgs e)
        {
            if (KCHECK.Text == "")
            {
                CustomMessageBox _CustomMessageBox = new CustomMessageBox(10);
                _CustomMessageBox.Show();
            }
            else
            {
                k = Int32.Parse(KCHECK.Text); // 基向量數量設為 3

            }
        }
    }
}
