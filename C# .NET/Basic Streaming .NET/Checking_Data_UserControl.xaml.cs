using Basic_Streaming.NET;
using Basic_Streaming_NET;
using Basic_Streaming_NET.Views;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Basic_Streaming_NET
{
    public partial class Checking_Data_UserControl : UserControl
    {
        MainWindow _mainWindow;

        public Checking_Data_UserControl(MainWindow mainWindowPanel, string patient_path)
        {
            InitializeComponent();

            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

            // 病人資料夾路徑
            string Patien_File_Path = System.IO.Path.Combine(baseDirectory, "sensor_data\\" + patient_path);

            MainWindow.GlobalDirPath = Patien_File_Path;
            // 病人資訊txt檔
            string Patien_Data_txt_Path = Patien_File_Path + "\\" + patient_path + "個人資料.txt";

            // 加載病人資料與看診紀錄
            LoadFolderNames(Patien_Data_txt_Path, Patien_File_Path);
            _mainWindow = mainWindowPanel;
        }

        // 將資料用到UI上
        private void LoadFolderNames(string Patien_Data_txt_Path, string Patien_File_Path)
        {
            List<string> specificLines = ReadSpecificLines(Patien_Data_txt_Path);
            Dictionary<string, List<string>> folderStructure = GetFolderStructure(Patien_File_Path);

            try
            {
                // 綁定個人資訊到 UI
                Patien_name.Text = specificLines[0];
                Patien_ID.Text = specificLines[1];
                Patien_Age.Text = specificLines[2];
                Patien_Gender.Text = specificLines[3];
                Patien_LV.Text = specificLines[4];
                Patien_Site.Text = specificLines[5];
                Patien_TreatmentPart.Text = specificLines[6];
                Patien_ImplantDate.Text = specificLines[7];
                Patien_InjuryDate.Text = specificLines[8];
                Doctor_name.Text = specificLines[9];
                Patien_CreateDay.Text = specificLines[10];
                Patien_Remark.Text = specificLines[11];
            }
            catch
            {
                var customMessageBox = new CustomMessageBox(4);
                customMessageBox.ShowDialog();
            }

            // 動態加載看診紀錄到 UI
            LoadDiagnosisRecords(folderStructure);
        }

        // 動態加載看診紀錄
        private void LoadDiagnosisRecords(Dictionary<string, List<string>> folderStructure)
        {
            // 清空現有的紀錄，避免重複
            DiagnosisRecordsPanel.Children.Clear();

            if (folderStructure.Count == 0)
            {
                // 如果沒有診紀錄，顯示「無診紀錄」
                var noRecordsTextBlock = new TextBlock
                {
                    Text = "無復健診紀錄",
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Foreground = System.Windows.Media.Brushes.Gray,
                    Margin = new Thickness(0, 10, 0, 5)
                };
                DiagnosisRecordsPanel.Children.Add(noRecordsTextBlock);
                return;
            }

            // 如果有診紀錄，對於每個日期資料夾，創建日期標題和復健動作列表
            foreach (var date in folderStructure)
            {
                // 日期標題
                var dateTextBlock = new TextBlock
                {
                    Text = date.Key,
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Foreground = System.Windows.Media.Brushes.Gray,
                    Margin = new Thickness(0, 10, 0, 5)
                };
                DiagnosisRecordsPanel.Children.Add(dateTextBlock);

                // 復健動作
                var actionsTextBlock = new TextBlock
                {
                    Text = "復健部位：" + string.Join("、", date.Value),
                    FontSize = 16,
                    Foreground = System.Windows.Media.Brushes.DarkSlateGray,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(10, 0, 0, 10)
                };
                DiagnosisRecordsPanel.Children.Add(actionsTextBlock);
            }
        }

        // 個人資料
        static List<string> ReadSpecificLines(string filePath)
        {
            int[] linesToRead = { 2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24 };
            List<string> result = new List<string>();

            try
            {
                using (StreamReader reader = new StreamReader(filePath))
                {
                    int currentLine = 1;
                    string line;

                    while ((line = reader.ReadLine()) != null)
                    {
                        if (Array.Exists(linesToRead, element => element == currentLine))
                        {
                            result.Add(line);
                        }
                        currentLine++;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error reading file: " + ex.Message);
            }

            return result;
        }

        // 獲取資料夾結構
        static Dictionary<string, List<string>> GetFolderStructure(string rootPath)
        {
            Dictionary<string, List<string>> result = new Dictionary<string, List<string>>();

            try
            {
                DirectoryInfo rootDirectory = new DirectoryInfo(rootPath);

                foreach (var dateFolder in rootDirectory.GetDirectories())
                {
                    List<string> actions = new List<string>();

                    foreach (var actionFolder in dateFolder.GetDirectories())
                    {
                        actions.Add(actionFolder.Name);
                    }

                    result[dateFolder.Name] = actions;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }

            return result;
        }
    }
}
