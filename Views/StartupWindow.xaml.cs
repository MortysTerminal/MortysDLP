using MortysDLP.Helpers;
using MortysDLP.Services;
using System.IO.Compression;
using System.Windows;
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
            InitializeComponent();
            // Sprache wurde bereits in App.xaml.cs gesetzt - nicht nochmal aufrufen!
            // Debug: Zeige welche Sprache aktiv ist
            System.Diagnostics.Debug.WriteLine($"[StartupWindow] Language: {UITexte.UITextDictionary.CurrentLanguage}");
            SetUITexts();
            StartLoadingAnimation();
        }

        private void SetUITexts()
        {
            var T = UITexte.UITextDictionary.Get;
            TitleText.Text = T("StartupWindow.Title");
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
                var T = UITexte.UITextDictionary.Get;
                var ytDlpService = new YtDlpUpdateService();
                var ffmpegService = new FfmpegUpdateService();

                string ytDlpPath = Properties.Settings.Default.YtdlpPath;
                string ffmpegPath = Properties.Settings.Default.FfmpegPath;
                string ffprobePath = Properties.Settings.Default.FfprobePath;

                // yt-dlp: Existenz prüfen
                SetStatus(T("StartupWindow.Status.CheckingYtDlp"));
                bool ytDlpExists = ytDlpService.ToolExists(ytDlpPath);

                // yt-dlp: Download nur wenn nicht vorhanden
                if (!ytDlpExists)
                {
                    SetStatus(T("StartupWindow.Status.YtDlpNotFound"));
                    bool downloadSuccess = await CheckAndDownloadYtDlpAsync(ytDlpService, ytDlpPath, "yt-dlp", "yt-dlp.exe");
                    if (!downloadSuccess)
                        return false;
                    ytDlpExists = true;
                }

                // Version prüfen und ggf. Update anbieten (nur wenn nicht gerade installiert)
                if (ytDlpExists)
                {
                    SetStatus(T("StartupWindow.Status.CheckingYtDlpVersion"));
                    await CheckAndUpdateYtDlpVersionAsync(ytDlpService, ytDlpPath);
                }

                // ffmpeg/ffprobe: Existenz prüfen
                SetStatus(T("StartupWindow.Status.CheckingFfmpeg"));
                bool ffmpegExists = ffmpegService.FfmpegExists(ffmpegPath);
                bool ffprobeExists = ffmpegService.FfprobeExists(ffprobePath);

                if (!ffmpegExists || !ffprobeExists)
                {
                    SetStatus(T("StartupWindow.Status.FfmpegNotFound"));
                    await CheckAndDownloadFfmpegAndFfprobeAsync(ffmpegService, ffmpegPath, ffprobePath);
                }

                // Existenz nach Download prüfen
                ffmpegExists = ffmpegService.FfmpegExists(ffmpegPath);
                ffprobeExists = ffmpegService.FfprobeExists(ffprobePath);

                return ytDlpExists && ffmpegExists && ffprobeExists;
            }
            catch (Exception ex)
            {
                var T = UITexte.UITextDictionary.Get;
                Dispatcher.Invoke(() => FluentMessageBox.Show(
                    string.Format(T("StartupWindow.Error.ToolUpdate"), ex.Message),
                    T("StartupWindow.Title.Error"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error));
                return false;
            }
        }

        private async Task<bool> DownloadToolWithProgressAsync(IDownloadableToolService service, string assetUrl, string toolPath, string infoText)
        {
            var T = UITexte.UITextDictionary.Get;
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
                SetStatus(T("StartupWindow.Status.DownloadCanceled"));
                return false;
            }
            catch (Exception)
            {
                SetStatus(T("StartupWindow.Status.DownloadFailed"));
                return false;
            }
            finally
            {
                try { if (System.IO.File.Exists(tempPath)) System.IO.File.Delete(tempPath); } catch { }
            }
        }

        private async Task<bool> CheckAndDownloadYtDlpAsync(YtDlpUpdateService service, string toolPath, string toolDisplayName, string toolFileName)
        {
            var T = UITexte.UITextDictionary.Get;
            var releaseInfo = await service.GetLatestReleaseInfoAsync();
            string? assetUrl = releaseInfo.Item2;

            string message = string.Format(T("StartupWindow.YtDlp.Message"), YtDlpDocUrl);

            var result = FluentMessageBox.Show(
                message + T("StartupWindow.YtDlp.Question"),
                string.Format(T("StartupWindow.YtDlp.Title"), toolDisplayName),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                this);

            if (result == MessageBoxResult.Yes && assetUrl != null)
            {
                SetStatus(string.Format(T("StartupWindow.Status.Downloading"), toolDisplayName));
                bool downloadSuccess = await DownloadToolWithProgressAsync(service, assetUrl, toolPath, string.Format(T("StartupWindow.Status.Downloading"), toolDisplayName));
                if (!downloadSuccess)
                    return false;
                FluentMessageBox.Show(
                    string.Format(T("StartupWindow.YtDlp.Success"), toolDisplayName),
                    T("StartupWindow.Title.DownloadComplete"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information,
                    this);
                return service.ToolExists(toolPath);
            }
            else
            {
                FluentMessageBox.Show(
                    string.Format(T("StartupWindow.YtDlp.Required"), toolDisplayName, Properties.Settings.Default.MortysDLPGitHubURL),
                    string.Format(T("StartupWindow.YtDlp.Title"), toolDisplayName),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error,
                    this);

                Application.Current.Shutdown();
                return false;
            }
        }

        private async Task CheckAndDownloadFfmpegAndFfprobeAsync(FfmpegUpdateService service, string ffmpegPath, string ffprobePath)
        {
            var T = UITexte.UITextDictionary.Get;
            bool ffmpegExists = service.FfmpegExists(ffmpegPath);
            bool ffprobeExists = service.FfprobeExists(ffprobePath);

            if (!ffmpegExists || !ffprobeExists)
            {
                string message = T("StartupWindow.Ffmpeg.Message");

                var result = FluentMessageBox.Show(
                    message + T("StartupWindow.Ffmpeg.Question"),
                    string.Format(T("StartupWindow.YtDlp.Title"), "ffmpeg / ffprobe"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    this);

                if (result == MessageBoxResult.Yes)
                {
                    string tempZip = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ffmpeg_download_" + System.Guid.NewGuid() + ".zip");
                    try
                    {
                        using var dialog = new DownloadProgressDialog(T("StartupWindow.Ffmpeg.Downloading"));
                        dialog.Owner = this;
                        dialog.Show();
                        var progress = new Progress<double>(value => dialog.SetProgress(value));

                        SetStatus(T("StartupWindow.Ffmpeg.Downloading"));
                        await service.DownloadAssetAsync(FfmpegDownloadUrl, tempZip, progress, dialog.CancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        SetStatus(T("StartupWindow.Status.DownloadCanceled"));
                        return;
                    }

                    SetStatus(T("StartupWindow.Ffmpeg.Extracting"));
                    var (allSuccessful, failedFiles) = await TryExtractMultipleExeFromZipAsync(tempZip,
                        ("ffmpeg.exe", ffmpegPath),
                        ("ffprobe.exe", ffprobePath));

                    if (allSuccessful)
                    {
                        FluentMessageBox.Show(
                            T("StartupWindow.Ffmpeg.Success"),
                            T("StartupWindow.Title.DownloadComplete"),
                            MessageBoxButton.OK,
                            MessageBoxImage.Information,
                            this);
                    }
                    else
                    {
                        FluentMessageBox.Show(
                            T("StartupWindow.Ffmpeg.Failed"),
                            T("StartupWindow.Title.Error"),
                            MessageBoxButton.OK,
                            MessageBoxImage.Error,
                            this);
                    }
                }
                else
                {
                    FluentMessageBox.Show(
                        string.Format(T("StartupWindow.Ffmpeg.Required"), "https://github.com/MortysTerminal/MortysDLP"),
                        string.Format(T("StartupWindow.YtDlp.Title"), "ffmpeg / ffprobe"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Error,
                        this);

                    Application.Current.Shutdown();
                }
            }

        }

        private async Task CheckAndUpdateYtDlpVersionAsync(YtDlpUpdateService ytDlpService, string ytDlpPath)
        {
            var T = UITexte.UITextDictionary.Get;
            SetStatus(T("StartupWindow.Status.CheckingYtDlpVersion"));
            var releaseInfo = await ytDlpService.GetLatestReleaseInfoAsync();
            string? latestVersion = releaseInfo.Item1;
            string? assetUrl = releaseInfo.Item2;

            string? localVersion = await ytDlpService.GetLocalVersionAsync(ytDlpPath);

            if (ytDlpService.IsUpdateRequired(localVersion, latestVersion))
            {
                string message = string.Format(T("StartupWindow.YtDlpUpdate.NewVersion"), latestVersion, localVersion ?? "?");

                var result = FluentMessageBox.Show(
                    message + T("StartupWindow.YtDlpUpdate.Question"),
                    T("StartupWindow.YtDlpUpdate.Title"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information,
                    this);

                if (result == MessageBoxResult.Yes && assetUrl != null)
                {
                    SetStatus(string.Format(T("StartupWindow.Status.Downloading"), "yt-dlp"));
                    bool updateSuccess = await DownloadToolWithProgressAsync(ytDlpService, assetUrl, ytDlpPath, string.Format(T("StartupWindow.Status.Downloading"), "yt-dlp"));
                    if (updateSuccess)
                        FluentMessageBox.Show(
                            T("StartupWindow.YtDlpUpdate.Success"),
                            T("StartupWindow.Title.DownloadComplete"),
                            MessageBoxButton.OK,
                            MessageBoxImage.Information,
                            this);
                    else
                        FluentMessageBox.Show(
                            T("StartupWindow.YtDlpUpdate.Failed"),
                            T("StartupWindow.Title.Error"),
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning,
                            this);
                }
            }
        }
        private void ShowError(string message) =>
            FluentMessageBox.Show(message, icon: MessageBoxImage.Error, owner: this);

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
    }
}
