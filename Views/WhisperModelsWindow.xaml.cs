using MortysDLP.Models;
using MortysDLP.Services;
using MortysDLP.UITexte;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace MortysDLP.Views
{
    public partial class WhisperModelsWindow : Window
    {
        private CancellationTokenSource? _cts;
        private bool _busy = false;

        public WhisperModelsWindow()
        {
            InitializeComponent();
            SetUITexts();
            Loaded += (_, _) => RefreshModelList();
        }

        private void SetUITexts()
        {
            var T = UITextDictionary.Get;
            var lang = UITextDictionary.CurrentLanguage;

            Title = T("WhisperModels.Title");
            txtHeaderTitle.Text = T("WhisperModels.Header.Title");
            txtHeaderSubtitle.Text = T("WhisperModels.Header.Subtitle");
            btnInstallWhisper.Content = T("WhisperModels.Button.InstallWhisper");
            btnUninstall.Content = T("WhisperModels.Button.Uninstall");
            btnClose.Content = T("WhisperModels.Button.Close");

            bool whisperInstalled = WhisperService.IsWhisperInstalled();
            string statusKey = whisperInstalled ? "WhisperModels.Whisper.Installed" : "WhisperModels.Whisper.NotInstalled";
            txtWhisperEngineStatus.Text = string.Format(T("WhisperModels.Whisper.Status"), T(statusKey));
            txtModelsDir.Text = string.Format(T("WhisperModels.Info.ModelsDir"),
                Path.GetFullPath(WhisperService.ModelsDirectory));
            btnUninstall.Visibility = whisperInstalled ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RefreshModelList()
        {
            var T = UITextDictionary.Get;
            var lang = UITextDictionary.CurrentLanguage;
            string modelsDir = WhisperService.ModelsDirectory;

            var items = WhisperModelInfo.All.Select(m => new ModelViewModel
            {
                ModelId = m.Id,
                DisplayName = m.GetDisplayName(lang),
                Description = m.GetDescription(lang),
                SizeHint = m.SizeHint,
                IsInstalled = m.IsDownloaded(modelsDir),
                CanDownload = !_busy,
                CanDelete = !_busy,
                DownloadButtonText = T("WhisperModels.Button.Download"),
                DeleteButtonText = T("WhisperModels.Button.Delete"),
            }).ToList();

            foreach (var item in items)
            {
                item.StatusText = item.IsInstalled ? T("WhisperModels.Status.Installed") : T("WhisperModels.Status.NotInstalled");
                item.StatusColor = item.IsInstalled ? "#22C55E" : "#94A3B8";
                item.DownloadVisibility = item.IsInstalled ? Visibility.Collapsed : Visibility.Visible;
                item.DeleteVisibility = item.IsInstalled ? Visibility.Visible : Visibility.Collapsed;
            }

            icModels.ItemsSource = items;

            bool whisperInstalled = WhisperService.IsWhisperInstalled();
            bool hasAnyModel = items.Any(i => i.IsInstalled);

            // Modellliste nur zeigen wenn Whisper installiert ist ODER bereits Modelle vorhanden sind
            scrollModels.Visibility = (whisperInstalled || hasAnyModel) ? Visibility.Visible : Visibility.Collapsed;

            string statusKey = whisperInstalled ? "WhisperModels.Whisper.Installed" : "WhisperModels.Whisper.NotInstalled";
            txtWhisperEngineStatus.Text = string.Format(T("WhisperModels.Whisper.Status"), T(statusKey));
            btnUninstall.Visibility = whisperInstalled ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void btnInstallWhisper_Click(object sender, RoutedEventArgs e)
        {
            if (_busy) return;
            var T = UITextDictionary.Get;

            SetBusy(true, T("WhisperModels.Installing"));

            var service = new WhisperUpdateService();
            string tempZip = Path.Combine(Path.GetTempPath(), $"whisper_download_{Guid.NewGuid():N}.zip");

            try
            {
                _cts = new CancellationTokenSource();

                var (version, assetUrl) = await service.GetLatestReleaseInfoAsync();
                if (string.IsNullOrEmpty(assetUrl))
                {
                    FluentMessageBox.Show(T("WhisperModels.NoAsset"), icon: MessageBoxImage.Warning, owner: this);
                    return;
                }

                var progress = new Progress<double>(v =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        pbProgress.Value = v * 100;
                        txtProgressPercent.Text = $"{v * 100:F0} %";
                    });
                });

                await service.DownloadAssetAsync(assetUrl, tempZip, progress, _cts.Token);
                SetStatus(T("StartupWindow.Ffmpeg.Extracting") ?? "Entpacke...");

                string whisperExe = Properties.Settings.Default.WhisperPath;
                string? whisperDir = Path.GetDirectoryName(whisperExe);
                if (!string.IsNullOrEmpty(whisperDir) && !Directory.Exists(whisperDir))
                    Directory.CreateDirectory(whisperDir);

                bool extracted = await WhisperUpdateService.ExtractWhisperExeFromZipAsync(tempZip, whisperExe);
                if (!extracted)
                {
                    FluentMessageBox.Show(string.Format(T("WhisperModels.Error.Install"), "Executable not found in ZIP"),
                        icon: MessageBoxImage.Error, owner: this);
                    return;
                }

                FluentMessageBox.Show(T("WhisperModels.Success.Install"),
                    icon: MessageBoxImage.Information, owner: this);
                RefreshModelList();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                FluentMessageBox.Show(string.Format(T("WhisperModels.Error.Install"), ex.Message),
                    icon: MessageBoxImage.Error, owner: this);
            }
            finally
            {
                try { if (File.Exists(tempZip)) File.Delete(tempZip); } catch { }
                SetBusy(false);
            }
        }

        private async void btnDownloadModel_Click(object sender, RoutedEventArgs e)
        {
            if (_busy || sender is not System.Windows.Controls.Button btn) return;
            string? modelId = btn.Tag?.ToString();
            if (string.IsNullOrEmpty(modelId)) return;

            var model = WhisperModelInfo.All.FirstOrDefault(m => m.Id == modelId);
            if (model == null) return;

            var T = UITextDictionary.Get;
            SetBusy(true, string.Format(T("WhisperModels.Downloading"), model.GetDisplayName(UITextDictionary.CurrentLanguage)));

            WhisperService.EnsureModelsDirExists();

            _cts = new CancellationTokenSource();
            var service = new WhisperUpdateService();
            string targetPath = Path.Combine(WhisperService.ModelsDirectory, model.FileName);

            try
            {
                var progress = new Progress<double>(v => Dispatcher.Invoke(() =>
                {
                    pbProgress.Value = v * 100;
                    txtProgressPercent.Text = $"{v * 100:F0} %";
                }));

                await service.DownloadModelAsync(model.DownloadUrl, targetPath, progress, _cts.Token);

                FluentMessageBox.Show(
                    string.Format(T("WhisperModels.Success.Download"),
                        model.GetDisplayName(UITextDictionary.CurrentLanguage)),
                    icon: MessageBoxImage.Information, owner: this);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                FluentMessageBox.Show(string.Format(T("WhisperModels.Error.Download"), ex.Message),
                    icon: MessageBoxImage.Error, owner: this);
                try { if (File.Exists(targetPath)) File.Delete(targetPath); } catch { }
            }
            finally
            {
                SetBusy(false);
                RefreshModelList();
            }
        }

        private void btnDeleteModel_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button btn) return;
            string? modelId = btn.Tag?.ToString();
            if (string.IsNullOrEmpty(modelId)) return;

            var model = WhisperModelInfo.All.FirstOrDefault(m => m.Id == modelId);
            if (model == null) return;

            var T = UITextDictionary.Get;
            var lang = UITextDictionary.CurrentLanguage;

            var result = FluentMessageBox.Show(
                string.Format(T("WhisperModels.Delete.Question"), model.GetDisplayName(lang)),
                T("WhisperModels.Delete.Title"),
                MessageBoxButton.YesNo, MessageBoxImage.Warning, this);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                string path = Path.Combine(WhisperService.ModelsDirectory, model.FileName);
                if (File.Exists(path)) File.Delete(path);
                RefreshModelList();
            }
            catch (Exception ex)
            {
                FluentMessageBox.Show(string.Format(T("WhisperModels.Error.Delete"), ex.Message),
                    icon: MessageBoxImage.Error, owner: this);
            }
        }

        private async void btnUninstall_Click(object sender, RoutedEventArgs e)
        {
            if (_busy) return;
            var T = UITextDictionary.Get;

            // Dialog: Erklärungstext als Nachricht, kurze Labels als Buttons
            var dlgResult = FluentMessageBox.Show(
                T("WhisperModels.Uninstall.Message"),
                T("WhisperModels.Uninstall.Title"),
                MessageBoxImage.Warning,
                this,
                (T("WhisperModels.Uninstall.Btn.KeepModels"), MessageBoxResult.Yes,    false),
                (T("WhisperModels.Uninstall.Btn.Full"),        MessageBoxResult.No,     false),
                (T("WhisperModels.Uninstall.Cancel"),          MessageBoxResult.Cancel, true));

            if (dlgResult == MessageBoxResult.Cancel) return;

            bool keepModels = dlgResult == MessageBoxResult.Yes;

            SetBusy(true, T("WhisperModels.Uninstall.Title"));
            try
            {
                await WhisperService.UninstallAsync(keepModels);
                FluentMessageBox.Show(T("WhisperModels.Uninstall.Success"),
                    icon: MessageBoxImage.Information, owner: this);
                RefreshModelList();
            }
            catch (Exception ex)
            {
                FluentMessageBox.Show(string.Format(T("WhisperModels.Uninstall.Error"), ex.Message),
                    icon: MessageBoxImage.Error, owner: this);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _cts?.Cancel();
            base.OnClosing(e);
        }

        private void SetBusy(bool busy, string? statusText = null)
        {
            _busy = busy;
            Dispatcher.Invoke(() =>
            {
                btnInstallWhisper.IsEnabled = !busy;
                btnUninstall.IsEnabled = !busy;
                btnClose.IsEnabled = !busy;
                pnlProgress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
                if (busy && statusText != null)
                {
                    txtProgressLabel.Text = statusText;
                    pbProgress.Value = 0;
                    txtProgressPercent.Text = "0 %";
                }
            });
            if (!busy) RefreshModelList();
        }

        private void SetStatus(string text) => Dispatcher.Invoke(() => txtProgressLabel.Text = text);
    }

    // ── ViewModel für die Modellliste ──────────────────────────────────────────────
    internal class ModelViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public string ModelId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Description { get; set; } = "";
        public string SizeHint { get; set; } = "";
        public bool IsInstalled { get; set; }
        public string StatusText { get; set; } = "";
        public string StatusColor { get; set; } = "#94A3B8";
        public bool CanDownload { get; set; }
        public bool CanDelete { get; set; }
        public string DownloadButtonText { get; set; } = "Download";
        public string DeleteButtonText { get; set; } = "Delete";
        public Visibility DownloadVisibility { get; set; }
        public Visibility DeleteVisibility { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
