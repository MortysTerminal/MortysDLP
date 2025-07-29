using MortysDLP.Properties;
using MortysDLP.Services;
//using MortysDLP.UITexte;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;

namespace MortysDLP
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private string currentVersion = Settings.Default.CURRENTVERSION;

        /* 
         * DEBUG
         * */
        private int DebugSleepTimer = 0; // 1000 = 1 Sekunde


        protected override async void OnStartup(StartupEventArgs e)
        {

            /********************************************************************/
            /*
              Sprachanpassung bei Software-Start
            */
            // Debug: Sprache erzwingen
            bool forceEnglish = Settings.Default.FORCE_ENGLISH_LANGUAGE; ;
            SetLanguage(forceEnglish);

            /********************************************************************/


            var splash = new StartupWindow();
            splash.Show();

            // Splash: Logo und Titel (optional)
            // splash.SetLogo("Assets/dein_logo.png");
            // splash.SetTitle("Dein Produktname");

            // 1. Status: Nach Software-Update suchen
            await SetzeStatusTextVomStartupWindowUndWarte(splash, UITexte.UITexte.Splash_SearchingForUpdate, DebugSleepTimer);

            if (await UpdateAvailable(CurrentVersion))
            {
                try
                {
                    await SetzeStatusTextVomStartupWindowUndWarte(splash, UITexte.UITexte.Splash_StartingUpdate, DebugSleepTimer);
                    await StartUpdate(CurrentVersion);
                    await SetzeStatusTextVomStartupWindowUndWarte(splash, UITexte.UITexte.Splash_StartingApp, DebugSleepTimer);
                }
                catch
                {
                    await SetzeStatusTextVomStartupWindowUndWarte(splash, UITexte.UITexte.Splash_UpdateError, DebugSleepTimer);
                    await SetzeStatusTextVomStartupWindowUndWarte(splash, UITexte.UITexte.Splash_StartingApp, DebugSleepTimer);
                }                
            }
            else
            {
                await SetzeStatusTextVomStartupWindowUndWarte(splash, UITexte.UITexte.Splash_NoUpdate, DebugSleepTimer);
            }
            // 2. Status: Voraussetzungen prüfen (nur Info, Download im MainWindow)
            await SetzeStatusTextVomStartupWindowUndWarte(splash, UITexte.UITexte.Splash_CheckingTools, DebugSleepTimer);

            // Start des ToolUpdaters 
            if (await splash.ToolUpdater())
            {
                await SetzeStatusTextVomStartupWindowUndWarte(splash, UITexte.UITexte.Splash_AllToolsOk, DebugSleepTimer);
            }
            else
            {
                await SetzeStatusTextVomStartupWindowUndWarte(splash, UITexte.UITexte.Splash_ToolsMissing, DebugSleepTimer);
                return; // Beende die Anwendung, wenn Tools fehlen
            }

            // 3. Splash schließen, MainWindow starten (dort werden Tools ggf. heruntergeladen)
            await SetzeStatusTextVomStartupWindowUndWarte(splash, UITexte.UITexte.Splash_StartingApp, DebugSleepTimer);

            Thread.Sleep(DebugSleepTimer); // Kurze Pause für den Splashscreen

            // ... Komponenten prüfen, ggf. Dialoge anzeigen, Downloads abwarten

            var mainWindow = new Hauptfenster();
            MainWindow = mainWindow;
            mainWindow.Show();
            mainWindow.Activate();

            splash.Close(); // Splash explizit schließen

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

        public string CurrentVersion { get => currentVersion; set => currentVersion = value; }

        public async Task StartUpdate(string currentVersion)
        {
            var updateService = new UpdateService();
            var (latestVersion, assetUrl) = await updateService.GetLatestReleaseInfoAsync();

            // ZIP-Datei im Temp-Ordner speichern
            string tempZipPath = Path.Combine(Path.GetTempPath(), Settings.Default.MORTYSDLP_UPDATE_ZIP_FILE);
            await updateService.DownloadAssetAsync(assetUrl, tempZipPath);

            // Updater in ein temporäres Verzeichnis kopieren (rekursiv, inkl. aller Dateien und Unterordner)
            string sourceUpdaterDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Settings.Default.MORTYSDLP_UPDATER_BASE_FOLDERNAME);
            string tempUpdaterDir = Path.Combine(Path.GetTempPath(), Settings.Default.MORTYSDLP_UPDATER_FOLDERNAME);
            CopyDirectory(sourceUpdaterDir, tempUpdaterDir);

            // Argumente: <MainExeName> <ZipPath> <TargetDir>
            string mainExeName = Settings.Default.MORTYSDLP_EXE_FILE;
            string arguments = $"\"{mainExeName}\" \"{tempZipPath}\" \"{AppDomain.CurrentDomain.BaseDirectory}\"";

            // Updater starten und App beenden
            Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(tempUpdaterDir,Settings.Default.MORTYSDLP_UPDATE_EXE_FILE),
                Arguments = arguments,
                UseShellExecute = false
            });
            Shutdown();
        }

        public async Task<bool> UpdateAvailable(string currentVersion)
        {
            // Hier kannst du die Logik zum Überprüfen auf Software-Updates einfügen
            // Zum Beispiel: Überprüfen, ob eine neue Version verfügbar ist
            // Aktuell gibt es keine Implementierung, daher immer true zurückgeben
            if(currentVersion.Equals(Settings.Default.VERSIONSKIP))
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

        static void CopyDirectory(string sourceDir, string targetDir)
        {
            if (!Directory.Exists(sourceDir))
                throw new DirectoryNotFoundException(
                    string.Format(UITexte.UITexte.Error_DirectoryNotFound, sourceDir));

            Directory.CreateDirectory(targetDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(targetDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }
            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                string destDir = Path.Combine(targetDir, Path.GetFileName(dir));
                CopyDirectory(dir, destDir);
            }
        }
    }
}
