using Basic_Streaming.NET;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using OxyPlot;
using OxyPlot.Wpf;
using OxyPlot.Series;
using OxyPlot.Axes;
using System.Windows.Threading;
using OxyPlot.Annotations;
using System.Windows.Input;
using Windows.Graphics.Display;

namespace Basic_Streaming_NET.Views
{
    public partial class LookFile_Control : UserControl
    {
        MainWindow _mainWindow;
        string _patientFilePath;
        string _patientNameTxt;
        private FileItem _selectedCsvFileItem;

        private List<PlotModel> plotModels;
        private const double SamplingRate = 1926;
        private int maxDataPoints = 19260;
        private bool isDraggingMark = false;
        private LineAnnotation draggingMark;
        private double initialMarkX;

        private bool isDraggingSlider = false;
        private double sliderNewValue = 0;
        private DispatcherTimer sliderUpdateTimer;

        // 保存EMG數據的總數量
        private double emgDataCount;

        // 用於保存所有頻道的標記位置
        private List<List<LineAnnotation>> markAnnotations;
        private List<double> originalMarkPositions;
        private List<double> updatedMarkPositions;
        private List<double> currentMarkPositions;

        private bool isMarkUpdateMode = true;
        private bool markUpdated = false;

        public LookFile_Control(MainWindow mainWindowPanel, string patient_name)
        {
            InitializeComponent();
            _mainWindow = mainWindowPanel;
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

            _patientFilePath = Path.Combine(baseDirectory, "sensor_data", patient_name);
            _patientNameTxt = patient_name + "個人資料.txt";

            var rootItem = LoadDirectoryStructure(_patientFilePath);
            FileTreeView.Items.Add(rootItem);

            sliderUpdateTimer = new DispatcherTimer();
            sliderUpdateTimer.Interval = TimeSpan.FromMilliseconds(50);
            sliderUpdateTimer.Tick += SliderUpdateTimer_Tick;

            updatedMarkPositions = new List<double>();
            currentMarkPositions = new List<double>();
        }

        //Y放大
        private void ZoomInY_Click(object sender, RoutedEventArgs e)
        {
            foreach (var plotModel in plotModels)
            {
                var yAxis = plotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Left);
                if (yAxis != null)
                {
                    double range = yAxis.ActualMaximum - yAxis.ActualMinimum;
                    double newMin = yAxis.ActualMinimum + range * 0.1;
                    double newMax = yAxis.ActualMaximum - range * 0.1;
                    yAxis.Minimum = newMin;
                    yAxis.Maximum = newMax;
                    plotModel.InvalidatePlot(false);
                }
            }
        }

