using Microsoft.Win32;
using MortysDLP.Models;
using MortysDLP.UITexte;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace MortysDLP.Views
{
    public partial class ConvertPage : Page
    {
        private readonly ObservableCollection<ConvertFileItem> _fileList = new();
        private CancellationTokenSource? _convertCancellationTokenSource;

        private const int MaxDebugLines = 200;

        // Fortschritts-Regex (kompiliert)
        private static readonly Regex FfmpegTimeRegex =
            new(@"time=(\d{2}:\d{2}:\d{2}\.\d{2})", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // Parallelitätsbegrenzung
        private readonly SemaphoreSlim _conversionLimiter =
            new(Math.Clamp(Environment.ProcessorCount / 2, 1, 4));

        // Cache für bereits ermittelte Metadaten (gleiche Quelldatei mehrfach)
        private readonly ConcurrentDictionary<string, (int? sr, int? ch, int? brKbps)> _audioMetaCache = new();

        public ConvertPage()
        {
            InitializeComponent();

            dgFiles.ItemsSource = _fileList;

            cbTargetFormat.ItemsSource = Enum.GetValues(typeof(VideoFormat))
                .Cast<VideoFormat>().Select(v => v.ToString().ToLower())
                .Concat(Enum.GetValues(typeof(AudioFormat))
                    .Cast<AudioFormat>()
                    .Select(a => a.ToString().ToLower()))
                .OrderBy(v => v)
                .ToList();

            cbTargetFormat.SelectedIndex = 0;
            btnConvertCancel.IsEnabled = false;
            UpdateFileButtonsState();
            ApplyDebugMode();
        }

        private void ConvertPage_Loaded(object sender, RoutedEventArgs e)
        {
            SetUITexts();
        }

        public void SetUITexts()
        {
            var T = UITextDictionary.Get;
            
            Title = T("MainWindow.Nav.Convert");
            
            // Section 1
            txtSection1Header.Text = T("ConvertPage.Section.SelectFiles");
            colFilename.Header = T("ConvertPage.DataGrid.Filename");
            colStatus.Header = T("ConvertPage.DataGrid.Status");
            colProgress.Header = T("ConvertPage.DataGrid.Progress");
            btnAddFiles.Content = T("ConvertPage.Button.AddFiles");
            btnRemoveFiles.Content = T("ConvertPage.Button.Remove");
            btnClearFiles.Content = T("ConvertPage.Button.ClearList");
            
            // Section 2
            txtSection2Header.Text = T("ConvertPage.Section.TargetFormat");
            lblTargetFormat.Content = T("ConvertPage.Label.TargetFormat");
            lblTargetFolder.Content = T("ConvertPage.Label.TargetFolder");
            btnBrowseTargetFolder.Content = T("ConvertPage.Button.Browse");
            btnOpenTargetFolder.Content = T("ConvertPage.Button.OpenFolder");
            txtUsePath.Text = T("ConvertPage.Label.UsePath");
            btnUseSavedDownloadpath.Content = T("ConvertPage.Button.DownloadPath");
            btnUseSavedAudioOnlyPath.Content = T("ConvertPage.Button.AudioOnlyPath");
            
            // Section 3
            txtSection3Header.Text = T("ConvertPage.Section.Quality");
            lblVideoQuality.Content = T("ConvertPage.Label.VideoQuality");
            lblAudioQuality.Content = T("ConvertPage.Label.AudioQuality");
            cbiVideoQualityOriginal.Content = T("ConvertPage.Quality.Original");
            cbiAudioQualityOriginal.Content = T("ConvertPage.Quality.Original");
            
            // Section 4
            btnConvertStart.Content = T("ConvertPage.Button.StartConversion");
            btnConvertCancel.Content = T("ConvertPage.Button.CancelConversion");
            
            // Section 5
            expDebug.Header = T("ConvertPage.Section.Debug");
        }

        public void ApplyDebugMode()
        {
            borderDebug.Visibility = Properties.Settings.Default.DebugMode ? Visibility.Visible : Visibility.Collapsed;
        }

        private void btnAddFiles_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = UITextDictionary.Get("ConvertPage.Dialog.FileFilter"),
                Multiselect = true
            };
            if (dlg.ShowDialog() == true)
            {
                AddFilesToList(dlg.FileNames);
            }
            UpdateFileButtonsState();
        }

        private void AddFilesToList(IEnumerable<string> files)
        {
            foreach (var file in files)
            {
                if (!_fileList.Any(f => f.SourcePath == file))
                    _fileList.Add(new ConvertFileItem { SourcePath = file });
            }
        }

        private void btnRemoveFiles_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgFiles.SelectedItems.Cast<ConvertFileItem>().ToList();
            foreach (var item in selected)
                _fileList.Remove(item);
            UpdateFileButtonsState();
        }

        private async void btnConvertStart_Click(object sender, RoutedEventArgs e)
        {
            tbDebugOutput.Clear();
            btnConvertStart.IsEnabled = false;
            btnConvertCancel.IsEnabled = true;

            string targetFolder = tbTargetFolder.Text;
            if (string.IsNullOrWhiteSpace(targetFolder) || !_fileList.Any())
            {
                MessageBox.Show(UITextDictionary.Get("ConvertPage.Message.NoTargetOrFiles"), 
                    UITextDictionary.Get("Common.Error"), 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateFileButtonsState();
                btnConvertCancel.IsEnabled = false;
                return;
            }
            if (!Directory.Exists(targetFolder))
            {
                try { Directory.CreateDirectory(targetFolder); }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format(UITextDictionary.Get("ConvertPage.Message.CannotCreateFolder"), ex.Message), 
                        UITextDictionary.Get("Common.Error"), 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    btnConvertStart.IsEnabled = true;
                    btnConvertCancel.IsEnabled = false;
                    return;
                }
            }

            _convertCancellationTokenSource = new CancellationTokenSource();
            var token = _convertCancellationTokenSource.Token;

            string selectedFormatStr = cbTargetFormat.SelectedItem?.ToString()?.ToLower() ?? "mp3";
            string ffmpegPath = Properties.Settings.Default.FfmpegPath;
            string ffprobePath = Properties.Settings.Default.FfprobePath;

            int successCount = 0;
            int failCount = 0;
            int canceledCount = 0;

            var tasks = _fileList.Select(async file =>
            {
                await _conversionLimiter.WaitAsync(token);
                try
                {
                    await ConvertSingleAsync(file, selectedFormatStr, ffmpegPath, ffprobePath, targetFolder, token);
                    var T = UITextDictionary.Get;
                    if (file.Status == T("ConvertPage.Status.Finished")) Interlocked.Increment(ref successCount);
                    else if (file.Status == T("ConvertPage.Status.Canceled")) Interlocked.Increment(ref canceledCount);
                    else if (file.Status == T("ConvertPage.Status.Error")) Interlocked.Increment(ref failCount);

                }
                finally
                {
                    _conversionLimiter.Release();
                }
            }).ToList();

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
                // globaler Abbruch
            }

            btnConvertCancel.IsEnabled = false;
            UpdateFileButtonsState();

            var T = UITextDictionary.Get;
            AppendDebugOutput(T("ConvertPage.Debug.Summary"));
            AppendDebugOutput(string.Format(T("ConvertPage.Debug.Successful"), successCount));
            AppendDebugOutput(string.Format(T("ConvertPage.Debug.Failed"), failCount));
            AppendDebugOutput(string.Format(T("ConvertPage.Debug.Canceled"), canceledCount));

            btnConvertStart.IsEnabled = true;
        }

        private async Task ConvertSingleAsync(
            ConvertFileItem file,
            string selectedFormatStr,
            string ffmpegPath,
            string ffprobePath,
            string targetFolder,
            CancellationToken token)
        {
            bool isVideoTarget = Enum.TryParse<VideoFormat>(selectedFormatStr, true, out var videoFormat);
            bool isAudioTarget = Enum.TryParse<AudioFormat>(selectedFormatStr, true, out var audioFormat);

            string videoQuality = cbVideoQuality.IsEnabled && cbVideoQuality.SelectedItem is ComboBoxItem vqItem
                ? vqItem.Content?.ToString() ?? ""
                : "";
            string audioQuality = cbAudioQuality.IsEnabled && cbAudioQuality.SelectedItem is ComboBoxItem aqItem
                ? aqItem.Content?.ToString() ?? ""
                : "";

            string qualitySuffix = "";
            if (isVideoTarget && cbVideoQuality.IsEnabled &&
                !string.IsNullOrWhiteSpace(videoQuality) &&
                !videoQuality.Contains("Original", StringComparison.OrdinalIgnoreCase))
            {
                qualitySuffix = $"_{videoQuality}";
            }
            else if (isAudioTarget && cbAudioQuality.IsEnabled &&
                     !string.IsNullOrWhiteSpace(audioQuality) &&
                     !audioQuality.Contains("Original", StringComparison.OrdinalIgnoreCase))
            {
                qualitySuffix = $"_{audioQuality}";
            }

            string extension = isVideoTarget ? videoFormat.ToString().ToLower() : audioFormat.ToString().ToLower();
            string destPath = Path.Combine(
                targetFolder,
                Path.GetFileNameWithoutExtension(file.SourcePath) + qualitySuffix + "." + extension
            );

            file.Status = UITextDictionary.Get("ConvertPage.Status.Converting");
            file.Progress = 0;
            Dispatcher.Invoke(() => dgFiles.Items.Refresh());

            if (File.Exists(destPath))
            {
                file.Status = UITextDictionary.Get("ConvertPage.Status.AlreadyConverted");
                file.Progress = 100;
                Dispatcher.Invoke(() => dgFiles.Items.Refresh());
                return;
            }

            // Metadaten nur einmal ermittlen und cachen
            var meta = _audioMetaCache.GetOrAdd(file.SourcePath, _ =>
            {
                var m = GetAudioStreamInfo(ffprobePath, file.SourcePath);
                AppendDebugOutput($"[{file.Name}] Quelle Audio: SR={m.sr?.ToString() ?? "?"}Hz Ch={m.ch?.ToString() ?? "?"} BR={m.brKbps?.ToString() ?? "?"}kbps");
                return m;
            });

            // ffmpeg Args zusammenbauen
            string args = BuildFfmpegArguments(
                sourcePath: file.SourcePath,
                destPath: destPath,
                isVideoTarget: isVideoTarget,
                extension: extension,
                videoQuality: videoQuality,
                audioQuality: audioQuality,
                meta,
                token);

            try
            {
                await RunFfmpegForItemAsync(file, ffmpegPath, args, token);
                var T = UITextDictionary.Get;
                if (file.Status != T("ConvertPage.Status.Canceled") && file.Status != T("ConvertPage.Status.Error"))
                {
                    file.Status = T("ConvertPage.Status.Finished");
                    file.Progress = 100;
                }
            }
            catch (OperationCanceledException)
            {
                file.Status = UITextDictionary.Get("ConvertPage.Status.Canceled");
            }
            catch (Exception ex)
            {
                file.Status = UITextDictionary.Get("ConvertPage.Status.Error");
                AppendDebugOutput($"[{file.Name}] Fehler: {ex.Message}");
            }
            Dispatcher.Invoke(() => dgFiles.Items.Refresh());
        }

        private string BuildFfmpegArguments(
            string sourcePath,
            string destPath,
            bool isVideoTarget,
            string extension,
            string videoQuality,
            string audioQuality,
            (int? sr, int? ch, int? brKbps) meta,
            CancellationToken token)
        {
            // Basis
            var args = $" -y -i \"{sourcePath}\"";

            // VIDEO
            if (isVideoTarget)
            {
                args += BuildVideoArgs(videoQuality);
                // Audio für Video-Container
                args += BuildAudioArgsForVideoContainer(meta, audioQuality, extension);
            }
            else
            {
                // AUDIO
                args += BuildAudioArgs(meta, extension, audioQuality);
            }

            args += $" \"{destPath}\"";
            return args;
        }

        private string BuildVideoArgs(string videoQuality)
        {
            if (string.IsNullOrWhiteSpace(videoQuality) ||
                videoQuality.Contains("Original", StringComparison.OrdinalIgnoreCase))
            {
                // Copy wenn keine Skalierung gewünscht
                return " -c:v copy";
            }

            // Erwartetes Format z.B. "1080p"
            string h = videoQuality.ToLower().Replace("p", "").Trim();
            if (!int.TryParse(h, out _))
            {
                // Fallback: copy
                return " -c:v copy";
            }

            // Skalierung + Re-Encode (libx264 Standard)
            // CRF/Preset könnten konfigurierbar gemacht werden
            return $" -vf scale=-2:{h} -c:v libx264 -preset medium -crf 20";
        }

        private string BuildAudioArgs(
            (int? sr, int? ch, int? brKbps) meta,
            string targetExt,
            string uiQuality)
        {
            bool isOriginal = string.IsNullOrWhiteSpace(uiQuality) ||
                              uiQuality.Equals("Original", StringComparison.OrdinalIgnoreCase);

            bool forceStereo = meta.ch == 1;
            bool upsample = !meta.sr.HasValue || meta.sr.Value < 44100;

            // Wenn Original & keine Anpassung nötig & container-kompatibel → copy
            if (isOriginal && !forceStereo && !upsample)
            {
                // Manche Formate besser neu encodieren (mp3/aac/opus/flac/wav) – hier encoden wir trotzdem,
                // um konsistente Endcodierung sicherzustellen.
                if (!(targetExt is "mp3" or "aac" or "m4a" or "opus" or "flac" or "wav"))
                    return " -c:a copy";
            }

            return BuildAudioEncodingLine(targetExt, uiQuality, forceStereo, upsample);
        }

        private string BuildAudioArgsForVideoContainer(
            (int? sr, int? ch, int? brKbps) meta,
            string uiQuality,
            string containerExt)
        {
            bool isOriginal = string.IsNullOrWhiteSpace(uiQuality) ||
                              uiQuality.Equals("Original", StringComparison.OrdinalIgnoreCase);

            bool forceStereo = meta.ch == 1;
            bool upsample = !meta.sr.HasValue || meta.sr.Value < 48000; // Für Video bevorzugt 48k

            if (isOriginal && !forceStereo && !upsample && meta.sr == 48000)
            {
                return " -c:a copy";
            }

            // AAC als Standard für mp4/mov/mkv Ausgaben (Kompatibilität)
            string bitrate = ResolveBitrate(uiQuality, "192k");
            return $" -c:a aac -b:a {bitrate}" +
                   (upsample ? " -ar 48000" : "") +
                   (forceStereo ? " -ac 2" : "");
        }

        private string BuildAudioEncodingLine(
            string targetExt,
            string uiQuality,
            bool forceStereo,
            bool upsample)
        {
            string codec;
            string bitrate = ResolveBitrate(uiQuality, "192k");
            string extra = "";
            bool vbrMode = false;

            if (uiQuality.StartsWith("vbr", StringComparison.OrdinalIgnoreCase))
                vbrMode = int.TryParse(uiQuality.Replace("vbr", ""), out _);

            switch (targetExt)
            {
                case "mp3":
                    codec = "libmp3lame";
                    if (vbrMode && int.TryParse(uiQuality.Replace("vbr", ""), out int v))
                        extra = $" -q:a {Math.Clamp(v, 0, 9)}";
                    else
                        extra = $" -b:a {bitrate}";
                    break;

                case "aac":
                case "m4a":
                    codec = "aac";
                    extra = $" -b:a {bitrate}";
                    break;

                case "opus":
                    codec = "libopus";
                    extra = $" -b:a {bitrate} -vbr on";
                    break;

                case "flac":
                    codec = "flac";
                    extra = " -compression_level 8";
                    break;

                case "wav":
                    codec = "pcm_s16le";
                    break;

                default:
                    codec = "aac";
                    extra = $" -b:a {bitrate}";
                    break;
            }

            string line = $" -c:a {codec}{extra}";
            if (forceStereo) line += " -ac 2";
            if (upsample) line += " -ar 48000";
            return line;
        }

        private string ResolveBitrate(string uiValue, string fallback)
        {
            if (string.IsNullOrWhiteSpace(uiValue) ||
                uiValue.Equals("Original", StringComparison.OrdinalIgnoreCase))
                return fallback;

            if (uiValue.EndsWith("k", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(uiValue[..^1], out _))
                return uiValue.ToLower();

            return uiValue.ToLower() switch
            {
                "low" => "96k",
                "medium" => "160k",
                "high" => "192k",
                "veryhigh" => "256k",
                _ => fallback
            };
        }

        private (int? sr, int? ch, int? brKbps) GetAudioStreamInfo(string ffprobePath, string filePath)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = ffprobePath,
                    Arguments = $"-v error -select_streams a:0 -show_entries stream=sample_rate,channels,bit_rate -of default=noprint_wrappers=1 \"{filePath}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                string output = proc!.StandardOutput.ReadToEnd();
                proc.WaitForExit(5000);

                int? sr = null;
                int? ch = null;
                int? br = null;

                foreach (var line in output.Split('\n', '\r', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (line.StartsWith("sample_rate=") &&
                        int.TryParse(line["sample_rate=".Length..].Trim(), out int v1)) sr = v1;
                    else if (line.StartsWith("channels=") &&
                             int.TryParse(line["channels=".Length..].Trim(), out int v2)) ch = v2;
                    else if (line.StartsWith("bit_rate=") &&
                             int.TryParse(line["bit_rate=".Length..].Trim(), out int v3)) br = v3 / 1000;
                }

                return (sr, ch, br);
            }
            catch
            {
                return (null, null, null);
            }
        }

        private async Task RunFfmpegForItemAsync(ConvertFileItem file, string ffmpegPath, string arguments, CancellationToken token)
        {
            string ffprobePath = Properties.Settings.Default.FfprobePath;
            double totalSeconds = GetMediaDurationInSeconds(ffprobePath, file.SourcePath) ?? 0;

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = arguments,
                    RedirectStandardError = true,
                    RedirectStandardOutput = false,
                    UseShellExecute = false,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;
                Dispatcher.Invoke(() =>
                {
                    AppendDebugOutput($"[{file.Name}] {e.Data}");
                    var percent = ParseFfmpegProgress(e.Data, totalSeconds);
                    if (percent.HasValue)
                    {
                        file.Progress = percent.Value;
                        dgFiles.Items.Refresh();
                    }
                });
            };

            AppendDebugOutput($"[{file.Name}] CMD: ffmpeg {arguments}");

            try
            {
                process.Start();
                process.BeginErrorReadLine();

                while (!process.HasExited)
                {
                    if (token.IsCancellationRequested)
                    {
                        try { process.Kill(true); } catch { }
                        throw new OperationCanceledException(token);
                    }
                    await Task.Delay(150, token);
                }

                if (process.ExitCode != 0 && !token.IsCancellationRequested)
                {
                    file.Status = UITextDictionary.Get("ConvertPage.Status.Error");
                    AppendDebugOutput($"[{file.Name}] ffmpeg ExitCode={process.ExitCode}");
                }
            }
            finally
            {
                try { process.CancelErrorRead(); } catch { }
            }
        }

        private double? ParseFfmpegProgress(string line, double totalSeconds)
        {
            if (totalSeconds <= 0) return null;
            var m = FfmpegTimeRegex.Match(line);
            if (!m.Success) return null;
            if (TimeSpan.TryParseExact(m.Groups[1].Value, @"hh\:mm\:ss\.ff", null, out var current))
            {
                double pct = (current.TotalSeconds / totalSeconds) * 100.0;
                return Math.Max(0, Math.Min(100, pct));
            }
            return null;
        }

        private double? GetMediaDurationInSeconds(string ffprobePath, string filePath)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = ffprobePath,
                    Arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{filePath}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var process = Process.Start(psi);
                string? output = process?.StandardOutput.ReadLine();
                process?.WaitForExit(5000);
                if (double.TryParse(output, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out double seconds))
                    return seconds;
            }
            catch { }
            return null;
        }

        private void btnConvertCancel_Click(object sender, RoutedEventArgs e)
        {
            _convertCancellationTokenSource?.Cancel();
            btnConvertCancel.IsEnabled = false;
        }

        private void btnClearFiles_Click(object sender, RoutedEventArgs e)
        {
            _fileList.Clear();
            UpdateFileButtonsState();
        }

        private void cbTargetFormat_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = cbTargetFormat.SelectedItem?.ToString()?.ToLower();
            cbVideoQuality.IsEnabled = Enum.TryParse<VideoFormat>(selected, true, out _);
            // Audio-Qualität (cbAudioQuality) bleibt unverändert aktiviert (falls vorhanden)
        }

        private void UpdateFileButtonsState()
        {
            bool hasFiles = _fileList.Any();
            btnRemoveFiles.IsEnabled = hasFiles;
            btnClearFiles.IsEnabled = hasFiles;
            btnConvertStart.IsEnabled = hasFiles;
        }

        private void AppendDebugOutput(string text)
        {
            tbDebugOutput.AppendText(text + Environment.NewLine);
            tbDebugOutput.ScrollToEnd();

            var lines = tbDebugOutput.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            if (lines.Length > MaxDebugLines)
            {
                tbDebugOutput.Text = string.Join(Environment.NewLine, lines.Skip(lines.Length - MaxDebugLines));
                tbDebugOutput.CaretIndex = tbDebugOutput.Text.Length;
                tbDebugOutput.ScrollToEnd();
            }
        }

        private void btnBrowseTargetFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog
            {
                Description = UITextDictionary.Get("ConvertPage.Dialog.FolderBrowser"),
                SelectedPath = tbTargetFolder.Text,
                UseDescriptionForTitle = true
            };
            if (dialog.ShowDialog(Window.GetWindow(this)) == true)
                tbTargetFolder.Text = dialog.SelectedPath;
        }

        private void btnUseSavedDownloadpath_Click(object sender, RoutedEventArgs e)
        {
            string savedPath = Properties.Settings.Default.DownloadPath;
            if (!string.IsNullOrWhiteSpace(savedPath) && Directory.Exists(savedPath))
                tbTargetFolder.Text = savedPath;
        }

        private void btnUseSavedAudioOnlyPath_Click(object sender, RoutedEventArgs e)
        {
            string savedAudioPath = Properties.Settings.Default.DownloadAudioOnlyPath;
            if (!string.IsNullOrWhiteSpace(savedAudioPath) && Directory.Exists(savedAudioPath))
                tbTargetFolder.Text = savedAudioPath;
        }

        private void btnOpenTargetFolder_Click(object sender, RoutedEventArgs e)
        {
            string folder = tbTargetFolder.Text;
            if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
            {
                Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
            }
            else
            {
                MessageBox.Show(UITextDictionary.Get("ConvertPage.Message.FolderNotExists"), 
                    UITextDictionary.Get("Common.Error"), 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void dgFiles_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void dgFiles_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    var mediaExtensions = new[] { ".mov", ".mp4", ".mkv", ".avi", ".mp3", ".aac", ".wav", ".flac", ".opus" };
                    var mediaFiles = files.Where(f => mediaExtensions.Contains(Path.GetExtension(f).ToLower())).ToList();
                    
                    if (mediaFiles.Any())
                    {
                        AddFilesToList(mediaFiles);
                        UpdateFileButtonsState();
                    }
                    
                    if (files.Length > mediaFiles.Count)
                    {
                        var T = UITextDictionary.Get;
                        MessageBox.Show(string.Format(T("ConvertPage.Message.IgnoredFiles"), files.Length - mediaFiles.Count), 
                            T("ConvertPage.Message.Info"), 
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
        }
    }
}