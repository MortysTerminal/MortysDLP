using MortysDLP.Helpers;
using MortysDLP.Properties;
using MortysDLP.Services;
using MortysDLP.UITexte;
using MortysDLP.Views;
using System.Collections.Generic;
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
        private readonly ToolsPage _toolsPage = new();

        private string? _pendingUpdateVersion;
        private string? _pendingUpdateChangelog;

        /// <summary>Gesetzt, solange der Banner ein Update meldet, das hier nicht installierbar
        /// ist. Ein Klick auf den Banner zeigt dann die Erklärung statt des Änderungsdialogs —
        /// „Jetzt aktualisieren" gäbe es an dieser Stelle nichts anzubieten.</summary>
        private string? _blockedUpdateReasonKey;

        internal DownloadPage DownloadPage => _downloadPage;
        internal ConvertPage ConvertPage => _convertPage;
        internal TranscribePage TranscribePage => _transcribePage;
        internal TwitchPage TwitchPage => _twitchPage;
        internal BatchDownloadPage BatchDownloadPage => _batchDownloadPage;
        internal GifPage GifPage => _gifPage;
        internal ToolsPage ToolsPage => _toolsPage;

        /// <summary>Alle Seiten mit einem abbrechbaren Hintergrundvorgang — Grundlage der
        /// Update-Vorprüfung. Seiten laufen als Singletons im Hintergrund weiter,
        /// auch wenn gerade eine andere Seite angezeigt wird (siehe Navigate-Aufrufe unten).</summary>
        internal IReadOnlyList<ICancellableWork> ActiveWorkSources =>
            new ICancellableWork[] { _downloadPage, _batchDownloadPage, _convertPage, _gifPage, _transcribePage, _twitchPage, _toolsPage };

        public MainWindow()
        {
            InitializeComponent();
            lblMainVersion.Text = AppInfo.Current ?? UITextDictionary.Get("MainWindow.Version.Unknown");
            NavigationList.SelectedIndex = 0;
            SetUITexts();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (Application.Current is not App app)
                return;

            // Die Rückmeldung zu einem zuvor angestoßenen Update liegt bereits vor - sie kommt
            // aus einer kleinen lokalen Datei, die vor diesem Fenster ausgewertet wurde. Ein
            // ggf. neues Update-Angebot dagegen läuft erst jetzt im Hintergrund an und trifft
            // über ApplyPendingUpdateOffer nach, sobald es fertig ist.
            if (app.PendingUpdateOutcome is { } outcome)
                ShowUpdateOutcomeNotice(outcome.Outcome, outcome.ToVersion, outcome.Attempts,
                    outcome.Changelog, outcome.UpdaterLogPath);
        }

        /// <summary>Zeigt Update-Banner oder Blockiert-Hinweis, sobald die im Hintergrund erst
        /// nach dem Anzeigen dieses Fensters gestartete Prüfung (siehe
        /// <see cref="App.OnStartup"/>) ein Ergebnis hat. Wird über den Dispatcher aufgerufen,
        /// weil das Ergebnis auf einem Hintergrundthread ankommt.</summary>
        internal void ApplyPendingUpdateOffer(App app)
        {
            if (app.PendingUpdateInfo.HasValue)
            {
                var info = app.PendingUpdateInfo.Value;
                _pendingUpdateVersion  = info.Version;
                _pendingUpdateChangelog = info.Changelog;
                ShowUpdateBanner(info.Version, app.PendingUpdateInstallKind);
            }
            else if (app.BlockedUpdateInfo is { } blocked)
            {
                ShowBlockedUpdateBanner(blocked.Version, blocked.ReasonKey);
            }
        }

        /// <summary>Zeigt die Rückmeldung zu einem beim letzten Lauf angestoßenen Update. Bei
        /// einem Fehlschlag mit „Trotzdem erneut versuchen" — das löscht nur den
        /// Schleifenschutz-Zustand, ein neuer Versuch geschieht erst über das nächste
        /// automatische oder manuelle Angebot. Bei Erfolg mit vorliegendem Changelog-Text
        /// zusätzlich „Änderungen ansehen" — der einmalige „Was ist neu"-Hinweis, der
        /// dank Löschung von <c>update-state.json</c> danach nicht erneut erscheint.</summary>
        private void ShowUpdateOutcomeNotice(UpdateOutcome outcome, string? version, int attempts,
            string? changelog, string? updaterLogPath)
        {
            var T = UITextDictionary.Get;
            string versionText = version ?? string.Empty;

            if (outcome == UpdateOutcome.Succeeded)
            {
                string successMessage = T("Update.Result.Success").Replace("{0}", versionText, StringComparison.Ordinal);

                if (string.IsNullOrWhiteSpace(changelog))
                {
                    FluentMessageBox.Show(successMessage, icon: MessageBoxImage.Information, owner: this);
                    return;
                }

                var choice = FluentMessageBox.Show(
                    successMessage, "", MessageBoxImage.Information, this,
                    (T("Update.Result.ViewChanges"), MessageBoxResult.Yes, true),
                    (T("Common.Button.OK"), MessageBoxResult.OK, false));

                if (choice == MessageBoxResult.Yes)
                {
                    var whatsNewDialog = new UpdateChangelogDialog(versionText, changelog, isWhatsNewOnly: true)
                    {
                        Owner = this
                    };
                    whatsNewDialog.ShowDialog();
                }
                return;
            }

            if (outcome != UpdateOutcome.Failed)
                return;

            // Der Grund des Fehlschlags steht im Protokoll des Updaters, nicht in dem der
            // Anwendung - deren Protokoll endet beim Start des Updaters. Nur wenn der Pfad
            // nicht aufgezeichnet wurde (Zustandsdatei aus einer älteren Version), bleibt das
            // App-Protokoll der beste verfügbare Hinweis.
            string logPathForUser = string.IsNullOrWhiteSpace(updaterLogPath)
                ? Log.CurrentLogFile
                : updaterLogPath;

            string message =
                T("Update.Result.Failed").Replace("{0}", versionText, StringComparison.Ordinal) + "\n\n" +
                T("Update.Result.Failed.Hint").Replace("{0}", logPathForUser, StringComparison.Ordinal);

            if (attempts >= UpdateState.MaxAttemptsBeforeBlocking)
                message += "\n\n" +
                    T("Update.Blocked.TooManyAttempts").Replace("{0}", versionText, StringComparison.Ordinal);

            var result = FluentMessageBox.Show(
                message, "", MessageBoxImage.Warning, this,
                (T("Update.Result.Retry"), MessageBoxResult.Retry, true),
                (T("Common.Button.OK"), MessageBoxResult.OK, false));

            if (result == MessageBoxResult.Retry)
            {
                _ = UpdateState.DeleteAsync();
                Log.Info("Schleifenschutz für die Zielversion zurückgesetzt (Trotzdem erneut versuchen).");
            }
        }

        /// <summary><paramref name="installKind"/> ändert nur den Unterhinweis im Banner
        /// (der Klick öffnet weiterhin denselben Änderungsdialog) — die eigentliche Warnung für
        /// <c>NeedsElevation</c> greift erst bei „Jetzt aktualisieren" in
        /// <see cref="btnUpdateBanner_Click"/>, wo mehr Platz für die volle Erklärung ist.</summary>
        private void ShowUpdateBanner(string version, InstallKind? installKind)
        {
            var T = UITextDictionary.Get;
            _blockedUpdateReasonKey  = null;
            txtUpdateBannerMain.Text = string.Format(T("UpdateBanner.Text"), version);
            txtUpdateBannerSub.Text  = installKind == InstallKind.NeedsElevation
                ? T("UpdateBanner.SubText.NeedsElevation")
                : T("UpdateBanner.SubText");
            btnDismissBanner.ToolTip = T("UpdateBanner.Dismiss");
            UpdateBanner.Visibility  = Visibility.Visible;
        }

        /// <summary>Banner für ein Update, das es gibt, das sich hier aber nicht installieren
        /// lässt (schreibgeschützter Ordner, Start aus der ZIP-Vorschau). Der Download wird
        /// nicht angeboten — der Grund aber genannt, statt dass die Anwendung kommentarlos nie
        /// wieder ein Update meldet.</summary>
        private void ShowBlockedUpdateBanner(string version, string reasonKey)
        {
            var T = UITextDictionary.Get;
            _blockedUpdateReasonKey  = reasonKey;
            txtUpdateBannerMain.Text = T("UpdateBanner.Text.Blocked")
                .Replace("{0}", version, StringComparison.Ordinal);
            txtUpdateBannerSub.Text  = T("UpdateBanner.SubText.Blocked");
            btnDismissBanner.ToolTip = T("UpdateBanner.Dismiss");
            UpdateBanner.Visibility  = Visibility.Visible;
        }

        private async void btnUpdateBanner_Click(object sender, RoutedEventArgs e)
        {
            // Update vorhanden, aber hier nicht installierbar: Es gibt nichts zu entscheiden,
            // also auch keinen Änderungsdialog mit "Jetzt aktualisieren" - nur die Erklärung.
            if (_blockedUpdateReasonKey is { } reasonKey)
            {
                FluentMessageBox.Show(
                    UITextDictionary.Get(reasonKey), "", MessageBoxImage.Information, this);
                return;
            }

            var dialog = new UpdateChangelogDialog(_pendingUpdateVersion ?? string.Empty, _pendingUpdateChangelog ?? string.Empty)
            {
                Owner = this
            };
            dialog.ShowDialog();

            switch (dialog.Choice)
            {
                case UpdateChoice.Update:
                    if (Application.Current is App appForElevationCheck &&
                        appForElevationCheck.PendingUpdateInstallKind == InstallKind.NeedsElevation &&
                        !ConfirmElevationRisk())
                    {
                        // Abgelehnt: Banner bleibt bewusst sichtbar - der Nutzer kann die
                        // Installation verschieben und es danach erneut versuchen, ohne das
                        // Angebot zu verlieren.
                        break;
                    }

                    UpdateBanner.Visibility = Visibility.Collapsed;
                    await ((App)Application.Current).StartUpdate();
                    break;

                case UpdateChoice.Skip:
                    TrySkipVersion(_pendingUpdateVersion);
                    UpdateBanner.Visibility = Visibility.Collapsed;
                    break;

                case UpdateChoice.Later:
                    UpdateBanner.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        /// <summary>Erklärt vor einem Update in einem geschützten Systemordner, was dort
        /// passieren wird, und lässt den Nutzer entscheiden. „Trotzdem versuchen" ist bewusst
        /// nicht die vorbelegte Antwort, aber vorhanden: Die Einschätzung „geschützter Ordner"
        /// stammt aus einer Pfadprüfung und kann danebenliegen — etwa bei einer Installation,
        /// deren Berechtigungen jemand bewusst geöffnet hat. Ein Programm, das dem Nutzer die
        /// Entscheidung ganz abnimmt, sperrt genau diese Fälle grundlos aus. Scheitert der
        /// Versuch, meldet der Updater das geordnet und die alte Version läuft weiter.</summary>
        private bool ConfirmElevationRisk()
        {
            var T = UITextDictionary.Get;

            return FluentMessageBox.Show(
                T("Update.Elevation.Warning"), "", MessageBoxImage.Warning, this,
                (T("Update.Elevation.TryAnyway"), MessageBoxResult.Yes, false),
                (T("Common.Button.Cancel"), MessageBoxResult.Cancel, true)) == MessageBoxResult.Yes;
        }

        /// <summary>Schreibt <c>VersionSkip</c> — der Dialog selbst speichert keine
        /// Einstellungen, das übernimmt der Aufrufer.
        /// Best-Effort: Ein Fehlschlag hier soll den Nutzer nicht mit einer Fehlermeldung
        /// stören, nur beim nächsten Start erneut fragen.</summary>
        private static void TrySkipVersion(string? version)
        {
            if (string.IsNullOrEmpty(version))
                return;

            try
            {
                Settings.Default.VersionSkip = version;
                Settings.Default.Save();
                Log.Info($"Version {version} wird übersprungen (VersionSkip gesetzt).");
            }
            catch (Exception ex)
            {
                Log.Warn("VersionSkip konnte nicht gespeichert werden.", ex);
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
            txtNavTools.Text = T("MainWindow.Nav.Tools");

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

            // Werkzeuge/Einstellungen in separater ListBox prüfen
            int settingsIdx = SettingsNavigationList.SelectedIndex;
            if (settingsIdx >= 0)
            {
                var settingsSectionTitles = new[] {
                    T("MainWindow.Nav.Tools"),
                    T("MainWindow.Nav.Settings"),
                };

                if (settingsIdx < settingsSectionTitles.Length)
                    txtSectionTitle.Text = settingsSectionTitles[settingsIdx];
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
            int idx = SettingsNavigationList.SelectedIndex;
            if (idx < 0) return;

            // Haupt-NavList abwählen
            NavigationList.SelectionChanged -= NavigationList_SelectionChanged;
            NavigationList.SelectedIndex = -1;
            NavigationList.SelectionChanged += NavigationList_SelectionChanged;

            RefreshSectionTitle();

            switch (idx)
            {
                case 0:
                    MainFrame.Navigate(_toolsPage);
                    break;
                case 1:
                    MainFrame.Navigate(_settingsPage);
                    break;
            }
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

