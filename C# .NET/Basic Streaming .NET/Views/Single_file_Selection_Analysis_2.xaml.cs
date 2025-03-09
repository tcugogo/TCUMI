using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Series;
using OxyPlot.Wpf;

namespace Basic_Streaming_NET.Views
{
    /// <summary>
    /// Single_file_Selection_Analysis_2.xaml 的互動邏輯
    /// </summary>
    public partial class Single_file_Selection_Analysis_2 : Window
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

        // 用於存儲每個通道的 HFD 計算結果
        private List<List<double>> hfdValues;

        // 全局變量：HFD 計算窗口大小
        private int hfdWindowSize = 10; // 默認窗口大小爲 5

        public Single_file_Selection_Analysis_2(string patient_name)
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


        // 加載 CSV 文件中的標記位置到全局變量中
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

        }


        private void ConfirmButton_Click_DiffParams(object sender, RoutedEventArgs e)
        {
            // 檢查是否有選擇的 CSV 文件路徑
            if (!string.IsNullOrEmpty(SelectedCsvFilePath))
            {
                //MessageBox.Show($"已選擇的文件: {SelectedCsvFilePath}");

                // 調用函數加載 CSV 數據到全局 List 中
                LoadDataToGlobalList(SelectedCsvFilePath);

                // 加載 CSV 文件中的標記位置
                LoadMarkerPositionsFromCsv(SelectedCsvFilePath);

                // 對所有通道的數據進行正則化(Z-score)
                NormalizeAllChannelsUsingZScore();

                // 計算 1/10 秒的 RMS
                CalculateRMSForAllChannels();

                // 將 RMS 數據固定正則化成 100 個點
                NormalizeRMSDataToFixedPoints(100);

                // 計算 Markers 之間的 RMS 數據的 HFD
                CalculateHFDForMarkers();

                // 使用 Dispatcher 更新 UI，確保主線程安全
                Application.Current.Dispatcher.Invoke(() =>
                {
                    PlotHFDChartsToUI();
                });

                //MessageBox.Show("CSV 數據已成功讀取並存儲到全局變量中！");
            }
            else
            {
                MessageBox.Show("請選擇一個.csv檔文件！");
            }
        }
        private void NormalizeRMSDataToFixedPoints(int targetPoints)
        {
            for (int i = 0; i < rmsValues.Count; i++)
            {
                rmsValues[i] = NormalizeToFixedPoints(rmsValues[i], targetPoints);
            }
        }
        private List<double> NormalizeToFixedPoints(List<double> data, int targetPoints)
        {
            List<double> fixedPointData = new List<double>();

            if (data.Count == targetPoints)
            {
                fixedPointData = new List<double>(data);
            }
            else if (data.Count > targetPoints)
            {
                // 如果數據點多於目標點數，則進行下采樣
                double step = (double)data.Count / targetPoints;
                for (int i = 0; i < targetPoints; i++)
                {
                    int index = (int)(i * step);
                    fixedPointData.Add(data[index]);
                }
            }
            else
            {
                // 如果數據點少於目標點數，則進行插值
                double step = (double)(data.Count - 1) / (targetPoints - 1);
                for (int i = 0; i < targetPoints; i++)
                {
                    double position = i * step;
                    int index = (int)position;
                    double fraction = position - index;

                    if (index + 1 < data.Count)
                    {
                        double interpolatedValue = data[index] + fraction * (data[index + 1] - data[index]);
                        fixedPointData.Add(interpolatedValue);
                    }
                    else
                    {
                        fixedPointData.Add(data[index]);
                    }
                }
            }

            return fixedPointData;
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
        // 對所有通道的數據進行正則化
        private void NormalizeAllChannelsUsingZScore()
        {
            normalizedEmgData = new List<List<double>>();

            // 遍歷每個通道的數據
            foreach (var channelData in globalEmgData)
            {
                // 進行 Z-score 正則化並保存
                var normalizedData = NormalizeUsingZScore(channelData);
                normalizedEmgData.Add(normalizedData);
            }

            Console.WriteLine("所有通道的數據已完成 Z-score 正則化！");
        }

        // 計算 1/10 秒的 RMS
        private void CalculateRMSForAllChannels()
        {
            // 設置取樣頻率爲 1926 點每秒
            const int SamplingRate = 1926;

            // 設置每 1/10 秒的數據點數
            int windowSize = SamplingRate / 10;

            // 初始化全局變量 rmsValues
            rmsValues = new List<List<double>>();

            // 遍歷每個通道的數據（使用正則化後的數據）
            foreach (var channelData in normalizedEmgData)
            {
                List<double> channelRmsValues = new List<double>();

                // 分割數據並計算每個窗口的 RMS 值
                for (int i = 0; i + windowSize <= channelData.Count; i += windowSize)
                {
                    // 獲取當前窗口的數據
                    var windowData = channelData.Skip(i).Take(windowSize).ToList();

                    // 計算 RMS 值
                    double rms = CalculateRMS(windowData);
                    channelRmsValues.Add(rms);
                }

                rmsValues.Add(channelRmsValues);
            }

            Console.WriteLine("所有通道的 RMS 已完成計算！");
        }
        // 正則化數據

        // 使用 Z-score 正則化數據
        private List<double> NormalizeUsingZScore(List<double> data)
        {
            double mean = data.Average();
            double stdDev = Math.Sqrt(data.Sum(x => Math.Pow(x - mean, 2)) / data.Count);

            // 防止標準差爲零的情況
            if (stdDev == 0)
            {
                return data.Select(d => 0.0).ToList();
            }

            return data.Select(d => (d - mean) / stdDev).ToList();
        }

        // 計算 RMS 值
        private double CalculateRMS(List<double> data)
        {
            double sumOfSquares = data.Select(d => d * d).Sum();
            return Math.Sqrt(sumOfSquares / data.Count);
        }

        // 計算 Markers 之間的 RMS 數據的 HFD
        private void CalculateHFDForMarkers()
        {
            if (markerPositions == null || markerPositions.Count < 2)
            {
                Console.WriteLine("標記點不足以計算 HFD。");
                return;
            }

            hfdValues = new List<List<double>>();

            // 遍歷每兩個相鄰的標記，計算它們之間的 HFD
            for (int m = 0; m < markerPositions.Count - 1; m++)
            {
                int startMarkerIndex = markerPositions[m];
                int endMarkerIndex = markerPositions[m + 1];

                // 找到最接近 startMarkerIndex 和 endMarkerIndex 的 RMS 數據點索引
                int startIndex = (int)Math.Round((double)startMarkerIndex / (SamplingRate / 10));
                int endIndex = rmsValues[0].Count + startIndex;

                if (startIndex >= endIndex || startIndex < 0 )
                {
                    Console.WriteLine("標記點索引無效，跳過此段 HFD 計算。");
                    continue;
                }

                // 遍歷每個通道的 RMS 數據，計算 HFD
                foreach (var channelRmsValues in rmsValues)
                {
                    List<double> hfdChannelValues = new List<double>();

                    // 在每 hfdWindowSize 個 RMS 值的窗口內計算 HFD
                    for (int i = 0; i + hfdWindowSize <=100; i += hfdWindowSize)
                    {
                        var segment = channelRmsValues.Skip(i).Take(hfdWindowSize).ToList();

                        // 計算 HFD
                        double hfdValue = CalculateHFD(segment);
                        hfdChannelValues.Add(hfdValue);
                    }

                    hfdValues.Add(hfdChannelValues);
                }
            }

            Console.WriteLine("HFD 計算已完成！");
        }

        // 計算 HFD 的函數
        private double CalculateHFD(List<double> data)
        {
            if (data.Count < 2)
            {
                return 0; // 防止無效計算
            }

            //int kMax = Math.Min(10, data.Count / 2); // 最大的間隔長度不能超過數據長度的一半
            int kMax = 5; // 最大的間隔長度不能超過數據長度的一半

            List<double> Lk = new List<double>();

            for (int k = 1; k <= kMax; k++)
            {
                double lengthSum = 0;
                int numSegments = 0;

                for (int m = 0; m < k; m++)
                {
                    double length = 0;
                    int count = 0;

                    for (int j = m; j < data.Count - k; j += k)
                    {
                        // 取絕對值，確保 length 始終爲正數
                        length += Math.Abs(data[j + k] - data[j]);
                        count++;
                    }

                    if (count > 0)
                    {
                        length = (length * (data.Count - 1)) / (count * k);
                        lengthSum += length;
                        numSegments++;
                    }
                }

                if (numSegments > 0)
                {
                    // 設置最小值閾值，防止對數運算出錯
                    double minThreshold = 1e-6;

                    // 使用 Math.Max 來確保 averageLength 不小於 minThreshold
                    double averageLength = Math.Max(lengthSum / numSegments, minThreshold);

                    Lk.Add(averageLength);
                }
            }

            // 計算 Lk 的斜率，即 HFD
            if (Lk.Count > 1)
            {
                // 取 log(k) 和 log(L(k))，確保 L(k) 的值大於零
                List<double> logLk = Lk.Select(length => Math.Log(length)).ToList();
                List<double> logK = Enumerable.Range(1, logLk.Count).Select(k => Math.Log(k)).ToList();

                if (logLk.Count > 1)
                {
                    double slope = LinearRegression(logK, logLk);
                    return slope; // 返回正值的斜率，應該爲正數
                }
            }
            return 0;
        }


        // 獲取最接近的 RMS 索引
        private int GetClosestRmsIndex(int markerIndex)
        {
            // 設置取樣頻率爲 1926 點每秒，RMS 的窗口大小爲 192.6 點
            const int SamplingRate = 1926;
            const int RmsWindowSize = SamplingRate / 10;

            // 計算最接近的 RMS 索引
            return markerIndex / RmsWindowSize;
        }





        // 計算線性迴歸的斜率
        private double LinearRegression(List<double> x, List<double> y)
        {
            int n = x.Count;
            double sumX = x.Sum();
            double sumY = y.Sum();
            double sumX2 = x.Select(val => val * val).Sum();
            double sumXY = x.Zip(y, (xi, yi) => xi * yi).Sum();

            double denominator = (n * sumX2 - sumX * sumX);
            if (denominator == 0)
            {
                return 0; // 避免除以零，返回默認斜率
            }
            double slope = (n * sumXY - sumX * sumY) / denominator;
            return -slope;
        }

        // 在 UI 上繪製 HFD 結果
        private void PlotHFDChartsToUI()
        {
            // 清除之前的內容
            AnalysisStackPanel.Children.Clear();

            // 如果沒有 HFD 數據，顯示默認的文本
            if (hfdValues == null || hfdValues.Count == 0)
            {
                //AnalysisTextBlock.Text = "沒有 HFD 數據可供顯示。";
                return;
            }
            else
            {
                //AnalysisTextBlock.Text = ""; // 清空文本提示
            }

            // 設置起始時間，根據 mark[0] 來計算
            double startTime = (double)markerPositions[0] / 1926.0;

            // 先繪製所有通道的彙總圖表
            var combinedPlotModel = new OxyPlot.PlotModel
            {
                Title = "所有 Sensor 的 HFD ",
                IsLegendVisible = false // 不使用自動生成的圖例
            };

            // 添加 X 軸 (時間)，設置最小值爲 startTime，確保起始時間顯示出來
            combinedPlotModel.Axes.Add(new OxyPlot.Axes.LinearAxis
            {
                Position = OxyPlot.Axes.AxisPosition.Bottom,
                Title = "Time (s)",
                Minimum = startTime, // 設置 X 軸最小值爲起始時間
                IntervalLength = 60 // 設定合適的刻度長度，確保顯示清晰
            });

            // 添加 Y 軸 (HFD 值)，並設置 Y 軸範圍
            combinedPlotModel.Axes.Add(new OxyPlot.Axes.LinearAxis
            {
                Position = OxyPlot.Axes.AxisPosition.Left,
                Title = "Muscle complexity",
                Minimum = 0,
                Maximum = 3 // 固定 Y 軸範圍爲 0 到 3
            });

            // 將每個通道的數據彙總繪製在一張圖上
            for (int channelIndex = 0; channelIndex < hfdValues.Count; channelIndex++)
            {
                var combinedLineSeries = new OxyPlot.Series.LineSeries
                {
                    Title = $"sensor {channelIndex + 1}", // 在彙總圖中，每條線顯示其對應的通道編號
                    MarkerType = OxyPlot.MarkerType.None,
                    StrokeThickness = 2,
                    Color = OxyPlot.OxyColor.FromRgb((byte)((channelIndex * 50) % 255), (byte)((channelIndex * 80) % 255), (byte)((channelIndex * 110) % 255)) // 使用相同顏色
                };

                double timeStep = 1.0; // 假設每個 HFD 數據點間隔爲 1 秒
                List<double> channelHfdValues = hfdValues[channelIndex];
                for (int i = 0; i < channelHfdValues.Count; i++)
                {
                    double time = startTime + i * timeStep;
                    combinedLineSeries.Points.Add(new OxyPlot.DataPoint(time, channelHfdValues[i]));
                }

                combinedPlotModel.Series.Add(combinedLineSeries);
            }

            // 創建一個 PlotView 控件來顯示彙總圖表
            var combinedPlotView = new OxyPlot.Wpf.PlotView
            {
                Model = combinedPlotModel,
                Height = 400,
                Margin = new Thickness(0, 10, 0, 10)
            };

            // 在 AnalysisStackPanel 中添加一個 StackPanel 來放置彙總圖和自定義的圖例框
            var combinedPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(0, 10, 0, 10)
            };

            // 創建一個 Grid 作爲自定義圖例的容器
            var legendGrid = new Grid
            {
                Margin = new Thickness(0, 0, 0, 10)
            };

            // 創建兩列的定義
            legendGrid.ColumnDefinitions.Add(new ColumnDefinition());
            legendGrid.ColumnDefinitions.Add(new ColumnDefinition());

            // 創建兩個 StackPanel，分別放置在 Grid 的兩列中
            var leftLegendPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left
            };
            var rightLegendPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left
            };

            // 將兩個 StackPanel 分別放置到 Grid 的左右兩列中
            Grid.SetColumn(leftLegendPanel, 0);
            Grid.SetColumn(rightLegendPanel, 1);
            legendGrid.Children.Add(leftLegendPanel);
            legendGrid.Children.Add(rightLegendPanel);

            // 遍歷所有的通道數據，將每個通道的圖例分配到兩列中
            for (int channelIndex = 0; channelIndex < hfdValues.Count; channelIndex++)
            {
                // 創建一個包含顏色和通道名稱的小框
                var legendItem = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(10, 5, 10, 5)
                };

                // 創建一個矩形，表示顏色
                var colorBox = new System.Windows.Shapes.Rectangle
                {
                    Width = 20,
                    Height = 20,
                    Fill = new SolidColorBrush(Color.FromRgb((byte)((channelIndex * 50) % 255), (byte)((channelIndex * 80) % 255), (byte)((channelIndex * 110) % 255))),
                    Margin = new Thickness(5, 0, 5, 0)
                };

                // 創建一個文本塊，表示通道名稱
                var channelLabel = new TextBlock
                {
                    Text = $"sensor {channelIndex + 1}",
                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
                    FontSize = 14,
                    Margin = new Thickness(5, 0, 5, 0)
                };

                // 將顏色矩形和通道名稱添加到 legendItem 中
                legendItem.Children.Add(colorBox);
                legendItem.Children.Add(channelLabel);

                // 將圖例項添加到左側或右側的 StackPanel
                if (channelIndex % 2 == 0)
                {
                    leftLegendPanel.Children.Add(legendItem);
                }
                else
                {
                    rightLegendPanel.Children.Add(legendItem);
                }
            }

            // 將圖例面板（Grid）添加到彙總面板中
            combinedPanel.Children.Add(legendGrid);

            // 將彙總圖表 PlotView 添加到彙總面板中
            combinedPanel.Children.Add(combinedPlotView);

            // 最後將彙總面板添加到 AnalysisStackPanel 中
            AnalysisStackPanel.Children.Add(combinedPanel);

            // 然後繪製每個通道的獨立 HFD 圖表
            for (int channelIndex = 0; channelIndex < hfdValues.Count; channelIndex++)
            {
                // 創建一個 PlotModel 來保存圖表的基本信息
                var plotModel = new OxyPlot.PlotModel
                {
                    Title = $" Sensor {channelIndex + 1} 的 HFD 結果",
                    IsLegendVisible = true
                };

                // 添加 X 軸 (時間)，設置最小值爲 startTime，確保起始時間顯示出來
                plotModel.Axes.Add(new OxyPlot.Axes.LinearAxis
                {
                    Position = OxyPlot.Axes.AxisPosition.Bottom,
                    Title = "Time (s)",
                    Minimum = startTime, // 設置 X 軸最小值爲起始時間
                    IntervalLength = 60 // 設定合適的刻度長度，確保顯示清晰
                });

                // 添加 Y 軸 (HFD 值)，並設置 Y 軸範圍
                plotModel.Axes.Add(new OxyPlot.Axes.LinearAxis
                {
                    Position = OxyPlot.Axes.AxisPosition.Left,
                    Title = "Muscle complexity",
                    Minimum = 0,
                    Maximum = 3 // 固定 Y 軸範圍爲 0 到 3
                });

                // 創建並添加數據點
                var lineSeries = new OxyPlot.Series.LineSeries
                {
                    Title = $"Sensor {channelIndex + 1}",
                    MarkerType = OxyPlot.MarkerType.Circle,
                    MarkerSize = 3,
                    StrokeThickness = 1,
                    Color = OxyPlot.OxyColor.FromRgb((byte)((channelIndex * 50) % 255), (byte)((channelIndex * 80) % 255), (byte)((channelIndex * 110) % 255)) // 每條線不同的顏色
                };

                // 使用標記之間的時間進行繪製
                double timeStep = 1.0; // 假設每個 HFD 數據點間隔爲 1 秒
                List<double> channelHfdValues = hfdValues[channelIndex];
                for (int i = 0; i < channelHfdValues.Count; i++)
                {
                    double time = startTime + i * timeStep;
                    lineSeries.Points.Add(new OxyPlot.DataPoint(time, channelHfdValues[i]));
                }

                plotModel.Series.Add(lineSeries);

                // 創建一個 PlotView 控件來顯示圖表
                var plotView = new OxyPlot.Wpf.PlotView
                {
                    Model = plotModel,
                    Height = 400,
                    Margin = new Thickness(0, 10, 0, 10)
                };

                // 將 PlotView 添加到 AnalysisStackPanel 中
                AnalysisStackPanel.Children.Add(plotView);
            }
        }



    }
}
