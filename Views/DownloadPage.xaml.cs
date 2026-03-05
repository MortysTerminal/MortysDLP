using Microsoft.Win32;
using MortysDLP.Helpers;
using MortysDLP.Models;
using MortysDLP.Services;
using MortysDLP.UITexte;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;

namespace MortysDLP.Views
{
    public partial class DownloadPage : Page
    {
        private CancellationTokenSource? _downloadCancellationTokenSource;
        private Task? _downloadTask;
        private string _lastDownloadPath = "";
        private double _lastProgress = 0;
        private Process? _ytDlpProcess;
        private bool _initialized = false;

        public enum iaStatusIconType { None, Loading, Success, Error }

        public DownloadPage()
        {
            InitializeComponent();
            Loaded += DownloadPage_Loaded;
        }

        private void DownloadPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (_initialized) 
            {
                // Bei erneutem Laden (z.B. nach Sprachumschaltung) nur UI-Texte aktualisieren
                SetUITexts();
                return;
            }
            _initialized = true;

            SetUITexts();
            SettingsLoad();
            AudioOnlyAdjustments();
            VideoformatAdjustments();
            TimespanAdjustments();
            FirstSecondsAdjustments();
            CustomFilenameAdjustments();
            ValidateDownloadButton();
            ApplyDebugMode();
        }

        internal void RefreshPaths()
        {
            lblDownloadPath.Content = Properties.Settings.Default.DownloadPath;
            lblAudioPath.Content = Properties.Settings.Default.DownloadAudioOnlyPath;
        }

        internal void SetDownloadUrl(string url)
        {
            tbURL.Text = url;
            ValidateDownloadButton();
        }

        internal void SetUiAudioEnabled(bool enabled)
        {
            dpAudioPath.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        }

        public void ApplyDebugMode()
        {
            expDebug.Visibility = Properties.Settings.Default.DebugMode ? Visibility.Visible : Visibility.Collapsed;
        }

        public void SetUITexts()
        {
            var T = UITextDictionary.Get;
            
            // Section: Download Paths
            txtSectionDownloadPaths.Text = T("DownloadPage.Section.DownloadPaths");
            lblDownloadPathInfo.Content = T("DownloadPage.Label.DownloadPath");
            lblAudioPathInfo.Content = T("DownloadPage.Label.AudioOnlyPath");
            
            // Section: Download
            txtSectionDownload.Text = T("DownloadPage.Section.Download");
            lblURLInfo.Content = T("DownloadPage.Label.EnterURL");
            btnHistory.Content = T("DownloadPage.Button.History");
            txtCustomFilenameInfo.Text = T("DownloadPage.Label.CustomFilename");
            tbCustomFilename.ToolTip = T("DownloadPage.Tooltip.CustomFilename");
            
            // Section: Options
            txtSectionOptions.Text = T("DownloadPage.Section.Options");
            txtTimespanFrom.Text = T("DownloadPage.Label.Timespan");
            txtTimespanDash.Text = "-";
            txtTimespanInfo.Text = T("DownloadPage.Label.TimespanFormat");
            ToolTipTimeSpan.Content = T("DownloadPage.Tooltip.Timespan");
            
            txtFirstSecondsInfo1.Text = T("DownloadPage.Label.FirstSeconds");
            txtFirstSecondsInfo2.Text = T("DownloadPage.Label.Seconds");
            
            var videoFormatText = T("DownloadPage.Label.VideoFormat");
            txtVideoformatInfo1.Text = videoFormatText.Split('(')[0].Trim();
            txtVideoformatInfo2.Text = "(" + videoFormatText.Split('(')[1];
            txtVideoformatInfo3.Text = T("DownloadPage.Label.VideoFormatInfo");
            
            txtAudioOnlyInfo.Text = T("DownloadPage.Label.AudioOnly");
            txtBitrateLabel.Text = T("DownloadPage.Label.Bitrate");
            txtVideoQuality.Text = T("DownloadPage.Label.VideoQuality");
            txtVideoContainer.Text = T("DownloadPage.Label.VideoContainer");
            
            // Quality Options
            cbiAudioBitrateHighest.Content = T("DownloadPage.Quality.Highest");
            cbiVideoQualityHighest.Content = T("DownloadPage.Quality.Highest");
            
            // Buttons
            btnDownloadStart.Content = T("DownloadPage.Button.DownloadStart");
            btnDownloadCancel.Content = T("DownloadPage.Button.DownloadCancel");
            btnSaveSettings.Content = T("DownloadPage.Button.SaveSettings");
            
            // Status
            txtDownloadStatus.Text = T("DownloadPage.Status.Loading");
            
            // Debug
            expDebug.Header = T("DownloadPage.Section.Debug");
        }

        private static async Task AddDownloadToHistoryAsync(string url, string title, string downloadDirectory,
            bool isAudioOnly, string? videoQuality, string? videoFormat, string? audioFormat, string? audioBitrate)
        {
            await DownloadHistoryService.AddAsync(new DownloadHistoryEntry
            {
                Url = url,
                Title = title.Trim(),
                DownloadDirectory = downloadDirectory,
                DownloadedAt = DateTime.Now,
                IsAudioOnly = isAudioOnly,
                VideoQuality = videoQuality,
                VideoFormat = videoFormat,
                AudioFormat = audioFormat,
                AudioBitrate = audioBitrate
            });
        }

        private void AppendOutput(string text)
        {
            Dispatcher.Invoke(() =>
            {
                tbDebugOutput.AppendText($"{text}{Environment.NewLine}");
                tbDebugOutput.ScrollToEnd();
            });
        }

        private void AudioOnlyAdjustments()
        {
            bool a = cbAudioOnly.IsChecked == true;

            txtAudioOnlyInfo.IsEnabled = a;
            combAudioFormat.IsEnabled = a;

            cbVideoformat.IsEnabled = !a;
            combAudioBitrate.IsEnabled = a;
            combVideoQuality.IsEnabled = !a;
            combVideoFormat.IsEnabled = !a && (cbVideoformat.IsChecked != true);
        }

