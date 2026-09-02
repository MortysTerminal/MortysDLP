using Microsoft.Win32;
using MortysDLP.Helpers;
using MortysDLP.Services;
using MortysDLP.UITexte;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace MortysDLP.Views
{
    public partial class GifPage : Page, ICancellableWork
    {
        bool ICancellableWork.IsBusy => btnCancel.IsEnabled;
        string ICancellableWork.BusyLabel => UITextDictionary.Get("ActiveWork.Label.Gif");
        void ICancellableWork.RequestCancel() => btnCancel_Click(this, new RoutedEventArgs());

        private CancellationTokenSource? _cts;
        private bool _initialized = false;
        private string _lastOutputDir = string.Empty;
        private string _currentOutputFile = string.Empty;

        // Quality preset: (fps, width, bayerScale)
        private static readonly (int Fps, int Width, int BayerScale)[] QualityPresets =
        {
            (15, 480, 3),  // Web/Discord (Index 0 = default)
            (8,  320, 2),  // Low
            (12, 480, 3),  // Medium
            (18, 640, 4),  // High
        };

        private readonly LogBuffer _log;

        public GifPage()
        {
            InitializeComponent();
            _log = new LogBuffer(tbDebugOutput);
            Loaded += GifPage_Loaded;
        }

        private void GifPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (_initialized)
            {
                SetUITexts();
                return;
            }
            _initialized = true;
            SetUITexts();
            tbOutputFolder.Text = Properties.Settings.Default.DownloadPath;
        }

        public void SetUITexts()
        {
            var T = UITextDictionary.Get;

            txtSectionInfo.Text     = T("GifPage.Section.Info");
            txtInfoText.Text        = T("GifPage.Info.Text");
            txtSectionInput.Text    = T("GifPage.Section.Input");
            lblInputFile.Content    = T("GifPage.Label.InputFile");
            txtBrowseInput.Text     = T("GifPage.Button.BrowseInput");
            tooltipInputFile.Content = T("GifPage.Tooltip.InputFile");
            txtSectionSettings.Text = T("GifPage.Section.Settings");
            lblQuality.Content      = T("GifPage.Label.Quality");
            lblStartTime.Content    = T("GifPage.Label.StartTime");
            lblEndTime.Content      = T("GifPage.Label.EndTime");
            tooltipQuality.Content  = T("GifPage.Tooltip.Quality");
            tooltipTime.Content     = T("GifPage.Tooltip.Time");
            cbTimeRange.Content     = T("GifPage.Label.TimeRange");

            cbiQualityWeb.Content    = T("GifPage.Quality.Web");
            cbiQualityLow.Content    = T("GifPage.Quality.Low");
            cbiQualityMedium.Content = T("GifPage.Quality.Medium");
            cbiQualityHigh.Content   = T("GifPage.Quality.High");

            txtSectionOutput.Text    = T("GifPage.Section.Output");
            lblOutputFolder.Content  = T("GifPage.Label.OutputFolder");
            txtBrowseOutput.Text     = T("GifPage.Button.BrowseOutput");
            txtOpenOutput.Text       = T("GifPage.Button.OpenOutput");
            txtUsePath.Text          = T("GifPage.Section.UsePath");
            txtUseDownloadPath.Text  = T("GifPage.Button.UseDownloadPath");
            txtBtnStart.Text         = T("GifPage.Button.Start");
            txtBtnCancel.Text        = T("GifPage.Button.Cancel");
            expDebug.Header          = T("DownloadPage.Section.Debug");

            ApplyDebugMode();
        }

        public void ApplyDebugMode()
        {
            borderDebug.Visibility = Properties.Settings.Default.DebugMode
                ? Visibility.Visible : Visibility.Collapsed;
        }

        // ─── File selection ────────────────────────────────────────────────────────

        private void btnBrowseInput_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Video-Dateien|*.mp4;*.mkv;*.mov;*.avi;*.webm;*.flv;*.wmv;*.m4v|Alle Dateien|*.*",
                Title  = UITextDictionary.Get("GifPage.Label.InputFile")
            };
            if (dlg.ShowDialog() == true)
                tbInputFile.Text = dlg.FileName;
        }

        private void borderInput_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void borderInput_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                tbInputFile.Text = files[0];
        }

        // ─── Output folder ─────────────────────────────────────────────────────────

        private void btnBrowseOutput_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFolderDialog
            {
                Title = UITextDictionary.Get("GifPage.Label.OutputFolder")
            };
            if (dlg.ShowDialog() == true)
                tbOutputFolder.Text = dlg.FolderName;
        }

        private void btnOpenOutput_Click(object sender, RoutedEventArgs e)
        {
            string folder = tbOutputFolder.Text;
            if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
                Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
        }

        private void btnUseDownloadPath_Click(object sender, RoutedEventArgs e)
        {
            tbOutputFolder.Text = Properties.Settings.Default.DownloadPath;
        }

        private void cbTimeRange_Check(object sender, RoutedEventArgs e)
        {
            bool enabled = cbTimeRange.IsChecked == true;
            tbStartTime.IsEnabled = enabled;
            tbEndTime.IsEnabled   = enabled;
            if (!enabled)
            {
                tbStartTime.Text = string.Empty;
                tbEndTime.Text   = string.Empty;
            }
        }

        // ─── GIF conversion

        private async void btnStart_Click(object sender, RoutedEventArgs e)
        {
            var T = UITextDictionary.Get;
            string inputFile  = tbInputFile.Text.Trim();
            string outputDir  = tbOutputFolder.Text.Trim();
            string ffmpegPath = AppPaths.Ffmpeg;

            if (string.IsNullOrWhiteSpace(inputFile) || !File.Exists(inputFile))
            {
                FluentMessageBox.Show(T("GifPage.Error.NoFile"), icon: MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(outputDir))
            {
                FluentMessageBox.Show(T("GifPage.Error.NoOutput"), icon: MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(ffmpegPath) || !File.Exists(ffmpegPath))
            {
                FluentMessageBox.Show(T("GifPage.Error.FfmpegMissing"), icon: MessageBoxImage.Error);
                return;
            }

            if (!Directory.Exists(outputDir))
            {
                try { Directory.CreateDirectory(outputDir); }
                catch (Exception ex)
                {
                    FluentMessageBox.Show(
                        string.Format(T("ConvertPage.Message.CannotCreateFolder"), ex.Message),
                        icon: MessageBoxImage.Error);
                    return;
                }
            }

            SetUiEnabled(false);
            btnCancel.IsEnabled     = true;
            pnlRunning.Visibility   = Visibility.Visible;
            pnlResult.Visibility    = Visibility.Collapsed;
            pbProgress.Value        = 0;
            _log.Clear();
            txtStatus.Text = T("GifPage.Status.Converting");

            _cts = new CancellationTokenSource();

            try
            {
                string outputFile = await CreateGifAsync(inputFile, outputDir, ffmpegPath, _cts.Token);

                pbProgress.Value = 100;
                ShowResult(true, outputDir);
            }
            catch (OperationCanceledException)
            {
                DeletePartialGif();
                ShowResult(null, string.Empty);
            }
            catch (Exception ex)
            {
                DeletePartialGif();
                AppendDebug($"[ERROR] {ex.Message}");
                ShowResult(false, string.Empty);
            }
            finally
            {
                SetUiEnabled(true);
                btnCancel.IsEnabled = false;
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
        }

        private void ShowResult(bool? success, string outputDir)
        {
            var T = UITextDictionary.Get;
            pnlRunning.Visibility = Visibility.Hidden;
            pnlResult.Visibility  = Visibility.Visible;

            if (success == true)
            {
                txtResultIcon.Text       = "\uE73E"; // Checkmark
                txtResultIcon.Foreground = System.Windows.Media.Brushes.LimeGreen;
                txtResultTitle.Text      = T("GifPage.Status.Success");
                _lastOutputDir           = outputDir;
                btnOpenResult.Visibility = Visibility.Visible;
            }
            else if (success == false)
            {
                txtResultIcon.Text       = "\uE783"; // Error badge
                txtResultIcon.Foreground = System.Windows.Media.Brushes.OrangeRed;
                txtResultTitle.Text      = T("GifPage.Status.Error");
                btnOpenResult.Visibility = Visibility.Collapsed;
            }
            else
            {
                txtResultIcon.Text       = "\uE711"; // Cancel
                txtResultIcon.Foreground = System.Windows.Media.Brushes.Gray;
                txtResultTitle.Text      = T("GifPage.Status.Canceled");
                btnOpenResult.Visibility = Visibility.Collapsed;
            }
        }

        private void btnOpenResult_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(_lastOutputDir) && Directory.Exists(_lastOutputDir))
                Process.Start(new ProcessStartInfo { FileName = _lastOutputDir, UseShellExecute = true });
        }

        private void DeletePartialGif()
        {
            string path = _currentOutputFile;
            _currentOutputFile = string.Empty;
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                try
                {
                    File.Delete(path);
                    AppendDebug($"[GIF] Unvollst\u00e4ndige Datei gel\u00f6scht: {path}");
                }
                catch (Exception ex)
                {
                    AppendDebug($"[GIF] Fehler beim L\u00f6schen: {ex.Message}");
                }
            }
        }

        private async Task<string> CreateGifAsync(
            string inputFile,
            string outputDir,
            string ffmpegPath,
            CancellationToken token)
        {
            int presetIndex = combQuality.SelectedIndex < 0 ? 0 : combQuality.SelectedIndex;
            var (fps, width, bayerScale) = QualityPresets[presetIndex];

            bool useTimeRange = cbTimeRange.IsChecked == true;
            string startTime = useTimeRange ? tbStartTime.Text.Trim() : string.Empty;
            string endTime   = useTimeRange ? tbEndTime.Text.Trim()   : string.Empty;

            string baseName  = AppPaths.SanitizeFileName(Path.GetFileNameWithoutExtension(inputFile));
            string outputFile = Path.Combine(outputDir, baseName + "_gifmaker.gif");

            // Ensure unique output filename
            int counter = 1;
            while (File.Exists(outputFile))
                outputFile = Path.Combine(outputDir, $"{baseName}_gifmaker_{counter++}.gif");

            // Build time args
            var args = new List<string>();
            if (!string.IsNullOrWhiteSpace(startTime))
            {
                args.Add("-ss");
                args.Add(startTime);
            }
            if (!string.IsNullOrWhiteSpace(endTime) && !string.IsNullOrWhiteSpace(startTime))
            {
                // Calculate duration from start to end
                if (TryParseTime(startTime, out var tsStart) && TryParseTime(endTime, out var tsEnd) && tsEnd > tsStart)
                {
                    double duration = (tsEnd - tsStart).TotalSeconds;
                    args.Add("-t");
                    args.Add(duration.ToString(CultureInfo.InvariantCulture));
                }
            }
            else if (!string.IsNullOrWhiteSpace(endTime) && string.IsNullOrWhiteSpace(startTime))
            {
                if (TryParseTime(endTime, out var tsEnd))
                {
                    args.Add("-t");
                    args.Add(tsEnd.TotalSeconds.ToString(CultureInfo.InvariantCulture));
                }
            }

            // Two-pass GIF in a single filter_complex command (palettegen + paletteuse piped)
            string filter = $"fps={fps},scale={width}:-1:flags=lanczos,split[s0][s1];" +
                           $"[s0]palettegen=max_colors=256:stats_mode=diff[p];" +
                           $"[s1][p]paletteuse=dither=bayer:bayer_scale={bayerScale}:diff_mode=rectangle";
            args.Add("-i");
            args.Add(inputFile);
            args.Add("-vf");
            args.Add(filter);
            args.Add("-loop");
            args.Add("0");
            args.Add("-y");
            args.Add(outputFile);

            AppendDebug($"[GIF] CMD: {ffmpegPath} {string.Join(' ', args)}");

            // Get duration for progress
            double totalSeconds = await GetMediaDurationAsync(ffmpegPath, inputFile, startTime, endTime, token);

            _currentOutputFile = outputFile;
            await RunFfmpegAsync(ffmpegPath, args, totalSeconds, token);
            _currentOutputFile = string.Empty;
            return outputFile;
        }

        private async Task<double> GetMediaDurationAsync(
            string ffmpegPath,
            string inputFile,
            string startTime,
            string endTime,
            CancellationToken token)
        {
            if (!string.IsNullOrWhiteSpace(startTime) && !string.IsNullOrWhiteSpace(endTime))
            {
                if (TryParseTime(startTime, out var s) && TryParseTime(endTime, out var e2) && e2 > s)
                    return (e2 - s).TotalSeconds;
            }

            string ffprobePath = AppPaths.Ffprobe;
            if (string.IsNullOrWhiteSpace(ffprobePath) || !File.Exists(ffprobePath))
                return 0;

            return await MediaProbe.GetDurationAsync(ffprobePath, inputFile, token) ?? 0;
        }

        private async Task RunFfmpegAsync(
            string ffmpegPath,
            List<string> arguments,
            double totalSeconds,
            CancellationToken token)
        {
            var result = await FfmpegRunner.RunAsync(
                ffmpegPath, arguments, totalSeconds,
                onStdErrLine: line => AppendDebug($"[ffmpeg] {line}"),
                onProgress: pct => Dispatcher.Invoke(() => pbProgress.Value = Math.Min(pct, 99)),
                token);

            AppendDebug($"[ffmpeg] Beendet mit Exit-Code: {result.ExitCode}");

            if (!result.Success)
                throw new InvalidOperationException($"ffmpeg beendet mit Exit-Code {result.ExitCode}");
        }

        // ─── Helpers ───────────────────────────────────────────────────────────────

        private static bool TryParseTime(string input, out TimeSpan result)
        {
            result = TimeSpan.Zero;
            if (string.IsNullOrWhiteSpace(input)) return false;
            if (TimeSpan.TryParseExact(input.Trim(), new[] { @"hh\:mm\:ss", @"mm\:ss", @"h\:mm\:ss" },
                CultureInfo.InvariantCulture, out result))
                return true;
            if (TimeSpan.TryParse(input.Trim(), CultureInfo.InvariantCulture, out result))
                return true;
            return false;
        }

        private void SetUiEnabled(bool enabled)
        {
            btnStart.IsEnabled           = enabled;
            tbInputFile.IsEnabled        = enabled;
            btnBrowseInput.IsEnabled     = enabled;
            combQuality.IsEnabled        = enabled;
            cbTimeRange.IsEnabled        = enabled;
            tbStartTime.IsEnabled        = enabled && cbTimeRange.IsChecked == true;
            tbEndTime.IsEnabled          = enabled && cbTimeRange.IsChecked == true;
            tbOutputFolder.IsEnabled     = enabled;
            btnBrowseOutput.IsEnabled    = enabled;
            btnUseDownloadPath.IsEnabled = enabled;
        }

        private void AppendDebug(string text) => _log.Append(text);

        /// <summary>Öffnet die GIF-Seite mit einer vorausgewählten Datei (z.B. nach dem Download).</summary>
        internal void SetInputFile(string filePath)
        {
            tbInputFile.Text = filePath;
            tbOutputFolder.Text = Path.GetDirectoryName(filePath) ?? Properties.Settings.Default.DownloadPath;
        }
    }
}
