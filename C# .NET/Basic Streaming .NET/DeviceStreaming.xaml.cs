using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Aero.PipeLine;
using Basic_Streaming.NET.Views;
using Basic_Streaming_NET.Views;
using DelsysAPI.Components.TrignoRf;
using DelsysAPI.DelsysDevices;
using DelsysAPI.Events;
using DelsysAPI.Exceptions;
using DelsysAPI.Pipelines;
using DelsysAPI.Utils;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using SkiaSharp;
using Basic_Streaming.NET.Models;
using Basic_Streaming_NET.Models;
using DelsysAPI.Transforms;
using ScottPlot.Palettes;
using Org.BouncyCastle.Asn1.Esf;
using System.Windows.Input;
namespace Basic_Streaming.NET
{
    public partial class DeviceStreaming : UserControl
    {
        public static bool Globalreflash { get; set; }
        //mycode
        string pathTopatient = "";
        string shans_path = "";
        public PlotModel PlotModel { get; set; }
        private RealTimePlot _realTimePlot;
        private List<RealTimePlot> realTimePlots = new List<RealTimePlot>();
        private List<int> _lastDataPointCounts = new List<int>();
        private int maxDataPointsPerExport = 1000; // 每次匯出的最大資料點數
        private int dataPointsCollected = 0; // 已收集的資料點數
        private bool Enter_mark = false;
        private double Mark = 0;
        private string csvFilePath; // CSV檔位置
        private StreamWriter csvWriter; // 創建或打開CSV文件用於追加數據
        private System.Timers.Timer _writeTimer;
        private bool creat_file = false;
        private bool need_mark = true;
        private int write_minpoint;
        private readonly object _lastDataPointCountsLock = new object();
        private readonly object _EMGDataLock = new object();
        public static int lines { get; set; } = 0;

        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        //mycode

        // Add your API key & license here 

        private float _globalScaleY = 1f; // 全局Y軸縮放比例
        private const float ScaleStep = 1.1f; // 每次縮放的比例變化

        private string key = "";
        private string license = "";

        public List<SensorTrignoRf> SelectedSensors;

        // Pipeline fields
        private IDelsysDevice _deviceSource;
        private Pipeline _pipeline;

        // Holds collection data
        private List<List<double>> _data;
        private List<List<double>> EMG_data;
        private List<List<double>> copy_data;
        private List<double> array;

        // Metadata fields
        private int _totalFrames;
        private int _totalLostPackets;
        private int _frameThroughput;
        private double _packetInterval;
        private double _streamTime = 0.0;

        // User Controls
        private PairSensor _pairSensor;
        private ScannedSensors _scannedSensors;

        private MainWindow _mainWindow;

        private string dirPath;
        
        public DeviceStreaming(MainWindow mainWindowPanel, string patient_path)
        {
            //string todayDate = DateTime.Now.ToString("yyyy_MM_dd");
            //dirPath = patient_path + "/" + todayDate;
            dirPath = patient_path;
            InitializeComponent();
            InitializeDataSource();
            _mainWindow = mainWindowPanel;
            EMG_data = new List<List<double>>();
            copy_data = new List<List<double>>();
            InitializeDataSource();
            LoadDataSource();
            // 訂閱 ComboBox 的 SelectionChanged 事件
            _mainWindow.folderComboBox_Action.SelectionChanged += FolderComboBox_Action_SelectionChanged;
            _mainWindow.folderComboBox_Action.PreviewKeyUp += FolderComboBox_Action_PreviewKeyUp;


            DateTime today = DateTime.Today;
            string Date = today.ToString("MM-dd");
            currently_parameter_file.Text = Path.Combine("Other", "NP" + '-' + Date);
        }

        string Parameter_Value="";//參數值
        // 定義事件處理函數
        private void FolderComboBox_Action_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox comboBox = sender as ComboBox;
            ComboBoxItem selectedItem = comboBox.SelectedItem as ComboBoxItem;

