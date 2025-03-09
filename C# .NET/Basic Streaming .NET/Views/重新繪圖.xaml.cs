using OxyPlot;
using OxyPlot.Series;
using OxyPlot.Wpf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.ComponentModel;

namespace Basic_Streaming_NET.Views
{
    public partial class 重新繪圖 : Window, INotifyPropertyChanged
    {
        private double plotHeight;
        public double PlotHeight
        {
            get { return plotHeight; }
            set
            {
                plotHeight = value;
                OnPropertyChanged(nameof(PlotHeight));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public 重新繪圖()
        {
            InitializeComponent();
            DataContext = this;
            LoadDataAndPlot();
        }

        private void LoadDataAndPlot()
        {
            // Load data from the CSV file
            var filePath = "D:\\Program\\大三下\\專題\\Data\\SCS_on-1.csv"; // Update with the correct file path
            var lines = File.ReadAllLines(filePath);
            var data = lines.Skip(1)
                            .Select(line => line.Split(','))
                            .Select(values => values.Select(v => double.Parse(v)).ToArray())
                            .ToArray();

            // Determine the number of EMG data columns
            int numberOfPlots = data[0].Length;

            // Calculate PlotHeight based on the number of plots
            PlotHeight = 800.0 / numberOfPlots;

            // Dynamically create and add PlotViews to the StackPanel
            for (int i = 0; i < numberOfPlots; i++)
            {
                var plotModel = CreatePlotModel($"EMG {i + 1}", data, i);
                var plotView = new PlotView
                {
                    Model = plotModel,
                    Height = PlotHeight,
                    Width = 1600
                };
                PlotStackPanel.Children.Add(plotView);
            }
        }

        private PlotModel CreatePlotModel(string title, double[][] data, int columnIndex)
        {
            var plotModel = new PlotModel { /*Title = title*/ };
            var series = new LineSeries { Title = title };
            for (int i = 0; i < data.Length; i++)
            {
                series.Points.Add(new DataPoint(i, data[i][columnIndex]));
            }
            plotModel.Series.Add(series);
            return plotModel;
        }

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
