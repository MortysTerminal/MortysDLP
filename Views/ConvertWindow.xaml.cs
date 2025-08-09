using Microsoft.Win32;
using MortysDLP.Models;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace MortysDLP.Views
{
    public partial class ConvertWindow : Window
    {
        private ObservableCollection<ConvertFileItem> _fileList = new();
        private CancellationTokenSource? _convertCancellationTokenSource;

        private const int MaxDebugLines = 200;

        public ConvertWindow()
        {
            InitializeComponent();
            dgFiles.ItemsSource = _fileList;
            cbTargetFormat.ItemsSource = Enum.GetValues(typeof(VideoFormat))
                .Cast<VideoFormat>()
                .Select(v => v.ToString().ToLower())
                .Concat(Enum.GetValues(typeof(AudioFormat)).Cast<AudioFormat>().Select(a => a.ToString().ToLower()))
                .ToList();

            cbTargetFormat.SelectedIndex = 0;
            btnConvertCancel.IsEnabled = false; // Immer initial deaktiviert
            UpdateFileButtonsState();
        }

        private void btnAddFiles_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Medien-Dateien|*.mov;*.mp4;*.mkv;*.avi;*.mp3;*.aac;*.wav|Alle Dateien|*.*",
                Multiselect = true
            };
            if (dlg.ShowDialog() == true)
            {
                foreach (var file in dlg.FileNames)
                {
                    if (!_fileList.Any(f => f.SourcePath == file))
                        _fileList.Add(new ConvertFileItem { SourcePath = file });
                }
            }
            UpdateFileButtonsState();
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
            btnConvertCancel.IsEnabled = true; // Nur hier aktivieren

            string targetFolder = tbTargetFolder.Text;
            var selectedFormatStr = cbTargetFormat.SelectedItem?.ToString()?.ToLower() ?? "mp3";
            string ffmpegPath = Properties.Settings.Default.FfmpegPath;

            if (string.IsNullOrWhiteSpace(targetFolder) || !_fileList.Any())
            {
                MessageBox.Show("Bitte Zielordner und mindestens eine Datei auswählen.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateFileButtonsState();
                btnConvertCancel.IsEnabled = false;
                return;
            }

            _convertCancellationTokenSource = new CancellationTokenSource();
            var token = _convertCancellationTokenSource.Token;

            var tasks = _fileList.Select(async file =>
            {
                bool isVideo = Enum.TryParse<VideoFormat>(selectedFormatStr, true, out var videoFormat);
                bool isAudio = Enum.TryParse<AudioFormat>(selectedFormatStr, true, out var audioFormat);

                // Qualitätsparameter auslesen
                string videoQuality = cbVideoQuality.IsEnabled && cbVideoQuality.SelectedItem is ComboBoxItem vqItem
                    ? vqItem.Content?.ToString() ?? ""
                    : "";
                string audioQuality = cbAudioQuality.IsEnabled && cbAudioQuality.SelectedItem is ComboBoxItem aqItem
                    ? aqItem.Content?.ToString() ?? ""
                    : "";

                // Dateinamen mit Qualität ergänzen
                string qualitySuffix = "";
                if (isVideo && cbVideoQuality.IsEnabled && !string.IsNullOrWhiteSpace(videoQuality) && !videoQuality.Contains("Original"))
                    qualitySuffix = $"_{videoQuality}";
                else if (isAudio && cbAudioQuality.IsEnabled && !string.IsNullOrWhiteSpace(audioQuality) && !audioQuality.Contains("Original"))
                    qualitySuffix = $"_{audioQuality}";

                string extension = isVideo ? videoFormat.ToString().ToLower() : audioFormat.ToString().ToLower();
                string targetFile = Path.Combine(
                    targetFolder,
                    Path.GetFileNameWithoutExtension(file.SourcePath) + qualitySuffix + "." + extension
                );

                if (System.IO.File.Exists(targetFile))
                {
                    file.Status = "Schon konvertiert";
                    file.Progress = 100;
                    Dispatcher.Invoke(() => dgFiles.Items.Refresh());
                    return;
                }

                file.Status = "Wird konvertiert...";
                file.Progress = 0;
                Dispatcher.Invoke(() => dgFiles.Items.Refresh());

                // ffmpeg-Argumente dynamisch bauen
                string args = $"-y -i \"{file.SourcePath}\"";

                if (isVideo)
                {
                    if (cbVideoQuality.IsEnabled && !string.IsNullOrWhiteSpace(videoQuality) && !videoQuality.Contains("Original"))
                    {
                        args += $" -vf scale=-2:{videoQuality.Replace("p", "")}";
                    }
                }
                if (isAudio)
                {
                    if (cbAudioQuality.IsEnabled && !string.IsNullOrWhiteSpace(audioQuality) && !audioQuality.Contains("Original"))
                    {
                        args += $" -b:a {audioQuality}";
                    }
                }

                args += $" \"{targetFile}\"";

                try
                {
                    await RunFfmpegForItemAsync(file, ffmpegPath, args, token);
                    file.Status = "Fertig";
                    file.Progress = 100;
                }
                catch (OperationCanceledException)
                {
                    file.Status = "Abgebrochen";
                }
                catch (Exception ex)
                {
                    file.Status = "Fehler";
                    AppendDebugOutput($"[{file.Name}] Fehler: {ex.Message}");
                }
                Dispatcher.Invoke(() => dgFiles.Items.Refresh());
            }).ToList();

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
                // Abbruch wurde ausgelöst
            }

            btnConvertCancel.IsEnabled = false; // Nach Abschluss wieder deaktivieren
            UpdateFileButtonsState(); // Buttons nach Konvertierung aktualisieren
        }
        private void btnBrowseTargetFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog
            {
                Description = "Bitte wählen Sie den Zielordner für die konvertierten Dateien aus.",
                SelectedPath = tbTargetFolder.Text,
                UseDescriptionForTitle = true
            };
            if (dialog.ShowDialog(this) == true)
            {
                tbTargetFolder.Text = dialog.SelectedPath;
            }
        }
        private void btnUseSavedDownloadpath_Click(object sender, RoutedEventArgs e)
        {
            string savedPath = Properties.Settings.Default.DownloadPath;
            if (!string.IsNullOrWhiteSpace(savedPath) && System.IO.Directory.Exists(savedPath))
            {
                tbTargetFolder.Text = savedPath;
            }
        }
        private void btnUseSavedAudioOnlyPath_Click(object sender, RoutedEventArgs e)
        {
            string savedAudioPath = Properties.Settings.Default.DownloadAudioOnlyPath;
            if (!string.IsNullOrWhiteSpace(savedAudioPath) && Directory.Exists(savedAudioPath))
            {
                tbTargetFolder.Text = savedAudioPath;
            }
        }

        private async Task RunFfmpegForItemAsync(ConvertFileItem file, string ffmpegPath, string arguments, CancellationToken token)
        {
            string ffprobePath = Properties.Settings.Default.FfprobePath;
            double totalSeconds = GetMediaDurationInSeconds(ffprobePath, file.SourcePath) ?? 0;

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = arguments,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
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
                }
            };

            process.Start();
            process.BeginErrorReadLine();

            while (!process.HasExited)
            {
                if (token.IsCancellationRequested)
                {
                    try { process.Kill(true); } catch { }
                    throw new OperationCanceledException(token);
                }
                await Task.Delay(100, token);
            }
            await process.WaitForExitAsync(token);
        }

        private double? ParseFfmpegProgress(string line, double totalSeconds)
        {
            var match = System.Text.RegularExpressions.Regex.Match(line, @"time=(\d{2}:\d{2}:\d{2}\.\d{2})");
            if (match.Success)
            {
                if (TimeSpan.TryParseExact(match.Groups[1].Value, @"hh\:mm\:ss\.ff", null, out var current))
                {
                    if (totalSeconds > 0)
                    {
                        var percent = (current.TotalSeconds / totalSeconds) * 100.0;
                        return Math.Max(0, Math.Min(100, percent));
                    }
                }
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
                process?.WaitForExit();
                if (double.TryParse(output, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double seconds))
                    return seconds;
            }
            catch { }
            return null;
        }

        private void btnConvertCancel_Click(object sender, RoutedEventArgs e)
        {
            _convertCancellationTokenSource?.Cancel();
            btnConvertCancel.IsEnabled = false; // Sofort deaktivieren nach Klick
        }
        private void btnClearFiles_Click(object sender, RoutedEventArgs e)
        {
            _fileList.Clear();
            UpdateFileButtonsState();
        }
        private void cbTargetFormat_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = cbTargetFormat.SelectedItem?.ToString()?.ToLower();
            // Videoformate aktivieren
            cbVideoQuality.IsEnabled = Enum.TryParse<VideoFormat>(selected, true, out _);
        }

        private void UpdateFileButtonsState()
        {
            bool hasFiles = _fileList.Any();
            btnRemoveFiles.IsEnabled = hasFiles;
            btnClearFiles.IsEnabled = hasFiles;
            btnConvertStart.IsEnabled = hasFiles;
            // btnConvertCancel wird nur während einer Konvertierung aktiviert/deaktiviert
        }

        private void AppendDebugOutput(string text)
        {
            tbDebugOutput.AppendText(text + Environment.NewLine);
            tbDebugOutput.ScrollToEnd();

            // Zeilen begrenzen
            var lines = tbDebugOutput.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            if (lines.Length > MaxDebugLines)
            {
                tbDebugOutput.Text = string.Join(Environment.NewLine, lines.Skip(lines.Length - MaxDebugLines));
                tbDebugOutput.CaretIndex = tbDebugOutput.Text.Length;
                tbDebugOutput.ScrollToEnd();
            }
        }

        private void btnOpenTargetFolder_Click(object sender, RoutedEventArgs e)
        {
            string folder = tbTargetFolder.Text;
            if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });
            }
            else
            {
                MessageBox.Show("Der Zielordner existiert nicht oder ist ungültig.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}