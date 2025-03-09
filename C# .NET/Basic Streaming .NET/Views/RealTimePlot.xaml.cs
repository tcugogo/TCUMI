using Basic_Streaming.NET;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;


namespace Basic_Streaming_NET.Views
{
    public partial class RealTimePlot : UserControl
    {

        private CancellationTokenSource _cancellationTokenSource;
        private List<SKPoint[]> sensorData = new List<SKPoint[]>();
        private const int MaxPoints = 1926 * 5;
        private double timeSinceLastReset = 0;
        private float _scaleY = 1f;
        private bool isPainting = false;
        private int plotColor;
        private DeviceStreaming _deviceStreaming;
        private SKColor lineColor;

        public float CurrentScaleY => _scaleY;
        // 這是用於追蹤 Y 軸的平移偏移量
        private float _yAxisOffset = 0;
        private bool _isDragging = false;      // 標誌當前是否正在拖動
        private float _previousMouseY = 0;     // 記錄上一次滑鼠 Y 位置
        private float dragSpeedFactor = 0.1f;  // 控制拖動速度的比例係數
        private string unit = "mV";
        private float scaleFactor = 1f;

        public RealTimePlot(int sensorIndex, DeviceStreaming dev, CancellationTokenSource cancellationTokenSource)
        {
            SKColor[] colors = new SKColor[] { SKColors.Red, SKColors.Yellow, SKColors.Green, SKColors.White };
            InitializeComponent();
            _deviceStreaming = dev;
            int line = DeviceStreaming.lines;
            
            _cancellationTokenSource = cancellationTokenSource;
            this.lineColor = colors[line % 4];
            skiaView.PaintSurface += OnPaintSurface;
            skiaView.MouseWheel += OnMouseWheel;
            skiaView.MouseDown += OnMouseDown;    // 註冊滑鼠按下事件
            skiaView.MouseMove += OnMouseMove;    // 註冊滑鼠移動事件
            skiaView.MouseUp += OnMouseUp;        // 註冊滑鼠鬆開事件
        }

        public void RefreshPlot()
        {
            if (skiaView.Dispatcher.CheckAccess())
            {
                skiaView.InvalidateVisual();
            }
            else
            {
                skiaView.Dispatcher.Invoke(() => skiaView.InvalidateVisual());
            }
        }

