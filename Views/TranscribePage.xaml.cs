using Microsoft.Win32;
using MortysDLP.Models;
using MortysDLP.Services;
using MortysDLP.UITexte;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace MortysDLP.Views
{
    public partial class TranscribePage : Page
    {
        private CancellationTokenSource? _cts;
        private bool _initialized = false;

        // Sprach-Einträge: (Code, Anzeigename)
        private static readonly (string Code, string NameDe, string NameEn)[] Languages = new[]
        {
            ("auto",  "Automatisch erkennen",  "Auto-detect"),
            ("de",    "Deutsch",               "German"),
            ("en",    "Englisch",              "English"),
            ("fr",    "Französisch",           "French"),
            ("es",    "Spanisch",              "Spanish"),
            ("it",    "Italienisch",           "Italian"),
            ("pt",    "Portugiesisch",         "Portuguese"),
            ("nl",    "Niederländisch",        "Dutch"),
            ("pl",    "Polnisch",              "Polish"),
            ("ru",    "Russisch",              "Russian"),
            ("zh",    "Chinesisch",            "Chinese"),
            ("ja",    "Japanisch",             "Japanese"),
            ("ko",    "Koreanisch",            "Korean"),
            ("ar",    "Arabisch",              "Arabic"),
            ("tr",    "Türkisch",              "Turkish"),
            ("sv",    "Schwedisch",            "Swedish"),
            ("no",    "Norwegisch",            "Norwegian"),
            ("da",    "Dänisch",               "Danish"),
            ("fi",    "Finnisch",              "Finnish"),
        };

        public TranscribePage()
        {
            InitializeComponent();
            Loaded += TranscribePage_Loaded;
        }

        private void TranscribePage_Loaded(object sender, RoutedEventArgs e)
        {
            if (_initialized)
            {
                SetUITexts();
                return;
            }
            _initialized = true;
            SetUITexts();
            PopulateLanguages();
            RefreshWhisperStatus();
            RefreshModelComboBox();
        }

        internal void RefreshAll()
        {
            SetUITexts();
            RefreshWhisperStatus();
            RefreshModelComboBox();
        }

        public void SetUITexts()
        {
            var T = UITextDictionary.Get;

            txtSectionInfo.Text = T("TranscribePage.Section.Info");
            txtInfoText.Text = T("TranscribePage.Info.Text");
            txtSectionWhisper.Text = T("TranscribePage.Section.Whisper");
            btnInstallWhisper.Content = T("TranscribePage.Button.InstallWhisper");
            btnManageModels.Content = T("TranscribePage.Button.ManageModels");
            txtSectionInput.Text = T("TranscribePage.Section.Input");
            lblInputFile.Content = T("TranscribePage.Label.InputFile");
            txtBrowseInput.Text = T("TranscribePage.Button.BrowseInput");
            tooltipInputFile.Content = T("TranscribePage.Tooltip.InputFile");
            txtSectionSettings.Text = T("TranscribePage.Section.Settings");
            lblModel.Content = T("TranscribePage.Label.Model");
            tooltipModel2.Content = T("TranscribePage.Tooltip.Model");
            lblLanguage.Content = T("TranscribePage.Label.Language");
            tooltipLang.Content = T("TranscribePage.Tooltip.Language");
            lblOutputFormat.Content = T("TranscribePage.Label.OutputFormat");
            txtOutputTxt.Text = T("TranscribePage.CheckBox.OutputTxt");
            txtOutputSrt.Text = T("TranscribePage.CheckBox.OutputSrt");
            txtOutputVtt.Text = T("TranscribePage.CheckBox.OutputVtt");
            tooltipFormat.Content = T("TranscribePage.Tooltip.OutputFormat");
            txtSectionOutput.Text = T("TranscribePage.Section.Output");
            lblOutputDir.Content = T("TranscribePage.Label.OutputDir");
            txtBrowseOutput.Text = T("TranscribePage.Button.BrowseOutput");
            txtOpenOutput.Text = T("TranscribePage.Button.OpenOutput");
            btnStart.Content = T("TranscribePage.Button.Start");
            btnCancel.Content = T("TranscribePage.Button.Cancel");
            expLog.Header = T("DownloadPage.Section.Debug");

            ApplyDebugMode();
            RefreshWhisperStatus();
            PopulateLanguages();
            RefreshModelComboBox();
        }

        public void ApplyDebugMode()
        {
            bool fullyReady = WhisperService.IsWhisperInstalled() && WhisperService.GetInstalledModels().Any();
            dockLog.Visibility = (fullyReady && Properties.Settings.Default.DebugMode)
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RefreshWhisperStatus()
        {
            var T = UITextDictionary.Get;
            bool whisperInstalled = WhisperService.IsWhisperInstalled();
            bool hasModels = WhisperService.GetInstalledModels().Any();
            bool fullyReady = whisperInstalled && hasModels;

            // Alle Arbeits-Sektionen nur zeigen wenn Whisper vollständig eingerichtet ist
            var workVisibility = fullyReady ? Visibility.Visible : Visibility.Collapsed;
            borderInput.Visibility    = workVisibility;
            borderSettings.Visibility = workVisibility;
            borderOutput.Visibility   = workVisibility;
            borderActions.Visibility  = workVisibility;

            // Debug-Log: nur wenn bereit UND DebugMode aktiviert
            dockLog.Visibility = (fullyReady && Properties.Settings.Default.DebugMode)
                ? Visibility.Visible : Visibility.Collapsed;

            // Setup-Panel: zeige wenn nicht vollständig eingerichtet
            pnlSetup.Visibility = fullyReady ? Visibility.Collapsed : Visibility.Visible;
            txtWhisperStatus.Visibility = fullyReady ? Visibility.Visible : Visibility.Collapsed;

            if (fullyReady)
            {
                txtWhisperStatus.Text = T("TranscribePage.Whisper.Installed");
            }
            else
            {
                txtSetupHint.Text = T("TranscribePage.Setup.Hint");
                txtSetupStep1.Text = whisperInstalled
                    ? T("TranscribePage.Setup.Step1.Done")
                    : T("TranscribePage.Setup.Step1");
                txtSetupStep1Status.Text = whisperInstalled ? "" : "←";
                txtSetupStep2.Text = hasModels
                    ? T("TranscribePage.Setup.Step2.Done")
                    : T("TranscribePage.Setup.Step2");
                txtSetupStep2Status.Text = (whisperInstalled && !hasModels) ? "←" : "";
            }

            // Setup-Sektion immer korrekt benennen
            txtSectionWhisper.Text = fullyReady
                ? T("TranscribePage.Section.Whisper")
                : T("TranscribePage.Setup.Title");

            // Start-Button: Tooltip erklären wenn deaktiviert
            UpdateStartButtonState();
        }

        private void UpdateStartButtonState()
        {
            var T = UITextDictionary.Get;
            bool whisperOk = WhisperService.IsWhisperInstalled();
            bool hasModels = WhisperService.GetInstalledModels().Any();

            bool canStart = whisperOk && hasModels;
            btnStart.IsEnabled = canStart;

            if (!whisperOk)
                tooltipBtnStart.Content = T("TranscribePage.Error.WhisperMissing");
            else if (!hasModels)
                tooltipBtnStart.Content = T("TranscribePage.Error.NoModel");
            else
                tooltipBtnStart.Content = null;
        }

        private void RefreshModelComboBox()
        {
            var T = UITextDictionary.Get;
            var lang = UITextDictionary.CurrentLanguage;
            var installed = WhisperService.GetInstalledModels().ToList();

            combModel.Items.Clear();

            if (!installed.Any())
            {
                combModel.Items.Add(new ComboBoxItem
                {
                    Content = T("TranscribePage.NoModels.Hint"),
                    IsEnabled = false,
                    Tag = ""
                });
                combModel.SelectedIndex = 0;
                UpdateStartButtonState();
                return;
            }

            foreach (var m in installed)
            {
                combModel.Items.Add(new ComboBoxItem
                {
                    Content = m.GetDisplayName(lang),
                    Tag = m.Id,
                    ToolTip = m.GetDescription(lang)
                });
            }

            combModel.SelectedIndex = 0;
            UpdateStartButtonState();
        }

        private void PopulateLanguages()
        {
            string lang = UITextDictionary.CurrentLanguage;
            string? selectedCode = (combLanguage.SelectedItem as ComboBoxItem)?.Tag?.ToString();

            combLanguage.Items.Clear();
            foreach (var (code, nameDe, nameEn) in Languages)
            {
                combLanguage.Items.Add(new ComboBoxItem
                {
                    Content = lang == "de" ? nameDe : nameEn,
                    Tag = code
                });
            }

            // Vorherige Auswahl wiederherstellen oder "auto" wählen
            int idx = 0;
            if (!string.IsNullOrEmpty(selectedCode))
            {
                for (int i = 0; i < combLanguage.Items.Count; i++)
                {
                    if ((combLanguage.Items[i] as ComboBoxItem)?.Tag?.ToString() == selectedCode)
                    {
                        idx = i;
                        break;
                    }
                }
            }
            combLanguage.SelectedIndex = idx;
        }

        // ── Browse ───────────────────────────────────────────────────────────────────

        private void btnBrowseInput_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = UITextDictionary.Get("TranscribePage.Label.InputFile"),
                Filter = "Mediendateien|*.mp4;*.mkv;*.mov;*.avi;*.mp3;*.wav;*.flac;*.m4a;*.ogg;*.opus;*.aac;*.wma;*.webm|Alle Dateien|*.*"
            };
            if (dlg.ShowDialog() == true)
            {
                tbInputFile.Text = dlg.FileName;
                // Ausgabeordner automatisch auf denselben Ordner setzen
                if (string.IsNullOrWhiteSpace(tbOutputDir.Text))
                    tbOutputDir.Text = Path.GetDirectoryName(dlg.FileName) ?? "";
            }
        }

        private void btnBrowseOutput_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = UITextDictionary.Get("TranscribePage.Label.OutputDir"),
                SelectedPath = tbOutputDir.Text,
                UseDescriptionForTitle = true
            };
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                tbOutputDir.Text = dlg.SelectedPath;
        }

        private void btnOpenOutput_Click(object sender, RoutedEventArgs e)
        {
            string path = tbOutputDir.Text;
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }

        private void btnInstallWhisper_Click(object sender, RoutedEventArgs e) => OpenModelsWindow();
        private void btnManageModels_Click(object sender, RoutedEventArgs e) => OpenModelsWindow();

        private void OpenModelsWindow()
        {
            var win = new WhisperModelsWindow { Owner = Window.GetWindow(this) };
            win.ShowDialog();
            RefreshWhisperStatus();
            RefreshModelComboBox();
        }

        // ── Transkription ────────────────────────────────────────────────────────────

        private async void btnStart_Click(object sender, RoutedEventArgs e)
        {
            var T = UITextDictionary.Get;

            // Validierung
            if (string.IsNullOrWhiteSpace(tbInputFile.Text) || !File.Exists(tbInputFile.Text))
            {
                FluentMessageBox.Show(T("TranscribePage.Error.NoFile"), icon: MessageBoxImage.Warning,
                    owner: Window.GetWindow(this));
                return;
            }

            string? modelId = (combModel.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            if (string.IsNullOrEmpty(modelId))
            {
                FluentMessageBox.Show(T("TranscribePage.Error.NoModel"), icon: MessageBoxImage.Warning,
                    owner: Window.GetWindow(this));
                return;
            }

            if (!WhisperService.IsWhisperInstalled())
            {
                FluentMessageBox.Show(T("TranscribePage.Error.WhisperMissing"), icon: MessageBoxImage.Error,
                    owner: Window.GetWindow(this));
                return;
            }

            if (cbOutputTxt.IsChecked != true && cbOutputSrt.IsChecked != true && cbOutputVtt.IsChecked != true)
            {
                FluentMessageBox.Show(T("TranscribePage.Error.NoFormat"), icon: MessageBoxImage.Warning,
                    owner: Window.GetWindow(this));
                return;
            }

            string ffmpegPath = Properties.Settings.Default.FfmpegPath;
            if (!File.Exists(ffmpegPath))
            {
                FluentMessageBox.Show(T("TranscribePage.Error.FfmpegMissing"), icon: MessageBoxImage.Error,
                    owner: Window.GetWindow(this));
                return;
            }

            string outputDir = tbOutputDir.Text.Trim();
            if (string.IsNullOrWhiteSpace(outputDir))
                outputDir = Path.GetDirectoryName(tbInputFile.Text) ?? "";

            tbLog.Clear();
            SetUiRunning(true);
            txtStatus.Text = T("TranscribePage.Status.ExtractingAudio");

            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            try
            {
                string inputFile = tbInputFile.Text;
                string modelPath = WhisperService.GetModelPath(modelId);
                string language = (combLanguage.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "auto";
                bool outTxt = cbOutputTxt.IsChecked == true;
                bool outSrt = cbOutputSrt.IsChecked == true;
                bool outVtt = cbOutputVtt.IsChecked == true;
                string prefix = Path.GetFileNameWithoutExtension(inputFile);

                var progress = new Progress<string>(line => Dispatcher.Invoke(() =>
                {
                    AppendLog(line);
                }));

                var numericProgress = new Progress<double>(pct => Dispatcher.Invoke(() =>
                {
                    pbProgress.IsIndeterminate = false;
                    pbProgress.Value = pct;
                    txtProgressPercent.Text = $"{pct:F0} %";
                }));

                Dispatcher.Invoke(() =>
                {
                    txtStatus.Text = T("TranscribePage.Status.Transcribing");
                    pbProgress.IsIndeterminate = true;
                    txtProgressPercent.Text = "";
                });

                await WhisperService.RunTranscriptionAsync(
                    WhisperService.WhisperExePath,
                    ffmpegPath,
                    inputFile, modelPath, language,
                    outTxt, outSrt, outVtt,
                    outputDir, prefix,
                    progress, token, numericProgress);

                // Erfolg: Inline anzeigen, keine MessageBox
                Dispatcher.Invoke(() => ShowResult(success: true,
                    title: T("TranscribePage.Status.Success"),
                    file: Path.GetFileName(inputFile),
                    outputDir: outputDir));
            }
            catch (OperationCanceledException)
            {
                Dispatcher.Invoke(() => ShowResult(success: null,
                    title: T("TranscribePage.Status.Canceled"),
                    file: Path.GetFileName(tbInputFile.Text)));
                AppendLog("[ABGEBROCHEN]");
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => ShowResult(success: false,
                    title: T("TranscribePage.Status.Error"),
                    file: ex.Message));
                AppendLog($"[FEHLER] {ex.Message}");
            }
            finally
            {
                SetUiRunning(false);
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
        }

        private void btnOpenResult_Click(object sender, RoutedEventArgs e)
        {
            string path = tbOutputDir.Text;
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }

        private void ShowResult(bool? success, string title, string? file, string? outputDir = null)
        {
            // success=true → grün, success=false → rot, success=null → grau (abgebrochen)
            pnlRunning.Visibility = Visibility.Collapsed;
            pnlResult.Visibility = Visibility.Visible;

            txtResultTitle.Text = title;
            txtResultFile.Text = file ?? "";

            if (success == true)
            {
                txtResultIcon.Text = "\uE73E"; // Segoe MDL2: Checkmark
                txtResultIcon.Foreground = System.Windows.Media.Brushes.LimeGreen;
                if (!string.IsNullOrEmpty(outputDir))
                {
                    btnOpenResult.Visibility = Visibility.Visible;
                    _lastOutputDir = outputDir;
                }
            }
            else if (success == false)
            {
                txtResultIcon.Text = "\uE783"; // Segoe MDL2: Error badge
                txtResultIcon.Foreground = System.Windows.Media.Brushes.OrangeRed;
                btnOpenResult.Visibility = Visibility.Collapsed;
            }
            else
            {
                txtResultIcon.Text = "\uE711"; // Segoe MDL2: Cancel
                txtResultIcon.Foreground = System.Windows.Media.Brushes.Gray;
                btnOpenResult.Visibility = Visibility.Collapsed;
            }
        }

        private string? _lastOutputDir;

        private void SetUiRunning(bool running)
        {
            Dispatcher.Invoke(() =>
            {
                btnStart.IsEnabled = !running;
                btnCancel.IsEnabled = running;
                btnBrowseInput.IsEnabled = !running;
                btnBrowseOutput.IsEnabled = !running;
                combModel.IsEnabled = !running;
                combLanguage.IsEnabled = !running;
                cbOutputTxt.IsEnabled = !running;
                cbOutputSrt.IsEnabled = !running;
                cbOutputVtt.IsEnabled = !running;

                if (running)
                {
                    // Ergebnis-Panel zurücksetzen, Fortschritt einblenden
                    pnlResult.Visibility = Visibility.Collapsed;
                    btnOpenResult.Visibility = Visibility.Collapsed;
                    pbProgress.Value = 0;
                    pbProgress.IsIndeterminate = true;
                    txtProgressPercent.Text = "";
                    pnlRunning.Visibility = Visibility.Visible;
                }
                else
                {
                    pnlRunning.Visibility = Visibility.Collapsed;
                    // Debug-Log nur öffnen wenn DebugMode aktiv
                    if (Properties.Settings.Default.DebugMode)
                        expLog.IsExpanded = true;
                }
            });
        }

        private void AppendLog(string text) => Dispatcher.Invoke(() =>
        {
            tbLog.AppendText(text + Environment.NewLine);
            tbLog.ScrollToEnd();
        });

        private void tbLog_TextChanged(object sender, TextChangedEventArgs e)
        {
            tbLog.ScrollToEnd();
        }
    }
}
