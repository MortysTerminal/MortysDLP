using MortysDLP.Services.Tools;
using MortysDLP.UITexte;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace MortysDLP.Views
{
    /// <summary>
    /// Sperr-Karte für eine Funktionsseite, wenn ein für die Seite erforderliches externes
    /// Werkzeug fehlt. Statt den Arbeitsbereich weiterlaufen zu lassen und erst mitten in der
    /// Arbeit mit einer technischen Meldung zu scheitern, sagt die Karte sofort, was fehlt und
    /// welche Funktionen davon betroffen sind, und führt mit einem Knopf zur Werkzeuge-Seite.
    ///
    /// <para>Gleiches Muster wie die Setup-Hinweise auf <see cref="TranscribePage"/> und
    /// <see cref="TwitchPage"/> — hier als wiederverwendbares Steuerelement, damit alle
    /// betroffenen Seiten (Download, Batch, Convert, GIF) dieselbe Karte tragen.</para>
    /// </summary>
    public partial class ToolMissingNotice : UserControl
    {
        public ToolMissingNotice()
        {
            InitializeComponent();
            Visibility = Visibility.Collapsed;
        }

        /// <summary>Prüft die übergebenen Werkzeuge anhand ihrer Zieldatei. Fehlt mindestens
        /// eines, wird die Karte sichtbar und mit Text gefüllt; sonst bleibt sie
        /// <see cref="Visibility.Collapsed"/>. Rückgabe: <c>true</c>, wenn alle vorhanden sind.</summary>
        public bool Evaluate(params (string ToolId, string ExePath)[] required)
        {
            var missingIds = required
                .Where(r => !File.Exists(r.ExePath))
                .Select(r => r.ToolId)
                .Distinct()
                .ToList();

            if (missingIds.Count == 0)
            {
                Visibility = Visibility.Collapsed;
                return true;
            }

            var T = UITextDictionary.Get;
            string names = string.Join(", ", missingIds.Select(ToolFeatureMap.DisplayName));
            string features = string.Join(", ", missingIds
                .SelectMany(ToolFeatureMap.FeatureKeys)
                .Distinct()
                .Select(T));

            txtTitle.Text = T("ToolMissingNotice.Title");
            txtBody.Text = string.Format(CultureInfo.CurrentCulture, T("ToolMissingNotice.Body"), names, features);
            btnToTools.Content = T("ToolMissingNotice.Button");
            Visibility = Visibility.Visible;
            return false;
        }

        private void btnToTools_Click(object sender, RoutedEventArgs e) =>
            (Window.GetWindow(this) as MainWindow)?.NavigateToTools();
    }
}
