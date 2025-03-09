using System;
using DelsysAPI.Pipelines;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Toolkit.Uwp.Notifications;

namespace Basic_Streaming.NET.Views
{
    public partial class PairSensor : UserControl
    {
        private Pipeline _pipeline;
        private LoadingIcon _loadingIcon;
        private MainWindow _mainWindow;
        private DeviceStreaming _deviceStreaming;
        private System.Threading.CancellationTokenSource cancellationToken;

        private static readonly Regex _regex = new Regex("^[0-9]+$");
        private int[] IconMargin = { 97, -3, 108, 150 };
        private int[] msgMargin = { 0, 45, 0, 0 };
        private int[] msgONLYMargin = { 0, 22, 0, 0 };

        int Sensor_count = 1;

        public PairSensor(Pipeline pipeline, MainWindow mainWindowPanel, DeviceStreaming deviceStreaming)
        {
            InitializeComponent();

            _pipeline = pipeline;
            _mainWindow = mainWindowPanel;
            _deviceStreaming = deviceStreaming;

            textbox_ForSensorNumber.Text = Sensor_count.ToString();
        }

        public async void selectComponentNumber()
        {
            // 預檢查輸入是否爲有效的數字
            if (!_regex.IsMatch(textbox_ForSensorNumber.Text))
            {
                ShowErrorMessage("Your input was not a valid integer. Please enter a valid integer.");
                return;
            }

            int sensorNumber;
            if (!int.TryParse(textbox_ForSensorNumber.Text, out sensorNumber) || sensorNumber > 100000)
            {
                ShowErrorMessage("The entered number is not within range (0-100,000)");
                return;
            }

            Sensor_count++;

            // 開始掃描前更新UI
            mainPageButtonAndResetButtonToggle(false);
            TextBoxForSensorNumberDropDown.Visibility = Visibility.Collapsed;
            textbox_ForSensorNumber.Visibility = Visibility.Collapsed;
            textbox_ForSensorNumber.IsEnabled = false;

            // 顯示加載圖標
            _loadingIcon = new LoadingIcon("Scanning for pair requests...", IconMargin, msgMargin);
            MainPanel.Children.Add(_loadingIcon);

            cancellationToken = new System.Threading.CancellationTokenSource();

            try
            {
                bool succeedInFindingAdditionalSensors = await ScanForPairRequestAsync(sensorNumber);

                if (!succeedInFindingAdditionalSensors)
                {
                    _loadingIcon.JustShowMessage("Could not find any additional sensors to pair...", msgONLYMargin);
                    return;
                }

                new ToastContentBuilder()
                    .AddText($"Sensor {textbox_ForSensorNumber.Text} has been paired!")
                    .Show();

                textbox_ForSensorNumber.Text = Sensor_count.ToString();
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"An error occurred: {ex.Message}");
            }
            finally
            {
                // 掃描完成後更新UI
                mainPageButtonAndResetButtonToggle(true);
                _deviceStreaming.btn_ScanSensors.IsEnabled = true;

                MainPanel.Children.Remove(_loadingIcon);
                TextBoxForSensorNumberDropDown.Visibility = Visibility.Visible;
                textbox_ForSensorNumber.Visibility = Visibility.Visible;
                textbox_ForSensorNumber.IsEnabled = true;
            }
        }

        private async Task<bool> ScanForPairRequestAsync(int sensorNumber = 0)
        {
            // 檢查是否超過可支持的傳感器數量
            if (_pipeline.TrignoRfManager.Components.Count <= _pipeline.TrignoRfManager.SupportedNumberOfSlots())
            {
                return await _pipeline.TrignoRfManager.AddTrignoComponent(cancellationToken.Token, sensorNumber, false);
            }

            Debug.WriteLine("# of components after pair: " + _pipeline.TrignoRfManager.Components.Count);
            return false;
        }

        public void clk_AddSensor(object sender, RoutedEventArgs e)
        {
            UserInputLettersErrorMessage.Visibility = Visibility.Collapsed;
            selectComponentNumber();
        }

        private void mainPageButtonAndResetButtonToggle(bool shouldTurnOn)
        {
            _mainWindow.btn_backToMainPageButton.IsEnabled = shouldTurnOn;
        }

        private void clk_Cancel(object sender, RoutedEventArgs e)
        {
            _deviceStreaming.ConfigurePipeline();
            _deviceStreaming.btn_ScanSensors.IsEnabled = true;
            (this.Parent as Grid).Children.Remove(this);
        }

        private void ShowErrorMessage(string message)
        {
            UserInputLettersErrorMessage.Text = message;
            UserInputLettersErrorMessage.Visibility = Visibility.Visible;
        }
    }
}
