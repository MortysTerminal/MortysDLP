using MortysDLP.Helpers;
using MortysDLP.Services;
using System.IO.Compression;
using System.Windows;
using System.Windows.Interop;

namespace MortysDLP
{
    /// <summary>
    /// Interaktionslogik für StartupWindow.xaml
    /// </summary>
    public partial class StartupWindow : Window
    {
        private readonly string FfmpegDownloadUrl = Properties.Resources.URL_FFMPEG;
        private readonly string YtDlpDocUrl = Properties.Resources.URL_YTDLP;

        private bool _toolUpdaterStarted = false;

        public StartupWindow()
        {
            LanguageHelper.ApplyLanguage(LanguageHelper.ForceEnglish);
            InitializeComponent();
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

                // Existenz prüfen
                bool ytDlpExists = ytDlpService.ToolExists(ytDlpPath);

                // yt-dlp: Download nur wenn nicht vorhanden
                if (!ytDlpExists)
                {
                    progress?.Report("Lade yt-dlp herunter...");
                    bool downloadSuccess = await CheckAndDownloadYtDlpAsync(ytDlpService, ytDlpPath, "yt-dlp", "yt-dlp.exe");
                    if (!downloadSuccess)
                        return false; // Abbruch, wenn Download fehlschlägt
                    ytDlpExists = true;
                }

                // Version prüfen und ggf. Update anbieten (nur wenn nicht gerade installiert)
                if (ytDlpExists)
                {
                    progress?.Report("Prüfe yt-dlp-Version...");
                    await CheckAndUpdateYtDlpVersionAsync(ytDlpService, ytDlpPath);
                }

                // ffmpeg/ffprobe: Download nur wenn nicht vorhanden
                bool ffmpegExists = ffmpegService.FfmpegExists(ffmpegPath);
                bool ffprobeExists = ffmpegService.FfprobeExists(ffprobePath);

                if (!ffmpegExists || !ffprobeExists)
                {
                    progress?.Report("Lade ffmpeg und ffprobe herunter...");
                    await CheckAndDownloadFfmpegAndFfprobeAsync(ffmpegService, ffmpegPath, ffprobePath);
                }

                // Existenz nach Download prüfen
                ytDlpExists = ytDlpService.ToolExists(ytDlpPath);
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

        private async Task DownloadToolWithProgressAsync(dynamic service, string assetUrl, string toolPath, string infoText)
        {
            using (DownloadProgressDialog dialog = new(infoText))
            {
                dialog.Owner = this;
                dialog.Show();
                var progress = new Progress<double>(value => dialog.SetProgress(value));
                string tempPath = toolPath + ".download";

                await service.DownloadAssetAsync(assetUrl, tempPath, progress);

                // Prüfe, ob es sich um eine ZIP handelt (ffmpeg/ffprobe)
                if (System.IO.Path.GetExtension(tempPath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    // Suche die gewünschte EXE im entpackten Ordner
                    string exeName = System.IO.Path.GetFileName(toolPath);
                    bool extractionSuccess = TryExtractExeFromZip(tempPath, exeName, toolPath);

                    if (!extractionSuccess)
                    {
                        MessageBox.Show($"{exeName} wurde im ZIP-Archiv nicht gefunden!", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    // Für yt-dlp: einfach umbenennen/verschieben
                    if (System.IO.File.Exists(toolPath))
                        System.IO.File.Delete(toolPath);
                    System.IO.File.Move(tempPath, toolPath);
                }
            }
        }

        private async Task<bool> CheckAndDownloadYtDlpAsync(dynamic service, string toolPath, string toolDisplayName, string toolFileName)
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
                await DownloadToolWithProgressAsync(service, assetUrl, toolPath, $"Lade {toolDisplayName} herunter...");
                MessageBox.Show($"{toolDisplayName} wurde erfolgreich heruntergeladen.", "Download abgeschlossen", MessageBoxButton.OK, MessageBoxImage.Information);
                return service.ToolExists(toolPath); // Existenz nach Download prüfen
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

        private async Task CheckAndDownloadFfmpegAndFfprobeAsync(dynamic service, string ffmpegPath, string ffprobePath)
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
                    var dialog = new DownloadProgressDialog("Lade ffmpeg & ffprobe herunter...");
                    dialog.Owner = this;
                    dialog.Show();

                    var progress = new Progress<double>(value => dialog.SetProgress(value));
                    string tempZip = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ffmpeg_download_" + System.Guid.NewGuid() + ".zip");
                    string assetUrl = FfmpegDownloadUrl;
                    await service.DownloadAssetAsync(assetUrl, tempZip, progress);

                    dialog.Close();

                    bool ffmpegSuccess = TryExtractExeFromZip(tempZip, "ffmpeg.exe", ffmpegPath);
                    bool ffprobeSuccess = TryExtractExeFromZip(tempZip, "ffprobe.exe", ffprobePath);

                    if (ffmpegSuccess && ffprobeSuccess)
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
            var releaseInfo = await ytDlpService.GetLatestReleaseInfoAsync();
            string? latestVersion = releaseInfo.Item1;
            string? assetUrl = releaseInfo.Item2;

            string? localVersion = ytDlpService.GetLocalVersion(ytDlpPath);

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
                    await DownloadToolWithProgressAsync(ytDlpService, assetUrl, ytDlpPath, "Aktualisiere yt-dlp...");
                    MessageBox.Show("yt-dlp wurde erfolgreich aktualisiert.", "Update abgeschlossen", MessageBoxButton.OK, MessageBoxImage.Information);
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
            finally
            {
                try { System.IO.File.Delete(zipPath); } catch { }
                try { System.IO.Directory.Delete(tempExtractDir, true); } catch { }
            }
        }

        private void ShowError(string message)
        {
            MessageBox.Show(message, "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