                if (selectedItem != null)
                {
                    string selectedText = selectedItem.Content.ToString();

                    DateTime today = DateTime.Today;
                    string Date = today.ToString("MM-dd");

                    // 設定 csvFilePath
                    if (string.IsNullOrEmpty(selectedText))
                    {
                        if (MainWindow.Pulse_frequency == null && MainWindow.Pulse_amplitude == null && MainWindow.Pulse_rate == null)
                        {
                            currently_parameter_file.Text = Path.Combine("Other", "NP" + '-' + Date);
                        }
                        else if (MainWindow.Pulse_rate == null)
                        {
                            currently_parameter_file.Text = Path.Combine("Other", MainWindow.Pulse_frequency + "頻率" + MainWindow.Pulse_amplitude + "震幅" + MainWindow.Pulse_rate + '-' + Date);
                        }
                        else
                        {
                            currently_parameter_file.Text = Path.Combine("Other", MainWindow.Pulse_frequency + "頻率" + MainWindow.Pulse_amplitude + "震幅" + MainWindow.Pulse_rate + "內部脈衝速率" + '-' + Date);
                        }
                    }
                    else
                    {
                        if (MainWindow.Pulse_frequency == null && MainWindow.Pulse_amplitude == null && MainWindow.Pulse_rate == null)
                        {
                            currently_parameter_file.Text = Path.Combine(selectedText, "NP" + '-' + Date);
                        }
                        else if (MainWindow.Pulse_rate == null)
                        {
                            currently_parameter_file.Text = Path.Combine(selectedText, MainWindow.Pulse_frequency + "頻率" + MainWindow.Pulse_amplitude + "震幅" + MainWindow.Pulse_rate + '-' + Date);
                        }
                        else
                        {
                            currently_parameter_file.Text = Path.Combine(selectedText, MainWindow.Pulse_frequency + "頻率" + MainWindow.Pulse_amplitude + "震幅" + MainWindow.Pulse_rate + "內部脈衝速率" + '-' + Date);
                        }
                    }
                }
        }

        private void FolderComboBox_Action_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            ComboBox comboBox = sender as ComboBox;
            string inputText = comboBox.Text;

