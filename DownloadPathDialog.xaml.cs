using MortysDLP.Services;
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
            InitializeComponent();
            cbAudioOnlyPath.IsChecked = Properties.Settings.Default.CHECKED_AUDIOPATH;
            PathBox.Text = Properties.Settings.Default.DOWNLOADPATH;
            AudioPathBox.Text = Properties.Settings.Default.DOWNLOADAUDIOONLYPATH;
            SetUIAudioPathBox();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {

            bool isDownloadPathEmpty = string.IsNullOrWhiteSpace(PathBox.Text);
            bool isAudioPathRequired = cbAudioOnlyPath.IsChecked == true;
            bool isAudioPathEmpty = isAudioPathRequired && string.IsNullOrWhiteSpace(AudioPathBox.Text);

            if (isDownloadPathEmpty || isAudioPathEmpty)
            {
                string message = "Es wurde kein Download-Ordner gewählt.\n";
                if (isDownloadPathEmpty)
                    message += "- Standard-Downloadpfad fehlt.\n";
                if (isAudioPathEmpty)
                    message += "- AudioOnly-Downloadpfad fehlt.\n";
                message += "\nMöchten Sie den Windows Standard-Ordner verwenden?";

                var result = MessageBox.Show(
                    message,
                    "Kein Ordner gewählt",
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
            Properties.Settings.Default.DOWNLOADAUDIOONLYPATH = AudioPathBox.Text;
            Properties.Settings.Default.CHECKED_AUDIOPATH = cbAudioOnlyPath.IsChecked ?? false;
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
                Description = "Wähle den Download-Ordner",
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
                Description = "Wähle den AudioOnly-Download-Ordner",
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
    }
}
