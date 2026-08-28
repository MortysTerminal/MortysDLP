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

            var T = UITexte.UITextDictionary.Get;
            Title = T("DownloadProgressDialog.Title");
            CancelButton.Content = T("DownloadProgressDialog.Button.Cancel");

            InfoText.Text = info;
            // Unbestimmt, bis der erste Fortschrittswert eintrifft - fehlt die Gesamtgröße
            // (kein Content-Length), bleibt der Balken sonst dauerhaft bei 0 stehen.
            ProgressBar.IsIndeterminate = true;
            ProgressBar.Value = 0;
        }

        /// <param name="value">Fortschritt als Anteil (0.0–1.0).</param>
        public void SetProgress(double value)
        {
            double clamped = Math.Clamp(value, 0.0, 1.0);
            ProgressBar.IsIndeterminate = false;
            ProgressBar.Value = clamped * 100;
            PercentText.Text = $"{clamped * 100:F0} %";
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Explizit auslösen statt sich nur auf OnClosing zu verlassen, und den Knopf
            // sofort deaktivieren - ein zweiter Klick während des Schließens darf nicht zu
            // einem zweiten Abbruchversuch führen.
            CancelButton.IsEnabled = false;
            _cts.Cancel();
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
