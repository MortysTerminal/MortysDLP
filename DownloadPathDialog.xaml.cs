using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;

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
            PathBox.Text = Properties.Settings.Default.DOWNLOADPATH;
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.DOWNLOADPATH = PathBox.Text;
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

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Hauptfenster suchen und lbl_downloadpath.Content aktualisieren
            foreach (Window window in Application.Current.Windows)
            {
                if (window is Hauptfenster mainWindow)
                {
                    if (mainWindow.FindName("lbl_downloadpath") is Label lbl)
                    {
                        lbl.Content = Properties.Settings.Default.DOWNLOADPATH;
                    }
                    break;
                }
            }
        }
    }
}
