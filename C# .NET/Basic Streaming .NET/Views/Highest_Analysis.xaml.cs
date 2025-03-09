using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using OxyPlot.Wpf;
using OxyPlot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Basic_Streaming_NET.Views
{
    /// <summary>
    /// Highest_Analysis.xaml 的互動邏輯
    /// </summary>
    public partial class Highest_Analysis : Window
    {
        private string Path;
        public Highest_Analysis(string filePath)
        {
            InitializeComponent();
            Path = filePath;
            LoadTreeView(filePath, true); // 初始加載，假設選擇了相同參數比較
        }
        private string currentFilePath;

        // 當 RadioButton 被點擊時，重新加載 TreeView 並顯示確認按鈕
        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radioButton)
            {
                // 根據選擇的 RadioButton 加載 TreeView
                LoadTreeView(currentFilePath, radioButton.Content.ToString() == "相同參數比較");

                TreeView.Visibility = Visibility.Visible;

                // 顯示對應的按鈕
                if (radioButton.Content.ToString() == "不同參數比較")
                {
                    // 顯示不同參數比較的確認按鈕
                    DiffParamsButton.Visibility = Visibility.Visible;
                }
                else
                {
                    // 隱藏不同參數比較按鈕
                    DiffParamsButton.Visibility = Visibility.Collapsed;
                }
            }
        }

        // 取得被勾選的 CSV 檔案
        private List<string> GetSelectedCsvFiles()
        {
            List<string> selectedFiles = new List<string>();

            // 第一層（例如 "林睿傑_54888123"）
            foreach (TreeViewItem rootItem in TreeView.Items)
            {
                // 第二層（例如 "2024_9_19"）
                foreach (TreeViewItem yearItem in rootItem.Items)
                {
                    // 第三層（例如 "other" 和 "抬腳"）
                    foreach (TreeViewItem categoryItem in yearItem.Items)
                    {
                        // 第四層（例如 "15頻率20速率30寬度"）
                        foreach (TreeViewItem parameterItem in categoryItem.Items)
                        {
                            // 確認第四層的 CheckBox 被勾選
                            if (parameterItem.Header is CheckBox parameterCheckBox && parameterCheckBox.IsChecked == true)
                            {
                                // 遍歷第五層（CSV 檔案）
                                foreach (TreeViewItem fileItem in parameterItem.Items)
                                {
                                    // 確認第五層的 CheckBox 被勾選並且檔案存在
                                    if (fileItem.Header is CheckBox fileCheckBox && fileCheckBox.IsChecked == true)
                                    {
                                        if (fileItem.Tag is string filePath && File.Exists(filePath))
                                        {
                                            selectedFiles.Add(filePath); // 加入勾選的 CSV 檔案
                                        }
                                        else
                                        {
                                            Console.WriteLine($"無法讀取檔案路徑，請檢查 Tag 設定是否正確：{fileItem.Tag}");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Debug: 列出找到的檔案
            Console.WriteLine("符合條件的勾選檔案:");
            foreach (var file in selectedFiles)
            {
                Console.WriteLine(file);
            }

            return selectedFiles;
        }

        private void CollectSelectedFilesForCalculation(TreeViewItem item, List<string> selectedFiles)
        {
            // 確認當前節點是第四層且已被勾選
            if (item.Header is CheckBox fourthLevelCheckBox && fourthLevelCheckBox.IsChecked == true)
            {
                bool hasSelectedCsv = false;

                // 遍歷第五層的 CSV 文件檢查
                foreach (var subItem in item.Items)
                {
                    if (subItem is TreeViewItem fileItem && fileItem.Header is CheckBox fileCheckBox && fileCheckBox.IsChecked == true)
                    {
                        if (fileItem.Tag is string filePath && File.Exists(filePath))
                        {
                            selectedFiles.Add(filePath);
                            hasSelectedCsv = true;
                        }
                        else if (fileItem.Tag != null)
                        {
                            Console.WriteLine($"無效的檔案路徑：{fileItem.Tag}");
                        }
                    }
                }

                // 若該第四層節點沒有任何選中的 CSV 文件則不計算
                if (!hasSelectedCsv)
                {
                    Console.WriteLine($"第四層節點「{fourthLevelCheckBox.Content}」沒有選中的 CSV 文件，將跳過計算。");
                }
            }
            else
            {
                // 遞迴檢查其他層級的子項目
                foreach (var subItem in item.Items)
                {
                    if (subItem is TreeViewItem subTreeViewItem)
                    {
                        CollectSelectedFilesForCalculation(subTreeViewItem, selectedFiles);
                    }
                }
            }
        }

        // 確認檔案按鈕的點擊事件 - 相同參數比較
        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            // 取得所有勾選的 .csv 檔案
            List<string> selectedCsvFiles = GetSelectedCsvFiles();

            // Debug: 列印勾選的 CSV 檔案名稱來確認
            Console.WriteLine("選中的CSV檔案:");
            foreach (var file in selectedCsvFiles)
            {
                Console.WriteLine(file);
            }

            // 確保有勾選的 CSV 檔案
            if (selectedCsvFiles.Count == 0)
            {
                MessageBox.Show("請選擇至少一個 CSV 檔案進行分析！");
                return;
            }

            ConcurrentDictionary<string, List<double>> totalAreas = new ConcurrentDictionary<string, List<double>>();
            int samplingRate = 1920; // 每秒的資料點數

            Parallel.ForEach(selectedCsvFiles, csvFilePath =>
            {
                try
                {
                    var lines = File.ReadAllLines(csvFilePath);  // 讀取 CSV 檔案
                    var headers = lines[0].Split(',');           // 解析標頭
                    var emgLabels = headers.Take(headers.Length - 1).ToArray(); // 忽略最後的 "MARK"
                    (int markStart, int markEnd) = FindMarkStartAndEnd(lines);  // 找到 MARK 的開始和結束

                    Dictionary<string, List<double>> emgData = new Dictionary<string, List<double>>();
                    foreach (var label in emgLabels)
                    {
                        emgData[label] = new List<double>();
                    }

                    // 讀取每行數據
                    for (int i = 1; i < lines.Length; i++)
                    {
                        var values = lines[i].Split(',');
                        for (int j = 0; j < emgLabels.Length; j++)
                        {
                            if (double.TryParse(values[j], out double emgValue))
                            {
                                emgData[emgLabels[j]].Add(emgValue);
                            }
                        }
                    }

                    // 計算每個 EMG 的 RMS
                    Dictionary<string, List<double>> rmsEmgData = new Dictionary<string, List<double>>();
                    foreach (var label in emgLabels)
                    {
                        rmsEmgData[label] = CalculateRMS(emgData[label], samplingRate);
                    }

                    // 計算面積
                    foreach (var label in rmsEmgData.Keys)
                    {
                        // 找到 markStart 和 markEnd 之間的最大值及其索引
                        var rmsSegment = rmsEmgData[label].Skip(markStart).Take(markEnd - markStart).ToList();
                        double maxRmsValue = rmsSegment.Max();
                        int maxIndexWithinSegment = rmsSegment.IndexOf(maxRmsValue);

                        // 計算最大值在整個 RMS 數據中的索引
                        int maxIndex = markStart + maxIndexWithinSegment;

                        // 計算最大值前後 0.5 秒的範圍 (window)
                        int windowSize = (int)(0.5 * samplingRate); // 0.5 秒的資料點數

                        // 確保不超出範圍
                        int windowStart = Math.Max(maxIndex - windowSize / 2, 0);
                        int windowEnd = Math.Min(maxIndex + windowSize / 2, rmsEmgData[label].Count);

                        // 使用窗口內的 RMS 值計算積分 (面積)
                        double area = 0.0;
                        double timeInterval = 1.0 / samplingRate; // 每個資料點的時間間隔

                        for (int i = windowStart; i < windowEnd - 1; i++)
                        {
                            double height1 = rmsEmgData[label][i];
                            double height2 = rmsEmgData[label][i + 1];
                            area += (height1 + height2) / 2 * timeInterval;
                        }

                        // 調整面積
                        double actualDuration = (windowEnd - windowStart) / (double)samplingRate;

                        if (actualDuration > 0)
                        {
                            double adjustedArea = area * (5 / actualDuration);  // 調整區間為5秒

                            totalAreas.AddOrUpdate(label, new List<double> { adjustedArea }, (key, existingList) =>
                            {
                                existingList.Add(adjustedArea);
                                return existingList;
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"讀取檔案 {csvFilePath} 時出錯: {ex.Message}");
                }
            });

            // 計算平均面積
            Dictionary<string, double> averageAreas = new Dictionary<string, double>();
            foreach (var label in totalAreas.Keys)
            {
                averageAreas[label] = totalAreas[label].Average();
            }

            // 繪製平均面積的長條圖
            DrawBarChart(averageAreas);
        }

        // 確認檔案按鈕的點擊事件 - 不同參數比較
        private void ConfirmButton_Click_DiffParams(object sender, RoutedEventArgs e)
        {
            // 清空右邊區域中的所有圖表視圖
            PlotContainer.Children.Clear();

            List<Dictionary<string, double>> allParameterAreas = new List<Dictionary<string, double>>();
            List<string> parameterNames = new List<string>();
            int samplingRate = 1920; // 每秒的資料點數

            // 第一層 (例如 "林睿傑_54888123")
            foreach (TreeViewItem rootItem in TreeView.Items)
            {
                // 第二層 (例如 "2024_9_19")
                foreach (TreeViewItem yearItem in rootItem.Items)
                {
                    // 第三層 (例如 "other" 和 "抬腳")
                    foreach (TreeViewItem categoryItem in yearItem.Items)
                    {
                        // 第四層 (例如 "15頻率20速率30寬度")
                        foreach (TreeViewItem parameterItem in categoryItem.Items)
                        {
                            // 檢查第四層的 CheckBox 是否被勾選
                            if (parameterItem.Header is CheckBox parameterCheckBox && parameterCheckBox.IsChecked == true)
                            {
                                List<string> selectedCsvFiles = new List<string>();

                                // 第五層 (CSV 檔案)
                                foreach (TreeViewItem fileItem in parameterItem.Items)
                                {
                                    // 檢查第五層的 CheckBox 是否被勾選並且檔案是否存在
                                    if (fileItem.Header is CheckBox fileCheckBox && fileCheckBox.IsChecked == true)
                                    {
                                        if (fileItem.Tag is string filePath && File.Exists(filePath))
                                        {
                                            selectedCsvFiles.Add(filePath);
                                        }
                                    }
                                }

                                // 如果有符合條件的 CSV 文件，則計算 RMS 面積並添加到結果中
                                if (selectedCsvFiles.Count > 0)
                                {
                                    Dictionary<string, double> averageAreas = CalculateRMSAreasForFiles(selectedCsvFiles);
                                    allParameterAreas.Add(averageAreas);
                                    parameterNames.Add(parameterCheckBox.Content.ToString()); // 保存參數名稱
                                }
                            }
                        }
                    }
                }
            }

            // 繪製所有參數的長條圖
            DrawComparisonBarChart(allParameterAreas, parameterNames);
        }


        // 繪製長條圖
        private void DrawBarChart(Dictionary<string, double> averageAreas)
        {
            // 按照 EMG1, EMG2, ..., EMG7 的順序進行排序，並確保 EMG1 在最上方
            var orderedKeys = averageAreas.Keys.OrderBy(key => int.Parse(key.Replace("EMG", ""))).ToList();

            var plotModel = new PlotModel { Title = "選擇檔案的平均 RMS 面積" };

            var barSeries = new BarSeries
            {
                LabelPlacement = LabelPlacement.Middle,
                LabelFormatString = "{0:.00}",
                BarWidth = 0.6
            };

            foreach (var key in orderedKeys)
            {
                barSeries.Items.Add(new BarItem { Value = averageAreas[key] });
            }

            plotModel.Series.Add(barSeries);

            plotModel.Axes.Add(new CategoryAxis
            {
                Position = AxisPosition.Left,
                ItemsSource = orderedKeys,  // 使用排序後的 Keys
                Title = "EMG Channels"
            });

            plotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Area (RMS)"
            });

            BarChartView.Model = plotModel; // 將圖表顯示在 PlotView 中
        }

        // 為所有參數繪製比較的長條圖
        private void DrawComparisonBarChart(List<Dictionary<string, double>> allParameterAreas, List<string> parameterNames)
        {
            var plotModel = new PlotModel { Title = "不同參數的平均 RMS 面積比較" };

            for (int i = 0; i < allParameterAreas.Count; i++)
            {
                var barSeries = new BarSeries
                {
                    Title = parameterNames[i],
                    LabelPlacement = LabelPlacement.Middle,
                    LabelFormatString = "{0:.00}",
                    BarWidth = 0.6
                };

                foreach (var area in allParameterAreas[i])
                {
                    barSeries.Items.Add(new BarItem { Value = area.Value });
                }

                plotModel.Series.Add(barSeries);
            }

            plotModel.Axes.Add(new CategoryAxis
            {
                Position = AxisPosition.Left,
                ItemsSource = allParameterAreas.FirstOrDefault()?.Keys.ToList() ?? new List<string>(),
                Title = "EMG Channels"
            });

            plotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Area (RMS)"
            });

            // 修改部分：設置圖例可見並配置圖例位置
            plotModel.IsLegendVisible = true;  // 顯示圖例

            var legend = new Legend
            {
                LegendPlacement = LegendPlacement.Outside, // 圖例放置在繪圖區域外部
                LegendPosition = LegendPosition.TopRight,  // 圖例位置在右上角
                LegendOrientation = LegendOrientation.Horizontal,  // 圖例方向為橫向
                LegendBorderThickness = 1
            };

            // 將圖例添加到模型中
            plotModel.Legends.Add(legend);

            // 創建新的 PlotView 來顯示圖表
            PlotView plotView = new PlotView
            {
                Model = plotModel,
                Height = 500,
                Width = 650,
                Margin = new Thickness(10)
            };

            // 將圖表添加到視圖容器中
            PlotContainer.Children.Add(plotView);
        }

        // 計算多個 CSV 檔案的 RMS 面積（每個 EMG 通道單獨計算）
        private Dictionary<string, double> CalculateRMSAreasForFiles(List<string> csvFiles)
        {
            int samplingRate = 1920; // 每秒的資料點數
            ConcurrentDictionary<string, List<double>> totalAreas = new ConcurrentDictionary<string, List<double>>();

            Parallel.ForEach(csvFiles, csvFilePath =>
            {
                try
                {
                    var lines = File.ReadAllLines(csvFilePath);
                    var headers = lines[0].Split(',');
                    var emgLabels = headers.Take(headers.Length - 1).ToArray(); // 忽略最後的 "MARK"
                    (int markStart, int markEnd) = FindMarkStartAndEnd(lines);

                    Dictionary<string, List<double>> emgData = new Dictionary<string, List<double>>();
                    foreach (var label in emgLabels)
                    {
                        emgData[label] = new List<double>();
                    }

                    for (int i = 1; i < lines.Length; i++)
                    {
                        var values = lines[i].Split(',');
                        for (int j = 0; j < emgLabels.Length; j++)
                        {
                            if (double.TryParse(values[j], out double emgValue))
                            {
                                emgData[emgLabels[j]].Add(emgValue);
                            }
                        }
                    }

                    // 計算 RMS 和麪積
                    Dictionary<string, List<double>> rmsEmgData = new Dictionary<string, List<double>>();
                    foreach (var label in emgLabels)
                    {
                        rmsEmgData[label] = CalculateRMS(emgData[label], samplingRate);
                    }

                    foreach (var label in rmsEmgData.Keys)
                    {
                        double area = IntegrateRMS(rmsEmgData[label], markStart, markEnd, samplingRate);
                        double actualDuration = (markEnd - markStart) / (double)samplingRate;

                        if (actualDuration > 0)
                        {
                            double adjustedArea = area * (5 / actualDuration); // 調整區間為5秒

                            totalAreas.AddOrUpdate(label, new List<double> { adjustedArea }, (key, existingList) =>
                            {
                                existingList.Add(adjustedArea);
                                return existingList;
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"讀取檔案 {csvFilePath} 時出錯: {ex.Message}");
                }
            });

            // 計算每個通道的平均面積
            Dictionary<string, double> averageAreas = new Dictionary<string, double>();
            foreach (var label in totalAreas.Keys)
            {
                averageAreas[label] = totalAreas[label].Average();
            }

            return averageAreas;
        }

        // 計算 RMS 的函數，每 0.5 秒為一組，不完整窗口補 0
        static List<double> CalculateRMS(List<double> emgValues, int samplingRate)
        {
            int windowSize = (int)(0.5 * samplingRate); // 每 0.5 秒的資料點數，窗口大小為960
            int halfWindowSize = windowSize / 2; // 0.25秒為 480
            List<double> rmsList = new List<double>(new double[emgValues.Count]); // 預設為0

            // 遍歷資料，只有當 i 超過 halfWindowSize 且距離結束資料超過 halfWindowSize 時才做 RMS 計算
            for (int i = halfWindowSize; i < emgValues.Count - halfWindowSize; i++)
            {
                var window = emgValues.Skip(i - halfWindowSize).Take(windowSize).ToList();
                double rms = Math.Sqrt(window.Select(v => v * v).Sum() / windowSize);
                rmsList[i] = rms;
            }

            return rmsList;
        }

        // 計算區間內的積分（面積）
        static double IntegrateRMS(List<double> rmsValues, int markStart, int markEnd, int samplingRate)
        {
            double area = 0.0;
            double timeInterval = 1.0 / samplingRate; // 每個資料點的時間間隔

            // 使用梯形法計算積分
            for (int i = markStart; i < markEnd - 1; i++)
            {
                double height1 = rmsValues[i];
                double height2 = rmsValues[i + 1];
                area += (height1 + height2) / 2 * timeInterval;
            }

            return area;
        }

        // 動態加載 TreeView
        private void LoadTreeView(string filePath, bool useRadioButtonForDirectories)
        {
            currentFilePath = filePath;

            // 向上找到三層
            DirectoryInfo upperLevelDirectory = Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(filePath).FullName).FullName).FullName);

            // 創建根節點（可展開，但不可選）
            TreeViewItem rootNode = new TreeViewItem
            {
                Header = upperLevelDirectory.Name,
                IsExpanded = true
            };

            // 加入第一層子資料夾（可展開，但不可選）
            foreach (var dir in upperLevelDirectory.GetDirectories())
            {
                TreeViewItem subNode1 = new TreeViewItem
                {
                    Header = dir.Name,
                    IsExpanded = true
                };

                // 加入第二層子資料夾（可展開，但不可選）
                foreach (var subDir in dir.GetDirectories())
                {
                    TreeViewItem subNode2 = new TreeViewItem
                    {
                        Header = subDir.Name,
                        IsExpanded = true
                    };

                    // 加入第三層及以下的資料夾和文件
                    foreach (var subSubDir in subDir.GetDirectories())
                    {
                        subNode2.Items.Add(CreateDirectoryNode(subSubDir, useRadioButtonForDirectories, 3));
                    }

                    subNode1.Items.Add(subNode2);
                }

                rootNode.Items.Add(subNode1);
            }

            TreeView.Items.Clear();
            TreeView.Items.Add(rootNode);
        }

        // 追蹤是否是第一個 RadioButton
        private bool isFirstRadioButton = true;

        private TreeViewItem CreateDirectoryNode(DirectoryInfo directoryInfo, bool useRadioButtonForDirectories, int depth)
        {
            TreeViewItem directoryNode = new TreeViewItem();

            // 只有在第四層（depth == 3）及更深層時，才顯示 CheckBox 或 RadioButton
            if (depth >= 3)
            {
                if (useRadioButtonForDirectories)
                {
                    var radioButton = new RadioButton
                    {
                        Content = directoryInfo.Name,
                        GroupName = "DirectoryGroup",
                        IsChecked = isFirstRadioButton // 設置第一個 RadioButton 為選中狀態
                    };

                    directoryNode.Header = radioButton;

                    if (isFirstRadioButton)
                    {
                        isFirstRadioButton = false;
                    }
                }
                else
                {
                    var checkBox = new CheckBox
                    {
                        Content = directoryInfo.Name,
                        IsChecked = false
                    };

                    directoryNode.Header = checkBox;
                }
            }
            else
            {
                // 在前三層顯示普通標題，不添加 CheckBox 或 RadioButton
                directoryNode.Header = directoryInfo.Name;
            }

            directoryNode.Tag = directoryInfo.FullName;

            // 遞歸處理子目錄
            foreach (var dir in directoryInfo.GetDirectories())
            {
                directoryNode.Items.Add(CreateDirectoryNode(dir, useRadioButtonForDirectories, depth + 1));
            }

            // 只在第四層（depth >= 3）及更深處添加文件的 CheckBox
            if (depth >= 3)
            {
                foreach (var file in directoryInfo.GetFiles("*.csv"))
                {
                    TreeViewItem fileNode = new TreeViewItem
                    {
                        Header = new CheckBox { Content = file.Name, IsChecked = false },
                        Tag = file.FullName
                    };
                    directoryNode.Items.Add(fileNode);
                }
            }

            return directoryNode;
        }

        static (int, int) FindMarkStartAndEnd(string[] lines)
        {
            var markColumn = lines.Skip(1).Select(line => line.Split(',').Last()).ToList();

            int markStart = -1;
            int markEnd = -1;
            for (int i = 0; i < markColumn.Count; i++)
            {
                if (markColumn[i] != "0")
                {
                    if (markStart == -1)
                    {
                        markStart = i;
                    }
                    else
                    {
                        markEnd = i;
                        break;
                    }
                }
            }

            return (markStart, markEnd);
        }
    }
}
