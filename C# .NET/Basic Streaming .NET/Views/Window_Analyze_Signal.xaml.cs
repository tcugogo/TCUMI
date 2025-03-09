using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using OxyPlot;
using OxyPlot.Series;
using OxyPlot.Axes;
using OxyPlot.Annotations;
using OxyPlot.Wpf;
using System.Collections.Generic;
using OxyPlot.Legends;

namespace Basic_Streaming_NET.Views
{
    public partial class Window_Analyze_Signal : Window
    {
        private string[] lines;
        private string[] originalLines; // 保存原始數據
        private bool isRMSDisplayed = false;
        private List<PlotModel> plotModels = new List<PlotModel>();
        private Dictionary<int, LineSeries> overallRmsSeriesDict = new Dictionary<int, LineSeries>();
        private List<List<double>> EMGData = new List<List<double>>();
        private Dictionary<int, List<DataPoint>> rmsDataCache = new Dictionary<int, List<DataPoint>>();
        private Dictionary<int, List<LineAnnotation>> markAnnotationsDict = new Dictionary<int, List<LineAnnotation>>(); // 存儲所有的MARK線
        private bool isDragging = false;
        private LineAnnotation currentMark = null;
        static string save_path;
        string parentDirectory;
        int sensorCount; // 儲存傳感器數量

        public Window_Analyze_Signal(string path)
        {
            InitializeComponent();
            parentDirectory = Directory.GetParent(path).FullName;
            LoadData(path);
            save_path = path;
            CreatePlots();
        }

        private void LoadData(string path)
        {
            lines = File.ReadAllLines(path).Skip(1).ToArray(); // Skip the header row
            originalLines = (string[])lines.Clone(); // 保存原始數據
            sensorCount = lines[0].Split(',').Length - 1; // 假設最後一列是標記列，其他列是傳感器數據

            for (int i = 0; i < sensorCount; i++)
            {
                EMGData.Add(new List<double>());
            }
        }

        private void CreatePlots()
        {
            PlotPanel.Children.Clear(); // Clear existing plots
            plotModels.Clear();
            overallRmsSeriesDict.Clear();
            markAnnotationsDict.Clear(); // 清除現有的MARK線

            for (int i = 0; i < sensorCount; i++)
            {
                var plotView = new PlotView
                {
                    Height = 300,
                    Width = 2000,  // Set a larger width to display all data
                    Margin = new Thickness(10),
                    MinWidth = 600,  // Set minimum width to ensure scroll bars appear
                    MinHeight = 300  // Set minimum height to ensure scroll bars appear
                };

                var model = new PlotModel { Title = $"EMG {i + 1}" };

                var xAxis = new LinearAxis
                {
                    Position = AxisPosition.Bottom,
                    Title = "Sample",
                    Minimum = 0,
                    Maximum = lines.Length,
                    AbsoluteMinimum = 0,
                    AbsoluteMaximum = lines.Length
                };
                model.Axes.Add(xAxis);

                var yAxis = new LinearAxis
                {
                    Position = AxisPosition.Left,
                    Minimum = -5,
                    Maximum = 5,
                    MajorGridlineStyle = LineStyle.Solid,
                    MinorGridlineStyle = LineStyle.Dot,
                    MajorGridlineColor = OxyColors.Gray,
                    MajorGridlineThickness = 0.5,
                    MinimumPadding = 0,
                    MaximumPadding = 0
                };
                model.Axes.Add(yAxis);

                var series = new LineSeries();
                LoadAllData(series, i);
                model.Series.Add(series);

                var rmsSeries = new LineSeries { Title = "RMS" };
                if (rmsDataCache.ContainsKey(i))
                {
                    rmsSeries.Points.AddRange(rmsDataCache[i]);
                }
                model.Series.Add(rmsSeries);
                overallRmsSeriesDict[i] = rmsSeries;

                plotModels.Add(model);
                AddMarkBackground(model, i);

                plotView.Model = model;
                plotView.Controller = new PlotController();
                plotView.Controller.UnbindAll();

                // 添加滑鼠事件
                plotView.MouseDown += PlotView_MouseDown;
                plotView.MouseMove += PlotView_MouseMove;
                plotView.MouseUp += PlotView_MouseUp;

                PlotPanel.Children.Add(plotView);
            }
        }

