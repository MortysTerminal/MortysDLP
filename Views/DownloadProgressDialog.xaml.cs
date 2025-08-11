using MortysDLP.Helpers;
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
    public partial class DownloadProgressDialog : Window, IDisposable
    {

        public DownloadProgressDialog(string info)
        {
            /* Sprachanpassung bei Window-Start */
            LanguageHelper.ApplyLanguage(LanguageHelper.ForceEnglish);

            InitializeComponent();
            InfoText.Text = info;
            ProgressBar.Value = 0;
        }

        public void SetProgress(double value)
        {
            ProgressBar.Value = value * 100;
        }
        public void Dispose()
        {
            this.Close();
        }
    }
}
