using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Forms;
using MessageBox = System.Windows.Forms.MessageBox;
using UserControl = System.Windows.Controls.UserControl;
using Path = System.IO.Path;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Basic_Streaming_NET.Views
{
    public partial class EMGChartUserControl : UserControl
    {
        private List<List<double>> EMGData = new List<List<double>>();
        private ObservableCollection<PlotModel> plotModels = new ObservableCollection<PlotModel>();
        private List<string> activeEMGs = new List<string>();
        private int windowSize = 10000;

        private Dictionary<int, LineSeries> rmsSeriesDict = new Dictionary<int, LineSeries>();
        private Dictionary<int, LineSeries> overallRmsSeriesDict = new Dictionary<int, LineSeries>();
        private bool isRMSDisplayed = false;

        Checking_Data_UserControl _Checking_Data_UserControl_panel;
        double start;
        double end;

        public EMGChartUserControl(Checking_Data_UserControl Checking_Data_UserControl_panel, string selection_dirPath)
        {
            InitializeComponent();

            _Checking_Data_UserControl_panel = Checking_Data_UserControl_panel;
            LoadAllCsvFiles(selection_dirPath);

            PlotAndSliderGrid.PreviewMouseWheel += PlotAndSliderGrid_PreviewMouseWheel;
            this.KeyDown += UserControl_KeyDown;

            PlotItemsControl_1.ItemsSource = plotModels;
        }

        private void PlotAndSliderGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            {
                if (e.Delta > 0)
                {
                    AdjustYAxisScale(0.8);
                }
                else if (e.Delta < 0)
                {
                    AdjustYAxisScale(1.25);
                }
            }
            else
            {
                foreach (var model in plotModels)
                {
                    var xAxis = model.Axes.FirstOrDefault(a => a.Position == AxisPosition.Bottom);
                    if (xAxis != null)
                    {
                        double range = xAxis.Maximum - xAxis.Minimum;
                        double delta = range * 0.1 * (e.Delta > 0 ? -1 : 1);

                        xAxis.Minimum += delta;
                        xAxis.Maximum += delta;

                        xAxis.Minimum = Math.Max(xAxis.Minimum, 0);
                        xAxis.Maximum = Math.Min(xAxis.Maximum, EMGData[0].Count - 1);

                        // 只更新 X 軸範圍，不重繪所有數據
                        model.InvalidatePlot(false);
                    }
                }
            }

            e.Handled = true;
        }

        public async void LoadAllCsvFiles(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                MessageBox.Show($"Directory not found: {directoryPath}");
                return;
            }

            var csvFiles = Directory.GetFiles(directoryPath, "*.csv");
            if (csvFiles.Length == 0)
            {
                MessageBox.Show($"No CSV files found in: {directoryPath}");
                return;
            }

            foreach (var filePath in csvFiles)
            {
                await LoadData(filePath);
            }
        }

        public async Task LoadData(string filePath)
        {
            var data = new List<List<double>>();
            for (int i = 0; i < 8; i++)
            {
                data.Add(new List<double>());
            }

            var lines = await Task.Run(() => File.ReadAllLines(filePath));
            var headers = lines.First().Split(',');

            var emgIndex = new List<int>();
            for (int i = 0; i < headers.Length; i++)
            {
                if (headers[i].StartsWith("EMG"))
                {
                    emgIndex.Add(i);
                }
            }

            foreach (var line in lines.Skip(1))
            {
                var parts = line.Split(',');
                for (int i = 0; i < emgIndex.Count; i++)
                {
                    if (parts.Length > emgIndex[i] && double.TryParse(parts[emgIndex[i]], out double value))
                    {
                        data[i].Add(value);
                    }
                }
            }

            EMGData.AddRange(data);

            Slider.Maximum = Math.Max(0, EMGData[0].Count - windowSize);
            Slider.Value = 0;

            InitializePlotModel(data, Path.GetFileName(filePath));
        }

        private void InitializePlotModel(List<List<double>> data, string fileName)
        {
            var model = new PlotModel
            {
                Title = fileName,
                Background = OxyColors.Black,
                TextColor = OxyColors.Gray,
                PlotAreaBorderColor = OxyColors.White,
                PlotMargins = new OxyThickness(0), // 減少繪圖區的外邊距
                Padding = new OxyThickness(30, 5, 5, 25) // 設置繪圖區的內邊距
            };

            for (int i = 0; i < data.Count; i++)
            {
                var series = new LineSeries
                {
                    Color = OxyColors.White,
                    StrokeThickness = 0.3
                };
                series.Points.AddRange(data[i].Select((y, x) => new DataPoint(x, y)));
                series.TrackerFormatString = "{0}\n{1}: {2}\n{3}: {4:F2}";
                model.Series.Add(series);
            }

            var xAxis = new LinearAxis
            {
                Position = AxisPosition.Bottom,
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                Minimum = 0,
                Maximum = windowSize,
                IsZoomEnabled = false,
                IsPanEnabled = false,
                LabelFormatter = val => (val / 1926).ToString("F2") // 顯示/1926的值
            };

            var yAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = OxyColors.Gray, // 設置 Y 軸的主要水平線顏色為灰色
                MajorGridlineThickness = 0.5, // 設置主要網格線的粗細
                MinimumPadding = 0,
                MaximumPadding = 0
            };

            model.Axes.Add(xAxis);
            model.Axes.Add(yAxis);

            plotModels.Add(model);
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdatePlot((int)e.NewValue);
        }

        private void UpdatePlot(int startIndex)
        {
            startIndex = Math.Max(0, Math.Min(startIndex, EMGData[0].Count - 1));
            var endIndex = Math.Min(startIndex + windowSize, EMGData[0].Count);

            foreach (var model in plotModels)
            {
                var xAxis = model.Axes.FirstOrDefault(a => a.Position == AxisPosition.Bottom);
                if (xAxis != null)
                {
                    xAxis.Minimum = startIndex;
                    xAxis.Maximum = endIndex;

                    // 只更新 X 軸範圍，不重繪所有數據
                    model.InvalidatePlot(false);
                }
            }

            Slider.Maximum = Math.Max(0, EMGData[0].Count - windowSize);
            Slider.Value = startIndex;
        }

        public void CalculateAndPlotRMS()
        {
            start = 0;
            end = EMGData[0].Count;

            int startIndex = (int)Math.Round(start);
            int endIndex = (int)Math.Round(end);

            if (startIndex > endIndex)
            {
                (startIndex, endIndex) = (endIndex, startIndex);
            }

            double interval = 0.1; // 0.1秒
            int sampleRate = 1926; // 每秒的樣本數
            int intervalPoints = (int)(sampleRate * interval);

            foreach (var rmsSeries in rmsSeriesDict.Values)
            {
                rmsSeries.Points.Clear();
            }

            for (int i = 0; i < activeEMGs.Count; i++)
            {
                var model = plotModels[i];
                var rmsSeries = rmsSeriesDict[i];

                for (int j = startIndex; j <= endIndex; j += intervalPoints)
                {
                    int segmentEnd = Math.Min(j + intervalPoints, endIndex);

                    double sumSquares = 0;
                    int count = 0;

                    for (int k = j; k < segmentEnd; k++)
                    {
                        if (k < EMGData[i].Count)
                        {
                            sumSquares += EMGData[i][k] * EMGData[i][k];
                            count++;
                        }
                    }

                    double rms = Math.Sqrt(sumSquares / count);
                    rmsSeries.Points.Add(new DataPoint(j, rms));
                }

                model.InvalidatePlot(true);
            }
        }

        public void RMS()
        {
            if (isRMSDisplayed)
            {
                foreach (var rmsSeries in overallRmsSeriesDict.Values)
                {
                    rmsSeries.Points.Clear();
                }
                isRMSDisplayed = false;

                // 強制重繪所有圖表
                foreach (var model in plotModels)
                {
                    model.InvalidatePlot(true);
                }
            }
            else
            {
                double interval = 0.1; // 0.1秒
                int sampleRate = 1926; // 每秒的樣本數
                int intervalPoints = (int)(sampleRate * interval);

                for (int i = 0; i < activeEMGs.Count; i++)
                {
                    var model = plotModels[i];
                    var rmsSeries = overallRmsSeriesDict[i];
                    rmsSeries.Points.Clear();

                    for (int j = 0; j < EMGData[i].Count; j += intervalPoints)
                    {
                        int segmentEnd = Math.Min(j + intervalPoints, EMGData[i].Count);

                        double sumSquares = 0;
                        int count = 0;

                        for (int k = j; k < segmentEnd; k++)
                        {
                            sumSquares += EMGData[i][k] * EMGData[i][k];
                            count++;
                        }

                        double rms = Math.Sqrt(sumSquares / count);
                        rmsSeries.Points.Add(new DataPoint(j, rms));
                    }

                    model.InvalidatePlot(true); // 確保重新繪製圖表
                }
                isRMSDisplayed = true;
            }
        }

        public void ZoomInButton_Click()
        {
            AdjustYAxisScale(0.8);
        }
        public void ZoomOutButton_Click()
        {
            AdjustYAxisScale(1.25);
        }

        private void AdjustYAxisScale(double zoomFactor)
        {
            foreach (var model in plotModels)
            {
                var yAxis = model.Axes.FirstOrDefault(a => a.Position == AxisPosition.Left);
                if (yAxis != null)
                {
                    double newMaximum = yAxis.Maximum * zoomFactor;
                    double newMinimum = yAxis.Minimum * zoomFactor;

                    // 限制最大和最小範圍
                    yAxis.Maximum = Math.Min(newMaximum, 6);
                    yAxis.Minimum = Math.Max(newMinimum, -6);

                    // 根據新的最大值調整MajorStep
                    if (yAxis.Maximum > 5.5)
                    {
                        yAxis.MajorStep = 5.5;
                    }
                    else if (yAxis.Maximum > 4)
                    {
                        yAxis.MajorStep = 4;
                    }
                    else if (yAxis.Maximum > 3)
                    {
                        yAxis.MajorStep = 3;
                    }
                    else if (yAxis.Maximum > 2)
                    {
                        yAxis.MajorStep = 2;
                    }
                    else if (yAxis.Maximum > 1)
                    {
                        yAxis.MajorStep = 1;
                    }
                    else if (yAxis.Maximum > 0.5)
                    {
                        yAxis.MajorStep = 0.5;
                    }
                    else
                    {
                        yAxis.MajorStep = 0.1; // 預設的最小步長
                    }

                    model.InvalidatePlot(true);
                }
            }
        }

        private void UserControl_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            {
                if (e.Key == Key.Up)
                {
                    AdjustXAxisScale(0.8);
                }
                else if (e.Key == Key.Down)
                {
                    AdjustXAxisScale(1.25);
                }
            }
        }

        private void AdjustXAxisScale(double zoomFactor)
        {
            foreach (var model in plotModels)
            {
                var xAxis = model.Axes.FirstOrDefault(a => a.Position == AxisPosition.Bottom);
                if (xAxis != null)
                {
                    double range = xAxis.Maximum - xAxis.Minimum;
                    double newRange = range * zoomFactor;

                    newRange = Math.Clamp(newRange, 1, EMGData[0].Count);

                    double midpoint = (xAxis.Maximum + xAxis.Minimum) / 2;
                    double newMin = Math.Max(0, midpoint - newRange / 2);
                    double newMax = Math.Min(EMGData[0].Count, midpoint + newRange / 2);

                    xAxis.Minimum = newMin;
                    xAxis.Maximum = newMax;

                    windowSize = (int)newRange;

                    model.InvalidatePlot(true);
                }
            }

            UpdatePlot((int)plotModels.First().Axes.First(a => a.Position == AxisPosition.Bottom).Minimum);
            Slider.Maximum = Math.Max(0, EMGData[0].Count - windowSize);
            Slider.Value = (int)plotModels.First().Axes.First(a => a.Position == AxisPosition.Bottom).Minimum;
        }

        public double findintegral()
        {
            double integral = 0.0;
            int startIndex = (int)Math.Round(start);
            int endIndex = (int)Math.Round(end);

            // 確保索引有效
            if (startIndex < 0 || endIndex > EMGData[0].Count || startIndex >= endIndex)
            {
                throw new ArgumentOutOfRangeException("索引範圍無效");
            }

            for (int i = 0; i < EMGData[1].Count; i++)
            {
                integral += Math.Abs(EMGData[1][i]);
            }

            return integral;
        }
    }
}
