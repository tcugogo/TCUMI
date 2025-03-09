using Basic_Streaming_NET;
using Basic_Streaming_NET.Views;
using DelsysAPI.Pipelines;
using System.Diagnostics;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using Window = System.Windows.Window;
using System.Threading.Tasks;
using AForge.Imaging.Filters;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows.Input;

namespace Basic_Streaming.NET
{
    public partial class MainWindow : Window
    {
        public static string GlobalDirPath { get; set; }//全域資料夾

        public static string Patient_Name { get; set; }//全域資料夾

        public static string Pulse_frequency { get; set; }//脈衝頻率
        public static string Pulse_amplitude { get; set; }//脈衝震幅
        public static string Pulse_rate { get; set; }//內部脈衝速率

        private DeviceStreaming _deviceSteamingUC;

        private Checking_Data_UserControl _CheckData;

        private AnalyzeFile _AnalyzeFile;//分析畫面

        private LookFile_Control _LookFile;

        private Revise_mark reviseMark;

        private UserDate_Record _UserDataRecord;
        private Select_Patient _Select_Patient;
        public int[] Shoot_electric { get; set; } = new int[16];
        private Adjustment_Parameters _Adjustment_Parameters;
        public string path_shootname;
        //static int Page_SelectOptions = 1, Finished_selecting = 0, Page_AnalyzeData = 0;
        //Page_MainWindow = 0,
        //主畫面 選擇選項畫面 新增病人畫面 串流sensor畫面 數據分析畫面

        //是否串流
        public int streaming_start = 0;

        public MainWindow()
        {
            Array.Clear(Shoot_electric, 0, Shoot_electric.Length);
            try
            {
                Process currentProcess = Process.GetCurrentProcess();
                currentProcess.PriorityClass = ProcessPriorityClass.High;
                Console.WriteLine("Priority has been set to high.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error setting priority: " + ex.Message);
            }
            InitializeComponent();
            _Select_Patient = new Select_Patient(this);
            MainPanel.Children.Add(_Select_Patient);
            this.Loaded += MainWindow_Loaded;
            this.SizeChanged += MainWindow_SizeChanged;

            //toggleOption.Checked += ToggleOptionChanged;
            //toggleOption.Unchecked += ToggleOptionChanged;
        }

        //private void ToggleOptionChanged(object sender, RoutedEventArgs e)
        //{
        //    _deviceSteamingUC.streamInfo_visibility(); // 確保你有辦法訪問 DeviceStreaming 實例
        //}

