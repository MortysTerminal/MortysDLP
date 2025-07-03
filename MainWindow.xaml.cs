using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MortysDLP.Services;
using System.Diagnostics;
using System.Threading.Tasks;

namespace MortysDLP
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        // Aktuelle Version der Anwendung
        string mortysdlpVersion = "2025.07.03";

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var updateService = new UpdateService();
            var (latestVersion, assetUrl) = await updateService.GetLatestReleaseInfoAsync();

            if (latestVersion != null && assetUrl != null && updateService.IsNewerVersion(latestVersion, mortysdlpVersion))
            {
                // Optional: Nutzer informieren
                var result = MessageBox.Show(
                    $"Eine neue Version ({latestVersion}) ist verfügbar. Jetzt updaten?",
                    "Update verfügbar",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    // ZIP-Datei im Temp-Ordner speichern
                    string tempZipPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MortysDLP.zip");
                    await updateService.DownloadAssetAsync(assetUrl, tempZipPath);

                    // Updater starten und App beenden
                    Process.Start("Updater/MortysDLP-Updater.exe", $"MortysDLP.exe \"{tempZipPath}\"");
                    Application.Current.Shutdown();
                }
            }
        }
    }
}