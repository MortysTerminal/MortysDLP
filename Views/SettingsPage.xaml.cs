using MortysDLP.Helpers;
using MortysDLP.UITexte;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace MortysDLP.Views
{
    public partial class SettingsPage : Page
    {
        private bool _isInitializing = false;

        public SettingsPage()
        {
            InitializeComponent();
        }

        private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            _isInitializing = true;
            
            cbDebugMode.IsChecked = Properties.Settings.Default.DebugMode;

            // Bandbreiten-Limit laden
            double bw = Properties.Settings.Default.DownloadBandwidthMBps;
            bool bwEnabled = bw > 0;
            cbBandwidthEnabled.IsChecked = bwEnabled;
            tbBandwidthLimit.IsEnabled = bwEnabled;
            tbBandwidthLimit.Text = bwEnabled ? bw.ToString(System.Globalization.CultureInfo.InvariantCulture) : "10";
            
            // Lade verfügbare Sprachen
            cbLanguage.Items.Clear();
            
            string savedLanguage = Properties.Settings.Default.SelectedLanguage;
            if (string.IsNullOrEmpty(savedLanguage))
            {
                savedLanguage = "auto";
            }
            
            foreach (var langOption in UITextDictionary.AvailableLanguages)
            {
                var displayText = GetLanguageDisplayName(langOption.Code, langOption.IsAuto);
                var item = new ComboBoxItem
                {
                    Content = displayText,
                    Tag = langOption.Code
                };
                cbLanguage.Items.Add(item);
                
                if (langOption.Code == savedLanguage)
                {
                    cbLanguage.SelectedItem = item;
                }
            }
            
            _isInitializing = false;
            SetUITexts();
        }

        private string GetLanguageDisplayName(string code, bool isAuto)
        {
            var T = UITextDictionary.Get;
            
            if (isAuto)
            {
                // Zeige "Automatisch (Deutsch)" oder "Automatic (English)" je nach erkannter Sprache
                string detectedLang = LanguageHelper.GetAutoDetectedLanguage();
                string detectedLangName = T($"Language.{(detectedLang == "de" ? "German" : "English")}");
                return $"{T("Language.Auto")} ({detectedLangName})";
            }
            else if (code == "de")
            {
                return T("Language.German");
            }
            else if (code == "en")
            {
                return T("Language.English");
            }
            
            return code.ToUpper();
        }

        public void SetUITexts()
        {
            var T = UITextDictionary.Get;
            
            Title = T("MainWindow.Nav.Settings");
            
            // Download Paths Section
            txtSectionDownloadPaths.Text = T("SettingsPage.Section.DownloadPaths");
            btnChangeDownloadPath.Content = T("SettingsPage.Button.ChangeDownloadPath");
            
            // App Section
            txtSectionApp.Text = T("SettingsPage.Section.App");
            btnGitHub.Content = T("SettingsPage.Button.OpenGitHub");
            btnClose.Content = T("SettingsPage.Button.CloseApp");
            
            // Debug Section
            txtSectionDebug.Text = T("SettingsPage.Section.Debug");
            cbDebugMode.Content = T("SettingsPage.Checkbox.DebugMode");
            
            // Language Section
            txtSectionLanguage.Text = T("SettingsPage.Section.Language");
            lblSelectLanguage.Content = T("SettingsPage.Label.SelectLanguage");
            txtLanguageInfo.Text = T("SettingsPage.Label.LanguageInfo");

            // Bandwidth Section
            txtSectionBandwidth.Text  = T("SettingsPage.Section.Bandwidth");
            txtBandwidthInfo.Text     = T("SettingsPage.Bandwidth.Info");
            cbBandwidthEnabled.Content = T("SettingsPage.Bandwidth.EnableCheckbox");
            lblBandwidthLimit.Content = T("SettingsPage.Bandwidth.Label");
            txtBandwidthUnit.Text     = T("SettingsPage.Bandwidth.Unit");
            lblBandwidthLimit.IsEnabled = cbBandwidthEnabled.IsChecked == true;
            tbBandwidthLimit.IsEnabled  = cbBandwidthEnabled.IsChecked == true;

            // Aktualisiere Dropdown-Inhalte
            if (!_isInitializing)
            {
                _isInitializing = true;
                foreach (ComboBoxItem item in cbLanguage.Items)
                {
                    var code = item.Tag.ToString()!;
                    var isAuto = code == "auto";
                    item.Content = GetLanguageDisplayName(code, isAuto);
                }
                _isInitializing = false;
            }
        }

        private void btnChangeDownloadPath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new DownloadPathDialog { Owner = Window.GetWindow(this) };
            dialog.ShowDialog();
        }

        private void btnGitHub_Click(object sender, RoutedEventArgs e)
        {
            string url = Properties.Settings.Default.MortysDLPGitHubURL;
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch
            {
                MessageBox.Show(UITexte.UITexte.Error_OpenBrowser, UITexte.UITexte.Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void tbBandwidthLimit_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            // Nur Ziffern und maximal ein Dezimalpunkt erlauben
            e.Handled = !System.Text.RegularExpressions.Regex.IsMatch(e.Text, @"[\d\.]");
        }

        private void tbBandwidthLimit_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing) return;
            if (cbBandwidthEnabled.IsChecked != true) return;
            string raw = tbBandwidthLimit.Text.Trim();
            if (double.TryParse(raw, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double val) && val > 0)
            {
                Properties.Settings.Default.DownloadBandwidthMBps = val;
                Properties.Settings.Default.Save();
                // Neustart erfolgt erst beim Verlassen der TextBox (LostFocus)
            }
        }

        private void tbBandwidthLimit_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            if (cbBandwidthEnabled.IsChecked != true) return;
            NotifyBandwidthChanged();
        }

        private void cbBandwidthEnabled_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            bool enabled = cbBandwidthEnabled.IsChecked == true;
            tbBandwidthLimit.IsEnabled = enabled;
            lblBandwidthLimit.IsEnabled = enabled;
            if (enabled)
            {
                // Gespeicherten Wert wieder einsetzen, Fallback 10 MB/s
                string raw = tbBandwidthLimit.Text.Trim();
                if (!double.TryParse(raw, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double val) || val <= 0)
                {
                    tbBandwidthLimit.Text = "10";
                    val = 10;
                }
                Properties.Settings.Default.DownloadBandwidthMBps = val;
            }
            else
            {
                Properties.Settings.Default.DownloadBandwidthMBps = 0;
            }
            Properties.Settings.Default.Save();
            RefreshBandwidthHints();
            NotifyBandwidthChanged();
        }

        private void cbDebugMode_Changed(object sender, RoutedEventArgs e)
        {
            bool newValue = cbDebugMode.IsChecked == true;
            Properties.Settings.Default.DebugMode = newValue;
            Properties.Settings.Default.Save();

            foreach (Window window in Application.Current.Windows)
            {
                if (window is MainWindow mainWindow)
                {
                    mainWindow.DownloadPage.ApplyDebugMode();
                    mainWindow.ConvertPage.ApplyDebugMode();
                    mainWindow.TranscribePage.ApplyDebugMode();
                    break;
                }
            }
        }

        private void cbLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            
            if (cbLanguage.SelectedItem is ComboBoxItem selectedItem)
            {
                string selectedCode = selectedItem.Tag.ToString()!;
                
                // Speichere die Sprachauswahl
                Properties.Settings.Default.SelectedLanguage = selectedCode;
                
                // Legacy-Support: Setze auch ForceEnglishLanguage
                Properties.Settings.Default.ForceEnglishLanguage = (selectedCode == "en");
                
                Properties.Settings.Default.Save();
                
                // Bestimme die tatsächlich zu verwendende Sprache
                string actualLanguage;
                if (selectedCode == "auto")
                {
                    actualLanguage = LanguageHelper.GetAutoDetectedLanguage();
                }
                else
                {
                    actualLanguage = selectedCode;
                }
                
                // Setze neue Sprache (inkl. Culture)
                LanguageHelper.ApplyLanguageCode(actualLanguage);
                
                // Hotload: Aktualisiere alle UI-Texte
                RefreshAllUITexts();
            }
        }

        private async void btnInstallTwitchDownloader_Click(object sender, RoutedEventArgs e)
        {
            // Navigiere zur TwitchPage und starte dort die Installation
            foreach (Window window in Application.Current.Windows)
            {
                if (window is MainWindow mainWindow)
                {
                    mainWindow.NavigationList.SelectedIndex = 5;
                    break;
                }
            }
        }

        private void RefreshBandwidthHints()
        {
            if (Window.GetWindow(this) is not MainWindow mw) return;
            if (mw.DownloadPage.IsLoaded) mw.DownloadPage.SetUITexts();
        }

        /// <summary>Benachrichtigt alle laufenden Download-Seiten über die geänderte Bandbreite.</summary>
        private void NotifyBandwidthChanged()
        {
            if (Window.GetWindow(this) is not MainWindow mw) return;
            mw.TwitchPage.ApplyBandwidthChange();
            mw.DownloadPage.ApplyBandwidthChange();
            mw.BatchDownloadPage.ApplyBandwidthChange();
        }

        private void RefreshAllUITexts()
        {
            // Aktualisiere diese Seite
            SetUITexts();
            
            // Finde MainWindow und aktualisiere alle Pages
            if (Window.GetWindow(this) is MainWindow mainWindow)
            {
                // Aktualisiere MainWindow selbst
                mainWindow.SetUITexts();
                
                // Aktualisiere Download Page
                if (mainWindow.DownloadPage.IsLoaded)
                {
                    mainWindow.DownloadPage.SetUITexts();
                }
                
                // Aktualisiere Convert Page
                if (mainWindow.ConvertPage.IsLoaded)
                {
                    mainWindow.ConvertPage.SetUITexts();
                }
            }
        }
    }
}
