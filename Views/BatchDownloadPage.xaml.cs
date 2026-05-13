using MortysDLP.Helpers;
using MortysDLP.UITexte;
using MortysDLP.Views;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MortysDLP.Views
{
    internal enum BatchFinishState { Done, Canceled, PartialError, PartialCanceled }
    public class BatchDownloadEntry : INotifyPropertyChanged
    {
        private string _url = "";
        private string _title = "";
        private string _status = "";
        private double _progress = 0;
        private string _icon = "\uE73E"; // Default: MDL2 Wait Icon (or empty depends on your preference)
        private System.Windows.Media.Brush _iconColor = System.Windows.Media.Brushes.Gray;

        public string Url
        {
            get => _url;
            set { _url = value; OnPropertyChanged(); }
        }
        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(); }
        }
        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }
        public double Progress
        {
            get => _progress;
            set { _progress = value; OnPropertyChanged(); }
        }
        public string Icon
        {
            get => _icon;
            set { _icon = value; OnPropertyChanged(); }
        }
        public System.Windows.Media.Brush IconColor
        {
            get => _iconColor;
            set { _iconColor = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public partial class BatchDownloadPage : Page
    {
        private readonly ObservableCollection<BatchDownloadEntry> _entries = new();
        private CancellationTokenSource? _cts;
        private bool _initialized = false;
        private string _lastDownloadPath = "";
        private bool _downloadRunning = false;

        public BatchDownloadPage()
        {
            InitializeComponent();
            dgUrls.ItemsSource = _entries;
            _entries.CollectionChanged += (_, _) => UpdateActionButtons();
        }

        private void BatchDownloadPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (_initialized)
            {
                SetUITexts();
                RefreshPaths();
                return;
            }
            _initialized = true;
            SetUITexts();
            RefreshPaths();
            AudioOnlyAdjustments();
            ApplyDebugMode();
        }

        private void UpdateActionButtons()
        {
            bool hasEntries = _entries.Count > 0;
            bool hasSelection = dgUrls.SelectedItems.Count > 0;

            btnAddUrl.IsEnabled = !_downloadRunning && IsValidUrl(tbAddUrl.Text.Trim());
            btnStartAll.IsEnabled = !_downloadRunning && hasEntries;
            btnClearList.IsEnabled = !_downloadRunning && hasEntries;
            btnRequeueDone.IsEnabled = !_downloadRunning && hasEntries && hasSelection;
            btnRemoveSelected.IsEnabled = !_downloadRunning && hasEntries && hasSelection;
        }

        internal void RefreshPaths()
        {
            lblDownloadPath.Content = Properties.Settings.Default.DownloadPath;
            lblAudioPath.Content    = Properties.Settings.Default.DownloadAudioOnlyPath;
            bool hasAudioPath = Properties.Settings.Default.CheckedAudioOnlyPath;
            dpAudioPath.Visibility  = hasAudioPath ? Visibility.Visible : Visibility.Collapsed;
        }

        public void ApplyDebugMode()
        {
            expDebug.Visibility = Properties.Settings.Default.DebugMode
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public void SetUITexts()
        {
            var T = UITextDictionary.Get;

            txtSectionDownloadPaths.Text   = T("DownloadPage.Section.DownloadPaths");
            lblDownloadPathInfo.Content    = T("DownloadPage.Label.DownloadPath");
            lblAudioPathInfo.Content       = T("DownloadPage.Label.AudioOnlyPath");
            btnChangeDownloadPath.Content  = T("DownloadPage.Button.ChangePath");

            txtSectionUrlList.Text         = T("BatchDownloadPage.Section.UrlList");
            lblAddUrl.Content              = T("BatchDownloadPage.Label.AddUrl");
            txtUrlHint.Text                = T("BatchDownloadPage.Label.UrlHint");
            btnAddUrl.Content              = T("BatchDownloadPage.Button.AddUrl");
            btnAddBatchUrl.Content         = T("BatchDownloadPage.Button.AddBatchUrls");
            btnHistory.Content             = T("BatchDownloadPage.Button.History");
            btnRequeueDone.Content         = T("BatchDownloadPage.Button.RequeueDone");
            btnRemoveSelected.Content      = T("BatchDownloadPage.Button.RemoveSelected");
            btnClearList.Content           = T("BatchDownloadPage.Button.ClearList");
            colUrl.Header                  = T("BatchDownloadPage.Column.Url");
            colTitle.Header                = T("BatchDownloadPage.Column.Title");
            colStatus.Header               = T("BatchDownloadPage.Column.Status");
            colProgress.Header             = T("BatchDownloadPage.Column.Progress");

            // Kontextmenü-Header (MenuItem liegt in eigenem NameScope)
            if (dgUrls.ContextMenu?.Items.Count >= 3)
            {
                if (dgUrls.ContextMenu.Items[0] is MenuItem miRequeue)  miRequeue.Header  = T("BatchDownloadPage.Context.Requeue");
                if (dgUrls.ContextMenu.Items[1] is MenuItem miSelected) miSelected.Header = T("BatchDownloadPage.Context.DownloadSelected");
                if (dgUrls.ContextMenu.Items[3] is MenuItem miRemove)   miRemove.Header   = T("BatchDownloadPage.Context.Remove");
            }

            txtSectionOptions.Text         = T("BatchDownloadPage.Section.Options");
            txtAudioOnlyInfo.Text          = T("DownloadPage.Label.AudioOnly");
            txtBitrateLabel.Text           = T("DownloadPage.Label.Bitrate");
            txtVideoQuality.Text           = T("DownloadPage.Label.VideoQuality");
            txtVideoContainer.Text         = T("DownloadPage.Label.VideoContainer");
            cbiAudioBitrateHighest.Content = T("DownloadPage.Quality.Highest");
            cbiVideoQualityHighest.Content = T("DownloadPage.Quality.Highest");

            var videoFormatText = T("DownloadPage.Label.VideoFormat");
            var parts = videoFormatText.Split('(');
            txtVideoformatInfo1.Text = parts[0].Trim();
            txtVideoformatInfo2.Text = parts.Length > 1 ? "(" + parts[1] : "";

            btnStartAll.Content            = T("BatchDownloadPage.Button.StartAll");
            btnCancelAll.Content           = T("BatchDownloadPage.Button.CancelAll");
            expDebug.Header                = T("DownloadPage.Section.Debug");
        }

        // ── Download-Pfad ────────────────────────────────────────────────

        private void btnChangeDownloadPath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new DownloadPathDialog { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() == true)
                RefreshPaths();
        }

        private void DownloadPath_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is not Label label) return;
            string? path = label.Content?.ToString();
            if (!string.IsNullOrWhiteSpace(path) && System.IO.Directory.Exists(path))
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            else
                FluentMessageBox.Show(
                    string.Format(UITexte.UITexte.MainWindow_Label_Click_DownloadPathNotFound, path),
                    icon: MessageBoxImage.Error);
        }

        // ── URL hinzufügen ───────────────────────────────────────────────

        private void tbAddUrl_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && btnAddUrl.IsEnabled) AddCurrentUrl();
        }

        private void btnAddUrl_Click(object sender, RoutedEventArgs e) => AddCurrentUrl();

        private void btnAddBatchUrl_Click(object sender, RoutedEventArgs e)
        {
            var win = new AddBatchURLsWindow
            {
                Owner = Window.GetWindow(this)   // liefert das MainWindow, das die Page hostet
            };

            if (win.ShowDialog() == true)
            {
                List<string> urls = win.ValidUrls;
                foreach (var url in urls)
                {
                    AddCurrentUrl(url);
                }
            }
        }

        private void tbAddUrl_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Remove error border when user types
            tbAddUrl.ClearValue(Control.BorderBrushProperty);
            tbAddUrl.ClearValue(FrameworkElement.ToolTipProperty);
            txtUrlHint.Visibility = Visibility.Hidden;
            UpdateActionButtons();
        }

        private void AddCurrentUrl(string? presetUrl = null)
        {
            bool fromUi = string.IsNullOrWhiteSpace(presetUrl);
            string url = fromUi ? tbAddUrl.Text.Trim() : presetUrl!.Trim();

            // 1) Validierung
            if (!IsValidUrl(url))
                return;

            // 2) Duplikat-Prüfung
            if (IsDuplicate(url))
            {
                if (fromUi)
                {
                    // Inline-Hinweis
                    //txtUrlHint.Text = UITextDictionary.Get("BatchDownloadPage.Label.UrlHint");
                    txtUrlHint.Visibility = Visibility.Visible;
                    tbAddUrl.BorderBrush = System.Windows.Media.Brushes.Orange;
                }
                return;
            }

            // 3) Eintrag anlegen
            var entry = new BatchDownloadEntry
            {
                Url = url,
                Title = "...",
                Status = UITextDictionary.Get("BatchDownloadPage.Status.Waiting"),
                Icon = "\uE118",
                IconColor = System.Windows.Media.Brushes.Gray
            };
            _entries.Add(entry);

            if (fromUi)
            {
                tbAddUrl.ClearValue(Control.BorderBrushProperty);
                tbAddUrl.ClearValue(FrameworkElement.ToolTipProperty);
                tbAddUrl.Clear();
            }

            _ = FetchTitleAsync(entry);
        }

        private static bool IsValidUrl(string url)
        {
            return !string.IsNullOrWhiteSpace(url)
                && Uri.TryCreate(url, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        private bool IsDuplicate(string url)
        {
            return _entries.Any(e =>
                string.Equals(e.Url?.Trim(), url, StringComparison.OrdinalIgnoreCase));
        }

        private async Task FetchTitleAsync(BatchDownloadEntry entry)
        {
            try
            {
                string ytDlpPath = Properties.Settings.Default.YtdlpPath;
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName               = ytDlpPath,
                        Arguments              = $"--no-check-certificates --print \"%(title)s\" \"{entry.Url}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError  = true,
                        UseShellExecute        = false,
                        CreateNoWindow         = true,
                        StandardOutputEncoding = System.Text.Encoding.UTF8,
                    }
                };
                process.Start();
                string output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                string title = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                     .FirstOrDefault() ?? "";

                entry.Title = string.IsNullOrWhiteSpace(title) ? entry.Url : title;
            }
            catch
            {
                entry.Title = entry.Url;
            }
        }

        // ── Verlauf ──────────────────────────────────────────────────────

        private void btnHistory_Click(object sender, RoutedEventArgs e)
        {
            var win = new DownloadHistoryWindow(addToBatchCallback: url =>
            {
                Dispatcher.Invoke(() =>
                {
                    var entry = new BatchDownloadEntry
                    {
                        Url    = url,
                        Title  = "...",
                        Status = UITextDictionary.Get("BatchDownloadPage.Status.Waiting")
                    };
                    AddCurrentUrl(entry.Url);
                });
            })
            { Owner = Window.GetWindow(this) };
            win.ShowDialog();
        }

        // ── Liste verwalten ──────────────────────────────────────────────

        private void btnRemoveSelected_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgUrls.SelectedItems.Cast<BatchDownloadEntry>().ToList();
            foreach (var entry in selected) _entries.Remove(entry);
        }

        private void btnClearList_Click(object sender, RoutedEventArgs e)
        {
            if (_entries.Count == 0) return;

            var T = UITextDictionary.Get;
            var result = FluentMessageBox.Show(
                T("BatchDownloadPage.ClearList.Question"),
                T("BatchDownloadPage.ClearList.Title"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                owner: Window.GetWindow(this));

            if (result == MessageBoxResult.Yes)
                _entries.Clear();
        }

        private void btnRequeueDone_Click(object sender, RoutedEventArgs e)
        {
            string doneStatus   = UITextDictionary.Get("BatchDownloadPage.Status.Done");
            string waitStatus   = UITextDictionary.Get("BatchDownloadPage.Status.Waiting");
            string errorStatus  = UITextDictionary.Get("BatchDownloadPage.Status.Error");
            string cancelStatus = UITextDictionary.Get("BatchDownloadPage.Status.Canceled");

            foreach (var entry in _entries)
            {
                if (entry.Status == doneStatus || entry.Status == errorStatus || entry.Status == cancelStatus)
                {
                    entry.Status   = waitStatus;
                    entry.Progress = 0;
                    entry.Icon = "\uE118";
                    entry.IconColor = System.Windows.Media.Brushes.Gray;
                }
            }
        }

        // Kontextmenü: Ausgewählte zurück in Warteschlange
        private void ctxRequeue_Click(object sender, RoutedEventArgs e)
        {
            string waitStatus = UITextDictionary.Get("BatchDownloadPage.Status.Waiting");
            foreach (var entry in dgUrls.SelectedItems.Cast<BatchDownloadEntry>().ToList())
            {
                entry.Status   = waitStatus;
                entry.Progress = 0;
                entry.Icon = "\uE118";
                entry.IconColor = System.Windows.Media.Brushes.Gray;
            }
        }

        // Kontextmenü: Nur ausgewählte herunterladen
        private async void ctxDownloadSelected_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgUrls.SelectedItems.Cast<BatchDownloadEntry>().ToList();
            if (selected.Count == 0) return;
            await RunBatchDownload(selected);
        }

        private void dgUrls_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Delete) return;
            if (dgUrls.SelectedItems.Count == 0) return;

            // Kopie ziehen, sonst InvalidOperationException beim Modifizieren während Iteration
            var toRemove = dgUrls.SelectedItems.Cast<BatchDownloadEntry>().ToList();
            foreach (var item in toRemove)
                _entries.Remove(item);

            e.Handled = true;
        }

        private void dgUrls_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateActionButtons();
        }


        // ── Optionen ─────────────────────────────────────────────────────

        private void cbAudioOnlyCheck(object sender, RoutedEventArgs e) => AudioOnlyAdjustments();
        private void cbVideoFormatCheck(object sender, RoutedEventArgs e) => VideoformatAdjustments();

        private void AudioOnlyAdjustments()
        {
            bool audioOnly = cbAudioOnly.IsChecked == true;
            combAudioFormat.IsEnabled    = audioOnly;
            combAudioBitrate.IsEnabled   = audioOnly;
            combVideoQuality.IsEnabled   = !audioOnly;
            combVideoFormat.IsEnabled    = !audioOnly && cbVideoformat.IsChecked != true;
            cbVideoformat.IsEnabled      = !audioOnly;
            txtVideoQuality.IsEnabled    = !audioOnly;
            txtVideoContainer.IsEnabled  = !audioOnly;
            txtVideoformatInfo1.IsEnabled = !audioOnly;
            txtVideoformatInfo2.IsEnabled = !audioOnly;
            if (audioOnly) cbVideoformat.IsChecked = false;
        }

        private void VideoformatAdjustments()
        {
            bool x264 = cbVideoformat.IsChecked == true;
            // x264-Modus erzwingt mp4 -> Container-Auswahl deaktivieren
            combVideoFormat.IsEnabled = !x264 && cbAudioOnly.IsChecked != true;
        }

        // ── Download ─────────────────────────────────────────────────────

        private async void btnStartAll_Click(object sender, RoutedEventArgs e)
        {
            // Nur Einträge die noch nicht fertig sind starten; wenn alle fertig → alle nochmal
            var toRun = _entries
                .Where(en => en.Status == UITextDictionary.Get("BatchDownloadPage.Status.Waiting")
                          || en.Status == UITextDictionary.Get("BatchDownloadPage.Status.Error")
                          || en.Status == UITextDictionary.Get("BatchDownloadPage.Status.Canceled"))
                .ToList();
            if (toRun.Count == 0) return; // Alle fertig → nichts zu tun
            await RunBatchDownload(toRun);
        }

        private async Task RunBatchDownload(List<BatchDownloadEntry> toRun)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            SetDownloadUILocked(true);
            spOverallProgress.Visibility = Visibility.Visible;
            btnOpenFolder.Visibility     = Visibility.Collapsed;

            bool audioOnly = cbAudioOnly.IsChecked == true;
            bool useX264   = cbVideoformat.IsChecked == true;

            string downloadPath = audioOnly
                ? Properties.Settings.Default.DownloadAudioOnlyPath
                : Properties.Settings.Default.DownloadPath;
            _lastDownloadPath = downloadPath;

            string ytDlpPath       = Properties.Settings.Default.YtdlpPath;
            string audioFormat     = (combAudioFormat.SelectedItem    as ComboBoxItem)?.Content?.ToString() ?? "mp3";
            string audioBitrate    = (combAudioBitrate.SelectedItem   as ComboBoxItem)?.Content?.ToString() ?? "192k";
            string videoQualityTag = (combVideoQuality.SelectedItem   as ComboBoxItem)?.Tag?.ToString()    ?? "best";
            string videoFormat     = (combVideoFormat.SelectedItem    as ComboBoxItem)?.Content?.ToString() ?? "mp4";
            string highestLabel    = UITextDictionary.Get("DownloadPage.Quality.Highest");
            bool isHighestAbr      = audioBitrate.Equals(highestLabel, StringComparison.OrdinalIgnoreCase);

            int total     = toRun.Count;
            int completed = 0;
            int errors    = 0;
            bool canceled = false;

            UpdateOverall(0, total, isRunning: true);

            foreach (var entry in toRun)
            {
                if (token.IsCancellationRequested)
                {
                    canceled = true;
                    entry.Status = UITextDictionary.Get("BatchDownloadPage.Status.Waiting");
                    entry.Icon = "\uE118";
                    entry.IconColor = System.Windows.Media.Brushes.Gray;
                    continue;
                }

                entry.Status   = UITextDictionary.Get("BatchDownloadPage.Status.Downloading");
                entry.Icon = "\uE896"; // Download loading
                entry.IconColor = (System.Windows.Media.Brush)FindResource("BrandOrangeBrush") ?? System.Windows.Media.Brushes.Orange;
                entry.Progress = 0;
                UpdateOverall(completed, total, isRunning: true);

                try
                {
                    await RunDownloadAsync(
                        entry, ytDlpPath, downloadPath,
                        audioOnly, useX264, audioFormat, audioBitrate, isHighestAbr,
                        videoQualityTag, videoFormat, token);

                    entry.Status   = UITextDictionary.Get("BatchDownloadPage.Status.Done");
                    entry.Progress = 100;
                    entry.Icon = "\uE001"; // Checkmark
                    entry.IconColor = System.Windows.Media.Brushes.MediumSeaGreen;
                    completed++;
                }
                catch (OperationCanceledException)
                {
                    canceled = true;
                    entry.Status   = UITextDictionary.Get("BatchDownloadPage.Status.Canceled");
                    entry.Progress = 0;
                    entry.Icon = "\uE711"; // Cancel
                    entry.IconColor = System.Windows.Media.Brushes.Orange;
                }
                catch (Exception ex)
                {
                    errors++;
                    completed++;
                    entry.Status   = UITextDictionary.Get("BatchDownloadPage.Status.Error");
                    entry.Progress = 0;
                    entry.Icon = "\uEA39"; // Error/Warning
                    entry.IconColor = System.Windows.Media.Brushes.Red;
                    AppendDebug($"[ERROR] {entry.Url}: {ex.Message}");
                }
            }

            BatchFinishState state;
            if (canceled && completed > 0)
                state = BatchFinishState.PartialCanceled;
            else if (canceled)
                state = BatchFinishState.Canceled;
            else if (errors > 0)
                state = BatchFinishState.PartialError;
            else
                state = BatchFinishState.Done;

            UpdateOverall(completed - errors, total, isRunning: false, state);
            SetDownloadUILocked(false);

            if (completed > 0 && System.IO.Directory.Exists(_lastDownloadPath))
                btnOpenFolder.Visibility = Visibility.Visible;
        }

        /// <summary>Sperrt/entsperrt alle Steuerelemente während eines aktiven Downloads.</summary>
        private void SetDownloadUILocked(bool locked)
        {
            _downloadRunning = locked;

            btnCancelAll.IsEnabled = locked;
            tbAddUrl.IsEnabled = !locked;
            btnHistory.IsEnabled = !locked;
            btnChangeDownloadPath.IsEnabled = !locked;

            // Optionen sperren
            cbAudioOnly.IsEnabled = !locked;
            combAudioFormat.IsEnabled = !locked && cbAudioOnly.IsChecked == true;
            combAudioBitrate.IsEnabled = !locked && cbAudioOnly.IsChecked == true;
            cbVideoformat.IsEnabled = !locked && cbAudioOnly.IsChecked != true;
            combVideoQuality.IsEnabled = !locked && cbAudioOnly.IsChecked != true;
            combVideoFormat.IsEnabled = !locked && cbAudioOnly.IsChecked != true && cbVideoformat.IsChecked != true;

            // Alle Aktions-Buttons gehen über den zentralen Helper
            UpdateActionButtons();
        }

        private void btnCancelAll_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();

        private void btnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(_lastDownloadPath) && System.IO.Directory.Exists(_lastDownloadPath))
                Process.Start(new ProcessStartInfo { FileName = _lastDownloadPath, UseShellExecute = true });
        }

        private async Task RunDownloadAsync(
            BatchDownloadEntry entry, string ytDlpPath, string downloadPath,
            bool audioOnly, bool useX264,
            string audioFormat, string audioBitrate, bool isHighestAbr,
            string videoQualityTag, string videoFormat,
            CancellationToken token)
        {
            var args = new System.Text.StringBuilder();

            if (audioOnly)
            {
                args.Append($"-x --audio-format \"{audioFormat}\" ");
                if (!isHighestAbr) args.Append($"--audio-quality \"{audioBitrate.ToUpperInvariant()}\" ");
            }
            else if (useX264)
            {
                string fSelector = videoQualityTag == "best"
                    ? "bestvideo[vcodec^=avc1]+bestaudio[ext=m4a]/bestvideo+bestaudio/best"
                    : $"bestvideo[vcodec^=avc1][height<={videoQualityTag}]+bestaudio[ext=m4a]/bestvideo[height<={videoQualityTag}]+bestaudio/best[height<={videoQualityTag}]";
                args.Append($"-f \"{fSelector}\" ");
                args.Append("--merge-output-format mp4 ");
                args.Append("--postprocessor-args \"Merger:-c copy -movflags +faststart\" ");
            }
            else
            {
                string fSelector = videoQualityTag == "best"
                    ? $"bestvideo[ext={videoFormat}]+bestaudio/bestvideo+bestaudio/best"
                    : $"bestvideo[ext={videoFormat}][height<={videoQualityTag}]+bestaudio/bestvideo[height<={videoQualityTag}]+bestaudio/best[height<={videoQualityTag}]";
                args.Append($"-f \"{fSelector}\" --merge-output-format {videoFormat} ");
            }

            args.Append($"-o \"{downloadPath}\\%(title)s_%(id)s.%(ext)s\" ");
            args.Append("--no-check-certificates --no-mtime --newline --no-playlist ");
            args.Append($"\"{entry.Url}\"");

            AppendDebug($"[START] {entry.Url}");
            AppendDebug($"ARGS: {args}");

            string? lastOutputFile = null;

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName               = ytDlpPath,
                    Arguments              = args.ToString(),
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding  = System.Text.Encoding.UTF8,
                }
            };

            process.OutputDataReceived += (_, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;
                AppendDebug(e.Data);

                // Ausgabedatei tracken
                if (e.Data.StartsWith("[Merger] Merging formats into \""))
                {
                    var m = System.Text.RegularExpressions.Regex.Match(e.Data, @"\[Merger\] Merging formats into ""(.+)""");
                    if (m.Success) lastOutputFile = m.Groups[1].Value;
                }
                else if (e.Data.StartsWith("[download] Destination: "))
                    lastOutputFile = e.Data["[download] Destination: ".Length..].Trim();

                // Phase erkennen → entry.Status
                var phase = DetectBatchStage(e.Data);
                if (phase != null) entry.Status = phase;

                // Fortschritt + Geschwindigkeit
                var (pct, speed) = ParseBatchProgress(e.Data);
                if (pct.HasValue)
                {
                    entry.Progress = pct.Value;
                    UpdateCurrentSpeed(speed);
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data)) AppendDebug($"[ERR] {e.Data}");
            };

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await using var reg = token.Register(() => { try { process.Kill(entireProcessTree: true); } catch { } });

                await process.WaitForExitAsync(CancellationToken.None);
                process.WaitForExit();

                if (token.IsCancellationRequested) throw new OperationCanceledException(token);
                if (process.ExitCode != 0) throw new Exception($"yt-dlp exit code {process.ExitCode}");
            }
            finally
            {
                process.Dispose();
            }

            // x264-Nachbearbeitung mit eigenem Fortschritt
            if (useX264 && !audioOnly && lastOutputFile != null && System.IO.File.Exists(lastOutputFile))
            {
                await RunBatchFfmpegConvertAsync(entry, lastOutputFile, token);
            }
        }

        /// <summary>Erkennt die aktuelle yt-dlp Verarbeitungsphase für Batch-Einträge.</summary>
        private string? DetectBatchStage(string line) =>
            line switch
            {
                _ when line.StartsWith("[Merger]")
                    => UITextDictionary.Get("DownloadPage.Status.Merging"),
                _ when line.StartsWith("[ExtractAudio]")
                    => UITextDictionary.Get("DownloadPage.Status.ExtractingAudio"),
                _ when line.StartsWith("[VideoConvertor]") || line.StartsWith("[VideoRemuxer]")
                    => UITextDictionary.Get("DownloadPage.Status.Converting"),
                _ when line.StartsWith("[download]") && line.Contains('%')
                    => UITextDictionary.Get("BatchDownloadPage.Status.Downloading"),
                _ => null
            };

        /// <summary>Parst Fortschritt (%) und Geschwindigkeit aus einer yt-dlp Ausgabezeile.</summary>
        private static (double? Progress, double? SpeedMBs) ParseBatchProgress(string line)
        {
            if (!line.StartsWith("[download]")) return (null, null);

            double? speed = null;
            var speedMatch = System.Text.RegularExpressions.Regex.Match(line, @"at\s+([\d.]+)(KiB|MiB|GiB)/s");
            if (speedMatch.Success && double.TryParse(speedMatch.Groups[1].Value,
                System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double s))
            {
                speed = speedMatch.Groups[2].Value switch
                {
                    "KiB" => s / 1024.0,
                    "GiB" => s * 1024.0,
                    _     => s
                };
            }

            var pctMatch = System.Text.RegularExpressions.Regex.Match(line, @"^\[download\]\s+([\d.]+)%");
            if (pctMatch.Success && double.TryParse(pctMatch.Groups[1].Value,
                System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double pct))
                return (pct, speed);

            return (null, speed);
        }

        private void UpdateCurrentSpeed(double? speedMBs)
        {
            Dispatcher.Invoke(() =>
            {
                txtCurrentSpeed.Text = speedMBs.HasValue && speedMBs.Value > 0
                    ? $"{speedMBs.Value:F2} MB/s"
                    : "";
            });
        }

        /// <summary>Konvertiert nach dem Download zu H.264 via ffmpeg und zeigt Fortschritt am Eintrag.</summary>
        private async Task RunBatchFfmpegConvertAsync(BatchDownloadEntry entry, string filePath, CancellationToken token)
        {
            string ffmpegPath  = Properties.Settings.Default.FfmpegPath;
            string ffprobePath = Properties.Settings.Default.FfprobePath;

            entry.Status   = UITextDictionary.Get("DownloadPage.Status.CheckingCodec");
            entry.Progress = 0;
            UpdateCurrentSpeed(null);

            // Codec prüfen
            var (codec, w, h) = await GetBatchVideoStreamInfoAsync(ffprobePath, filePath);
            if (codec != null && (codec.StartsWith("h264", StringComparison.OrdinalIgnoreCase)
                               || codec.StartsWith("avc",  StringComparison.OrdinalIgnoreCase)))
            {
                AppendDebug($"[SCHNITT] Bereits H.264 – keine Konvertierung nötig.");
                return;
            }

            entry.Status = UITextDictionary.Get("DownloadPage.Status.DetectingEncoder");
            string encoder = await HwAccelHelper.DetectBestH264EncoderAsync(ffmpegPath, w, h);
            AppendDebug($"[SCHNITT] Encoder: {HwAccelHelper.GetEncoderDisplayName(encoder)}");

            entry.Status   = UITextDictionary.Get("DownloadPage.Status.ConvertingH264");
            entry.Progress = 0;

            // Gesamtdauer ermitteln
            double totalSec = await GetBatchMediaDurationAsync(ffprobePath, filePath);

            string dir      = System.IO.Path.GetDirectoryName(filePath)!;
            string tempPath = System.IO.Path.Combine(dir,
                System.IO.Path.GetFileNameWithoutExtension(filePath) + "_h264_tmp" + System.IO.Path.GetExtension(filePath));

            string ffmpegArgs = HwAccelHelper.BuildH264Args(encoder, filePath, tempPath);
            AppendDebug($"[ffmpeg] {ffmpegPath} {ffmpegArgs}");

            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName              = ffmpegPath,
                    Arguments             = ffmpegArgs,
                    RedirectStandardError = true,
                    UseShellExecute       = false,
                    CreateNoWindow        = true,
                }
            };

            proc.ErrorDataReceived += (_, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;
                AppendDebug($"[ffmpeg] {e.Data}");
                if (totalSec > 0)
                {
                    var m = System.Text.RegularExpressions.Regex.Match(e.Data, @"time=(\d{2}:\d{2}:\d{2}\.\d{2})");
                    if (m.Success && TimeSpan.TryParse(m.Groups[1].Value, out var cur))
                    {
                        double pct = Math.Max(0, Math.Min(100, cur.TotalSeconds / totalSec * 100.0));
                        entry.Progress = pct;
                        UpdateOverallBar(pct);
                    }
                }
            };

            try
            {
                proc.Start();
                proc.BeginErrorReadLine();

                await using var reg = token.Register(() => { try { proc.Kill(entireProcessTree: true); } catch { } });
                await proc.WaitForExitAsync(CancellationToken.None);
                proc.WaitForExit();

                if (token.IsCancellationRequested) throw new OperationCanceledException(token);
                if (proc.ExitCode != 0) throw new Exception($"ffmpeg exit code {proc.ExitCode}");

                if (System.IO.File.Exists(tempPath))
                {
                    System.IO.File.Delete(filePath);
                    System.IO.File.Move(tempPath, filePath);
                    AppendDebug($"[SCHNITT] Fertig: {System.IO.Path.GetFileName(filePath)}");
                }
            }
            finally
            {
                try { proc.CancelErrorRead(); } catch { }
            }
        }

        private async Task<(string? Codec, int Width, int Height)> GetBatchVideoStreamInfoAsync(string ffprobePath, string filePath)
        {
            try
            {
                using var p = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName               = ffprobePath,
                        Arguments              = $"-v error -select_streams v:0 -show_entries stream=codec_name,width,height -of csv=p=0 \"{filePath}\"",
                        RedirectStandardOutput = true,
                        UseShellExecute        = false,
                        CreateNoWindow         = true,
                    }
                };
                p.Start();
                string output = await p.StandardOutput.ReadToEndAsync();
                await p.WaitForExitAsync();
                var parts = output.Trim().Split(',');
                string? codec = parts.Length > 0 ? parts[0] : null;
                int.TryParse(parts.Length > 1 ? parts[1] : "", out int w);
                int.TryParse(parts.Length > 2 ? parts[2] : "", out int h);
                return (codec, w, h);
            }
            catch { return (null, 0, 0); }
        }

        private async Task<double> GetBatchMediaDurationAsync(string ffprobePath, string filePath)
        {
            try
            {
                using var p = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName               = ffprobePath,
                        Arguments              = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{filePath}\"",
                        RedirectStandardOutput = true,
                        UseShellExecute        = false,
                        CreateNoWindow         = true,
                    }
                };
                p.Start();
                string output = await p.StandardOutput.ReadToEndAsync();
                await p.WaitForExitAsync();
                if (double.TryParse(output.Trim(), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double d))
                    return d;
            }
            catch { }
            return 0;
        }

        /// <summary>Aktualisiert nur den Gesamtfortschrittsbalken ohne die Zähler-Texte zu ändern (für ffmpeg-Phase).</summary>
        private void UpdateOverallBar(double pct)
        {
            Dispatcher.Invoke(() => pbOverall.Value = pct);
        }

        // ── Hilfsfunktionen ──────────────────────────────────────────────

        private void UpdateOverall(int done, int total, bool isRunning = true,
                                    BatchFinishState state = BatchFinishState.Done)
        {
            Dispatcher.Invoke(() =>
            {
                txtOverallCount.Text   = $"{done}/{total}";
                pbOverall.Value        = total > 0 ? done * 100.0 / total : 0;
                txtOverallPercent.Text = total > 0 ? $"{done * 100 / total} %" : "";

                if (isRunning)
                {
                    txtOverallStatus.Text = UITextDictionary.Get("BatchDownloadPage.Status.Downloading");
                }
                else
                {
                    txtOverallStatus.Text = state switch
                    {
                        BatchFinishState.Done           => UITextDictionary.Get("BatchDownloadPage.Status.Done"),
                        BatchFinishState.Canceled       => UITextDictionary.Get("BatchDownloadPage.Status.Canceled"),
                        BatchFinishState.PartialCanceled => UITextDictionary.Get("BatchDownloadPage.Status.PartialCanceled"),
                        BatchFinishState.PartialError    => UITextDictionary.Get("BatchDownloadPage.Status.PartialError"),
                        _                               => UITextDictionary.Get("BatchDownloadPage.Status.Done"),
                    };
                }
            });
        }

        private void AppendDebug(string text)
        {
            Dispatcher.Invoke(() =>
            {
                tbDebugOutput.AppendText($"{text}{Environment.NewLine}");
                tbDebugOutput.ScrollToEnd();
            });
        }

        private void tbDebugOutput_TextChanged(object sender, TextChangedEventArgs e)
        {
            tbDebugOutput.ScrollToEnd();
        }
    }
}
