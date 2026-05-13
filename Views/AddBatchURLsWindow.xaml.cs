using MortysDLP.UITexte;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace MortysDLP.Views
{
    public partial class AddBatchURLsWindow : Window
    {
        /// <summary>
        /// Enthält nach erfolgreichem Schließen alle gültigen URLs.
        /// </summary>
        public List<string> ValidUrls { get; private set; } = new List<string>();

        public AddBatchURLsWindow()
        {
            InitializeComponent();
        }

        private void txtUrls_TextChanged(object sender, TextChangedEventArgs e)
        {
            var lines = SplitLines(txtUrls.Text);
            int validCount = lines.Count(IsValidUrl);
            txtStatus.Text = $"{lines.Count} Zeile(n), {validCount} gültige URL(s)";
        }

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            var T = UITextDictionary.Get;
            var lines = SplitLines(txtUrls.Text);

            var valid = lines.Where(IsValidUrl).Distinct().ToList();
            int invalidCount = lines.Count - valid.Count;

            if (valid.Count == 0)
            {
                FluentMessageBox.Show(
                    T("AddBatchUrlsWindow.NoValidUrls.Message"),
                    T("AddBatchUrlsWindow.NoValidUrls.Title"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information,
                    owner: this);
                return;
            }

            if (invalidCount > 0)
            {
                var message = string.Format(
                    T("AddBatchUrlsWindow.Confirm.Message"),
                    valid.Count,
                    invalidCount);

                var res = FluentMessageBox.Show(
                    message,
                    T("AddBatchUrlsWindow.Confirm.Title"),
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Question,
                    owner: this);

                if (res != MessageBoxResult.OK) return;
            }

            ValidUrls = valid;
            DialogResult = true;
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;  // schließt das Fenster und signalisiert Abbruch
        }

        /// <summary>
        /// Zerlegt den Eingabetext zeilenweise, entfernt Leerraum und leere Einträge.
        /// </summary>
        private static List<string> SplitLines(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new List<string>();

            return text
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();
        }

        /// <summary>
        /// Prüft, ob ein String eine gültige http/https-URL ist.
        /// </summary>
        private static bool IsValidUrl(string text)
        {
            return Uri.TryCreate(text, UriKind.Absolute, out var uri)
                   && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }
    }
}