        private void LoadAllData(LineSeries series, int sensorIndex)
        {
            EMGData[sensorIndex].Clear();
            for (int i = 0; i < lines.Length; i++)
            {
                var values = lines[i].Split(',');
                if (double.TryParse(values[sensorIndex], out double value))
                {
                    series.Points.Add(new DataPoint(i, value));
                    EMGData[sensorIndex].Add(value);
                }
            }
        }

        private void AddMarkBackground(PlotModel model, int sensorIndex)
        {
            var markAnnotations = new List<LineAnnotation>();

            for (int i = 0; i < lines.Length; i++)
            {
                var values = lines[i].Split(',');
                if (int.TryParse(values[sensorCount], out int mark) && mark > 0) // 假設最後一列是標記列
                {
                    var area = new LineAnnotation
                    {
                        Type = LineAnnotationType.Vertical,
                        X = i,
                        Color = OxyColors.Red, // Set the color of the MARK line
                        LineStyle = LineStyle.Solid,
                        StrokeThickness = 2, // Set the thickness of the MARK line
                        Text = $"Mark {mark}", // Set the text of the MARK line
                        TextColor = OxyColors.Red
                    };
                    model.Annotations.Add(area);
                    markAnnotations.Add(area); // 保存MARK線
                }
            }
            markAnnotationsDict[sensorIndex] = markAnnotations;
        }

        private void PlotView_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var plotView = sender as PlotView;
            var model = plotView.Model;
            var position = e.GetPosition(plotView);
            var screenPoint = new ScreenPoint(position.X, position.Y);
            var dataPoint = InverseTransform(screenPoint, plotView);

            foreach (var markAnnotations in markAnnotationsDict.Values)
            {
                var nearestMark = markAnnotations.OrderBy(mark => Math.Abs(mark.X - dataPoint.X)).FirstOrDefault();
                if (nearestMark != null && Math.Abs(nearestMark.X - dataPoint.X) < 20) // 增大點擊範圍
                {
                    currentMark = nearestMark;
                    break;
                }
            }

            if (currentMark != null)
            {
                isDragging = true;
                plotView.CaptureMouse();
            }
        }

