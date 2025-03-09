using OxyPlot;
using OxyPlot.Series;
using OxyPlot.Axes;
using OxyPlot.Annotations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using OxyPlot.Wpf;
using System.Windows.Controls;
using System.Windows.Data;
using System.Globalization;
using System.Windows.Media;
using System.Windows.Input;
using Accord.Statistics.Analysis;
using Accord.Statistics.Models; // 嘗試引用這個命名空間
using System.Threading.Tasks; // 引入命名空間
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using System.Windows.Threading;

namespace Basic_Streaming_NET.Views
{
    public partial class Last_Window : Window
    {
        // Fixed Y-axis range
        private const double YAxisMin = -5;
        private const double YAxisMax = 5;

        // 用來存放所有圖表模型的清單
        private List<PlotModel> plotModels;

        // 用來存放所有 RMS 系列的清單
        private List<LineSeries> rmsSeries;

        // 用來控制是否顯示 RMS 曲線的標記
        private bool rmsDisplayed = false;

        // 設置最大資料點數量，對應於 10 秒的數據量（1926點/秒 * 10秒）
        private int maxDataPoints = 19260;

        // 存放總的資料點數
        private int totalDataPoints;

        // 取樣頻率，設置為每秒 1926 點
        private const double SamplingRate = 1926;

        // 用來存放總的 RMS 系列的字典，以索引為鍵
        private Dictionary<int, LineSeries> overallRmsSeriesDict;

        // 存放 EMG 通道數量
        private int emgCount = 0;

        // 用來標記是否正在拖動標記線
        private bool isDraggingMark = false;

        // 存放當前正在拖動的標記索引
        private int draggingMarkIndex = -1;

        // 存放原始的標記時間
        private double originalMarkTime;

        // 存放原始標記位置的字典，以通道索引為鍵
        private Dictionary<int, List<double>> originalMarkPositions = new Dictionary<int, List<double>>();

        // 初始化視窗並載入資料
        public Last_Window(string pathTopatient)
        {
            InitializeComponent();
            PlotData(pathTopatient);


            // 視窗載入時事件
            this.Loaded += Last_Window_Loaded;
            // 鍵盤按下事件
            this.KeyDown += Last_Window_KeyDown;
            // 視窗關閉時事件
            this.Closed += Last_Window_Closed;
            this.Loaded += (s, e) =>
            {
                // 為所有 PlotView 控制項註冊滑鼠事件
                foreach (var item in PlotsContainer.Items)
                {
                    if (PlotsContainer.ItemContainerGenerator.ContainerFromItem(item) is ContentPresenter contentPresenter)
                    {
                        if (VisualTreeHelper.GetChild(contentPresenter, 0) is PlotView plotView)
                        {
                            plotView.MouseDown += PlotView_MouseDown;
                            plotView.MouseMove += PlotView_MouseMove;
                            plotView.MouseUp += PlotView_MouseUp;
                        }
                    }
                }
            };
        }

        // 關閉視窗時釋放資源
        private void Last_Window_Closed(object sender, EventArgs e)
        {
            Dispose();
        }


        // 自定義 Dispose 方法來釋放資源
        public void Dispose()
        {
            // 停止計時器
            if (sliderUpdateTimer != null)
            {
                sliderUpdateTimer.Stop();
                sliderUpdateTimer = null;
            }

            // 清除圖表模型
            plotModels?.Clear();
            rmsSeries?.Clear();
            overallRmsSeriesDict?.Clear();

            // 移除事件訂閱
            this.Loaded -= Last_Window_Loaded;
            this.KeyDown -= Last_Window_KeyDown;
            this.Closed -= Last_Window_Closed;

            // 為每個 PlotView 解除事件訂閱並釋放資源
            foreach (var item in PlotsContainer.Items)
            {
                if (PlotsContainer.ItemContainerGenerator.ContainerFromItem(item) is ContentPresenter contentPresenter)
                {
                    if (VisualTreeHelper.GetChild(contentPresenter, 0) is PlotView plotView)
                    {
                        plotView.MouseDown -= PlotView_MouseDown;
                        plotView.MouseMove -= PlotView_MouseMove;
                        plotView.MouseUp -= PlotView_MouseUp;
                        plotView.Model = null; // 解除繫結，便於垃圾回收
                    }
                }
            }

            // 釋放其他需要釋放的物件或資源
        }

        // 滑鼠按下事件，用於檢測是否點擊到標記線
        private void PlotView_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var plotView = sender as PlotView;
                var plotModel = plotView?.Model;

                if (plotModel != null)
                {
                    var position = e.GetPosition(plotView);
                    var screenPoint = new ScreenPoint(position.X, position.Y);
                    var dataPoint = Axis.InverseTransform(screenPoint, plotModel.Axes[0], plotModel.Axes[1]);

                    double xValue = dataPoint.X;

                    var marks = plotModel.Annotations.OfType<LineAnnotation>().Where(a => a.Text == "MARK").OrderBy(a => a.X).ToList();
                    for (int i = 0; i < marks.Count; i++)
                    {
                        if (Math.Abs(xValue - marks[i].X) < 1)
                        {
                            isDraggingMark = true;
                            draggingMarkIndex = i;
                            originalMarkTime = marks[i].X;
                            break;
                        }
                    }

                }

