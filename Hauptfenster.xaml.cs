using FontAwesome.WPF;
using MortysDLP.Models;
using MortysDLP.Services;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MortysDLP
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class Hauptfenster : Window
    {
        private CancellationTokenSource _downloadCancellationTokenSource;
        private Task _downloadTask;
        private double _lastProgress = 0;
        private Process _ytDlpProcess;
        private string _lastDownloadPath = "";

        // Feld für letzten Fortschritt

        public Hauptfenster()
        {
            /*
             Setze den Downloadpfad in den Einstellungen, falls kein Pfad vorhanden ist.
             */
            //SetzeDownloadPfadInEinstellungen();
            InitializeComponent();
            EinstellungenLaden();
            ErsteSekundenAnpassen();
            AudioOnlyAnpassen();
            VideoSchnittFormatAnpassen();
            ZeitspanneAnpassen();


            btn_download_starten.IsEnabled = !string.IsNullOrWhiteSpace(tb_URL.Text);

            // Setze den Titel des Fensters mit der aktuellen Version
            this.Title = "MortysDLP - (" + Properties.Settings.Default.CURRENTVERSION + ")";
        }

        public enum StatusIconType
        {
            None,
            Loading,
            Success,
            Error
        }

        private void AppendOutput(string text)
        {
            Dispatcher.Invoke(() =>
            {
                OutputTextBox.AppendText($"{text}{Environment.NewLine}");
                OutputTextBox.ScrollToEnd(); // Automatisch nach unten scrollen
            });
        }

        private void AudioOnlyAnpassen()
        {
            if (cb_AudioOnly.IsChecked == true)
            {
                //txt_AudioOnly_info.Foreground = Brushes.Black;
                txt_AudioOnly_info.IsEnabled = true;
                AudioFormatComboBox.IsReadOnly = false;
                AudioFormatComboBox.IsEnabled = true;

                cb_Videoformat.IsEnabled = false;
            }
            else
            {
                //txt_AudioOnly_info.Foreground = Brushes.Silver;
                txt_AudioOnly_info.IsEnabled = false;
                AudioFormatComboBox.IsReadOnly = true;
                AudioFormatComboBox.IsEnabled = false;

                cb_Videoformat.IsEnabled = true;
            }
        }

        private void AudioOnlyVideoSchnittWorkaround()
        {
            if (cb_AudioOnly.IsChecked == true && cb_Videoformat.IsChecked == true)
            {
                cb_AudioOnly.IsChecked = false;
                cb_Videoformat.IsChecked = false;
            }
        }

        private string BaueYTDLPArgumente()
        {
            // Alle UI-Werte im UI-Thread abholen
            string url = "";
            string downloadPath = "";
            string downloadAudioPath = "";
            string zeitspanneVon = "";
            string zeitspanneBis = "";
            string ersteSekunden = "";
            bool isZeitspanne = false;
            bool isErsteSekunden = false;
            bool isAudioOnly = false;
            bool isVideoformat = false;
            string selectedAudioFormatComboBox = "";
            string ffmpegPath = "";

            Dispatcher.Invoke(() =>
            {
                url = tb_URL.Text;
                zeitspanneVon = tb_zeitspanne_von.Text;
                zeitspanneBis = tb_zeitspanne_bis.Text;
                ersteSekunden = tb_ErsteSekunden_Sekunden.Text;
                isZeitspanne = cb_Zeitspanne.IsChecked == true;
                isErsteSekunden = cb_ErsteSekunden.IsChecked == true;
                isAudioOnly = cb_AudioOnly.IsChecked == true;
                isVideoformat = cb_Videoformat.IsChecked == true;
                downloadPath = isAudioOnly
                    ? Properties.Settings.Default.DOWNLOADAUDIOONLYPATH
                    : Properties.Settings.Default.DOWNLOADPATH;
                selectedAudioFormatComboBox = GetSelectedAudioFormat();
                ffmpegPath = Properties.Settings.Default.FFMPEGPATH;
            });

            string ba_Args = "-f \"bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]/best\"";
            ba_Args += $" \"{url}\"";

            if (isZeitspanne)
            {
                ba_Args += $" -o \"{downloadPath}\\z_%(title)s.%(ext)s\"";
                ba_Args += $" --download-sections \"*{zeitspanneVon}-{zeitspanneBis}\"";
            }
            if (isErsteSekunden)
            {
                ba_Args += $" -o \"{downloadPath}\\{ersteSekunden}_%(title)s.%(ext)s\"";
                ba_Args += $" --downloader \"{ffmpegPath}\" --downloader-args \"ffmpeg:-t {ersteSekunden}\"";
            }
            if (isAudioOnly)
            {
                ba_Args += $" -x --audio-format {selectedAudioFormatComboBox} --audio-quality 0";
            }
            else
            {
                if (isVideoformat)
                {
                    ba_Args += " -S vcodec:h264";
                }
            }

            ba_Args += $" -o \"{downloadPath}\\%(title)s.%(ext)s\"";
            ba_Args += " --no-check-certificates";
            ba_Args += " --no-mtime";

            return ba_Args;
        }

        private void cbAudioOnlyCheck(object sender, RoutedEventArgs e)
        {
            AudioOnlyAnpassen();
        }

        private void cbErsteSekundenCheck(object sender, RoutedEventArgs e)
        {
            ErsteSekundenAnpassen();
            ValidateDownloadButton();
        }

        private void cbVideoFormatCheck(object sender, RoutedEventArgs e)
        {
            VideoSchnittFormatAnpassen();
        }

        private void cbZeitspanneCheck(object sender, RoutedEventArgs e)
        {
            ZeitspanneAnpassen();
        }

        private void CloseMenu_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void DownloadAbbrechen_Click(object sender, RoutedEventArgs e)
        {
            //btn_download_abbrechen.IsEnabled = false;
            DownloadStatusText.Text = "Breche ab...";
            _downloadCancellationTokenSource?.Cancel();
            // Prozess ggf. direkt beenden (optional, falls nicht im Thread überwacht)
            _ytDlpProcess?.Kill(true);
            UpdateProgress(0);
        }

        private void DownloadPathMenu_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new DownloadPathDialog();
            dialog.Owner = this;
            dialog.ShowDialog();
        }

        private async void DownloadStarten_Click(object sender, RoutedEventArgs e)
        {
            OutputTextBox.Clear();

            Dispatcher.Invoke(() =>
            {
                _lastDownloadPath = cb_AudioOnly.IsChecked == true
                    ? Properties.Settings.Default.DOWNLOADAUDIOONLYPATH
                    : Properties.Settings.Default.DOWNLOADPATH;
            });

            SetUiEnabled(false); // UI sperren
            btn_download_abbrechen.IsEnabled = true;
            sp_Ladebalken.Visibility = Visibility.Visible;

            // Fortschritt und Status-Icon zurücksetzen
            UpdateProgress(0);
            SetStatusIcon(StatusIconType.Loading);
            DownloadStatusText.Text = "Lädt";

            _downloadCancellationTokenSource = new CancellationTokenSource();
            var token = _downloadCancellationTokenSource.Token;

            string url = tb_URL.Text;
            string ytDlpPath = Properties.Settings.Default.YTDLPPATH;
            string title = "";

            try
            {
                // Titel holen, aber Abbruch beachten
                title = await GetVideoTitleAsync(ytDlpPath, url, token);
                if (token.IsCancellationRequested)
                    throw new OperationCanceledException(token);

                string downloadDir = lbl_downloadpath.Content?.ToString() ?? "";
                NachFertigemDownload(url, title, downloadDir);

                _downloadTask = Task.Run(() => StarteDownload(token), token);
                await _downloadTask;

                if (token.IsCancellationRequested)
                    throw new OperationCanceledException(token);

                OutputTextBox.AppendText("Download abgeschlossen.\n");
                SetStatusIcon(StatusIconType.Success);
                UpdateProgress(100);
                DownloadStatusText.Text = "Abgeschlossen";
            }
            catch (OperationCanceledException)
            {
                OutputTextBox.AppendText("Download abgebrochen.\n");
                SetStatusIcon(StatusIconType.Error);
                UpdateProgress(_lastProgress, isError: true);
                DownloadStatusText.Text = "Abgebrochen";
            }
            catch
            {
                SetStatusIcon(StatusIconType.Error);
                UpdateProgress(_lastProgress, isError: true);
                DownloadStatusText.Text = "Abgebrochen";
                throw;
            }
            finally
            {
                SetUiEnabled(true); // UI wieder freigeben
                btn_download_starten.IsEnabled = !string.IsNullOrWhiteSpace(tb_URL.Text);
                btn_download_abbrechen.IsEnabled = false;
                AudioOnlyAnpassen();
                ErsteSekundenAnpassen();
                VideoSchnittFormatAnpassen();
                ZeitspanneAnpassen();
                // sp_Ladebalken.Visibility = Visibility.Hidden;
            }
        }

        private void EinstellungenLaden()
        {
            SetzeDownloadPfadInEinstellungen();
            SetzeAudioDownloadPfadInEinstellungen();

            if (Properties.Settings.Default.CHECKED_ZEITSPANNE)
            {
                tb_zeitspanne_von.Text = Properties.Settings.Default.ZEITSPANNE_VON;
                tb_zeitspanne_bis.Text = Properties.Settings.Default.ZEITSPANNE_BIS;
            }
            else
            {
                tb_zeitspanne_von.Text = "";
                tb_zeitspanne_bis.Text = "";
            }

            if (Properties.Settings.Default.CHECKED_ERSTESEKUNDEN)
            {
                tb_ErsteSekunden_Sekunden.Text = Properties.Settings.Default.ERSTESEKUNDEN_SEKUNDEN;
            }
            else
            {
                tb_ErsteSekunden_Sekunden.Text = "";
            }

            cb_Zeitspanne.IsChecked = Properties.Settings.Default.CHECKED_ZEITSPANNE;
            cb_ErsteSekunden.IsChecked = Properties.Settings.Default.CHECKED_ERSTESEKUNDEN;
            cb_Videoformat.IsChecked = Properties.Settings.Default.CHECKED_VIDEOFORMAT;
            cb_AudioOnly.IsChecked = Properties.Settings.Default.CHECKED_AUDIO_ONLY;

            if (Properties.Settings.Default.CHECKED_AUDIO_ONLY)
            {
                SelectAudioFormat(Properties.Settings.Default.SELECTED_AUDIO_FORMAT);
            }

            SetUiAudioEnabled(Properties.Settings.Default.CHECKED_AUDIOPATH);

            // Downloadpfad einfuegen in TextBox
            //tb_downloadpath.Text = downloadspath;
        }

        private void EinstellungenSpeichern(object sender, RoutedEventArgs e)
        {
            // Lade Zustaende aus Form

            //string? tempdownloadpath = lbl_downloadpath.Content.ToString();
            bool cb_ErsteSekundenChecked = false;
            bool cb_VideoformatChecked = false;
            bool cb_ZeitspanneChecked = false;
            bool cb_AudioOnlyChecked = false;
            string tb_AudioOnly_Text = "";
            string tb_Von_Text = "";
            string tb_Bis_Text = "";
            string tb_ErsteSekunden_Text = "";
            if (cb_Zeitspanne.IsChecked == true)
            {
                cb_ZeitspanneChecked = true;
                tb_Von_Text = tb_zeitspanne_von.Text;
                tb_Bis_Text = tb_zeitspanne_bis.Text;
            }

            if (cb_ErsteSekunden.IsChecked == true)
            {
                cb_ErsteSekundenChecked = true;
                tb_ErsteSekunden_Text = tb_ErsteSekunden_Sekunden.Text;
            }
            if (cb_Videoformat.IsChecked == true) cb_VideoformatChecked = true;

            if (cb_AudioOnly.IsChecked == true)
            {
                cb_AudioOnlyChecked = true;
                tb_AudioOnly_Text = GetSelectedAudioFormat();
            }

            var Result = System.Windows.MessageBox.Show("Sollen die Einstellungen für den nächsten Programmstart gespeichert werden?", "Einstellunge abspeichern?", System.Windows.MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (Result == System.Windows.MessageBoxResult.Yes)
            {
                Properties.Settings.Default.CHECKED_ZEITSPANNE = cb_ZeitspanneChecked;
                Properties.Settings.Default.ZEITSPANNE_VON = tb_Von_Text;
                Properties.Settings.Default.ZEITSPANNE_BIS = tb_Bis_Text;
                Properties.Settings.Default.CHECKED_ERSTESEKUNDEN = cb_ErsteSekundenChecked;
                Properties.Settings.Default.ERSTESEKUNDEN_SEKUNDEN = tb_ErsteSekunden_Text;
                Properties.Settings.Default.CHECKED_VIDEOFORMAT = cb_VideoformatChecked;
                Properties.Settings.Default.DOWNLOADPATH = lbl_downloadpath.Content.ToString();
                Properties.Settings.Default.CHECKED_AUDIO_ONLY = cb_AudioOnlyChecked;
                Properties.Settings.Default.SELECTED_AUDIO_FORMAT = tb_AudioOnly_Text;
                Properties.Settings.Default.DOWNLOADAUDIOONLYPATH = lbl_audiopath.Content.ToString();
                Properties.Settings.Default.Save();
                System.Windows.MessageBox.Show("Einstellungen gespeichert");
            }
            else if (Result == System.Windows.MessageBoxResult.No)
            {
                //Environment.Exit(0);
            }
        }

        private void ErsteSekundenAnpassen()
        {
            //if(cb_Zeitspanne.IsChecked == true)
            //{
            //    cb_ErsteSekunden.IsEnabled = false;
            //}

            if (cb_ErsteSekunden.IsChecked == true)
            {
                //txt_ErsteSekunden_info1.Foreground = Brushes.Black;
                //txt_ErsteSekunden_info2.Foreground = Brushes.Black;
                tb_ErsteSekunden_Sekunden.IsEnabled = true;
                tb_ErsteSekunden_Sekunden.IsReadOnly = false;
                cb_Zeitspanne.IsEnabled = false;
            }
            else
            {
                //txt_ErsteSekunden_info1.Foreground = Brushes.Silver;
                //txt_ErsteSekunden_info2.Foreground = Brushes.Silver;
                tb_ErsteSekunden_Sekunden.IsEnabled = false;
                tb_ErsteSekunden_Sekunden.IsReadOnly = true;
                cb_Zeitspanne.IsEnabled = true;
            }
        }

        private string GetSelectedAudioFormat()
        {
            if (!Dispatcher.CheckAccess())
            {
                // Wenn nicht im UI-Thread, dann Dispatcher verwenden
                return Dispatcher.Invoke(() => GetSelectedAudioFormat());
            }

            if (AudioFormatComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                return selectedItem.Content.ToString();
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
                Arguments = $"--get-title \"{url}\"",
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

            // Warte auf Prozessende, prüfe auf Abbruch
            var waitTask = process.WaitForExitAsync(token);
            await waitTask;

            if (token.IsCancellationRequested)
            {
                try { process.Kill(true); } catch { }
                token.ThrowIfCancellationRequested();
            }

            return title ?? "";
        }

        private void History_Click(object sender, RoutedEventArgs e)
        {
            var win = new DownloadHistoryWindow();
            win.Owner = this;
            win.ShowDialog();
        }

        private void NachFertigemDownload(string url, string title, string downloaddirectory)
        {
            // Hier die Logik nach dem erfolgreichen Download einfügen
            DownloadHistoryService.Add(new DownloadHistoryEntry
            {
                Url = url,
                Title = title,
                DownloadDirectory = downloaddirectory,
                DownloadedAt = DateTime.Now
            });
        }

        private void OpenGitHub_Click(object sender, RoutedEventArgs e)
        {
            string url = "https://github.com/MortysTerminal/MortysDLP";
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
                MessageBox.Show("Der Browser konnte nicht geöffnet werden.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

        private void RunYtDlpAsync(string YtDlpPath, string arguments, CancellationToken token)
        {
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
                    AppendOutput(e.Data);
                }
            };

            _ytDlpProcess.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
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
                        try
                        {
                            _ytDlpProcess.Kill(true);
                        }
                        catch { }
                        break;
                    }
                    Thread.Sleep(100);
                }

                _ytDlpProcess.WaitForExit();

                // Prüfe, ob abgebrochen wurde
                if (token.IsCancellationRequested)
                {
                    Dispatcher.Invoke(() =>
                    {
                        SetStatusIcon(StatusIconType.Error);
                        UpdateProgress(_lastProgress, isError: true);
                        DownloadStatusText.Text = "Abgebrochen";
                        btn_download_abbrechen.IsEnabled = false;
                    });
                    AppendOutput("[INFO] Download wurde abgebrochen.");
                    return; // Keine weiteren Status-Updates!
                }

                if (_ytDlpProcess.ExitCode != 0)
                {
                    AppendOutput($"[FEHLER] yt-dlp wurde mit ExitCode {_ytDlpProcess.ExitCode} beendet.");
                    Dispatcher.Invoke(() =>
                    {
                        SetStatusIcon(StatusIconType.Error);
                        UpdateProgress(_lastProgress, isError: true);
                        DownloadStatusText.Text = "Fehler";
                        btn_download_abbrechen.IsEnabled = false;
                    });
                }
                else
                {
                    AppendOutput($"Prozess beendet mit Code: {_ytDlpProcess.ExitCode}");
                    Dispatcher.Invoke(() =>
                    {
                        SetStatusIcon(StatusIconType.Success);
                        UpdateProgress(100);
                        DownloadStatusText.Text = "Abgeschlossen";
                        btn_download_abbrechen.IsEnabled = false;
                    });
                }
            }
            catch (Exception ex)
            {
                AppendOutput($"[EXCEPTION] {ex.Message}");
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(ex.Message, "Interner Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                    btn_download_abbrechen.IsEnabled = false;
                });
            }
        }
        private void StatusIcon_Click(object sender, RoutedEventArgs e)
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
                MessageBox.Show("Downloadpfad nicht gefunden!", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SelectAudioFormat(string savedFormat)
        {
            // Angenommener gespeichterter Wert
            //string savedFormat = Properties.Settings.Default.AudioFormat ?? "mp3";

            // Suche nach dem gespeicherten Eintrag und setze ihn als ausgewählt
            // Standardwert setzen, falls das gespeicherte Format nicht gefunden wird
            bool formatFound = false;

            foreach (ComboBoxItem item in AudioFormatComboBox.Items)
            {
                if (item.Content.ToString() == savedFormat)
                {
                    AudioFormatComboBox.SelectedItem = item;
                    formatFound = true;
                    break;
                }
            }

            // Wenn das Format nicht gefunden wurde, "mp3" auswählen
            if (!formatFound)
            {
                foreach (ComboBoxItem item in AudioFormatComboBox.Items)
                {
                    if (item.Content.ToString() == "mp3")
                    {
                        AudioFormatComboBox.SelectedItem = item;
                        break;
                    }
                }
            }
        }

        private void SetStatusIcon(StatusIconType type)
        {
            Dispatcher.Invoke(() =>
            {
                StatusIcon.Spin = false; // Standard: keine Animation
                btn_StatusIcon.IsEnabled = (type == StatusIconType.Success || type == StatusIconType.Error);
                switch (type)
                {
                    case StatusIconType.Loading:
                        StatusIcon.Icon = FontAwesome.WPF.FontAwesomeIcon.Spinner;
                        StatusIcon.Spin = true; // Animation aktivieren
                        StatusIcon.Foreground = new SolidColorBrush(Colors.SteelBlue);
                        break;

                    case StatusIconType.Success:
                        StatusIcon.Icon = FontAwesome.WPF.FontAwesomeIcon.CheckCircle;
                        StatusIcon.Foreground = new SolidColorBrush(Colors.Green);
                        break;

                    case StatusIconType.Error:
                        StatusIcon.Icon = FontAwesome.WPF.FontAwesomeIcon.TimesCircle;
                        StatusIcon.Foreground = new SolidColorBrush(Colors.Red);
                        break;

                    default:
                        StatusIcon.Icon = FontAwesome.WPF.FontAwesomeIcon.None;
                        break;
                }
            });
        }

        private void SetUiEnabled(bool enabled)
        {
            tb_URL.IsEnabled = enabled;
            cb_Zeitspanne.IsEnabled = enabled;
            tb_zeitspanne_von.IsEnabled = enabled && cb_Zeitspanne.IsChecked == true;
            tb_zeitspanne_bis.IsEnabled = enabled && cb_Zeitspanne.IsChecked == true;
            cb_ErsteSekunden.IsEnabled = enabled;
            tb_ErsteSekunden_Sekunden.IsEnabled = enabled && cb_ErsteSekunden.IsChecked == true;
            cb_Videoformat.IsEnabled = enabled;
            cb_AudioOnly.IsEnabled = enabled;
            AudioFormatComboBox.IsEnabled = enabled && cb_AudioOnly.IsChecked == true;
            btn_einstellungen_speichern.IsEnabled = enabled;
            btn_History.IsEnabled = enabled;
            btn_download_starten.IsEnabled = enabled;
            btn_Header_Einstellungen.IsEnabled = enabled;
            // Menüeinträge ggf. sperren
            // DownloadPathMenu_Click ist im Menü, ggf. Menü sperren:
            // Menü kannst du z.B. über Menu.IsEnabled = enabled; sperren, falls du einen Namen vergeben hast.
        }

        internal void SetUiAudioEnabled(bool enabled)
        {
            if(enabled == true)
            {
                dp_AudioPath.Visibility = Visibility.Visible;
                //lbl_audiopath.Visibility = Visibility.Visible;
                //lbl_audiopath_info.Visibility = Visibility.Visible;
            }
            else
            {
                dp_AudioPath.Visibility = Visibility.Collapsed;
                //lbl_audiopath.Visibility = Visibility.Collapsed;
                //lbl_audiopath_info.Visibility = Visibility.Collapsed;
            }
                
        }

        private void SetzeDownloadPfadInEinstellungen()
        {
            // Prüfe, ob der gespeicherte Downloadpfad existiert und gültig ist
            string gespeicherterPfad = Properties.Settings.Default.DOWNLOADPATH;
            if (string.IsNullOrEmpty(gespeicherterPfad) || !System.IO.Directory.Exists(gespeicherterPfad))
            {
                // Fallback: KnownFolder Downloads verwenden
                string downloadsFolder = KnownFolders.GetPath(KnownFolder.Downloads);
                Properties.Settings.Default.DOWNLOADPATH = downloadsFolder.ToString();
                Properties.Settings.Default.Save();
            }
        }

        private void SetzeAudioDownloadPfadInEinstellungen()
        {
            // Prüfe, ob der gespeicherte Downloadpfad existiert und gültig ist
            string gespeicherterAudioPfad = Properties.Settings.Default.DOWNLOADAUDIOONLYPATH;
            if (string.IsNullOrEmpty(gespeicherterAudioPfad) || !System.IO.Directory.Exists(gespeicherterAudioPfad))
            {
                // Fallback: KnownFolder Downloads verwenden
                string downloadsFolder = KnownFolders.GetPath(KnownFolder.Downloads);
                Properties.Settings.Default.DOWNLOADAUDIOONLYPATH = downloadsFolder.ToString();
                Properties.Settings.Default.Save();
            }
        }

        private void StarteDownload(CancellationToken token)
        {
            string args = BaueYTDLPArgumente();
            RunYtDlpAsync(Properties.Settings.Default.YTDLPPATH, args, token);
        }
        private void tb_ErsteSekunden_Sekunden_TextChanged(object sender, TextChangedEventArgs e)
        {
            ValidateDownloadButton();
        }

        private void tb_URL_GotFocus(object sender, RoutedEventArgs e)
        {
            tb_URL.SelectAll();
        }
        private void tb_URL_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                btn_download_starten.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                e.Handled = true;
            }
        }

        private void tb_URL_TextChanged(object sender, TextChangedEventArgs e)
        {
            ValidateDownloadButton();
        }
        private void tb_zeitspanne_von_TextChanged(object sender, TextChangedEventArgs e)
        {
            ValidateDownloadButton();
        }
        private void tb_zeitspanne_bis_TextChanged(object sender, TextChangedEventArgs e)
        {
            ValidateDownloadButton();
        }

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                Dispatcher.InvokeAsync(() =>
                {
                    textBox.SelectAll();
                    //textBox.Focus(); // Erneut den Fokus sicherstellen
                });
            }
        }

        private void topBar_Info_Click(object sender, RoutedEventArgs e)
        {
        }

        private void UpdateProgress(double percent, bool isError = false, double? speedMBs = null)
        {
            Dispatcher.Invoke(() =>
            {
                _lastProgress = percent;
                DownloadProgressBar.Value = percent;
                if (isError)
                {
                    DownloadProgressBar.Foreground = new SolidColorBrush(Colors.Red);
                    DownloadProgressText.Text = "Fehler";
                }
                else
                {
                    string speedText = speedMBs.HasValue ? $" ({speedMBs.Value:F2} MB/s)" : "";
                    DownloadProgressBar.Foreground = new SolidColorBrush(Colors.SteelBlue);
                    DownloadProgressText.Text = $"{percent:F1} % {speedText}";
                }
            });
        }
        private void ValidateDownloadButton()
        {
            bool urlOk = !string.IsNullOrWhiteSpace(tb_URL.Text);

            bool zeitspanneOk = !cb_Zeitspanne.IsChecked == true ||
                (!string.IsNullOrWhiteSpace(tb_zeitspanne_von.Text) && !string.IsNullOrWhiteSpace(tb_zeitspanne_bis.Text));

            bool sekundenOk = !cb_ErsteSekunden.IsChecked == true ||
                !string.IsNullOrWhiteSpace(tb_ErsteSekunden_Sekunden.Text);

            btn_download_starten.IsEnabled = urlOk && zeitspanneOk && sekundenOk;
        }

        private void VideoSchnittFormatAnpassen()

        {
            if (cb_Videoformat.IsChecked == true)
            {
                //txt_Videoformat_info.Foreground = Brushes.Black;
                txt_Videoformat_info.IsEnabled = true;

                cb_AudioOnly.IsEnabled = false;
            }
            else
            {
                //txt_Videoformat_info.Foreground = Brushes.Silver;
                txt_Videoformat_info.IsEnabled = false;

                cb_AudioOnly.IsEnabled = true;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Downloadpfad aus Ressourcen lesen und Label setzen
            var downloadPath = Properties.Settings.Default.DOWNLOADPATH;
            var audioDownloadPath = Properties.Settings.Default.DOWNLOADAUDIOONLYPATH;
            lbl_downloadpath.Content = downloadPath;
            lbl_audiopath.Content = audioDownloadPath;
        }

        private void ZeitspanneAnpassen()
        {
            if (cb_Zeitspanne.IsChecked == true)
            {
                //txt_zeitspanne_von.Foreground = Brushes.Black;
                //txt_zeitspanne_info.Foreground = Brushes.Black;
                //txt_zeitspanne_bindestrich.Foreground = Brushes.Black;
                tb_zeitspanne_von.IsReadOnly = false;
                tb_zeitspanne_von.IsEnabled = true;
                tb_zeitspanne_bis.IsReadOnly = false;
                tb_zeitspanne_bis.IsEnabled = true;
                cb_ErsteSekunden.IsEnabled = false;
            }
            else
            {
                //txt_zeitspanne_von.Foreground = Brushes.Silver;
                //txt_zeitspanne_info.Foreground = Brushes.Silver;
                //txt_zeitspanne_bindestrich.Foreground = Brushes.Silver;
                tb_zeitspanne_von.IsReadOnly = true;
                tb_zeitspanne_von.IsEnabled = false;
                tb_zeitspanne_bis.IsReadOnly = true;
                tb_zeitspanne_bis.IsEnabled = false;
                cb_ErsteSekunden.IsEnabled = true;
            }
            ValidateDownloadButton();
        }

        private void OutputTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            OutputTextBox.ScrollToEnd();
        }

        private void TwitchButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(new ProcessStartInfo
            {
                FileName = "https://www.twitch.tv/mortys_welt",
                UseShellExecute = true
            });
        }

        private void GitHubButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/MortysTerminal/MortysDLP",
                UseShellExecute = true
            });
        }
    }
}