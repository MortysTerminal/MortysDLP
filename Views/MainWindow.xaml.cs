using MortysDLP.UITexte;
using MortysDLP.Views;
using System.Windows;
using System.Windows.Controls;

namespace MortysDLP
{
    public partial class MainWindow : Window
    {
        private readonly DownloadPage _downloadPage = new();
        private readonly ConvertPage _convertPage = new();
        private readonly SettingsPage _settingsPage = new();

        private string? _pendingUpdateVersion;
        private string? _pendingUpdateChangelog;

        internal DownloadPage DownloadPage => _downloadPage;
        internal ConvertPage ConvertPage => _convertPage;

        public MainWindow()
        {
            InitializeComponent();
            lblMainVersion.Text = Properties.Settings.Default.CurrentVersion;
            NavigationList.SelectedIndex = 0;
            SetUITexts();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (Application.Current is App app && app.PendingUpdateInfo.HasValue)
            {
                var info = app.PendingUpdateInfo.Value;
                _pendingUpdateVersion  = info.Version;
                _pendingUpdateChangelog = info.Changelog;
                ShowUpdateBanner(info.Version);
            }
        }

        private void ShowUpdateBanner(string version)
        {
            var T = UITextDictionary.Get;
            txtUpdateBannerMain.Text = string.Format(T("UpdateBanner.Text"), version);
            txtUpdateBannerSub.Text  = T("UpdateBanner.SubText");
            btnDismissBanner.ToolTip = T("UpdateBanner.Dismiss");
            UpdateBanner.Visibility  = Visibility.Visible;
        }

        private async void btnUpdateBanner_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new UpdateChangelogDialog(_pendingUpdateVersion ?? string.Empty, _pendingUpdateChangelog ?? string.Empty)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true && dialog.UpdateConfirmed)
            {
                UpdateBanner.Visibility = Visibility.Collapsed;
                await ((App)Application.Current).StartUpdate(Properties.Settings.Default.CurrentVersion);
            }
        }

        private void btnDismissBanner_Click(object sender, RoutedEventArgs e)
        {
            UpdateBanner.Visibility = Visibility.Collapsed;
        }

        public void SetUITexts()
        {
            var T = UITextDictionary.Get;

            // App-Titel und Untertitel
            txtAppTitle.Text = T("MainWindow.AppTitle");
            txtAppSubtitle.Text = T("MainWindow.AppSubtitle");

            // Navigation
            txtNavDownload.Text = T("MainWindow.Nav.Download");
            txtNavConvert.Text = T("MainWindow.Nav.Convert");
            txtNavSettings.Text = T("MainWindow.Nav.Settings");

            // Version Label und Softwareinfo
            txtVersionLabel.Text = T("MainWindow.Version");
            lblSoftwareinfo.Text = T("MainWindow.Softwareinfo");

            // joke: Support-Button
            txtJokeDonateTitle.Text    = T("MainWindow.JokeDonate.Title");
            txtJokeDonateSubtitle.Text = T("MainWindow.JokeDonate.Subtitle");

            // Sektion-Titel aktualisieren
            RefreshSectionTitle();
        }

        public void RefreshSectionTitle()
        {
            int idx = NavigationList.SelectedIndex;
            if (idx < 0) return;

            var T = UITextDictionary.Get;
            var sectionTitles = new[] {
                T("MainWindow.Nav.Download"),
                T("MainWindow.Nav.Convert"),
                T("MainWindow.Nav.Settings")
            };

            txtSectionTitle.Text = sectionTitles[idx];
        }

        private void NavigationList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int idx = NavigationList.SelectedIndex;
            if (idx < 0) return;

            RefreshSectionTitle();

            switch (idx)
            {
                case 0:
                    _downloadPage.RefreshPaths();
                    MainFrame.Navigate(_downloadPage);
                    break;
                case 1:
                    MainFrame.Navigate(_convertPage);
                    break;
                case 2:
                    MainFrame.Navigate(_settingsPage);
                    break;
            }
        }

        // joke
        private void btnJokeDonate_Click(object sender, RoutedEventArgs e)
        {
            var T = UITextDictionary.Get;
            FluentMessageBox.Show(
                T("MainWindow.JokeDonate.Message"),
                T("MainWindow.JokeDonate.MessageTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Information,
                owner: this);
        }
    }
}

