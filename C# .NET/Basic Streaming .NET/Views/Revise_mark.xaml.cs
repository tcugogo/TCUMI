using System.Windows.Controls;
using OxyPlot.Annotations;
using OxyPlot;
using System.Windows.Input;
using System;
using OxyPlot.Wpf;
using System.IO;
using OxyPlot.Series;

namespace Basic_Streaming_NET.Views
{
    public partial class Revise_mark : UserControl
    {
        private PlotModel plotModel;
        private LineAnnotation markAnnotation;

        public Revise_mark(Basic_Streaming.NET.MainWindow mainWindow)
        {
            InitializeComponent();
            InitializeChart();
        }

        private void InitializeChart()
        {
            plotModel = new PlotModel { Title = "EMG Data with Mark" };

            // 創建三個用於EMG數據的序列
            var seriesEMG1 = new LineSeries { Title = "EMG 1" };
            var seriesEMG2 = new LineSeries { Title = "EMG 2" };
            var seriesEMG3 = new LineSeries { Title = "EMG 3" };

            // 添加序列到模型
            plotModel.Series.Add(seriesEMG1);
            plotModel.Series.Add(seriesEMG2);
            plotModel.Series.Add(seriesEMG3);
            markAnnotation = new LineAnnotation
            {
                Type = LineAnnotationType.Vertical,
                Color = OxyColors.Red,
                X = 0 // Initial position
            };
            plotModel.Annotations.Add(markAnnotation);

            LoadDataFromFile();
            PlotView.Model = plotModel;
            PlotView.MouseDown += Chart_MouseDown;
            PlotView.MouseMove += Chart_MouseMove;
        }
        public void AddData(int index, double emg1, double emg2, double emg3)
        {
            ((LineSeries)plotModel.Series[0]).Points.Add(new DataPoint(index, emg1));
            ((LineSeries)plotModel.Series[1]).Points.Add(new DataPoint(index, emg2));
            ((LineSeries)plotModel.Series[2]).Points.Add(new DataPoint(index, emg3));

            plotModel.InvalidatePlot(true); // 刷新圖表以顯示新的數據點
        }
        private void Chart_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var pt = PlotView.Model.DefaultXAxis.InverseTransform(e.GetPosition(PlotView).X);
            if (Math.Abs(pt - markAnnotation.X) < 0.1) // Check if click is near the mark
            {
                markAnnotation.X = pt;
                PlotView.Model.InvalidatePlot(false);
            }
        }

        private void Chart_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var pt = PlotView.Model.DefaultXAxis.InverseTransform(e.GetPosition(PlotView).X);
                markAnnotation.X = pt;
                PlotView.Model.InvalidatePlot(false);
            }
        }
        private void LoadDataFromFile()
        {
            string filePath = @"C:\Users\TCUMI\Downloads\C# .NET (5)\C# .NET\Basic Streaming .NET\bin\Debug\net6.0-windows10.0.17763.0\sensor_data\2024-06-05_15-31-51.csv";
            if (File.Exists(filePath))
            {
                using (var reader = new StreamReader(filePath))
                {
                    string headerLine = reader.ReadLine(); // Skip header
                    int index = 0; // Initialize an index for the X-axis
                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine();
                        var values = line.Split(',');
                        if (values.Length >= 4 && double.TryParse(values[0], out double emg1) &&
                            double.TryParse(values[1], out double emg2) && double.TryParse(values[2], out double emg3) &&
                            double.TryParse(values[3], out double mark))
                        {
                            ((LineSeries)plotModel.Series[0]).Points.Add(new DataPoint(index, emg1));
                            ((LineSeries)plotModel.Series[1]).Points.Add(new DataPoint(index, emg2));
                            ((LineSeries)plotModel.Series[2]).Points.Add(new DataPoint(index, emg3));

                            if (mark != 0)
                            {
                                var annotation = new LineAnnotation
                                {
                                    Type = LineAnnotationType.Vertical,
                                    X = index,
                                    Color = OxyColors.Green
                                };
                                plotModel.Annotations.Add(annotation);
                            }
                            index++; // Increment index for the next data point
                        }
                    }
                    plotModel.InvalidatePlot(true);
                }
            }
            else
            {
                Console.WriteLine("File not found: " + filePath);
            }
        }
    }
}