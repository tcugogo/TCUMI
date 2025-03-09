using Basic_Streaming.NET;
using System;
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

namespace Basic_Streaming_NET.Views
{
    /// <summary>
    /// AnalyzeFile.xaml 的互動邏輯
    /// </summary>
    public partial class AnalyzeFile : UserControl
    {

        MainWindow _mainWindow;
        string AnalyzeFile_patient_name;

        public AnalyzeFile(MainWindow mainWindowPanel, string patient_name)
        {
            InitializeComponent();
            _mainWindow = mainWindowPanel;
            AnalyzeFile_patient_name = patient_name;
        }

        //積分長條圖
        private void BtnIntegrationBarChart_Click(object sender, RoutedEventArgs e)
        {
            Multiple_file_Selection_Analysis _Multiple_file_Selection_Analysis = new Multiple_file_Selection_Analysis(AnalyzeFile_patient_name, 0);
            _Multiple_file_Selection_Analysis.Show();
        }
        //最高點前後0.5秒比較
        private void BtnPeakComparison_Click(object sender, RoutedEventArgs e)
        {
            Multiple_file_Selection_Analysis _Multiple_file_Selection_Analysis = new Multiple_file_Selection_Analysis(AnalyzeFile_patient_name, 1);
            _Multiple_file_Selection_Analysis.Show();
        }
        //NNMF 分析
        private void BtnNNMF_Click(object sender, RoutedEventArgs e)
        {
            Single_file_Selection_Analysis_1 _Single_file_Selection_Analysis_1 = new Single_file_Selection_Analysis_1(AnalyzeFile_patient_name);
            _Single_file_Selection_Analysis_1.Show();
        }
        //PCA + HFD 分析
        private void BtnPCANHFD_Click(object sender, RoutedEventArgs e)
        {
            Single_file_Selection_Analysis_2 _Single_file_Selection_Analysis_2 = new Single_file_Selection_Analysis_2(AnalyzeFile_patient_name);
            _Single_file_Selection_Analysis_2.Show();
        }
    }
}
