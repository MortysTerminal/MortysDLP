using MortysDLP.Helpers;
using MortysDLP.Properties;
using MortysDLP.Services;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace MortysDLP
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private string currentVersion = Settings.Default.CurrentVersion;

        /* 
         * DEBUG
         * */
        private int DebugSleepTimer = 0; // 1000 = 1 Sekunde

        protected override async void OnStartup(StartupEventArgs e)
        {
            /* Sprachanpassung bei Window-Start */
            LanguageHelper.ApplyLanguage(LanguageHelper.ForceEnglish);


            var splash = new StartupWindow();
            splash.Show();

            // Splash: Logo und Titel (optional)
            // splash.SetLogo("Assets/dein_logo.png");
            // splash.SetTitle("Dein Produktname");

            // 1. Status: Nach Software-Update suchen
            await SetStatusTextAndWaitAsync(splash, UITexte.UITexte.Splash_SearchingForUpdate, DebugSleepTimer);

            if (await UpdateAvailable(CurrentVersion))
            {
                try
                {
                    await SetStatusTextAndWaitAsync(splash, UITexte.UITexte.Splash_StartingUpdate, DebugSleepTimer);
                    await StartUpdate(CurrentVersion);
                    await SetStatusTextAndWaitAsync(splash, UITexte.UITexte.Splash_StartingApp, DebugSleepTimer);
                }
                catch
                {
                    await SetStatusTextAndWaitAsync(splash, UITexte.UITexte.Splash_UpdateError, DebugSleepTimer);
                    await SetStatusTextAndWaitAsync(splash, UITexte.UITexte.Splash_StartingApp, DebugSleepTimer);
                }
            }
            else
            {
                await SetStatusTextAndWaitAsync(splash, UITexte.UITexte.Splash_NoUpdate, DebugSleepTimer);
            }
            // 2. Status: Voraussetzungen prüfen (nur Info, Download im MainWindow)
            await SetStatusTextAndWaitAsync(splash, UITexte.UITexte.Splash_CheckingTools, DebugSleepTimer);

            // Start des ToolUpdaters 
            if (await splash.ToolUpdaterAsync())
            {
                await SetStatusTextAndWaitAsync(splash, UITexte.UITexte.Splash_AllToolsOk, DebugSleepTimer);
            }
            else
            {
                await SetStatusTextAndWaitAsync(splash, UITexte.UITexte.Splash_ToolsMissing, DebugSleepTimer);
                return; // Beende die Anwendung, wenn Tools fehlen
            }

            // 3. Splash schließen, MainWindow starten (dort werden Tools ggf. heruntergeladen)
            await SetStatusTextAndWaitAsync(splash, UITexte.UITexte.Splash_StartingApp, DebugSleepTimer);

            Thread.Sleep(DebugSleepTimer); // Kurze Pause für den Splashscreen

            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();
            mainWindow.Activate();

            // Aufräumen der temporären ffmpeg-/Entpack-Artefakte (nicht blockierend, Best-Effort)
            _ = CleanupTempArtifactsAsync();

            splash.Close(); // Splash explizit schließen
        }

        public string CurrentVersion { get => currentVersion; set => currentVersion = value; }

        public async Task StartUpdate(string currentVersion)
        {
            var updateService = new UpdateService();
            var (latestVersion, assetUrl) = await updateService.GetLatestReleaseInfoAsync();

            // ZIP-Datei im Temp-Ordner speichern
            string tempZipPath = Path.Combine(Path.GetTempPath(), Settings.Default.MortysDLPUpdateZipFile);
            await updateService.DownloadAssetAsync(assetUrl, tempZipPath);

            // Updater in ein temporäres Verzeichnis kopieren (rekursiv, inkl. aller Dateien und Unterordner)
            string sourceUpdaterDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Settings.Default.MortysDLPUpdaterBaseFolderName);
            string tempUpdaterDir = Path.Combine(Path.GetTempPath(), Settings.Default.MortysDLPUpdaterFolderName);
            CopyDirectory(sourceUpdaterDir, tempUpdaterDir);

            // Argumente: <MainExeName> <ZipPath> <TargetDir>
            string mainExeName = Settings.Default.MortysDLPExeFile;
            string arguments = $"\"{mainExeName}\" \"{tempZipPath}\" \"{AppDomain.CurrentDomain.BaseDirectory}\"";

            // Updater starten und App beenden
            Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(tempUpdaterDir, Settings.Default.MortysDLPUpdateExeFile),
                Arguments = arguments,
                UseShellExecute = false
            });
            Shutdown();
        }

        public async Task<bool> UpdateAvailable(string currentVersion)
        {
            if (currentVersion.Equals(Settings.Default.VersionSkip))
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

        public async Task SetStatusTextAndWaitAsync(StartupWindow windowWithText, string statusText, int delay)
        {
            int skipdelay = 0;
            windowWithText.SetStatus(statusText);
            if (delay != skipdelay)
            {
                await Task.Delay(delay); // DEBUG
            }
        }

        public static void CopyDirectory(string sourceDir, string targetDir)
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

        private static Task CleanupTempArtifactsAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    string tempDir = Path.GetTempPath();

                    // Exakt die im Code verwendeten Muster:
                    // - ffmpeg_download_{GUID}.zip
                    // - extract_{GUID}
                    string guidPattern = "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}";
                    var zipRegex = new Regex($"^ffmpeg_download_{guidPattern}\\.zip$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                    var dirRegex = new Regex($"^extract_{guidPattern}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

                    foreach (var file in Directory.EnumerateFiles(tempDir, "ffmpeg_download_*.zip", SearchOption.TopDirectoryOnly))
                    {
                        string name = Path.GetFileName(file);
                        if (zipRegex.IsMatch(name))
                        {
                            TryDeleteFile(file);
                        }
                    }

                    foreach (var dir in Directory.EnumerateDirectories(tempDir, "extract_*", SearchOption.TopDirectoryOnly))
                    {
                        string name = Path.GetFileName(dir);
                        if (dirRegex.IsMatch(name))
                        {
                            TryDeleteDirectory(dir);
                        }
                    }
                }
                catch
                {
                    // Best-effort: Keine Exception nach außen
                }
            });
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    try { File.SetAttributes(path, FileAttributes.Normal); } catch { }
                    File.Delete(path);
                }
            }
            catch { }
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    ClearReadOnlyAttributes(path);
                    Directory.Delete(path, true);
                }
            }
            catch { }
        }

        private static void ClearReadOnlyAttributes(string rootDir)
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(rootDir, "*", SearchOption.AllDirectories))
                {
                    try { File.SetAttributes(file, FileAttributes.Normal); } catch { }
                }
            }
            catch { }
        }
    }
}