        //固定跳轉到分析Button到畫面對齊80%的位子
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateButtonPosition();
        }
        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateButtonPosition();
        }
        private void UpdateButtonPosition()
        {
            //固定位置
        }




        public void clk_DeviceStreaming(object sender, RoutedEventArgs e)
        {

        }

        //private void btn_User_SelectOptions(object sender, RoutedEventArgs e)
        //{
        //    MainPanel.Children.Clear();
        //}

        //新增病人
        public void clk_Add_patient()
        {
            MainPanel.Children.Clear();
            btn_backToMainPageButton.Visibility = Visibility.Visible;

            // 更換背景圖爲 /Picture/BackGround3.png
            SetBackgroundImage("Picture/BackGround3.png");

            _UserDataRecord = new UserDate_Record(this);
            MainPanel.Children.Add(_UserDataRecord);
        }

        //進入串流
        public void clk_DeviceStreaming(string patient_path)
        {
            MainPanel.Children.Clear();

            //meau.Visibility = Visibility.Visible;

            // 更換背景圖爲 /Picture/BackGround3.png
            SetBackgroundImage("Picture/BackGround3.png");
            btn_backToMainPageButton.Visibility = Visibility.Visible;
            btn_Shooting.Visibility = Visibility.Visible;
            folderComboBox_Action.Visibility = Visibility.Visible;
            btn_personal_data.Visibility = Visibility.Visible;
            btn_look_AnalyzeFile.Visibility = Visibility.Visible;
            btn_AnalyzeFile.Visibility= Visibility.Visible;

            _deviceSteamingUC = new DeviceStreaming(this, patient_path);
            MainPanel.Children.Add(_deviceSteamingUC);
        }



        public void clk_Exit(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        //忘記用在哪了
        public void First_Window()
        {
            MainPanel.Children.Clear();

            btn_backToMainPageButton.Visibility = Visibility.Hidden;
            MainPanel.Children.Add(User_SelectOptions_Grid);

            System.Diagnostics.Debug.WriteLine("Simulating Stream # of pipeline Ids before removal: " + PipelineController.Instance.PipelineIds.Count);
            if (PipelineController.Instance.PipelineIds.Count > 0)
            {
                PipelineController.Instance.RemovePipeline(0);
            }
            System.Diagnostics.Debug.WriteLine("Simulating Stream # of pipeline Ids after removal: " + PipelineController.Instance.PipelineIds.Count);
        }

        //回到首頁
        public void clk_BackButton(object sender, RoutedEventArgs e)
        {
            Function_clk_BackButton();
        }
        public void Function_clk_BackButton()
        {
            streaming_start = 0;
            
            MainPanel.Children.Clear();
            

            //回到首頁全部功能都隱藏

            btn_backToMainPageButton.Visibility = Visibility.Hidden;
            btn_Shooting.Visibility = Visibility.Collapsed;
            folderComboBox_Action.Visibility = Visibility.Collapsed;
            btn_personal_data.Visibility = Visibility.Collapsed;
            btn_look_AnalyzeFile.Visibility = Visibility.Collapsed;
            btn_AnalyzeFile.Visibility = Visibility.Collapsed;
            btn_Rehabilitation.Visibility = Visibility.Collapsed;
            btn_DeviceStreaming_Last.Visibility = Visibility.Collapsed;


            // 更換背景圖爲 /Picture/BackGround2.png
            SetBackgroundImage("Picture/BackGround2.png");

            MainPanel.Children.Add(_Select_Patient);

            System.Diagnostics.Debug.WriteLine("Simulating Stream # of pipeline Ids before removal: " + PipelineController.Instance.PipelineIds.Count);
            if (PipelineController.Instance.PipelineIds.Count > 0)
            {
                PipelineController.Instance.RemovePipeline(0);
            }
            System.Diagnostics.Debug.WriteLine("Simulating Stream # of pipeline Ids after removal: " + PipelineController.Instance.PipelineIds.Count);
        }


        string personal_path;

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {

        }
        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            // 當勾選框被勾選時執行的代碼
            // 例如：開啟某個功能
        }

        private void folderComboBox_Action_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var comboBox = sender as System.Windows.Controls.ComboBox;
            if (comboBox != null && comboBox.SelectedItem != null)
            {
                var selectedItem = comboBox.SelectedItem as System.Windows.Controls.ComboBoxItem;
                if (selectedItem != null && selectedItem.Content.ToString() == "其他")
                {
                    comboBox.IsEditable = true;
                    comboBox.Text = string.Empty;  // 清空文字框
                }
                else
                {
                    comboBox.IsEditable = false;
                }
            }
        }

        private void FolderComboBox_Action_PreviewKeyUp(object sender, KeyEventArgs e)
        {

        }

        //private async void testmark(object sender, RoutedEventArgs e)
        //{
        //    string pa = "C:\\Users\\Andy\\OneDrive - 慈濟大學\\桌面\\專題\\Git-Code\\2024.6.23\\Topics\\C# .NET\\Basic Streaming .NET\\bin\\Debug\\net6.0-windows10.0.17763.0\\sensor_data\\林睿傑_54888123\\2024_9_19\\抬腳\\15頻率20速率30寬度\\20-56-58.csv";
        //    Last_Window last_window = new Last_Window(pa);
        //    last_window.Show();
        //}

        private async Task RunPythonScriptAsync(string scriptPath)
        {
            var start = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"\"{scriptPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = new Process { StartInfo = start, EnableRaisingEvents = true })
            {
                var tcs = new TaskCompletionSource<bool>();

                process.OutputDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        Console.WriteLine(args.Data);
                        Debug.WriteLine(args.Data);
                    }
                };

                process.ErrorDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        Console.WriteLine("ERROR: " + args.Data);
                        Debug.WriteLine("ERROR: " + args.Data);
                    }
                };

                process.Exited += (sender, args) => tcs.SetResult(true);

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await tcs.Task;
            }
        }

        private void clk_Shooting(object sender, RoutedEventArgs e)
        {
            Shooting_Parameters Shooting_Window = new Shooting_Parameters(this, _deviceSteamingUC);
            Shooting_Window.Show();
        }



        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            // 當勾選框被取消勾選時執行的代碼
            // 例如：關閉某個功能
        }

        // 方法：設置窗口背景圖
        private void SetBackgroundImage(string relativeImagePath)
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string fullImagePath = System.IO.Path.Combine(baseDirectory, relativeImagePath);
            Debug.WriteLine($"Trying to load image from: {fullImagePath}"); // 調試信息

            if (System.IO.File.Exists(fullImagePath))
            {
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(fullImagePath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad; // 確保圖像完全加載到內存
                bitmap.EndInit();
                bitmap.Freeze(); // 凍結圖像以提高性能

                ImageBrush brush = new ImageBrush();
                brush.ImageSource = bitmap;
                //brush.Stretch = Stretch.UniformToFill; // 根據需要調整圖像拉伸方式

                // 設置 DockPanel 的背景
                mainDockPanel.Background = brush;

                Debug.WriteLine("Background image successfully loaded and applied."); // 成功加載調試信息
            }
            else
            {
                MessageBox.Show($"Image not found: {fullImagePath}");
                Debug.WriteLine("Image not found at path: " + fullImagePath); // 圖片未找到時的調試信息
            }
        }


        //檢查病患資料
        private void clk__personal_data(object sender, RoutedEventArgs e)
        {
            for (int i = MainPanel.Children.Count - 1; i >= 0; i--)
            {
                var child = MainPanel.Children[i];
                if (child != _deviceSteamingUC)
                {
                    MainPanel.Children.Remove(child);
                }
                else
                {
                    // 隱藏 _deviceSteamingUC
                    _deviceSteamingUC.Visibility = Visibility.Hidden;
                }
            }

            btn_backToMainPageButton.Visibility = Visibility.Visible;
            //拍照、動作、復健一組
            btn_Shooting.Visibility = Visibility.Collapsed;
            folderComboBox_Action.Visibility = Visibility.Collapsed;
            if (streaming_start > 0)
            {
                btn_Rehabilitation.Visibility = Visibility.Visible;
            }


            _CheckData = new Checking_Data_UserControl(this, Patient_Name);
            MainPanel.Children.Add(_CheckData);
        }

        //檢查病患資料
        public void CheckingButton_Click(string patient_path)
        {
            personal_path = patient_path;
            for (int i = MainPanel.Children.Count - 1; i >= 0; i--)
            {
                var child = MainPanel.Children[i];
                if (child != _deviceSteamingUC)
                {
                    MainPanel.Children.Remove(child);
                }
                else
                {
                    // 隱藏 _deviceSteamingUC
                    _deviceSteamingUC.Visibility = Visibility.Hidden;
                }
            }

            btn_backToMainPageButton.Visibility = Visibility.Visible;
            btn_personal_data.Visibility = Visibility.Visible;
            btn_look_AnalyzeFile.Visibility = Visibility.Visible;
            btn_AnalyzeFile.Visibility = Visibility.Visible;
           

            //拍照、動作、復健一組
            btn_Shooting.Visibility = Visibility.Collapsed;
            folderComboBox_Action.Visibility = Visibility.Collapsed;
            btn_Rehabilitation.Visibility = Visibility.Collapsed;

            btn_DeviceStreaming_Last.Visibility= Visibility.Visible;

            // 更換背景圖爲 /Picture/BackGround3.png
            SetBackgroundImage("Picture/BackGround3.png");

            // 清除當前背景（可選）
            this.Background = null;

            _CheckData = new Checking_Data_UserControl(this, patient_path);
            MainPanel.Children.Add(_CheckData);

            System.Diagnostics.Debug.WriteLine("Simulating Stream # of pipeline Ids before removal: " + PipelineController.Instance.PipelineIds.Count);
            if (PipelineController.Instance.PipelineIds.Count > 0)
            {
                PipelineController.Instance.RemovePipeline(0);
            }
            System.Diagnostics.Debug.WriteLine("Simulating Stream # of pipeline Ids after removal: " + PipelineController.Instance.PipelineIds.Count);
            
        }

        private void clk_look_AnalyzeFile(object sender, RoutedEventArgs e)
        {
            for (int i = MainPanel.Children.Count - 1; i >= 0; i--)
            {
                var child = MainPanel.Children[i];
                if (child != _deviceSteamingUC)
                {
                    MainPanel.Children.Remove(child);
                }
                else
                {
                    // 隱藏 _deviceSteamingUC
                    _deviceSteamingUC.Visibility = Visibility.Hidden;
                }
            }


            btn_backToMainPageButton.Visibility = Visibility.Visible;
            //拍照、動作、復健一組
            btn_Shooting.Visibility = Visibility.Collapsed;
            folderComboBox_Action.Visibility = Visibility.Collapsed;
            if (streaming_start > 0)
            {
                btn_Rehabilitation.Visibility = Visibility.Visible;
            }

            _LookFile = new LookFile_Control(this, Patient_Name);
            MainPanel.Children.Add(_LookFile);
        }

        //分析檔案
        private void clk_AnalyzeFile(object sender, RoutedEventArgs e)
        {
            for (int i = MainPanel.Children.Count - 1; i >= 0; i--)
            {
                var child = MainPanel.Children[i];
                if (child != _deviceSteamingUC)
                {
                    MainPanel.Children.Remove(child);
                }
                else
                {
                    // 隱藏 _deviceSteamingUC
                    _deviceSteamingUC.Visibility = Visibility.Hidden;
                }
            }
            //拍照、動作、復健一組
            btn_Shooting.Visibility = Visibility.Collapsed;
            folderComboBox_Action.Visibility = Visibility.Collapsed;
            if (streaming_start > 0)
            {
                btn_Rehabilitation.Visibility = Visibility.Visible;
            }
            

            _AnalyzeFile =new AnalyzeFile(this, Patient_Name);
            MainPanel.Children.Add(_AnalyzeFile);
        }


        private void clk_DeviceStreaming_Last(object sender, RoutedEventArgs e)
        {
            string timestamp = DateTime.Now.ToString("yyyy_MM_dd");

            MainWindow.GlobalDirPath = MainWindow.GlobalDirPath + "\\" + timestamp;

            streaming_start++;
            clk_DeviceStreaming(MainWindow.GlobalDirPath);

            if (streaming_start > 0) {
                btn_DeviceStreaming_Last.Visibility = Visibility.Collapsed;
            }
            
            
        }

        private void clk_Rehabilitation(object sender, RoutedEventArgs e)
        {
            // 顯示 _deviceSteamingUC
            for (int i = MainPanel.Children.Count - 1; i >= 0; i--)
            {
                var child = MainPanel.Children[i];
                if (child != _deviceSteamingUC)
                {
                    MainPanel.Children.Remove(child);
                }
            }
            //串流畫面顯示、拍照按鈕顯示、動作選單顯示
            //復健按鈕隱藏
            _deviceSteamingUC.Visibility = Visibility.Visible;
            btn_Shooting.Visibility = Visibility.Visible;
            folderComboBox_Action.Visibility = Visibility.Visible;
            btn_Rehabilitation.Visibility = Visibility.Collapsed;
        }
    }
}
