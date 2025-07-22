using System.Windows;
using System.Threading.Tasks;
using MortysDLP.Services;
using System.Runtime.CompilerServices;
using System.Drawing;

namespace MortysDLP
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        // Aktuelle Version aus den Einstellungen (value: latest = Update Skip ; For DEBUGGING!)
        // "2025.07.02"
        private string currentVersion = "latest";

        /* 
         * DEBUG
         * */
        private int DebugSleepTimer = 0; // 1 Sekunde ; 1000 ms


        protected override async void OnStartup(StartupEventArgs e)
        {
            var splash = new StartupWindow();
            splash.Show();

            // Splash: Logo und Titel (optional)
            // splash.SetLogo("Assets/dein_logo.png");
            // splash.SetTitle("Dein Produktname");

            // 1. Status: Nach Software-Update suchen
            await SetzeStatusTextVomStartupWindowUndWarte(splash, "Suche nach neuer Version...", DebugSleepTimer);

            if (await UpdateAvailable(CurrentVersion))
            {
                try
                {
                    await SetzeStatusTextVomStartupWindowUndWarte(splash, "Starte Update", DebugSleepTimer);
                    await StartUpdate(CurrentVersion);
                    await SetzeStatusTextVomStartupWindowUndWarte(splash, "Starte Anwendung", DebugSleepTimer);
                }
                catch
                {
                    await SetzeStatusTextVomStartupWindowUndWarte(splash, "Fehler beim Überprüfen auf Updates. Starte Anwendung ohne Update.", DebugSleepTimer);
                    await SetzeStatusTextVomStartupWindowUndWarte(splash, "Starte Anwendung...", DebugSleepTimer);
                }                
            }
            else
            {
                await SetzeStatusTextVomStartupWindowUndWarte(splash, "Keine Updates verfügbar.", DebugSleepTimer);
            }
            // 2. Status: Voraussetzungen prüfen (nur Info, Download im MainWindow)
            await SetzeStatusTextVomStartupWindowUndWarte(splash, "Prüfe Voraussetzungen (yt-dlp, ffmpeg, ffprobe)...", DebugSleepTimer);

            // Start des ToolUpdaters 
            if (await splash.ToolUpdater())
            {
                await SetzeStatusTextVomStartupWindowUndWarte(splash, "Alle Tools sind aktuell.", DebugSleepTimer);
            }
            else
            {
                await SetzeStatusTextVomStartupWindowUndWarte(splash, "Einige Tools fehlen oder sind veraltet. Bitte aktualisieren Sie die Tools manuell.", DebugSleepTimer);
                return; // Beende die Anwendung, wenn Tools fehlen
            }

            // 3. Splash schließen, MainWindow starten (dort werden Tools ggf. heruntergeladen)
            await SetzeStatusTextVomStartupWindowUndWarte(splash, "Starte Anwendung...", DebugSleepTimer);

            Thread.Sleep(DebugSleepTimer); // Kurze Pause für den Splashscreen

            // ... Komponenten prüfen, ggf. Dialoge anzeigen, Downloads abwarten

            var mainWindow = new Hauptfenster();
            MainWindow = mainWindow;
            mainWindow.Show();
            mainWindow.Activate();

            splash.Close(); // Splash explizit schließen

        }

        public string CurrentVersion { get => currentVersion; set => currentVersion = value; }

        public async Task StartUpdate(string currentVersion)
        {
            var updateService = new UpdateService();
            var (latestVersion, assetUrl) = await updateService.GetLatestReleaseInfoAsync();

            // ZIP-Datei im Temp-Ordner speichern
            string tempZipPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MortysDLP.zip");
            await updateService.DownloadAssetAsync(assetUrl, tempZipPath);

            // Updater starten und App beenden
            string updaterPath = System.IO.Path.Combine("Updater", "MortysDLP-Updater.exe");
            string mainExeName = "MortysDLP.exe";
            string arguments = $"\"{mainExeName}\" \"{tempZipPath}\"";
            System.Diagnostics.Process.Start(updaterPath, arguments);
            Shutdown();
            return;
        }

        public async Task<bool> UpdateAvailable(string currentVersion)
        {
            // Hier kannst du die Logik zum Überprüfen auf Software-Updates einfügen
            // Zum Beispiel: Überprüfen, ob eine neue Version verfügbar ist
            // Aktuell gibt es keine Implementierung, daher immer true zurückgeben
            if(currentVersion.Equals("latest"))
            {
                return false; // DEBUG: Nie Update verfügbar
            }

            UpdateService updateService = new UpdateService();
            var (latestVersion, assetUrl) = await updateService.GetLatestReleaseInfoAsync();

            if (latestVersion != null && assetUrl != null && updateService.IsNewerVersion(latestVersion, currentVersion))
            {
                return true;
            }
            return false;
        }

        public async Task SetzeStatusTextVomStartupWindowUndWarte(StartupWindow windowWithText, string statusText, int delay)
        {
            int skipdelay = 0;
            windowWithText.SetStatus(statusText);
            if(delay != skipdelay)
            {
                await Task.Delay(delay); // DEBUG
            }
        }
    }
}
