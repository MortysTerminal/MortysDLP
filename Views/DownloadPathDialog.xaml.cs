using MortysDLP.Helpers;
using MortysDLP.UITexte;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace MortysDLP
{
    public partial class DownloadPathDialog : Window
    {
        public DownloadPathDialog()
        {
            /* Sprachanpassung bei Window-Start */
            LanguageHelper.ApplyLanguage(LanguageHelper.ForceEnglish);

            InitializeComponent();
            cbAudioOnlyPath.IsChecked = Properties.Settings.Default.CheckedAudioOnlyPath;
            tbDownloadPath.Text = Properties.Settings.Default.DownloadPath;
            tbAudioPathBox.Text = Properties.Settings.Default.DownloadAudioOnlyPath;
            SetUItbAudioPathBox();
            SetUITexte();
        }
        private void btnOK_Click(object sender, RoutedEventArgs e)
        {

            bool isDownloadPathEmpty = string.IsNullOrWhiteSpace(tbDownloadPath.Text);
            bool isAudioPathRequired = cbAudioOnlyPath.IsChecked == true;
            bool isAudioPathEmpty = isAudioPathRequired && string.IsNullOrWhiteSpace(tbAudioPathBox.Text);

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
                        tbDownloadPath.Text = KnownFoldersHelper.GetPath(KnownFolder.Downloads);
                    if (isAudioPathEmpty)
                        tbAudioPathBox.Text = KnownFoldersHelper.GetPath(KnownFolder.Downloads);
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


            Properties.Settings.Default.DownloadPath = tbDownloadPath.Text;

            Properties.Settings.Default.CheckedAudioOnlyPath = cbAudioOnlyPath.IsChecked ?? false;

            if (cbAudioOnlyPath.IsChecked == true)
            {
                Properties.Settings.Default.DownloadAudioOnlyPath = tbAudioPathBox.Text;
            }
            else
            {
                Properties.Settings.Default.DownloadAudioOnlyPath = tbDownloadPath.Text; // Wenn AudioOnly nicht aktiviert, dann den normalen Downloadpfad verwenden
            }
            Properties.Settings.Default.Save();
            DialogResult = true;
            Close();
        }
        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
        private void btnDownloadPath_Click(object sender, RoutedEventArgs e)
        {
            #pragma warning disable CA1416 // Plattformkompatibilität überprüfen
            var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog
            {
                Description = UITexte.UITexte.DownloadPathDialog_BrowseFolder_Description,
                SelectedPath = tbDownloadPath.Text,
                UseDescriptionForTitle = true
            };
            #pragma warning restore CA1416 // Plattformkompatibilität überprüfen
            #pragma warning disable CA1416 // Plattformkompatibilität überprüfen
            if (dialog.ShowDialog(this) == true)
            {
                tbDownloadPath.Text = dialog.SelectedPath;
            }
            #pragma warning restore CA1416 // Plattformkompatibilität überprüfen
        }
        private void btnSearchAudioPath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog
            {
                Description = UITexte.UITexte.DownloadPathDialog_BrowseAudioFolder_Description,
                SelectedPath = tbAudioPathBox.Text,
                UseDescriptionForTitle = true
            };
            if (dialog.ShowDialog(this) == true)
            {
                tbAudioPathBox.Text = dialog.SelectedPath;
            }
        }
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            foreach (Window window in Application.Current.Windows)
            {
                if (window is MainWindow mainWindow)
                {
                    if (mainWindow.FindName("lblDownloadPath") is Label lbl)
                        lbl.Content = Properties.Settings.Default.DownloadPath;
                    if (mainWindow.FindName("lblAudioPath") is Label lblAudio)
                        lblAudio.Content = Properties.Settings.Default.DownloadAudioOnlyPath;
                    mainWindow.SetUiAudioEnabled(Properties.Settings.Default.CheckedAudioOnlyPath);
                    break;
                }
            }
        }
        private void cbAudioOnlyPath_Checked(object sender, RoutedEventArgs e) => SetUItbAudioPathBox();
        private void cbAudioOnlyPath_Unchecked(object sender, RoutedEventArgs e) => SetUItbAudioPathBox();
        private void SetUItbAudioPathBox()
        {
            if (this.cbAudioOnlyPath.IsChecked == true)
            {
                tbAudioPathBox.IsEnabled = true;
                btnSearchAudioPath.IsEnabled = true;
            }
            else
            {
                tbAudioPathBox.Text = String.Empty;
                tbAudioPathBox.IsEnabled = false;
                btnSearchAudioPath.IsEnabled = false;
            }

        }
        private void SetUITexte()
        {
            this.Title = UITexte.UITexte.DownloadPathDialog_Title;
            this.lblDefaultDownloadPath.Text = UITexte.UITexte.DownloadPathDialog_StandardDownloadPathLabel;
            this.lblDefaultAudioDownloadPath.Text = UITexte.UITexte.DownloadPathDialog_StandardAudioDownloadPathLabel;
            this.btnDownloadPath.Content = UITexte.UITexte.Button_Browse;
            this.btnSearchAudioPath.Content = UITexte.UITexte.Button_Browse;
            this.btnOK.Content = UITexte.UITexte.Button_OK;
            this.btnCancel.Content = UITexte.UITexte.Button_Cancel;
        }
    }
}
