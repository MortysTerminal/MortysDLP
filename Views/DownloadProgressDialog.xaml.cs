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
    public partial class DownloadProgressDialog : Window
    {
        private readonly CancellationTokenSource _cts = new();
        private bool _closed;

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

        /// <summary>Wechselt die Anzeige auf einen Abschnitt ohne eigenen Prozentwert
        /// (Entpacken, Ersetzen, Prüfen) — der Balken pulsiert wieder, statt bei 100 % stehen
        /// zu bleiben. Ohne diesen Wechsel sieht der Dialog nach dem Download eingefroren aus,
        /// obwohl im Hintergrund weitergearbeitet wird: Der Nutzer sieht nur den Balken, nicht
        /// die Statuszeile dahinter, die zwar mitläuft, aber vom Dialog verdeckt wird.</summary>
        public void SetStage(string text)
        {
            InfoText.Text = text;
            ProgressBar.IsIndeterminate = true;
            PercentText.Text = "";
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Knopf sofort deaktivieren, damit ein zweiter Klick während des Schließens nichts
            // mehr auslöst. Der Abbruch selbst passiert in OnClosing - der einzige Abbruchpfad.
            CancelButton.IsEnabled = false;
            Close();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Einziger Abbruchpfad: über den Knopf, das Fenster-X oder CloseIfOpen() vom
            // Aufrufer landet alles hier. Cancel() ist idempotent.
            _cts.Cancel();
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            _closed = true;
            base.OnClosed(e);
            _cts.Dispose();
        }

        /// <summary>Schließt den Dialog, wenn er noch offen ist. Der Aufrufer ruft das im
        /// <c>finally</c> nach der begleiteten Arbeit auf; hat der Nutzer vorher „Abbrechen"
        /// gedrückt, ist das ein wirkungsloser Aufruf statt einer Ausnahme.</summary>
        public void CloseIfOpen()
        {
            if (!_closed)
                Close();
        }
    }
}