        private void PlotView_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (isDragging && currentMark != null)
            {
                var plotView = sender as PlotView;
                var position = e.GetPosition(plotView);
                var screenPoint = new ScreenPoint(position.X, position.Y);
                var dataPoint = InverseTransform(screenPoint, plotView);

                int markIndex = -1;
                foreach (var markAnnotations in markAnnotationsDict.Values)
                {
                    markIndex = markAnnotations.IndexOf(currentMark);
                    if (markIndex != -1)
                        break;
                }

                if (markIndex != -1)
                {
                    foreach (var markAnnotations in markAnnotationsDict.Values)
                    {
                        if (markIndex < markAnnotations.Count)
                        {
                            markAnnotations[markIndex].X = dataPoint.X;
                        }
                    }

                    foreach (var plotModel in plotModels)
                    {
                        plotModel.InvalidatePlot(false);
                    }
                }
            }
        }

        private void PlotView_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (isDragging)
            {
                var plotView = sender as PlotView;
                plotView.ReleaseMouseCapture();
                isDragging = false;
                currentMark = null;
            }
        }

        private DataPoint InverseTransform(ScreenPoint screenPoint, PlotView plotView)
        {
            var xAxis = plotView.Model.Axes.FirstOrDefault(a => a.Position == AxisPosition.Bottom);
            var yAxis = plotView.Model.Axes.FirstOrDefault(a => a.Position == AxisPosition.Left);

            if (xAxis != null && yAxis != null)
            {
                double x = xAxis.InverseTransform(screenPoint.X);
                double y = yAxis.InverseTransform(screenPoint.Y);
                return new DataPoint(x, y);
            }
            return new DataPoint(double.NaN, double.NaN);
        }

        private void RMS_Click(object sender, RoutedEventArgs e)
        {
            RMS();
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

                for (int i = 0; i < plotModels.Count; i++)
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
                        var dataPoint = new DataPoint(j, rms);
                        rmsSeries.Points.Add(dataPoint);

                        // 保存 RMS 數據到緩存
                        if (!rmsDataCache.ContainsKey(i))
                        {
                            rmsDataCache[i] = new List<DataPoint>();
                        }
                        rmsDataCache[i].Add(dataPoint);
                    }

                    model.InvalidatePlot(true); // 確保重新繪製圖表
                }
                isRMSDisplayed = true;
            }
        }

        private void Only_Save_Mark_Data_Click(object sender, RoutedEventArgs e)
        {
            Name_RestoreOriginal.IsEnabled = true;
            Name_Only_Save_Mark_Data.IsEnabled = false;

            // 保存原始數據行數
            int originalLineCount = lines.Length;

            // 保存Mark數據
            SaveMarkData();

            // 獲取Mark的位置
            int firstMarkIndex = GetMarkIndex(0);
            int secondMarkIndex = GetMarkIndex(1);

            // 重新創建圖表，顯示保存後的數據，並確保Mark線位置正確
            CreatePlots();

            // 調整Mark線的位置，使其在新的範圍內顯示
            foreach (var plotModel in plotModels)
            {
                var markAnnotations = plotModel.Annotations.OfType<LineAnnotation>().ToList();
                if (markAnnotations.Count > 0)
                {
                    markAnnotations[0].X = 0;
                }
                if (markAnnotations.Count > 1)
                {
                    markAnnotations[1].X = lines.Length - 1;
                }
                plotModel.InvalidatePlot(false); // 確保重新繪製圖表
            }
        }

        private void SaveMarkData()
        {
            int firstMarkIndex = GetMarkIndex(0);
            int secondMarkIndex = GetMarkIndex(1);

            if (firstMarkIndex != -1 && secondMarkIndex != -1 && firstMarkIndex != secondMarkIndex)
            {
                var newLines = lines.Skip(firstMarkIndex).Take(secondMarkIndex - firstMarkIndex + 1).ToArray();
                lines = newLines;

                for (int i = 0; i < EMGData.Count; i++)
                {
                    EMGData[i] = EMGData[i].Skip(firstMarkIndex).Take(secondMarkIndex - firstMarkIndex + 1).ToList();
                }

                foreach (var key in rmsDataCache.Keys.ToList())
                {
                    var originalRmsData = rmsDataCache[key];
                    rmsDataCache[key] = originalRmsData
                        .Where(p => p.X >= firstMarkIndex && p.X <= secondMarkIndex)
                        .Select(p => new DataPoint(p.X - firstMarkIndex, p.Y))
                        .ToList();
                }

                UpdateMarkAnnotations(firstMarkIndex, secondMarkIndex);
            }
            else
            {
                MessageBox.Show("無法找到有效的標記位置，請確保已正確設置標記。");
            }
        }

        private void UpdateMarkAnnotations(int firstMarkIndex, int secondMarkIndex)
        {
            foreach (var key in markAnnotationsDict.Keys)
            {
                var markAnnotations = markAnnotationsDict[key];
                if (markAnnotations.Count > 0)
                {
                    markAnnotations[0].X = 0;
                }
                if (markAnnotations.Count > 1)
                {
                    markAnnotations[1].X = lines.Length - 1;
                }
            }

            foreach (var plotModel in plotModels)
            {
                plotModel.InvalidatePlot(false);
            }
        }

        private void ZoomInX_Click(object sender, RoutedEventArgs e)
        {
            ZoomAxis(AxisPosition.Bottom, 0.8);
        }

        private void ZoomOutX_Click(object sender, RoutedEventArgs e)
        {
            ZoomAxis(AxisPosition.Bottom, 1.2);
        }

        private void ZoomInY_Click(object sender, RoutedEventArgs e)
        {
            ZoomAxis(AxisPosition.Left, 0.8);
        }

        private void ZoomOutY_Click(object sender, RoutedEventArgs e)
        {
            ZoomAxis(AxisPosition.Left, 1.2);
        }

        private void ZoomAxis(AxisPosition position, double factor)
        {
            foreach (var model in plotModels)
            {
                var axis = model.Axes.FirstOrDefault(a => a.Position == position);
                if (axis != null)
                {
                    double range = axis.ActualMaximum - axis.ActualMinimum;
                    double newRange = range * factor;
                    double midPoint = (axis.ActualMaximum + axis.ActualMinimum) / 2;
                    axis.Zoom(midPoint - newRange / 2, midPoint + newRange / 2);
                    model.InvalidatePlot(false);
                }
            }
        }

        private void RestoreOriginal_Click(object sender, RoutedEventArgs e)
        {
            Name_RestoreOriginal.IsEnabled = false;
            Name_Only_Save_Mark_Data.IsEnabled = true;
            lines = (string[])originalLines.Clone();
            LoadData(save_path);

            rmsDataCache.Clear();
            isRMSDisplayed = false;

            // 不創建圖表，只是恢復數據
            // CreatePlots();
            // CalculateAndCompareArea(); // 重新計算和顯示比例
        }

        private void bar_chart_Click(object sender, RoutedEventArgs e)
        {
            CompareEMGWithOthers();
        }

        private void CompareEMGWithOthers()
        {
            var files = Directory.GetFiles(parentDirectory, "*.csv");
            if (files.Length == 0)
            {
                MessageBox.Show("No CSV files found in the directory.");
                return;
            }

            var comparisonResults = new Dictionary<string, List<double>>();
            int maxSensorCount = 0;

            // 對每個文件的所有EMG數據進行RMS計算並積分
            foreach (var file in files)
            {
                var fileData = LoadFileData(file, out int sensorCount);
                maxSensorCount = Math.Max(maxSensorCount, sensorCount);

                for (int i = 0; i < sensorCount; i++)
                {
                    var rmsData = CalculateRMS(fileData[i]);
                    var area = CalculateArea(rmsData);

                    if (!comparisonResults.ContainsKey(Path.GetFileName(file)))
                    {
                        comparisonResults[Path.GetFileName(file)] = new List<double>();
                    }
                    comparisonResults[Path.GetFileName(file)].Add(area);
                }
            }

            // 顯示長條圖
            ShowBarChart(comparisonResults, maxSensorCount);
        }

        private List<List<double>> LoadFileData(string path, out int sensorCount)
        {
            var data = new List<List<double>>();
            var lines = File.ReadAllLines(path).Skip(1).ToArray(); // Skip header row
            sensorCount = lines[0].Split(',').Length - 1; // 假設最後一列是標記列

            for (int i = 0; i < sensorCount; i++)
            {
                data.Add(new List<double>());
            }

            foreach (var line in lines)
            {
                var values = line.Split(',');

                for (int i = 0; i < sensorCount; i++)
                {
                    if (double.TryParse(values[i], out double value))
                    {
                        data[i].Add(value);
                    }
                }
            }

            return data;
        }

        private List<double> CalculateRMS(List<double> data)
        {
            var rmsData = new List<double>();
            int intervalPoints = (int)(1926 * 0.1); // 0.1 seconds interval at 1926 Hz sample rate

            for (int i = 0; i < data.Count; i += intervalPoints)
            {
                int segmentEnd = Math.Min(i + intervalPoints, data.Count);
                double sumSquares = 0;
                int count = 0;

                for (int j = i; j < segmentEnd; j++)
                {
                    sumSquares += data[j] * data[j];
                    count++;
                }

                double rms = Math.Sqrt(sumSquares / count);
                rmsData.Add(rms);
            }

            return rmsData;
        }

        private double CalculateArea(List<double> rmsData)
        {
            // 使用數值積分方法計算面積
            double area = 0.0;
            for (int i = 0; i < rmsData.Count; i++)
            {
                area += rmsData[i] * 0.1; // 假設每個間隔為0.1秒
            }
            return area;
        }

        private void ShowBarChart(Dictionary<string, List<double>> comparisonResults, int maxSensorCount)
        {
            var barModel = new PlotModel { Title = "EMG RMS Area Comparison" };
            var categoryAxis = new CategoryAxis { Position = AxisPosition.Left, Title = "Files" };
            var valueAxis = new LinearAxis { Position = AxisPosition.Bottom, Title = "Area" };

            barModel.Axes.Add(categoryAxis);
            barModel.Axes.Add(valueAxis);

            for (int i = 0; i < maxSensorCount; i++)
            {
                var barSeries = new BarSeries { Title = $"EMG{i + 1}" };

                foreach (var result in comparisonResults)
                {
                    if (i < result.Value.Count)
                    {
                        barSeries.Items.Add(new BarItem { Value = result.Value[i] });
                    }
                }

                barModel.Series.Add(barSeries);
            }

            barModel.IsLegendVisible = true;

            var legend = new Legend
            {
                LegendPlacement = LegendPlacement.Inside,
                LegendPosition = LegendPosition.TopRight,
                LegendBackground = OxyColors.White,
                LegendBorder = OxyColors.Black
            };

            barModel.Legends.Add(legend);

            var barWindow = new Window
            {
                Title = "EMG RMS Area Comparison",
                Content = new PlotView { Model = barModel },
                Width = 800,
                Height = 600
            };

            barWindow.Show();
        }






        private void CalculateAndCompareArea_Click(object sender, RoutedEventArgs e)
        {
            CalculateAndCompareArea();
        }

        private void CalculateAndCompareArea()
        {
            var areaDict = new Dictionary<int, double>();

            int firstMarkIndex = GetMarkIndex(0);
            int secondMarkIndex = GetMarkIndex(1);

            if (firstMarkIndex != -1 && secondMarkIndex != -1)
            {
                foreach (var kvp in overallRmsSeriesDict)
                {
                    int index = kvp.Key;
                    var series = kvp.Value;

                    double area = series.Points
                        .Where(p => p.X >= firstMarkIndex && p.X <= secondMarkIndex)
                        .Sum(p => p.Y);

                    areaDict[index] = area;
                }
            }

            ShowPieChart(areaDict);
        }

        private void ShowPieChart(Dictionary<int, double> areaDict)
        {
            var pieModel = new PlotModel { Title = "RMS 面積比較" };

            double totalArea = areaDict.Values.Sum();
            var pieSeries = new PieSeries
            {
                InsideLabelFormat = "{0}: {1:.00} ({2:.00}%)",
                OutsideLabelFormat = "{0}: {1:.00} ({2:.00}%)"
            };

            foreach (var kvp in areaDict)
            {
                double v = Math.Round(kvp.Value, 2); // 將值四捨五入到小數點後兩位
                double percentage = (v / totalArea) * 100;
                pieSeries.Slices.Add(new PieSlice($"EMG {kvp.Key + 1}", v) { IsExploded = true });
            }

            pieModel.Series.Add(pieSeries);

            var pieWindow = new Window
            {
                Title = "RMS 面積比較",
                Content = new PlotView { Model = pieModel },
                Width = 400,
                Height = 400
            };

            pieWindow.Show();
        }

        private int GetMarkIndex(int markNumber)
        {
            var uniqueMarks = markAnnotationsDict.Values
                .SelectMany(marks => marks)
                .GroupBy(m => m.X)
                .Select(g => g.First())
                .OrderBy(m => m.X)
                .ToList();

            if (markNumber < uniqueMarks.Count)
            {
                return (int)uniqueMarks[markNumber].X;
            }
            return -1;
        }
    }
}
