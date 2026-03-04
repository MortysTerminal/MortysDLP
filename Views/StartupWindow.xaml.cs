using MortysDLP.Helpers;
using MortysDLP.Services;
using System.IO.Compression;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MortysDLP
{
    /// <summary>
    /// Interaktionslogik für StartupWindow.xaml
    /// </summary>
    public partial class StartupWindow : Window
    {
        private readonly string FfmpegDownloadUrl = Properties.Resources.URL_FFMPEG;
        private readonly string YtDlpDocUrl = Properties.Resources.URL_YTDLP;

        public StartupWindow()
        {
            LanguageHelper.ApplyLanguage(LanguageHelper.ForceEnglish);
            InitializeComponent();
            StartLoadingAnimation();
        }

        private void StartLoadingAnimation()
        {
            var animation = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = TimeSpan.FromSeconds(2),
                RepeatBehavior = RepeatBehavior.Forever
            };
            LoadingRotation.BeginAnimation(RotateTransform.AngleProperty, animation);
        }

        public void SetStatus(string text)
        {
            if (!Dispatcher.CheckAccess())
                Dispatcher.Invoke(() => StatusText.Text = text);
            else
                StatusText.Text = text;
        }

        public void SetTitle(string text)
        {
            TitleText.Text = text;
        }

        public void SetLogo(string imagePath)
        {
            LogoImage.Source = new System.Windows.Media.Imaging.BitmapImage(new System.Uri(imagePath, System.UriKind.RelativeOrAbsolute));
        }

        public async Task<bool> ToolUpdaterAsync(IProgress<string>? progress = null)
        {
            try
            {
                var ytDlpService = new YtDlpUpdateService();
                var ffmpegService = new FfmpegUpdateService();

                string ytDlpPath = Properties.Settings.Default.YtdlpPath;
                string ffmpegPath = Properties.Settings.Default.FfmpegPath;
                string ffprobePath = Properties.Settings.Default.FfprobePath;

                // yt-dlp: Existenz prüfen
                SetStatus("Prüfe yt-dlp...");
                bool ytDlpExists = ytDlpService.ToolExists(ytDlpPath);

                // yt-dlp: Download nur wenn nicht vorhanden
                if (!ytDlpExists)
                {
                    SetStatus("yt-dlp nicht gefunden – starte Download...");
                    bool downloadSuccess = await CheckAndDownloadYtDlpAsync(ytDlpService, ytDlpPath, "yt-dlp", "yt-dlp.exe");
                    if (!downloadSuccess)
                        return false;
                    ytDlpExists = true;
                }

                // Version prüfen und ggf. Update anbieten (nur wenn nicht gerade installiert)
                if (ytDlpExists)
                {
                    SetStatus("Prüfe yt-dlp-Version...");
                    await CheckAndUpdateYtDlpVersionAsync(ytDlpService, ytDlpPath);
                }

                // ffmpeg/ffprobe: Existenz prüfen
                SetStatus("Prüfe ffmpeg / ffprobe...");
                bool ffmpegExists = ffmpegService.FfmpegExists(ffmpegPath);
                bool ffprobeExists = ffmpegService.FfprobeExists(ffprobePath);

                if (!ffmpegExists || !ffprobeExists)
                {
                    SetStatus("ffmpeg / ffprobe nicht gefunden – starte Download...");
                    await CheckAndDownloadFfmpegAndFfprobeAsync(ffmpegService, ffmpegPath, ffprobePath);
                }

                // Existenz nach Download prüfen
                ffmpegExists = ffmpegService.FfmpegExists(ffmpegPath);
                ffprobeExists = ffmpegService.FfprobeExists(ffprobePath);

                return ytDlpExists && ffmpegExists && ffprobeExists;
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => MessageBox.Show($"Fehler beim Aktualisieren der Tools:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error));
                return false;
            }
        }

        private async Task<bool> DownloadToolWithProgressAsync(IDownloadableToolService service, string assetUrl, string toolPath, string infoText)
        {
            string tempPath = toolPath + ".download";
            using var dialog = new DownloadProgressDialog(infoText);
            dialog.Owner = this;
            dialog.Show();
            var progressCallback = new Progress<double>(value => dialog.SetProgress(value));
            try
            {
                await service.DownloadAssetAsync(assetUrl, tempPath, progressCallback, dialog.CancellationToken);
                if (System.IO.File.Exists(toolPath))
                    System.IO.File.Delete(toolPath);
                System.IO.File.Move(tempPath, toolPath);
                return true;
            }
            catch (OperationCanceledException)
            {
                SetStatus("Download abgebrochen.");
                return false;
            }
            catch (Exception)
            {
                SetStatus("Download fehlgeschlagen.");
                return false;
            }
            finally
            {
                try { if (System.IO.File.Exists(tempPath)) System.IO.File.Delete(tempPath); } catch { }
            }
        }

        private async Task<bool> CheckAndDownloadYtDlpAsync(YtDlpUpdateService service, string toolPath, string toolDisplayName, string toolFileName)
        {
            var releaseInfo = await service.GetLatestReleaseInfoAsync();
            string? assetUrl = releaseInfo.Item2;

            string message =
                "Das Tool 'yt-dlp' ist erforderlich, um Videos und Audios von verschiedenen Plattformen herunterzuladen. " +
                "Es handelt sich um ein Open-Source-Projekt, das als Nachfolger von youtube-dl gilt und regelmäßig aktualisiert wird.\n\n" +
                "Ohne yt-dlp kann MortysDLP keine Downloads durchführen.\n\n" +
                "Weitere Informationen findest du in der Dokumentation:\n" +
                YtDlpDocUrl;

            var result = ShowCenteredMessageBox(
                message + "\n\nMöchtest du yt-dlp jetzt herunterladen?",
                $"{toolDisplayName} fehlt",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes && assetUrl != null)
            {
                SetStatus($"Lade {toolDisplayName} herunter...");
                bool downloadSuccess = await DownloadToolWithProgressAsync(service, assetUrl, toolPath, $"Lade {toolDisplayName} herunter...");
                if (!downloadSuccess)
                    return false;
                MessageBox.Show($"{toolDisplayName} wurde erfolgreich heruntergeladen.", "Download abgeschlossen", MessageBoxButton.OK, MessageBoxImage.Information);
                return service.ToolExists(toolPath);
            }
            else
            {
                MessageBox.Show(
                    $"{toolDisplayName} ist zwingend erforderlich, damit die Software funktioniert.\n\n" +
                    $"Du kannst das Tool auch manuell installieren. Schaue dazu in die Dokumentation:\n" +
                    Properties.Settings.Default.MortysDLPGitHubURL,
                    $"{toolDisplayName} fehlt",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Application.Current.Shutdown();
                return false;
            }
        }

        private async Task CheckAndDownloadFfmpegAndFfprobeAsync(FfmpegUpdateService service, string ffmpegPath, string ffprobePath)
        {
            bool ffmpegExists = service.FfmpegExists(ffmpegPath);
            bool ffprobeExists = service.FfprobeExists(ffprobePath);

            if (!ffmpegExists || !ffprobeExists)
            {
                string message =
                    "Die Tools 'ffmpeg' und 'ffprobe' sind notwendig, um Mediendateien zu verarbeiten, zu analysieren und zu konvertieren. " +
                    "Ohne diese Tools kann MortysDLP keine Audio-/Video-Konvertierung oder Metadatenanalyse durchführen.\n\n" +
                    "Weitere Informationen findest du in der Dokumentation:\n" +
                    "https://ffmpeg.org/documentation.html\n" +
                    "https://ffmpeg.org/ffprobe.html";

                var result = MessageBox.Show(
                    message + "\n\nMöchtest du ffmpeg und ffprobe jetzt herunterladen?",
                    "Tools fehlen",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    string tempZip = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ffmpeg_download_" + System.Guid.NewGuid() + ".zip");
                    try
                    {
                        using var dialog = new DownloadProgressDialog("Lade ffmpeg & ffprobe herunter...");
                        dialog.Owner = this;
                        dialog.Show();
                        var progress = new Progress<double>(value => dialog.SetProgress(value));

                        SetStatus("Lade ffmpeg & ffprobe herunter...");
                        await service.DownloadAssetAsync(FfmpegDownloadUrl, tempZip, progress, dialog.CancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        SetStatus("Download abgebrochen.");
                        return;
                    }

                    SetStatus("Entpacke ffmpeg & ffprobe...");
                    var (allSuccessful, failedFiles) = await TryExtractMultipleExeFromZipAsync(tempZip,
                        ("ffmpeg.exe", ffmpegPath),
                        ("ffprobe.exe", ffprobePath));

                    if (allSuccessful)
                    {
                        MessageBox.Show("ffmpeg und ffprobe wurden erfolgreich heruntergeladen.", "Download abgeschlossen", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("ffmpeg.exe oder ffprobe.exe wurde im ZIP-Archiv nicht gefunden - Oder heruntergeladene Datei fehlerhaft.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    MessageBox.Show(
                        "ffmpeg und ffprobe sind zwingend erforderlich, damit die Software funktioniert.\n\n" +
                        "Du kannst die Tools auch manuell installieren. Schaue dazu in die Dokumentation:\n" +
                        "https://github.com/MortysTerminal/MortysDLP",
                        "Tools fehlen",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    Application.Current.Shutdown();
                }
            }

        }

        private async Task CheckAndUpdateYtDlpVersionAsync(YtDlpUpdateService ytDlpService, string ytDlpPath)
        {
            SetStatus("Suche neueste yt-dlp-Version...");
            var releaseInfo = await ytDlpService.GetLatestReleaseInfoAsync();
            string? latestVersion = releaseInfo.Item1;
            string? assetUrl = releaseInfo.Item2;

            SetStatus("Lese lokale yt-dlp-Version...");
            string? localVersion = await ytDlpService.GetLocalVersionAsync(ytDlpPath);

            if (ytDlpService.IsUpdateRequired(localVersion, latestVersion))
            {
                string message =
                    $"Es ist eine neue Version von yt-dlp verfügbar!\n\n" +
                    $"Installiert: {localVersion ?? "Nicht gefunden"}\n" +
                    $"Neueste Version: {latestVersion}\n\n" +
                    "Die Software funktioniert auch mit der aktuellen Version, jedoch können Fehler auftreten.\n" +
                    "Es wird empfohlen, das Update durchzuführen.\n\n" +
                    "Möchtest du yt-dlp jetzt aktualisieren?";

                var result = ShowCenteredMessageBox(
                    message,
                    "yt-dlp Update verfügbar",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes && assetUrl != null)
                {
                    SetStatus("Aktualisiere yt-dlp...");
                    bool updateSuccess = await DownloadToolWithProgressAsync(ytDlpService, assetUrl, ytDlpPath, "Aktualisiere yt-dlp...");
                    if (updateSuccess)
                        MessageBox.Show("yt-dlp wurde erfolgreich aktualisiert.", "Update abgeschlossen", MessageBoxButton.OK, MessageBoxImage.Information);
                    else
                        MessageBox.Show("Update wurde abgebrochen. Die vorhandene Version wird weiter verwendet.", "Update übersprungen", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    MessageBox.Show(
                        "Du kannst yt-dlp auch später aktualisieren. Beachte, dass ältere Versionen zu Problemen führen können.",
                        "Update übersprungen",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
        }
        // Hilfsmethode zum Anzeigen einer zentrierten MessageBox relativ zu StartupWindow
        private MessageBoxResult ShowCenteredMessageBox(string message, string caption, MessageBoxButton buttons, MessageBoxImage icon)
        {
            if (!Dispatcher.CheckAccess())
            {
                // Falls nicht im UI-Thread, Dispatcher verwenden
                return Dispatcher.Invoke(() => ShowCenteredMessageBox(message, caption, buttons, icon));
            }

            this.Activate();

            WindowInteropHelper helper = new(this);
            System.Windows.Forms.IWin32Window win32Window = new Win32Window(helper.Handle);

            return System.Windows.Forms.MessageBox.Show(
                win32Window,
                message,
                caption,
                (System.Windows.Forms.MessageBoxButtons)buttons,
                (System.Windows.Forms.MessageBoxIcon)icon
            ) switch
            {
                System.Windows.Forms.DialogResult.Yes => MessageBoxResult.Yes,
                System.Windows.Forms.DialogResult.No => MessageBoxResult.No,
                System.Windows.Forms.DialogResult.OK => MessageBoxResult.OK,
                _ => MessageBoxResult.None
            };
        }

        // Hilfsklasse für Win32-Handle
        private class Win32Window : System.Windows.Forms.IWin32Window
        {
            private readonly IntPtr _handle;
            public Win32Window(IntPtr handle) { _handle = handle; }
            public IntPtr Handle => _handle;
        }

        private Task<bool> TryExtractExeFromZipAsync(string zipPath, string exeName, string targetPath)
        {
            return Task.Run(() => TryExtractExeFromZip(zipPath, exeName, targetPath));
        }

        private Task<(bool AllSuccessful, List<string> FailedFiles)> TryExtractMultipleExeFromZipAsync(string zipPath, params (string ExeName, string TargetPath)[] files)
        {
            return Task.Run(() => TryExtractMultipleExeFromZip(zipPath, files));
        }

        private bool TryExtractExeFromZip(string zipPath, string exeName, string targetPath)
        {
            string tempExtractDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "extract_" + Guid.NewGuid());
            System.IO.Directory.CreateDirectory(tempExtractDir);
            try
            {
                ZipFile.ExtractToDirectory(zipPath, tempExtractDir);
                string? foundExe = System.IO.Directory.GetFiles(tempExtractDir, exeName, System.IO.SearchOption.AllDirectories).FirstOrDefault();
                if (foundExe != null)
                {
                    if (System.IO.File.Exists(targetPath))
                        System.IO.File.Delete(targetPath);
                    System.IO.File.Copy(foundExe, targetPath, true);
                    return true;
                }
                return false;
            }
            catch {
                return false;
            }
            finally
            {
                try 
                { 
                    if (System.IO.Directory.Exists(tempExtractDir))
                        System.IO.Directory.Delete(tempExtractDir, true); 
                } 
                catch { }
            }
        }

        private (bool AllSuccessful, List<string> FailedFiles) TryExtractMultipleExeFromZip(string zipPath, params (string ExeName, string TargetPath)[] files)
        {
            string tempExtractDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "extract_" + Guid.NewGuid());
            var failedFiles = new List<string>();
            
            try
            {
                System.IO.Directory.CreateDirectory(tempExtractDir);
                ZipFile.ExtractToDirectory(zipPath, tempExtractDir);

                foreach (var (exeName, targetPath) in files)
                {
                    string? foundExe = System.IO.Directory.GetFiles(tempExtractDir, exeName, System.IO.SearchOption.AllDirectories).FirstOrDefault();
                    if (foundExe != null)
                    {
                        try
                        {
                            if (System.IO.File.Exists(targetPath))
                                System.IO.File.Delete(targetPath);
                            System.IO.File.Copy(foundExe, targetPath, true);
                        }
                        catch
                        {
                            failedFiles.Add(exeName);
                        }
                    }
                    else
                    {
                        failedFiles.Add(exeName);
                    }
                }

                return (failedFiles.Count == 0, failedFiles);
            }
            catch
            {
                failedFiles.AddRange(files.Select(f => f.ExeName));
                return (false, failedFiles);
            }
            finally
            {
                try 
                { 
                    if (System.IO.File.Exists(zipPath))
                        System.IO.File.Delete(zipPath); 
                } 
                catch { }
                
                try 
                { 
                    if (System.IO.Directory.Exists(tempExtractDir))
                        System.IO.Directory.Delete(tempExtractDir, true); 
                } 
                catch { }
            }
        }

        private void ShowError(string message)
        {
            MessageBox.Show(message, "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