        private void btnDownloadCancel_Click(object sender, RoutedEventArgs e)
        {
            txtDownloadStatus.Text = UITextDictionary.Get("DownloadPage.Status.Canceling");
            _downloadCancellationTokenSource?.Cancel();
            _ytDlpProcess?.Kill(true);
            UpdateProgress(0);
        }

        private async Task FetchTitleAndAddHistoryAsync(string ytDlpPath, string url, CancellationToken token)
        {
            try
            {
                string title = await GetVideoTitleAsync(ytDlpPath, url, token);
                if (token.IsCancellationRequested) return;

                bool isAudioOnly = false;
                string? vq = null;
                string? vf = null;
                string? af = null;
                string? ab = null;

                Dispatcher.Invoke(() =>
                {
                    isAudioOnly = cbAudioOnly.IsChecked == true;
                    vq = GetSelectedVideoQualityLabel();
                    vf = GetSelectedVideoFormat();
                    af = GetSelectedAudioFormat();
                    ab = GetSelectedAudioBitrate();
                });

                await AddDownloadToHistoryAsync(url, title, "", isAudioOnly, vq, vf, af, ab);
                AppendOutput($"[TITLE] Gespeichert: {title} | Modus={(isAudioOnly ? "Audio" : "Video")} | VQ={vq ?? "-"} | VF={vf ?? "-"} | AF={af ?? "-"} | AB={ab ?? "-"}");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                AppendOutput($"[TITLE] Fehler ({ex.Message}) – verwende Platzhalter");
                try
                {
                    bool isAudioOnly = false;
                    string? vq = null;
                    string? vf = null;
                    string? af = null;
                    string? ab = null;

                    Dispatcher.Invoke(() =>
                    {
                        isAudioOnly = cbAudioOnly.IsChecked == true;
                        vq = GetSelectedVideoQualityLabel();
                        vf = GetSelectedVideoFormat();
                        af = GetSelectedAudioFormat();
                        ab = GetSelectedAudioBitrate();
                    });

                    await AddDownloadToHistoryAsync(url, UITexte.UITexte.MainWindow_Download_UnknownTitle, "", isAudioOnly, vq, vf, af, ab);
                }
                catch { }
            }
        }

        private async void btnDownloadStart_Click(object sender, RoutedEventArgs e)
        {
            tbDebugOutput.Clear();
            Dispatcher.Invoke(() =>
            {
                _lastDownloadPath = cbAudioOnly.IsChecked == true
                    ? Properties.Settings.Default.DownloadAudioOnlyPath
                    : Properties.Settings.Default.DownloadPath;
            });

            SetUiEnabled(false);
            btnDownloadCancel.IsEnabled = true;
            spLoadingbar.Visibility = Visibility.Visible;

            UpdateProgress(0);
            SetiaStatusIcon(iaStatusIconType.Loading);
            txtDownloadStatus.Text = UITextDictionary.Get("DownloadPage.Status.Loading");

            _downloadCancellationTokenSource = new CancellationTokenSource();
            var token = _downloadCancellationTokenSource.Token;

            string url = tbURL.Text;
            string ytDlpPath = Properties.Settings.Default.YtdlpPath;
            string downloadDir = lblDownloadPath.Content?.ToString() ?? "";

            Task? titleTask = null;

            try
            {
                _downloadTask = StartDownloadAsync(token);

                bool useCustom = false;
                string customName = "";
                Dispatcher.Invoke(() =>
                {
                    useCustom = cbCustomFilename.IsChecked == true;
                    customName = tbCustomFilename.Text?.Trim() ?? "";
                });

                if (useCustom && !string.IsNullOrWhiteSpace(customName))
                {
                    titleTask = AddDownloadToHistoryAsync(url, customName, downloadDir,
                        isAudioOnly: cbAudioOnly.IsChecked == true,
                        videoQuality: GetSelectedVideoQualityLabel(),
                        videoFormat: GetSelectedVideoFormat(),
                        audioFormat: GetSelectedAudioFormat(),
                        audioBitrate: GetSelectedAudioBitrate());
                }
                else
                {
                    titleTask = FetchTitleAndAddHistoryAsync(ytDlpPath, url, token);
                }

                await _downloadTask;

                if (titleTask != null) await titleTask;

                if (token.IsCancellationRequested) throw new OperationCanceledException(token);

                AppendOutput(UITexte.UITexte.MainWindow_DebugOutput_DownloadSuccess);
                SetiaStatusIcon(iaStatusIconType.Success);
                UpdateProgress(100);
                txtDownloadStatus.Text = UITextDictionary.Get("DownloadPage.Status.Success");
            }
            catch (OperationCanceledException)
            {
                AppendOutput(UITexte.UITexte.MainWindow_DebugOutput_DownloadCancel);
                SetiaStatusIcon(iaStatusIconType.Error);
                UpdateProgress(_lastProgress, isError: true);
                txtDownloadStatus.Text = UITextDictionary.Get("DownloadPage.Status.Canceled");
            }
            catch (Exception ex)
            {
                AppendOutput($"[ERROR] {ex.Message}");
                SetiaStatusIcon(iaStatusIconType.Error);
                UpdateProgress(_lastProgress, isError: true);
                txtDownloadStatus.Text = UITextDictionary.Get("DownloadPage.Status.Error");
            }
            finally
            {
                SetUiEnabled(true);
                btnDownloadStart.IsEnabled = !string.IsNullOrWhiteSpace(tbURL.Text);
                btnDownloadCancel.IsEnabled = false;
                AudioOnlyAdjustments();
                FirstSecondsAdjustments();
                VideoformatAdjustments();
                TimespanAdjustments();
                CustomFilenameAdjustments();
            }
        }

