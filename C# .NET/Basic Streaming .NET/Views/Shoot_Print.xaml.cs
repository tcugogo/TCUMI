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
    /// Shoot_Print.xaml 的互動邏輯
    /// </summary>
    public partial class Shoot_Print : Window
    {
        private MainWindow mainWindow;
        public string[] ReceivedString { get; set; }

        public Shoot_Print(MainWindow mainWindow_Shoot_ele,string parameter)
        {
            InitializeComponent();
            mainWindow = mainWindow_Shoot_ele;

            Shoot_ele_reset(mainWindow.Shoot_electric);
        }
        private void exit_Click(object sender, RoutedEventArgs e)
        {

            this.Close();
        }
        public void Shoot_ele_reset(int[] Shoot_electric)
        {

            Shoot_ele_color_change(Shoot_electric[0], extractedContents_photo_1_button);
            Shoot_ele_color_change(Shoot_electric[1], extractedContents_photo_2_button);
            Shoot_ele_color_change(Shoot_electric[2], extractedContents_photo_3_button);
            Shoot_ele_color_change(Shoot_electric[3], extractedContents_photo_4_button);
            Shoot_ele_color_change(Shoot_electric[4], extractedContents_photo_5_button);
            Shoot_ele_color_change(Shoot_electric[5], extractedContents_photo_6_button);
            Shoot_ele_color_change(Shoot_electric[6], extractedContents_photo_7_button);
            Shoot_ele_color_change(Shoot_electric[7], extractedContents_photo_8_button);
            Shoot_ele_color_change(Shoot_electric[8], extractedContents_photo_9_button);
            Shoot_ele_color_change(Shoot_electric[9], extractedContents_photo_10_button);
            Shoot_ele_color_change(Shoot_electric[10], extractedContents_photo_11_button);
            Shoot_ele_color_change(Shoot_electric[11], extractedContents_photo_12_button);
            Shoot_ele_color_change(Shoot_electric[12], extractedContents_photo_13_button);
            Shoot_ele_color_change(Shoot_electric[13], extractedContents_photo_14_button);
            Shoot_ele_color_change(Shoot_electric[14], extractedContents_photo_15_button);
            Shoot_ele_color_change(Shoot_electric[15], extractedContents_photo_1_button);


        }


        public void Shoot_ele_color_change(int Shoot_num, Button button)
        {
            if (Shoot_num != 1 || Shoot_num != 2)
            {
                button.Background = System.Windows.Media.Brushes.Gray;

            }
            if (Shoot_num == 1)
            {
                button.Background = System.Windows.Media.Brushes.Red;
            }
            if (Shoot_num == 2)
            {
                button.Background = System.Windows.Media.Brushes.Black;
            }
        }

        private void Shoot_correct_Click(object sender, RoutedEventArgs e)
        {

        }



    }
}
