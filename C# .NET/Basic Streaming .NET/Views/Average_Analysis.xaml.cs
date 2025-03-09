using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using OxyPlot.Wpf;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Basic_Streaming_NET.Views
{
    public partial class Average_Analysis : Window
    {
        private string Path;
        public Average_Analysis(string filePath)
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
                if (radioButton.Content.ToString() == "相同參數比較")
                {
                    // 顯示相同參數比較的確認按鈕，隱藏不同參數比較按鈕
                    ConfirmButton.Visibility = Visibility.Visible;
                    DiffParamsButton.Visibility = Visibility.Collapsed;
                }
                else
                {
                    // 顯示不同參數比較的確認按鈕，隱藏相同參數比較按鈕
                    ConfirmButton.Visibility = Visibility.Collapsed;
                    DiffParamsButton.Visibility = Visibility.Visible;
                }
            }
        }

        // 取得被勾選的 CSV 檔案
        private List<string> GetSelectedCsvFiles()
        {
            List<string> selectedFiles = new List<string>();

            // 遍歷所有的根節點，這裡的根節點是「抬腳」
            foreach (TreeViewItem rootItem in TreeView.Items)
            {
                // 遍歷「抬腳」下的子節點（例如「15頻率20速率30寬度」）
                foreach (TreeViewItem subItem in rootItem.Items)
                {
                    // 檢查子節點中的 RadioButton 或 CheckBox 是否被選中
                    if (subItem.Header is RadioButton radioButton && radioButton.IsChecked == true ||
                        subItem.Header is CheckBox checkBox && checkBox.IsChecked == true)
                    {
                        // 當找到被選中的 RadioButton 或 CheckBox，進一步檢查其子節點中的 CheckBox
                        foreach (TreeViewItem fileItem in subItem.Items)
                        {
                            if (fileItem.Header is CheckBox fileCheckBox && fileCheckBox.IsChecked == true)
                            {
                                // 檢查 CheckBox 對應的檔案是否存在
                                if (fileItem.Tag is string selectedFilePath && File.Exists(selectedFilePath))
                                {
                                    selectedFiles.Add(selectedFilePath); // 加入勾選的 CSV 檔案
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

            return selectedFiles;
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
                        double area = IntegrateRMS(rmsEmgData[label], markStart, markEnd, samplingRate);
                        double actualDuration = (markEnd - markStart) / (double)samplingRate;

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

            // 遍歷根節點（例如「抬腳」）
            foreach (TreeViewItem rootItem in TreeView.Items)
            {
                // 遍歷「抬腳」下的子節點（例如「15頻率20速率30寬度」）
                foreach (TreeViewItem subItem in rootItem.Items)
                {
                    if (subItem.Header is CheckBox parameterCheckBox && parameterCheckBox.IsChecked == true)
                    {
                        List<string> selectedCsvFiles = new List<string>();

                        // 遍歷該參數下的 CSV 檔案（CheckBox）
                        foreach (TreeViewItem fileItem in subItem.Items)
                        {
                            if (fileItem.Header is CheckBox fileCheckBox && fileCheckBox.IsChecked == true)
                            {
                                if (fileItem.Tag is string filePath && File.Exists(filePath))
                                {
                                    selectedCsvFiles.Add(filePath); // 加入勾選的 CSV 檔案
                                }
                            }
                        }

                        // 如果有 CSV 檔案被勾選，進行 RMS 面積計算並存入結果
                        if (selectedCsvFiles.Count > 0)
                        {
                            Dictionary<string, double> averageAreas = CalculateRMSAreasForFiles(selectedCsvFiles);
                            allParameterAreas.Add(averageAreas);
                            parameterNames.Add(parameterCheckBox.Content.ToString()); // 保存參數名稱
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
                Height = 400,
                Width = 600,
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

            DirectoryInfo parentDirectory = Directory.GetParent(Directory.GetParent(filePath).FullName);

            TreeViewItem rootNode = new TreeViewItem
            {
                Header = parentDirectory.Name,
                IsExpanded = true
            };

            foreach (var dir in parentDirectory.GetDirectories())
            {
                rootNode.Items.Add(CreateDirectoryNode(dir, useRadioButtonForDirectories));
            }

            TreeView.Items.Clear();
            TreeView.Items.Add(rootNode);
            SelectFirstRadioButton(rootNode); // 自動選擇第一個 RadioButton
        }

        // 追蹤是否是第一個 RadioButton
        private bool isFirstRadioButton = true;

        private TreeViewItem CreateDirectoryNode(DirectoryInfo directoryInfo, bool useRadioButtonForDirectories)
        {
            TreeViewItem directoryNode = new TreeViewItem();

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
                    IsChecked = true
                };

                directoryNode.Header = checkBox;
            }

            directoryNode.Tag = directoryInfo.FullName;

            foreach (var dir in directoryInfo.GetDirectories())
            {
                directoryNode.Items.Add(CreateDirectoryNode(dir, useRadioButtonForDirectories));
            }

            foreach (var file in directoryInfo.GetFiles("*.csv"))
            {
                TreeViewItem fileNode = new TreeViewItem
                {
                    Header = new CheckBox { Content = file.Name, IsChecked = true },
                    Tag = file.FullName
                };
                directoryNode.Items.Add(fileNode);
            }

            return directoryNode;
        }

        private void SelectFirstRadioButton(ItemsControl parentItem)
        {
            foreach (var item in parentItem.Items)
            {
                if (item is TreeViewItem treeViewItem)
                {
                    if (treeViewItem.Header is RadioButton radioButton)
                    {
                        radioButton.IsChecked = true;
                        return;
                    }
                    else
                    {
                        SelectFirstRadioButton(treeViewItem);
                    }
                }
            }
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
