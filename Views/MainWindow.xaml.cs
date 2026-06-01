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
        private readonly TranscribePage _transcribePage = new();
        private readonly GifPage _gifPage = new();
        private readonly BatchDownloadPage _batchDownloadPage = new();
        private readonly TwitchPage _twitchPage = new();

        private string? _pendingUpdateVersion;
        private string? _pendingUpdateChangelog;

        internal DownloadPage DownloadPage => _downloadPage;
        internal ConvertPage ConvertPage => _convertPage;
        internal TranscribePage TranscribePage => _transcribePage;
        internal TwitchPage TwitchPage => _twitchPage;
        internal BatchDownloadPage BatchDownloadPage => _batchDownloadPage;

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
            txtNavTranscribe.Text = T("MainWindow.Nav.Transcribe");
            txtNavGifMaker.Text = T("MainWindow.Nav.GifMaker");
            txtNavTwitchDownload.Text = T("MainWindow.Nav.TwitchDownload");
            txtNavBatchDownload.Text = T("MainWindow.Nav.BatchDownload");

            // Version Label und Softwareinfo
            txtVersionLabel.Text = T("MainWindow.Version");
            lblSoftwareinfo.Text = T("MainWindow.Softwareinfo");

            // Credits-Button
            txtCreditsTitle.Text    = T("MainWindow.Credits.Title");
            txtCreditsSubtitle.Text = T("MainWindow.Credits.Subtitle");

            // joke: Support-Button
            txtJokeDonateTitle.Text    = T("MainWindow.JokeDonate.Title");
            txtJokeDonateSubtitle.Text = T("MainWindow.JokeDonate.Subtitle");

            // Sektion-Titel aktualisieren
            RefreshSectionTitle();
        }

        public void RefreshSectionTitle()
        {
            var T = UITextDictionary.Get;

            // Einstellungen in separater ListBox prüfen
            if (SettingsNavigationList.SelectedIndex >= 0)
            {
                txtSectionTitle.Text = T("MainWindow.Nav.Settings");
                return;
            }

            int idx = NavigationList.SelectedIndex;
            if (idx < 0) return;

            var sectionTitles = new[] {
                T("MainWindow.Nav.Download"),
                T("MainWindow.Nav.BatchDownload"),
                T("MainWindow.Nav.Convert"),
                T("MainWindow.Nav.Transcribe"),
                T("MainWindow.Nav.GifMaker"),
                T("MainWindow.Nav.TwitchDownload"),
            };

            if (idx < sectionTitles.Length)
                txtSectionTitle.Text = sectionTitles[idx];
        }

        private void NavigationList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int idx = NavigationList.SelectedIndex;
            if (idx < 0) return;

            // Einstellungs-ListBox abwählen
            SettingsNavigationList.SelectionChanged -= SettingsNavigationList_SelectionChanged;
            SettingsNavigationList.SelectedIndex = -1;
            SettingsNavigationList.SelectionChanged += SettingsNavigationList_SelectionChanged;

            RefreshSectionTitle();

            switch (idx)
            {
                case 0:
                    _downloadPage.RefreshPaths();
                    MainFrame.Navigate(_downloadPage);
                    break;
                case 1:
                    _batchDownloadPage.ApplyDebugMode();
                    MainFrame.Navigate(_batchDownloadPage);
                    break;
                case 2:
                    MainFrame.Navigate(_convertPage);
                    break;
                case 3:
                    _transcribePage.RefreshAll();
                    MainFrame.Navigate(_transcribePage);
                    break;
                case 4:
                    MainFrame.Navigate(_gifPage);
                    break;
                case 5:
                    MainFrame.Navigate(_twitchPage);
                    break;
            }
        }

        private void SettingsNavigationList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SettingsNavigationList.SelectedIndex < 0) return;

            // Haupt-NavList abwählen
            NavigationList.SelectionChanged -= NavigationList_SelectionChanged;
            NavigationList.SelectedIndex = -1;
            NavigationList.SelectionChanged += NavigationList_SelectionChanged;

            RefreshSectionTitle();
            MainFrame.Navigate(_settingsPage);
        }

        private void btnCredits_Click(object sender, RoutedEventArgs e)
        {
            var win = new CreditsWindow { Owner = this };
            win.ShowDialog();
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