        //Y縮小
        private void ZoomOutY_Click(object sender, RoutedEventArgs e)
        {
            foreach (var plotModel in plotModels)
            {
                var yAxis = plotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Left);
                if (yAxis != null)
                {
                    double range = yAxis.ActualMaximum - yAxis.ActualMinimum;
                    double newMin = yAxis.ActualMinimum - range * 0.1;
                    double newMax = yAxis.ActualMaximum + range * 0.1;
                    yAxis.Minimum = newMin;
                    yAxis.Maximum = newMax;
                    plotModel.InvalidatePlot(false);
                }
            }
        }





        




        private DirectoryItem LoadDirectoryStructure(string directoryPath)
        {
            var directoryItem = new DirectoryItem { Name = Path.GetFileName(directoryPath) };

            // 加載子目錄
            foreach (var directory in Directory.GetDirectories(directoryPath))
            {
                directoryItem.Children.Add(LoadDirectoryStructure(directory));
            }

            // 加載檔案，排除特定檔案
            foreach (var file in Directory.GetFiles(directoryPath))
            {
                if (Path.GetFileName(file) != _patientNameTxt)
                {
                    directoryItem.Children.Add(new FileItem { Name = Path.GetFileName(file), Path = file });
                }
            }

            return directoryItem;
        }

        private void FileTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is FileItem fileItem)
            {
                if (fileItem.Path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        // 讀取檔案內容並顯示於 TextBox，並設置可見性
                        string content = File.ReadAllText(fileItem.Path);
                        FileContentTextBox.Text = content;
                        FileContentTextBox.Visibility = Visibility.Visible;

                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("無法讀取檔案內容: " + ex.Message, "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    GetCsvPathButton.Visibility = Visibility.Hidden;
                }
                else if (fileItem.Path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    // 當選擇 .csv 檔案時，顯示按鈕並儲存選中的檔案
                    _selectedCsvFileItem = fileItem;
                    GetCsvPathButton.Visibility = Visibility.Visible;
                    FileContentTextBox.Visibility = Visibility.Collapsed;
                }
                else
                {
                    // 清除選擇的檔案並隱藏按鈕
                    _selectedCsvFileItem = null;
                    GetCsvPathButton.Visibility = Visibility.Hidden;
                    FileContentTextBox.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void GetCsvPathButton_Click(object sender, RoutedEventArgs e)
        {
            
            if (_selectedCsvFileItem != null)
            {
                // 找到 "sensor_data" 這部分的位置
                int startIndex = _selectedCsvFileItem.Path.IndexOf("sensor_data");
                if (startIndex != -1)
                {
                    // 獲取 "sensor_data" 之後的子路徑
                    string desiredPath = _selectedCsvFileItem.Path.Substring(startIndex + "sensor_data".Length + 1);
                    GetCsvPath_text.Text = desiredPath;
                }

                // 讀取並顯示選定的 .csv 檔案資料
                LoadDataFromCsv(_selectedCsvFileItem.Path);

                btn_ZoomInY.Visibility = Visibility.Visible;
                btn_ZoomOutY.Visibility = Visibility.Visible;

                btn_ZoomInX.Visibility = Visibility.Visible;
                btn_ZoomOutX.Visibility = Visibility.Visible;

                btn_Update_mark.Visibility = Visibility.Visible;
                //btn_Add_mark.Visibility = Visibility.Visible;
                //btn_Delete_mark.Visibility = Visibility.Visible;

                XAxisSlider.Visibility = Visibility.Visible;

                btn_Cancel_mark.Visibility = Visibility.Hidden;
            }
        }

        private void LoadDataFromCsv(string filePath)
        {
            var lines = File.ReadAllLines(filePath);
            int emgCount = lines[0].Split(',').Length - 1;
            emgDataCount = lines.Length - 1;
            plotModels = new List<PlotModel>();
            markAnnotations = new List<List<LineAnnotation>>();
            originalMarkPositions = new List<double>();
            updatedMarkPositions = new List<double>();

            var emgData = new List<LineSeries>[emgCount];
            for (int i = 0; i < emgCount; i++)
            {
                emgData[i] = new List<LineSeries>
                {
                    new LineSeries
                    {
                        Title = $"EMG {i + 1}",
                        Color = OxyColors.Black,
                        StrokeThickness = 1
                    }
                };
            }

            var markPositions = new List<double>();

            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                var parts = line.Split(',');

                for (int j = 0; j < emgCount; j++)
                {
                    if (double.TryParse(parts[j], out double emgValue))
                    {
                        emgData[j][0].Points.Add(new DataPoint(i / SamplingRate, emgValue));
                    }
                }

                if (parts.Length > emgCount && double.TryParse(parts[emgCount], out double markValue) && markValue != 0)
                {
                    double markTime = i / SamplingRate;
                    markPositions.Add(markTime);
                    originalMarkPositions.Add(markTime);
                }
            }

            for (int i = 0; i < emgCount; i++)
            {
                var plotModel = new PlotModel { Title = $"Sensor {i + 1}" };
                plotModel.Series.Add(emgData[i][0]);

                var xAxis = new LinearAxis
                {
                    Position = AxisPosition.Bottom,
                    Minimum = 0,
                    Maximum = maxDataPoints / SamplingRate,
                    IsZoomEnabled = false,
                    IsPanEnabled = false
                };
                plotModel.Axes.Add(xAxis);

                var yAxis = new LinearAxis
                {
                    Position = AxisPosition.Left,
                    Minimum = -5.5,
                    Maximum = 5.5,
                    MajorGridlineStyle = LineStyle.Solid,
                    MinorGridlineStyle = LineStyle.Dot
                };
                plotModel.Axes.Add(yAxis);

                var channelMarks = new List<LineAnnotation>();
                foreach (var markTime in markPositions)
                {
                    var lineAnnotation = new LineAnnotation
                    {
                        Type = LineAnnotationType.Vertical,
                        X = markTime,
                        Color = OxyColors.Red,
                        LineStyle = LineStyle.Dash,
                        Text = "MARK",
                        TextVerticalAlignment = OxyPlot.VerticalAlignment.Bottom
                    };

                    // 加入事件監聽
                    lineAnnotation.MouseDown += Mark_MouseDown;
                    lineAnnotation.MouseMove += Mark_MouseMove;
                    lineAnnotation.MouseUp += Mark_MouseUp;

                    plotModel.Annotations.Add(lineAnnotation);
                    channelMarks.Add(lineAnnotation);
                }

                plotModels.Add(plotModel);
                markAnnotations.Add(channelMarks);
            }

            var markPlotModel = new PlotModel { Title = "MARK Channel" };

            var markChannelMarks = new List<LineAnnotation>();
            foreach (var markTime in markPositions)
            {
                var markLine = new LineAnnotation
                {
                    Type = LineAnnotationType.Vertical,
                    X = markTime,
                    Color = OxyColors.Red,
                    LineStyle = LineStyle.Dash,
                    Text = "MARK",
                    TextVerticalAlignment = OxyPlot.VerticalAlignment.Bottom
                };

                markLine.MouseDown += Mark_MouseDown;
                markLine.MouseMove += Mark_MouseMove;
                markLine.MouseUp += Mark_MouseUp;

                markPlotModel.Annotations.Add(markLine);
                markChannelMarks.Add(markLine);
            }

            var markXAxis = new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Minimum = 0,
                Maximum = maxDataPoints / SamplingRate,
                IsZoomEnabled = false,
                IsPanEnabled = false
            };
            markPlotModel.Axes.Add(markXAxis);

            var markYAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                Minimum = -1,
                Maximum = 1,
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot
            };
            markPlotModel.Axes.Add(markYAxis);

            plotModels.Add(markPlotModel);
            markAnnotations.Add(markChannelMarks);

            PlotsContainer.ItemsSource = plotModels;
            XAxisSlider.Maximum = (lines.Length - maxDataPoints) / SamplingRate;
            XAxisSlider.Value = 0;
        }

        // 滑鼠按下事件，用於檢測是否點擊到標記線
        private void Mark_MouseDown(object sender, OxyMouseDownEventArgs e)
        {
            if (isMarkUpdateMode && e.ChangedButton == OxyMouseButton.Left && sender is LineAnnotation lineAnnotation)
            {
                var plotModel = lineAnnotation.PlotModel;
                if (plotModel != null)
                {
                    // 拖動標記功能
                    isDraggingMark = true;
                    draggingMark = lineAnnotation;
                    initialMarkX = lineAnnotation.X;
                    e.Handled = true;
                }
            }
        }

        // 滑鼠移動事件，用於拖動標記線
        // Modify Mark_MouseMove to call UpdateMarkPositions
        private void Mark_MouseMove(object sender, OxyMouseEventArgs e)
        {
            if (isDraggingMark && draggingMark != null)
            {
                var plotModel = draggingMark.PlotModel;

                if (plotModel != null)
                {
                    var position = e.Position;
                    var screenPoint = position;
                    var dataPoint = Axis.InverseTransform(screenPoint, plotModel.Axes[0], plotModel.Axes[1]);

                    double newMarkX = dataPoint.X;

                    // Limit the mark movement range
                    if (newMarkX < 0)
                        newMarkX = 0;
                    if (newMarkX > emgDataCount / SamplingRate)
                        newMarkX = emgDataCount / SamplingRate;

                    // Update all corresponding marks in each channel
                    UpdateMarkPositions(draggingMark, newMarkX);
                }

                e.Handled = true; // Mark the event as handled
            }
        }
        // Update mark positions for all channels when moving a mark
        private void UpdateMarkPositions(LineAnnotation mark, double newMarkX)
        {
            // Find the index of the moved mark
            int markIndex = -1;
            for (int i = 0; i < markAnnotations.Count; i++)
            {
                int index = markAnnotations[i].IndexOf(mark);
                if (index != -1)
                {
                    markIndex = index;
                    break;
                }
            }

            // If the mark index is valid, update all corresponding marks in each channel
            if (markIndex != -1)
            {
                foreach (var channelMarks in markAnnotations)
                {
                    if (markIndex < channelMarks.Count)
                    {
                        channelMarks[markIndex].X = newMarkX;
                        channelMarks[markIndex].PlotModel.InvalidatePlot(false);
                    }
                }

                // Update the positions list
                if (markIndex < updatedMarkPositions.Count)
                {
                    updatedMarkPositions[markIndex] = newMarkX;
                }
                else
                {
                    updatedMarkPositions.Add(newMarkX);
                }
            }
        }


        // 滑鼠放開事件，結束標記線拖動
        private void Mark_MouseUp(object sender, OxyMouseEventArgs e)
        {
            if (isDraggingMark && draggingMark != null)
            {
                isDraggingMark = false;

                var finalMarkX = draggingMark.X;

                // 更新所有通道相同索引的标记到最终位置
                int markIndex = -1;
                for (int i = 0; i < markAnnotations.Count; i++)
                {
                    int index = markAnnotations[i].IndexOf(draggingMark);
                    if (index != -1)
                    {
                        markIndex = index;
                        break;
                    }
                }

                if (markIndex != -1)
                {
                    foreach (var channelMarks in markAnnotations)
                    {
                        if (markIndex < channelMarks.Count)
                        {
                            channelMarks[markIndex].X = finalMarkX;
                            channelMarks[markIndex].PlotModel.InvalidatePlot(false);
                        }
                    }
                }

                draggingMark = null;
                e.Handled = true;
            }
        }


        private void XAxisSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdatePlotViews(e.NewValue);
        }



        private void SliderUpdateTimer_Tick(object sender, EventArgs e)
        {
            sliderUpdateTimer.Stop();
            UpdatePlotViews(sliderNewValue);
        }

        private void XAxisSlider_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            isDraggingSlider = false;
            XAxisSlider.PreviewMouseLeftButtonUp -= XAxisSlider_MouseLeftButtonUp;
            UpdatePlotViews(sliderNewValue); // 在滑塊釋放時更新圖表
        }

        private void UpdatePlotViews(double newValue)
        {
            foreach (var plotModel in plotModels)
            {
                var xAxis = plotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Bottom);
                if (xAxis != null)
                {
                    // 計算新範圍
                    double newMin = newValue;
                    double newMax = newMin + maxDataPoints / SamplingRate;

                    // 確保範圍有效
                    if (newMin >= newMax) return;

                    xAxis.Minimum = newMin;
                    xAxis.Maximum = newMax;

                    plotModel.InvalidatePlot(false);
                }
            }
        }



        private void Update_mark(object sender, RoutedEventArgs e)
        {
            //if (_selectedCsvFileItem != null)
            //{
                try
                {
                    // 讀取 CSV 檔案的所有行
                    var lines = File.ReadAllLines(_selectedCsvFileItem.Path).ToList();

                    // 確保檔案包含至少一行數據（標題行和數據行）
                    if (lines.Count < 2)
                    {
                        MessageBox.Show("CSV檔案內容不足，無法更新標記。", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // 解析標記位置，使用分號分隔的線條來表示
                    int emgCount = lines[0].Split(',').Length - 1;

                    // 清空所有標記為 0
                    for (int i = 1; i < lines.Count; i++)
                    {
                        var parts = lines[i].Split(',');

                        if (emgCount < parts.Length)
                        {
                            parts[emgCount] = "0";
                        }
                        else
                        {
                            parts = parts.Concat(new[] { "0" }).ToArray();
                        }

                        lines[i] = string.Join(",", parts);
                    }

                    // 更新檔案中的標記位置為 20
                    updatedMarkPositions.Clear();
                    currentMarkPositions.Clear();
                    foreach (var channelMarks in markAnnotations)
                    {
                        foreach (var mark in channelMarks)
                        {
                            int markIndex = (int)(mark.X * SamplingRate);
                            if (markIndex >= 1 && markIndex < lines.Count)
                            {
                                var parts = lines[markIndex].Split(',');

                                if (emgCount < parts.Length)
                                {
                                    parts[emgCount] = "20";
                                }
                                else
                                {
                                    parts = parts.Concat(new[] { "20" }).ToArray();
                                }

                                lines[markIndex] = string.Join(",", parts);
                                updatedMarkPositions.Add(mark.X);
                                currentMarkPositions.Add(mark.X);
                            }
                        }
                    }

                    // 將更新的行寫回檔案
                    File.WriteAllLines(_selectedCsvFileItem.Path, lines);

                //if (isMarkUpdateMode == true)
                //{
                CustomMessageBox _CustomMessageBox = new CustomMessageBox(6);
                _CustomMessageBox.Show();
                //    markUpdated = true;
                //}
                //else
                //{
                //    CustomMessageBox _CustomMessageBox = new CustomMessageBox(8);
                //    _CustomMessageBox.Show();
                //}
            }
                catch (Exception ex)
                {
                    MessageBox.Show("無法更新檔案: " + ex.Message, "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            //}
            //else
            //{
            //    MessageBox.Show("請選擇有效的CSV檔案進行更新。", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            //}
            //isMarkUpdateMode = true;
            //btn_Cancel_mark.Visibility = Visibility.Visible;
        }

        private bool isAddingMark = false;
        private bool isDeletingMark = false;

        private void Add_mark(object sender, RoutedEventArgs e)
        {
            isAddingMark = true;
            isDeletingMark = false;
            btn_Cancel_mark.Visibility = Visibility.Visible;
            MessageBox.Show("點擊圖表以新增標記。", "新增標記", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Delete_mark(object sender, RoutedEventArgs e)
        {
            isAddingMark = false;
            isDeletingMark = true;
            btn_Cancel_mark.Visibility = Visibility.Visible;
            MessageBox.Show("點擊圖表以刪除標記。", "刪除標記", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Zoom In X functionality
        private void ZoomInX_Click(object sender, RoutedEventArgs e)
        {
            foreach (var plotModel in plotModels)
            {
                var xAxis = plotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Bottom);
                if (xAxis != null)
                {
                    // Calculate the new range by reducing it by 10%
                    double range = xAxis.ActualMaximum - xAxis.ActualMinimum;
                    double newMin = xAxis.ActualMinimum + range * 0.1;
                    double newMax = xAxis.ActualMaximum - range * 0.1;

                    // Prevent range inversion or exceeding data boundaries
                    if (newMin >= 0 && newMax <= emgDataCount / SamplingRate)
                    {
                        xAxis.Minimum = newMin;
                        xAxis.Maximum = newMax;
                        plotModel.InvalidatePlot(false);
                    }
                    else if (newMin >= 0 && newMax >= emgDataCount / SamplingRate)
                    {
                        xAxis.Minimum = newMin;
                        xAxis.Maximum = emgDataCount / SamplingRate;
                        plotModel.InvalidatePlot(false);
                    }
                    else if (newMin <= 0 && newMax <= emgDataCount / SamplingRate)
                    {
                        xAxis.Minimum = 0;
                        xAxis.Maximum = newMax;
                        plotModel.InvalidatePlot(false);
                    }
                    else
                    {
                        xAxis.Minimum = 0;
                        xAxis.Maximum = emgDataCount / SamplingRate;
                        plotModel.InvalidatePlot(false);
                    }
                    range = Math.Clamp(range, 1 / SamplingRate, emgDataCount / SamplingRate);
                    maxDataPoints = (int)(range * SamplingRate);
                }
            }
            UpdatePlot((int)(plotModels.First().Axes.First(a => a.Position == AxisPosition.Bottom).Minimum * SamplingRate));
            XAxisSlider.Maximum = Math.Max(0, (emgDataCount - maxDataPoints) / SamplingRate);
            XAxisSlider.Value = plotModels.First().Axes.First(a => a.Position == AxisPosition.Bottom).Minimum;
        }

        // Zoom Out X functionality
        private void ZoomOutX_Click(object sender, RoutedEventArgs e)
        {
            foreach (var plotModel in plotModels)
            {
                var xAxis = plotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Bottom);
                if (xAxis != null)
                {
                    // Calculate the new range by expanding it by 10%
                    double range = xAxis.ActualMaximum - xAxis.ActualMinimum;
                    double newMin = xAxis.ActualMinimum - range * 0.1;
                    double newMax = xAxis.ActualMaximum + range * 0.1;

                    // Prevent zooming out beyond data boundaries
                    if (newMin >= 0 && newMax <= emgDataCount / SamplingRate)
                    {
                        xAxis.Minimum = newMin;
                        xAxis.Maximum = newMax;
                        plotModel.InvalidatePlot(false);
                    }
                    else if(newMin >= 0 && newMax >= emgDataCount / SamplingRate)
                    {
                        xAxis.Minimum = newMin;
                        xAxis.Maximum = emgDataCount / SamplingRate;
                        plotModel.InvalidatePlot(false);
                    }
                    else if (newMin <= 0 && newMax <= emgDataCount / SamplingRate)
                    {
                        xAxis.Minimum = 0;
                        xAxis.Maximum = newMax;
                        plotModel.InvalidatePlot(false);
                    }
                    else
                    {
                        xAxis.Minimum = 0;
                        xAxis.Maximum = emgDataCount / SamplingRate;
                        plotModel.InvalidatePlot(false);
                    }
                    range = Math.Clamp(range, 1 / SamplingRate, emgDataCount / SamplingRate);
                    maxDataPoints = (int)(range * SamplingRate);
                }
            }

            UpdatePlot((int)(plotModels.First().Axes.First(a => a.Position == AxisPosition.Bottom).Minimum * SamplingRate));
            XAxisSlider.Maximum = Math.Max(0, (emgDataCount - maxDataPoints) / SamplingRate);
            XAxisSlider.Value = plotModels.First().Axes.First(a => a.Position == AxisPosition.Bottom).Minimum;
        }

        // 更新圖表（需自訂更新邏輯）
        private void UpdatePlot(int start)
        {
            // 根據需要實現更新圖表的邏輯
        }

        // 動態更新滑條範圍
        private void UpdateSliderRange(Axis xAxis)
        {
            if (XAxisSlider != null)
            {
                // 基於 X 軸範圍更新滑條
                double sliderMax = xAxis.Maximum - (maxDataPoints / SamplingRate);
                XAxisSlider.Minimum = xAxis.Minimum;
                XAxisSlider.Maximum = Math.Max(xAxis.Minimum, sliderMax);

                // 確保滑條值在新範圍內
                XAxisSlider.Value = Math.Max(XAxisSlider.Minimum, Math.Min(XAxisSlider.Value, XAxisSlider.Maximum));
            }
        }




        private void Cancel_mark(object sender, RoutedEventArgs e)
        {
            isAddingMark = false;
            isDeletingMark = false;
            isMarkUpdateMode = false;
            btn_Cancel_mark.Visibility = Visibility.Collapsed;
            // 恢復到 currentMarkPositions 標記的位置
            for (int i = 0; i < markAnnotations.Count; i++)
            {
                for (int j = 0; j < markAnnotations[i].Count; j++)
                {
                    if (j < currentMarkPositions.Count)
                    {
                        markAnnotations[i][j].X = currentMarkPositions[j];
                        markAnnotations[i][j].PlotModel.InvalidatePlot(false);
                    }
                }
            }
            
            markUpdated = false;
        }
    }






    public class DirectoryItem
    {
        public string Name { get; set; }
        public List<object> Children { get; set; } = new List<object>();

        public override string ToString()
        {
            return Name;
        }
    }

    public class FileItem
    {
        public string Name { get; set; }
        public string Path { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }
}
