using MortysDLP.Helpers;
using MortysDLP.UITexte;
using System.Windows;
using System.Windows.Input;

namespace MortysDLP.Views
{
    /// <summary>Die drei Möglichkeiten, auf einen Update-Hinweis zu reagieren. Der Dialog
    /// speichert selbst keine Einstellungen — er meldet nur die Entscheidung, der Aufrufer
    /// (<c>MainWindow</c>) schreibt <c>VersionSkip</c>. Siehe
    /// <c>werkstatt/tasks/W2-T09.md</c>.</summary>
    public enum UpdateChoice
    {
        /// <summary>Jetzt aktualisieren.</summary>
        Update,
        /// <summary>Banner bleibt nur für die laufende Sitzung weg.</summary>
        Later,
        /// <summary>Banner bleibt weg, bis eine neuere Version als die hier angebotene
        /// erscheint.</summary>
        Skip,
    }

    public partial class UpdateChangelogDialog : Window
    {
        public UpdateChoice Choice { get; private set; } = UpdateChoice.Later;

        public UpdateChangelogDialog(string version, string changelog)
        {
            InitializeComponent();

            var T = UITextDictionary.Get;

            txtTitle.Text          = T("UpdateBannerDialog.Title");
            txtSubtitle.Text       = string.Format(T("UpdateBannerDialog.Subtitle"), version);
            txtChangelogLabel.Text = T("UpdateBannerDialog.ChangelogLabel");
            btnUpdateNow.Content   = T("UpdateBannerDialog.Button.UpdateNow");
            btnLater.Content       = T("UpdateBannerDialog.Button.Later");
            btnSkip.Content        = T("UpdateBannerDialog.Button.Skip");
            btnSkip.ToolTip        = T("UpdateBannerDialog.Button.Skip.Tooltip");

            var markdownText = string.IsNullOrWhiteSpace(changelog)
                ? T("UpdateBannerDialog.NoChangelog")
                : changelog;

            rtfChangelog.Document = MarkdownHelper.ToFlowDocument(markdownText);
        }

        private void btnUpdateNow_Click(object sender, RoutedEventArgs e)
        {
            Choice = UpdateChoice.Update;
            DialogResult = true;
        }

        private void btnSkip_Click(object sender, RoutedEventArgs e)
        {
            Choice = UpdateChoice.Skip;
            DialogResult = true;
        }

        private void btnLater_Click(object sender, RoutedEventArgs e)
        {
            Choice = UpdateChoice.Later;
            DialogResult = false;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Escape)
            {
                Choice = UpdateChoice.Later;
                DialogResult = false;
                e.Handled = true;
            }
        }
    }
}
