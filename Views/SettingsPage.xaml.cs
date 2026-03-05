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
