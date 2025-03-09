using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Tesseract; // 確保通過 NuGet 安裝了 Tesseract 包
using AForge.Video;
using AForge.Video.DirectShow;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading; // 用於 DispatcherTimer
using AForge.Imaging;
using AForge.Imaging.Filters;
using System.Text.RegularExpressions;
using AForge.Imaging.ColorReduction;
using AForge;
using AForge.Math.Geometry;
using System.Drawing.Imaging;
using Basic_Streaming.NET;
using System.Windows.Media;

namespace Basic_Streaming_NET.Views
{
    /// <summary>
    /// Shooting_Parameters.xaml 的互動邏輯
    /// </summary>

    public partial class Shooting_Parameters : Window
    {

        private FilterInfoCollection videoDevices;
        private VideoCaptureDevice videoSource;
        private Bitmap[] grayImage_array = new Bitmap[3];
        private Bitmap[] electrode_Image_array = new Bitmap[6];
        private string[] electrode_text = new string[16];
        List<string> transform_text = new List<string>();

        private Bitmap currentFrame;
        private Bitmap num_bitmap_nomal;
        private Bitmap photo_num;
        private Bitmap photo_img;
        private Bitmap electrode_photo_to_text;
        private List<Bitmap> extractedContents = new List<Bitmap>();
        private int rectX, rectY, rectWidth, rectHeight;
        private int rectX_num, rectY_num, rectWidth_num, rectHeight_num;
        private int rectX_electrode, rectY_electrode, rectWidth_electrode, rectHeight_electrode;
        int partHeight_num, partHeight_electrode;
        public string Electrode_positive_show;
        public string Electrode_negative_show;
        private List<Bitmap> extractedContents_1 = new List<Bitmap>();
        private List<Bitmap> extractedContents_2 = new List<Bitmap>();
        private List<Bitmap> extractedContents_3 = new List<Bitmap>();
        private List<Bitmap> extractedContents_4 = new List<Bitmap>();
        private List<Bitmap> extractedContents_5 = new List<Bitmap>();
        private List<Bitmap> extractedContents_6 = new List<Bitmap>();

        List<List<Bitmap>> extractedContents_array_Of_Lists = new List<List<Bitmap>>();
        private List<int> red_numbewr_list = new List<int>();
        private List<int> black_numbewr_list = new List<int>();

        private Bitmap[] extractedContents_array = new Bitmap[16];
        private string imgPath;
        private DispatcherTimer memoryReleaseTimer;
        private MainWindow _mainWindow;

        public string Pulse_frequency;//脈衝頻率
        public string Pulse_amplitude;//脈衝震幅

        public string Pulse_rate;//內部脈衝速率

        private DispatcherTimer extractionTimer;

        private DeviceStreaming _deviceSteamingUC;

        private static int count_1 = 1;
        public Shooting_Parameters(MainWindow mainWindow, DeviceStreaming deviceSteamingUC)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            _deviceSteamingUC = deviceSteamingUC;
            GetVideoDevices();
            SetupMemoryReleaseTimer();
            SetupExtractionTimer(); // 在這裡呼叫 SetupExtractionTimer
            this.Loaded += Shooting_Parameters_Loaded;
            this.SizeChanged += Shooting_Parameters_SizeChanged;

            image_stop.IsEnabled = false;
            Photo_TAKE.IsEnabled = false;
            count_1 = 1;
            //Electrode_positive.Text = "";
            //Electrode_negative.Text = "";

            //Electrode_positive_show = "";
            //Electrode_negative_show = "";

            //for (int i = 0; i < 16; i++)
            //{
            //    if (_mainWindow.Shoot_electric[i] == 1)
            //    {
            //        Electrode_positive_show += i + 1 + ",";
            //    }
            //    if (_mainWindow.Shoot_electric[i] == 2)
            //    {
            //        Electrode_negative_show += i + 1 + ",";
            //    }
            //}

            ChangeButtonColors(_mainWindow.Shoot_electric);

            change_num();
        }
        // 窗口加載時調整控件
        private void Shooting_Parameters_Loaded(object sender, RoutedEventArgs e)
        {
            AdjustControls();
        }

