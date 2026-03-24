using MortysDLP.UITexte;
using System.Windows;
using System.Windows.Input;

namespace MortysDLP.Views
{
    public partial class UpdateChangelogDialog : Window
    {
        public bool UpdateConfirmed { get; private set; }

        public UpdateChangelogDialog(string version, string changelog)
        {
            InitializeComponent();

            var T = UITextDictionary.Get;

            txtTitle.Text          = T("UpdateBannerDialog.Title");
            txtSubtitle.Text       = string.Format(T("UpdateBannerDialog.Subtitle"), version);
            txtChangelogLabel.Text = T("UpdateBannerDialog.ChangelogLabel");
            txtChangelog.Text      = string.IsNullOrWhiteSpace(changelog)
                                         ? T("UpdateBannerDialog.NoChangelog")
                                         : changelog;
            btnUpdateNow.Content   = T("UpdateBannerDialog.Button.UpdateNow");
            btnLater.Content       = T("UpdateBannerDialog.Button.Later");
        }

        private void btnUpdateNow_Click(object sender, RoutedEventArgs e)
        {
            UpdateConfirmed = true;
            DialogResult = true;
        }

        private void btnLater_Click(object sender, RoutedEventArgs e)
        {
            UpdateConfirmed = false;
            DialogResult = false;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Escape)
            {
                UpdateConfirmed = false;
                DialogResult = false;
                e.Handled = true;
            }
        }
    }
}
