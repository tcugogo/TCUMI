using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using OxyPlot.Wpf;

namespace Basic_Streaming_NET.Views
{
    /// <summary>
    /// Multiple_file_Selection_Analysis.xaml 的互動邏輯
    /// </summary>
    public partial class Multiple_file_Selection_Analysis : Window
    {
        private string _patientFilePath;
        int choice;
        public Multiple_file_Selection_Analysis(string patient_name, int number)
        {
            InitializeComponent();
            choice=number;
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            _patientFilePath = Path.Combine(baseDirectory, "sensor_data", patient_name);

            // 初始化 TreeView
            LoadTreeView();
        }

        /// <summary>
        /// 加載 TreeView 的根節點
        /// </summary>
        private void LoadTreeView()
        {
            if (!Directory.Exists(_patientFilePath))
            {
                MessageBox.Show("目錄不存在: " + _patientFilePath, "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 創建根節點
            TreeViewItem rootItem = new TreeViewItem
            {
                Header = new DirectoryInfo(_patientFilePath).Name,
                Tag = _patientFilePath,
                IsExpanded = true // 根節點默認展開
            };

            // 加載第一層子項
            foreach (var dir in Directory.GetDirectories(_patientFilePath))
            {
                var subItem = CreateDirectoryNode(new DirectoryInfo(dir), 1);
                rootItem.Items.Add(subItem);
            }

            TreeView.Items.Clear();
            TreeView.Items.Add(rootItem);
        }

        /// <summary>
        /// 創建目錄節點
        /// </summary>
        private TreeViewItem CreateDirectoryNode(DirectoryInfo directoryInfo, int depth)
        {
            TreeViewItem directoryNode = new TreeViewItem
            {
                Header = directoryInfo.Name,
                Tag = directoryInfo.FullName
            };

            if (depth == 3) // 第四層 (參數目錄) 使用 CheckBox
            {
                directoryNode.Header = new CheckBox
                {
                    Content = directoryInfo.Name,
                    IsChecked = false
                };
            }

            // 遍歷子目錄
            foreach (var dir in directoryInfo.GetDirectories())
            {
                directoryNode.Items.Add(CreateDirectoryNode(dir, depth + 1));
            }

            // 僅在第四層 (depth == 3) 和更深層加載 .csv 檔案
            if (depth >= 3)
            {
                foreach (var file in directoryInfo.GetFiles("*.csv"))
                {
                    TreeViewItem fileNode = new TreeViewItem
                    {
                        Header = new CheckBox
                        {
                            Content = file.Name,
                            IsChecked = false
                        },
                        Tag = file.FullName
                    };
                    directoryNode.Items.Add(fileNode);
                }
            }

            return directoryNode;
        }

        private void ConfirmButton_Click_DiffParams(object sender, RoutedEventArgs e)
        {
            if (choice == 0)
            {
                // 用於存放分組結果
                Dictionary<string, List<string>> groupedFiles = new Dictionary<string, List<string>>();

                foreach (TreeViewItem rootItem in TreeView.Items) // 遍歷根節點
                {
                    CollectGroups(rootItem, groupedFiles);
                }

                // 用於儲存每個分組的 RMS 平均值（按通道）
                Dictionary<string, Dictionary<string, double>> groupChannelAverageRMS = new Dictionary<string, Dictionary<string, double>>();

                // 遍歷每組的檔案，處理 MARK 列並計算 RMS
                foreach (var group in groupedFiles)
                {
                    Console.WriteLine($"分組名：{group.Key}");

                    // 用於存放該分組內每個通道的 RMS 值
                    Dictionary<string, List<double>> channelRMSValues = new Dictionary<string, List<double>>();

                    foreach (var filePath in group.Value)
                    {
                        // 取得 startMark 和 endMark
                        (int startMark, int endMark) = GetMarkPositions(filePath);
                        Console.WriteLine($"  文件：{Path.GetFileName(filePath)}");
                        Console.WriteLine($"    startMark: {startMark}, endMark: {endMark}");

                        // 讀取檔案並計算 RMS
                        var lines = File.ReadAllLines(filePath);
                        var headers = lines[0].Split(',');
                        var emgLabels = headers.Take(headers.Length - 1).ToArray(); // 忽略 "MARK" 欄位

                        // 初始化通道 RMS 儲存
                        foreach (var label in emgLabels)
                        {
                            if (!channelRMSValues.ContainsKey(label))
                                channelRMSValues[label] = new List<double>();
                        }

                        // 儲存該檔案的 RMS 結果
                        foreach (var label in emgLabels)
                        {
                            // 解析每個 EMG 通道的數據
                            List<double> emgData = lines
                                .Skip(1) // 跳過標頭
                                .Select(line => line.Split(',')[Array.IndexOf(headers, label)]) // 取對應 EMG 通道的列
                                .Where(value => double.TryParse(value, out _)) // 過濾合法數字
                                .Select(double.Parse)
                                .ToList();


                            // 對 rmsData 做 z-score 標準化
                            var zScoreEmgData = CalculateZScore(emgData);

                            /// 計算該 EMG 通道的 RMS 並取 MARK 區間內的值
                            var zScoreRmsData = CalculateRMS(zScoreEmgData, 1926); // 假設取樣率為 1926

                            // 取 MARK 區間內的值
                            var segmentRMS = zScoreRmsData.Skip(startMark).Take(endMark - startMark).ToList();

                            // 計算該通道的 RMS 區間積分，並存入該通道的 RMS 清單
                            if (segmentRMS.Any())
                            {
                                double segmentArea = IntegrateSegment(segmentRMS, 1.0 / 1926); // 假設取樣率為 1926，因此時間間隔為 1/1926 秒

                                // 調整積分結果到五秒的大小
                                double actualDuration = segmentRMS.Count / 1926.0; // 實際時間長度，以秒為單位
                                double adjustedArea;

                                if (actualDuration > 5)
                                {
                                    adjustedArea = segmentArea / (actualDuration / 5); // 將面積縮小到五秒的大小
                                }
                                else if (actualDuration < 5)
                                {
                                    adjustedArea = segmentArea * (5 / actualDuration); // 將面積擴大到五秒的大小
                                }
                                else
                                {
                                    adjustedArea = segmentArea; // 如果剛好是五秒，不做任何調整
                                }

                                channelRMSValues[label].Add(adjustedArea);
                            }

                        }
                    }



                    // 計算該分組的每個通道的平均 RMS 值
                    Dictionary<string, double> channelAverages = new Dictionary<string, double>();
                    foreach (var channel in channelRMSValues)
                    {
                        if (channel.Value.Any())
                        {
                            double average = channel.Value.Average();
                            channelAverages[channel.Key] = average;
                            Console.WriteLine($"通道 {channel.Key} 平均 RMS: {average}");
                        }
                        else
                        {
                            Console.WriteLine($"通道 {channel.Key} 沒有有效的 RMS 數據。");
                        }
                    }

                    // 保存分組的通道平均 RMS 值
                    groupChannelAverageRMS[group.Key] = channelAverages;
                }

                // 繪製長條圖
                DrawGroupedBarChart(groupChannelAverageRMS);
            }
            else if (choice == 1)
            {
                // 用於存放分組結果
                Dictionary<string, List<string>> groupedFiles = new Dictionary<string, List<string>>();

                foreach (TreeViewItem rootItem in TreeView.Items) // 遍歷根節點
                {
                    CollectGroups(rootItem, groupedFiles);
                }

                // 用於儲存每個分組的 RMS 平均值（按通道）
                Dictionary<string, Dictionary<string, double>> groupChannelAverageRMS = new Dictionary<string, Dictionary<string, double>>();

                // 遍歷每組的檔案，處理 MARK 列並計算 RMS
                foreach (var group in groupedFiles)
                {
                    Console.WriteLine($"分組名：{group.Key}");

                    // 用於存放該分組內每個通道的 RMS 值
                    Dictionary<string, List<double>> channelRMSValues = new Dictionary<string, List<double>>();

                    foreach (var filePath in group.Value)
                    {
                        // 取得 startMark 和 endMark
                        (int startMark, int endMark) = GetMarkPositions(filePath);
                        Console.WriteLine($"  文件：{Path.GetFileName(filePath)}");
                        Console.WriteLine($"    startMark: {startMark}, endMark: {endMark}");

                        // 讀取檔案並計算 RMS
                        var lines = File.ReadAllLines(filePath);
                        var headers = lines[0].Split(',');
                        var emgLabels = headers.Take(headers.Length - 1).ToArray(); // 忽略 "MARK" 欄位

                        // 初始化通道 RMS 儲存
                        foreach (var label in emgLabels)
                        {
                            if (!channelRMSValues.ContainsKey(label))
                                channelRMSValues[label] = new List<double>();
                        }

                        // 儲存該檔案的 RMS 結果
                        foreach (var label in emgLabels)
                        {
                            // 解析每個 EMG 通道的數據
                            List<double> emgData = lines
                                .Skip(1) // 跳過標頭
                                .Select(line => line.Split(',')[Array.IndexOf(headers, label)]) // 取對應 EMG 通道的列
                                .Where(value => double.TryParse(value, out _)) // 過濾合法數字
                                .Select(double.Parse)
                                .ToList();


                            // 對 rmsData 做 z-score 標準化
                            var zScoreEmgData = CalculateZScore(emgData);


                            /// 計算該 EMG 通道的 RMS 並取 MARK 區間內的值
                            var zScoreRmsData = CalculateRMS(zScoreEmgData, 1926); // 假設取樣率為 1926

                            // 取 MARK 區間內的值
                            var segmentRMS = zScoreRmsData.Skip(startMark).Take(endMark - startMark).ToList();

                            // 找到 segmentRMS 中的最大值及其索引
                            int maxIndex = segmentRMS.IndexOf(segmentRMS.Max());
                            // 定義取樣率
                            int samplingRate = 1926;
                            // 計算前後 0.5 秒的資料點數
                            int halfSecondPoints = (int)(0.5 * samplingRate);
                            // 計算窗口的起始和結束索引
                            int windowStart = Math.Max(maxIndex - halfSecondPoints, 0);
                            int windowEnd = Math.Min(maxIndex + halfSecondPoints, segmentRMS.Count - 1);
                            // 取最高點前後 0.5 秒的範圍
                            var maxSegment = segmentRMS.Skip(windowStart).Take(windowEnd - windowStart + 1).ToList();


                            // 計算該通道的 RMS 區間積分，並存入該通道的 RMS 清單
                            if (maxSegment.Any())
                            {
                                //算出面積
                                double segmentArea = IntegrateSegment(maxSegment, 1.0 / 1926); // 假設取樣率為 1926，因此時間間隔為 1/1926 秒

                                // 調整積分結果到五秒的大小
                                double actualDuration = maxSegment.Count / 1926.0; // 實際時間長度，以秒為單位

                                channelRMSValues[label].Add(segmentArea);
                            }

                        }
                    }

                    // 計算該分組的每個通道的平均 RMS 值
                    Dictionary<string, double> channelAverages = new Dictionary<string, double>();
                    foreach (var channel in channelRMSValues)
                    {
                        if (channel.Value.Any())
                        {
                            double average = channel.Value.Average();
                            channelAverages[channel.Key] = average;
                            Console.WriteLine($"通道 {channel.Key} 平均 RMS: {average}");
                        }
                        else
                        {
                            Console.WriteLine($"通道 {channel.Key} 沒有有效的 RMS 數據。");
                        }
                    }

                    // 保存分組的通道平均 RMS 值
                    groupChannelAverageRMS[group.Key] = channelAverages;
                }

                // 繪製長條圖
                DrawGroupedBarChart(groupChannelAverageRMS);
            }
            
        }


        /// <summary>
        /// 對數據進行 z-score 標準化
        /// </summary>
        /// <param name="data">數據列表</param>
        /// <returns>z-score 標準化後的數據列表</returns>
        private List<double> CalculateZScore(List<double> data)
        {
            double mean = data.Average();
            double stdDev = Math.Sqrt(data.Select(x => Math.Pow(x - mean, 2)).Sum() / data.Count);

            // 如果標準差為零，返回原始數據，以避免除以零的錯誤
            if (stdDev == 0)
                return data;

            return data.Select(x => (x - mean) / stdDev).ToList();
        }


        /// <summary>
        /// 使用梯形法計算 RMS 數據的積分（面積）
        /// </summary>
        /// <param name="values">RMS 數據列表</param>
        /// <param name="timeInterval">每個資料點的時間間隔</param>
        /// <returns>積分面積</returns>
        private double IntegrateSegment(List<double> values, double timeInterval)
        {
            double area = 0.0;

            for (int i = 0; i < values.Count - 1; i++)
            {
                double height1 = values[i];
                double height2 = values[i + 1];
                area += (height1 + height2) / 2 * timeInterval; // 使用梯形法計算積分
            }

            return area;
        }


        // 計算 RMS 的函數，每 0.1 秒為一組
        static List<double> CalculateRMS(List<double> emgValues, int samplingRate)
        {
            int windowSize = (int)(0.1 * samplingRate); // 0.1 秒的資料點數
            int halfWindowSize = windowSize / 2;
            List<double> rmsList = new List<double>(new double[emgValues.Count]);

            for (int i = halfWindowSize; i < emgValues.Count - halfWindowSize; i++)
            {
                var window = emgValues.Skip(i - halfWindowSize).Take(windowSize).ToList();
                double rms = Math.Sqrt(window.Select(x => x * x).Sum() / windowSize);
                rmsList[i] = rms;
            }

            return rmsList;
        }

        /// <summary>
        /// 遞歸收集分組的檔案
        /// </summary>
        private void CollectGroups(TreeViewItem item, Dictionary<string, List<string>> groupedFiles)
        {
            if (item.Header is CheckBox checkBox && checkBox.IsChecked == true)
            {
                // 如果該節點是倒數第二層（且下一層是檔案），檢查下一層
                List<string> files = new List<string>();
                bool hasCheckedFiles = false;

                foreach (TreeViewItem subItem in item.Items)
                {
                    if (subItem.Header is CheckBox subCheckBox && subCheckBox.IsChecked == true)
                    {
                        if (subItem.Tag is string filePath && File.Exists(filePath))
                        {
                            files.Add(filePath);
                            hasCheckedFiles = true;
                        }
                    }
                }

                // 如果下一層有選中的檔案，則加入分組
                if (hasCheckedFiles)
                {
                    groupedFiles[checkBox.Content.ToString()] = files;
                }
            }

            // 遍歷子節點（避免漏掉其他結構）
            foreach (TreeViewItem subItem in item.Items)
            {
                CollectGroups(subItem, groupedFiles);
            }
        }

        /// <summary>
        /// 提取 MARK 列的 startMark 和 endMark
        /// </summary>
        private (int startMark, int endMark) GetMarkPositions(string filePath)
        {
            try
            {
                var lines = File.ReadAllLines(filePath);
                var markColumn = lines.Skip(1) // 跳過標頭
                                      .Select(line => line.Split(',').Last()) // 獲取每行的 MARK 列
                                      .ToList();

                int startMark = -1;
                int endMark = -1;

                for (int i = 0; i < markColumn.Count; i++)
                {
                    if (markColumn[i] != "0") // 找到非零值
                    {
                        if (startMark == -1)
                        {
                            startMark = i; // 設置第一個非零值為 startMark
                        }
                        else
                        {
                            endMark = i; // 設置第二個非零值為 endMark
                            break; // 找到 endMark 後退出循環
                        }
                    }
                }

                return (startMark, endMark);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"讀取文件 {filePath} 時出錯：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return (-1, -1);
            }
        }

        // 繪製分組通道的平均 RMS 值
        private void DrawGroupedBarChart(Dictionary<string, Dictionary<string, double>> groupChannelAverageRMS)
        {
            var plotModel = new PlotModel { Title = "" };

            // 設置橫軸（通道名稱）
            var categoryAxis = new CategoryAxis
            {
                Position = AxisPosition.Left,
                Title = "通道名稱",
                IsPanEnabled = false,
                IsZoomEnabled = false
            };

            // 確保所有分組的通道名稱一致，按照順序填充
            var allChannels = groupChannelAverageRMS
                .SelectMany(group => group.Value.Keys)
                .Distinct()
                .OrderBy(channel => channel)
                .ToList();
            categoryAxis.ItemsSource = allChannels;

            plotModel.Axes.Add(categoryAxis);

            // 設置縱軸（RMS 值）
            var linearAxis = new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "平均 RMS 值",
                MinimumPadding = 0,
                AbsoluteMinimum = 0
            };
            plotModel.Axes.Add(linearAxis);

            // 為每個分組創建一個 BarSeries
            foreach (var group in groupChannelAverageRMS)
            {
                var barSeries = new BarSeries
                {
                    Title = group.Key, // 分組名稱
                    LabelPlacement = LabelPlacement.Middle,
                    LabelFormatString = "{0:.00}" // 顯示小數點後兩位
                };

                foreach (var channel in allChannels)
                {
                    // 如果該分組有該通道的 RMS 值，則添加到長條圖，否則添加 0
                    double value = group.Value.ContainsKey(channel) ? group.Value[channel] : 0.0;
                    barSeries.Items.Add(new BarItem { Value = value });
                }

                plotModel.Series.Add(barSeries);
            }

            // 顯示圖例
            var legend = new Legend
            {
                LegendPosition = LegendPosition.TopCenter,
                LegendPlacement = LegendPlacement.Outside,
                LegendOrientation = LegendOrientation.Horizontal,
                LegendBorderThickness = 0
            };
            plotModel.Legends.Add(legend);

            // 更新到 PlotView
            BarChartView.Model = plotModel;
        }
    }
}
