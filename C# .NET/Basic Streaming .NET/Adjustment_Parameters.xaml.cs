using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Tesseract; // 確保通過 NuGet 安裝了 Tesseract 包
using Microsoft.Win32; // 用於 OpenFileDialog
using AForge.Video;
using AForge.Video.DirectShow;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading; // 用於 DispatcherTimer
using System.Drawing.Imaging;
using AForge.Imaging;
using AForge.Imaging.Filters;
using System.Text.RegularExpressions;


namespace Basic_Streaming_NET
{
    public partial class Adjustment_Parameters : UserControl
    {
        private FilterInfoCollection videoDevices;
        private VideoCaptureDevice videoSource;
        private Bitmap[] grayImage_array = new Bitmap[3];
        private Bitmap[] electrode_Image_array = new Bitmap[6];
        private string[] electrode_text = new string[16];

        private Bitmap currentFrame;
        private Bitmap num_bitmap_nomal;
        private Bitmap photo_num;
        private Bitmap photo_img;
        private Bitmap electrode_photo_to_text;
        private int rectX, rectY, rectWidth, rectHeight;
        private int rectX_num, rectY_num, rectWidth_num, rectHeight_num;
        private int rectX_electrode, rectY_electrode, rectWidth_electrode, rectHeight_electrode;
        int partHeight_num, partHeight_electrode;


        private string imgPath;
        private DispatcherTimer memoryReleaseTimer;

