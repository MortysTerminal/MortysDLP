using MortysDLP.Helpers;
using MortysDLP.UITexte;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace MortysDLP.Views
{
    public partial class SettingsPage : Page
    {
        private bool _isInitializing;

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
            SetBandwidthRowEnabled(bwEnabled);
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

            // Schriftbild
            cbFontAppearance.Items.Clear();
            string savedAppearance = FontAppearance.Current;
            foreach (string value in FontAppearance.All)
            {
                var item = new ComboBoxItem { Tag = value };
                cbFontAppearance.Items.Add(item);
                if (value == savedAppearance)
                    cbFontAppearance.SelectedItem = item;
            }

            _isInitializing = false;
            SetUITexts();
        }

        private static string GetLanguageDisplayName(string code, bool isAuto)
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
            
            return code.ToUpperInvariant();
        }

        public void SetUITexts()
        {
            var T = UITextDictionary.Get;
            
            Title = T("MainWindow.Nav.Settings");
            
            // Karte "Downloads": Zielpfad + Geschwindigkeit
            txtSectionDownloads.Text = T("SettingsPage.Section.Downloads");
            btnChangeDownloadPath.Content = T("SettingsPage.Button.ChangeDownloadPath");

            // Karte "Darstellung": Sprache + Schriftbild
            txtSectionAppearance.Text = T("SettingsPage.Section.Appearance");
            lblSelectLanguage.Content = T("SettingsPage.Label.SelectLanguage");
            txtLanguageInfo.Text = T("SettingsPage.Label.LanguageInfo");
            lblFontAppearance.Content = T("SettingsPage.Label.FontAppearance");
            txtFontAppearanceInfo.Text = T("SettingsPage.FontAppearance.Info");
            foreach (ComboBoxItem item in cbFontAppearance.Items)
            {
                string v = (string)item.Tag;
                item.Content = T($"FontAppearance.{char.ToUpperInvariant(v[0])}{v[1..]}");
            }

            // Karte "Anwendung": Links, Beenden, Debug
            txtSectionApplication.Text = T("SettingsPage.Section.Application");
            btnGitHub.Content = T("SettingsPage.Button.OpenGitHub");
            btnClose.Content = T("SettingsPage.Button.CloseApp");
            cbDebugMode.Content = T("SettingsPage.Checkbox.DebugMode");

            // Bandbreite (in der Karte "Downloads")
            txtBandwidthInfo.Text     = T("SettingsPage.Bandwidth.Info");
            cbBandwidthEnabled.Content = T("SettingsPage.Bandwidth.EnableCheckbox");
            lblBandwidthLimit.Content = T("SettingsPage.Bandwidth.Label");
            txtBandwidthUnit.Text     = T("SettingsPage.Bandwidth.Unit");
            SetBandwidthRowEnabled(cbBandwidthEnabled.IsChecked == true);

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
                FluentMessageBox.Show(UITexte.UITexte.Error_OpenBrowser, UITexte.UITexte.Error, MessageBoxButton.OK, MessageBoxImage.Error, Window.GetWindow(this));
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        /// <summary>Aktiviert bzw. sperrt die Bandbreiten-Zeile (Feld + „MB/s"). Das Feld
        /// bekommt im gesperrten Zustand zusätzlich eine gedämpfte Schriftfarbe — das
        /// Fluent-Theme lässt den eingegebenen Wert sonst fast normal stehen. Nur dieses eine
        /// Feld, kein globaler Stil (der hat den modernen Feld-Look zerschossen).</summary>
        private void SetBandwidthRowEnabled(bool enabled)
        {
            pnlBandwidthLimit.IsEnabled = enabled;
            if (enabled)
                tbBandwidthLimit.ClearValue(Control.ForegroundProperty);
            else
                tbBandwidthLimit.SetResourceReference(Control.ForegroundProperty, "TextFillColorDisabledBrush");
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
            SetBandwidthRowEnabled(enabled);
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
            Log.MinLevel = newValue ? LogLevel.Debug : LogLevel.Info;

            foreach (Window window in Application.Current.Windows)
            {
                if (window is MainWindow mainWindow)
                {
                    mainWindow.RefreshDebugMode();
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

        private void cbFontAppearance_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;

            if (cbFontAppearance.SelectedItem is ComboBoxItem item && item.Tag is string value)
            {
                Properties.Settings.Default.FontAppearance = value;
                Properties.Settings.Default.Save();
                FontAppearance.Apply(value);
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
            if (Window.GetWindow(this) is MainWindow mw)
                mw.RefreshVisiblePageUITexts();
        }

        /// <summary>Benachrichtigt die laufenden Download-Seiten über die geänderte Bandbreite.</summary>
        private void NotifyBandwidthChanged()
        {
            if (Window.GetWindow(this) is MainWindow mw)
                mw.NotifyBandwidthChanged();
        }

        private void RefreshAllUITexts()
        {
            SetUITexts();

            if (Window.GetWindow(this) is MainWindow mainWindow)
            {
                mainWindow.SetUITexts();
                mainWindow.RefreshVisiblePageUITexts();
            }
        }
    }
}
