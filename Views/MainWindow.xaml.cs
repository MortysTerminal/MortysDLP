using MortysDLP.Helpers;
using MortysDLP.Models;
using MortysDLP.Services;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MortysDLP
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private CancellationTokenSource? _downloadCancellationTokenSource;
        private Task? _downloadTask;
        private string _lastDownloadPath = "";
        private double _lastProgress = 0;
        private Process? _ytDlpProcess;

        public MainWindow()
        {
            /* Sprachanpassung bei Window-Start */
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
            ValidateDownloadButton();
        }
        public enum iaStatusIconType
        {
            None,
            Loading,
            Success,
            Error
        }
        internal void SetUiAudioEnabled(bool enabled)
        {
            if (enabled == true)
            {
                dpAudioPath.Visibility = Visibility.Visible;
                //lblAudioPath.Visibility = Visibility.Visible;
                //lblAudioPathInfo.Visibility = Visibility.Visible;
            }
            else
            {
                dpAudioPath.Visibility = Visibility.Collapsed;
                //lblAudioPath.Visibility = Visibility.Collapsed;
                //lblAudioPathInfo.Visibility = Visibility.Collapsed;
            }

        }
        private static async Task AddDownloadToHistoryAsync(string url, string title, string downloadDirectory)
        {
            // Hier die Logik nach dem erfolgreichen Download einfügen
            // Trim, da yt-dlp bei Titeln manchmal vorne und hinten Leerzeichen hinzufügt
            await DownloadHistoryService.AddAsync(new DownloadHistoryEntry
            {
                Url = url,
                Title = title.Trim(),
                DownloadDirectory = downloadDirectory,
                DownloadedAt = DateTime.Now
            });
        }
        private void AppendOutput(string text)
        {
            Dispatcher.Invoke(() =>
            {
                tbDebugOutput.AppendText($"{text}{Environment.NewLine}");
                tbDebugOutput.ScrollToEnd(); // Automatisch nach unten scrollen
            });
        }
        private void AudioOnlyAdjustments()
        {
            if (cbAudioOnly.IsChecked == true)
            {
                //txtAudioOnlyInfo.Foreground = Brushes.Black;
                txtAudioOnlyInfo.IsEnabled = true;
                combAudioFormat.IsReadOnly = false;
                combAudioFormat.IsEnabled = true;

                cbVideoformat.IsEnabled = false;
            }
            else
            {
                //txtAudioOnlyInfo.Foreground = Brushes.Silver;
                txtAudioOnlyInfo.IsEnabled = false;
                combAudioFormat.IsReadOnly = true;
                combAudioFormat.IsEnabled = false;

                cbVideoformat.IsEnabled = true;
            }
        }
        //private void AudioOnlyVideoSchnittWorkaround()
        //{
        //    if (cbAudioOnly.IsChecked == true && cbVideoformat.IsChecked == true)
        //    {
        //        cbAudioOnly.IsChecked = false;
        //        cbVideoformat.IsChecked = false;
        //    }
        //}

        private void btnConvert_Click(object sender, RoutedEventArgs e)
        {
            var convertWindow = new MortysDLP.Views.ConvertWindow();
            convertWindow.Owner = this;
            convertWindow.ShowDialog();
        }

        private void btnDownloadCancel_Click(object sender, RoutedEventArgs e)
        {
            //btnDownloadCancel.IsEnabled = false;
            txtDownloadStatus.Text = UITexte.UITexte.MainWindow_Label_DownloadStatus_WhileCanceling;
            _downloadCancellationTokenSource?.Cancel();
            // Prozess ggf. direkt beenden (optional, falls nicht im Thread überwacht)
            _ytDlpProcess?.Kill(true);
            UpdateProgress(0);
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

            SetUiEnabled(false); // UI sperren
            btnDownloadCancel.IsEnabled = true;
            spLoadingbar.Visibility = Visibility.Visible;

            // Fortschritt und Status-Icon zurücksetzen
            UpdateProgress(0);
            SetiaStatusIcon(iaStatusIconType.Loading);
            txtDownloadStatus.Text = UITexte.UITexte.MainWindow_Label_DownloadStatus_Loading;

            _downloadCancellationTokenSource = new CancellationTokenSource();
            var token = _downloadCancellationTokenSource.Token;

            string url = tbURL.Text;
            string ytDlpPath = Properties.Settings.Default.YtdlpPath;

            try
            {
                // Titel holen, aber Abbruch beachten
                string title = await GetVideoTitleAsync(ytDlpPath, url, token);
                if (token.IsCancellationRequested)
                    throw new OperationCanceledException(token);

                string downloadDir = lblDownloadPath.Content?.ToString() ?? "";
                await AddDownloadToHistoryAsync(url, title, downloadDir);

                _downloadTask = StartDownloadAsync(token);
                await _downloadTask;

                if (token.IsCancellationRequested)
                    throw new OperationCanceledException(token);

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
                SetUiEnabled(true); // UI wieder freigeben
                btnDownloadStart.IsEnabled = !string.IsNullOrWhiteSpace(tbURL.Text);
                btnDownloadCancel.IsEnabled = false;
                AudioOnlyAdjustments();
                FirstSecondsAdjustments();
                VideoformatAdjustments();
                TimespanAdjustments();
                // spLoadingbar.Visibility = Visibility.Hidden;
            }
        }
        private void btnHeaderChangeDownloadPath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new DownloadPathDialog();
            dialog.Owner = this;
            dialog.ShowDialog();
        }
        private void btnHeaderClose_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
        private void btnHeaderGitHub_Click(object sender, RoutedEventArgs e)
        {
            string url = Properties.Settings.Default.MortysDLPGitHubURL;
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch
            {
                MessageBox.Show(UITexte.UITexte.Error_OpenBrowser, UITexte.UITexte.Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void btnHistory_Click(object sender, RoutedEventArgs e)
        {
            var win = new DownloadHistoryWindow();
            win.Owner = this;
            win.ShowDialog();
        }
        private void btnStatusIcon_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_lastDownloadPath) && System.IO.Directory.Exists(_lastDownloadPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _lastDownloadPath,
                    UseShellExecute = true
                });
            }
            else
            {
                MessageBox.Show(UITexte.UITexte.MainWindow_Download_PathNotFound, UITexte.UITexte.Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private string BuildYTDLPArguments()
        {
            // Alle UI-Werte im UI-Thread abholen
            string url = "";
            string downloadPath = "";
            //string downloadAudioPath = "";
            string timespanFrom = "";
            string timespanTo = "";
            string firstSeconds = "";
            bool isTimespan = false;
            bool isFirstSeconds = false;
            bool isAudioOnly = false;
            bool isVideoformat = false;
            string selectedcombAudioFormat = "";
            string ffmpegPath = "";

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
                downloadPath = isAudioOnly
                    ? Properties.Settings.Default.DownloadAudioOnlyPath
                    : Properties.Settings.Default.DownloadPath;
                selectedcombAudioFormat = GetSelectedAudioFormat();
                ffmpegPath = Properties.Settings.Default.FfmpegPath;
            });

            string ba_Args = "-f \"bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]/best\"";
            ba_Args += $" \"{url}\"";

            if (isTimespan)
            {
                ba_Args += $" -o \"{downloadPath}\\z_%(title)s_%(id)s.%(ext)s\"";
                ba_Args += $" --download-sections \"*{timespanFrom}-{timespanTo}\"";
            }
            if (isFirstSeconds)
            {
                ba_Args += $" -o \"{downloadPath}\\{firstSeconds}_%(title)s_%(id)s.%(ext)s\"";
                ba_Args += $" --downloader \"{ffmpegPath}\" --downloader-args \"ffmpeg:-t {firstSeconds}\"";
            }
            // Standard-Output
            ba_Args += $" -o \"{downloadPath}\\%(title)s_%(id)s.%(ext)s\"";
            ba_Args += " --no-check-certificates";
            ba_Args += " --no-mtime";

            return ba_Args;
        }
        private void cbAudioOnlyCheck(object sender, RoutedEventArgs e) => AudioOnlyAdjustments();
        private void cbFirstSecondsCheck(object sender, RoutedEventArgs e)
        {
            FirstSecondsAdjustments();
            ValidateDownloadButton();
        }
        private void cbTimespanCheck(object sender, RoutedEventArgs e) => TimespanAdjustments();
        private void cbVideoFormatCheck(object sender, RoutedEventArgs e) => VideoformatAdjustments();
        private void FirstSecondsAdjustments()
        {
            if (cbFirstSeconds.IsChecked == true)
            {
                tbFirstSecondsSeconds.IsEnabled = true;
                tbFirstSecondsSeconds.IsReadOnly = false;
                cbTimespan.IsEnabled = false;
            }
            else
            {
                tbFirstSecondsSeconds.IsEnabled = false;
                tbFirstSecondsSeconds.IsReadOnly = true;
                cbTimespan.IsEnabled = true;
            }
        }
        private string GetSelectedAudioFormat()
        {
            if (!Dispatcher.CheckAccess())
            {
                // Wenn nicht im UI-Thread, dann Dispatcher verwenden 
                return Dispatcher.Invoke(() => GetSelectedAudioFormat());
            }

            if (combAudioFormat.SelectedItem is ComboBoxItem selectedItem)
            {
                // .Content kann null sein, daher null-Check und Fallback auf "mp3"
                return selectedItem.Content?.ToString() ?? "mp3";
            }
            else
            {
                return "mp3";
            }
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

            // Lies die Ausgabe asynchron und prüfe auf Abbruch
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

            // Lies ggf. Fehlerausgabe
            string? error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync(token);

            // Fallback, falls kein Titel gefunden
            if (string.IsNullOrWhiteSpace(title))
            {
                // Optional: Fehlerausgabe loggen
                AppendOutput(string.Format(UITexte.UITexte.MainWindow_DebugOutput_NoTitleFromYTDLP, error?.Trim()));
                // Fallback: Versuche, aus der URL einen Titel zu generieren
                try
                {
                    var uri = new Uri(url);
                    if (uri.Host.Contains("twitch.tv"))
                    {
                        // Extrahiere Channel oder Video-ID
                        var segments = uri.Segments;
                        if (segments.Length > 1)
                        {
                            title = $"Twitch: {segments[segments.Length - 1].Trim('/')}";
                        }
                        else
                        {
                            title = "Twitch-Video";
                        }
                    }
                    else
                    {
                        title = UITexte.UITexte.MainWindow_Download_UnknownTitle;
                    }
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
            Label? label = sender as Label;
            if(label == null) return;

            string? path = label.Content.ToString();
            if (!string.IsNullOrWhiteSpace(path) && System.IO.Directory.Exists(path) && !(path == null))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
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
                        percent = Math.Max(0, Math.Min(100, percent));
                        return percent;
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
                // Prozent extrahieren
                var percentMatch = System.Text.RegularExpressions.Regex.Match(line, @"\s(\d{1,3}(?:\.\d+)?)%");
                // Geschwindigkeit extrahieren (z.B. "at   20.67MiB/s")
                var speedMatch = System.Text.RegularExpressions.Regex.Match(line, @"at\s+([\d\.]+)MiB/s");

                if (speedMatch.Success && double.TryParse(speedMatch.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double speed))
                {
                    speedMBs = speed;
                }

                if (percentMatch.Success && double.TryParse(percentMatch.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double percent))
                {
                    return percent;
                }
            }
            return null;
        }
        private async Task RunYtDlpAsync(string YtDlpPath, string arguments, CancellationToken token)
        {
            // Wert im UI-Thread holen!
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
                    {
                        UpdateProgress(progress.Value, false, speedMBs);
                    }
                    else if (isTimespanChecked)
                    {
                        var percent = ParseFfmpegTimeProgress(e.Data, timespanFrom, timespanTo, out var secCurrent, out var secTotal);
                        if (percent.HasValue) { UpdateProgress(percent.Value, false, null); }
                    }
                    AppendOutput(e.Data);
                }
            };

            _ytDlpProcess.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    // Fortschritt aus ffmpeg-Zeilen extrahieren (time=...)
                    var percent = ParseFfmpegTimeProgress(e.Data, timespanFrom, timespanTo, out var secCurrent, out var secTotal);
                    if (percent.HasValue)
                    {
                        UpdateProgress(percent.Value, false, null);
                    }
                    AppendOutput($"[ERROR] {e.Data}");
                }
            };

            try
            {
                _ytDlpProcess.Start();
                _ytDlpProcess.BeginOutputReadLine();
                _ytDlpProcess.BeginErrorReadLine();

                // Überwache das CancellationToken
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

                // Prüfe, ob abgebrochen wurde
                if (token.IsCancellationRequested)
                {
                    Dispatcher.Invoke((Action)(() =>
                    {
                        SetiaStatusIcon(iaStatusIconType.Error);
                        UpdateProgress(_lastProgress, isError: true);
                        txtDownloadStatus.Text = UITexte.UITexte.MainWindow_Label_DownloadStatus_Cancel;
                        this.btnDownloadCancel.IsEnabled = false;
                    }));
                    AppendOutput(UITexte.UITexte.MainWindow_DebugOutput_DownloadCancel);
                    return; // Keine weiteren Status-Updates!
                }

                if (_ytDlpProcess.ExitCode != 0)
                {
                    AppendOutput(string.Format(UITexte.UITexte.MainWindow_DebugOutput_YTDLPError, _ytDlpProcess.ExitCode));
                    Dispatcher.Invoke((Action)(() =>
                    {
                        SetiaStatusIcon(iaStatusIconType.Error);
                        UpdateProgress(_lastProgress, isError: true);
                        txtDownloadStatus.Text = UITexte.UITexte.Error;
                        this.btnDownloadCancel.IsEnabled = false;
                    }));
                }
                else
                {
                    AppendOutput(string.Format(UITexte.UITexte.MainWindow_DebugOutput_ProcessEnd, _ytDlpProcess.ExitCode));
                    Dispatcher.Invoke((Action)(() =>
                    {
                        SetiaStatusIcon(iaStatusIconType.Success);
                        UpdateProgress(100);
                        txtDownloadStatus.Text = UITexte.UITexte.MainWindow_Label_DownloadStatus_Success;
                        this.btnDownloadCancel.IsEnabled = false;
                    }));
                }
            }
            catch (Exception ex)
            {
                AppendOutput(string.Format(UITexte.UITexte.MainWindow_DebugOutput_ThrowException, ex.Message));
                Dispatcher.Invoke((Action)(() =>
                {
                    AppendOutput(string.Format(UITexte.UITexte.MainWindow_DebugOutput_InternalError, ex.Message));
                    this.btnDownloadCancel.IsEnabled = false;
                }));
            }
        }
        private void SaveSettings(object sender, RoutedEventArgs e)
        {
            bool cbFirstSecondsChecked = false;
            bool cbVideoformatChecked = false;
            bool cbTimespanChecked = false;
            bool cbAudioOnlyChecked = false;
            string tbAudioOnlyText = "";
            string tbTimespanFromText = "";
            string tbTimespanToText = "";
            string tbFirstSecondsText = "";
            if (cbTimespan.IsChecked == true)
            {
                cbTimespanChecked = true;
                tbTimespanFromText = tbTimespanFrom.Text;
                tbTimespanToText = tbTimespanTo.Text;
            }

            if (cbFirstSeconds.IsChecked == true)
            {
                cbFirstSecondsChecked = true;
                tbFirstSecondsText = tbFirstSecondsSeconds.Text;
            }
            if (cbVideoformat.IsChecked == true) cbVideoformatChecked = true;

            if (cbAudioOnly.IsChecked == true)
            {
                cbAudioOnlyChecked = true;
                tbAudioOnlyText = GetSelectedAudioFormat();
            }

            var result = System.Windows.MessageBox.Show(UITexte.UITexte.MessageBox_SaveSettings_Question, UITexte.UITexte.MessageBox_SaveSettings_Title, System.Windows.MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
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
                Properties.Settings.Default.Save();
                System.Windows.MessageBox.Show("Einstellungen gespeichert");
            }
            else if (result == System.Windows.MessageBoxResult.No)
            {
                //Environment.Exit(0);
            }
        }
        private void SelectAudioFormat(string savedFormat)
        {
            bool formatFound = false;

            foreach (ComboBoxItem item in combAudioFormat.Items)
            {
                if (item.Content.ToString() == savedFormat)
                {
                    combAudioFormat.SelectedItem = item;
                    formatFound = true;
                    break;
                }
            }

            // Wenn das Format nicht gefunden wurde, "mp3" auswählen
            if (!formatFound)
            {
                foreach (ComboBoxItem item in combAudioFormat.Items)
                {
                    if (item.Content.ToString() == "mp3")
                    {
                        combAudioFormat.SelectedItem = item;
                        break;
                    }
                }
            }
        }
        private void SetAudioDownloadPathInSettings()
        {
            // Prüfe, ob der gespeicherte Downloadpfad existiert und gültig ist
            string savedAudioPath = Properties.Settings.Default.DownloadAudioOnlyPath;
            if (string.IsNullOrEmpty(savedAudioPath) || !System.IO.Directory.Exists(savedAudioPath))
            {
                // Fallback: KnownFolder Downloads verwenden
                string downloadsFolder = KnownFoldersHelper.GetPath(KnownFolder.Downloads);
                Properties.Settings.Default.DownloadAudioOnlyPath = downloadsFolder.ToString();
                Properties.Settings.Default.Save();
            }
        }
        private void SetDownloadPathInSettings()
        {
            // Prüfe, ob der gespeicherte Downloadpfad existiert und gültig ist
            string savedPath = Properties.Settings.Default.DownloadPath;
            if (string.IsNullOrEmpty(savedPath) || !System.IO.Directory.Exists(savedPath))
            {
                // Fallback: KnownFolder Downloads verwenden
                string downloadsFolder = KnownFoldersHelper.GetPath(KnownFolder.Downloads);
                Properties.Settings.Default.DownloadPath = downloadsFolder.ToString();
                Properties.Settings.Default.Save();
            }
        }
        private void SetiaStatusIcon(iaStatusIconType type)
        {
            Dispatcher.Invoke(() =>
            {
                iaStatusIcon.Spin = false; // Standard: keine Animation
                btnStatusIcon.IsEnabled = (type == iaStatusIconType.Success || type == iaStatusIconType.Error);
                switch (type)
                {
                    case iaStatusIconType.Loading:
                        iaStatusIcon.Icon = FontAwesome.WPF.FontAwesomeIcon.Spinner;
                        iaStatusIcon.Spin = true; // Animation aktivieren
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

            // Downloadpfad aus Ressourcen lesen und Label setzen

            lblDownloadPath.Content = Properties.Settings.Default.DownloadPath;
            lblAudioPath.Content = Properties.Settings.Default.DownloadAudioOnlyPath;

            if (Properties.Settings.Default.CheckedTimespan)
            {
                tbTimespanFrom.Text = Properties.Settings.Default.TimespanFrom;
                tbTimespanTo.Text = Properties.Settings.Default.TimespanTo;
            }
            else
            {
                tbTimespanFrom.Text = "";
                tbTimespanTo.Text = "";
            }

            if (Properties.Settings.Default.CheckedFirstSeconds)
            {
                tbFirstSecondsSeconds.Text = Properties.Settings.Default.FirstSecondsSeconds;
            }
            else
            {
                tbFirstSecondsSeconds.Text = "";
            }

            cbTimespan.IsChecked = Properties.Settings.Default.CheckedTimespan;
            cbFirstSeconds.IsChecked = Properties.Settings.Default.CheckedFirstSeconds;
            cbVideoformat.IsChecked = Properties.Settings.Default.CheckedVideoFormat;
            cbAudioOnly.IsChecked = Properties.Settings.Default.CheckedAudioOnly;

            if (Properties.Settings.Default.CheckedAudioOnly)
            {
                SelectAudioFormat(Properties.Settings.Default.SelectedAudioFormat);
            }

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
            cbVideoformat.IsEnabled = enabled;
            cbAudioOnly.IsEnabled = enabled;
            combAudioFormat.IsEnabled = enabled && cbAudioOnly.IsChecked == true;
            btnSaveSettings.IsEnabled = enabled;
            btnHistory.IsEnabled = enabled;
            btnDownloadStart.IsEnabled = enabled;
            btnHeaderSettings.IsEnabled = enabled;
            btnHeaderGitHub.IsEnabled = enabled;
            // Menüeinträge ggf. sperren
            // btnHeaderChangeDownloadPath_Click ist im Menü, ggf. Menü sperren:
            // Menü kannst du z.B. über Menu.IsEnabled = enabled; sperren, falls du einen Namen vergeben hast.
        }
        private void SetUITexte()
        {
            this.btnHeaderSettings.Header = UITexte.UITexte.MainWindow_Button_Menu_Settings;
            this.btnHeaderChangeDownloadPath.Header = UITexte.UITexte.MainWindow_Button_Menu_ChangeDownloadPath;
            this.btnHeaderClose.Header = UITexte.UITexte.Button_Close;
            this.lblSoftwareinfo.Text = UITexte.UITexte.Softwareinfo;
            this.lblDownloadPathInfo.Content = UITexte.UITexte.MainWindow_Label_DownloadPathInfo;
            this.lblAudioPathInfo.Content = UITexte.UITexte.MainWindow_Label_AudioOnly_Info;
            this.lblURLInfo.Content = UITexte.UITexte.MainWindow_Label_URL;
            this.btnHistory.Content = UITexte.UITexte.MainWindow_Button_History;
            this.txtTimespanFrom.Text = UITexte.UITexte.MainWindow_Label_TimespanLeft;
            this.txtTimespanDash.Text = UITexte.UITexte.MainWindow_Label_TimespanMiddle;
            this.txtTimespanInfo.Text = UITexte.UITexte.MainWindow_Label_TimespanRight;
            this.ToolTipTimeSpan.Content = UITexte.UITexte.MainWindow_Button_Timespan_Info;
            this.txtFirstSecondsInfo1.Text = UITexte.UITexte.MainWindow_Label_TimeStartLeft;
            this.txtFirstSecondsInfo2.Text = UITexte.UITexte.MainWindow_Label_TimeStartRight;
            this.txtVideoformatInfo1.Text = UITexte.UITexte.MainWindow_Label_Videoformat;
            this.txtVideoformatInfo2.Text = UITexte.UITexte.MainWindow_Label_Videoformat_Info;
            this.txtAudioOnlyInfo.Text = UITexte.UITexte.MainWindow_Label_AudioOnly;
            this.txtAudioOnlyInfo2.Text = UITexte.UITexte.MainWindow_Label_AudioOnly_Info2;
            this.btnDownloadStart.Content = UITexte.UITexte.MainWindow_Button_DownloadStart;
            this.btnDownloadCancel.Content = UITexte.UITexte.MainWindow_Button_DownloadAbort;
            this.btnSaveSettings.Content = UITexte.UITexte.MainWindow_Button_SettingsSave;
            this.expDebug.Header = UITexte.UITexte.MainWindow_DebugInfo;
            this.lblMainVersion.Content = Properties.Settings.Default.CurrentVersion;
        }
        private async Task StartDownloadAsync(CancellationToken token)
        {
            string args = BuildYTDLPArguments();
            await RunYtDlpAsync(Properties.Settings.Default.YtdlpPath, args, token);
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
            {
                Dispatcher.InvokeAsync(textBox.SelectAll);
            }
        }
        private void TimespanAdjustments()
        {
            if (cbTimespan.IsChecked == true)
            {
                tbTimespanFrom.IsReadOnly = false;
                tbTimespanFrom.IsEnabled = true;
                tbTimespanTo.IsReadOnly = false;
                tbTimespanTo.IsEnabled = true;
                cbFirstSeconds.IsEnabled = false;
            }
            else
            {
                tbTimespanFrom.IsReadOnly = true;
                tbTimespanFrom.IsEnabled = false;
                tbTimespanTo.IsReadOnly = true;
                tbTimespanTo.IsEnabled = false;
                cbFirstSeconds.IsEnabled = true;
            }
            ValidateDownloadButton();
        }
        private bool TryParseFlexibleTime(string input, out TimeSpan result)
        {
            // Unterstützt hh:mm:ss, mm:ss und optional mit .ff für ffmpeg
            string[] formats = { @"hh\:mm\:ss\.ff", @"hh\:mm\:ss", @"mm\:ss\.ff", @"mm\:ss" };
            foreach (var format in formats)
            {
                if (TimeSpan.TryParseExact(input, format, null, out result))
                    return true;
            }
            // Fallback: Standard-TryParse (z.B. falls jemand 90 eingibt für 90 Sekunden)
            return TimeSpan.TryParse(input, out result);
        }
        private void UpdateProgress(double percent, bool isError = false, double? speedMBs = null)
        {
            //System.Diagnostics.Debug.WriteLine($"UpdateProgress: percent={percent}, speedMBs={speedMBs}, isError={isError}"); // DEBUG

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
                        if (speedMBs.HasValue)
                            txtDownloadProgress.Text = $"{percent:F2} % ({speedMBs.Value:F2} MB/s)";
                        else
                            txtDownloadProgress.Text = $"{percent:F2} %";
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
                //System.Diagnostics.Debug.WriteLine($"UpdateProgress: txtDownloadProgress={txtDownloadProgress.Text}"); // DEBUG

            });


        }
        private void ValidateDownloadButton()
        {
            bool urlOk = !string.IsNullOrWhiteSpace(tbURL.Text);

            bool timespanOk = !cbTimespan.IsChecked == true ||
                (!string.IsNullOrWhiteSpace(tbTimespanFrom.Text) && !string.IsNullOrWhiteSpace(tbTimespanTo.Text));

            bool secondsOk = !cbFirstSeconds.IsChecked == true ||
                !string.IsNullOrWhiteSpace(tbFirstSecondsSeconds.Text);

            btnDownloadStart.IsEnabled = urlOk && timespanOk && secondsOk;
        }
        private void VideoformatAdjustments()

        {
            if (cbVideoformat.IsChecked == true)
            {
                //txtVideoformatInfo1.Foreground = Brushes.Black;
                txtVideoformatInfo1.IsEnabled = true;

                cbAudioOnly.IsEnabled = false;
            }
            else
            {
                //txtVideoformatInfo1.Foreground = Brushes.Silver;
                txtVideoformatInfo1.IsEnabled = false;

                cbAudioOnly.IsEnabled = true;
            }
        }
    }
}