        private void btnHistory_Click(object sender, RoutedEventArgs e)
        {
            var win = new DownloadHistoryWindow { Owner = Window.GetWindow(this) };
            win.ShowDialog();
        }

        private void btnStatusIcon_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_lastDownloadPath) && System.IO.Directory.Exists(_lastDownloadPath))
            {
                Process.Start(new ProcessStartInfo { FileName = _lastDownloadPath, UseShellExecute = true });
            }
            else
            {
                FluentMessageBox.Show(UITexte.UITexte.MainWindow_Download_PathNotFound,
                    icon: MessageBoxImage.Error);
            }
        }

        private async Task<(int? SampleRate, int? Channels)> GetSourceAudioMetadataAsync(string ytDlpPath, string url, CancellationToken token)
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName  = ytDlpPath,
                        Arguments = $"--no-check-certificates -f bestaudio --print \"%(asr)s|%(audio_channels)s\" \"{url}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError  = true,
                        UseShellExecute  = false,
                        CreateNoWindow   = true,
                        StandardOutputEncoding = System.Text.Encoding.UTF8,
                    }
                };

                process.Start();
                await using var reg = token.Register(() => { try { process.Kill(true); } catch { } });

                string output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync(CancellationToken.None);

                if (token.IsCancellationRequested)
                    throw new OperationCanceledException(token);

                var line = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
                int? sr = null;
                int? ch = null;

                if (!string.IsNullOrWhiteSpace(line))
                {
                    var parts = line.Split('|');
                    if (parts.Length >= 1 && int.TryParse(parts[0].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out int sVal))
                        sr = sVal;
                    if (parts.Length >= 2 && int.TryParse(parts[1].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out int cVal))
                        ch = cVal;
                }

                AppendOutput($"[META] SampleRate={(sr?.ToString() ?? "?")} Hz, Channels={(ch?.ToString() ?? "?")}");
                return (sr, ch);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                AppendOutput($"[META] Fehler: {ex.Message}");
                return (null, null);
            }
        }

        private string GetSelectedVideoQualityLabel()
        {
            if (!Dispatcher.CheckAccess())
                return Dispatcher.Invoke(GetSelectedVideoQualityLabel);

            if (combVideoQuality.SelectedItem is ComboBoxItem item)
                return item.Content?.ToString() ?? UITextDictionary.Get("DownloadPage.Quality.Highest");
            return UITextDictionary.Get("DownloadPage.Quality.Highest");
        }

        /// <summary>Gibt den Tag-Wert des gewählten Qualitäts-Items zurück (z.B. "best", "1080", "720").
        /// Sprachunabhängig – für den yt-dlp Format-Selector.</summary>
        private string GetSelectedVideoQualityTag()
        {
            if (!Dispatcher.CheckAccess())
                return Dispatcher.Invoke(GetSelectedVideoQualityTag);

            if (combVideoQuality.SelectedItem is ComboBoxItem item)
                return item.Tag?.ToString() ?? "best";
            return "best";
        }

        private string GetSelectedVideoFormat()
        {
            if (!Dispatcher.CheckAccess())
                return Dispatcher.Invoke(GetSelectedVideoFormat);

            if (combVideoFormat.SelectedItem is ComboBoxItem item)
                return item.Content?.ToString()?.ToLowerInvariant() ?? "mp4";
            return "mp4";
        }

        private string GetSelectedAudioBitrate()
        {
            if (!Dispatcher.CheckAccess())
                return Dispatcher.Invoke(GetSelectedAudioBitrate);

            if (combAudioBitrate.SelectedItem is ComboBoxItem item)
                return item.Content?.ToString()?.ToLowerInvariant() ?? "192k";
            return "192k";
        }

        /// <summary>Baut den yt-dlp Format-Selector aus dem Tag-Wert (sprachunabhängig).</summary>
        private static string BuildYtDlpVideoFormatSelector(string qualityTag) =>
            qualityTag switch
            {
                "best" or "" => "bestvideo+bestaudio/best",
                _ when int.TryParse(qualityTag, out int h) && h > 0
                    => $"bestvideo[height<={h}]+bestaudio/best[height<={h}]",
                _ => "bestvideo+bestaudio/best"
            };

        private string BuildYTDLPArguments(int? sourceAsr, int? sourceChannels)
        {
            string url = "";
            string timespanFrom = "";
            string timespanTo = "";
            string firstSeconds = "";
            bool isTimespan = false;
            bool isFirstSeconds = false;
            bool isAudioOnly = false;
            bool isVideoformat = false;
            string selectedAudioFormat = "";
            string ffmpegPath = "";
            string downloadPath = "";
            string vqLabel = "";     // für History-Anzeige
            string vqTag = "best";   // für Format-Selector (sprachunabhängig)
            string vfContainer = "";
            string abitrate = "";
            bool useCustomName = false;
            string customName = "";

            Dispatcher.Invoke(() =>
            {
                url = tbURL.Text;
                timespanFrom = tbTimespanFrom.Text;
                timespanTo = tbTimespanTo.Text;
                firstSeconds = tbFirstSecondsSeconds.Text;
                isTimespan = cbTimespan.IsChecked == true;
                isFirstSeconds = cbFirstSeconds.IsChecked == true;
                isAudioOnly = cbAudioOnly.IsChecked == true;
                isVideoformat = cbVideoformat.IsChecked == true;

                downloadPath = isAudioOnly ? Properties.Settings.Default.DownloadAudioOnlyPath : Properties.Settings.Default.DownloadPath;

                selectedAudioFormat = GetSelectedAudioFormat();
                vqLabel = GetSelectedVideoQualityLabel();
                vqTag   = GetSelectedVideoQualityTag();
                vfContainer = GetSelectedVideoFormat();
                abitrate = GetSelectedAudioBitrate();

                ffmpegPath = Properties.Settings.Default.FfmpegPath;

                useCustomName = cbCustomFilename.IsChecked == true;
                customName = tbCustomFilename.Text?.Trim() ?? "";
            });

            static string SanitizeSegment(string? s)
            {
                if (string.IsNullOrWhiteSpace(s)) return "";
                var invalid = System.IO.Path.GetInvalidFileNameChars();
                var cleaned = new string(s.Select(ch => invalid.Contains(ch) || ch == ':' ? '-' : ch).ToArray());
                cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned.Trim().Replace(' ', '-'), "-{2,}", "-");
                return cleaned.ToLowerInvariant();
            }

            var sb = new System.Text.StringBuilder();
            bool isHighestAbr = string.Equals(abitrate, "höchste", StringComparison.OrdinalIgnoreCase);

            if (isAudioOnly)
            {
                bool needEnhance = (!sourceAsr.HasValue) ||
                                   (sourceAsr.HasValue && sourceAsr.Value < 44100) ||
                                   (sourceChannels.HasValue && sourceChannels.Value == 1);

                sb.Append($"-x --audio-format \"{selectedAudioFormat}\" ");

                if (!isHighestAbr)
                {
                    sb.Append($"--audio-quality \"{abitrate.ToUpperInvariant()}\" ");
                    AppendOutput($"[AUDIO-ONLY] Ziel-Bitrate: {abitrate}");
                }
                else
                {
                    AppendOutput("[AUDIO-ONLY] Audio-Bitrate: Höchste (keine feste Bitrate erzwungen)");
                }

                if (needEnhance)
                {
                    sb.Append("--postprocessor-args \"ffmpeg:-ar 48000 -ac 2\" ");
                    AppendOutput($"[AUDIO-ONLY] Reencode erzwungen (asr={(sourceAsr?.ToString() ?? "NA")}, ch={(sourceChannels?.ToString() ?? "NA")}) -> 48kHz Stereo");
                }
                else
                {
                    AppendOutput($"[AUDIO-ONLY] Kein SR/Channel-Reencode nötig (asr={sourceAsr} Hz, ch={sourceChannels})");
                }
            }
            else
            {
                var fSelector = BuildYtDlpVideoFormatSelector(vqTag);
                sb.Append($"-f \"{fSelector}\" ");

                if (isVideoformat)
                {
                    AppendOutput($"[VIDEO] Schnittmodus aktiv -> mp4 + x264, VQ={vqLabel}");
                    sb.Append("--recode-video mp4 ");
                    sb.Append("--ppa \"VideoConvertor:-c:v libx264 -preset medium -crf 20 -pix_fmt yuv420p -c:a aac -ar 48000 -ac 2 -movflags +faststart\" ");
                }
                else
                {
                    AppendOutput($"[VIDEO] Recode in Container: {vfContainer}, VQ={vqLabel}");
                    sb.Append($"--recode-video {vfContainer} ");
                    if (vfContainer.Equals("mp4", StringComparison.OrdinalIgnoreCase))
                        sb.Append("--ppa \"VideoConvertor:-movflags +faststart\" ");
                }
            }

            var tags = new System.Collections.Generic.List<string>();

            if (isTimespan)
                tags.Add($"t{SanitizeSegment(timespanFrom)}-{SanitizeSegment(timespanTo)}");
            else if (isFirstSeconds)
                tags.Add($"s{SanitizeSegment(firstSeconds)}");

            if (isAudioOnly)
            {
                tags.Add("a");
                tags.Add(SanitizeSegment(selectedAudioFormat));
                tags.Add(isHighestAbr ? "abest" : $"a{SanitizeSegment(abitrate)}");
            }
            else
            {
                tags.Add($"q{vqTag}");                        // Tag-Wert (sprachunabhängig)
                tags.Add(SanitizeSegment(vfContainer));
                if (isVideoformat) tags.Add("x264");
            }

            string variantSuffix = tags.Count > 0 ? "_" + string.Join("_", tags) : "";

            string fileBase;
            if (useCustomName && !string.IsNullOrWhiteSpace(customName))
            {
                fileBase = SanitizeSegment(customName);
                AppendOutput($"[NAME] Benutzerdefinierter Dateiname aktiv: \"{fileBase}\"");
            }
            else
            {
                fileBase = "%(title)s";
            }

            string outputPattern = $"{fileBase}{variantSuffix}_%(id)s.%(ext)s";
            sb.Append($"-o \"{downloadPath}\\{outputPattern}\" ");

            if (isTimespan)
                sb.Append($"--download-sections \"*{timespanFrom}-{timespanTo}\" ");

            if (isFirstSeconds)
                sb.Append($"--downloader \"{ffmpegPath}\" --downloader-args \"ffmpeg:-t {firstSeconds}\" ");

            // --newline: Progress-Updates als separate Zeilen (statt \r), verbessert Parsing
            sb.Append("--no-check-certificates --no-mtime --newline ");
            sb.Append($"\"{url}\"");

            AppendOutput("ARGS: " + sb);
            return sb.ToString();
        }

        private void cbAudioOnlyCheck(object sender, RoutedEventArgs e) => AudioOnlyAdjustments();
        private void cbFirstSecondsCheck(object sender, RoutedEventArgs e) { FirstSecondsAdjustments(); ValidateDownloadButton(); }
        private void cbTimespanCheck(object sender, RoutedEventArgs e) => TimespanAdjustments();
        private void cbVideoFormatCheck(object sender, RoutedEventArgs e) => VideoformatAdjustments();

        private void FirstSecondsAdjustments()
        {
            bool b = cbFirstSeconds.IsChecked == true;
            tbFirstSecondsSeconds.IsEnabled = b;
            tbFirstSecondsSeconds.IsReadOnly = !b;
            txtFirstSecondsInfo1.IsEnabled = b;
            txtFirstSecondsInfo2.IsEnabled = b;
            cbTimespan.IsEnabled = !b;
        }

        private string GetSelectedAudioFormat()
        {
            if (!Dispatcher.CheckAccess())
                return Dispatcher.Invoke(GetSelectedAudioFormat);

            if (combAudioFormat.SelectedItem is ComboBoxItem selectedItem)
                return selectedItem.Content?.ToString() ?? "mp3";
            return "mp3";
        }

        private async Task<string> GetVideoTitleAsync(string ytDlpPath, string url, CancellationToken token)
        {
            // --print "%(title)s" ist der moderne Ersatz für das veraltete --get-title
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName  = ytDlpPath,
                    Arguments = $"--no-check-certificates --print \"%(title)s\" \"{url}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute  = false,
                    CreateNoWindow   = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                }
            };

            process.Start();

            // Prozess killen wenn Token abbricht
            await using var reg = token.Register(() => { try { process.Kill(true); } catch { } });

            string output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync(CancellationToken.None);

            if (token.IsCancellationRequested)
                throw new OperationCanceledException(token);

            string title = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                  .FirstOrDefault() ?? "";

            if (string.IsNullOrWhiteSpace(title))
            {
                AppendOutput("[TITLE] Kein Titel empfangen – nutze URL-Fallback");
                try
                {
                    var uri = new Uri(url);
                    title = uri.Host.Contains("twitch.tv")
                        ? (uri.Segments.Length > 1 ? $"Twitch: {uri.Segments[^1].Trim('/')}" : "Twitch-Video")
                        : UITexte.UITexte.MainWindow_Download_UnknownTitle;
                }
                catch { title = UITexte.UITexte.MainWindow_Download_UnknownTitle; }
            }

            return title;
        }

        private void DownloadPath_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is not Label label) return;
            string? path = label.Content.ToString();
            if (!string.IsNullOrWhiteSpace(path) && System.IO.Directory.Exists(path))
            {
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            else
            {
                FluentMessageBox.Show(
                    string.Format(UITexte.UITexte.MainWindow_Label_Click_DownloadPathNotFound, path),
                    icon: MessageBoxImage.Error);
            }
        }

        private double? ParseFfmpegTimeProgress(string line, string timespanFrom, string timespanTo, out double? secondsCurrent, out double? secondsTotal)
        {
            secondsCurrent = null;
            secondsTotal = null;
            var timeMatch = System.Text.RegularExpressions.Regex.Match(line, @"time=(\d{2}:\d{2}:\d{2}\.\d{2})");
            if (timeMatch.Success)
            {
                var timeStr = timeMatch.Groups[1].Value;
                if (TryParseFlexibleTime(timeStr, out var current) &&
                    TryParseFlexibleTime(timespanFrom, out var from) &&
                    TryParseFlexibleTime(timespanTo, out var to))
                {
                    var totalDuration = to - from;
                    var currentDuration = current - from;
                    secondsCurrent = currentDuration.TotalSeconds;
                    secondsTotal = totalDuration.TotalSeconds;
                    if (totalDuration.TotalSeconds > 0)
                    {
                        var percent = (currentDuration.TotalSeconds / totalDuration.TotalSeconds) * 100.0;
                        return Math.Max(0, Math.Min(100, percent));
                    }
                }
            }
            return null;
        }

        private double? ParseYtDlpProgress(string line, out double? speedMBs)
        {
            var (progress, speed) = ParseYtDlpDownloadProgress(line);
            speedMBs = speed;
            return progress;
        }

        // ─── Neue, performante Hilfsmethoden ────────────────────────────────────────

        /// <summary>Parst eine yt-dlp Ausgabezeile auf Fortschritt und Geschwindigkeit.
        /// Unterstützt KiB/s, MiB/s und GiB/s.</summary>
        private static (double? Progress, double? SpeedMBs) ParseYtDlpDownloadProgress(string line)
        {
            if (!line.StartsWith("[download]")) return (null, null);

            double? speed = null;
            var speedMatch = System.Text.RegularExpressions.Regex.Match(
                line, @"at\s+([\d.]+)(KiB|MiB|GiB)/s");
            if (speedMatch.Success &&
                double.TryParse(speedMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double s))
            {
                speed = speedMatch.Groups[2].Value switch
                {
                    "KiB" => s / 1024.0,
                    "GiB" => s * 1024.0,
                    _     => s   // MiB
                };
            }

            var pctMatch = System.Text.RegularExpressions.Regex.Match(
                line, @"^\[download\]\s+([\d.]+)%");
            if (pctMatch.Success &&
                double.TryParse(pctMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double pct))
                return (pct, speed);

            return (null, speed);
        }

        /// <summary>Verarbeitet eine Ausgabezeile von yt-dlp:
        /// Fortschritt, Geschwindigkeit und Prozess-Phase.</summary>
        private void ProcessYtDlpOutputLine(string line, string timespanFrom, string timespanTo,
                                            bool isTimespan, bool isError)
        {
            AppendOutput(isError ? $"[STDERR] {line}" : line);

            var (progress, speed) = ParseYtDlpDownloadProgress(line);
            if (progress.HasValue)
            {
                UpdateProgress(progress.Value, false, speed);
                return;
            }

            if (isTimespan || line.StartsWith("[ffmpeg]"))
            {
                var pct = ParseFfmpegTimeProgress(line, timespanFrom, timespanTo, out _, out _);
                if (pct.HasValue) UpdateProgress(pct.Value, false, null);
            }

            var stage = DetectDownloadStage(line);
            if (stage != null) Dispatcher.Invoke(() => txtDownloadStatus.Text = stage);
        }

        /// <summary>Erkennt die aktuelle yt-dlp Verarbeitungsphase aus der Ausgabezeile.</summary>
        private string? DetectDownloadStage(string line) =>
            line switch
            {
                _ when line.StartsWith("[Merger]")
                    => UITextDictionary.Get("DownloadPage.Status.Merging"),
                _ when line.StartsWith("[ExtractAudio]")
                    => UITextDictionary.Get("DownloadPage.Status.ExtractingAudio"),
                _ when line.StartsWith("[VideoConvertor]") || line.StartsWith("[VideoRemuxer]")
                    => UITextDictionary.Get("DownloadPage.Status.Converting"),
                _ when line.StartsWith("[ffmpeg]") && !line.Contains("time=")
                    => UITextDictionary.Get("DownloadPage.Status.Processing"),
                _ => null
            };

        private async Task RunYtDlpAsync(string ytDlpPath, string arguments, CancellationToken token)
        {
            string timespanTo = string.Empty;
            string timespanFrom = string.Empty;
            bool isTimespanChecked = false;
            Dispatcher.Invoke(() =>
            {
                timespanFrom      = tbTimespanFrom.Text;
                timespanTo        = tbTimespanTo.Text;
                isTimespanChecked = cbTimespan.IsChecked == true;
            });

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName  = ytDlpPath,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute  = false,
                    CreateNoWindow   = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding  = System.Text.Encoding.UTF8,
                }
            };

            _ytDlpProcess = process;

            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    ProcessYtDlpOutputLine(e.Data, timespanFrom, timespanTo, isTimespanChecked, isError: false);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    ProcessYtDlpOutputLine(e.Data, timespanFrom, timespanTo, isTimespanChecked, isError: true);
            };

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Token-Registrierung: Prozess bei Abbruch sofort killen
                await using var tokenReg = token.Register(() =>
                {
                    try { process.Kill(entireProcessTree: true); } catch { }
                });

                // CancellationToken.None: sauber warten bis Kill abgeschlossen ist
                await process.WaitForExitAsync(CancellationToken.None);

                if (token.IsCancellationRequested)
                    throw new OperationCanceledException(token);

                AppendOutput($"[yt-dlp] Beendet mit Exit-Code: {process.ExitCode}");

                if (process.ExitCode != 0)
                    throw new InvalidOperationException($"yt-dlp beendet mit Exit-Code {process.ExitCode}");
            }
            finally
            {
                _ytDlpProcess = null;
                process.Dispose();
            }
        }

        private void SaveSettings(object sender, RoutedEventArgs e)
        {
            bool cbFirstSecondsChecked = cbFirstSeconds.IsChecked == true;
            bool cbVideoformatChecked = cbVideoformat.IsChecked == true;
            bool cbTimespanChecked = cbTimespan.IsChecked == true;
            bool cbAudioOnlyChecked = cbAudioOnly.IsChecked == true;

            bool cbCustomFilenameChecked = cbCustomFilename.IsChecked == true;
            string tbCustomFilenameText = cbCustomFilenameChecked ? tbCustomFilename.Text : "";

            string tbAudioOnlyText = cbAudioOnlyChecked ? GetSelectedAudioFormat() : "";
            string tbTimespanFromText = cbTimespanChecked ? tbTimespanFrom.Text : "";
            string tbTimespanToText = cbTimespanChecked ? tbTimespanTo.Text : "";
            string tbFirstSecondsText = cbFirstSecondsChecked ? tbFirstSecondsSeconds.Text : "";

            string selVq = GetSelectedVideoQualityLabel();
            string selVf = GetSelectedVideoFormat();
            string selAb = GetSelectedAudioBitrate();

            var T = UITextDictionary.Get;
            var result = FluentMessageBox.Show(
                T("DownloadPage.SaveSettings.Question"),
                T("DownloadPage.SaveSettings.Title"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                Properties.Settings.Default.CheckedTimespan = cbTimespanChecked;
                Properties.Settings.Default.TimespanFrom = tbTimespanFromText;
                Properties.Settings.Default.TimespanTo = tbTimespanToText;
                Properties.Settings.Default.CheckedFirstSeconds = cbFirstSecondsChecked;
                Properties.Settings.Default.FirstSecondsSeconds = tbFirstSecondsText;
                Properties.Settings.Default.CheckedVideoFormat = cbVideoformatChecked;
                Properties.Settings.Default.DownloadPath = lblDownloadPath.Content.ToString();
                Properties.Settings.Default.CheckedAudioOnly = cbAudioOnlyChecked;
                Properties.Settings.Default.SelectedAudioFormat = tbAudioOnlyText;
                Properties.Settings.Default.DownloadAudioOnlyPath = lblAudioPath.Content.ToString();

                Properties.Settings.Default.SelectedVideoQuality = selVq;
                Properties.Settings.Default.SelectedVideoFormat = selVf;
                Properties.Settings.Default.SelectedAudioBitrate = selAb;

                Properties.Settings.Default.CheckedCustomFilename = cbCustomFilenameChecked;
                Properties.Settings.Default.CustomFilename = tbCustomFilenameText;

                Properties.Settings.Default.Save();
                FluentMessageBox.Show(
                    T("DownloadPage.SaveSettings.Saved"),
                    T("DownloadPage.SaveSettings.Title"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void SelectAudioFormat(string savedFormat)
        {
            bool formatFound = false;
            foreach (ComboBoxItem item in combAudioFormat.Items)
            {
                if (item.Content?.ToString() == savedFormat)
                {
                    combAudioFormat.SelectedItem = item;
                    formatFound = true;
                    break;
                }
            }
            if (!formatFound)
            {
                foreach (ComboBoxItem item in combAudioFormat.Items)
                {
                    if (item.Content?.ToString() == "mp3")
                    {
                        combAudioFormat.SelectedItem = item;
                        break;
                    }
                }
            }
        }

        private void SelectComboByContent(ComboBox combo, string desired, string fallback)
        {
            if (string.IsNullOrWhiteSpace(desired)) desired = fallback;
            foreach (ComboBoxItem item in combo.Items)
            {
                if (string.Equals(item.Content?.ToString(), desired, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedItem = item;
                    return;
                }
            }
            foreach (ComboBoxItem item in combo.Items)
            {
                if (string.Equals(item.Content?.ToString(), fallback, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedItem = item;
                    return;
                }
            }
        }

        private void SetAudioDownloadPathInSettings()
        {
            string savedAudioPath = Properties.Settings.Default.DownloadAudioOnlyPath;
            if (string.IsNullOrEmpty(savedAudioPath) || !System.IO.Directory.Exists(savedAudioPath))
            {
                string downloadsFolder = KnownFoldersHelper.GetPath(KnownFolder.Downloads);
                Properties.Settings.Default.DownloadAudioOnlyPath = downloadsFolder;
                Properties.Settings.Default.Save();
            }
        }

        private void SetDownloadPathInSettings()
        {
            string savedPath = Properties.Settings.Default.DownloadPath;
            if (string.IsNullOrEmpty(savedPath) || !System.IO.Directory.Exists(savedPath))
            {
                string downloadsFolder = KnownFoldersHelper.GetPath(KnownFolder.Downloads);
                Properties.Settings.Default.DownloadPath = downloadsFolder;
                Properties.Settings.Default.Save();
            }
        }

        private void SetiaStatusIcon(iaStatusIconType type)
        {
            Dispatcher.Invoke(() =>
            {
                btnStatusIcon.IsEnabled = (type == iaStatusIconType.Success || type == iaStatusIconType.Error);
                switch (type)
                {
                    case iaStatusIconType.Loading:
                        iaStatusIcon.Text = "\uE895";
                        iaStatusIcon.Foreground = new SolidColorBrush(Colors.SteelBlue);
                        StartIconRotation(iaStatusIcon);
                        break;
                    case iaStatusIconType.Success:
                        iaStatusIcon.Text = "\uE73E";
                        iaStatusIcon.Foreground = new SolidColorBrush(Colors.Green);
                        StopIconRotation(iaStatusIcon);
                        break;
                    case iaStatusIconType.Error:
                        iaStatusIcon.Text = "\uE711";
                        iaStatusIcon.Foreground = new SolidColorBrush(Colors.Red);
                        StopIconRotation(iaStatusIcon);
                        break;
                    default:
                        iaStatusIcon.Text = "";
                        StopIconRotation(iaStatusIcon);
                        break;
                }
            });
        }

        private void StartIconRotation(TextBlock icon)
        {
            var rotateTransform = new RotateTransform();
            icon.RenderTransform = rotateTransform;
            icon.RenderTransformOrigin = new Point(0.5, 0.5);

            var animation = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = TimeSpan.FromSeconds(2),
                RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever
            };
            rotateTransform.BeginAnimation(RotateTransform.AngleProperty, animation);
        }

        private void StopIconRotation(TextBlock icon)
        {
            if (icon.RenderTransform is RotateTransform rotateTransform)
            {
                rotateTransform.BeginAnimation(RotateTransform.AngleProperty, null);
                icon.RenderTransform = null;
            }
        }

        private void SettingsLoad()
        {
            SetDownloadPathInSettings();
            SetAudioDownloadPathInSettings();

            lblDownloadPath.Content = Properties.Settings.Default.DownloadPath;
            lblAudioPath.Content = Properties.Settings.Default.DownloadAudioOnlyPath;

            tbTimespanFrom.Text = Properties.Settings.Default.CheckedTimespan ? Properties.Settings.Default.TimespanFrom : "";
            tbTimespanTo.Text = Properties.Settings.Default.CheckedTimespan ? Properties.Settings.Default.TimespanTo : "";
            tbFirstSecondsSeconds.Text = Properties.Settings.Default.CheckedFirstSeconds ? Properties.Settings.Default.FirstSecondsSeconds : "";

            cbTimespan.IsChecked = Properties.Settings.Default.CheckedTimespan;
            cbFirstSeconds.IsChecked = Properties.Settings.Default.CheckedFirstSeconds;
            cbVideoformat.IsChecked = Properties.Settings.Default.CheckedVideoFormat;
            cbAudioOnly.IsChecked = Properties.Settings.Default.CheckedAudioOnly;

            cbCustomFilename.IsChecked = Properties.Settings.Default.CheckedCustomFilename;
            tbCustomFilename.Text = Properties.Settings.Default.CheckedCustomFilename ? Properties.Settings.Default.CustomFilename : "";
            CustomFilenameAdjustments();

            if (Properties.Settings.Default.CheckedAudioOnly)
                SelectAudioFormat(Properties.Settings.Default.SelectedAudioFormat);

            SelectComboByContent(combVideoQuality, Properties.Settings.Default.SelectedVideoQuality, "Höchste");
            SelectComboByContent(combVideoFormat, Properties.Settings.Default.SelectedVideoFormat, "mp4");
            SelectComboByContent(combAudioBitrate, Properties.Settings.Default.SelectedAudioBitrate, "192k");

            SetUiAudioEnabled(Properties.Settings.Default.CheckedAudioOnlyPath);
        }

        private void SetUiEnabled(bool enabled)
        {
            tbURL.IsEnabled = enabled;
            cbTimespan.IsEnabled = enabled;
            tbTimespanFrom.IsEnabled = enabled && cbTimespan.IsChecked == true;
            tbTimespanTo.IsEnabled = enabled && cbTimespan.IsChecked == true;
            cbFirstSeconds.IsEnabled = enabled;
            tbFirstSecondsSeconds.IsEnabled = enabled && cbFirstSeconds.IsChecked == true;
            cbVideoformat.IsEnabled = enabled && cbAudioOnly.IsChecked != true;
            cbAudioOnly.IsEnabled = enabled;
            combAudioFormat.IsEnabled = enabled && cbAudioOnly.IsChecked == true;
            btnSaveSettings.IsEnabled = enabled;
            btnHistory.IsEnabled = enabled;
            btnDownloadStart.IsEnabled = enabled;

            combVideoQuality.IsEnabled = enabled && cbAudioOnly.IsChecked != true;
            combVideoFormat.IsEnabled = enabled && cbAudioOnly.IsChecked != true && cbVideoformat.IsChecked != true;
            combAudioBitrate.IsEnabled = enabled && cbAudioOnly.IsChecked == true;

            cbCustomFilename.IsEnabled = enabled;
            tbCustomFilename.IsEnabled = enabled && cbCustomFilename.IsChecked == true;
            tbCustomFilename.IsReadOnly = !(enabled && cbCustomFilename.IsChecked == true);
        }

        private async Task StartDownloadAsync(CancellationToken token)
        {
            int? sourceAsr = null;
            int? sourceChannels = null;
            bool needsMeta = false;
            string url = "";
            string ytDlpPath = Properties.Settings.Default.YtdlpPath;

            Dispatcher.Invoke(() =>
            {
                bool videoformat = cbVideoformat.IsChecked == true && cbAudioOnly.IsChecked != true;
                bool audioOnly = cbAudioOnly.IsChecked == true;
                needsMeta = videoformat || audioOnly;
                url = tbURL.Text;
            });

            if (needsMeta)
            {
                var meta = await GetSourceAudioMetadataAsync(ytDlpPath, url, token);
                sourceAsr = meta.SampleRate;
                sourceChannels = meta.Channels;
            }

            string args = BuildYTDLPArguments(sourceAsr, sourceChannels);
            await RunYtDlpAsync(ytDlpPath, args, token);
        }

        private void tbDebugOutput_TextChanged(object sender, TextChangedEventArgs e) => tbDebugOutput.ScrollToEnd();
        private void tbFirstSecondsSeconds_TextChanged(object sender, TextChangedEventArgs e) => ValidateDownloadButton();
        private void tbTimespanFrom_TextChanged(object sender, TextChangedEventArgs e) => ValidateDownloadButton();
        private void tbTimespanTo_TextChanged(object sender, TextChangedEventArgs e) => ValidateDownloadButton();

        private void tbURL_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && btnDownloadStart.IsEnabled)
            {
                btnDownloadStart.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                e.Handled = true;
            }
        }

        private void tbURL_TextChanged(object sender, TextChangedEventArgs e) => ValidateDownloadButton();

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
                Dispatcher.InvokeAsync(textBox.SelectAll);
        }

        private void TimespanAdjustments()
        {
            bool t = cbTimespan.IsChecked == true;
            tbTimespanFrom.IsReadOnly = !t;
            tbTimespanFrom.IsEnabled = t;
            tbTimespanTo.IsReadOnly = !t;
            tbTimespanTo.IsEnabled = t;
            txtTimespanFrom.IsEnabled = t;
            txtTimespanDash.IsEnabled = t;
            txtTimespanInfo.IsEnabled = t;
            imgTimespanInfo.IsEnabled = t;
            cbFirstSeconds.IsEnabled = !t;
            ValidateDownloadButton();
        }

        private bool TryParseFlexibleTime(string input, out TimeSpan result)
        {
            string[] formats = { @"hh\:mm\:ss\.ff", @"hh\:mm\:ss", @"mm\:ss\.ff", @"mm\:ss" };
            foreach (var format in formats)
            {
                if (TimeSpan.TryParseExact(input, format, null, out result))
                    return true;
            }
            return TimeSpan.TryParse(input, out result);
        }

        private void UpdateProgress(double percent, bool isError = false, double? speedMBs = null)
        {
            Dispatcher.Invoke(() =>
            {
                _lastProgress = percent;
                pbDownload.Value = percent;
                if (isError)
                {
                    pbDownload.Foreground = new SolidColorBrush(Colors.Red);
                    txtDownloadProgress.Text = "";
                }
                else
                {
                    pbDownload.Foreground = new SolidColorBrush(Colors.SteelBlue);
                    if (percent > 0)
                    {
                        txtDownloadProgress.Text = speedMBs.HasValue
                            ? $"{percent:F2} % ({speedMBs.Value:F2} MB/s)"
                            : $"{percent:F2} %";
                    }
                    else if (speedMBs.HasValue)
                    {
                        txtDownloadProgress.Text = $"{speedMBs.Value:F2} MB/s";
                    }
                    else
                    {
                        txtDownloadProgress.Text = "";
                    }
                }
            });
        }

        private void ValidateDownloadButton()
        {
            bool urlOk = !string.IsNullOrWhiteSpace(tbURL.Text);
            bool timespanOk = cbTimespan.IsChecked != true ||
                              (!string.IsNullOrWhiteSpace(tbTimespanFrom.Text) && !string.IsNullOrWhiteSpace(tbTimespanTo.Text));
            bool secondsOk = cbFirstSeconds.IsChecked != true ||
                             !string.IsNullOrWhiteSpace(tbFirstSecondsSeconds.Text);
            btnDownloadStart.IsEnabled = urlOk && timespanOk && secondsOk;
        }

        private void VideoformatAdjustments()
        {
            bool v = cbVideoformat.IsChecked == true;
            txtVideoformatInfo1.IsEnabled = v;
            txtVideoformatInfo2.IsEnabled = v;
            txtVideoformatInfo3.IsEnabled = v;
            cbAudioOnly.IsEnabled = !v;

            if (v)
            {
                SelectComboByContent(combVideoFormat, "mp4", "mp4");
                combVideoFormat.IsEnabled = false;
            }
            else
            {
                combVideoFormat.IsEnabled = cbAudioOnly.IsChecked != true;
            }
        }

        private void cbCustomFilenameCheck(object sender, RoutedEventArgs e)
        {
            CustomFilenameAdjustments();
            ValidateDownloadButton();
        }

        private void CustomFilenameAdjustments()
        {
            bool enabled = cbCustomFilename.IsChecked == true;
            tbCustomFilename.IsEnabled = enabled;
            tbCustomFilename.IsReadOnly = !enabled;
        }
    }
}
