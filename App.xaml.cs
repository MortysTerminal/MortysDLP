using MortysDLP.Helpers;
using MortysDLP.Models;
using MortysDLP.Properties;
using MortysDLP.Services;
using MortysDLP.Services.Releases;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
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
        /// <summary>
        /// Wenn beim Start ein Update gefunden wurde, wird hier die Info hinterlegt.
        /// Das MainWindow liest <c>Version</c>/<c>Changelog</c> für den Update-Banner.
        /// <c>Assets</c>/<c>Sha256</c>/<c>ExpectedSize</c> sind seit W2-T07 dabei, damit
        /// <see cref="StartUpdateCore"/> das richtige Asset wählen und den Download
        /// verifizieren kann, ohne die Update-Prüfung zu wiederholen. <c>AssetUrl</c> bleibt
        /// der Rückfallwert, wenn <c>Assets</c> leer ist (Atom-Feed, Weiterleitung,
        /// <c>version.json</c> kennen keine Asset-Liste).
        /// </summary>
        internal (string Version, string AssetUrl, string Changelog,
            IReadOnlyList<ReleaseAsset> Assets, string? Sha256, long? ExpectedSize)? PendingUpdateInfo
        { get; private set; }

        /* 
         * DEBUG
         * */
        private int DebugSleepTimer = 0; // 1000 = 1 Sekunde

        protected override async void OnStartup(StartupEventArgs e)
        {
            RegisterGlobalExceptionHandlers();

            // Ganz früh, vor dem ersten Lesen einer Einstellung: Seit die AssemblyVersion pro
            // Release wechselt (W2-T02), wechselt auch der user.config-Ordner. Ohne diese
            // Übernahme stünde der Nutzer nach jedem Update vor Standardeinstellungen.
            ApplySettingsUpgradeIfNeeded();

            LogEnvironmentInfo();

            try
            {
                // Nutzerverzeichnisse anlegen und ggf. vorhandenen Verlauf übernehmen -
                // muss vor jedem Zugriff auf AppPaths.HistoryFile passiert sein.
                AppPaths.EnsureDataDirs();

                /* Sprachanpassung bei Window-Start - MUSS VOR ALLEM ANDEREN PASSIEREN */
                LanguageHelper.ApplyLanguage();

                Log.Debug($"Language set to: {UITexte.UITextDictionary.CurrentLanguage}");
                Log.Debug($"CurrentUICulture: {System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName}");

                var splash = new StartupWindow();
                splash.Show();

                // Splash: Logo und Titel (optional)
                // splash.SetLogo("Assets/dein_logo.png");
                // splash.SetTitle("Dein Produktname");

                // 1. Status: Nach Software-Update suchen — höchstens alle 6 Stunden, sonst aus
                // dem Zwischenspeicher (W2-T06). Kein direkter GitHub-Zugriff mehr bei jedem Start.
                ResetVersionSkipIfObsolete();
                await SetStatusTextAndWaitAsync(splash, UITexte.UITexte.Splash_SearchingForUpdate, DebugSleepTimer);

                var updateCheckService = new UpdateCheckService(
                    new ResilientReleaseResolver(ReleaseSources.CreateAppChain()),
                    new UpdateCache(),
                    () => DateTimeOffset.UtcNow);

                var checkResult = await updateCheckService.CheckAppAsync(force: false, CancellationToken.None);

                if (ShouldOfferUpdate(checkResult))
                {
                    var info = checkResult.Info!;
                    PendingUpdateInfo = (info.Version.ToString(), info.DownloadUrl!, info.Changelog ?? string.Empty,
                        info.Assets, info.Sha256, info.ExpectedSize);
                }

                await SetStatusTextAndWaitAsync(splash, UITexte.UITexte.Splash_NoUpdate, DebugSleepTimer);

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
            catch (Exception ex)
            {
                Log.Error("Start fehlgeschlagen", ex);
                Views.ErrorDialog.Show(ex, fatal: true);
                Shutdown();
            }
        }

        /// <summary>Registriert die drei globalen Ausnahmebehandler. Muss ganz am Anfang von
        /// <see cref="OnStartup"/> passieren, bevor irgendetwas fehlschlagen kann.</summary>
        private void RegisterGlobalExceptionHandlers()
        {
            DispatcherUnhandledException += (s, e) =>
            {
                Log.Error("Unbehandelte Ausnahme im UI-Thread", e.Exception);
                Views.ErrorDialog.Show(e.Exception, fatal: false);
                e.Handled = true; // Anwendung läuft weiter
            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                // Hier ist die Anwendung nicht mehr zu retten: nur protokollieren, Puffer leeren.
                if (e.ExceptionObject is Exception ex)
                    Log.Error("Unbehandelte Ausnahme (Anwendung wird beendet)", ex);
                else
                    Log.Error($"Unbehandelte Ausnahme (Anwendung wird beendet): {e.ExceptionObject}");
                Log.Flush(TimeSpan.FromSeconds(2));
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                Log.Warn("Unbeobachtete Task-Ausnahme", e.Exception);
                e.SetObserved();
            };
        }

        private static void LogEnvironmentInfo()
        {
            Log.Info($"MortysDLP {AppInfo.Current ?? "unbekannt"} startet " +
                $"(Windows {Environment.OSVersion.Version}, .NET {Environment.Version})");
            Log.Info($"AppDir={AppPaths.AppDir}");
            Log.Info($"DataDir={AppPaths.DataDir}");
            Log.Info($"ToolsDir={AppPaths.ToolsDir}");

            var installInfo = InstallLocation.Analyze();
            Log.Info($"Installationsort: {InstallLocation.DescribeForLog(installInfo)}");
        }

        /// <summary>Übernimmt einmalig Einstellungen der vorherigen Version, sobald sich der
        /// user.config-Ordner mit der AssemblyVersion ändert (siehe
        /// <c>werkstatt/04-UPDATE-ARCHITEKTUR.md</c>, Abschnitt 11.1). Das Kennzeichen muss VOR
        /// dem Aufruf von <c>Upgrade()</c> gelesen werden, weil dieser es im Speicher mit dem
        /// Wert der Vorgängerversion überschreibt.</summary>
        private static void ApplySettingsUpgradeIfNeeded()
        {
            try
            {
                if (!Settings.Default.SettingsUpgradeRequired)
                    return;

                string? sourceDir = FindPreviousSettingsDirectory();

                Settings.Default.Upgrade();
                Settings.Default.SettingsUpgradeRequired = false;
                Settings.Default.Save();

                Log.Info(sourceDir != null
                    ? $"Einstellungen aus vorheriger Version übernommen: {sourceDir}"
                    : "Keine Einstellungen einer vorherigen Version gefunden - Standardwerte werden verwendet.");
            }
            catch (Exception ex)
            {
                // Eine defekte user.config darf den Start nicht verhindern - Standardwerte
                // gelten dann einfach weiter.
                Log.Warn("Einstellungen konnten nicht aus einer vorherigen Version übernommen werden", ex);
            }
        }

        /// <summary>Ermittelt best-effort das Verzeichnis, aus dem <c>Settings.Default.Upgrade()</c>
        /// vermutlich übernimmt - für die Protokollzeile, nicht für die Übernahme selbst (die
        /// erledigt .NET). Ohne diese Zeile ist ein „meine Einstellungen sind weg" später nicht
        /// zu klären.</summary>
        private static string? FindPreviousSettingsDirectory()
        {
            try
            {
                string? currentConfigPath = ConfigurationManager
                    .OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath;
                if (string.IsNullOrEmpty(currentConfigPath))
                    return null;

                string? versionDir = Path.GetDirectoryName(currentConfigPath);
                string? hashDir = versionDir != null ? Path.GetDirectoryName(versionDir) : null;
                if (versionDir == null || hashDir == null)
                    return null;

                if (!Version.TryParse(Path.GetFileName(versionDir), out var currentVersion))
                    return null;

                return SettingsUpgradeHelper.FindPreviousVersionDirectory(hashDir, currentVersion);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Trifft die ENTSCHEIDUNG, ob das Update angeboten wird — der Sachverhalt
        /// ("es gibt etwas Neueres") kommt bereits fertig aus <see cref="UpdateCheckService"/>.
        /// Die eigentliche Regel (inkl. <c>VersionSkip</c>) steckt in
        /// <see cref="UpdateDecision.ShouldOffer"/>; hier kommt nur noch die Prüfung auf eine
        /// tatsächlich nutzbare Download-Adresse dazu, da <see cref="UpdateCheckService"/>
        /// reine Versionsermittlung ist. Protokolliert immer, wenn ein vorhandenes Update wegen
        /// <c>VersionSkip</c> nicht angeboten wird — ohne diese Zeile ist später nicht
        /// erklärbar, warum kein Hinweis erscheint.</summary>
        private static bool ShouldOfferUpdate(UpdateCheckResult result)
        {
            if (result.Info?.DownloadUrl is null || AppInfo.CurrentVersion is not { } current)
                return false;

            bool offer = UpdateDecision.ShouldOffer(current, result.Info.Version, Settings.Default.VersionSkip);

            if (!offer && result.Info.Version > current)
                Log.Info($"Update {result.Info.Version} verfügbar, aber vom Nutzer übersprungen.");

            return offer;
        }

        /// <summary>Setzt <c>VersionSkip</c> zurück, sobald der Wert bedeutungslos geworden
        /// ist: Die übersprungene Version ist inzwischen installiert oder überholt. Ohne dieses
        /// Aufräumen bliebe ein alter Wert stehen, der nichts mehr über die Absicht des Nutzers
        /// aussagt. Best-Effort — ein Fehlschlag hier darf den Start nicht verhindern.</summary>
        private static void ResetVersionSkipIfObsolete()
        {
            try
            {
                string? skipped = Settings.Default.VersionSkip;
                if (string.IsNullOrWhiteSpace(skipped))
                    return;

                if (AppInfo.CurrentVersion is not { } current)
                    return;

                if (!AppVersion.TryParse(skipped, out var skippedVersion))
                    return;

                if (current >= skippedVersion)
                {
                    Settings.Default.VersionSkip = string.Empty;
                    Settings.Default.Save();
                    Log.Info($"VersionSkip zurückgesetzt (übersprungene Version {skippedVersion} " +
                        "ist installiert oder überholt).");
                }
            }
            catch (Exception ex)
            {
                Log.Warn("VersionSkip konnte nicht zurückgesetzt werden.", ex);
            }
        }

        /// <summary>Namensmuster für das App-Update-Paket. Platzhalterfrei ergibt es
        /// <c>"MortysDLP.zip"</c> — genau der Name, den <see cref="AssetSelector"/> bei
        /// mehreren Treffern bevorzugt (siehe dort).</summary>
        private const string MainAssetPattern = "MortysDLP*.zip";

        public async Task StartUpdate()
        {
            // PendingUpdateInfo nutzen statt die Update-Prüfung zu wiederholen.
            if (PendingUpdateInfo is not { } info || string.IsNullOrEmpty(info.AssetUrl))
            {
                // Fallback: kein PendingUpdateInfo vorhanden (z. B. MainWindow ohne
                // vorangegangenen Startpfad) - erneut über dieselbe Ausweichkette prüfen,
                // erzwungen statt aus dem Zwischenspeicher.
                var updateCheckService = new UpdateCheckService(
                    new ResilientReleaseResolver(ReleaseSources.CreateAppChain()),
                    new UpdateCache(),
                    () => DateTimeOffset.UtcNow);
                var result = await updateCheckService.CheckAppAsync(force: true, CancellationToken.None);

                if (result.Info?.DownloadUrl is not { } assetUrl)
                {
                    MessageBox.Show(
                        UITexte.UITexte.Error_UpdateNotAvailable,
                        UITexte.UITexte.Error,
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                await StartUpdateCore(assetUrl, result.Info.Assets, result.Info.Sha256, result.Info.ExpectedSize);
                return;
            }

            await StartUpdateCore(info.AssetUrl, info.Assets, info.Sha256, info.ExpectedSize);
        }

        /// <summary>Sucht das gemeinte Anhang in <paramref name="assets"/> (leer bei Quellen
        /// ohne Asset-Information — dann bleibt <paramref name="fallbackAssetUrl"/> gültig),
        /// beschafft dazu wenn möglich die Prüfsumme aus <c>checksums.txt</c> im Release und
        /// lädt erst danach herunter. <paramref name="knownSha256"/>/<paramref name="knownSize"/>
        /// kommen von <c>version.json</c>, falls diese Quelle geantwortet hat, und haben
        /// Vorrang vor <c>checksums.txt</c> nur, wenn beide vorhanden wären — praktisch
        /// schließen sie sich aus, da nur eine Quelle je Prüfung antwortet.</summary>
        private async Task StartUpdateCore(
            string fallbackAssetUrl, IReadOnlyList<ReleaseAsset> assets, string? knownSha256, long? knownSize)
        {
            try
            {
                string assetUrl = fallbackAssetUrl;
                string? expectedSha256 = knownSha256;
                long? expectedSize = knownSize;

                if (assets.Count > 0)
                {
                    ReleaseAsset? selected;
                    try
                    {
                        selected = AssetSelector.Select(assets, MainAssetPattern);
                    }
                    catch (AssetAmbiguousException ex)
                    {
                        string candidateNames = string.Join(", ", ex.CandidateNames);
                        Log.Error($"Mehrere passende Update-Pakete gefunden: {candidateNames}");
                        MessageBox.Show(
                            UITexte.UITextDictionary.Get("Update.Error.AssetAmbiguous")
                                .Replace("{0}", candidateNames, StringComparison.Ordinal),
                            UITexte.UITexte.Error, MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    if (selected is null)
                    {
                        MessageBox.Show(
                            UITexte.UITextDictionary.Get("Update.Error.AssetNotFound"),
                            UITexte.UITexte.Error, MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    assetUrl = selected.Url;
                    if (expectedSize is null && selected.Size > 0)
                        expectedSize = selected.Size;
                    if (string.IsNullOrEmpty(expectedSha256))
                        expectedSha256 = await TryFetchChecksumFromAssetsAsync(assets, selected.Name, CancellationToken.None);
                }

                if (string.IsNullOrEmpty(assetUrl))
                {
                    MessageBox.Show(
                        UITexte.UITexte.Error_UpdateNotAvailable,
                        UITexte.UITexte.Error, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 1. Sicheren Temp-Pfad ermitteln (mit Fallback-Verzeichnissen)
                string tempDir = UpdateService.GetSafeTempDirectory();
                string tempZipPath = Path.Combine(tempDir, Settings.Default.MortysDLPUpdateZipFile);

                // 2. Download mit Fortschrittsdialog, Retry, streamender Prüfsumme und
                // Größenabgleich (W2-T07/T08) - erst nach bestandener Prüfung trägt die Datei
                // ihren endgültigen Namen. using: der Dialog wird in jedem Fall geschlossen,
                // auch bei Erfolg (K-08 - nicht-modal - bleibt bewusst unangetastet).
                using (var dialog = new DownloadProgressDialog(UITexte.UITextDictionary.Get("Update.Download.InProgress")))
                {
                    dialog.Owner = MainWindow;
                    dialog.Show();
                    var progress = new Progress<double>(dialog.SetProgress);

                    try
                    {
                        var verification = await VerifiedDownload.ToFileAsync(
                            assetUrl, tempZipPath, expectedSha256, expectedSize, progress, dialog.CancellationToken);

                        if (!verification.ChecksumChecked)
                            Log.Warn(UITexte.UITextDictionary.Get("Update.Warning.NoChecksum"));
                    }
                    catch (OperationCanceledException)
                    {
                        // Vom Nutzer über den Dialog abgebrochen - kein Fehler. Ein liegen
                        // gebliebenes .part hat VerifiedDownload bereits selbst entfernt; die
                        // Installation wurde an keiner Stelle berührt.
                        Log.Info("Update-Download vom Nutzer abgebrochen.");
                        return;
                    }
                    catch (ChecksumMismatchException ex)
                    {
                        Log.Error($"Update-Prüfsumme stimmt nicht überein. Erwartet: {ex.Expected}, " +
                            $"tatsächlich: {ex.Actual}");
                        MessageBox.Show(
                            UITexte.UITextDictionary.Get("Update.Error.ChecksumMismatch"),
                            UITexte.UITexte.Error, MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                // 3. ZIP-Grundprüfung: enthält den erwarteten Haupteintrag, nicht nur
                // "irgendeine .exe" (Befund K-05)
                if (!UpdateService.ValidateZipContainsMainExe(tempZipPath, Settings.Default.MortysDLPExeFile))
                {
                    try { if (File.Exists(tempZipPath)) File.Delete(tempZipPath); } catch { }
                    MessageBox.Show(
                        UITexte.UITexte.Error_UpdateZipCorrupt,
                        UITexte.UITexte.Error,
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 4. Updater in Temp kopieren
                string sourceUpdaterDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Settings.Default.MortysDLPUpdaterBaseFolderName);
                if (!Directory.Exists(sourceUpdaterDir))
                {
                    MessageBox.Show(
                        UITexte.UITexte.Error_UpdaterNotFound,
                        UITexte.UITexte.Error,
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string tempUpdaterDir = Path.Combine(tempDir, Settings.Default.MortysDLPUpdaterFolderName);
                CopyDirectory(sourceUpdaterDir, tempUpdaterDir);

                // 5. Argumente: <MainExeName> <ZipPath> <TargetDir> <ProcessId>
                string mainExeName = Settings.Default.MortysDLPExeFile;
                int currentPid = Environment.ProcessId;
                string targetDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                List<string> arguments = [mainExeName, tempZipPath, targetDir, currentPid.ToString(System.Globalization.CultureInfo.InvariantCulture)];

                string updaterExePath = Path.Combine(tempUpdaterDir, Settings.Default.MortysDLPUpdateExeFile);
                Log.Info($"Starte Updater: {updaterExePath}");
                Log.Info($"Argumente: {string.Join(' ', arguments)}");

                // 6. Updater starten – UseShellExecute = true für unabhängigen Prozess, der MortysDLP
                // überlebt. Läuft deshalb bewusst außerhalb von ProcessRunner (das immer
                // UseShellExecute=false und Streams umleitet) — ArgumentList schließt trotzdem die
                // Argument-Einschleusung aus, auch wenn diese Werte nicht von außen kommen.
                var psi = new ProcessStartInfo { FileName = updaterExePath, UseShellExecute = true };
                foreach (string a in arguments) psi.ArgumentList.Add(a);
                var updaterProcess = Process.Start(psi);

                if (updaterProcess == null)
                {
                    MessageBox.Show(
                        "Der Updater-Prozess konnte nicht gestartet werden.",
                        UITexte.UITexte.Error,
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                Log.Info($"Updater gestartet (PID: {updaterProcess.Id}). Beende Hauptanwendung...");

                // 7. VersionSkip zurücksetzen - nach diesem Neustart ist ein übersprungener
                // Wert ohnehin überholt (siehe auch ResetVersionSkipIfObsolete für den
                // umgekehrten Fall: eine übersprungene Version, die inzwischen ohne dieses
                // Update installiert wurde, z. B. durch eine andere Quelle).
                try
                {
                    Settings.Default.VersionSkip = string.Empty;
                    Settings.Default.Save();
                }
                catch (Exception ex)
                {
                    Log.Warn("VersionSkip konnte nicht zurückgesetzt werden.", ex);
                }

                // 8. App sicher beenden – Shutdown + Environment.Exit als Fallback
                Shutdown();
                // Kurze Verzögerung, damit Shutdown-Events verarbeitet werden
                await Task.Delay(500);
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                Log.Error("Update fehlgeschlagen", ex);
                MessageBox.Show(
                    string.Format(UITexte.UITexte.Error_UpdateFailed, ex.Message),
                    UITexte.UITexte.Error,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>Sucht ein Asset namens <c>checksums.txt</c> im selben Release und liefert
        /// die darin für <paramref name="fileName"/> hinterlegte Prüfsumme — oder <c>null</c>,
        /// wenn es keine solche Datei gibt, sie nicht lesbar ist oder keinen Eintrag für die
        /// Datei enthält. Wirft nie: Ein Netzwerkfehler beim Lesen der Prüfsummendatei darf
        /// das Update nicht verhindern, nur die Prüfsumme unbekannt lassen.</summary>
        private static async Task<string?> TryFetchChecksumFromAssetsAsync(
            IReadOnlyList<ReleaseAsset> assets, string fileName, CancellationToken ct)
        {
            var checksumsAsset = assets.FirstOrDefault(a =>
                string.Equals(a.Name, "checksums.txt", StringComparison.OrdinalIgnoreCase));
            if (checksumsAsset is null)
                return null;

            try
            {
                UrlSafety.EnsureAllowed(new Uri(checksumsAsset.Url));

                using var response = await Http.SendWithRetryAsync(
                    Http.Shared, () => new HttpRequestMessage(HttpMethod.Get, checksumsAsset.Url), ct: ct);
                if (!response.IsSuccessStatusCode)
                    return null;

                string content = await response.Content.ReadAsStringAsync(ct);
                return ChecksumFile.Find(content, fileName);
            }
            catch (Exception ex)
            {
                Log.Warn("checksums.txt konnte nicht gelesen werden.", ex);
                return null;
            }
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
