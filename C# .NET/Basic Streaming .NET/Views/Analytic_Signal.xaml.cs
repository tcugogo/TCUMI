using OxyPlot;
using OxyPlot.Series;
using OxyPlot.Wpf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;

namespace Basic_Streaming_NET.Views
{
    /// <summary>
    /// Analytic_Signal.xaml 的互動邏輯
    /// </summary>
    public partial class Analytic_Signal : UserControl, INotifyPropertyChanged
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
        Checking_Data_UserControl _Checking_Data_UserControl_panel;
        public Analytic_Signal(Checking_Data_UserControl Checking_Data_UserControl)
        {

            InitializeComponent();
            _Checking_Data_UserControl_panel = Checking_Data_UserControl;

            DataContext = this;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            LoadDataAndPlot();
        }

        private void LoadDataAndPlot()
        {
            // Load data from the CSV file
            var filePath = "C:\\Users\\Andy\\OneDrive - 慈濟大學\\桌面\\專題\\2024.6.5\\C# .NET\\Basic Streaming .NET\\bin\\Debug\\net6.0-windows10.0.17763.0\\sensor_data\\2024-05-09_16-12-45.csv"; // Update with the correct file path
            var lines = File.ReadAllLines(filePath);
            var data = lines.Skip(1)
                            .Select(line => line.Split(','))
                            .Select(values => values.Select(v => double.Parse(v)).ToArray())
                            .ToArray();

            // Determine the number of EMG data columns
            int numberOfPlots = data[0].Length;

            // Calculate PlotHeight based on the current height of the UserControl
            PlotHeight = (_Checking_Data_UserControl_panel.ActualHeight-50) / numberOfPlots;

            // Dynamically create and add PlotViews to the StackPanel
            PlotStackPanel.Children.Clear(); // Clear previous plots if any
            for (int i = 0; i < numberOfPlots; i++)
            {
                var plotModel = CreatePlotModel($"EMG {i + 1}", data, i);
                var plotView = new PlotView
                {
                    Model = plotModel,
                    Height = PlotHeight,
                    Width = 9630
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
