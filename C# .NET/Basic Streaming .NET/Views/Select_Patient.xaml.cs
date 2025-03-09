using Basic_Streaming.NET;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Basic_Streaming_NET.Views
{
    /// <summary>
    /// Select_Patient.xaml 的互動邏輯
    /// </summary>

    public partial class Select_Patient : UserControl
    {
        private MainWindow _mainWindow;

        public Select_Patient(MainWindow mainWindowPanel)
        {
            _mainWindow = mainWindowPanel;

            InitializeComponent();
            LoadFolderNames();
            this.Loaded += Select_Patient_Loaded; // 註冊 Loaded 事件處理程序
        }

        private void Select_Patient_Loaded(object sender, RoutedEventArgs e)
        {
            double adjustedX_1 =0;

            // 獲取 ComboBox 的父 Grid
            var parentGrid_folderComboBox = folderComboBox_Name.Parent as Grid;
            if (parentGrid_folderComboBox != null)
            {
                // 計算目標位置
                double targetX = parentGrid_folderComboBox.ActualWidth * -0.2;
                double targetY = parentGrid_folderComboBox.ActualHeight * -0.03;

                // 由於 ComboBox 設置了寬度和高度，需要調整 Margin 以使左上角位於計算的位置
                double adjustedX = targetX - folderComboBox_Name.Width / 2;
                double adjustedY = targetY - folderComboBox_Name.Height / 2;
                adjustedX_1 = adjustedX;
                // 設置 ComboBox 的 Margin
                folderComboBox_Name.Margin = new Thickness(adjustedX, adjustedY, 0, 0);
            }

            var parentGrid_Lable_Select = Lable_Select.Parent as Grid;
            if (parentGrid_Lable_Select != null)
            {
                // 計算目標位置
                double targetX = parentGrid_Lable_Select.ActualWidth * -0.26;
                double targetY = parentGrid_Lable_Select.ActualHeight * -0.23;
                // 設置 Label 的 Margin
                Lable_Select.Margin = new Thickness(targetX, targetY, 0, 0);
            }


            var parentGrid_Add_Patuent = Add_Patuent.Parent as Grid;
            if (parentGrid_Add_Patuent != null)
            {
                double targetX = parentGrid_Add_Patuent.ActualWidth * 0.1;
                double targetY = parentGrid_Add_Patuent.ActualHeight * 0.3;

                // 由於按鈕設置了寬度和高度，需要調整Margin以使按鈕的左上角位於計算的位置
                double adjustedX = targetX - Add_Patuent.Width / 2;
                double adjustedY = targetY - Add_Patuent.Height / 2;

                Add_Patuent.Margin = new Thickness(adjustedX, adjustedY, 0, 0);
            }

            //var parentGrid_UserDate_Record = UserDate_Record.Parent as Grid;
            //if (parentGrid_UserDate_Record != null)
            //{
            //    double targetX = parentGrid_UserDate_Record.ActualWidth * 0.1;
            //    double targetY = parentGrid_UserDate_Record.ActualHeight * 0;

            //    // 由於按鈕設置了寬度和高度，需要調整Margin以使按鈕的左上角位於計算的位置
            //    double adjustedX = targetX - UserDate_Record.Width / 2;
            //    double adjustedY = targetY - UserDate_Record.Height / 2;

            //    UserDate_Record.Margin = new Thickness(adjustedX, adjustedY, 0, 0);
            //}

            var parentGrid_CheckingButton = CheckingButton.Parent as Grid;
            if (parentGrid_CheckingButton != null)
            {
                double targetX = parentGrid_CheckingButton.ActualWidth *  0.1;
                double targetY = parentGrid_CheckingButton.ActualHeight * 0;

                // 由於按鈕設置了寬度和高度，需要調整Margin以使按鈕的左上角位於計算的位置
                double adjustedX = targetX - CheckingButton.Width / 2;
                double adjustedY = targetY - CheckingButton.Height / 2;

                CheckingButton.Margin = new Thickness(adjustedX, adjustedY, 0, 0);
            }
        }
        
        private void LoadFolderNames()
        {
            string dirPath = @"sensor_data";
            if (Directory.Exists(dirPath))
            {
                var folders = Directory.GetDirectories(dirPath);
                foreach (var folder in folders)
                {
                    folderComboBox_Name.Items.Add(System.IO.Path.GetFileName(folder));
                }
            }
            else
            {
                MessageBox.Show($"Directory {dirPath} does not exist.");
            }
        }

        private void folderComboBox_Action_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox != null && comboBox.SelectedItem != null)
            {
                var selectedItem = comboBox.SelectedItem as ComboBoxItem;
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

        private void btn_UserDate_Record(object sender, RoutedEventArgs e)
        {
            if (folderComboBox_Name.Text == "")
            {
                var customMessageBox = new CustomMessageBox(3);
                customMessageBox.ShowDialog();
            }
            else
            {
                string patient = folderComboBox_Name.Text;
                string timestamp = DateTime.Now.ToString("yyyy_MM_dd");

                MainWindow.GlobalDirPath = @"sensor_data" + '\\' + folderComboBox_Name.Text + "\\" + timestamp;
                _mainWindow.clk_DeviceStreaming(MainWindow.GlobalDirPath);
            }
        }

        public void Start_Record()
        {
            string patient = folderComboBox_Name.Text;
            string timestamp = DateTime.Now.ToString("yyyy_MM_dd");

            MainWindow.GlobalDirPath = @"sensor_data" + '\\' + folderComboBox_Name.Text + "\\" + timestamp;
            _mainWindow.clk_DeviceStreaming(MainWindow.GlobalDirPath);
        }

        private void btn_Add_New_Patient(object sender, RoutedEventArgs e)
        {
            string directoryPath = MainWindow.GlobalDirPath;
            directoryPath = @"sensor_data" + "\\" + folderComboBox_Name.Text;

            _mainWindow.clk_Add_patient();
        }   

        private void CheckingButton_Click(object sender, RoutedEventArgs e)
        {
            if (folderComboBox_Name.Text == "")
            {
               
                var customMessageBox = new CustomMessageBox(3);
                customMessageBox.ShowDialog();
            }
            else
            {
                for (int i = 0; i < 16; i++)
                {
                    _mainWindow.Shoot_electric[i] = 0;
                }

                string patient = folderComboBox_Name.Text;

                string timestamp = DateTime.Now.ToString("yyyy_MM_dd");
                MainWindow.GlobalDirPath = @"sensor_data" + '\\' + folderComboBox_Name.Text + "\\" + timestamp;

                MainWindow.Patient_Name= patient;
                _mainWindow.CheckingButton_Click(patient);
            }
        }

        private void testmark(object sender, RoutedEventArgs e)
        {
            string pa = "C:\\Users\\Andy\\OneDrive - 慈濟大學\\桌面\\專題\\Git-Code\\2024.6.23\\Topics\\C# .NET\\Basic Streaming .NET\\bin\\Debug\\net6.0-windows10.0.17763.0\\sensor_data\\沈_A556\\2024_12_05\\Other\\NP-12-05\\02-28-36-2.csv";
            Last_Window last_Window = new Last_Window(pa);
            last_Window.Show();
        }

        private void folderComboBox_Name_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}
