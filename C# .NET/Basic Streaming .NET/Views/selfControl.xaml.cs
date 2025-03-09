using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows;

namespace Basic_Streaming_NET.Views
{
    public partial class selfControl : UserControl
    {
        private Polyline polyline;
        private Queue<double> buffer = new Queue<double>();
        private DispatcherTimer timer;
        private int samplingRate = 1926;
        private double xScale = 1;
        private double yScale = 50;
        private double xOffset = 0;
        private double Max_range = 3, Min_range = -3;

        public selfControl()
        {
            InitializeComponent();
            InitializePlot();
        }

        private void InitializePlot()
        {
            polyline = new Polyline
            {
                Stroke = Brushes.Black,
                StrokeThickness = 1
            };
            plotCanvas.Children.Add(polyline);

            timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1.0 / samplingRate)
            };
            timer.Tick += UpdatePlot;
            timer.Start();
        }

        private void UpdatePlot(object sender, EventArgs e)
        {
            while (buffer.Count > 0)
            {
                double nextValue = buffer.Dequeue();
                Point newPoint = new Point(xOffset, (Max_range - nextValue) * yScale);
                polyline.Points.Add(newPoint);
                xOffset += xScale;

                if (xOffset > plotCanvas.ActualWidth)
                {
                    double shift = xOffset - plotCanvas.ActualWidth;
                    xOffset -= shift;
                    for (int i = 0; i < polyline.Points.Count; i++)
                    {
                        Point point = polyline.Points[i];
                        polyline.Points[i] = new Point(point.X - shift, point.Y);
                    }
                }
            }
        }

        public void UpdatePlotData(List<double> newData)
        {
            Task.Run(() =>
            {
                lock (buffer)
                {
                    foreach (double val in newData)
                    {
                        buffer.Enqueue(val);
                    }
                }
            });
        }
    }
}