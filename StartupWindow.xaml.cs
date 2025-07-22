using MortysDLP.Services;
using System;
using System.Collections.Generic;
using System.IO.Compression;
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
using System.Windows.Interop;

namespace MortysDLP
{
    /// <summary>
    /// Interaktionslogik für StartupWindow.xaml
    /// </summary>
    public partial class StartupWindow : Window
    {
        public StartupWindow()
        {
            InitializeComponent();
        }

        public void SetStatus(string text)
        {
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

        public async Task<bool> ToolUpdater()
        {
            var ytDlpService = new YtDlpUpdateService();
            var ffmpegService = new FfmpegUpdateService();

            string ytDlpPath = Properties.Settings.Default.YTDLPPATH;
            string ffmpegPath = Properties.Settings.Default.FFMPEGPATH;
            string ffprobePath = Properties.Settings.Default.FFPROBEPATH;

            await CheckAndUpdateToolAsync(
                ytDlpService,
                ytDlpPath,
                "yt-dlp",
                "yt-dlp.exe");

            await CheckAndDownloadFfmpegAndFfprobeAsync();

            // Existenz prüfen
            bool ytDlpExists = ytDlpService.ToolExists(ytDlpPath);
            bool ffmpegExists = ffmpegService.FfmpegExists(ffmpegPath);
            bool ffprobeExists = ffmpegService.FfprobeExists(ffprobePath);

            return ytDlpExists && ffmpegExists && ffprobeExists;

        }

        private async Task DownloadToolWithProgressAsync(dynamic service, string assetUrl, string toolPath, string infoText)
        {
            var dialog = new DownloadProgressDialog(infoText);
            dialog.Owner = this;
            dialog.Show();

            var progress = new Progress<double>(value => dialog.SetProgress(value));
            string tempPath = toolPath + ".download";

            await service.DownloadAssetAsync(assetUrl, tempPath, progress);

            dialog.Close();

            // Prüfe, ob es sich um eine ZIP handelt (ffmpeg/ffprobe)
            if (System.IO.Path.GetExtension(tempPath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                string tempExtractDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ffmpeg_extract_" + Guid.NewGuid());
                System.IO.Directory.CreateDirectory(tempExtractDir);

                try
                {
                    ZipFile.ExtractToDirectory(tempPath, tempExtractDir);

                    // Suche die gewünschte EXE im entpackten Ordner
                    string exeName = System.IO.Path.GetFileName(toolPath);
                    string? foundExe = System.IO.Directory.GetFiles(tempExtractDir, exeName, System.IO.SearchOption.AllDirectories).FirstOrDefault();

                    if (foundExe != null)
                    {
                        if (System.IO.File.Exists(toolPath))
                            System.IO.File.Delete(toolPath);
                        System.IO.File.Copy(foundExe, toolPath, true);
                    }
                    else
                    {
                        MessageBox.Show($"{exeName} wurde im ZIP-Archiv nicht gefunden!", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                finally
                {
                    // Aufräumen
                    try { System.IO.File.Delete(tempPath); } catch { }
                    try { System.IO.Directory.Delete(tempExtractDir, true); } catch { }
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

        private async Task CheckAndUpdateToolAsync(dynamic service, string toolPath, string toolDisplayName, string toolFileName)
        {
            var releaseInfo = await service.GetLatestReleaseInfoAsync();
            string? assetUrl = releaseInfo.Item2;

            bool exists = service.ToolExists(toolPath);

            if (!exists)
            {
                string message;
                string docUrl;

                if (toolDisplayName == "yt-dlp")
                {
                    message =
                        "Das Tool 'yt-dlp' ist erforderlich, um Videos und Audios von verschiedenen Plattformen herunterzuladen. " +
                        "Es handelt sich um ein Open-Source-Projekt, das als Nachfolger von youtube-dl gilt und regelmäßig aktualisiert wird.\n\n" +
                        "Ohne yt-dlp kann MortysDLP keine Downloads durchführen.\n\n" +
                        "Weitere Informationen findest du in der Dokumentation:\n" +
                        "https://github.com/yt-dlp/yt-dlp";
                    docUrl = "https://github.com/yt-dlp/yt-dlp";
                }
                else // Fallback
                {
                    message = "Das benötigte Tool wurde nicht gefunden.";
                    docUrl = "";
                }

                var result = ShowCenteredMessageBox(
                    message + "\n\nMöchtest du yt-dlp jetzt herunterladen?",
                    $"{toolDisplayName} fehlt",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes && assetUrl != null)
                {
                    await DownloadToolWithProgressAsync(service, assetUrl, toolPath, $"Lade {toolDisplayName} herunter...");
                    MessageBox.Show($"{toolDisplayName} wurde erfolgreich heruntergeladen.", "Download abgeschlossen", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else if (result == MessageBoxResult.No && !string.IsNullOrEmpty(docUrl))
                {
                    MessageBox.Show(
                    $"{toolDisplayName} ist zwingend erforderlich, damit die Software funktioniert.\n\n" +
                    $"Du kannst das Tool auch manuell installieren. Schaue dazu in die Dokumentation:\n" +
                    "https://github.com/MortysTerminal/MortysDLP",
                    $"{toolDisplayName} fehlt",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                    Application.Current.Shutdown();
                }
            }
        }

        private async Task CheckAndDownloadFfmpegAndFfprobeAsync()
        {
            string ffmpegPath = "Tools\\ffmpeg.exe";
            string ffprobePath = "Tools\\ffprobe.exe";
            var service = new FfmpegUpdateService();

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
                    string assetUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";
                    await service.DownloadAssetAsync(assetUrl, tempZip, progress);

                    dialog.Close();

                    string tempExtractDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ffmpeg_extract_" + System.Guid.NewGuid());
                    System.IO.Directory.CreateDirectory(tempExtractDir);

                    try
                    {
                        ZipFile.ExtractToDirectory(tempZip, tempExtractDir);

                        string? foundFfmpeg = System.IO.Directory.GetFiles(tempExtractDir, "ffmpeg.exe", System.IO.SearchOption.AllDirectories).FirstOrDefault();
                        string? foundFfprobe = System.IO.Directory.GetFiles(tempExtractDir, "ffprobe.exe", System.IO.SearchOption.AllDirectories).FirstOrDefault();

                        if (foundFfmpeg != null)
                        {
                            if (System.IO.File.Exists(ffmpegPath))
                                System.IO.File.Delete(ffmpegPath);
                            System.IO.File.Copy(foundFfmpeg, ffmpegPath, true);
                        }
                        if (foundFfprobe != null)
                        {
                            if (System.IO.File.Exists(ffprobePath))
                                System.IO.File.Delete(ffprobePath);
                            System.IO.File.Copy(foundFfprobe, ffprobePath, true);
                        }

                        if (foundFfmpeg != null && foundFfprobe != null)
                        {
                            MessageBox.Show("ffmpeg und ffprobe wurden erfolgreich heruntergeladen.", "Download abgeschlossen", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            MessageBox.Show("ffmpeg.exe oder ffprobe.exe wurde im ZIP-Archiv nicht gefunden - Oder heruntergeladene Datei fehlerhaft.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    finally
                    {
                        try { System.IO.File.Delete(tempZip); } catch { }
                        try { System.IO.Directory.Delete(tempExtractDir, true); } catch { }
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

        // Hilfsmethode zum Anzeigen einer zentrierten MessageBox relativ zu StartupWindow
        private MessageBoxResult ShowCenteredMessageBox(string message, string caption, MessageBoxButton buttons, MessageBoxImage icon)
        {
            this.Dispatcher.Invoke(() =>
            {
                this.Activate();
            });

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
    }
}
