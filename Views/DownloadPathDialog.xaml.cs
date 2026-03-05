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
            /* Sprache wurde bereits in App.xaml.cs gesetzt */
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
                var T = UITexte.UITextDictionary.Get;
                string message = T("DownloadPathDialog.Validation.Message");
                if (isDownloadPathEmpty) message += T("DownloadPathDialog.Validation.DownloadEmpty");
                if (isAudioPathEmpty)   message += T("DownloadPathDialog.Validation.AudioEmpty");
                message += T("DownloadPathDialog.Validation.UseDefault");

                var result = FluentMessageBox.Show(
                    message,
                    T("DownloadPathDialog.Validation.Title"),
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Warning,
                    this);

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
                Description = UITextDictionary.Get("DownloadPathDialog.Browse.Description"),
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
                Description = UITextDictionary.Get("DownloadPathDialog.Browse.AudioDescription"),
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
                    mainWindow.DownloadPage.RefreshPaths();
                    mainWindow.DownloadPage.SetUiAudioEnabled(Properties.Settings.Default.CheckedAudioOnlyPath);
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
                AudioPathPanel.Visibility = Visibility.Visible;
                tbAudioPathBox.IsEnabled = true;
                btnSearchAudioPath.IsEnabled = true;
            }
            else
            {
                AudioPathPanel.Visibility = Visibility.Collapsed;
                tbAudioPathBox.Text = String.Empty;
                tbAudioPathBox.IsEnabled = false;
                btnSearchAudioPath.IsEnabled = false;
            }
        }
        private void SetUITexte()
        {
            var T = UITextDictionary.Get;

            // Window & Header
            this.Title                            = T("DownloadPathDialog.Title");
            txtHeaderTitle.Text                   = T("DownloadPathDialog.Header.Title");
            txtHeaderSubtitle.Text                = T("DownloadPathDialog.Header.Subtitle");

            // Labels
            this.lblDefaultDownloadPath.Text      = T("DownloadPathDialog.Label.DownloadPath");
            this.lblAudioOnlyPath.Text            = T("DownloadPathDialog.Label.AudioPath");
            this.lblDefaultAudioDownloadPath.Text = T("DownloadPathDialog.Checkbox.AudioPath");

            // Buttons
            this.btnDownloadPath.Content          = T("DownloadPathDialog.Button.Browse");
            this.btnSearchAudioPath.Content       = T("DownloadPathDialog.Button.Browse");
            this.btnOK.Content                    = T("DownloadPathDialog.Button.OK");
            this.btnCancel.Content                = T("DownloadPathDialog.Button.Cancel");
        }
    }
}
