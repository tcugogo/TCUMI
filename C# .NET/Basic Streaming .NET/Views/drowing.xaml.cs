using System.Windows;
using System.IO;
using CsvHelper;
using LiveCharts;
using LiveCharts.Wpf;
using System;
using System.Globalization;
using System.ComponentModel;
using System.Collections.Generic;
using System.Threading.Tasks;
using MathNet.Numerics.RootFinding;
namespace Basic_Streaming_NET.Views
{
    public partial class drowing : Window
    {
        public SeriesCollection SeriesCollection { get; set; }

        public drowing()
        {
            InitializeComponent();

            // Initialize the SeriesCollection
            SeriesCollection = new SeriesCollection();

            // Load EMG data asynchronously
            LoadEMGDataAsync();
        }

        private async void LoadEMGDataAsync()
        {
            try
            {
                // Disable UI controls while loading data
                this.IsEnabled = false;

                // Read the CSV file and get the data for EMG 1
                string filePath = "C:\\Users\\TCUMI\\Downloads\\test.csv"; // 替換成你的CSV檔案路徑
                List<double> emgData = await ReadEMGDataFromCSVAsync(filePath, "EMG 1");

                // Add the EMG data to the chart
                LineSeries series = new LineSeries
                {
                    Title = "EMG 1",
                    Values = new ChartValues<double>(emgData)
                };
                SeriesCollection.Add(series);
                DataContext = this;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error reading CSV file: " + ex.Message);
            }
            finally
            {
                // Enable UI controls after loading data
                this.IsEnabled = true;
            }
        }

        private async Task<List<double>> ReadEMGDataFromCSVAsync(string filePath, string emgName)
        {
            List<double> emgData = new List<double>();

            using (StreamReader reader = new StreamReader(filePath))
            {
                string[] headers = (await reader.ReadLineAsync()).Split(',');
                int emgIndex = Array.IndexOf(headers, emgName);

                while (!reader.EndOfStream)
                {
                    string[] data = (await reader.ReadLineAsync()).Split(',');
                    if (data.Length > emgIndex && double.TryParse(data[emgIndex], out double emgValue))
                    {
                        emgData.Add(emgValue);
                    }
                }
            }

            return emgData;
        }
    }
}