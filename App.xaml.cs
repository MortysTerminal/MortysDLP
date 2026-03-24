using MortysDLP.Helpers;
using MortysDLP.Properties;
using MortysDLP.Services;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
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

        /// <summary>
        /// Wenn beim Start ein Update gefunden wurde, wird hier die Info hinterlegt.
        /// Das MainWindow liest dies und zeigt den Update-Banner an.
        /// </summary>
        public (string Version, string AssetUrl, string Changelog)? PendingUpdateInfo { get; private set; }

        /* 
         * DEBUG
         * */
        private int DebugSleepTimer = 0; // 1000 = 1 Sekunde

        protected override async void OnStartup(StartupEventArgs e)
        {
            /* Sprachanpassung bei Window-Start - MUSS VOR ALLEM ANDEREN PASSIEREN */
            LanguageHelper.ApplyLanguage();
            
            // Debug: Zeige welche Sprache geladen wurde
            System.Diagnostics.Debug.WriteLine($"[App] Language set to: {UITexte.UITextDictionary.CurrentLanguage}");
            System.Diagnostics.Debug.WriteLine($"[App] CurrentUICulture: {System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName}");

            var splash = new StartupWindow();
            splash.Show();

            // Splash: Logo und Titel (optional)
            // splash.SetLogo("Assets/dein_logo.png");
            // splash.SetTitle("Dein Produktname");

            // 1. Status: Nach Software-Update suchen
            await SetStatusTextAndWaitAsync(splash, UITexte.UITexte.Splash_SearchingForUpdate, DebugSleepTimer);

            using var updateService = new UpdateService();
            var (latestVersion, assetUrl, changelog) = await updateService.GetLatestReleaseInfoAsync();

            bool updateAvailable = !CurrentVersion.Equals(Settings.Default.VersionSkip)
                && latestVersion != null
                && assetUrl != null
                && updateService.IsNewerVersion(latestVersion, CurrentVersion);

            if (updateAvailable)
            {
                PendingUpdateInfo = (latestVersion!, assetUrl!, changelog ?? string.Empty);
                await SetStatusTextAndWaitAsync(splash, UITexte.UITexte.Splash_NoUpdate, DebugSleepTimer);
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
                splash.Close();
                Application.Current.Shutdown();
                return;
            }

            // 3. Splash schließen, MainWindow starten (dort werden Tools ggf. heruntergeladen)
            await SetStatusTextAndWaitAsync(splash, UITexte.UITexte.Splash_StartingApp, DebugSleepTimer);

            await Task.Delay(DebugSleepTimer); // Kurze Pause für den Splashscreen

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
            var (latestVersion, assetUrl, _) = await updateService.GetLatestReleaseInfoAsync();

            if (assetUrl is null) return;

            await StartUpdate(updateService, assetUrl);
        }

        private async Task StartUpdate(UpdateService updateService, string assetUrl)
        {
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
            var (latestVersion, assetUrl, _) = await updateService.GetLatestReleaseInfoAsync();

            if (latestVersion != null && assetUrl != null && updateService.IsNewerVersion(latestVersion, currentVersion))
            {
                return true;
            }
            return false;
        }

        private async Task SetStatusTextAndWaitAsync(StartupWindow windowWithText, string statusText, int delay)
        {
            windowWithText.SetStatus(statusText);
            if (delay > 0)
            {
                await Task.Delay(delay);
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

                    foreach (var file in Directory.EnumerateFiles(tempDir, "ffmpeg_download_*.zip", SearchOption.TopDirectoryOnly))
                    {
                        string name = Path.GetFileName(file);
                        if (FfmpegZipRegex().IsMatch(name))
                        {
                            TryDeleteFile(file);
                        }
                    }

                    foreach (var dir in Directory.EnumerateDirectories(tempDir, "extract_*", SearchOption.TopDirectoryOnly))
                    {
                        string name = Path.GetFileName(dir);
                        if (ExtractDirRegex().IsMatch(name))
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

        [GeneratedRegex(@"^ffmpeg_download_[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\.zip$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex FfmpegZipRegex();

        [GeneratedRegex(@"^extract_[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex ExtractDirRegex();

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
