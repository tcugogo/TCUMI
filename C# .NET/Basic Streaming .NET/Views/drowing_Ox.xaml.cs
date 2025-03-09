using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace Basic_Streaming_NET.Views
{
    /// <summary>
    /// drowing_Ox.xaml 的互動邏輯
    /// </summary>
    public partial class drowing_Ox : Window
    {
        private PlotModel PlotModel { get; set; }
        private Queue<double> buffer = new Queue<double>(); // 用於緩存每秒傳入的數據點
        private DispatcherTimer timer;
        private int samplingRate = 2000; // 採樣率為2000Hz
        public drowing_Ox()
        {
            InitializeComponent();

            // 初始化 PlotModel
            PlotModel = new PlotModel { Title = "Real-Time Plot" };

            // 添加初始數據序列
            var series = new LineSeries();
            PlotModel.Series.Add(series);

            // 設置 X 軸和 Y 軸
            PlotModel.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "X Axis" });
            PlotModel.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "Y Axis" });

            // 將 PlotModel 設置為 plotView 的 Model
            plotView.Model = PlotModel;

            // 初始化定時器
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1); // 設置定時器間隔為 3 秒
            timer.Tick += Timer_Tick;
            timer.Start();
        
    }

        private async void Timer_Tick(object sender, EventArgs e)
        {
            await Task.Run(() => UpdatePlotDataFromBufferAsync());
            // 等待一段時間以模擬 2000 個點在 1 秒內的延遲
            await Task.Delay(TimeSpan.FromSeconds(1.0 / 2000));
        }

        private async Task UpdatePlotDataFromBufferAsync()
        {
            var series = (LineSeries)PlotModel.Series[0];
            

            // 確認佇列中有數據點
            if (buffer.Count > 2000)
            {
                // 每秒更新一次
                int pointsToUpdatePerSecond = samplingRate;

                // 確認數據點數量不超過每秒更新的點數
                int pointsToUpdate = Math.Min(buffer.Count, pointsToUpdatePerSecond);

                for (int i = 0; i < pointsToUpdate; i++)
                {
                    // 使用 lock 來同步對佇列的訪問
                    lock (buffer)
                    {
                        // 檢查佇列是否為空
                        if (buffer.Count > 0)
                        {
                            double x = series.Points.Count > 0 ? series.Points[series.Points.Count - 1].X + 1.0 / samplingRate : 0;

                            // 從緩存中取出數據點
                            double y = buffer.Dequeue();

                            // 添加到數據序列中
                            series.Points.Add(new DataPoint(x, y));

                            // 如果數據點數量超出限制，則刪除最舊的數據點
                            if (series.Points.Count > 5 * pointsToUpdatePerSecond)
                            {
                                series.Points.RemoveAt(0);
                            }
                        }
                    }
                }

                // 強制圖表刷新
                PlotModel.InvalidatePlot(true);
            }
        }


        public void UpdatePlotData(List<double> newData)
        {
          
            // 使用 lock 來同步對佇列的訪問
            lock (buffer)
            {
                // 將新數據添加到緩存中
                foreach (var val in newData)
                {  
                    buffer.Enqueue(val);
              
                }
                
            }
        }

    }
}
