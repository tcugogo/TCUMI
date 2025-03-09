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
using System.Windows.Shapes;

namespace Basic_Streaming_NET.Views
{
    /// <summary>
    /// CustomMessageBox.xaml 的互動邏輯
    /// </summary>
    public partial class CustomMessageBox : Window
    {
        public CustomMessageBox(int x)
        {
            InitializeComponent();
            if (x == 0)
            {
                this.Title = "新增失敗";
                T.Text = "資料未填完整";
            }
            else if (x == 1)
            {
                this.Title = "新增成功";
                T.Text = "個案資料建檔成功";
            }
            else if (x == 2) {
                this.Title = "已存在";
                T.Text = "個案資料已存在";
            }
            else if (x == 3) {
                this.Title = "請選擇";
                T.Text = "請選擇病患";
            }
            else if (x == 4)
            {
                this.Title = "找不到檔案";
                T.Text = "沒有建檔";
            }
            else if (x == 5)
            {
                this.Title = "請先連接Delsys";
                T.Text = "請先連接Delsys";
            }
            else if (x == 6)
            {
                this.Title = "成功";
                T.Text = "Mark更新成功";
            }
            else if (x == 7)
            {
                this.Title = "新增成功";
                T.Text = "備註新增成功";
            }
            else if (x == 8)
            {
                this.Title = "開始更新Mark";
                T.Text = "開始更新Mark";
            }
            else if (x == 9)
            {
                this.Title = "取消操作";
                T.Text = "Mark無移動\n恢復原本位置";
            }
            else if(x == 10)
            {
                this.Title = "";
                T.Text = "K值沒有輸入";
            }
            
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
