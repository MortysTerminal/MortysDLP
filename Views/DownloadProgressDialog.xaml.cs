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
        private readonly CancellationTokenSource _cts = new();

        public CancellationToken CancellationToken => _cts.Token;

        public DownloadProgressDialog(string info)
        {
            /* Sprache wurde bereits in App.xaml.cs gesetzt */
            InitializeComponent();
            InfoText.Text = info;
            ProgressBar.Value = 0;
        }

        public void SetProgress(double value)
        {
            ProgressBar.Value = value * 100;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            _cts.Cancel();
            base.OnClosing(e);
        }

        public void Dispose()
        {
            try { _cts.Cancel(); } catch { }
            try { Close(); } catch { }
            _cts.Dispose();
        }
    }
}