                e.Handled = true;
            }
        }

        // 滑鼠移動事件，用於拖動標記線
        private void PlotView_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDraggingMark && draggingMarkIndex >= 0)
            {
                var plotView = sender as PlotView;
                var plotModel = plotView?.Model;

                if (plotModel != null)
                {
                    var position = e.GetPosition(plotView);
                    var screenPoint = new ScreenPoint(position.X, position.Y);
                    var dataPoint = Axis.InverseTransform(screenPoint, plotModel.Axes[0], plotModel.Axes[1]);

                    double newMarkTime = dataPoint.X;

                    // 更新所有圖表中的相應標記位置
                    foreach (var model in plotModels)
                    {
                        UpdateMarkTime(draggingMarkIndex, newMarkTime, model);
                        model.InvalidatePlot(false);
                    }
                }

                e.Handled = true;
            }
        }

        // 滑鼠放開事件，結束標記線拖動
        private void PlotView_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (isDraggingMark)
            {
                isDraggingMark = false;
                draggingMarkIndex = -1;
                originalMarkTime = 0;
            }
        }

        // 用來儲存更新後的標記時間
        private Dictionary<int, double> updatedMarkTimes = new Dictionary<int, double>();

        // 更新標記線位置
        private void UpdateMarkTime(int markIndex, double newMarkTime, PlotModel plotModel)
        {
            var marks = plotModel.Annotations.OfType<LineAnnotation>().Where(a => a.Text == "MARK").OrderBy(a => a.X).ToList();

            if (markIndex < marks.Count)
            {
                marks[markIndex].X = newMarkTime;
                // 儲存更新的標記位置
                updatedMarkTimes[markIndex] = newMarkTime;
            }
        }

        // 鍵盤按下事件，用於檢測快捷鍵操作
        private void Last_Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // 按下 'Q' 鍵關閉視窗
            if (e.Key == System.Windows.Input.Key.Q)
            {
                this.Close();
            }
        }

        // 視窗載入完成後調整視圖高度並載入資料夾內容
        private void Last_Window_Loaded(object sender, RoutedEventArgs e)
        {
            AdjustPlotViewHeights();
            LoadFolderContents();
        }

        // 儲存當前資料夾路徑
        string parentDirectory;

        // 載入資料夾內容，並填充至 ComboBox 控制項
        private void LoadFolderContents()
        {
            // 獲取當前 CSV 檔案所在的目錄
            string currentDirectory = Path.GetDirectoryName(filePath);

            // 獲取上一層的目錄
            parentDirectory = Directory.GetParent(currentDirectory).FullName;

            // 獲取上一層資料夾的內容並填充至 folderComboBox_Parameter
            var parentFolders = Directory.GetDirectories(parentDirectory);
            //folderComboBox_Parameter.Items.Clear();
            //foreach (var folder in parentFolders)
            //{
            //    folderComboBox_Parameter.Items.Add(Path.GetFileName(folder)); // 只顯示資料夾名稱
            //}

            //// 設置預設選中的資料夾名稱
            //string parentFolderName = Path.GetFileName(Directory.GetParent(filePath).FullName);
            //folderComboBox_Parameter.SelectedItem = parentFolderName;
        }

        // 當參數資料夾 ComboBox 改變時，載入選取資料夾內的檔案
        private void folderComboBox_SelectionChanged_Parameter(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // 獲取當前選取的資料夾名稱
            //string selectedFolder = folderComboBox_Parameter.SelectedItem as string;

            //if (!string.IsNullOrEmpty(selectedFolder))
            //{
            //    // 取得目前路徑下的上一層資料夾
            //    string currentDirectory = Path.GetDirectoryName(filePath);
            //    string parentDirectory = Directory.GetParent(currentDirectory).FullName;

            //    // 拼接選取資料夾的完整路徑
            //    string selectedFolderPath = Path.Combine(parentDirectory, selectedFolder);

            //    // 獲取該資料夾內所有的檔案
            //    var files = Directory.GetFiles(selectedFolderPath);

            //    // 清空 folderComboBox_Data
            //    folderComboBox_Data.Items.Clear();

                //// 將檔案名稱添加至 folderComboBox_Data
                //foreach (var file in files)
                //{
                //    folderComboBox_Data.Items.Add(Path.GetFileName(file));
                //}

                //// 如果有檔案，預設選中第一個檔案
                //if (folderComboBox_Data.Items.Count > 0)
                //{
                //    folderComboBox_Data.SelectedIndex = 0;
                //}
            //}
        }

        // 資料檔案 ComboBox 改變時的處理邏輯（目前為空）
        private void folderComboBox_SelectionChanged_Data(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {

        }

        // 固定 PlotView 控制項的高度為 300
        private void AdjustPlotViewHeights()
        {
            double fixedHeight = 180;

            foreach (var item in PlotsContainer.Items)
            {
                if (PlotsContainer.ItemContainerGenerator.ContainerFromItem(item) is ContentPresenter contentPresenter)
                {
                    if (VisualTreeHelper.GetChildrenCount(contentPresenter) > 0)
                    {
                        if (VisualTreeHelper.GetChild(contentPresenter, 0) is PlotView plotView)
                        {
                            plotView.Height = fixedHeight;
                            SetYAxisLimits(plotView.Model);
                        }
                    }
                }
            }
        }
        // Set Y-axis limits to be between -5 and 5
        private void SetYAxisLimits(PlotModel plotModel)
        {
            if (plotModel == null)
                return;

            var yAxis = plotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Left);
            if (yAxis != null)
            {
                yAxis.Minimum = YAxisMin;
                yAxis.Maximum = YAxisMax;
                plotModel.InvalidatePlot(false);
            }
        }


        // 存放檔案路徑
        string filePath;

        // 繪製資料，並加載 CSV 檔案的資料
        private void PlotData(string pathTopatient)
        {
            filePath = pathTopatient;
            LoadDataFromCsv(filePath);
        }

        // 加載 CSV 資料並初始化圖表
        private void LoadDataFromCsv(string filePath)
        {
            var lines = File.ReadAllLines(filePath);

            // 設置 EMG 通道數量
            emgCount = lines[0].Split(',').Length - 1;
            totalDataPoints = lines.Length - 1;
            XAxisSlider.Maximum = (totalDataPoints - maxDataPoints) / SamplingRate;

            // 初始化 EMG 資料陣列
            var emgData = new List<LineSeries>[emgCount];

            for (int i = 0; i < emgCount; i++)
            {
                emgData[i] = new List<LineSeries>
        {
            new LineSeries
            {
                Title = $"EMG {i + 1}",
                Color = OxyColors.Black,   // 設置線條顏色為黑色
                StrokeThickness = 1       // 設置線條寬度較細
            }
        };
            }

            // 初始化標記線系列
            var markers = new LineSeries { Title = "Markers" };
            var markerPositions = new List<double>();

            // 讀取每一行資料
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                var parts = line.Split(',');

                // 處理 EMG 數據
                for (int j = 0; j < emgCount; j++)
                {
                    if (double.TryParse(parts[j], out double emgValue))
                    {
                        emgData[j][0].Points.Add(new DataPoint(i / SamplingRate, emgValue));
                    }
                }

                // 收集 MARK 點的位置
                if (double.TryParse(parts[emgCount], out double markerValue) && markerValue > 0)
                {
                    double markPosition = i / SamplingRate;
                    markerPositions.Add(markPosition);
                }
            }

            // 保存原始的 MARK 位置
            originalMarkPositions.Clear();
            for (int i = 0; i < emgCount; i++)
            {
                originalMarkPositions[i] = new List<double>(markerPositions);  // 存入原始位置
            }

            // 創建圖表並添加標記
            plotModels = new List<PlotModel>();
            for (int i = 0; i < emgCount; i++)
            {
                var plotModel = new PlotModel { Title = $"Sensor {i + 1}" };
                plotModel.Series.Add(emgData[i][0]);

                // 添加標記線
                foreach (var markerPosition in markerPositions)
                {
                    var lineAnnotation = new LineAnnotation
                    {
                        Type = LineAnnotationType.Vertical,
                        X = markerPosition,
                        Color = OxyColors.Red,
                        LineStyle = LineStyle.Dash,
                        Text = "MARK",
                        TextVerticalAlignment = OxyPlot.VerticalAlignment.Bottom
                    };
                    plotModel.Annotations.Add(lineAnnotation);
                }

                // 設置 X 軸
                var xAxis = new LinearAxis
                {
                    Position = AxisPosition.Bottom,
                    Minimum = 0,
                    Maximum = maxDataPoints / SamplingRate,
                    IsZoomEnabled = false,  // 禁用 X 軸縮放
                    IsPanEnabled = false     // 禁用 X 軸拖動
                };
                plotModel.Axes.Add(xAxis);

                // 設置 Y 軸
                var yAxis = new LinearAxis
                {
                    Position = AxisPosition.Left,
                    Minimum = double.NaN,  // 自動適應最小值
                    Maximum = double.NaN,  // 自動適應最大值
                    MajorGridlineStyle = LineStyle.Solid,
                    MinorGridlineStyle = LineStyle.Dot,
                    MajorGridlineColor = OxyColors.Gray,
                    MinorGridlineColor = OxyColors.LightGray
                };
                plotModel.Axes.Add(yAxis);

                plotModels.Add(plotModel);
            }

            // 創建單獨顯示 MARK 的圖表
            var markOnlyPlotModel = new PlotModel { Title = "Marks Only" };

            // 添加標記線
            foreach (var markerPosition in markerPositions)
            {
                var lineAnnotation = new LineAnnotation
                {
                    Type = LineAnnotationType.Vertical,
                    X = markerPosition,
                    Color = OxyColors.Red,
                    LineStyle = LineStyle.Dash,
                    Text = "MARK",
                    TextVerticalAlignment = OxyPlot.VerticalAlignment.Bottom
                };
                markOnlyPlotModel.Annotations.Add(lineAnnotation);
            }

            // 設置 X 軸
            var markXAxis = new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Minimum = 0,
                Maximum = 10,
                IsZoomEnabled = false,  // 禁用 X 軸縮放
                IsPanEnabled = false     // 禁用 X 軸拖動
            };
            markOnlyPlotModel.Axes.Add(markXAxis);

            // 設置 Y 軸
            var markYAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                Minimum = 0,
                Maximum = 1,
                IsZoomEnabled = false,
                IsPanEnabled = false,
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = OxyColors.Gray,
                MinorGridlineColor = OxyColors.LightGray
            };
            markOnlyPlotModel.Axes.Add(markYAxis);

            // 將 MARK 圖表模型添加到 plotModels
            plotModels.Add(markOnlyPlotModel);

            // 將圖表模型設置為 ItemsSource
            PlotsContainer.ItemsSource = plotModels;
        }



        // 當 X 軸滑動條值變化時，調整圖表 X 軸範圍
        private void XAxisSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // 設置一個標誌，標記滑塊正在滑動
            if (!isDraggingSlider)
            {
                isDraggingSlider = true;
                XAxisSlider.PreviewMouseLeftButtonUp += XAxisSlider_MouseLeftButtonUp;
            }

            // 暫存滑塊的值，用於在滑塊釋放時更新圖表
            sliderNewValue = e.NewValue;

            // 使用 DispatcherTimer 來限制更新頻率
            if (sliderUpdateTimer == null)
            {
                sliderUpdateTimer = new DispatcherTimer();
                sliderUpdateTimer.Interval = TimeSpan.FromMilliseconds(50); // 設定更新間隔，例如 50ms
                sliderUpdateTimer.Tick += SliderUpdateTimer_Tick;
            }
            sliderUpdateTimer.Start();
        }
        private void SliderUpdateTimer_Tick(object sender, EventArgs e)
        {
            sliderUpdateTimer.Stop();
            UpdatePlotViews(sliderNewValue);
        }

        private void XAxisSlider_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
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
                    double newMin = newValue;
                    double newMax = newMin + maxDataPoints / SamplingRate;
                    xAxis.Minimum = newMin;
                    xAxis.Maximum = newMax;
                    plotModel.InvalidatePlot(false); // 只刷新需要改變的部分，降低刷新開銷
                }
            }
        }

        private bool isDraggingSlider = false;
        private double sliderNewValue = 0;
        private DispatcherTimer sliderUpdateTimer = null;



        // 點擊 ZoomInY 按鈕時，縮小 Y 軸範圍
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
                    yAxis.Zoom(newMin, newMax);
                    plotModel.InvalidatePlot(true);
                }
            }
        }

        // 點擊 ZoomOutY 按鈕時，放大 Y 軸範圍
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
                    yAxis.Zoom(newMin, newMax);
                    plotModel.InvalidatePlot(true);
                }
            }
        }

        // 點擊 ZoomInX 按鈕時，縮小 X 軸範圍
        private void ZoomInX_Click(object sender, RoutedEventArgs e)
        {
            AdjustXAxisScale(0.9);
        }

        // 點擊 ZoomOutX 按鈕時，放大 X 軸範圍
        private void ZoomOutX_Click(object sender, RoutedEventArgs e)
        {
            AdjustXAxisScale(1.1);
        }

        // 調整 X 軸比例
        private void AdjustXAxisScale(double zoomFactor)
        {
            foreach (var model in plotModels)
            {
                var xAxis = model.Axes.FirstOrDefault(a => a.Position == AxisPosition.Bottom);
                if (xAxis != null)
                {
                    double range = xAxis.Maximum - xAxis.Minimum;
                    double newRange = range * zoomFactor;

                    newRange = Math.Clamp(newRange, 1 / SamplingRate, totalDataPoints / SamplingRate);

                    double midpoint = (xAxis.Maximum + xAxis.Minimum) / 2;
                    double newMin = Math.Max(0, midpoint - newRange / 2);
                    double newMax = Math.Min(totalDataPoints / SamplingRate, midpoint + newRange / 2);

                    xAxis.Minimum = newMin;
                    xAxis.Maximum = newMax;

                    maxDataPoints = (int)(newRange * SamplingRate);

                    model.InvalidatePlot(true);
                }
            }

            UpdatePlot((int)(plotModels.First().Axes.First(a => a.Position == AxisPosition.Bottom).Minimum * SamplingRate));
            XAxisSlider.Maximum = Math.Max(0, (totalDataPoints - maxDataPoints) / SamplingRate);
            XAxisSlider.Value = plotModels.First().Axes.First(a => a.Position == AxisPosition.Bottom).Minimum;
        }

        // 更新圖表（需自訂更新邏輯）
        private void UpdatePlot(int start)
        {
            // 根據需要實現更新圖表的邏輯
        }
                                                                  // 用於保存每段 RMS 值的列表

        List<List<double>> RMS  = new List<List<double>>();
        List<List<double>> RMS_10 = new List<List<double>>();


        // 點擊 ToggleRMS 按鈕時，顯示或隱藏 RMS 曲線
        private void ToggleRMS_Click(object sender, RoutedEventArgs e)
        {
            if (rmsDisplayed)
            {
                foreach (var rms in rmsSeries)
                {
                    foreach (var model in plotModels)
                    {
                        model.Series.Remove(rms);
                    }
                }
                rmsDisplayed = false;
            }
            else
            {
                rmsSeries = new List<LineSeries>();
                overallRmsSeriesDict = new Dictionary<int, LineSeries>();
                int rmsWindowSize = (int)(0.1 * SamplingRate);
                //0.1
                for (int i = 0; i < plotModels.Count; i++)
                {
                    var originalSeries = plotModels[i].Series.FirstOrDefault() as LineSeries;
                    if (originalSeries != null)
                    {
                        var rmsSeriesData = new LineSeries { Title = $"RMS {i + 1}", Color = OxyColors.Blue };

                        List<double> rmsValuesList = new List<double>(); //ejj

                        for (int j = rmsWindowSize; j < originalSeries.Points.Count; j += rmsWindowSize)
                        {
                            double rms = CalculateRMS(originalSeries.Points.Skip(j - rmsWindowSize).Take(rmsWindowSize).Select(p => p.Y));
                            rmsSeriesData.Points.Add(new DataPoint(j / SamplingRate, rms));
                            //ejj
                            rmsValuesList.Add(rms);
                        }
                        RMS.Add(rmsValuesList);
                        plotModels[i].Series.Add(rmsSeriesData);
                        rmsSeries.Add(rmsSeriesData);
                        overallRmsSeriesDict[i] = rmsSeriesData;
                    }
                }
                rmsDisplayed = true;

                //0.1RMS
                CalculateRMS_10();

                // 對 0.1 秒的 RMS 進行正規化
                NormalizeRMS_10();

                //對 0.1 秒的 RMS 進行ZScore正規化
                //NormalizeRMS_10_ZScore();

            }

            foreach (var model in plotModels)
            {
                model.InvalidatePlot(true);
            }

            
            int x = 0;
        }

        // 計算 RMS 值
        private double CalculateRMS(IEnumerable<double> values)
        {
            double sumOfSquares = values.Select(v => v * v).Sum();
            return Math.Sqrt(sumOfSquares / values.Count());
        }
        //算0.1/sRMS
        private void CalculateRMS_10()
        {
            RMS_10.Clear(); // 清空之前的 RMS_10 數據

            int rmsWindowSize = (int)(0.1 * SamplingRate); // 每 0.1 秒對應的採樣點數

            for (int i = 0; i < plotModels.Count; i++)
            {
                var originalSeries = plotModels[i].Series.FirstOrDefault() as LineSeries;
                if (originalSeries != null)
                {
                    List<double> rmsValuesList = new List<double>();

                    // 遍歷數據，以每 0.1 秒為一個窗口計算 RMS
                    for (int j = rmsWindowSize; j < originalSeries.Points.Count; j += rmsWindowSize)
                    {
                        double rms = CalculateRMS(originalSeries.Points.Skip(j - rmsWindowSize).Take(rmsWindowSize).Select(p => p.Y));
                        rmsValuesList.Add(rms); // 添加計算出的 RMS 值到列表中
                    }
                    // 將計算出的 RMS 值列表添加到 RMS_10 中
                    RMS_10.Add(rmsValuesList);
                }
            }
        }

        // 對 0.1 秒的 RMS 進行正規化
        private void NormalizeRMS_10()
        {
            foreach (var rmsValuesList in RMS_10)
            {
                if (rmsValuesList.Count > 0)
                {
                    double sumOfSquares = rmsValuesList.Select(v => v * v).Sum(); // 計算平方和
                    double rmsNormalizationFactor = Math.Sqrt(sumOfSquares / rmsValuesList.Count); // 計算 RMS 歸一化因子

                    if (rmsNormalizationFactor > 0)
                    {
                        for (int i = 0; i < rmsValuesList.Count; i++)
                        {
                            rmsValuesList[i] /= rmsNormalizationFactor; // 將每個值除以 RMS 歸一化因子進行正規化
                        }
                    }
                }
            }
        }

        // 對 0.1 秒的 RMS 進行 Z-score 標準化
        private void NormalizeRMS_10_ZScore()
        {
            foreach (var rmsValuesList in RMS_10)
            {
                if (rmsValuesList.Count > 0)
                {
                    // 計算平均值
                    double mean = rmsValuesList.Average();

                    // 計算標準差
                    double variance = rmsValuesList.Select(v => Math.Pow(v - mean, 2)).Sum() / rmsValuesList.Count;
                    double standardDeviation = Math.Sqrt(variance);

                    if (standardDeviation > 0)
                    {
                        // 使用 Z-score 標準化
                        for (int i = 0; i < rmsValuesList.Count; i++)
                        {
                            rmsValuesList[i] = (rmsValuesList[i] - mean) / standardDeviation;
                        }
                    }
                    else
                    {
                        // 如果標準差為 0，表示所有值相同，則所有值都標準化為 0
                        for (int i = 0; i < rmsValuesList.Count; i++)
                        {
                            rmsValuesList[i] = 0;
                        }
                    }
                }
            }
        }

        // 點擊 ShowBarChart 按鈕時，顯示 RMS 面積的長條圖
        private void ShowBarChart_Click(object sender, RoutedEventArgs e)
        {
            Average_Analysis last_window = new Average_Analysis(filePath);
            last_window.Show();

        }



        // 創建包含 CheckBox 的目錄節點，並將所有節點設為展開狀態
        private TreeViewItem CreateDirectoryNodeWithCheckBoxes(DirectoryInfo directoryInfo)
        {
            var directoryNode = new TreeViewItem
            {
                Header = directoryInfo.Name,
                Tag = directoryInfo.FullName,
                IsExpanded = true // 設置為展開狀態
            };

            foreach (var dir in directoryInfo.GetDirectories())
            {
                directoryNode.Items.Add(CreateDirectoryNodeWithCheckBoxes(dir));
            }

            foreach (var file in directoryInfo.GetFiles("*.csv"))
            {
                var fileNode = new TreeViewItem
                {
                    Header = new CheckBox { Content = file.Name, IsChecked = true }, // 預設勾選 CheckBox
                    Tag = file.FullName
                };
                directoryNode.Items.Add(fileNode);
            }

            return directoryNode;
        }



        // 取得選中的檔案
        private List<string> GetSelectedFiles(TreeViewItem rootNode)
        {
            var selectedFiles = new List<string>();

            foreach (var item in rootNode.Items)
            {
                if (item is TreeViewItem subItem)
                {
                    if (subItem.Header is CheckBox checkBox && checkBox.IsChecked == true)
                    {
                        selectedFiles.Add(subItem.Tag.ToString());
                    }
                    selectedFiles.AddRange(GetSelectedFiles(subItem)); // 遞迴查找
                }
            }

            return selectedFiles;
        }

      

        // 顯示選中的檔案的 RMS 面積結果
        private void DisplayResults(Dictionary<string, Dictionary<int, double>> overallAreaDict, string[] subDirectories, string currentDirectory)
        {
            // 建立主 Grid 佈局
            var mainGrid = new Grid();
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) }); // 左側顯示 TreeView
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // 右側顯示結果圖表

            // 初始化圖表
            var overallPlotModel = new PlotModel { Title = "Overall RMS 面積比較" };
            var overallBarSeries = new BarSeries
            {
                LabelPlacement = LabelPlacement.Middle,
                LabelFormatString = "{0:.00}",
                BarWidth = 0.6
            };

            // 設置 X 軸類別
            var categoryAxisItems = overallAreaDict.Keys
                .SelectMany(file => Enumerable.Range(1, overallAreaDict[file].Count)
                .Select(i => $"{file} - EMG {i}"))
                .ToList();

            var categoryAxis = new CategoryAxis
            {
                Position = AxisPosition.Left,
                Title = "File Name and EMG Channel",
                ItemsSource = categoryAxisItems,
                GapWidth = 0.5,
                IsTickCentered = true
            };

            // 設置資料到 BarSeries
            foreach (var fileEntry in overallAreaDict)
            {
                foreach (var channelEntry in fileEntry.Value)
                {
                    var barItem = new BarItem
                    {
                        Value = channelEntry.Value,
                        CategoryIndex = categoryAxisItems.IndexOf($"{fileEntry.Key} - EMG {channelEntry.Key + 1}")
                    };
                    overallBarSeries.Items.Add(barItem);
                }
            }

            // 設定圖表的 X 軸和 Y 軸
            overallPlotModel.Series.Add(overallBarSeries);
            overallPlotModel.Axes.Add(categoryAxis);
            overallPlotModel.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "RMS 面積" });

            // 建立 PlotView 控制項來顯示圖表
            var plotView = new PlotView { Model = overallPlotModel };

            // 將圖表加入 Grid
            Grid.SetColumn(plotView, 1);
            mainGrid.Children.Add(plotView);

            // 建立結果視窗，顯示分析結果
            var resultsWindow = new Window
            {
                Title = "RMS 面積比較",
                Content = mainGrid,
                Width = 800,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            resultsWindow.ShowDialog();
        }



        private void UpdateOverallBarChart(Dictionary<string, Dictionary<int, double>> overallAreaDict, string currentDirectory, Grid chartGrid)
        {
            // 清空現有的圖表區域
            chartGrid.Children.Clear();

            // 建立一個新的 PlotModel 來顯示 Overall RMS 面積
            var overallPlotModel = new PlotModel { Title = "Overall RMS 面積比較" };
            var overallBarSeries = new BarSeries
            {
                LabelPlacement = LabelPlacement.Middle,
                LabelFormatString = "{0:.00}",
                BarWidth = 0.6
            };

            // 設置 X 軸類別
            var categoryAxisItems = overallAreaDict.Keys
                .SelectMany(file => Enumerable.Range(1, overallAreaDict[file].Count)
                .Select(i => $"{file} - EMG {i}"))
                .ToList();

            var categoryAxis = new CategoryAxis
            {
                Position = AxisPosition.Left,
                Title = "File Name and EMG Channel",
                ItemsSource = categoryAxisItems,
                GapWidth = 0.5,
                IsTickCentered = true
            };

            // 設置資料到 BarSeries
            foreach (var fileEntry in overallAreaDict)
            {
                foreach (var channelEntry in fileEntry.Value)
                {
                    var barItem = new BarItem
                    {
                        Value = channelEntry.Value,
                        CategoryIndex = categoryAxisItems.IndexOf($"{fileEntry.Key} - EMG {channelEntry.Key + 1}")
                    };
                    overallBarSeries.Items.Add(barItem);
                }
            }

            // 加入 Series 和 Axis 到 PlotModel
            overallPlotModel.Series.Add(overallBarSeries);
            overallPlotModel.Axes.Add(categoryAxis);
            overallPlotModel.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "RMS 面積" });

            // 建立 PlotView 並將圖表設置為內容
            var plotView = new PlotView { Model = overallPlotModel };
            chartGrid.Children.Add(plotView);
        }




        // 獲取標記線的時間位置
        private double GetMarkTime(int markNumber)
        {
            var marks = plotModels
                .SelectMany(pm => pm.Annotations.OfType<LineAnnotation>().Where(a => a.Text == "MARK"))
                .OrderBy(a => a.X)
                .ToList();

            if (markNumber < marks.Count)
            {
                markNumber = markNumber * (marks.Count) / 2;
                return marks[markNumber].X;
            }
            return -1;
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
        public static (Matrix<double>, Matrix<double>) NNMFDecomposition(Matrix<double> V, int k, int maxIter, double tolerance)
        {
            Matrix<double> bestW = null;
            Matrix<double> bestH = null;
            double bestError = double.MaxValue;

            int numRuns = 1; // 多次初始化以避免局部最小值

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
            }

            System.Diagnostics.Debug.WriteLine($"Best error after {numRuns} runs: {bestError}");
            return (bestW, bestH);
        }

        private double[,] GetRMSValuesInMarkRange(int startMarkNumber, int endMarkNumber)
        {
            double startTime = GetMarkTime(startMarkNumber);
            double endTime = GetMarkTime(endMarkNumber);

            if (startTime == -1 || endTime == -1 || startTime >= endTime)
            {
                throw new ArgumentException("無效的 MARK 區間");
            }

            // 用於保存每個 plotModel 的區間內 RMS 值的列表
            List<List<double>> allRmsValuesInRange = new List<List<double>>();

            // 遍歷每個 plotModel 並根據 MARK 區間提取 RMS 值
            for (int i = 0; i < plotModels.Count; i++)
            { 
                // 找到對應的 RMS_10 列表
                if (i < RMS_10.Count)
                {
                    List<double> rmsValuesList = RMS_10[i];
                    var originalSeries = plotModels[i].Series.FirstOrDefault() as LineSeries;

                    if (originalSeries != null)
                    {
                        // 找到 MARK 對應的索引（基於時間轉換為索引）
                        int startIndex = (int)(startTime * SamplingRate / (0.1 * SamplingRate));
                        int endIndex = (int)(endTime * SamplingRate / (0.1 * SamplingRate));

                        // 確保索引範圍有效
                        if (startIndex >= 0 && endIndex <= rmsValuesList.Count && startIndex < endIndex)
                        {
                            // 提取該區間的 RMS 值並保存到單獨的列表
                            List<double> rmsValuesInRange = rmsValuesList.Skip(startIndex).Take(endIndex - startIndex).ToList();

                            // 添加當前 plotModel 的區間 RMS 值到總列表中
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
                        // 如果沒有找到原始數據系列，則添加一個空的列表
                        allRmsValuesInRange.Add(new List<double>());
                    }
                }
            }

            // 將 List<List<double>> 轉換為 double[,] 格式
            int rowCount = allRmsValuesInRange.Count;
            int colCount = allRmsValuesInRange.Max(row => row.Count);
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

        // 視覺化 W 和 H 的結果
        private void VisualizeWandH(Matrix<double> W, Matrix<double> H)
        {
            int sensorCount = W.RowCount; // sensor 的數量應與 W 的行數一致
            int k = W.ColumnCount; // 基向量數量

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            for (int i = 0; i < k; i++)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            for (int j = 0; j < k; j++)
            {
                // W 矩陣的柱狀圖 - 顯示基向量的所有貢獻
                var modelW = new PlotModel { Title = $"W 矩陣 - 基向量 {j + 1} 的貢獻" };
                var contributions = W.Column(j).Select((value, index) => new { Value = value, Index = index }).ToList();

                var barSeriesW = new BarSeries
                {
                    Title = $"基向量 {j + 1}",
                    ItemsSource = contributions.Select(c => new BarItem { Value = c.Value, CategoryIndex = c.Index }).ToList(),
                    BaseValue = 0,
                    FillColor = OxyColor.FromRgb((byte)((j * 85) % 255), (byte)(((j * 85) + 100) % 255), (byte)(((j * 85) + 200) % 255))
                };
                modelW.Series.Add(barSeriesW);
                modelW.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom });

                var plotViewW = new PlotView
                {
                    Model = modelW,
                    Width = 300,
                    Height = 200
                };

                // 將 W 的圖加入到 Grid
                Grid.SetRow(plotViewW, j);
                Grid.SetColumn(plotViewW, 0);
                grid.Children.Add(plotViewW);

                // H 矩陣的激活係數折線圖 - 每個基向量
                var modelH = new PlotModel { Title = $"H 矩陣激活係數 - 基向量 {j + 1}" };
                var lineSeriesH = new LineSeries
                {
                    Title = $"基向量 {j + 1}",
                    Color = OxyColor.FromRgb((byte)((j * 85) % 255), (byte)(((j * 85) + 100) % 255), (byte)(((j * 85) + 200) % 255))
                };

                for (int i = 0; i < H.ColumnCount; i++)
                {
                    lineSeriesH.Points.Add(new DataPoint(i, H[j, i]));
                }
                modelH.Series.Add(lineSeriesH);
                modelH.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "時間" });
                modelH.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "激活係數" });

                var plotViewH = new PlotView
                {
                    Model = modelH,
                    Width = 300,
                    Height = 200
                };

                // 將 H 的圖加入到 Grid
                Grid.SetRow(plotViewH, j);
                Grid.SetColumn(plotViewH, 1);
                grid.Children.Add(plotViewH);
            }

            // 使用 ScrollViewer 包裝 Grid 來實現滾動
            var scrollViewer = new ScrollViewer
            {
                Content = grid,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            var window = new Window
            {
                Title = "W 和 H 矩陣的可視化",
                Content = scrollViewer,
                Width = 700,
                Height = 600
            };
            window.Show();
        }

        // 按鈕事件，顯示 W 和 H 的可視化
        private void EJ_ShowBarChart_Click(object sender, RoutedEventArgs e)
        {
            //0.1RMS
            CalculateRMS_10();

            // 對 0.1 秒的 RMS 進行正規化
            NormalizeRMS_10();
            // 提取區間內的 RMS 值
            var V = Matrix<double>.Build.DenseOfArray(GetRMSValuesInMarkRange(0, 1));
            int k = 3; // 基向量數量設為 3
            int maxIter = 5000;
            double tolerance = 1e-4;

            // NNMF 分解
            var (W, H) = NNMFDecomposition(V, k, maxIter, tolerance);

            // 檢查 W 和 H 矩陣是否有有效數據
            if ((W == null || W.RowCount == 0 || W.ColumnCount == 0) ||
                (H == null || H.RowCount == 0 || H.ColumnCount == 0))
            {
                MessageBox.Show("W 或 H 矩陣無有效數據。");
                return;
            }

            // 可視化 W 和 H
            VisualizeWandH(W, H);
        }






        // 按鈕事件，未實作（RJ_ShowBarChart_Click）
        private void RJ_ShowBarChart_Click(object sender, RoutedEventArgs e)
        {
            // 獲取 MARK 區間的開始和結束時間
            double startMarkTime = GetMarkTime(0);
            double endMarkTime = GetMarkTime(1);

            if (startMarkTime == -1 || endMarkTime == -1 || startMarkTime >= endMarkTime)
            {
                MessageBox.Show("無效的 MARK 區間", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 獲取區間內的 RMS 值
            List<List<double>> rmsValuesInRange = new List<List<double>>();
            foreach (var rmsChannel in RMS_10)
            {
                int startIndex = (int)(startMarkTime * SamplingRate / (0.1 * SamplingRate));
                int endIndex = (int)(endMarkTime * SamplingRate / (0.1 * SamplingRate));

                if (startIndex >= 0 && endIndex <= rmsChannel.Count && startIndex < endIndex)
                {
                    rmsValuesInRange.Add(rmsChannel.Skip(startIndex).Take(endIndex - startIndex).ToList());
                }
                else
                {
                    rmsValuesInRange.Add(new List<double>()); // 如果索引無效，添加空列表
                }
            }

            // 計算 HFD 並繪製結果
            double[][] standardizedEmgData = rmsValuesInRange.Select(r => r.ToArray()).ToArray();
            List<List<double>> hfdValues = CalculateHFDPerChannel(standardizedEmgData);
            PlotHFDResultsWithOxyPlot(hfdValues);
        }

        // 計算每個通道的 HFD 值
        private List<List<double>> CalculateHFDPerChannel(double[][] emgData)
        {
            List<List<double>> allHfdValues = new List<List<double>>();
            foreach (var channelData in emgData)
            {
                List<double> hfdValues = new List<double>();
                if (channelData.Length == 0)
                {
                    hfdValues.Add(0); // 如果數據無效，返回 0
                    allHfdValues.Add(hfdValues);
                    continue;
                }

                // 將數據分成每 10 個 RMS 值為一組，計算每組的 HFD
                int segmentSize = 10;
                for (int i = 0; i < channelData.Length; i += segmentSize)
                {
                    var segment = channelData.Skip(i).Take(segmentSize).ToArray();
                    if (segment.Length < segmentSize)
                    {
                        break; // 如果片段長度不足，跳過
                    }

                    // Higuchi Fractal Dimension 標準算法
                    int kMax = Math.Min(10, segment.Length / 2); // 最大的間隔長度不能超過數據長度的一半
                    List<double> Lk = new List<double>();

                    for (int k = 1; k <= kMax; k++)
                    {
                        double lengthSum = 0;
                        int numSegments = 0;

                        for (int m = 0; m < k; m++)
                        {
                            double length = 0;
                            int count = 0;

                            for (int j = m; j < segment.Length - k; j += k)
                            {
                                length += Math.Abs(segment[j + k] - segment[j]);
                                count++;
                            }

                            if (count > 0)
                            {
                                length = (length * (segment.Length - 1)) / (count * k);
                                lengthSum += length;
                                numSegments++;
                            }
                        }

                        if (numSegments > 0)
                        {
                            Lk.Add(lengthSum / numSegments);
                        }
                    }

                    // 計算 HFD 的斜率
                    if (Lk.Count > 1)
                    {
                        List<double> logLk = Lk.Select(length => Math.Log(length)).ToList();
                        List<double> logK = Enumerable.Range(1, Lk.Count).Select(k => Math.Log(1.0 / k)).ToList();
                        double slope = LinearRegression(logK, logLk);
                        hfdValues.Add(slope);
                    }
                    else
                    {
                        hfdValues.Add(0); // 如果 Lk 中沒有足夠的值，返回 0
                    }
                }
                allHfdValues.Add(hfdValues);
            }
            return allHfdValues;
        }

       


        // 使用 OxyPlot 繪製每個通道的 HFD 結果（折線圖）
        private void PlotHFDResultsWithOxyPlot(List<List<double>> allHfdValues)
        {
            // 獲取起始標記時間
            double startMarkTime = GetMarkTime(0);

            // 建立主 PlotModel 對象，設定圖表標題和圖例屬性
            var plotModel = new OxyPlot.PlotModel
            {
                Title = "HFD 結果 (折線圖)",
                IsLegendVisible = true // 顯示圖例
            };

            // 添加 X 軸，標題為 "時間 (秒)"
            plotModel.Axes.Add(new OxyPlot.Axes.LinearAxis
            {
                Position = OxyPlot.Axes.AxisPosition.Bottom, // 設定 X 軸位置在底部
                Title = "時間 (秒)", // 設定 X 軸標題
                Minimum = startMarkTime // 設定 X 軸的最小值為起始標記時間
            });

            // 添加 Y 軸，標題為 "HFD 值"
            plotModel.Axes.Add(new OxyPlot.Axes.LinearAxis
            {
                Position = OxyPlot.Axes.AxisPosition.Left, // 設定 Y 軸位置在左側
                Title = "HFD 值" // 設定 Y 軸標題
            });

            // 為每個通道繪製折線圖
            for (int channelIndex = 0; channelIndex < allHfdValues.Count; channelIndex++)
            {
                // 建立 LineSeries 對象來表示每個通道的折線
                var lineSeries = new OxyPlot.Series.LineSeries
                {
                    Title = $"Channel {channelIndex + 1}", // 每個通道的標題
                    MarkerType = OxyPlot.MarkerType.Circle, // 設定標記類型為圓形
                    MarkerSize = 3, // 標記大小
                    MarkerStroke = OxyPlot.OxyColors.Black, // 標記邊框顏色為黑色
                    StrokeThickness = 2, // 折線粗細
                    Color = OxyPlot.OxyColor.FromArgb(255, (byte)(channelIndex * 50 % 255), (byte)(channelIndex * 80 % 255), (byte)(channelIndex * 110 % 255)) // 設定每個通道不同的顏色
                };

                // 添加每個通道的 HFD 值到折線圖中
                List<double> hfdValues = allHfdValues[channelIndex];
                for (int i = 0; i < hfdValues.Count; i++)
                {
                    // 將每個點的數據添加到 LineSeries 中
                    lineSeries.Points.Add(new OxyPlot.DataPoint(i * 1.0 + startMarkTime, hfdValues[i])); // X 軸以秒為單位，每 1 個點為 1 秒
                }

                // 將折線圖添加到 PlotModel 中
                plotModel.Series.Add(lineSeries);
            }

            // 建立 PlotView 以顯示主圖表
            var plotView = new OxyPlot.Wpf.PlotView
            {
                Model = plotModel, // 設定要顯示的圖表模型
                Width = 800, // 設定圖表寬度
                Height = 600 // 設定圖表高度
            };

            // 建立一個新視窗來顯示主圖表和圖例標註
            var mainWindow = new Window
            {
                Title = "HFD 結果 (折線圖和圖例標註)", // 設定視窗標題
                Width = 1000, // 設定視窗寬度以容納圖例
                Height = 800 // 設定視窗高度
            };

            // 使用 Canvas 來佈局圖表和圖例標註，允許更靈活的位置控制
            var canvas = new Canvas();

            // 添加主圖表到 Canvas
            Canvas.SetLeft(plotView, 10); // 圖表距離左側 10 pixel
            Canvas.SetTop(plotView, 10); // 圖表距離頂部 10 pixel
            canvas.Children.Add(plotView);

            // 添加標註每條線代表的通道信息
            var legendGroupBox = new GroupBox
            {
                Header = "圖例標註", // 設定圖例標註標題
                Margin = new Thickness(0), // 設定外邊距
                Padding = new Thickness(10), // 設定內邊距
                Height = 400, // 設定 GroupBox 的高度
                Width = 100// 設定 GroupBox 的寬度
            };

            var legendPanel = new StackPanel { Orientation = Orientation.Vertical }; // 垂直排列的 StackPanel 來顯示圖例標註
            for (int channelIndex = 0; channelIndex < allHfdValues.Count; channelIndex++)
            {
                // 建立 TextBlock 來顯示每個通道的圖例標註
                var legendItem = new TextBlock
                {
                    Text = $"Channel {channelIndex + 1}", // 設定通道名稱
                    Foreground = new SolidColorBrush(Color.FromArgb(255, (byte)(channelIndex * 50 % 255), (byte)(channelIndex * 80 % 255), (byte)(channelIndex * 110 % 255))), // 設定文字顏色與折線圖顏色一致
                    FontSize = 10, // 調整文字大小為 14
                    Margin = new Thickness(5) // 設定文字外邊距
                };
                legendPanel.Children.Add(legendItem); // 將圖例標註添加到圖例面板中
            }

            legendGroupBox.Content = legendPanel; // 將圖例面板設為 GroupBox 的內容

            // 添加 GroupBox 到 Canvas，並放置在主圖表的右側，與主圖表上緣對齊
            Canvas.SetLeft(legendGroupBox, 820); // 圖例距離圖表右側 10 pixel（圖表寬度 800 + 10）
            Canvas.SetTop(legendGroupBox, 10); // 圖例距離頂部 10 pixel，與主圖表上緣對齊
            canvas.Children.Add(legendGroupBox);

            mainWindow.Content = canvas; // 將 Canvas 設為主視窗的內容

            // 顯示視窗
            mainWindow.Show();
        }



        // 計算線性回歸斜率
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







        // 保存變更的標記位置至 CSV 檔案
        private void Save_Change_MarkSite(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(filePath)) return;

            // 讀取 CSV 原始數據
            var lines = File.ReadAllLines(filePath).ToList();

            // 清空所有原來的 MARK（假設 MARK 列是最後一列）
            for (int i = 1; i < lines.Count; i++) // 從第 1 行開始，因爲第 0 行可能是標題
            {
                var columns = lines[i].Split(',');

                // 將最後一列 MARK 值設爲 0
                columns[columns.Length - 1] = "0";

                // 更新行內容
                lines[i] = string.Join(",", columns);
            }

            // 收集更新後的 MARK 位置
            var updatedMarkPositions = new Dictionary<int, List<double>>();
            foreach (var plotModel in plotModels)
            {
                int plotIndex = plotModels.IndexOf(plotModel);
                var marks = plotModel.Annotations.OfType<LineAnnotation>().Where(a => a.Text == "MARK").OrderBy(a => a.X).ToList();

                updatedMarkPositions[plotIndex] = new List<double>();
                foreach (var mark in marks)
                {
                    updatedMarkPositions[plotIndex].Add(mark.X);  // 獲取每個圖表中所有的新 MARK 位置
                }
            }

            // 遍歷所有的 MARK 數據，更新 CSV 中的 MARK 列
            foreach (var kvp in updatedMarkPositions)
            {
                int index = kvp.Key;
                var markPositions = kvp.Value;

                foreach (var markPosition in markPositions)
                {
                    // 根據當前的 MARK 位置找到最接近的時間點
                    int lineIndex = (int)(markPosition * SamplingRate);
                    if (lineIndex >= 1 && lineIndex < lines.Count)
                    {
                        var columns = lines[lineIndex].Split(',');

                        // 覆蓋原來的 MARK 位置，設置爲 20
                        columns[columns.Length - 1] = "20";

                        // 更新行數據
                        lines[lineIndex] = string.Join(",", columns);
                    }
                }
            }

            // 將更新後的內容寫回 CSV 文件
            File.WriteAllLines(filePath, lines);
            CustomMessageBox _CustomMessageBox = new CustomMessageBox(6);
            _CustomMessageBox.Show();
        }

        // 切換資料檔案並載入新的資料
        private void Change_Data(object sender, RoutedEventArgs e)
        {
            //// 獲取新的路徑
            //string New_ChangeData_Path = parentDirectory + "\\" + folderComboBox_Parameter.SelectedItem + "\\" + folderComboBox_Data.SelectedItem;
            //filePath = New_ChangeData_Path;
            //// 創建新的視窗
            //Last_Window last_window = new Last_Window(New_ChangeData_Path);

            //// 顯示新視窗
            //last_window.Show();

            //// 關閉當前視窗
            //this.Close();
        }

        private void Highest_ShowBarChart_Click(object sender, RoutedEventArgs e)
        {
            Highest_Analysis last_window = new Highest_Analysis(filePath);
            last_window.Show();
        }
    }
}
