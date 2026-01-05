using MortysDLP.Helpers;
using MortysDLP.Models;
using MortysDLP.Services;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MortysDLP
{
    public partial class MainWindow : Window
    {
        private CancellationTokenSource? _downloadCancellationTokenSource;
        private Task? _downloadTask;
        private string _lastDownloadPath = "";
        private double _lastProgress = 0;
        private Process? _ytDlpProcess;

        public MainWindow()
        {
            LanguageHelper.ApplyLanguage(LanguageHelper.ForceEnglish);
            InitializeComponent();
            SetUITexte();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SettingsLoad();
            FirstSecondsAdjustments();
            AudioOnlyAdjustments();
            VideoformatAdjustments();
            TimespanAdjustments();
            CustomFilenameAdjustments(); // NEU
            ValidateDownloadButton();
        }

        public enum iaStatusIconType { None, Loading, Success, Error }

        internal void SetUiAudioEnabled(bool enabled)
        {
            dpAudioPath.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
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
            combAudioFormat.IsReadOnly = !a;
            combAudioFormat.IsEnabled = a;

            // Checkbox „Videoformat (x264)“ gesperrt, wenn Audio-Only
            cbVideoformat.IsEnabled = !a;

            // Audio-Bitrate nur bei „Nur Audio“ änderbar
            combAudioBitrate.IsEnabled = a;

            // Videoqualität nur, wenn nicht Audio-Only
            combVideoQuality.IsEnabled = !a;

            // Container-Auswahl nur, wenn NICHT Audio-Only und Schnittmodus NICHT aktiv
            combVideoFormat.IsEnabled = !a && (cbVideoformat.IsChecked != true);
        }

        private void btnConvert_Click(object sender, RoutedEventArgs e)
        {
            var convertWindow = new MortysDLP.Views.ConvertWindow { Owner = this };
            convertWindow.ShowDialog();
        }

        private void btnDownloadCancel_Click(object sender, RoutedEventArgs e)
        {
            txtDownloadStatus.Text = UITexte.UITexte.MainWindow_Label_DownloadStatus_WhileCanceling;
            _downloadCancellationTokenSource?.Cancel();
            _ytDlpProcess?.Kill(true);
            UpdateProgress(0);
        }

        // Titel + Verlaufs-Schreiber mit Selektion
        private async Task FetchTitleAndAddHistoryAsync(string ytDlpPath, string url, string downloadDir, CancellationToken token)
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

                await AddDownloadToHistoryAsync(url, title, downloadDir, isAudioOnly, vq, vf, af, ab);
                AppendOutput($"[TITLE] Gespeichert: {title} | Modus={(isAudioOnly ? "Audio" : "Video")} | VQ={vq ?? "-"} | VF={vf ?? "-"} | AF={af ?? "-"} | AB={ab ?? "-"}");
            }
            catch (OperationCanceledException)
            {
                // Abbruch still akzeptieren
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

                    await AddDownloadToHistoryAsync(url, UITexte.UITexte.MainWindow_Download_UnknownTitle, downloadDir, isAudioOnly, vq, vf, af, ab);
                }
                catch { /* ignorieren */ }
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
            txtDownloadStatus.Text = UITexte.UITexte.MainWindow_Label_DownloadStatus_Loading;

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
                    // Kein Titel abrufen – direkt Verlauf mit benutzerdefiniertem Namen
                    titleTask = AddDownloadToHistoryAsync(url, customName, downloadDir,
                        isAudioOnly: cbAudioOnly.IsChecked == true,
                        videoQuality: GetSelectedVideoQualityLabel(),
                        videoFormat: GetSelectedVideoFormat(),
                        audioFormat: GetSelectedAudioFormat(),
                        audioBitrate: GetSelectedAudioBitrate());
                }
                else
                {
                    // Wie bisher: Titel abrufen
                    titleTask = FetchTitleAndAddHistoryAsync(ytDlpPath, url, downloadDir, token);
                }

                await _downloadTask;

                if (titleTask != null) await titleTask;

                if (token.IsCancellationRequested) throw new OperationCanceledException(token);

                AppendOutput(UITexte.UITexte.MainWindow_DebugOutput_DownloadSuccess);
                SetiaStatusIcon(iaStatusIconType.Success);
                UpdateProgress(100);
                txtDownloadStatus.Text = UITexte.UITexte.MainWindow_Label_DownloadStatus_Success;
            }
            catch (OperationCanceledException)
            {
                AppendOutput(UITexte.UITexte.MainWindow_DebugOutput_DownloadCancel);
                SetiaStatusIcon(iaStatusIconType.Error);
                UpdateProgress(_lastProgress, isError: true);
                txtDownloadStatus.Text = UITexte.UITexte.MainWindow_Label_DownloadStatus_Cancel;
            }
            catch
            {
                SetiaStatusIcon(iaStatusIconType.Error);
                UpdateProgress(_lastProgress, isError: true);
                txtDownloadStatus.Text = UITexte.UITexte.MainWindow_Label_DownloadStatus_Cancel;
                throw;
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
                CustomFilenameAdjustments(); // NEU
            }
        }

        private void btnHeaderChangeDownloadPath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new DownloadPathDialog { Owner = this };
            dialog.ShowDialog();
        }

        private void btnHeaderClose_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

        private void btnHeaderGitHub_Click(object sender, RoutedEventArgs e)
        {
            string url = Properties.Settings.Default.MortysDLPGitHubURL;
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch
            {
                MessageBox.Show(UITexte.UITexte.Error_OpenBrowser, UITexte.UITexte.Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnHistory_Click(object sender, RoutedEventArgs e)
        {
            var win = new DownloadHistoryWindow { Owner = this };
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
                MessageBox.Show(UITexte.UITexte.MainWindow_Download_PathNotFound, UITexte.UITexte.Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // NEU: Audio-Metadaten (SampleRate & Channels) ermitteln
        private async Task<(int? SampleRate, int? Channels)> GetSourceAudioMetadataAsync(string ytDlpPath, string url, CancellationToken token)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = ytDlpPath,
                    Arguments = $"--no-check-certificates -f bestaudio[ext=m4a]/bestaudio --print \"%(asr)s|%(audio_channels)s\" \"{url}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var process = new Process { StartInfo = psi };
                process.Start();
                string stdout = await process.StandardOutput.ReadToEndAsync();
                string stderr = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync(token);

                var line = stdout.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
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

                AppendOutput($"[META] Quelle: SampleRate={(sr?.ToString() ?? "unbekannt")} Hz, Channels={(ch?.ToString() ?? "unbekannt")})");
                if (!string.IsNullOrWhiteSpace(stderr))
                    AppendOutput($"[META-STDERR] {stderr.Trim()}");
                return (sr, ch);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                AppendOutput($"[META] Fehler: {ex.Message}");
                return (null, null);
            }
        }

        // NEU: Hilfsfunktionen für Auswahlen
        private string GetSelectedVideoQualityLabel()
        {
            if (!Dispatcher.CheckAccess())
                return Dispatcher.Invoke(GetSelectedVideoQualityLabel);

            if (combVideoQuality.SelectedItem is ComboBoxItem item)
                return item.Content?.ToString() ?? "Höchste";
            return "Höchste";
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

        // yt-dlp -f Filter aus Qualitätslabel
        private static string BuildYtDlpVideoFormatSelector(string qualityLabel)
        {
            if (string.Equals(qualityLabel, "Höchste", StringComparison.OrdinalIgnoreCase))
                return "bestvideo+bestaudio/best";

            if (qualityLabel.EndsWith("p", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(qualityLabel.TrimEnd('p', 'P'), out int h) && h > 0)
            {
                return $"bestvideo[height<={h}]+bestaudio/best[height<={h}]";
            }

            // Fallback
            return "bestvideo+bestaudio/best";
        }

        // Erweiterte Argument-Erzeugung mit Quality/Format
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
            string vqLabel = "";
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
                vfContainer = GetSelectedVideoFormat();   // mp4/mkv/mov/avi
                abitrate = GetSelectedAudioBitrate();     // z. B. 192k oder Höchste

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
            static string NormalizeQualityTag(string label)
                => string.Equals(label, "Höchste", StringComparison.OrdinalIgnoreCase) ? "best" : label.ToLowerInvariant();

            var sb = new System.Text.StringBuilder();
            bool isHighestAbr = string.Equals(abitrate, "höchste", StringComparison.OrdinalIgnoreCase);

            // 1) yt-dlp Auswahl/Transcode-Argumente
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
                    AppendOutput($"[AUDIO-ONLY] Audio-Bitrate: Höchste (keine feste Bitrate erzwungen)");
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
                var fSelector = BuildYtDlpVideoFormatSelector(vqLabel);
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

            // 2) Kurzen Varianten-Tag bauen
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
                tags.Add($"q{SanitizeSegment(NormalizeQualityTag(vqLabel))}");
                tags.Add(SanitizeSegment(vfContainer));
                if (isVideoformat) tags.Add("x264");
            }

            string variantSuffix = tags.Count > 0 ? "_" + string.Join("_", tags) : "";

            // 3) Output-Muster
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

            // 4) Zeit-Beschränkung/Downloadeinstellungen
            if (isTimespan)
                sb.Append($"--download-sections \"*{timespanFrom}-{timespanTo}\" ");

            if (isFirstSeconds)
                sb.Append($"--downloader \"{ffmpegPath}\" --downloader-args \"ffmpeg:-t {firstSeconds}\" ");

            sb.Append("--no-check-certificates --no-mtime ");
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
            var psi = new ProcessStartInfo
            {
                FileName = ytDlpPath,
                Arguments = $"--no-check-certificates --get-title \"{url}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = new Process { StartInfo = psi };
            process.Start();

            string? title = null;
            var readLineTask = process.StandardOutput.ReadLineAsync();
            while (!readLineTask.IsCompleted)
            {
                if (token.IsCancellationRequested)
                {
                    try { process.Kill(true); } catch { }
                    token.ThrowIfCancellationRequested();
                }
                await Task.Delay(50, token);
            }
            title = await readLineTask;
            string? error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync(token);

            if (string.IsNullOrWhiteSpace(title))
            {
                AppendOutput(string.Format(UITexte.UITexte.MainWindow_DebugOutput_NoTitleFromYTDLP, error?.Trim()));
                try
                {
                    var uri = new Uri(url);
                    if (uri.Host.Contains("twitch.tv"))
                    {
                        var segments = uri.Segments;
                        title = segments.Length > 1 ? $"Twitch: {segments[^1].Trim('/')}" : "Twitch-Video";
                    }
                    else
                        title = UITexte.UITexte.MainWindow_Download_UnknownTitle;
                }
                catch
                {
                    title = UITexte.UITexte.MainWindow_Download_UnknownTitle;
                }
            }
            return title ?? UITexte.UITexte.MainWindow_Download_UnknownTitle;
        }

        private void DownloadPath_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Label label) return;
            string? path = label.Content.ToString();
            if (!string.IsNullOrWhiteSpace(path) && System.IO.Directory.Exists(path))
            {
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            else
            {
                MessageBox.Show(string.Format(UITexte.UITexte.MainWindow_Label_Click_DownloadPathNotFound, path), UITexte.UITexte.Error, MessageBoxButton.OK, MessageBoxImage.Error);
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
            speedMBs = null;
            if (line.StartsWith("[download]"))
            {
                var percentMatch = System.Text.RegularExpressions.Regex.Match(line, @"\s(\d{1,3}(?:\.\d+)?)%");
                var speedMatch = System.Text.RegularExpressions.Regex.Match(line, @"at\s+([\d\.]+)MiB/s");
                if (speedMatch.Success && double.TryParse(speedMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double speed))
                    speedMBs = speed;
                if (percentMatch.Success && double.TryParse(percentMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double percent))
                    return percent;
            }
            return null;
        }

        private async Task RunYtDlpAsync(string YtDlpPath, string arguments, CancellationToken token)
        {
            string timespanTo = string.Empty;
            string timespanFrom = string.Empty;
            bool isTimespanChecked = false;
            Dispatcher.Invoke(() =>
            {
                timespanFrom = tbTimespanFrom.Text;
                timespanTo = tbTimespanTo.Text;
                isTimespanChecked = cbTimespan.IsChecked == true;
            });

            _ytDlpProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = YtDlpPath,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            _ytDlpProcess.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    var progress = ParseYtDlpProgress(e.Data, out double? speedMBs);
                    if (progress.HasValue)
                        UpdateProgress(progress.Value, false, speedMBs);
                    else if (isTimespanChecked)
                    {
                        var percent = ParseFfmpegTimeProgress(e.Data, timespanFrom, timespanTo, out _, out _);
                        if (percent.HasValue) UpdateProgress(percent.Value, false, null);
                    }
                    AppendOutput(e.Data);
                }
            };

            _ytDlpProcess.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    var percent = ParseFfmpegTimeProgress(e.Data, timespanFrom, timespanTo, out _, out _);
                    if (percent.HasValue) UpdateProgress(percent.Value, false, null);
                    AppendOutput($"[ERROR] {e.Data}");
                }
            };

            try
            {
                _ytDlpProcess.Start();
                _ytDlpProcess.BeginOutputReadLine();
                _ytDlpProcess.BeginErrorReadLine();

                while (!_ytDlpProcess.HasExited)
                {
                    if (token.IsCancellationRequested)
                    {
                        try { _ytDlpProcess.Kill(true); } catch { }
                        break;
                    }
                    await Task.Delay(100, token);
                }

                await _ytDlpProcess.WaitForExitAsync(token);

                if (token.IsCancellationRequested)
                {
                    Dispatcher.Invoke(() =>
                    {
                        SetiaStatusIcon(iaStatusIconType.Error);
                        UpdateProgress(_lastProgress, isError: true);
                        txtDownloadStatus.Text = UITexte.UITexte.MainWindow_Label_DownloadStatus_Cancel;
                        btnDownloadCancel.IsEnabled = false;
                    });
                    AppendOutput(UITexte.UITexte.MainWindow_DebugOutput_DownloadCancel);
                    return;
                }

                if (_ytDlpProcess.ExitCode != 0)
                {
                    AppendOutput(string.Format(UITexte.UITexte.MainWindow_DebugOutput_YTDLPError, _ytDlpProcess.ExitCode));
                    Dispatcher.Invoke(() =>
                    {
                        SetiaStatusIcon(iaStatusIconType.Error);
                        UpdateProgress(_lastProgress, isError: true);
                        txtDownloadStatus.Text = UITexte.UITexte.Error;
                        btnDownloadCancel.IsEnabled = false;
                    });
                }
                else
                {
                    AppendOutput(string.Format(UITexte.UITexte.MainWindow_DebugOutput_ProcessEnd, _ytDlpProcess.ExitCode));
                    Dispatcher.Invoke(() =>
                    {
                        SetiaStatusIcon(iaStatusIconType.Success);
                        UpdateProgress(100);
                        txtDownloadStatus.Text = UITexte.UITexte.MainWindow_Label_DownloadStatus_Success;
                        btnDownloadCancel.IsEnabled = false;
                    });
                }
            }
            catch (Exception ex)
            {
                AppendOutput(string.Format(UITexte.UITexte.MainWindow_DebugOutput_ThrowException, ex.Message));
                Dispatcher.Invoke(() =>
                {
                    AppendOutput(string.Format(UITexte.UITexte.MainWindow_DebugOutput_InternalError, ex.Message));
                    btnDownloadCancel.IsEnabled = false;
                });
            }
        }

        private void SaveSettings(object sender, RoutedEventArgs e)
        {
            bool cbFirstSecondsChecked = cbFirstSeconds.IsChecked == true;
            bool cbVideoformatChecked = cbVideoformat.IsChecked == true;
            bool cbTimespanChecked = cbTimespan.IsChecked == true;
            bool cbAudioOnlyChecked = cbAudioOnly.IsChecked == true;

            // NEU: Custom Filename
            bool cbCustomFilenameChecked = cbCustomFilename.IsChecked == true;
            string tbCustomFilenameText = cbCustomFilenameChecked ? tbCustomFilename.Text : "";

            string tbAudioOnlyText = cbAudioOnlyChecked ? GetSelectedAudioFormat() : "";
            string tbTimespanFromText = cbTimespanChecked ? tbTimespanFrom.Text : "";
            string tbTimespanToText = cbTimespanChecked ? tbTimespanTo.Text : "";
            string tbFirstSecondsText = cbFirstSecondsChecked ? tbFirstSecondsSeconds.Text : "";

            string selVq = GetSelectedVideoQualityLabel();
            string selVf = GetSelectedVideoFormat();
            string selAb = GetSelectedAudioBitrate();

            var result = MessageBox.Show(UITexte.UITexte.MessageBox_SaveSettings_Question, UITexte.UITexte.MessageBox_SaveSettings_Title, MessageBoxButton.YesNo, MessageBoxImage.Question);
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

                // NEU: Speichern Custom Filename
                Properties.Settings.Default.CheckedCustomFilename = cbCustomFilenameChecked;
                Properties.Settings.Default.CustomFilename = tbCustomFilenameText;

                Properties.Settings.Default.Save();
                MessageBox.Show("Einstellungen gespeichert");
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
                iaStatusIcon.Spin = false;
                btnStatusIcon.IsEnabled = (type == iaStatusIconType.Success || type == iaStatusIconType.Error);
                switch (type)
                {
                    case iaStatusIconType.Loading:
                        iaStatusIcon.Icon = FontAwesome.WPF.FontAwesomeIcon.Spinner;
                        iaStatusIcon.Spin = true;
                        iaStatusIcon.Foreground = new SolidColorBrush(Colors.SteelBlue);
                        break;
                    case iaStatusIconType.Success:
                        iaStatusIcon.Icon = FontAwesome.WPF.FontAwesomeIcon.CheckCircle;
                        iaStatusIcon.Foreground = new SolidColorBrush(Colors.Green);
                        break;
                    case iaStatusIconType.Error:
                        iaStatusIcon.Icon = FontAwesome.WPF.FontAwesomeIcon.TimesCircle;
                        iaStatusIcon.Foreground = new SolidColorBrush(Colors.Red);
                        break;
                    default:
                        iaStatusIcon.Icon = FontAwesome.WPF.FontAwesomeIcon.None;
                        break;
                }
            });
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

            // NEU: Custom Filename laden
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
            btnHeaderSettings.IsEnabled = enabled;
            btnHeaderGitHub.IsEnabled = enabled;
            btnConvert.IsEnabled = enabled;

            // Video
            combVideoQuality.IsEnabled = enabled && cbAudioOnly.IsChecked != true;
            combVideoFormat.IsEnabled = enabled && cbAudioOnly.IsChecked != true && cbVideoformat.IsChecked != true;

            // Audio-Bitrate
            combAudioBitrate.IsEnabled = enabled && cbAudioOnly.IsChecked == true;

            // NEU: Custom Filename
            cbCustomFilename.IsEnabled = enabled;
            tbCustomFilename.IsEnabled = enabled && cbCustomFilename.IsChecked == true;
            tbCustomFilename.IsReadOnly = !(enabled && cbCustomFilename.IsChecked == true);
        }

        private void SetUITexte()
        {
            btnHeaderSettings.Header = UITexte.UITexte.MainWindow_Button_Menu_Settings;
            btnHeaderChangeDownloadPath.Header = UITexte.UITexte.MainWindow_Button_Menu_ChangeDownloadPath;
            btnHeaderClose.Header = UITexte.UITexte.Button_Close;
            lblSoftwareinfo.Text = UITexte.UITexte.Softwareinfo;
            lblDownloadPathInfo.Content = UITexte.UITexte.MainWindow_Label_DownloadPathInfo;
            lblAudioPathInfo.Content = UITexte.UITexte.MainWindow_Label_AudioOnly_Info;
            lblURLInfo.Content = UITexte.UITexte.MainWindow_Label_URL;
            btnHistory.Content = UITexte.UITexte.MainWindow_Button_History;
            txtTimespanFrom.Text = UITexte.UITexte.MainWindow_Label_TimespanLeft;
            txtTimespanDash.Text = UITexte.UITexte.MainWindow_Label_TimespanMiddle;
            txtTimespanInfo.Text = UITexte.UITexte.MainWindow_Label_TimespanRight;
            ToolTipTimeSpan.Content = UITexte.UITexte.MainWindow_Button_Timespan_Info;
            txtFirstSecondsInfo1.Text = UITexte.UITexte.MainWindow_Label_TimeStartLeft;
            txtFirstSecondsInfo2.Text = UITexte.UITexte.MainWindow_Label_TimeStartRight;
            txtVideoformatInfo1.Text = UITexte.UITexte.MainWindow_Label_Videoformat;
            txtVideoformatInfo2.Text = UITexte.UITexte.MainWindow_Label_Videoformat_Info;
            txtAudioOnlyInfo.Text = UITexte.UITexte.MainWindow_Label_AudioOnly;
            // Entfernt: txtAudioOnlyInfo2 (Kontrollfeld existiert nicht mehr)
            btnDownloadStart.Content = UITexte.UITexte.MainWindow_Button_DownloadStart;
            btnDownloadCancel.Content = UITexte.UITexte.MainWindow_Button_DownloadAbort;
            btnSaveSettings.Content = UITexte.UITexte.MainWindow_Button_SettingsSave;
            expDebug.Header = UITexte.UITexte.MainWindow_DebugInfo;
            btnConvert.Header = UITexte.UITexte.MainWindow_Button_Convert;
            lblMainVersion.Content = Properties.Settings.Default.CurrentVersion;

            // Optional: Text für neue Felder lokalisieren, wenn vorhanden:
            // txtCustomFilenameInfo.Text = UITexte.UITexte.MainWindow_Label_CustomFilename;
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
        private void tbURL_GotFocus(object sender, RoutedEventArgs e) => tbURL.SelectAll();

        private void tbURL_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
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
            cbAudioOnly.IsEnabled = !v;

            // Neu: Container sperren, wenn Schnittmodus aktiv (mp4+x264)
            if (v)
            {
                // auf mp4 setzen und sperren
                SelectComboByContent(combVideoFormat, "mp4", "mp4");
                combVideoFormat.IsEnabled = false;
            }
            else
            {
                combVideoFormat.IsEnabled = cbAudioOnly.IsChecked != true;
            }
        }

        // NEU: Checkbox-Handler
        private void cbCustomFilenameCheck(object sender, RoutedEventArgs e)
        {
            CustomFilenameAdjustments();
            ValidateDownloadButton();
        }

        // NEU: UI-Enable/Disable für benutzerdefinierten Namen
        private void CustomFilenameAdjustments()
        {
            bool enabled = cbCustomFilename.IsChecked == true;
            tbCustomFilename.IsEnabled = enabled;
            tbCustomFilename.IsReadOnly = !enabled;
        }
    }
}