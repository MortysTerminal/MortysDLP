using MortysDLP.Helpers;
using MortysDLP.Services;
using MortysDLP.Services.Tools;
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
            btnCancelProgress.Content = T("Common.Button.Cancel");

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

            var items = WhisperModelCatalog.All.Select(m => new ModelViewModel
            {
                ModelId = m.Id,
                DisplayName = m.GetDisplayName(lang),
                Description = m.GetDescription(lang),
                SizeHint = m.FormatSize(),
                State = WhisperModelStore.GetState(m, modelsDir),
                CanDownload = !_busy,
                CanDelete = !_busy,
                DownloadButtonText = T("WhisperModels.Button.Download"),
                DeleteButtonText = T("WhisperModels.Button.Delete"),
            }).ToList();

            // Drei Zustände statt zwei: nicht vorhanden, unvollständig (Größe außerhalb der
            // Toleranz - typischerweise ein abgebrochener Download), vollständig. Unvollständig
            // bekommt beide Aktionen: erneut laden (überschreibt) oder löschen.
            foreach (var item in items)
            {
                (item.StatusText, item.StatusColor) = item.State switch
                {
                    WhisperModelState.Complete => (T("WhisperModels.Status.Installed"), "#22C55E"),
                    WhisperModelState.Incomplete => (T("WhisperModels.Status.Incomplete"), "#F59E0B"),
                    _ => (T("WhisperModels.Status.NotInstalled"), "#94A3B8"),
                };
                item.DownloadVisibility = item.State == WhisperModelState.Complete ? Visibility.Collapsed : Visibility.Visible;
                item.DeleteVisibility = item.State == WhisperModelState.NotPresent ? Visibility.Collapsed : Visibility.Visible;
            }

            icModels.ItemsSource = items;

            bool whisperInstalled = WhisperService.IsWhisperInstalled();
            bool hasAnyModel = items.Any(i => i.State != WhisperModelState.NotPresent);

            // Modellliste nur zeigen wenn Whisper installiert ist ODER bereits Modelle vorhanden sind
            scrollModels.Visibility = (whisperInstalled || hasAnyModel) ? Visibility.Visible : Visibility.Collapsed;

            string statusKey = whisperInstalled ? "WhisperModels.Whisper.Installed" : "WhisperModels.Whisper.NotInstalled";
            txtWhisperEngineStatus.Text = string.Format(T("WhisperModels.Whisper.Status"), T(statusKey));
            btnUninstall.Visibility = whisperInstalled ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Installiert oder aktualisiert whisper.cpp über <see cref="WhisperTool"/> — dieselbe
        /// Abstraktion wie bei yt-dlp und ffmpeg (Rückfallebene und Erfolgskontrolle inklusive),
        /// statt der früheren eigenen Release-Abfrage und ZIP-Entpackung dieser Klasse. Das
        /// Entpacken selbst läuft dabei in <see cref="Task.Run"/> und meldet über
        /// <paramref name="stage"/> ausdrücklich „Entpacke..." statt die Oberfläche stumm
        /// einfrieren zu lassen.
        /// </summary>
        private async void btnInstallWhisper_Click(object sender, RoutedEventArgs e)
        {
            if (_busy) return;
            var T = UITextDictionary.Get;

            SetBusy(true, T("WhisperModels.Installing"));

            var tool = new WhisperTool();

            try
            {
                _cts = new CancellationTokenSource();

                var catalog = new ToolCatalog();
                var outcome = await catalog.CheckAsync(tool, force: false, _cts.Token);
                var release = await catalog.ResolveForInstallAsync(outcome, _cts.Token);

                var progress = new Progress<double>(v => Dispatcher.Invoke(() =>
                {
                    pbProgress.Value = v * 100;
                    txtProgressPercent.Text = $"{v * 100:F0} %";
                }));
                var stage = new Progress<ToolInstallStage>(s => SetStatus(StageText(s, tool.DisplayName, T)));

                var result = await tool.InstallAsync(release, progress, stage, _cts.Token);

                if (!result.Success)
                {
                    // Wie im Startablauf: die technische Ursache steht im Protokoll (das hat
                    // WhisperTool bereits geschrieben), der Dialog nennt nur, was der Nutzer
                    // wissen muss (02-BEST-PRACTICES.md, Abschnitt 6).
                    if (result.Status == ToolInstallStatus.RolledBack)
                    {
                        FluentMessageBox.Show(string.Format(T("StartupWindow.ToolUpdate.RolledBack"), tool.DisplayName),
                            icon: MessageBoxImage.Warning, owner: this);
                    }
                    else if (result.Status != ToolInstallStatus.Canceled)
                    {
                        FluentMessageBox.Show(string.Format(T("StartupWindow.Tool.InstallFailed"), tool.DisplayName),
                            icon: MessageBoxImage.Error, owner: this);
                    }
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
                SetBusy(false);
            }
        }

        private static string StageText(ToolInstallStage stage, string displayName, Func<string, string> T) =>
            string.Format(stage switch
            {
                ToolInstallStage.Downloading => T("StartupWindow.Status.Downloading"),
                ToolInstallStage.Extracting => T("StartupWindow.Status.Extracting"),
                ToolInstallStage.Replacing => T("StartupWindow.Status.Replacing"),
                _ => T("StartupWindow.Status.Verifying"),
            }, displayName);

        /// <summary>
        /// Lädt ein Modell über <see cref="WhisperModelStore.DownloadAsync"/> — eine
        /// <c>.part</c>-Datei, Prüfsumme (wo bekannt) und Größenabgleich, Umbenennen erst nach
        /// bestandener Prüfung. Ein Abbruch oder Netzwerkfehler kann damit keine scheinbar
        /// fertige Datei mehr hinterlassen (bisher: <c>WhisperUpdateService.DownloadModelAsync</c>
        /// schrieb ohne diese Absicherung direkt in die Zieldatei).
        /// </summary>
        private async void btnDownloadModel_Click(object sender, RoutedEventArgs e)
        {
            if (_busy || sender is not System.Windows.Controls.Button btn) return;
            string? modelId = btn.Tag?.ToString();
            if (string.IsNullOrEmpty(modelId)) return;

            var model = WhisperModelCatalog.All.FirstOrDefault(m => m.Id == modelId);
            if (model == null) return;

            var T = UITextDictionary.Get;
            SetBusy(true, string.Format(T("WhisperModels.Downloading"), model.GetDisplayName(UITextDictionary.CurrentLanguage)));

            _cts = new CancellationTokenSource();

            try
            {
                var progress = new Progress<double>(v => Dispatcher.Invoke(() =>
                {
                    pbProgress.Value = v * 100;
                    txtProgressPercent.Text = $"{v * 100:F0} %";
                }));

                await WhisperModelStore.DownloadAsync(model, WhisperService.ModelsDirectory, progress, _cts.Token);

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

            var model = WhisperModelCatalog.All.FirstOrDefault(m => m.Id == modelId);
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
                WhisperModelStore.Delete(model, WhisperService.ModelsDirectory);
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
                    btnCancelProgress.IsEnabled = true;
                }
            });
            if (!busy) RefreshModelList();
        }

        /// <summary>Bricht den laufenden Vorgang ab (whisper.cpp-Installation oder
        /// Modell-Download, je nachdem, welcher gerade läuft) — bisher ließ sich das nur über
        /// das Schließen des ganzen Fensters erreichen. Der Knopf wird sofort deaktiviert, damit
        /// ein zweiter Klick während des Abbruchs keinen zweiten Versuch auslöst.</summary>
        private void btnCancelProgress_Click(object sender, RoutedEventArgs e)
        {
            btnCancelProgress.IsEnabled = false;
            _cts?.Cancel();
        }

        private void SetStatus(string text) => Dispatcher.Invoke(() => txtProgressLabel.Text = text);
    }

    // ── ViewModel für die Modellliste ──────────────────────────────────────────────
    // Kein INotifyPropertyChanged: RefreshModelList() baut icModels.ItemsSource bei jeder
    // Änderung komplett neu auf, es gibt keine Live-Bindung auf einzelne Instanzen.
    internal class ModelViewModel
    {
        public string ModelId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Description { get; set; } = "";
        public string SizeHint { get; set; } = "";
        public WhisperModelState State { get; set; }
        public string StatusText { get; set; } = "";
        public string StatusColor { get; set; } = "#94A3B8";
        public bool CanDownload { get; set; }
        public bool CanDelete { get; set; }
        public string DownloadButtonText { get; set; } = "Download";
        public string DeleteButtonText { get; set; } = "Delete";
        public Visibility DownloadVisibility { get; set; }
        public Visibility DeleteVisibility { get; set; }
    }
}
