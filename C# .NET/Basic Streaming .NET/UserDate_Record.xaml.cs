using Basic_Streaming.NET;
using System;
using System.Windows;
using System.Windows.Controls;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using TextBox = System.Windows.Controls.TextBox;
using System.IO;
using ModernWPF.Controls;
using Basic_Streaming_NET.Views;

namespace Basic_Streaming_NET
{
    /// <summary>
    /// Interaction logic for UserDate_Record.xaml
    /// </summary>
    public partial class UserDate_Record : UserControl
    {
        private MainWindow _mainWindow;

        public UserDate_Record(MainWindow mainWindowPanel)
        {
            InitializeComponent();

            _mainWindow = mainWindowPanel;

            // 預設選擇今天的日期
            dpChooseDate.SelectedDate = DateTime.Today;
            dpImplantDate.SelectedDate = null;
            dpInjuryDate.SelectedDate = null;
        }

        string Day_Path, Name_Path;

        private void SubmitButton_Click(object sender, RoutedEventArgs e)
        {
            // 確認建檔日期有效性
            if (!DateTime.TryParse(dpChooseDate.Text, out DateTime selectedDate))
            {
                MessageBox.Show("請選擇有效的建檔日期");
                return;
            }

            // 確認植入日期和受傷日期的有效性（如果有填）
            DateTime? implantDate = dpImplantDate.SelectedDate;
            DateTime? injuryDate = dpInjuryDate.SelectedDate;

            string thistime = selectedDate.ToString("yyyy_MM_dd");
            Name_Path = @"sensor_data" + "\\" + txtPatientName.Text + "_" + txtPatientNumber.Text;
            Day_Path = Name_Path + "\\" + thistime;

            // 確認必要欄位是否填寫
            if (string.IsNullOrWhiteSpace(txtPatientName.Text) ||
                string.IsNullOrWhiteSpace(txtPatientNumber.Text) ||
                string.IsNullOrWhiteSpace(cboInjuryLevel.Text) ||
                string.IsNullOrWhiteSpace(txtInjuryLocation.Text) ||
                cboGender.SelectedItem == null ||
                cboTreatmentPart.SelectedItem == null)
            {
                var customMessageBox = new CustomMessageBox(0);
                customMessageBox.ShowDialog();
                return;
            }

            MainWindow.GlobalDirPath = Day_Path;

            // 如果資料夾不存在，則建立新資料夾
            if (!Directory.Exists(Name_Path))
            {
                Directory.CreateDirectory(Name_Path);

                // 構建 txt 檔案的路徑
                string patientDataFilePath = Path.Combine(Name_Path, $"{txtPatientName.Text}_{txtPatientNumber.Text}個人資料.txt");

                // 備註內容（若未填寫則為「無」）
                string remarksText = string.IsNullOrEmpty(txtRemarks.Text) ? "無" : txtRemarks.Text;

                // 建立病人資料內容
                string patientDataContent = $"個案姓名:\n{txtPatientName.Text}\n" +
                                            $"個案編號:\n{txtPatientNumber.Text}\n" +
                                            $"年齡:\n{txtAge.Text}\n" +
                                            $"性別:\n{((ComboBoxItem)cboGender.SelectedItem)?.Content ?? "未填寫"}\n" +
                                            $"受傷等級:\n{cboInjuryLevel.Text}\n" +
                                            $"受傷部位:\n{txtInjuryLocation.Text}\n" +
                                            $"治療部位:\n{((ComboBoxItem)cboTreatmentPart.SelectedItem)?.Content ?? "未填寫"}\n" +
                                            $"植入日期:\n{implantDate?.ToString("yyyy/MM/dd") ?? "未填寫"}\n" +
                                            $"受傷日期:\n{injuryDate?.ToString("yyyy/MM/dd") ?? "未填寫"}\n" +
                                            $"主治醫師:\n{((ComboBoxItem)cboDoctor.SelectedItem)?.Content ?? "未填寫"}\n" +
                                            $"建檔日期:\n{selectedDate:yyyy/MM/dd}\n" +
                                            $"備註:\n{remarksText}";

                // 將內容寫入 txt 檔案
                File.WriteAllText(patientDataFilePath, patientDataContent);

                // 顯示成功訊息
                var customMessageBox = new CustomMessageBox(1);
                customMessageBox.ShowDialog();
                StartStreamingButton.IsEnabled = true;
            }
            else
            {
                // 資料夾已存在，顯示提示訊息
                var customMessageBox = new CustomMessageBox(2);
                customMessageBox.ShowDialog();
            }
        }

        private void StartStreamingButton_Click(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(Day_Path))
            {
                Directory.CreateDirectory(Day_Path);
            }
            MainWindow.Patient_Name = txtPatientName.Text + "_" + txtPatientNumber.Text;
            _mainWindow.streaming_start++;
            _mainWindow.clk_DeviceStreaming(Day_Path);
        }
    }
}
