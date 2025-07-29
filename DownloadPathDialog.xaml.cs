using MortysDLP.Services;
using MortysDLP.UITexte;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace MortysDLP
{
    /// <summary>
    /// Interaktionslogik für DownloadPathDialog.xaml
    /// </summary>
    public partial class DownloadPathDialog : Window
    {
        public DownloadPathDialog()
        {
            /********************************************************************/
            /*
              Sprachanpassung bei Software-Start
            */
            // Debug: Sprache erzwingen
            bool forceEnglish = Properties.Settings.Default.FORCE_ENGLISH_LANGUAGE;
            SetLanguage(forceEnglish);

            /********************************************************************/

            InitializeComponent();
            cbAudioOnlyPath.IsChecked = Properties.Settings.Default.CHECKED_AUDIOPATH;
            PathBox.Text = Properties.Settings.Default.DOWNLOADPATH;
            AudioPathBox.Text = Properties.Settings.Default.DOWNLOADAUDIOONLYPATH;
            SetUIAudioPathBox();
            
            
            SetUITexte();
        }

        private void SetLanguage(bool forceEnglish)
        {
            CultureInfo culture;
            if (forceEnglish)
            {
                culture = new CultureInfo("en");
            }
            else
            {
                // Standard: Englisch, aber wenn Windows-Sprache Deutsch ist, dann Deutsch verwenden
                var windowsCulture = CultureInfo.CurrentUICulture;
                if (windowsCulture.TwoLetterISOLanguageName == "de")
                    culture = new CultureInfo("de");
                else
                    culture = new CultureInfo("en");
            }

            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {

            bool isDownloadPathEmpty = string.IsNullOrWhiteSpace(PathBox.Text);
            bool isAudioPathRequired = cbAudioOnlyPath.IsChecked == true;
            bool isAudioPathEmpty = isAudioPathRequired && string.IsNullOrWhiteSpace(AudioPathBox.Text);

            if (isDownloadPathEmpty || isAudioPathEmpty)
            {
                string message = UITexte.UITexte.DownloadPathDialog_NoDownloadFolder_Message.Replace("\\n", Environment.NewLine);
                if (isDownloadPathEmpty)
                    message += UITexte.UITexte.DownloadPathDialog_DownloadPathEmpty.Replace("\\n", Environment.NewLine);
                if (isAudioPathEmpty)
                    message += UITexte.UITexte.DownloadPathDialog_AudioPathEmpty.Replace("\\n", Environment.NewLine);
                message += UITexte.UITexte.DownloadPathDialog_SetDefaultWindowsDownloadFolder.Replace("\\n", Environment.NewLine);

                var result = MessageBox.Show(
                    message,
                    UITexte.UITexte.DownloadPathDialog_NoDownloadFolder_Title,
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    // Windows Standardpfad setzen
                    if (isDownloadPathEmpty)
                        PathBox.Text = KnownFolders.GetPath(KnownFolder.Downloads);
                    if (isAudioPathEmpty)
                        AudioPathBox.Text = KnownFolders.GetPath(KnownFolder.Downloads);
                    // Nachsetzen, dann weiter wie gewohnt
                }
                else if (result == MessageBoxResult.No)
                {
                    // Dialog schließen, nichts speichern
                    DialogResult = false;
                    Close();
                    return;
                }
                else // Cancel
                {
                    // Dialog bleibt offen, User kann erneut suchen
                    return;
                }
            }


            Properties.Settings.Default.DOWNLOADPATH = PathBox.Text;

            Properties.Settings.Default.CHECKED_AUDIOPATH = cbAudioOnlyPath.IsChecked ?? false;

            if (cbAudioOnlyPath.IsChecked == true)
            {
                Properties.Settings.Default.DOWNLOADAUDIOONLYPATH = AudioPathBox.Text;
            }
            else
            {
                Properties.Settings.Default.DOWNLOADAUDIOONLYPATH = PathBox.Text; // Wenn AudioOnly nicht aktiviert, dann den normalen Downloadpfad verwenden
            }
            Properties.Settings.Default.Save();
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
#pragma warning disable CA1416 // Plattformkompatibilität überprüfen
            var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog
            {
                Description = UITexte.UITexte.DownloadPathDialog_BrowseFolder_Description,
                SelectedPath = PathBox.Text,
                UseDescriptionForTitle = true
            };
#pragma warning restore CA1416 // Plattformkompatibilität überprüfen
#pragma warning disable CA1416 // Plattformkompatibilität überprüfen
            if (dialog.ShowDialog(this) == true)
            {
                PathBox.Text = dialog.SelectedPath;
            }
#pragma warning restore CA1416 // Plattformkompatibilität überprüfen
        }

        private void BrowseAudio_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog
            {
                Description = UITexte.UITexte.DownloadPathDialog_BrowseAudioFolder_Description,
                SelectedPath = AudioPathBox.Text,
                UseDescriptionForTitle = true
            };
            if (dialog.ShowDialog(this) == true)
            {
                AudioPathBox.Text = dialog.SelectedPath;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Hauptfenster suchen und lbl_downloadpath.Content aktualisieren
            foreach (Window window in Application.Current.Windows)
            {
                if (window is Hauptfenster mainWindow)
                {
                    if (mainWindow.FindName("lbl_downloadpath") is Label lbl)
                        lbl.Content = Properties.Settings.Default.DOWNLOADPATH;
                    if (mainWindow.FindName("lbl_audiopath") is Label lblAudio)
                        lblAudio.Content = Properties.Settings.Default.DOWNLOADAUDIOONLYPATH;
                    mainWindow.SetUiAudioEnabled(Properties.Settings.Default.CHECKED_AUDIOPATH);
                    break;
                }
            }
        }

        private void cbAudioOnlyPath_Checked(object sender, RoutedEventArgs e)
        {
            SetUIAudioPathBox();
        }
        private void cbAudioOnlyPath_Unchecked(object sender, RoutedEventArgs e)
        {
            SetUIAudioPathBox();
        }

        private void SetUIAudioPathBox()
        {
            if (this.cbAudioOnlyPath.IsChecked == true)
            {
                AudioPathBox.IsEnabled = true;
                btn_SearchAudioPath.IsEnabled = true;
            }
            else
            {
                AudioPathBox.Text = String.Empty;
                AudioPathBox.IsEnabled = false;
                btn_SearchAudioPath.IsEnabled = false;
            }

        }

        private void SetUITexte()
        {
            this.Title = UITexte.UITexte.DownloadPathDialog_Title;
            this.lbl_DefaultDownloadPath.Text = UITexte.UITexte.DownloadPathDialog_StandardDownloadPathLabel;
            this.lbl_DefaultAudioDownloadPath.Text = UITexte.UITexte.DownloadPathDialog_StandardAudioDownloadPathLabel;
            this.btn_SearchDownloadPath.Content = UITexte.UITexte.Button_Browse;
            this.btn_SearchAudioPath.Content = UITexte.UITexte.Button_Browse;
            this.btn_OK.Content = UITexte.UITexte.Button_OK;
            this.btn_Cancel.Content = UITexte.UITexte.Button_Cancel;
        }
    }
}