        // 窗口尺寸改變時調整控件
        private void Shooting_Parameters_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            AdjustControls();
        }
        // 動態調整所有控件的位置和大小
        private void AdjustControls()
        {
            // 獲取當前窗口的寬度和高度
            double windowWidth = this.ActualWidth;
            double windowHeight = this.ActualHeight;

            // 原始窗口寬度和高度
            double originalWidth = 1295;
            double originalHeight = 989;

            // 計算寬度和高度的比例
            double widthRatio = windowWidth / originalWidth;
            double heightRatio = windowHeight / originalHeight;

            // 調整每個控件的位置和大小
            AdjustControl(Open_ccd_Copy, 28, 554, 114, 23, widthRatio, heightRatio);
            AdjustControl(Boder_main, 28, 41, 800, 450, widthRatio, heightRatio);
            AdjustControl(image_1, 28, 41, 800, 450, widthRatio, heightRatio);
            AdjustControl(Show_word_1, 708, 524, 120, 35, widthRatio, heightRatio);
            AdjustControl(Show_word_2, 708, 564, 120, 35, widthRatio, heightRatio);
            AdjustControl(num_photo_border, 559, 684, 278, 127, widthRatio, heightRatio);
            AdjustControl(num_photo, 559, 684, 278, 127, widthRatio, heightRatio);
            AdjustControl(Ccd_chose_ComboBox, 25, 523, 120, 30, widthRatio, heightRatio);
            AdjustControl(Show_word_3, 708, 604, 120, 35, widthRatio, heightRatio);
            AdjustControl(image_stop, 25, 590, 114, 28, widthRatio, heightRatio);
            AdjustControl(Photo_TAKE, 28, 630, 114, 43, widthRatio, heightRatio);
            AdjustControl(num_photo_save, 783, 831, 100, 30, widthRatio, heightRatio);
            AdjustControl(num_photo_ComboBox, 559, 831, 120, 30, widthRatio, heightRatio);
            AdjustControl(num_photo_border_2, 224, 524, 278, 381, widthRatio, heightRatio);
            AdjustControl(num_photo_2, 224, 524, 278, 381, widthRatio, heightRatio);
            AdjustControl(num_photo_save_2, 336, 945, 100, 30, widthRatio, heightRatio);
            AdjustControl(exit, 1021, 85, 74, 18, widthRatio, heightRatio);
            AdjustControl(num_photo_save_Copy, 783, 831, 100, 30, widthRatio, heightRatio);
            AdjustControl(extractedContents_photo_6, 1447, 24, 112, 122, widthRatio, heightRatio);
            AdjustControl(extractedContents_photo_17, 1447, 24, 112, 122, widthRatio, heightRatio);
            AdjustControl(extractedContents_photo_1, 1287, 191, 111, 123, widthRatio, heightRatio);
            AdjustControl(extractedContents_photo_1_border, 1287, 191, 111, 123, widthRatio, heightRatio);
            AdjustControl(extractedContents_photo_7, 1447, 191, 112, 123, widthRatio, heightRatio);
            AdjustControl(extractedContents_photo_7_border, 1447, 191, 112, 123, widthRatio, heightRatio);
            AdjustControl(extractedContents_photo_12, 1617, 173, 111, 123, widthRatio, heightRatio);
            AdjustControl(extractedContents_photo_12_border複製__C_2, 1617, 191, 111, 123, widthRatio, heightRatio);
            AdjustControl(extractedContents_photo_2, 1287, 346, 111, 123, widthRatio, heightRatio);
            AdjustControl(extractedContents_photo_2_border, 1287, 346, 111, 123, widthRatio, heightRatio);
            AdjustControl(extractedContents_photo_8, 1447, 346, 112, 123, widthRatio, heightRatio);
            AdjustControl(extractedContents_photo_8_border, 1447, 346, 112, 123, widthRatio, heightRatio);
            AdjustControl(extractedContents_photo_13, 1617, 346, 111, 123, widthRatio, heightRatio);
            AdjustControl(extractedContents_photo_13_border, 1617, 346, 111, 123, widthRatio, heightRatio);
            AdjustControl(extractedContents_photo_3, 1287, 495, 111, 122, widthRatio, heightRatio);
            AdjustControl(extractedContents_photo_3_border, 1287, 495, 111, 122, widthRatio, heightRatio);
            AdjustControl(extractedContents_photo_9, 1447, 495, 112, 122, widthRatio, heightRatio);
            AdjustControl(extractedContents_photo_9_border, 1447, 495, 112, 122, widthRatio, heightRatio);
            AdjustControl(extractedContents_photo_14, 1617, 495, 111, 122, widthRatio, heightRatio);
            AdjustControl(extractedContents_photo_14_border, 1617, 495, 111, 122, widthRatio, heightRatio);
            AdjustControl(extractedContents_photo_4, 1287, 638, 111, 123, widthRatio, heightRatio);
            AdjustControl(extractedContents_photo_4_border, 1287, 638, 111, 123, widthRatio, heightRatio);
            AdjustControl(extractedContents_photo_10, 1447, 638, 112, 123, widthRatio, heightRatio);
            AdjustControl(extractedContents_photo_10_border, 1447, 638, 112, 123, widthRatio, heightRatio);
            AdjustControl(extractedContents_photo_15, 1617, 638, 111, 123, widthRatio, heightRatio);
            AdjustControl(extractedContents_photo_15_border, 1617, 638, 111, 123, widthRatio, heightRatio);
            AdjustControl(extractedContents_photo_5, 1287, 780, 111, 122, widthRatio, heightRatio);
            AdjustControl(extractedContents_photo_5_border, 1287, 780, 111, 122, widthRatio, heightRatio);
            AdjustControl(extractedContents_photo_11, 1447, 780, 112, 122, widthRatio, heightRatio);
            AdjustControl(extractedContents_photo_11_border, 1447, 780, 112, 122, widthRatio, heightRatio);
            AdjustControl(extractedContents_photo_16, 1617, 780, 111, 122, widthRatio, heightRatio);
            AdjustControl(extractedContents_photo_16_border, 1617, 780, 111, 122, widthRatio, heightRatio);
            AdjustControl(Electrode_positive, 976, 241, 280, 31, widthRatio, heightRatio);
            AdjustControl(Electrode_negative, 976, 277, 280, 32, widthRatio, heightRatio);
            AdjustControl(extractedContents_photo_6_button, 1028, 410, 60, 59, widthRatio, heightRatio);
            AdjustControl(extractedContents_photo_1_button, 948, 493, 58, 60, widthRatio, heightRatio);
            AdjustControl(extractedContents_photo_7_button, 1028, 493, 60, 60, widthRatio, heightRatio);
            AdjustControl(extractedContents_photo_12_button, 1118, 493, 58, 60, widthRatio, heightRatio);
            AdjustControl(extractedContents_photo_2_button, 948, 571, 58, 59, widthRatio, heightRatio);
            AdjustControl(extractedContents_photo_8_button, 1028, 571, 60, 59, widthRatio, heightRatio);
            AdjustControl(extractedContents_photo_13_button, 1118, 571, 58, 59, widthRatio, heightRatio);
            AdjustControl(extractedContents_photo_3_button, 948, 643, 58, 60, widthRatio, heightRatio);
            AdjustControl(extractedContents_photo_9_button, 1028, 643, 60, 60, widthRatio, heightRatio);
            AdjustControl(extractedContents_photo_14_button, 1118, 643, 58, 60, widthRatio, heightRatio);
            AdjustControl(extractedContents_photo_4_button, 948, 716, 58, 59, widthRatio, heightRatio);
            AdjustControl(extractedContents_photo_10_button, 1028, 716, 60, 59, widthRatio, heightRatio);
            AdjustControl(extractedContents_photo_15_button, 1118, 716, 58, 59, widthRatio, heightRatio);
            AdjustControl(extractedContents_photo_5_button, 948, 791, 58, 59, widthRatio, heightRatio);
            AdjustControl(extractedContents_photo_11_button, 1028, 791, 60, 59, widthRatio, heightRatio);
            AdjustControl(extractedContents_photo_16_button, 1118, 791, 58, 59, widthRatio, heightRatio);
            AdjustControl(update, 34, 698, 102, 63, widthRatio, heightRatio);
            AdjustControl(frequency, 567, 524, 123, 35, widthRatio, heightRatio);
            AdjustControl(rate, 548, 564, 142, 35, widthRatio, heightRatio);
            AdjustControl(Amplitude, 588, 604, 102, 35, widthRatio, heightRatio);
            AdjustControl(positive_electrode, 882, 241, 76, 31, widthRatio, heightRatio);
            AdjustControl(negative_electrode, 882, 277, 76, 32, widthRatio, heightRatio);


            // 按此模式繼續爲其他控件調整...
        }
        // 封裝的調整控件位置和大小的邏輯
        private void AdjustControl(FrameworkElement control, double originalLeft, double originalTop, double originalWidth, double originalHeight, double widthRatio, double heightRatio)
        {
            if (control == null) return;

            // 調整寬度和高度
            if (originalWidth > 0) control.Width = originalWidth * widthRatio;
            if (originalHeight > 0) control.Height = originalHeight * heightRatio;

            // 調整位置
            double newLeft = originalLeft * widthRatio;
            double newTop = originalTop * heightRatio;

            // 使用 Canvas.SetLeft 和 Canvas.SetTop 來設置位置
            if (control.Parent is Canvas)
            {
                Canvas.SetLeft(control, newLeft);
                Canvas.SetTop(control, newTop);
            }
            else
            {
                control.Margin = new Thickness(newLeft, newTop, 0, 0);
            }
        }

        // 定義 SetupExtractionTimer 方法
        private void SetupExtractionTimer()
        {
            extractionTimer = new DispatcherTimer();
            extractionTimer.Interval = TimeSpan.FromSeconds(1); // 設置間隔時間為 10 秒，可根據需求調整
            extractionTimer.Tick += ExtractionTimer_Tick; // 設定定時器觸發的事件
        }
        // 定時器的觸發事件處理程序
        private void ExtractionTimer_Tick(object sender, EventArgs e)
        {
            // 清空 ComboBox 和列表
            extractedContents.Clear();
            extractedContents_1.Clear();
            extractedContents_2.Clear();
            extractedContents_3.Clear();
            extractedContents_4.Clear();
            extractedContents_5.Clear();
            extractedContents_6.Clear();

            extractedContents_array_Of_Lists.Clear();

            // 將空列表添加到數組中
            extractedContents_array_Of_Lists.Add(extractedContents_1);
            extractedContents_array_Of_Lists.Add(extractedContents_2);
            extractedContents_array_Of_Lists.Add(extractedContents_3);
            extractedContents_array_Of_Lists.Add(extractedContents_4);
            extractedContents_array_Of_Lists.Add(extractedContents_5);
            extractedContents_array_Of_Lists.Add(extractedContents_6);

            MainWindow.Pulse_frequency = "";
            MainWindow.Pulse_amplitude = "";
            MainWindow.Pulse_rate = "";

            // 執行提取和處理方法
            Photo_Retrieve();
            Photo_to_text();
            electrode_to_text();



        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 提示使用者確認關閉
            //var result = MessageBox.Show("確定要關閉視窗嗎？", "確認", MessageBoxButton.YesNo, MessageBoxImage.Question);
            //if (result == MessageBoxResult.No)
            //{
            //    e.Cancel = true; // 取消關閉
            //}
            //else
            //{
            //    // 停止視頻源
            //    if (videoSource != null && videoSource.IsRunning)
            //    {
            //        videoSource.SignalToStop();
            //    }
            //    // 其他清理操作
            //}\

            // 停止視頻源
            if (videoSource != null && videoSource.IsRunning)
            {
                videoSource.SignalToStop();
            }
        }

        // BitmapImage 轉換爲 Bitmap
        private Bitmap BitmapImage2Bitmap(BitmapImage bitmapImage)
        {
            using (MemoryStream outStream = new MemoryStream())
            {
                BitmapEncoder enc = new BmpBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bitmapImage));
                enc.Save(outStream);
                System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(outStream);
                return new Bitmap(bitmap);
            }
        }

        // Bitmap 轉換回 BitmapImage
        private BitmapImage Bitmap2BitmapImage(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
                memory.Position = 0;
                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                return bitmapImage;
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                Photo_Retrieve();
                Photo_to_text();
                electrode_to_text();
            }
        }

        private void num_photo_save_Click(object sender, RoutedEventArgs e)
        {
            // 獲取 Image 控件中的 BitmapImage
            BitmapImage bitmapImage = num_photo.Source as BitmapImage;

            if (bitmapImage != null)
            {
                // 將 BitmapImage 轉換爲 Bitmap
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    BitmapEncoder encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmapImage));
                    encoder.Save(memoryStream);
                    Bitmap bitmap = new Bitmap(memoryStream);

                    // 將 Bitmap 保存到文件
                    string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                    string filename = $"{timestamp}.png";
                    string filePath = "C:\\Users\\TCUMI\\Downloads\\iMAGE_TEXT\\iMAGE_TEXT\\裁切圖片\\" + filename; // 在這裏指定保存文件的路徑

                    bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
                    MessageBox.Show("儲存完畢 路徑為" + filePath);
                }
            }
            else
            {
                MessageBox.Show("num_photo 控件中沒有加載任何圖片。", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void Open_ccd_Copy_Click(object sender, RoutedEventArgs e)
        {
            image_stop.IsEnabled = true;
            Photo_TAKE.IsEnabled = true;
            Open_ccd();

        }

        // 按鈕事件：控制定時器的啟動和停止
        private void Photo_TAKE_Click(object sender, RoutedEventArgs e)
        {
            if (Ccd_chose_ComboBox.Text != "")
            {
                if (extractionTimer.IsEnabled)
                {
                    // 如果定時器已經運行，就停止它
                    extractionTimer.Stop();
                    MessageBox.Show("定時拍攝已停止");
                }
                else
                {
                    // 如果定時器沒有運行，就啟動它
                    extractionTimer.Start();
                    //MessageBox.Show("定時拍攝已啟動，每10秒拍攝一次");
                }
            }
            else
            {
                MessageBox.Show("無選擇攝影機");
            }

        }

        private void Photo_Retrieve()
        {
            Dispatcher.Invoke(() =>
            {
                //if (videoSource != null && videoSource.IsRunning)
                //{

                //    // 不再停止視頻源
                //    videoSource.SignalToStop();
                //    videoSource.WaitForStop();
                //    videoSource.NewFrame -= new NewFrameEventHandler(video_NewFrame);
                //}

                Bitmap croppedBitmap = null;
                Bitmap croppedBitmap2 = null;
                Bitmap croppedBitmap3 = null;

                try
                {
                    // 創建裁剪後的圖像
                    croppedBitmap = currentFrame.Clone(new System.Drawing.Rectangle(rectX, rectY, rectWidth, rectHeight), currentFrame.PixelFormat);
                    croppedBitmap2 = currentFrame.Clone(new System.Drawing.Rectangle(rectX_num, rectY_num, rectWidth_num, rectHeight_num), currentFrame.PixelFormat);
                    croppedBitmap3 = currentFrame.Clone(new System.Drawing.Rectangle(rectX_electrode, rectY_electrode, rectWidth_electrode, rectHeight_electrode), currentFrame.PixelFormat);

                    this.photo_num = croppedBitmap2;
                    this.photo_img = croppedBitmap;
                    this.electrode_photo_to_text = croppedBitmap3;

                    // 更新UI顯示裁剪後的圖像
                    BitmapImage numbitmap = Bitmap2BitmapImage(croppedBitmap2);
                    numbitmap.DecodePixelWidth = Convert.ToInt32(num_photo.Width); // 照這比例輸出畫面
                    num_photo_2.Source = numbitmap;

                    // 更新UI顯示電極裁剪後的圖像
                    BitmapImage electrodebitmap = Bitmap2BitmapImage(croppedBitmap3);

                }
                catch (Exception ex)
                {
                    //MessageBox.Show("保存裁剪圖像失敗: " + ex.Message);
                }
                finally
                {
                    croppedBitmap?.Dispose();
                }
            });
        }

        private void Photo_to_text()
        {
            //List<string> transform_text = new List<string>();
            Bitmap grayImage = photo_num;

            for (int i = 0; i < 3; i++)
            {
                System.Drawing.Rectangle srcRect = new System.Drawing.Rectangle(0, i * this.partHeight_num, grayImage.Width, this.partHeight_num);
                this.grayImage_array[i] = new Bitmap(srcRect.Width, srcRect.Height);

                using (Graphics g = Graphics.FromImage(this.grayImage_array[i]))
                {
                    g.DrawImage(grayImage, new System.Drawing.Rectangle(0, 0, srcRect.Width, srcRect.Height), srcRect, GraphicsUnit.Pixel);
                }
            }

            Bitmap[] Image_process = grayImage_array;
            for (int i = 0; i < 3; i++)
            {
                // 應用灰度濾鏡
                Grayscale grayscaleFilter = new Grayscale(0.2125, 0.7154, 0.0721);
                Image_process[i] = grayscaleFilter.Apply(Image_process[i]);

                // 應用中值濾波以減少噪聲
                Median medianFilter = new Median(3);
                Image_process[i] = medianFilter.Apply(Image_process[i]);

                //自適應2質化
                BradleyLocalThresholding bradleyLocalThresholdingFilter = new BradleyLocalThresholding();
                bradleyLocalThresholdingFilter.ApplyInPlace(Image_process[i]);

                BitmapImage show = Bitmap2BitmapImage(Image_process[1]);
                num_photo.Source = show;

                string turnword = ImageToText(Image_process[i], "300");  // 假設 photo_num 是當前處理的圖像
                string[] outputnum = turnword.Split('\n');
                Regex regex = new Regex("[^0123456789Hzus]+");
                List<string> filteredWords = new List<string>();
                foreach (string word in outputnum)
                {
                    // 替換每個字符串中不包含指定字符的部分
                    string filteredWord = regex.Replace(word, "");
                    // 如果替換後的字符串不爲空，則添加到列表中
                    if (!string.IsNullOrEmpty(filteredWord))
                    {
                        filteredWords.Add(filteredWord);
                    }
                }

                // 準備一個新的列表來存儲同時包含"Hz"和"us"的字符串
                foreach (string word in filteredWords)
                {
                    // 檢查字符串是否同時包含"Hz"和"us"
                    if (word.Contains("Hz") || word.Contains("us"))
                    {
                        transform_text.Add(word);
                    }
                }
            }
            try
            {
                string directoryPath = MainWindow.GlobalDirPath;
                if (transform_text.Count == 2)
                {
                    MainWindow.Pulse_frequency = transform_text[0];
                    MainWindow.Pulse_amplitude = transform_text[1];

                    Show_word_1.Text = MainWindow.Pulse_frequency;
                    Show_word_2.Text = "";
                    Show_word_3.Text = MainWindow.Pulse_amplitude;

                    if (extractionTimer.IsEnabled)
                    {
                        // 如果定時器已經運行，就停止它
                        extractionTimer.Stop();
                        MessageBox.Show("定時拍攝已停止");
                        Random random = new Random();
                        int randomnum = random.Next(1, 3);

                        for (int i = 0; i < randomnum; i++)
                        {
                            int randomInt = random.Next(0, 16);
                            int rand = random.Next(1, 2);
                            
                            _mainWindow.Shoot_electric[randomInt] = rand;
                            ChangeButtonColors(_mainWindow.Shoot_electric);

                            //count_1--;

                        }

                    }
                }
                else if (transform_text.Count == 3)
                {
                    MainWindow.Pulse_frequency = transform_text[0];
                    MainWindow.Pulse_amplitude = transform_text[3];
                    MainWindow.Pulse_rate = transform_text[1];

                    Show_word_1.Text = transform_text[0];
                    Show_word_2.Text = transform_text[1];
                    Show_word_3.Text = transform_text[2];

                    if (extractionTimer.IsEnabled)
                    {
                        // 如果定時器已經運行，就停止它
                        extractionTimer.Stop();
                        MessageBox.Show("定時拍攝已停止");

                        Random random = new Random();
                        int randomnum = random.Next(1, 3); 

                        for (int i = 0; i < randomnum; i++)
                        {
                            int randomInt = random.Next(0, 16); 
                            int rand = random.Next(1, 2); 

                            _mainWindow.Shoot_electric[randomInt] = rand;
                            ChangeButtonColors(_mainWindow.Shoot_electric);
                            //count_1--;

                        }




                    }
                }
                else
                {
                    Show_word_1.Text = "";
                    Show_word_2.Text = "";
                    Show_word_3.Text = "";
                    //MessageBox.Show("無判別到文字");
                }
            }
            catch
            {
                Show_word_1.Text = "";
                Show_word_2.Text = "";
                Show_word_3.Text = "";
                //MessageBox.Show("無判別到文字");
            }

            this.grayImage_array = Image_process;
        }

        private void electrode_to_text()
        {
            Bitmap original = electrode_photo_to_text;
            Bitmap grayImage = ConvertTo24bppRgb(original); // 確保圖像爲24位RGB格式

            Bitmap[] grayImage_array = new Bitmap[6];
            List<string> transform_text = new List<string>();

            for (int i = 0; i < 6; i++)
            {
                System.Drawing.Rectangle srcRect = new System.Drawing.Rectangle(0, i * this.partHeight_electrode, grayImage.Width, this.partHeight_electrode);
                this.electrode_Image_array[i] = new Bitmap(srcRect.Width, srcRect.Height);

                using (Graphics g = Graphics.FromImage(this.electrode_Image_array[i]))
                {
                    g.DrawImage(grayImage, new System.Drawing.Rectangle(0, 0, srcRect.Width, srcRect.Height), srcRect, GraphicsUnit.Pixel);
                }
            }

            Bitmap[] Image_process = electrode_Image_array;

            // 處理每個部分，找出框框並保留矩形內部的內容
            for (int i = 0; i < Image_process.Length; i++)
            {
                Bitmap part = Image_process[i];

                // 轉換爲灰度圖像
                Grayscale grayscaleFilter = new Grayscale(0.2125, 0.7154, 0.0721);
                Bitmap grayImage_2 = grayscaleFilter.Apply(part);
                // 預處理：應用高斯模糊來去噪
                GaussianBlur blurFilter = new GaussianBlur(1, 7);
                Bitmap blurredImage = blurFilter.Apply(grayImage_2);

                // 預處理：增強對比度
                ContrastCorrection contrastFilter = new ContrastCorrection(30);
                Bitmap enhancedImage = contrastFilter.Apply(blurredImage);

                // 應用銳化濾鏡
                Sharpen sharpenFilter = new Sharpen();
                Bitmap sharpenedImage = sharpenFilter.Apply(enhancedImage);

                // 應用Canny邊緣檢測
                CannyEdgeDetector edgeDetector = new CannyEdgeDetector();
                Bitmap edges = edgeDetector.Apply(sharpenedImage);

                // 記錄已處理的矩形區域
                Bitmap processedBitmap = new Bitmap(part.Width, part.Height);

                // 使用BlobCounter找到所有的連通區域
                BlobCounter blobCounter = new BlobCounter();
                blobCounter.FilterBlobs = true;
                blobCounter.MinHeight = 5;
                blobCounter.MinWidth = 5;
                blobCounter.ProcessImage(edges);
                Blob[] blobs = blobCounter.GetObjectsInformation();
                SimpleShapeChecker shapeChecker = new SimpleShapeChecker();

                // 計算邊界矩形的方法
                foreach (Blob blob in blobs)
                {
                    if (blob.Area >= 125)
                    {
                        List<IntPoint> edgePoints = blobCounter.GetBlobsEdgePoints(blob);
                        System.Drawing.Rectangle rect = GetBoundingRectangle(edgePoints);

                        // 檢查該矩形區域是否已經處理過
                        if (!IsAreaProcessed(processedBitmap, rect))
                        {
                            // 標記該區域為已處理
                            MarkAreaAsProcessed(processedBitmap, rect);

                            Bitmap extractedRect = new Bitmap(rect.Width, rect.Height);

                            using (Graphics g = Graphics.FromImage(extractedRect))
                            {
                                g.DrawImage(part, new System.Drawing.Rectangle(0, 0, rect.Width, rect.Height), rect, GraphicsUnit.Pixel);
                            }



                            switch (i)
                            {
                                case 0:
                                    // 將提取的內容添加到全局列表
                                    extractedContents_1.Add(extractedRect);
                                    break;
                                case 1:
                                    // 將提取的內容添加到全局列表
                                    extractedContents_2.Add(extractedRect);
                                    break;
                                case 2:
                                    // 將提取的內容添加到全局列表
                                    extractedContents_3.Add(extractedRect);
                                    break;
                                case 3:
                                    // 將提取的內容添加到全局列表
                                    extractedContents_4.Add(extractedRect);
                                    break;
                                case 4:
                                    // 將提取的內容添加到全局列表
                                    extractedContents_5.Add(extractedRect);
                                    break;
                                case 5:
                                    // 將提取的內容添加到全局列表
                                    extractedContents_6.Add(extractedRect);
                                    break;
                            }

                            extractedContents.Add(extractedRect);

                            // 在ComboBox中添加條目
                            int width = extractedRect.Width;
                            int height = extractedRect.Height;
                        }
                    }
                }
            }
            photo_transfom_red_black();
        }

        private static bool IsAreaProcessed(Bitmap bitmap, System.Drawing.Rectangle rect)
        {
            for (int y = rect.Top; y < rect.Bottom; y++)
            {
                for (int x = rect.Left; x < rect.Right; x++)
                {
                    if (bitmap.GetPixel(x, y).ToArgb() == System.Drawing.Color.Black.ToArgb())
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static void MarkAreaAsProcessed(Bitmap bitmap, System.Drawing.Rectangle rect)
        {
            for (int y = rect.Top; y < rect.Bottom; y++)
            {
                for (int x = rect.Left; x < rect.Right; x++)
                {
                    bitmap.SetPixel(x, y, System.Drawing.Color.Black);
                }
            }
        }

        private System.Drawing.Rectangle GetBoundingRectangle(List<IntPoint> points)
        {
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;

            foreach (IntPoint point in points)
            {
                if (point.X < minX) minX = point.X;
                if (point.Y < minY) minY = point.Y;
                if (point.X > maxX) maxX = point.X;
                if (point.Y > maxY) maxY = point.Y;
            }

            return new System.Drawing.Rectangle(minX, minY, maxX - minX, maxY - minY);
        }

        private Bitmap[] SplitImageIntoParts(Bitmap image, int parts)
        {
            int partHeight = image.Height / parts;
            Bitmap[] result = new Bitmap[parts];

            for (int i = 0; i < parts; i++)
            {
                System.Drawing.Rectangle srcRect = new System.Drawing.Rectangle(0, i * partHeight, image.Width, partHeight);
                result[i] = new Bitmap(srcRect.Width, srcRect.Height);

                using (Graphics g = Graphics.FromImage(result[i]))
                {
                    g.DrawImage(image, new System.Drawing.Rectangle(0, 0, srcRect.Width, srcRect.Height), srcRect, GraphicsUnit.Pixel);
                }
            }

            return result;
        }

        private static Bitmap ExtractPolygonContent(Bitmap image, List<IntPoint> points, System.Drawing.Rectangle rect)
        {
            // 創建與目標區域大小相同的空白圖像
            Bitmap extractedImage = new Bitmap(rect.Width, rect.Height);
            Graphics g = Graphics.FromImage(extractedImage);
            g.Clear(System.Drawing.Color.Transparent);

            // 創建多邊形路徑
            System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath();
            System.Drawing.PointF[] drawingPoints = new System.Drawing.PointF[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                drawingPoints[i] = new System.Drawing.PointF(points[i].X - rect.X, points[i].Y - rect.Y);
            }
            path.AddPolygon(drawingPoints);

            // 裁剪並繪製多邊形內容
            g.SetClip(path);
            g.DrawImage(image, new System.Drawing.Rectangle(0, 0, rect.Width, rect.Height), rect, GraphicsUnit.Pixel);
            g.ResetClip();

            return extractedImage;
        }

        private Bitmap ConvertToHSV(Bitmap image)
        {
            var filter = new ColorImageQuantizer(new MedianCutQuantizer());
            Bitmap hsvImage = filter.ReduceColors(image, 256);
            return hsvImage;
        }

        private Bitmap ConvertTo24bppRgb(Bitmap image)
        {
            if (image.PixelFormat != System.Drawing.Imaging.PixelFormat.Format24bppRgb)
            {
                Bitmap formattedImage = new Bitmap(image.Width, image.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                using (Graphics g = Graphics.FromImage(formattedImage))
                {
                    g.DrawImage(image, 0, 0, image.Width, image.Height);
                }
                return formattedImage;
            }
            return image;
        }

        private Bitmap CorrectRGB(Bitmap image)
        {
            int width = image.Width;
            int height = image.Height;

            int minR = 255, minG = 255, minB = 255;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    System.Drawing.Color pixel = image.GetPixel(x, y);
                    if (pixel.R < minR) minR = pixel.R;
                    if (pixel.G < minG) minG = pixel.G;
                    if (pixel.B < minB) minB = pixel.B;
                }
            }

            Bitmap correctedImage = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    System.Drawing.Color pixel = image.GetPixel(x, y);
                    int correctedR = pixel.R - minR;
                    int correctedG = pixel.G - minG;
                    int correctedB = pixel.B - minB;
                    correctedImage.SetPixel(x, y, System.Drawing.Color.FromArgb(correctedR, correctedG, correctedB));
                }
            }

            return correctedImage;
        }

        private void CropImage(string imgPath, string savefilePath, int locat_x, int locate_y, int width, int height)
        {
            BitmapImage originalImage = new BitmapImage(new Uri(imgPath, UriKind.Absolute));
            Int32Rect cropArea = new Int32Rect(locat_x, locate_y, width, height);
            CroppedBitmap croppedBitmap = new CroppedBitmap(originalImage, cropArea);
            SaveCroppedBitmapToFile(croppedBitmap, savefilePath);
        }

        private void SaveCroppedBitmapToFile(CroppedBitmap croppedBitmap, string filePath)
        {
            using (FileStream fileStream = new FileStream(filePath, FileMode.Create))
            {
                BitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(croppedBitmap));
                encoder.Save(fileStream);
            }
        }

        private string ImageToText(Bitmap image, string DPI)
        {
            using (var engine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default))
            {
                engine.DefaultPageSegMode = PageSegMode.Auto;
                engine.SetVariable("tessedit_char_whitelist", "0123456789Hzus");
                engine.SetVariable("user_defined_dpi", DPI);

                using (var ms = new MemoryStream())
                {
                    image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    ms.Position = 0;
                    using (var pix = Pix.LoadFromMemory(ms.ToArray()))
                    {
                        using (var page = engine.Process(pix))
                        {
                            return page.GetText();
                        }
                    }
                }
            }
        }

        private void video_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            currentFrame?.Dispose();
            currentFrame = (Bitmap)eventArgs.Frame.Clone();

            using (var currentFrame = (Bitmap)eventArgs.Frame.Clone())
            {
                int videoWidth = currentFrame.Width;
                int videoHeight = currentFrame.Height;
                int X2 = 0, Y2 = 0;

                rectHeight = videoHeight;
                rectWidth = (int)(rectHeight * 0.74);
                rectX = (videoWidth - rectWidth) / 2;
                rectY = (videoHeight - rectHeight) / 2;

                rectX_num = Convert.ToInt32(rectX + (rectWidth / 2.3));
                rectY_num = Convert.ToInt32(rectY + (rectHeight / 7.04));
                X2 = Convert.ToInt32((rectX_num + rectWidth) / 1.44);
                Y2 = Convert.ToInt32((rectY_num + rectHeight) / 2.31);
                rectWidth_num = Convert.ToInt32(X2 - rectX_num);
                rectHeight_num = Convert.ToInt32(Y2 - rectY_num);

                rectX_electrode = rectX + rectX_num / 25;
                rectY_electrode = rectY_num + rectHeight_num / 4;
                rectWidth_electrode = rectWidth_num - rectWidth_num / 7;
                rectHeight_electrode = (rectHeight - rectY_electrode) - rectY_electrode / 15;
                rectHeight_electrode = rectHeight_electrode - (rectHeight_electrode / 9) * 3;

                this.partHeight_num = rectHeight_num / 3;
                this.partHeight_electrode = rectHeight_electrode / 6;

                using (var graphics = Graphics.FromImage(currentFrame))
                {
                    using (var pen = new System.Drawing.Pen(System.Drawing.Color.Red, 3))
                    {
                        graphics.DrawRectangle(pen, rectX, rectY, rectWidth, rectHeight);
                        graphics.DrawRectangle(pen, rectX_num, rectY_num, rectWidth_num, rectHeight_num);
                        graphics.DrawRectangle(pen, rectX_electrode, rectY_electrode, rectWidth_electrode, rectHeight_electrode);
                        for (int i = 1; i < 3; i++)
                        {
                            graphics.DrawLine(pen, rectX_num, rectY_num + this.partHeight_num * i, rectX_num + rectWidth_num, rectY_num + this.partHeight_num * i);
                        }
                        for (int i = 1; i < 6; i++)
                        {
                            graphics.DrawLine(pen, rectX_electrode, rectY_electrode + this.partHeight_electrode * i, rectX_electrode + rectWidth_electrode, rectY_electrode + this.partHeight_electrode * i);
                        }
                    }
                }

                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        var bitmapSource = ConvertBitmapToBitmapSource(currentFrame);
                        image_1.Source = bitmapSource;
                        image_1.Height = 450;
                        image_1.Width = 800;
                        Boder_main.Width = image_1.Width;
                        Boder_main.Height = image_1.Height;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Exception: {ex.Message}");
                        Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                    }
                });
            }
        }

        private BitmapSource ConvertBitmapToBitmapSource(Bitmap bitmap)
        {
            var hBitmap = bitmap.GetHbitmap();
            BitmapSource bitmapSource;

            try
            {
                bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                DeleteObject(hBitmap);
            }

            return bitmapSource;
        }

        private void num_photo_ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = num_photo_ComboBox.SelectedItem as ComboBoxItem;
            if (selectedItem != null)
            {
                string selectedContent = selectedItem.Content.ToString();

                switch (selectedContent)
                {
                    case "Option 1":
                        if (this.grayImage_array[0] != null)
                        {
                            num_photo.Source = Bitmap2BitmapImage(this.grayImage_array[0]);
                        }
                        else
                        {
                            MessageBox.Show("此選項無圖片");
                        }
                        break;
                    case "Option 2":
                        if (this.grayImage_array[1] != null)
                        {
                            num_photo.Source = Bitmap2BitmapImage(this.grayImage_array[1]);
                        }
                        else
                        {
                            MessageBox.Show("此選項無圖片");
                        }
                        break;
                    case "Option 3":
                        if (this.grayImage_array[2] != null)
                        {
                            num_photo.Source = Bitmap2BitmapImage(this.grayImage_array[2]);
                        }
                        else
                        {
                            MessageBox.Show("此選項無圖片");
                        }
                        break;
                }
            }
        }

        private void num_photo_save_2_Click(object sender, RoutedEventArgs e)
        {
            BitmapImage bitmapImage = num_photo_2.Source as BitmapImage;

            if (bitmapImage != null)
            {
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    BitmapEncoder encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmapImage));
                    encoder.Save(memoryStream);
                    Bitmap bitmap = new Bitmap(memoryStream);

                    string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                    string filename = $"{timestamp}.png";
                    string filePath = "C:\\Users\\TCUMI\\Downloads\\iMAGE_TEXT\\iMAGE_TEXT\\裁切圖片\\" + filename + ".png";
                    bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
                    MessageBox.Show("儲存完畢 路徑為" + filePath);
                }
            }
            else
            {
                MessageBox.Show("num_photo 控件中沒有加載任何圖片。", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void exit_Click(object sender, RoutedEventArgs e)
        {
            if (videoSource != null && videoSource.IsRunning)
            {
                videoSource.SignalToStop();
            }
            this.Close();
        }


        private void GetVideoDevices()
        {
            videoDevices = new FilterInfoCollection(AForge.Video.DirectShow.FilterCategory.VideoInputDevice);
            foreach (AForge.Video.DirectShow.FilterInfo device in videoDevices)
            {
                Ccd_chose_ComboBox.Items.Add(device.Name);
            }
            if (Ccd_chose_ComboBox.Items.Count > 0)
            {
                Ccd_chose_ComboBox.SelectedIndex = 0;
            }
        }



        private void SetupMemoryReleaseTimer()
        {
            memoryReleaseTimer = new DispatcherTimer();
            memoryReleaseTimer.Interval = TimeSpan.FromSeconds(5);
            memoryReleaseTimer.Tick += MemoryReleaseTimer_Tick;
            memoryReleaseTimer.Start();
        }

        private void MemoryReleaseTimer_Tick(object sender, EventArgs e)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        private void LoadImage(Bitmap cot_image)
        {
            Console.WriteLine("開始加載圖片: " + imgPath);
            BitmapImage bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap = Bitmap2BitmapImage(cot_image);
            bitmap.DecodePixelWidth = 600;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();

            this.imgPath = Convert.ToString(bitmap);
            image_1.Source = bitmap;
            Console.WriteLine("圖片加載完成");
        }

        private BitmapSource ConvertBitmap(Bitmap source)
        {
            var hBitmap = source.GetHbitmap();
            BitmapSource result;

            try
            {
                result = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                DeleteObject(hBitmap);
            }

            return result;
        }
        string filePath;


        private void confirm_Click_1(object sender, RoutedEventArgs e)
        {

            //string photo_folder_name = dirPath + DateTime.Now.ToString("HH:mm ss tt")+ "頻率" + transform_text[0];//病人資料夾路徑加黨名_創資料夾

            if (transform_text.Count > 2)
            {

                //// 檢查資料夾是否存在，如果不存在則創建
                //if (!Directory.Exists(photo_folder_name))
                //{
                //    Directory.CreateDirectory(photo_folder_name);
                // 使用StreamWriter創建並寫入文件
                //using (StreamWriter writer = new StreamWriter(filePath))
                //{
                //writer.WriteLine(DateTime.Now.ToString("HH:mm ss tt") + "\n");

                if (transform_text.Count == 2)
                {
                    //writer.WriteLine("脈衝羣頻率" + transform_text[0] + "\n");
                    //writer.WriteLine("內部脈衝羣速率" + "NULL" + "\n");
                    //writer.WriteLine("脈衝寬度" + transform_text[1] + "\n");
                    _mainWindow.path_shootname = DateTime.Now.ToString("HH_mm ss tt") + "_頻率" + transform_text[0] + "_震幅NULL_寬度" + transform_text[2];//txt黨路徑
                }
                if (transform_text.Count == 3)
                {
                    //writer.WriteLine("脈衝羣頻率" + transform_text[0] + "\n");
                    //writer.WriteLine("內部脈衝羣速率" + transform_text[1] + "\n");
                    //writer.WriteLine("脈衝寬度" + transform_text[2] + "\n");
                    _mainWindow.path_shootname = DateTime.Now.ToString("HH_mm ss tt") + "_頻率" + transform_text[0] + "_5" + transform_text[1] + "_寬度" + transform_text[2];//txt黨路徑
                }

                //writer.WriteLine("正極位置:" + "\t");
                if (red_numbewr_list.Count > 0)
                {
                    for (int i = 0; i <= red_numbewr_list.Count; i++)
                    {
                        //writer.WriteLine(red_numbewr_list[i] + "\t");

                    }
                }
                //writer.WriteLine("\n");


                //writer.WriteLine("負極位置:" + "\t");
                if (black_numbewr_list.Count > 0)
                {
                    for (int i = 0; i <= black_numbewr_list.Count; i++)
                    {
                        //writer.WriteLine(black_numbewr_list[i] + "\t");

                    }
                }

                //}
            }


            //}

            else
            {
                Show_word_1.Text = "";
                Show_word_2.Text = "";
                Show_word_3.Text = "";
                //MessageBox.Show("無判別到文字");
                //Open_ccd();
            }
            //}
            //catch
            //{
            //    Show_word_1.Text = "";
            //    Show_word_2.Text = "";
            //    Show_word_3.Text = "";
            //    MessageBox.Show("無判別到文字");
            //    Open_ccd();
            //}
        }
        int[] buttonClickCounts = new int[16];
        private void extractedContents_button_Click(object sender, RoutedEventArgs e)
        {
            // 取得按下的按鈕
            Button clickedButton = sender as Button;

            // 取得按鈕的索引（這裡假設按鈕的名稱格式為 "button1", "button2", ...）
            int buttonIndex = int.Parse(clickedButton.Name.Replace("extractedContents_photo_", "").Replace("_button", "")) - 1;

            // 更新按下次數
            buttonClickCounts[buttonIndex]++;

            // 根據按下次數改變按鈕顏色
            if (buttonClickCounts[buttonIndex] % 3 == 1)
            {
                clickedButton.Background = System.Windows.Media.Brushes.Red; // 第一次按下變紅色--> +

                _mainWindow.Shoot_electric[buttonIndex] = 1;

            }
            else if (buttonClickCounts[buttonIndex] % 3 == 2)
            {
                clickedButton.Background = System.Windows.Media.Brushes.Black; // 第二次按下變黑色--> -
                _mainWindow.Shoot_electric[buttonIndex] = 2;


            }
            else
            {
                clickedButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(211, 211, 211)); // 使用淺灰色
                _mainWindow.Shoot_electric[buttonIndex] = 0;

            }
            change_num();
        }

        public void change_num()//歸零並重製
        {
            Electrode_positive_show = "";
            Electrode_negative_show = "";

            for (int i = 0; i < 16; i++)
            {
                if (_mainWindow.Shoot_electric[i] == 1)
                {
                    Electrode_positive_show += i + 1 + ",";
                }
                if (_mainWindow.Shoot_electric[i] == 2)
                {
                    Electrode_negative_show += i + 1 + ",";
                }
            }

            Electrode_positive.Text = Electrode_positive_show;
            Electrode_negative.Text = Electrode_negative_show;

            ChangeButtonColors(_mainWindow.Shoot_electric);


        }

        private void update_Click(object sender, RoutedEventArgs e)
        {

            try
            {
                MainWindow.Pulse_frequency = Show_word_1.Text;
                MainWindow.Pulse_amplitude = Show_word_3.Text;

                MainWindow.Pulse_rate = Show_word_2.Text;

                if (MainWindow.Pulse_frequency == "" && MainWindow.Pulse_amplitude == "")
                {
                    MessageBox.Show("無調控參數");
                }
                else
                {
                    //改 deviceSteamingUC.currently_parameter_file.Text
                    DateTime today = DateTime.Today;
                    string Date = today.ToString("MM-dd");
                    string directoryPath_2 = MainWindow.GlobalDirPath;



                    string creat_Path = "";

                    if (Show_word_2.Text == "")
                    {

                        if (_mainWindow.folderComboBox_Action.Text == "")
                        {
                            creat_Path = Path.Combine(directoryPath_2, _mainWindow.folderComboBox_Action.Text.ToString(), MainWindow.Pulse_frequency + "頻率" + MainWindow.Pulse_amplitude + "震幅" + MainWindow.Pulse_rate + "內部脈衝速率" + '-' + Date);
                            _deviceSteamingUC.currently_parameter_file.Text = Path.Combine("Other", MainWindow.Pulse_frequency + "頻率" + MainWindow.Pulse_amplitude + "震幅" + '-' + Date); ;
                        }
                        else
                        {
                            creat_Path = Path.Combine(directoryPath_2, _mainWindow.folderComboBox_Action.Text.ToString(), MainWindow.Pulse_frequency + "頻率" + MainWindow.Pulse_amplitude + "震幅" + MainWindow.Pulse_rate + "內部脈衝速率" + '-' + Date);
                            _deviceSteamingUC.currently_parameter_file.Text = Path.Combine(_mainWindow.folderComboBox_Action.Text.ToString(), MainWindow.Pulse_frequency + "頻率" + MainWindow.Pulse_amplitude + "震幅" + '-' + Date);
                        }
                    }
                    else if (Show_word_2.Text != "")
                    {
                        if (_mainWindow.folderComboBox_Action.Text == "")
                        {
                            creat_Path = Path.Combine(directoryPath_2, _mainWindow.folderComboBox_Action.Text.ToString(), MainWindow.Pulse_frequency + "頻率" + MainWindow.Pulse_amplitude + "震幅" + MainWindow.Pulse_rate + "內部脈衝速率" + '-' + Date);
                            _deviceSteamingUC.currently_parameter_file.Text = Path.Combine("Other", MainWindow.Pulse_frequency + "頻率" + MainWindow.Pulse_amplitude + "震幅" + MainWindow.Pulse_rate + "內部脈衝速率" + '-' + Date); ;
                        }
                        else
                        {
                            creat_Path = Path.Combine(directoryPath_2, _mainWindow.folderComboBox_Action.Text.ToString(), MainWindow.Pulse_frequency + "頻率" + MainWindow.Pulse_amplitude + "震幅" + MainWindow.Pulse_rate + "內部脈衝速率" + '-' + Date);
                            _deviceSteamingUC.currently_parameter_file.Text = Path.Combine(_mainWindow.folderComboBox_Action.Text.ToString(), MainWindow.Pulse_frequency + "頻率" + MainWindow.Pulse_amplitude + "震幅" + MainWindow.Pulse_rate + "內部脈衝速率" + '-' + Date);
                        }
                    }

                    //string directoryPath = ConstructDirectoryPath();
                    string directoryPath = creat_Path;

                    MainWindow.GlobalDirPath = directoryPath;
                    if (!Directory.Exists(directoryPath))
                    {
                        Directory.CreateDirectory(directoryPath);
                    }

                    // 創建包含電極信息的 .txt 文件  
                    string fileName = Path.Combine(directoryPath, "電極信息.txt");
                    string content = $"正極:\t{Electrode_positive_show}\n負極:\t{Electrode_negative_show}";
                    File.WriteAllText(fileName, content);

                    // 保存 num_photo_2.Source 裡的照片
                    if (image_1.Source is BitmapImage bitmapImage)
                    {
                        Bitmap bitmap = BitmapImage2Bitmap(bitmapImage);
                        string photoPath = Path.Combine(directoryPath, "photo.png");
                        bitmap.Save(photoPath, System.Drawing.Imaging.ImageFormat.Png);
                    }


                    // 顯示成功消息
                    MessageBox.Show($"儲存成功\n{content.Replace("\n", "\n")}");
                }

            }
            catch (Exception ex)
            {
                // 顯示錯誤消息
                MessageBox.Show($"操作失敗：{ex.Message}");
            }




        }
        int j = 0;
        private string ConstructDirectoryPath()
        {
            string directoryPath = MainWindow.GlobalDirPath;
            DateTime today = DateTime.Today;
            string Date = today.ToString("MM-dd");

            j++;
            if (j > 1)
            {
                directoryPath = Directory.GetParent(Directory.GetParent(directoryPath).FullName).FullName;
                MainWindow.GlobalDirPath = directoryPath;

            }
            if (Show_word_2.Text == "")
            {

                return directoryPath + '\\' + _mainWindow.folderComboBox_Action.Text + '\\' + Show_word_1.Text + "頻率" + Show_word_3.Text + "寬度" + '-' + Date;
            }
            else if (Show_word_2.Text != "")
            {
                return directoryPath + '\\' + _mainWindow.folderComboBox_Action.Text + '\\' + Show_word_1.Text + "頻率" + Show_word_2.Text + "強度" + Show_word_3.Text + "寬度" + '-' + Date;
            }

            throw new InvalidOperationException("transform_text 的數量不符合預期");
        }
        private bool stopRequested = false;

        private void inherit_button_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < 16; i++)
            {
                if (_mainWindow.Shoot_electric[i] == 1)
                {
                    Electrode_positive_show += i + 1 + ",";
                }
                if (_mainWindow.Shoot_electric[i] == 2)
                {
                    Electrode_negative_show += i + 1 + ",";
                }
            }
            //change_num();
        }

        private void StopRecognition_Click(object sender, RoutedEventArgs e)
        {
            if (Ccd_chose_ComboBox.Text != "")
            {
                if (videoSource != null && videoSource.IsRunning)
                {
                    videoSource.SignalToStop();
                    videoSource.WaitForStop();
                    videoSource.NewFrame -= new NewFrameEventHandler(video_NewFrame);
                }
            }
            else
            {
                MessageBox.Show("無選擇攝影機");
            }
        }

        private void Open_ccd()
        {
            //if (videoSource != null && videoSource.IsRunning)
            //{
            //    videoSource.Stop();
            //}

            videoSource = new VideoCaptureDevice(videoDevices[Ccd_chose_ComboBox.SelectedIndex].MonikerString);
            videoSource.NewFrame += new NewFrameEventHandler(video_NewFrame);
            videoSource.Start();

        }

        private void Show_photo()
        {
            try
            {
                extractedContents_photo_6.Source = Bitmap2BitmapImage(extractedContents_1[0]);
            }
            catch { }

            try
            {
                extractedContents_photo_1.Source = Bitmap2BitmapImage(extractedContents_2[0]);
                extractedContents_photo_7.Source = Bitmap2BitmapImage(extractedContents_2[2]);
                extractedContents_photo_12.Source = Bitmap2BitmapImage(extractedContents_2[1]);
            }
            catch { }

            try
            {
                extractedContents_photo_2.Source = Bitmap2BitmapImage(extractedContents_3[0]);
                extractedContents_photo_8.Source = Bitmap2BitmapImage(extractedContents_3[2]);
                extractedContents_photo_13.Source = Bitmap2BitmapImage(extractedContents_3[1]);
            }
            catch { }

            try
            {
                extractedContents_photo_3.Source = Bitmap2BitmapImage(extractedContents_4[0]);
                extractedContents_photo_9.Source = Bitmap2BitmapImage(extractedContents_4[2]);
                extractedContents_photo_14.Source = Bitmap2BitmapImage(extractedContents_4[1]);
            }
            catch { }

            try
            {
                extractedContents_photo_4.Source = Bitmap2BitmapImage(extractedContents_5[0]);
                extractedContents_photo_10.Source = Bitmap2BitmapImage(extractedContents_5[2]);
                extractedContents_photo_15.Source = Bitmap2BitmapImage(extractedContents_5[1]);
            }
            catch { }

            try
            {
                extractedContents_photo_5.Source = Bitmap2BitmapImage(extractedContents_6[0]);
                extractedContents_photo_11.Source = Bitmap2BitmapImage(extractedContents_6[2]);
                extractedContents_photo_16.Source = Bitmap2BitmapImage(extractedContents_6[1]);
            }
            catch { }
        }

        private void photo_transfom_red_black()
        {

            Show_photo();
            try
            {
                extractedContents_array[5] = extractedContents_1[0];

                extractedContents_array[0] = extractedContents_2[0];
                extractedContents_array[6] = extractedContents_2[2];
                extractedContents_array[11] = extractedContents_2[1];

                extractedContents_array[1] = extractedContents_3[0];
                extractedContents_array[7] = extractedContents_3[2];
                extractedContents_array[12] = extractedContents_3[1];

                extractedContents_array[2] = extractedContents_4[0];
                extractedContents_array[8] = extractedContents_4[2];
                extractedContents_array[13] = extractedContents_4[1];

                extractedContents_array[3] = extractedContents_5[0];
                extractedContents_array[9] = extractedContents_5[2];
                extractedContents_array[14] = extractedContents_5[1];

                extractedContents_array[4] = extractedContents_6[0];
                extractedContents_array[10] = extractedContents_6[2];
                extractedContents_array[15] = extractedContents_6[1];
            }
            catch { }

            for (int j = 0; j < 16; j++)
            {
                try
                {
                    Bitmap judge_bitmap = extractedContents_array[j];
                    if (judge_bitmap == null)
                    {
                        break;
                    }
                    if (IsImageMostlyRed(extractedContents_array[j], 0.5))
                    {
                        //red_numbewr_list.Add(j);
                        _mainWindow.Shoot_electric[j] = 1;


                        break;
                    }

                    if (IsImageMostlyBlack(extractedContents_array[j], 0.7))
                    {
                        //black_numbewr_list.Add(j);
                        _mainWindow.Shoot_electric[j] = 2;

                        break;
                    }

                    //int randnum= random.Next(1,3);
                    //for(int i = 0; i < randnum; i++) 
                    //{
                    //    int randomInt = random.Next(0, 15);
                    //    int rand_extracted = random.Next(1, 2);
                    //    _mainWindow.Shoot_electric[randomInt] = rand_extracted;
                    //}

                    change_num();
                }
                catch { }
            }


        }
        private void ChangeButtonColors(int[] buttonIndices)
        {
            try
            {
                // 將字串 "1,2,3" 拆解為整數陣列
                int[] indices = buttonIndices;

                // 遍歷索引，改變對應按鈕的顏色
                for (int index = 0; index < 16; index++)
                {
                    // 根據索引生成按鈕名稱
                    string buttonName = $"extractedContents_photo_{index + 1}_button";

                    // 使用 FindName 查找按鈕
                    Button button = this.FindName(buttonName) as Button;
                    // 確認按鈕存在後改變顏色
                    if (button != null)
                    {
                        switch (indices[index])
                        {
                            case 1:
                                button.Background = System.Windows.Media.Brushes.Red; // 設置紅色
                                break;
                            case 2:
                                button.Background = System.Windows.Media.Brushes.Black; // 設置黑色
                                break;
                            case 3:
                                button.Background = System.Windows.Media.Brushes.Gray; // 設置灰色
                                break;
                            default:
                                Console.WriteLine("未知的顏色選項");
                                break;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"找不到按鈕: {buttonName}");
                    }
                }
            }
            catch { Console.WriteLine("indices=NULL"); }

        }
        public static bool IsImageMostlyRed(Bitmap bitmap, double threshold)
        {
            int redPixelCount = 0;
            int totalPixelCount = bitmap.Width * bitmap.Height;

            System.Drawing.Rectangle rect = new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData bmpData = bitmap.LockBits(rect, ImageLockMode.ReadOnly, bitmap.PixelFormat);

            int bytesPerPixel = System.Drawing.Image.GetPixelFormatSize(bitmap.PixelFormat) / 8;
            int byteCount = bmpData.Stride * bitmap.Height;
            byte[] pixels = new byte[byteCount];
            IntPtr ptrFirstPixel = bmpData.Scan0;
            System.Runtime.InteropServices.Marshal.Copy(ptrFirstPixel, pixels, 0, pixels.Length);
            bitmap.UnlockBits(bmpData);

            for (int y = 0; y < bitmap.Height; y++)
            {
                int currentLine = y * bmpData.Stride;
                for (int x = 0; x < bitmap.Width; x++)
                {
                    int xIndex = x * bytesPerPixel;
                    byte blue = pixels[currentLine + xIndex];
                    byte green = pixels[currentLine + xIndex + 1];
                    byte red = pixels[currentLine + xIndex + 2];

                    if (IsRed(red, green, blue))
                    {
                        redPixelCount++;
                    }
                }
            }

            return (double)redPixelCount / totalPixelCount >= threshold;
        }

        public static bool IsImageMostlyBlack(Bitmap bitmap, double threshold)
        {
            int blackPixelCount = 0;
            int totalPixelCount = bitmap.Width * bitmap.Height;

            System.Drawing.Rectangle rect = new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData bmpData = bitmap.LockBits(rect, ImageLockMode.ReadOnly, bitmap.PixelFormat);

            int bytesPerPixel = System.Drawing.Image.GetPixelFormatSize(bitmap.PixelFormat) / 8;
            int byteCount = bmpData.Stride * bitmap.Height;
            byte[] pixels = new byte[byteCount];
            IntPtr ptrFirstPixel = bmpData.Scan0;
            System.Runtime.InteropServices.Marshal.Copy(ptrFirstPixel, pixels, 0, pixels.Length);
            bitmap.UnlockBits(bmpData);

            for (int y = 0; y < bitmap.Height; y++)
            {
                int currentLine = y * bmpData.Stride;
                for (int x = 0; x < bitmap.Width; x++)
                {
                    int xIndex = x * bytesPerPixel;
                    byte blue = pixels[currentLine + xIndex];
                    byte green = pixels[currentLine + xIndex + 1];
                    byte red = pixels[currentLine + xIndex + 2];

                    if (IsBlack(red, green, blue))
                    {
                        blackPixelCount++;
                    }
                }
            }

            return (double)blackPixelCount / totalPixelCount >= threshold;
        }

        private static bool IsBlack(byte red, byte green, byte blue)
        {
            return red < 100 && green < 100 && blue < 100;
        }

        private static bool IsRed(byte red, byte green, byte blue)
        {
            return red > 130 && green < 100 && blue < 100;
        }

        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);
    }
}