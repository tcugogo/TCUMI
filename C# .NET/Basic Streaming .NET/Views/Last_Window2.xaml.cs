using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Basic_Streaming_NET.Views
{
    public partial class Last_Window2 : Window
    {
        private string _patientFilePath;
        private List<PlotModel> plotModels;
        private const double SamplingRate = 1926;
        private int maxDataPoints = 19260;
        private DispatcherTimer sliderUpdateTimer;
        private bool isDraggingMark = false;
        private LineAnnotation draggingMark;
        private double initialMarkX;
        private List<List<LineAnnotation>> markAnnotations;
        private List<double> currentMarkPositions;

        public Last_Window2(string patientName)
        {
            InitializeComponent();

            _patientFilePath = patientName;

            sliderUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            sliderUpdateTimer.Tick += SliderUpdateTimer_Tick;

            markAnnotations = new List<List<LineAnnotation>>();
            currentMarkPositions = new List<double>();

            LoadDataFromCsv(_patientFilePath);
        }

        private void LoadDataFromCsv(string filePath)
        {
            var lines = File.ReadAllLines(filePath);

            // 獲取 EMG 通道數量
            int emgCount = lines[0].Split(',').Length - 1;
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

            // 初始化標記線
            var markerPositions = new List<double>();

            for (int i = 1; i < lines.Length; i++)
            {
                var parts = lines[i].Split(',');

                for (int j = 0; j < emgCount; j++)
                {
                    if (double.TryParse(parts[j], out double emgValue))
                    {
                        emgData[j][0].Points.Add(new DataPoint(i / SamplingRate, emgValue));
                    }
                }

                // 收集 MARK 點的位置
                if (parts.Length > emgCount && double.TryParse(parts[emgCount], out double markValue) && markValue > 0)
                {
                    double markPosition = i / SamplingRate;
                    markerPositions.Add(markPosition);
                    currentMarkPositions.Add(markPosition);
                }
            }

            // 添加圖表和標記
            plotModels = new List<PlotModel>();
            for (int i = 0; i < emgCount; i++)
            {
                var plotModel = new PlotModel { Title = $"Sensor {i + 1}" };
                plotModel.Series.Add(emgData[i][0]);

                var channelMarks = new List<LineAnnotation>();
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

                    // 加入事件監聽
                    lineAnnotation.MouseDown += Mark_MouseDown;
                    lineAnnotation.MouseMove += Mark_MouseMove;
                    lineAnnotation.MouseUp += Mark_MouseUp;

                    plotModel.Annotations.Add(lineAnnotation);
                    channelMarks.Add(lineAnnotation);
                }

                // 設置 X 軸
                var xAxis = new LinearAxis
                {
                    Position = AxisPosition.Bottom,
                    Minimum = 0,
                    Maximum = maxDataPoints / SamplingRate,
                    IsZoomEnabled = false,
                    IsPanEnabled = false
                };
                plotModel.Axes.Add(xAxis);

                // 設置 Y 軸
                var yAxis = new LinearAxis
                {
                    Position = AxisPosition.Left,
                    Minimum = -5.5,
                    Maximum = 5.5,
                    MajorGridlineStyle = LineStyle.Solid,
                    MinorGridlineStyle = LineStyle.Dot
                };
                plotModel.Axes.Add(yAxis);

                plotModels.Add(plotModel);
                markAnnotations.Add(channelMarks);
            }

            // 更新 UI
            PlotsContainer.ItemsSource = plotModels;
            XAxisSlider.Maximum = (lines.Length - maxDataPoints) / SamplingRate;
            XAxisSlider.Value = 0;
        }

        private void SliderUpdateTimer_Tick(object sender, EventArgs e)
        {
            sliderUpdateTimer.Stop();
            UpdatePlotViews(XAxisSlider.Value);
        }

        private void XAxisSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            sliderUpdateTimer.Stop();
            sliderUpdateTimer.Start();
            UpdatePlotViews(XAxisSlider.Value);
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
                    plotModel.InvalidatePlot(false);
                }
            }
        }

        private void ZoomInX_Click(object sender, RoutedEventArgs e)
        {
            // 放大，縮小範圍 (factor < 1)
            AdjustXAxisRange(0.9);
        }

        private void ZoomOutX_Click(object sender, RoutedEventArgs e)
        {
            // 縮小，擴大範圍 (factor > 1)
            AdjustXAxisRange(1.1);
        }

        private void AdjustXAxisRange(double factor)
        {
            foreach (var plotModel in plotModels)
            {
                var xAxis = plotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Bottom);
                if (xAxis != null)
                {
                    // 計算當前 X 軸的範圍
                    double range = xAxis.Maximum - xAxis.Minimum;

                    // 縮放範圍，放大時 factor < 1，縮小時 factor > 1
                    double newRange = range * factor;

                    // 確保縮放的範圍最小為 1 單位
                    if (newRange < 1) newRange = 1;

                    // 保持範圍的左邊界不變，右邊界依據新的範圍調整
                    double newMax = xAxis.Minimum + newRange;

                    // 限制右邊界不得超過資料的最大時間範圍
                    if (newMax > maxDataPoints / SamplingRate)
                    {
                        newMax = maxDataPoints / SamplingRate;
                        xAxis.Minimum = newMax - newRange; // 調整左邊界
                    }

                    xAxis.Maximum = newMax;

                    // 更新圖表
                    plotModel.InvalidatePlot(false);
                }
            }
            UpdateSliderRangeAndPosition();
        }

        private void UpdateSliderRangeAndPosition()
        {
            if (XAxisSlider != null)
            {
                var firstPlotModel = plotModels.FirstOrDefault();
                if (firstPlotModel != null)
                {
                    var xAxis = firstPlotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Bottom);
                    if (xAxis != null)
                    {
                        XAxisSlider.Minimum = 0;
                        XAxisSlider.Maximum = (maxDataPoints / SamplingRate) - (xAxis.Maximum - xAxis.Minimum);
                        XAxisSlider.Value = xAxis.Minimum;
                    }
                }
            }
        }

        private void Cancel_mark(object sender, RoutedEventArgs e)
        {
            foreach (var plotModel in plotModels)
            {
                var marksToRemove = plotModel.Annotations.OfType<LineAnnotation>().Where(a => a.Text == "MARK").ToList();
                foreach (var mark in marksToRemove)
                {
                    plotModel.Annotations.Remove(mark);
                }
                plotModel.InvalidatePlot(false); // 刷新圖表
            }
            currentMarkPositions.Clear();
            MessageBox.Show("所有標記已被移除。");
        }

        private void Update_mark(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_patientFilePath))
            {
                MessageBox.Show("檔案路徑無效，無法更新標記。");
                return;
            }

            var lines = File.ReadAllLines(_patientFilePath).ToList();

            // 清除原始標記數據（假設標記列是最後一列）
            for (int i = 1; i < lines.Count; i++)
            {
                var columns = lines[i].Split(',');
                columns[^1] = "0";
                lines[i] = string.Join(",", columns);
            }

            foreach (var plotModel in plotModels)
            {
                var marks = plotModel.Annotations.OfType<LineAnnotation>().Where(a => a.Text == "MARK").ToList();
                foreach (var mark in marks)
                {
                    int lineIndex = (int)(mark.X * SamplingRate);
                    if (lineIndex >= 1 && lineIndex < lines.Count)
                    {
                        var columns = lines[lineIndex].Split(',');
                        columns[^1] = "20";
                        lines[lineIndex] = string.Join(",", columns);
                    }
                }
            }

            File.WriteAllLines(_patientFilePath, lines);
            MessageBox.Show("標記已更新並保存到檔案。");
        }

        private void Mark_MouseDown(object sender, OxyMouseDownEventArgs e)
        {
            if (e.ChangedButton == OxyMouseButton.Left && sender is LineAnnotation lineAnnotation)
            {
                isDraggingMark = true;
                draggingMark = lineAnnotation;
                initialMarkX = lineAnnotation.X;
                e.Handled = true;
            }
        }

        private void Mark_MouseMove(object sender, OxyMouseEventArgs e)
        {
            if (isDraggingMark && draggingMark != null)
            {
                var plotModel = draggingMark.PlotModel;

                if (plotModel != null)
                {
                    // 使用 X 軸轉換滑鼠位置為數據點
                    var xAxis = plotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Bottom);
                    if (xAxis != null)
                    {
                        var xValue = xAxis.InverseTransform(e.Position.X);

                        // 限制標記移動範圍
                        if (xValue >= 0 && xValue <= maxDataPoints / SamplingRate)
                        {
                            draggingMark.X = xValue;  // 只更新拖動中的標記，提升流暢度
                            plotModel.InvalidatePlot(false);  // 立即刷新圖表
                        }
                    }
                }
                e.Handled = true; // 確保事件已被處理
            }
        }


        private void Mark_MouseUp(object sender, OxyMouseEventArgs e)
        {
            if (isDraggingMark)
            {
                isDraggingMark = false;

                if (draggingMark != null)
                {
                    double newX = draggingMark.X;

                    // 同步更新所有通道的相同位置的標記
                    foreach (var model in plotModels)
                    {
                        var markToMove = model.Annotations.OfType<LineAnnotation>().FirstOrDefault(a => a.Text == "MARK" && Math.Abs(a.X - initialMarkX) < 1e-6);
                        if (markToMove != null)
                        {
                            markToMove.X = newX;
                            model.InvalidatePlot(false); // 立即刷新圖表
                        }
                    }

                    draggingMark = null;
                }

                e.Handled = true;
            }
        }


        private void ZoomInY_Click(object sender, RoutedEventArgs e)
        {
            foreach (var plotModel in plotModels)
            {
                var yAxis = plotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Left);
                if (yAxis != null)
                {
                    double range = yAxis.Maximum - yAxis.Minimum;
                    double newMin = yAxis.Minimum + range * 0.1;
                    double newMax = yAxis.Maximum - range * 0.1;

                    yAxis.Minimum = newMin;
                    yAxis.Maximum = newMax;
                    plotModel.InvalidatePlot(false); // 更新圖表
                }
            }
        }

        private void ZoomOutY_Click(object sender, RoutedEventArgs e)
        {
            foreach (var plotModel in plotModels)
            {
                var yAxis = plotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Left);
                if (yAxis != null)
                {
                    double range = yAxis.Maximum - yAxis.Minimum;
                    double newMin = yAxis.Minimum - range * 0.1;
                    double newMax = yAxis.Maximum + range * 0.1;

                    yAxis.Minimum = newMin;
                    yAxis.Maximum = newMax;
                    plotModel.InvalidatePlot(false); // 更新圖表
                }
            }
        }
    }
}
