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

namespace MortysDLP
{
    /// <summary>
    /// Interaktionslogik für DownloadProgressDialog.xaml
    /// </summary>
    public partial class DownloadProgressDialog : Window
    {
        public DownloadProgressDialog(string info)
        {
            InitializeComponent();
            InfoText.Text = info;
            ProgressBar.Value = 0;
        }

        public void SetProgress(double value)
        {
            ProgressBar.Value = value * 100;
        }
    }
}