            if (!string.IsNullOrEmpty(inputText))
            {
                DateTime today = DateTime.Today;
                string Date = today.ToString("MM-dd");

                currently_parameter_file.Text = Path.Combine(inputText, "NP" + '-' + Date);
            }
        }


        #region Device Configuration

        private void InitializeDataSource()
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            string name = assembly.GetName().Name;

            string key;
            using (Stream stream = assembly.GetManifestResourceStream(name + ".PublicKey.lic"))
            {
                StreamReader sr = new StreamReader(stream);
                key = sr.ReadLine();
            }

            string license;
            using (Stream stream = assembly.GetManifestResourceStream(name + ".License.lic"))
            {
                StreamReader sr = new StreamReader(stream);
                license = sr.ReadToEnd();
            }

            var deviceSourceCreator = new DeviceSourcePortable(key, license);
            deviceSourceCreator.SetDebugOutputStream((str, args) => Trace.WriteLine(string.Format(str, args)));

            _deviceSource = deviceSourceCreator.GetDataSource(SourceType.TRIGNO_RF);
            _deviceSource.Key = key;
            _deviceSource.License = license;
        }

        private void LoadDataSource()
        {
            try
            {
                PipelineController.Instance.AddPipeline(_deviceSource);
            }
            catch (BaseDetectionFailedException e)
            {
                return;
            }
            try
            {
                _pipeline = PipelineController.Instance.PipelineIds[0];
                _pipeline.TrignoRfManager.InformationScanTime = 5;

                _pipeline.TrignoRfManager.ComponentAdded += ComponentAdded;
                _pipeline.TrignoRfManager.ComponentLost += ComponentLost;
                _pipeline.TrignoRfManager.ComponentRemoved += ComponentRemoved;
                _pipeline.TrignoRfManager.ComponentScanComplete += ComponentScanComplete;

                _pipeline.CollectionStarted += CollectionStarted;
                _pipeline.CollectionDataReady += CollectionDataReady;
                _pipeline.CollectionComplete += CollectionComplete;
            }
            catch
            {
                Streaming_grid.Visibility = Visibility.Collapsed;
                _mainWindow.btn_backToMainPageButton.Visibility = Visibility.Hidden;
                _mainWindow.btn_Shooting.Visibility = Visibility.Collapsed;
                _mainWindow.folderComboBox_Action.Visibility = Visibility.Collapsed;
                Streaming_Background.Visibility = Visibility.Collapsed;

                _mainWindow.btn_personal_data.Visibility = Visibility.Collapsed;
                _mainWindow.btn_look_AnalyzeFile.Visibility = Visibility.Collapsed;
                _mainWindow.btn_AnalyzeFile.Visibility = Visibility.Collapsed;
                _mainWindow.btn_DeviceStreaming_Last.Visibility = Visibility.Collapsed;


                //跳出請連接deslsy
                var customMessageBox = new CustomMessageBox(5);
                customMessageBox.ShowDialog();
                _mainWindow.streaming_start--;
                _mainWindow.CheckingButton_Click(MainWindow.Patient_Name);

            }
            
        }

        public void ConfigurePipeline()
        {
            DataLine dataLine = new DataLine(_pipeline);
            dataLine.ConfigurePipeline();
            _frameThroughput = PipelineController.Instance.GetFrameThroughput();

            int totalChannels = 0;
            foreach (var comp in _pipeline.TrignoRfManager.Components)
            {
                totalChannels += comp.TrignoChannels.Count();
            }
        }

        public async Task StopStreamAsync()
        {
            await _pipeline.Stop();
        }

        public async Task ResetPipeline()
        {
            await _pipeline.DisarmPipeline();

            _totalFrames = 0;
            _totalLostPackets = 0;
            _streamTime = 0.0;

            for (int i = 0; i < _pipeline.TrignoRfManager.Components.Count; i++)
            {
                _pipeline.TrignoRfManager.RemoveTrignoComponent(_pipeline.TrignoRfManager.Components[i]);
            }
        }

        #endregion

        #region API Component Event Handlers

        public void ComponentAdded(object sender, ComponentAddedEventArgs e) { }

        public void ComponentLost(object sender, ComponentLostEventArgs e) { }

        public void ComponentRemoved(object sender, ComponentRemovedEventArgs e) { }

        public void ComponentScanComplete(object sender, ComponentScanCompletedEventArgs e)
        {
            Dispatcher.Invoke(() => {
                _mainWindow.btn_backToMainPageButton.IsEnabled = true;
                btn_ScanSensors.IsEnabled = true;
                
                btn_PairSensors.IsEnabled = true;
            });

            if (e.ComponentDictionary.Count <= 0)
            {
                _scannedSensors.NoSensorsDetected();
                return;
            }

            var sortedComponents = _pipeline.TrignoRfManager.Components.OrderBy(comp => comp.DeviceIdx).ToList();

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                int sensorIndex = 0;
                foreach (var comp in sortedComponents)
                {
                    comp.SelectSampleMode(comp.Configuration.SampleModes[0]);
                    sensorIndex++;
                }
            }));

            Dispatcher.Invoke(() => { _scannedSensors.ScanComplete(sortedComponents); });
        }

        #endregion

        #region API Data Collection Event Handlers

        public void CollectionStarted(object sender, CollectionStartedEvent e)
        {
            Dispatcher.Invoke(() => {
                _mainWindow.btn_backToMainPageButton.IsEnabled = false;
            });

            _data = new List<List<double>>();
            _totalFrames = 0;
            _totalLostPackets = 0;
            int totalChannels = 0;

            for (int i = 0; i < _pipeline.TrignoRfManager.Components.Count; i++)
            {
                for (int j = 0; j < _pipeline.TrignoRfManager.Components[i].TrignoChannels.Count; j++)
                {
                    if (_data.Count <= totalChannels)
                    {
                        _data.Add(new List<double>());
                    }
                    else
                    {
                        _data[totalChannels] = new List<double>();
                    }
                    if (_packetInterval == 0)
                    {
                        _packetInterval = _pipeline.TrignoRfManager.Components[i].TrignoChannels[j].FrameInterval * _frameThroughput;
                    }
                    totalChannels++;
                }
            }
            creatEMG_data();  // 使用循環緩衝區
        }


        public void CollectionDataReady(object sender, ComponentDataReadyEventArgs e)
        {
            _streamTime += _packetInterval;
            int nSensors = _pipeline.TrignoRfManager.Components.Count;
            int nChannels = _data.Count;
            int lostPackets = 0;
            int EMGPoint = nChannels / 2;

            var sensorOrderMapping = _pipeline.TrignoRfManager.Components
                .Select((component, index) => new { component.Id, Order = index })
                .ToDictionary(x => x.Id, x => x.Order);

            for (int k = 0; k < e.Data.Count(); k++)
            {
                for (int i = 0; i < e.Data[k].SensorData.Count(); i++)
                {
                    if (e.Data[k].SensorData[i].IsDroppedPacket)
                    {
                        lostPackets++;
                    }
                }
            }
            _totalLostPackets += lostPackets;
            _totalFrames += _frameThroughput * e.Data[0].SensorData.Count();

            foreach (var frame in e.Data)
            {
                Array.Sort(frame.SensorData, (a, b) =>
                {
                    int orderA = sensorOrderMapping.ContainsKey(a.Id) ? sensorOrderMapping[a.Id] : int.MaxValue;
                    int orderB = sensorOrderMapping.ContainsKey(b.Id) ? sensorOrderMapping[b.Id] : int.MaxValue;
                    return orderA.CompareTo(orderB);
                });
            }

            int columnIndex = 0;

            for (int k = 0; k < e.Data.Count(); k++)
            {
                for (int i = 0; i < e.Data[k].SensorData.Count(); i++)
                {
                    // 確保 columnIndex 沒有超出 EMG_data 集合的大小
                    while (columnIndex >= EMG_data.Count)
                    {
                        EMG_data.Add(new List<double>());
                    }

                    // 確保 columnIndex 沒有超出 _lastDataPointCounts 集合的大小
                    lock (_lastDataPointCountsLock  )
                    {
                        while (columnIndex >= _lastDataPointCounts.Count)
                        {
                            _lastDataPointCounts.Add(0);
                        }
                    }

                    // 添加 SensorData 到 EMG_data 中
                    foreach (var val in e.Data[k].SensorData[i].ChannelData[0].Data)
                    {
                        EMG_data[columnIndex].Add(val);
                    }

                    columnIndex++;

                    // 處理 need_mark 的邏輯
                    if (need_mark)
                    {
                        // 確保 columnIndex 沒有超出 EMG_data 集合大小
                        if (columnIndex + 1 == EMG_data.Count())
                        {
                            
                            foreach (var val in e.Data[k].SensorData[i].ChannelData[0].Data)
                            {
                                lock (EMG_data)
                                {
                                    EMG_data[columnIndex].Add(0);

                                    if (Enter_mark)
                                    {
                                        EMG_data[columnIndex].Add(20);
                                    }
                                }
                                Enter_mark = false;
                            }
                        }
                    }
                }

                // 重置 columnIndex
                columnIndex = 0;
            }

            ProcessAllSensors();

            void ProcessAllSensors()
            {
                Task.Run(() =>
                {
                    var sensorDataList = new List<(int sensorIndex, SKPoint[] skPoints)>();

                    for (int i = 0; i < EMG_data.Count; i++)
                    {
                        int newDataPointCount;
                        List<double> newDataPoints;

                            if (_lastDataPointCounts[i] > EMG_data[i].Count)
                            {
                                // 這裡可以根據需求來決定要如何處理
                                _lastDataPointCounts[i] = EMG_data[i].Count; // 將其同步到正確的數值
                            }
                        newDataPointCount = EMG_data[i].Count - _lastDataPointCounts[i];
                        if (newDataPointCount > 0)
                        {
                            newDataPoints = EMG_data[i].Skip(_lastDataPointCounts[i]).Take(newDataPointCount).ToList();
                            //這裡
                            _lastDataPointCounts[i] = EMG_data[i].Count;
                            //這裡
                        }
                        else
                        {
                            continue;
                        }

                        int maxRetries = 1000;  // 設定最大重試次數
                        int retryCount = 0;
                        bool success = false;

                        while (!success && retryCount < maxRetries)
                        {
                            try
                            {
                                // 嘗試執行操作
                                if (i >= 0 && i < _lastDataPointCounts.Count)
                                {
                                    var skPoints = newDataPoints.Select((val, index) =>
                                        new SKPoint(
                                            (float)(_streamTime + _lastDataPointCounts[i] - newDataPointCount + index),
                                            (float)val))
                                        .ToArray();
                                    sensorDataList.Add((i, skPoints));

                                    success = true;  // 成功後退出循環
                                }
                                else
                                {
                                    Console.WriteLine($"索引 {i} 超出 _lastDataPointCounts 的範圍。");
                                    break;  // 如果索引超出範圍，無需重試，直接退出
                                }
                            }
                            catch (Exception ex)
                            {
                                retryCount++;
                                Console.WriteLine($"嘗試第 {retryCount} 次時發生異常：{ex.Message}");

                                if (retryCount >= maxRetries)
                                {
                                    // 如果達到最大重試次數，記錄錯誤並跳過該操作
                                    Console.WriteLine($"操作失敗，超過最大重試次數 {maxRetries}，跳過該操作。");
                                }
                                else
                                {
                                    // 如果還可以重試，可以選擇等待一小段時間後重試
                                    System.Threading.Thread.Sleep(500);  // 等待500毫秒後重試
                                }
                            }
                        }



                    }

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        foreach (var (sensorIndex, skPoints) in sensorDataList)
                        {
                            while (realTimePlots.Count <= sensorIndex)
                            {
                                realTimePlots.Add(new RealTimePlot(sensorIndex, this, _cancellationTokenSource));
                                lines++;
                            }

                            var plot = realTimePlots[sensorIndex];
                            plot.AddSensorData(skPoints);
                            plot.Visibility = Visibility.Visible;
                            if (Globalreflash)
                            {
                                realTimePlots[EMG_data.Count-1].ResetPlot();
                                Globalreflash = false;
                            }
                            var panel = GetOrCreatePanelForPlot(sensorIndex);
                            if (plot.Parent != null)
                            {
                                ((Panel)plot.Parent).Children.Remove(plot);
                            }
                            panel.Children.Add(plot);
                        }
                    });
                });
            }

        }

        public void creatEMG_data()
        {
            EMG_data.Clear();
            _lastDataPointCounts.Clear();
            int nSensors = _pipeline.TrignoRfManager.Components.Count;
            if (need_mark)
            {
                for (int i = 0; i < nSensors + 1; i++)
                {
                    array = new List<double>();
                    EMG_data.Add(array);
                    _lastDataPointCounts.Add(0);
                }
            }
            else
            {
                for (int i = 0; i < nSensors; i++)
                {
                    array = new List<double>();
                    EMG_data.Add(array);
                    _lastDataPointCounts.Add(0);
                }
            }
        }


        private Grid GetOrCreatePanelForPlot(int plotIndex)
        {
            var container = DynamicPanelContainer;
            Grid panel = container.Children.OfType<Grid>().ElementAtOrDefault(plotIndex);
            if (panel == null)
            {
                panel = new Grid { };
                container.Children.Add(panel);
            }
            return panel;
        }

        private void CreateSignalPanels(int numberOfSensors)
        {
            DynamicPanelContainer.Children.Clear();
            realTimePlots.Clear();

            

            // 設定 UniformGrid 的 Rows 屬性，確保動態調整行數
            DynamicPanelContainer.Rows = numberOfSensors;

        }


        public void CollectionComplete(object sender, CollectionCompleteEvent e)
        {
            Dispatcher.Invoke(() => {
                _mainWindow.btn_backToMainPageButton.IsEnabled = true;
               
                copy_data.AddRange(EMG_data);
                EMG_data.Clear();
                _lastDataPointCounts.Clear();
                creatEMG_data();
                WriteEMGDataToCSV();
            });
        }
        string csvPath;
        string txtPath;
        private void CreateOrOpenCSV(int count)
        {
            //他是病人>日期路徑
            string pathTopatient = dirPath;

            if (!Directory.Exists(pathTopatient))
            {
                Directory.CreateDirectory(pathTopatient);
            }

            string timestamp = DateTime.Now.ToString("HH-mm-ss");

            DateTime today = DateTime.Today;
            string Date = today.ToString("MM-dd");

            // 設定 csvFilePath
            //if (string.IsNullOrEmpty(_mainWindow.folderComboBox_Action.Text))
            //{
            //    Parameter_Value = "Null" + '-' + Date;
            //    csvFilePath = Path.Combine(define_parameter_file.Text, $"{timestamp}.csv");
            //}
            //else
            //{
                
            csvFilePath = Path.Combine(define_parameter_file.Text, $"{timestamp}-{_pipeline.DataSourceInfo[0].Count}.csv");
            string txtFilePath;
            txtFilePath = Path.Combine(define_parameter_file.Text, $"{timestamp}-{_pipeline.DataSourceInfo[0].Count}的備註.txt");
            //}

            // 合併成完整路徑
            csvPath = Path.Combine(pathTopatient, csvFilePath);
            txtPath = Path.Combine(pathTopatient, txtFilePath);
            // 檢查並創建資料夾（如果尚未存在）
            string directoryPath = Path.GetDirectoryName(csvPath);
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }


            // 創建文件並寫入數據
            using (var writer = new StreamWriter(csvPath, append: true))
            {
                // 在這裏寫入數據
                var columnNames = new List<string>();
                if (need_mark)
                {
                    for (int i = 1; i <= count-1; i++)
                    {
                        columnNames.Add($"EMG {i}");
                    }
                    columnNames.Add($"MARK");
                }
                else
                {
                    for (int i = 1; i <= count; i++)
                    {
                        columnNames.Add($"EMG {i}");
                    }
                }
                writer.WriteLine(string.Join(",", columnNames));
            }
        }

        private void SetupWriteTimer()
        {
            if (_writeTimer != null)
            {
                _writeTimer.Elapsed -= OnTimedEvent;  // 確保沒有重複訂閱
            }

            _writeTimer = new System.Timers.Timer(1000);
            _writeTimer.Elapsed += OnTimedEvent;
            _writeTimer.AutoReset = true;
            _writeTimer.Start();
        }

        private void OnTimedEvent(Object source, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                int write_minpoint;

                lock (_lastDataPointCountsLock)
                {
                    if (_lastDataPointCounts.Count > 0)
                    {
                        write_minpoint = _lastDataPointCounts.Min();
                    }
                    else
                    {
                        write_minpoint = 0;
                    }
                    _lastDataPointCounts.Clear();
                }

                lock (EMG_data)
                {
                    copy_data.AddRange(EMG_data);
                    EMG_data.Clear();
                    creatEMG_data(); // 重置 EMG 數據結構
                }

                WriteEMGDataToCSV(); // 寫入數據到 CSV
            }
            catch (Exception ex)
            {
                // 記錄或處理異常，避免定時器停止
                Console.WriteLine($"OnTimedEvent 發生異常: {ex.Message}");
            }
        }


        private bool IsFileLocked(string filePath)
        {
            try
            {
                using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    stream.Close();
                }
            }
            catch (IOException)
            {
                return true;
            }
            return false;
        }
        private async Task WriteEMGDataToCSV()
        {
            if (string.IsNullOrEmpty(csvPath))
            {
                return;
            }

            if (IsFileLocked(csvPath))
            {
                return;
            }

            int maxDataPoints = copy_data.Max(sensor => sensor?.Count ?? 0);

            using (var writer = new StreamWriter(csvPath, append: true))
            {
                for (int i = 0; i < maxDataPoints; i++)
                {
                    var lineData = new List<string>();

                    foreach (var sensor in copy_data)
                    {
                        if (sensor == null)
                        {
                            lineData.Add("");
                        }
                        else
                        {
                            string dataPoint = sensor.Count > i ? sensor[i].ToString() : "";
                            lineData.Add(dataPoint);
                        }
                    }

                    string line = string.Join(",", lineData);
                    await writer.WriteLineAsync(line);
                }
            }
            copy_data.Clear();
        }

        #endregion

        #region Button Events Handlers

        //public void clk_LoadDevice(object sender, RoutedEventArgs e)
        //{
        //    InitializeDataSource();
        //    LoadDataSource();

        //    btn_LoadDevice.IsEnabled = false;
        //    btn_PairSensors.IsEnabled = true;
        //}

        public async void clk_Scan(object sender, RoutedEventArgs e)
        {
            _mainWindow.btn_backToMainPageButton.IsEnabled = false;
          
            btn_ScanSensors.IsEnabled = false;
            btn_PairSensors.IsEnabled = false;
            //needmark_sensor.IsEnabled = true;
            foreach (var comp in _pipeline.TrignoRfManager.Components)
            {
                await _pipeline.TrignoRfManager.DeselectComponentAsync(comp);
                _pipeline.TrignoRfManager.RemoveTrignoComponent(comp);
            }

            SecondaryPanel.Children.Clear();
            _scannedSensors = new ScannedSensors(this, _pipeline);
            SecondaryPanel.Children.Add(_scannedSensors);

            await _pipeline.Scan();
        }

        public async void clk_Reset(object sender, RoutedEventArgs e)
        {
            await ResetPipeline();
            resetUI();
            btn_PairSensors.IsEnabled = true;
        }

        public void resetUI()
        {
            _totalFrames = 0;
            _totalLostPackets = 0;
            _streamTime = 0.0;

            btn_Stop.IsEnabled = false;
            

            btn_Start.IsEnabled = false;

            btn_ScanSensors.IsEnabled = true;
        }

        public void clk_Pair(object sender, RoutedEventArgs e)
        {
            SecondaryPanel.Children.Clear();
            _pairSensor = new PairSensor(_pipeline, _mainWindow, this);
            SecondaryPanel.Children.Add(_pairSensor);

            btn_PairSensors.IsEnabled = false;
            btn_ScanSensors.IsEnabled = false;
        }

        public void clk_Export(object sender, RoutedEventArgs e)
        {
            List<string> lines = new List<string>();

            int nSensors = _pipeline.TrignoRfManager.Components.Count;
            int nChannels = _data.Count;

            string labelRow = "";

            int EMG_Number = 0;

            foreach (var sensor in _pipeline.TrignoRfManager.Components.Where(x => x.State == SelectionState.Allocated))
            {
                foreach (var channel in sensor.TrignoChannels)
                {
                    if (channel.Name == "EMG 1")
                    {
                        EMG_Number++;
                        labelRow += "EMG " + EMG_Number.ToString() + ",";
                    }
                    else
                    {
                        labelRow += channel.Name + ",";
                    }
                }
            }

            int largestChannel = 0;
            for (int i = 1; i < nChannels; i++)
            {
                if (_data[i].Count > _data[largestChannel].Count)
                {
                    largestChannel = i;
                }
            }

            for (int i = 0; i < _data[largestChannel].Count; i++)
            {
                string dataRow = "";

                if (i == 0)
                {
                    dataRow += labelRow;
                    dataRow += "\n";
                }

                for (int j = 0; j < nChannels; j++)
                {
                    if (i < _data[j].Count)
                    {
                        dataRow += _data[j].ElementAt(i).ToString() + ",";
                    }
                    else
                    {
                        dataRow += ",";
                    }
                }

                lines.Add(dataRow);
            }

            string dataDir = "./sensor_data";
            if (!Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
            }

            string fileName = DateTime.Now.ToString("yyy-dd-MM--HH-mm-ss");
            string path = dataDir + "/" + fileName + ".csv";
            using (StreamWriter outputFile = new StreamWriter(path))
            {
                foreach (string line in lines)
                {
                    outputFile.WriteLine(line);
                }
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo()
            {
                FileName = "sensor_data",
                UseShellExecute = true,
                Verb = "open"
            });

          
        }

        private void mark_Click(object sender, RoutedEventArgs e)
        {
            Enter_mark = true;
            Mark = 20.0;
          
           
        }

        //private void need_mark_Click(object sender, RoutedEventArgs e)
        //{
        //    need_mark = true;
        //    MessageBox.Show("新增成功", "新增mark", MessageBoxButton.OK, MessageBoxImage.Information);
        //    needmark_sensor.IsEnabled = false;
        //}

        private bool _isStreaming = false;

        public async void clk_Start(object sender, RoutedEventArgs e)
        {
            btn_PairSensors.IsEnabled = false;
            _isStreaming = true;
            mark.IsEnabled = true;
            btn_ZoomIn.IsEnabled = true;
            btn_ZoomOut.IsEnabled = true;

            btn_check_parameter.IsEnabled = false;
            _mainWindow.btn_Shooting.IsEnabled = false;
            _mainWindow.btn_AnalyzeFile.IsEnabled = false;
            _mainWindow.btn_look_AnalyzeFile.IsEnabled = false;
            _mainWindow.btn_personal_data.IsEnabled = false;
            _mainWindow.btn_backToMainPageButton.IsEnabled = false;
            

            //儲存、開檔、分析button關閉
            btn_Store_text.IsEnabled = false;
            //btn_open_file.IsEnabled = false;
            btn_Analyze.IsEnabled = false;

            Remark_Text.Text = "";

            resetUI();
            btn_Stop.IsEnabled = false;
            int sensorCount = _pipeline.TrignoRfManager.Components.Count;
            if (need_mark)
            {
                sensorCount += 1;
            }

            if (realTimePlots.Count >= sensorCount)
            {
                foreach (var plot in realTimePlots)
                {
                    plot.ResetPlot();
                }
            }
            else
            {
                CreateSignalPanels(sensorCount);
            }

            SetupWriteTimer();
            CreateOrOpenCSV(sensorCount);

            // 確保數據結構重置
            creatEMG_data();

            // 實例化 CancellationTokenSource
            _cancellationTokenSource = new CancellationTokenSource();

            // 啓動刷新循環
            StartRefreshingPlots(_cancellationTokenSource.Token);

            // 啓動數據流
            await Task.Run(() =>
            {
                if (_isStreaming)
                {
                    _pipeline.Start();
                }
            });

            btn_Start.IsEnabled = false;
            btn_ScanSensors.IsEnabled = false;
            btn_Stop.IsEnabled = true;
        }

        private void StartRefreshingPlots(CancellationToken cancellationToken)
        {
            Task.Run(async () =>
            {
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        // 刷新所有的 RealTimePlot
                        Dispatcher.Invoke(() =>
                        {
                            foreach (var plot in realTimePlots)
                            {
                                plot.RefreshPlot();
                            }
                        });

                        await Task.Delay(10, cancellationToken);
                    }
                }
                catch (TaskCanceledException)
                {
                    // 正常結束任務
                }
            }, cancellationToken);
        }



        public async void clk_Stop(object sender, RoutedEventArgs e)
        {
            _isStreaming = false;
            await StopStreamAsync();
            mark.IsEnabled = false;
            btn_ZoomIn.IsEnabled = false;
            btn_ZoomOut.IsEnabled = false;

            btn_check_parameter.IsEnabled = true;
            _mainWindow.btn_Shooting.IsEnabled = true;
            _mainWindow.btn_AnalyzeFile.IsEnabled = true;
            _mainWindow.btn_look_AnalyzeFile.IsEnabled = true;
            _mainWindow.btn_personal_data.IsEnabled = true;
            _mainWindow.btn_backToMainPageButton.IsEnabled = true;

            //儲存、開檔、分析button打開
            btn_Store_text.IsEnabled = true;
            //btn_open_file.IsEnabled = true;
            btn_Analyze.IsEnabled = true;

            // 取消刷新循環
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }
            if (_writeTimer != null)
            {
                _writeTimer.Stop();
                _writeTimer.Dispose();
                _writeTimer = null;
            }
            btn_Start.IsEnabled = true;
            btn_Stop.IsEnabled = false;

            // 清空數據
            Thread.Sleep(100);

            // 這裏新增視窗或處理 CSV 文件
            string pathTopatient = MainWindow.GlobalDirPath; // 路徑
            if (!Directory.Exists(pathTopatient))
            {
                Directory.CreateDirectory(pathTopatient);
            }

            
            ClearData();
            csv_Path.Text = csvPath;
        }


        private void ClearData()
        {
            // 清空與數據相關的數組或列表
            //sensorData.Clear()
            EMG_data?.Clear();
            _lastDataPointCounts.Clear();
            copy_data?.Clear();
            _streamTime = 0.0;
            //tempDataBuffer?.Clear();

            // 如果有其他需要重置的狀態，請在這裏重置
            //timeSinceLastReset = 0;
            //pointsCounter = 0;

            foreach (var plot in realTimePlots)
            {
                plot.ResetPlot();  // 徹底清空繪圖狀態
            }
        }



        private void clk_ZoomIn(object sender, RoutedEventArgs e)
        {
            string ZoomIn = "ZoomIn";
            UpdateAllPlotsScale(ZoomIn);
        }

        private void clk_ZoomOut(object sender, RoutedEventArgs e)
        {
            string ZoomOut = "ZoomOut";
            UpdateAllPlotsScale(ZoomOut);
        }

        private void UpdateAllPlotsScale(string zoom)
        {
            foreach (var plot in realTimePlots)
            {
                plot.SetScaleY(zoom);
            }
        }

        private void ResetScaleAndPosition()
        {
            // 將縮放比例重置為初始值
            _globalScaleY = 1f;

            // 更新所有圖表以應用重置的縮放比例
            string Reset = "Reset";
            UpdateAllPlotsScale(Reset);

            // 重置線條位置，如果有其他相關參數，也可以在此處重置
            foreach (var plot in realTimePlots)
            {
                plot.ResetPlot();  // 這裡假設你的 RealTimePlot 有 ResetPlot 來重置繪圖位置
            }
        }


        #endregion

        // 儲存備註到txt檔
        private void clk_Store_text(object sender, RoutedEventArgs e)
        {
            // 設定備註檔案的完整路徑
            string patientDataFilePath = txtPath;

            // 檢查目錄是否存在，若不存在則創建目錄
            Directory.CreateDirectory(Path.GetDirectoryName(patientDataFilePath));

            // 將 Remark_Text.Text 的內容寫入檔案
            string remarksText = string.IsNullOrEmpty(Remark_Text.Text) ? "無" : Remark_Text.Text;
            File.WriteAllText(patientDataFilePath, remarksText);
            CustomMessageBox _CustomMessageBox = new CustomMessageBox(7);
            _CustomMessageBox.Show();


        }


        //打開檔案路徑
        //private void clk_open_file(object sender, RoutedEventArgs e)
        //{
        //    // 定義病人資料的路徑
        //    string pathTopatient = MainWindow.GlobalDirPath;

        //    // 確定目前動作>參數
        //    define_parameter_file.Text = currently_parameter_file.Text;

        //    // 設定備註檔案的完整路徑
        //    string patientDataFilePath = Path.Combine(pathTopatient, define_parameter_file.Text);

        //    // 檢查 csvPath 是否為有效的路徑
        //    if (Directory.Exists(patientDataFilePath))
        //    {
        //        // 使用 Process 類別啟動檔案總管並開啟指定資料夾
        //        System.Diagnostics.Process.Start("explorer.exe", patientDataFilePath);
        //    }
        //    else
        //    {
        //        // 提示使用者路徑無效
        //        MessageBox.Show("指定的資料夾路徑不存在。");
        //    }
        //}

        //分析檔案
        private void clk_Analyze(object sender, RoutedEventArgs e)
        {
            Last_Window _last_window = new Last_Window(csvPath); // 新的窗口
            _last_window.Show();
        }

        private void clk_check_parameter(object sender, RoutedEventArgs e)
        {
            // 顯示開始按鈕
            btn_Start.IsEnabled = true;

            //// 定義病人資料的路徑
            //string pathTopatient = MainWindow.GlobalDirPath;

            // 確定目前動作>參數
            define_parameter_file.Text = currently_parameter_file.Text;

            //// 設定備註檔案的完整路徑
            //string patientDataFilePath = Path.Combine(pathTopatient, define_parameter_file.Text, $"{Parameter_Value}的備註.txt");

            //// 確保目錄存在，若不存在則創建目錄
            //Directory.CreateDirectory(Path.GetDirectoryName(patientDataFilePath));

            //// 檢查備註檔案是否存在
            //if (!File.Exists(patientDataFilePath))
            //{
            //    // 如果檔案不存在，則創建一個空白的備註檔案
            //    File.WriteAllText(patientDataFilePath, "無");
            //    Remark_Text.Text = "無"; // 預設內容顯示 "無"
            //}
            //else
            //{
            //    // 如果檔案存在，讀取內容並顯示在 Remark_Text.Text 上
            //    string fileContent = File.ReadAllText(patientDataFilePath);
            //    Remark_Text.Text = fileContent;
            //}
        }

    }
}