        public void SetScaleY(string zoom)
        {
            if (zoom == "ZoomIn")
            {
                _scaleY *= 1.5f;
                UpdateYAxisUnit();
            }
            else if(zoom == "ZoomOut")
            {
                _scaleY /= 1.5f;
                UpdateYAxisUnit();
            }
            else
            {
                _scaleY = 1f;
                UpdateYAxisUnit();
            }
            
            skiaView.InvalidateVisual();
        }
        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _isDragging = true;
                _previousMouseY = (float)e.GetPosition(skiaView).Y; // 記錄當前 Y 座標
            }
        }
        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                float currentMouseY = (float)e.GetPosition(skiaView).Y;
                float deltaY = (_previousMouseY - currentMouseY) * dragSpeedFactor; // 計算滑鼠 Y 軸的移動差異並乘以比例係數

                _yAxisOffset += deltaY; // 更新 Y 軸偏移量

                _previousMouseY = currentMouseY; // 更新上一次的滑鼠位置

                skiaView.InvalidateVisual(); // 重繪圖表
            }
        }


        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Released)
            {
                _isDragging = false; // 停止拖動
            }
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
            {
                _scaleY *= 1.1f;
            }
            else if (e.Delta < 0)
            {
                _scaleY /= 1.1f;
            }
            UpdateYAxisUnit();
            skiaView.InvalidateVisual();
        }
        private void UpdateYAxisUnit()
        {
            float maxValue = 3f / _scaleY;

            // 限制 maxValue 在一個合理範圍內，避免過大的值影響顯示
            if (maxValue > 10000f)
            {
                maxValue = 10000f;
            }

            
            else if (maxValue < 0.1f)
            {
                unit = "μV";
                scaleFactor = 1000f;
            }
            else if (maxValue < 1000f)
            {
                unit = "mV";
                scaleFactor = 1f;
            }
            else
            {
                unit = "V";
                scaleFactor = 0.001f;
            }
        }


        private int pointsCounter = 0;
        private List<SKPoint> tempDataBuffer = new List<SKPoint>();

        public void AddSensorData(SKPoint[] newPoints)
        {
            if (newPoints == null || newPoints.Length == 0)
                return;

            lock (sensorData)
            {
                var currentData = sensorData.ElementAtOrDefault(0)?.ToList() ?? new List<SKPoint>();
                currentData.AddRange(newPoints);

                if (currentData.Count > MaxPoints)
                {
                    currentData = currentData.Skip(currentData.Count - MaxPoints).ToList();
                }

                if (sensorData.Count == 0)
                {
                    sensorData.Add(currentData.ToArray());
                }
                else
                {
                    sensorData[0] = currentData.ToArray();
                }
            }

            // 強制UI線程重繪
            //skiaView.Dispatcher.Invoke(() => skiaView.InvalidateVisual(), DispatcherPriority.Render);
        }



        private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            // 防止重複繪製，確保繪圖只會被一個線程佔用
            if (isPainting) return;
            isPainting = true;

            // 暫時取消鼠標滾輪事件以防止在繪製時進行縮放
            skiaView.MouseWheel -= OnMouseWheel;

            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.Black);  // 清空畫布，背景設爲黑色

            using (var paint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1,
                IsAntialias = true,
                Color = lineColor,
            })
            {
                // 繪製 Y 軸的輔助線
                DrawYAxis(canvas, e.Info.Width, e.Info.Height);

                // 確保傳感器數據存在
                if (sensorData.Count > 0)
                {
                    var canvasWidth = e.Info.Width;
                    var canvasHeight = e.Info.Height;

                    // 設置繪圖邊距
                    float leftMargin = 100;
                    float rightMargin = 20;
                    float topMargin = canvasHeight * 0.05f;
                    float bottomMargin = canvasHeight * 0.05f;

                    var plotWidth = canvasWidth - leftMargin - rightMargin;
                    var plotHeight = canvasHeight - topMargin - bottomMargin;

                    // 調整 Y 軸縮放比例
                    var scaleYAdjusted = (plotHeight / 6f) * _scaleY;

                    // 設置 Y 軸的最小和最大值
                    float minY = -3f;
                    float maxY = 3f;

                    var points = sensorData[0];

                    // 如果數據點過多，重置繪圖
                    if (points.Length >= MaxPoints)
                    {
                        _yAxisOffset = 0;
                        DeviceStreaming.Globalreflash = true;
                        ResetPlot();
                    }

                    // 在繪製點時應用 Y 軸偏移和縮放
                    var scaledPoints = points.Select((p, index) =>
                    {
                        // 將零點固定在繪圖區域的垂直中心位置
                        float yZeroPosition = topMargin + plotHeight / 2;

                        // 計算 Y 值，應用偏移量和縮放
                        float yValue = Math.Max(minY, Math.Min(maxY, p.Y)) * scaleYAdjusted + _yAxisOffset;
                        float x = leftMargin + index * (plotWidth / (MaxPoints - 1));
                        float y = yZeroPosition - yValue;
                        return new SKPoint(x, y);
                    }).ToArray();

                    // 繪製傳感器數據點
                    canvas.DrawPoints(SKPointMode.Polygon, scaledPoints, paint);
                }
            }

            // 重新啓用鼠標滾輪事件
            skiaView.MouseWheel += OnMouseWheel;

            // 釋放繪圖鎖
            isPainting = false;
        }

        private void DrawYAxis(SKCanvas canvas, int canvasWidth, int canvasHeight)
        {
            using (var paint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2,
                Color = SKColors.Gray,
                IsAntialias = true
            })
            {
                float leftMargin = 100; // 增加左側邊距以留出更多空間用於顯示單位
                float topMargin = canvasHeight * 0.05f;
                float bottomMargin = canvasHeight * 0.05f;
                var plotHeight = canvasHeight - topMargin - bottomMargin;

                canvas.DrawLine(leftMargin, topMargin, leftMargin, canvasHeight - bottomMargin, paint);

                int numberOfTicks = 7;
                float tickSpacing = plotHeight / (numberOfTicks - 1);
                float tickLength = 10;

                float maxValue = 3f / _scaleY;

                for (int i = 0; i < numberOfTicks; i++)
                {
                    float y = topMargin + i * tickSpacing;
                    canvas.DrawLine(leftMargin, y, leftMargin + tickLength, y, paint);

                    float yValue = (maxValue - i * (2 * maxValue / (numberOfTicks - 1))) * scaleFactor;

                    using (var textPaint = new SKPaint
                    {
                        Color = SKColors.White,
                        TextSize = 12, // 增加字體大小以更好地顯示單位
                        IsAntialias = true
                    })
                    {
                        string label = $"{yValue:F1} {unit}";
                        canvas.DrawText(label, leftMargin - textPaint.MeasureText(label) - 15, y + textPaint.TextSize / 2, textPaint);
                    }
                }
            }
        }


        public void ClearPlotData()
        {
            lock (sensorData)
            {
                sensorData.Clear();
            }
            timeSinceLastReset = 0;
            skiaView.InvalidateVisual();
        }

        public void ResetPlot()
        {
            lock (sensorData)
            {
                sensorData.Clear();
            }
            timeSinceLastReset = 0;
            skiaView.InvalidateVisual();
        }
    }
}