        public Adjustment_Parameters()
        {
            InitializeComponent();
            GetVideoDevices();
            SetupMemoryReleaseTimer();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 提示使用者確認關閉
            var result = MessageBox.Show("確定要關閉視窗嗎？", "確認", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.No)
            {
                e.Cancel = true; // 取消關閉
            }
            else
            {
                // 停止視頻源
                if (videoSource != null && videoSource.IsRunning)
                {
                    videoSource.SignalToStop();
                }

                // 其他清理操作
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
                    string filePath = "C:\\Users\\TCUMI\\Downloads\\iMAGE_TEXT (1) - 複製\\iMAGE_TEXT\\iMAGE_TEXT\\iMAGE_TEXT\\裁切圖片\\" + filename; // 在這裏指定保存文件的路徑


                    bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);



                    MessageBox.Show("儲存完畢 路徑為" + filePath);
                }
            }
            else
            {
                MessageBox.Show("num_photo 控件中沒有加載任何圖片。", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void electrode_photo1_Click(object sender, RoutedEventArgs e)
        {

            // 獲取 Image 控件中的 BitmapImage
            BitmapImage bitmapImage = electrode_photo.Source as BitmapImage;

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
                    string filePath = "C:\\Users\\TCUMI\\Downloads\\iMAGE_TEXT (1) - 複製\\iMAGE_TEXT\\iMAGE_TEXT\\iMAGE_TEXT\\裁切圖片\\" + filename + ".png"; // 在這裏指定保存文件的路徑
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
            Open_ccd();

        }

        private void Photo_TAKE_Click(object sender, RoutedEventArgs e)
        {
            Photo_Retrieve();
            Photo_to_text();
            electrode_to_text();




        }
        private void Photo_Retrieve()
        {

            Dispatcher.Invoke(() =>
            {
                if (videoSource != null && videoSource.IsRunning)
                {
                    videoSource.SignalToStop();
                    videoSource.WaitForStop();
                    videoSource.NewFrame -= new NewFrameEventHandler(video_NewFrame);
                }

                Bitmap croppedBitmap = null;
                Bitmap croppedBitmap2 = null;
                Bitmap croppedBitmap3 = null;


                //C: \Users\TCUMI\Downloads\iMAGE_TEXT(1)\iMAGE_TEXT\iMAGE_TEXT\iMAGE_TEXT\裁切圖片\
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

                    //
                    //LoadImage(croppedBitmap); // 使用自定義方法加載和顯示裁剪後的圖片
                    // 保存裁剪後的圖像                   

                    // 更新UI顯示裁剪後的數字區域圖像
                    BitmapImage numbitmap = Bitmap2BitmapImage(croppedBitmap2);
                    numbitmap.DecodePixelWidth = Convert.ToInt32(num_photo.Width); // 照這比例輸出畫面
                    num_photo_2.Source = numbitmap;

                    // 更新UI顯示電極裁剪後的圖像
                    BitmapImage electrodebitmap = Bitmap2BitmapImage(croppedBitmap3);
                    electrodebitmap.DecodePixelWidth = Convert.ToInt32(electrode_photo.Width); // 照這比例輸出畫面
                    electrode_photo.Source = electrodebitmap;



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


            //string[] transform_text = new string[3];
            List<string> transform_text = new List<string>();
            // 圖像處理和識別代碼...
            Bitmap grayImage = photo_num;
            //Bitmap[] grayImage_array = new Bitmap[3];
            //int partHeight = photo_num.Height / 3;
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


                // 應用銳化濾鏡
                Sharpen sharpenFilter = new Sharpen();
                Image_process[i] = sharpenFilter.Apply(Image_process[i]);

                // 創建瑞化二值化過濾器，閾值可以根據需要調整
                Threshold thresholdFilter = new Threshold(128);
                Image_process[i] = thresholdFilter.Apply(Image_process[i]);

                //// 自適應二值化
                //AdaptiveSmoothing adaptiveSmoothingFilter = new AdaptiveSmoothing();
                //Image_process[i] = adaptiveSmoothingFilter.Apply(Image_process[i]);
                // 應用中值濾波以減少噪聲，這裏設置的鄰域大小為5x5
                Median medianFilter = new Median(3);
                Image_process[i] = medianFilter.Apply(Image_process[i]);

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
                //List<string> filteredWords1 = new List<string>();

                foreach (string word in filteredWords)
                {
                    // 檢查字符串是否同時包含"Hz"和"us"
                    if (word.Contains("Hz") || word.Contains("us"))
                    {
                        // 如果字符串同時包含"Hz"和"us"，則添加到filteredWords1
                        transform_text.Add(word);
                    }
                }
            }
            try
            {
                if (transform_text.Count == 2)
                {
                    Show_word_1.Text = transform_text[0];
                    Show_word_2.Text = "";
                    Show_word_3.Text = transform_text[1];

                }
                else if (transform_text.Count == 3)
                {
                    Show_word_1.Text = transform_text[0];
                    Show_word_2.Text = transform_text[1];
                    Show_word_3.Text = transform_text[2];
                }
                else
                {
                    Show_word_1.Text = "";
                    Show_word_2.Text = "";
                    Show_word_3.Text = "";
                    MessageBox.Show("無判別到文字");
                    Open_ccd();
                }
            }
            catch
            {
                Show_word_1.Text = "";
                Show_word_2.Text = "";
                Show_word_3.Text = "";
                MessageBox.Show("無判別到文字");
                Open_ccd();


            }


            this.grayImage_array = Image_process;









        }

        private void electrode_to_text()
        {

            Bitmap original = electrode_photo_to_text;
            Bitmap grayImage = original;

            Bitmap[] grayImage_array = new Bitmap[6];

            List<string> transform_text = new List<string>();


            //int partHeight = original.Height / 6;
            // 圖像處理和識別代碼...

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
            int count = 0;
            for (int i = 0; i < 6; i++)
            {
                // 應用灰度濾鏡
                Grayscale grayscaleFilter = new Grayscale(0.2125, 0.7154, 0.0721);
                Image_process[i] = grayscaleFilter.Apply(Image_process[i]);


                //// 應用中值濾波以減少噪聲，這裏設置的鄰域大小為5x5
                //Median medianFilter = new Median(5);
                //Image_process[i] = medianFilter.Apply(Image_process[i]);

                //// 應用高斯濾波器去噪
                //GaussianBlur gaussianBlurFilter = new GaussianBlur(4, 7); // 4 為標準差，7 為核大小
                //Image_process[i] = gaussianBlurFilter.Apply(Image_process[i]);

                //// 應用銳化濾鏡
                ////Sharpen sharpenFilter = new Sharpen();
                //// Image_process[i] = sharpenFilter.Apply(Image_process[i]);


                // 應用中值濾波以減少噪聲，這裏設置的鄰域大小為5x5
                Median medianFilter = new Median(3);
                Image_process[i] = medianFilter.Apply(Image_process[i]);

                //自適應2質化
                BradleyLocalThresholding bradleyLocalThresholdingFilter = new BradleyLocalThresholding();
                bradleyLocalThresholdingFilter.ApplyInPlace(Image_process[i]);

                //// 二值化過濾器，閾值可以根據需要調整
                //Threshold thresholdFilter = new Threshold(150);
                // Image_process[i] = thresholdFilter.Apply(Image_process[i]);




                string turnword = ImageToText(Image_process[i], "200");  // 假設 photo_num 是當前處理的圖像
                string[] outputnum = turnword.Split('\n');
                Regex regex = new Regex("[^0123456789+-]+");
                List<string> filteredWords = new List<string>();
                foreach (string word in outputnum)
                {
                    // 替換每個字符串中不包含指定字符的部分
                    string filteredWord = regex.Replace(word, "");
                    // 如果替換後的字符串不爲空，則添加到列表中
                    if (!string.IsNullOrEmpty(filteredWord))
                    {
                        filteredWords.Add(filteredWord);
                        electrode_text[count] = filteredWord;
                        count++;
                    }
                }


            }




        }
        private void CropImage(string imgPath, string savefilePath, int locat_x, int locate_y, int width, int height)//裁切圖片
        {
            BitmapImage originalImage = new BitmapImage(new Uri(imgPath, UriKind.Absolute));

            // 定義裁剪區域，例如從圖像的locat_width,locate_hidth開始裁剪一個widthxheight的區域

            Int32Rect cropArea = new Int32Rect(locat_x, locate_y, width, height);
            CroppedBitmap croppedBitmap = new CroppedBitmap(originalImage, cropArea);

            // 將裁剪後的圖像設置爲Image控制向的Source
            SaveCroppedBitmapToFile(croppedBitmap, savefilePath);
        }

        private void SaveCroppedBitmapToFile(CroppedBitmap croppedBitmap, string filePath)//存圖CroppedBitmap
        {

            using (FileStream fileStream = new FileStream(filePath, FileMode.Create))//無法存圖報錯
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
                // 設置白名單為數字0-9
                engine.DefaultPageSegMode = PageSegMode.Auto;
                engine.SetVariable("tessedit_char_whitelist", "0123456789Hzus");
                engine.SetVariable("user_defined_dpi", DPI); // 設置用戶定義的 DPI

                using (var pix = BitmapToPix(image))
                {
                    using (var page = engine.Process(pix))
                    {
                        return page.GetText();
                    }
                }
            }
        }


        private Pix BitmapToPix(Bitmap bitmap)
        {
            using (var stream = new MemoryStream())
            {
                bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                stream.Position = 0;
                return Pix.LoadFromMemory(stream.ToArray());
            }
        }


        private void video_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            //currentFrame = (Bitmap)eventArgs.Frame.Clone();
            // 釋放之前的幀
            currentFrame?.Dispose();

            // 更新currentFrame爲最新的幀
            currentFrame = (Bitmap)eventArgs.Frame.Clone();



            // 克隆當前幀，避免修改原始幀
            using (var currentFrame = (Bitmap)eventArgs.Frame.Clone())
            {
                // 獲取攝像頭圖像的尺寸
                int videoWidth = currentFrame.Width;
                int videoHeight = currentFrame.Height;
                int X2 = 0, Y2 = 0;

                rectHeight = videoHeight; // 或者其他基於你需求的計算
                rectWidth = (int)(rectHeight * 0.74); // 比例爲0.74
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

                // 使用Graphics繪製矩形
                using (var graphics = Graphics.FromImage(currentFrame))
                {
                    // 設置矩形的顏色和寬度
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

                // 更新UI顯示
                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        // 將currentFrame轉換爲WPF可用的ImageSource並顯示
                        var bitmapSource = ConvertBitmapToBitmapSource(currentFrame);
                        image_1.Source = bitmapSource;
                        image_1.Height = 450;
                        image_1.Width = 800;
                        Boder_main.Width = image_1.Width;
                        Boder_main.Height = image_1.Height;
                    }
                    catch (Exception ex)
                    {
                        // Log the exception details
                        Console.WriteLine($"Exception: {ex.Message}");
                        Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                    }


                });
                // 釋放克隆的幀，避免內存泄漏
                //frame.Dispose();
            }
        }



        private BitmapSource ConvertBitmapToBitmapSource(Bitmap bitmap)//釋放
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
                // 釋放GDI對象
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
                //MessageBox.Show($"您選擇了: {selectedContent}", "選項變更", MessageBoxButton.OK, MessageBoxImage.Information);

                switch (selectedContent)
                {
                    case "Option 1":
                        // 執行 Option 1 的操作
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
                        // 執行 Option 2 的操作
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
                        // 執行 Option 3 的操作
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
            // 獲取 Image 控件中的 BitmapImage
            BitmapImage bitmapImage = num_photo_2.Source as BitmapImage;

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
                    string filePath = "C:\\Users\\TCUMI\\Downloads\\iMAGE_TEXT (1) - 複製\\iMAGE_TEXT\\iMAGE_TEXT\\iMAGE_TEXT\\裁切圖片\\" + filename + ".png"; // 在這裏指定保存文件的路徑
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
                // 停止視頻源
                videoSource.SignalToStop();
            }
            // 關閉當前視窗
            Exit_Click();
        }
        private void Exit_Click()
        {
            Window parentWindow = Window.GetWindow(this);
            if (parentWindow != null)
            {
                parentWindow.Close();
            }
        }
        private void electrode_save_Copy1_Click(object sender, RoutedEventArgs e)
        {
            // 獲取 Image 控件中的 BitmapImage
            BitmapImage bitmapImage = electrode_photo_Copy.Source as BitmapImage;

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
                    string filePath = "C:\\Users\\TCUMI\\Downloads\\iMAGE_TEXT (1) - 複製\\iMAGE_TEXT\\iMAGE_TEXT\\iMAGE_TEXT\\裁切圖片\\" + filename + ".png"; // 在這裏指定保存文件的路徑
                    bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);

                    //string filePath2 = "C:\\Users\\TCUMI\\Desktop\\OCR\\image\\待處理\\" + filename; // 在這裏指定保存文件的路徑
                    //bitmap.Save(filePath2, System.Drawing.Imaging.ImageFormat.Png);

                    MessageBox.Show("儲存完畢 路徑為" + filePath);
                }
            }
            else
            {
                MessageBox.Show("num_photo 控件中沒有加載任何圖片。", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void CameraControl_Click(object sender, RoutedEventArgs e)
        {

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

        private void electrode_ComboBox_Copy1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = electrode_ComboBox_Copy1.SelectedItem as ComboBoxItem;
            if (selectedItem != null)
            {
                string selectedContent = selectedItem.Content.ToString();
                //MessageBox.Show($"您選擇了: {selectedContent}", "選項變更", MessageBoxButton.OK, MessageBoxImage.Information);

                switch (selectedContent)
                {
                    case "Option 1":
                        // 執行 Option 1 的操作
                        if (this.electrode_Image_array[0] != null)
                        {
                            electrode_photo_Copy.Source = Bitmap2BitmapImage(this.electrode_Image_array[0]);

                        }
                        else
                        {
                            MessageBox.Show("此選項無圖片");
                        }
                        break;
                    case "Option 2":
                        // 執行 Option 1 的操作
                        if (this.electrode_Image_array[1] != null)
                        {
                            electrode_photo_Copy.Source = Bitmap2BitmapImage(this.electrode_Image_array[1]);

                        }
                        else
                        {
                            MessageBox.Show("此選項無圖片");
                        }
                        break;
                    case "Option 3":
                        // 執行 Option 1 的操作
                        if (this.electrode_Image_array[2] != null)
                        {
                            electrode_photo_Copy.Source = Bitmap2BitmapImage(this.electrode_Image_array[2]);

                        }
                        else
                        {
                            MessageBox.Show("此選項無圖片");
                        }
                        break;
                    case "Option 4":
                        // 執行 Option 1 的操作
                        if (this.electrode_Image_array[3] != null)
                        {
                            electrode_photo_Copy.Source = Bitmap2BitmapImage(this.electrode_Image_array[3]);

                        }
                        else
                        {
                            MessageBox.Show("此選項無圖片");
                        }
                        break;
                    case "Option 5":
                        // 執行 Option 1 的操作
                        if (this.electrode_Image_array[4] != null)
                        {
                            electrode_photo_Copy.Source = Bitmap2BitmapImage(this.electrode_Image_array[4]);

                        }
                        else
                        {
                            MessageBox.Show("此選項無圖片");
                        }
                        break;
                    case "Option 6":
                        // 執行 Option 1 的操作
                        if (this.electrode_Image_array[5] != null)
                        {
                            electrode_photo_Copy.Source = Bitmap2BitmapImage(this.electrode_Image_array[5]);

                        }
                        else
                        {
                            MessageBox.Show("此選項無圖片");
                        }
                        break;

                }
            }

        }

        private void SetupMemoryReleaseTimer()
        {
            // 初始化定時器
            memoryReleaseTimer = new DispatcherTimer();
            memoryReleaseTimer.Interval = TimeSpan.FromSeconds(5); // 每5秒觸發一次
            memoryReleaseTimer.Tick += MemoryReleaseTimer_Tick;
            memoryReleaseTimer.Start();
        }

        private void MemoryReleaseTimer_Tick(object sender, EventArgs e)
        {
            // 強制執行垃圾回收以釋放未使用的內存
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        private void LoadImage(Bitmap cot_image)//圖片載入
        {


            Console.WriteLine("開始加載圖片: " + imgPath); // 或使用 Debug.WriteLine() 如果在 WPF 應用中
            BitmapImage bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap = Bitmap2BitmapImage(cot_image);
            //bitmap.UriSource = new Uri(imgPath, UriKind.Absolute);
            bitmap.DecodePixelWidth = 600; //照這比例輸出畫面
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();

            this.imgPath = Convert.ToString(bitmap);
            image_1.Source = bitmap;
            Console.WriteLine("圖片加載完成");


        }




        private BitmapSource ConvertBitmap(Bitmap source)//轉換葉面並輸出在image上
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
                // 刪除用過的GDI對象
                DeleteObject(hBitmap);
            }

            return result;
        }
        private bool stopRequested = false;

        private void StopRecognition_Click(object sender, RoutedEventArgs e)
        {

            if (videoSource != null && videoSource.IsRunning)
            {
                videoSource.SignalToStop();
                videoSource.WaitForStop();
                videoSource.NewFrame -= new NewFrameEventHandler(video_NewFrame);
            }

        }
        private void Open_ccd()
        {
            if (videoSource != null && videoSource.IsRunning)
            {
                videoSource.Stop();
            }

            videoSource = new VideoCaptureDevice(videoDevices[Ccd_chose_ComboBox.SelectedIndex].MonikerString);
            videoSource.NewFrame += new NewFrameEventHandler(video_NewFrame);
            videoSource.Start();


        }


        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);
    }
}
