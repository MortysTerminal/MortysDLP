using MortysDLP.Helpers;
using MortysDLP.Models;
using MortysDLP.Services;
using MortysDLP.Services.Tools;
using MortysDLP.UITexte;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace MortysDLP.Views
{
    /// <summary>
    /// Zeigt alle vier verwalteten Werkzeuge (<see cref="ToolCatalog.CreateAll"/>) mit Zustand,
    /// Version, Speicherort und Größe, samt Aktualisieren/Reparieren/Deinstallieren/Speicherort
    /// öffnen — sowie einen schreibgeschützten Überblick der Whisper-Modelle, deren eigentliche
    /// Verwaltung weiterhin in <see cref="WhisperModelsWindow"/> läuft (nicht verdoppelt).
    ///
    /// <para>Bewusst schlicht gehalten: Welle 6 vereinheitlicht die Oberfläche über alle Tabs:
    /// Diese Seite folgt den bestehenden Mustern der anderen Seiten, mehr nicht.</para>
    /// </summary>
    public partial class ToolsPage : Page, ICancellableWork
    {
        bool ICancellableWork.IsBusy => _busy;
        string ICancellableWork.BusyLabel => UITextDictionary.Get("ActiveWork.Label.Tools");
        void ICancellableWork.RequestCancel() => _cts?.Cancel();

        private readonly IReadOnlyList<IManagedTool> _tools = ToolCatalog.CreateAll();
        private readonly ToolCatalog _catalog = new();
        private readonly List<ToolRowItem> _rows = [];
        private readonly Dictionary<string, ToolRowItem> _rowByToolId = [];
        private readonly Dictionary<string, ToolRowActions> _lastActions = [];

        private bool _initialized;
        private bool _busy;
        private CancellationTokenSource? _cts;

        public ToolsPage()
        {
            InitializeComponent();
            Loaded += ToolsPage_Loaded;
        }

        private void ToolsPage_Loaded(object sender, RoutedEventArgs e)
        {
            SetUITexts();

            if (!_initialized)
            {
                _initialized = true;
                BuildRows();
            }

            _ = RefreshAllAsync(force: false);
        }

        public void SetUITexts()
        {
            var T = UITextDictionary.Get;

            txtSectionInfo.Text = T("ToolsPage.Section.Info");
            txtInfoText.Text = T("ToolsPage.Info.Text");
            txtCheckAllHint.Text = T("ToolsPage.CheckAll.Hint");
            btnCheckAll.Content = T("ToolsPage.Button.CheckAll");
            txtSectionTools.Text = T("ToolsPage.Section.Tools");
            txtSectionModels.Text = T("ToolsPage.Section.Models");
            btnManageModels.Content = T("ToolsPage.Button.ManageModels");

            foreach (var row in _rows)
                ApplyButtonTexts(row);

            RefreshModelsDisplay();
        }

        // ── Zeilen aufbauen ──────────────────────────────────────────────────────────

        private void BuildRows()
        {
            foreach (var tool in _tools)
            {
                var row = new ToolRowItem(tool.Id)
                {
                    DisplayName = tool.DisplayName,
                    RequiredForOperation = tool.RequiredForOperation,
                };
                ApplyButtonTexts(row);

                _rows.Add(row);
                _rowByToolId[tool.Id] = row;
            }

            icTools.ItemsSource = _rows;
        }

        private static void ApplyButtonTexts(ToolRowItem row)
        {
            var T = UITextDictionary.Get;

            row.RepairButtonText = row.State == ToolRowState.Missing
                ? T("ToolsPage.Button.Install")
                : T("ToolsPage.Button.Repair");
            row.UninstallButtonText = T("ToolsPage.Button.Uninstall");
            row.OpenFolderButtonText = T("ToolsPage.Button.OpenFolder");
        }

        // ── Prüfen ───────────────────────────────────────────────────────────────────

        private async Task RefreshAllAsync(bool force)
        {
            if (_busy)
                return;

            SetPageBusy(true);
            _cts = new CancellationTokenSource();

            try
            {
                foreach (var tool in _tools)
                {
                    _cts.Token.ThrowIfCancellationRequested();
                    await RefreshOneAsync(tool, force, _cts.Token);
                }

                RefreshModelsDisplay();
            }
            catch (OperationCanceledException)
            {
                // Vom Nutzer abgebrochen oder von der Update-Vorprüfung der Anwendung ausgelöst
                // (ICancellableWork.RequestCancel) - kein Fehler.
            }
            finally
            {
                SetPageBusy(false);
            }
        }

        private async Task RefreshOneAsync(IManagedTool tool, bool force, CancellationToken ct)
        {
            var row = _rowByToolId[tool.Id];
            var outcome = await _catalog.CheckAsync(tool, force, ct);
            ApplyOutcome(row, tool, outcome);
        }

        private void ApplyOutcome(ToolRowItem row, IManagedTool tool, ToolCheckOutcome outcome)
        {
            var T = UITextDictionary.Get;
            var state = ToolRowActions.StateFor(outcome);
            var actions = ToolRowActions.For(state);

            row.State = state;
            (row.StateText, row.StateColor) = state switch
            {
                ToolRowState.Missing => (T("ToolsPage.State.Missing"), "#94A3B8"),
                ToolRowState.Broken => (T("ToolsPage.State.Broken"), "#EF4444"),
                ToolRowState.Ok => (T("ToolsPage.State.Ok"), "#22C55E"),
                ToolRowState.UpdateAvailable => (T("ToolsPage.State.UpdateAvailable"), "#F59E0B"),
                _ => ("", "#94A3B8"),
            };

            row.VersionText = outcome.LocalVersion.HasValue ? outcome.LocalVersion.Raw : "–";
            row.LocationText = Path.GetDirectoryName(tool.TargetPaths[0]) ?? "";
            row.SizeText = WhisperModelCatalog.FormatSize(ToolCatalog.GetInstalledSize(tool));
            row.RepairButtonText = state == ToolRowState.Missing
                ? T("ToolsPage.Button.Install")
                : T("ToolsPage.Button.Repair");
            row.UpdateButtonText = string.Format(CultureInfo.CurrentCulture, T("ToolsPage.Button.UpdateTo"), outcome.RemoteVersion);

            _lastActions[row.ToolId] = actions;
            ApplyRowInteractivity(row);
        }

        /// <summary>Wendet Aktionslogik (<see cref="ToolRowActions"/>) und Seiten-Sperre
        /// zusammen auf eine Zeile an — an einer Stelle, damit „während einer Aktion sind alle
        /// Knöpfe gesperrt" nicht doppelt gepflegt werden muss.</summary>
        private void ApplyRowInteractivity(ToolRowItem row)
        {
            if (!_lastActions.TryGetValue(row.ToolId, out var actions))
                return;

            bool enabled = !_busy && !row.IsBusy;
            row.CanRepair = actions.CanRepair && enabled;
            row.CanUninstall = actions.CanUninstall && enabled;
            row.CanOpenFolder = actions.CanOpenFolder && enabled;
            row.UpdateVisibility = actions.CanUpdate && enabled ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SetPageBusy(bool busy)
        {
            _busy = busy;
            btnCheckAll.IsEnabled = !busy;

            var T = UITextDictionary.Get;
            btnCheckAll.Content = busy ? T("ToolsPage.Status.Checking") : T("ToolsPage.Button.CheckAll");

            foreach (var row in _rows)
                ApplyRowInteractivity(row);
        }

        private async void btnCheckAll_Click(object sender, RoutedEventArgs e) => await RefreshAllAsync(force: true);

        // ── Reparieren / Aktualisieren ───────────────────────────────────────────────

        private async void btnRepair_Click(object sender, RoutedEventArgs e) => await RunInstallActionAsync(sender);

        private async void btnUpdate_Click(object sender, RoutedEventArgs e) => await RunInstallActionAsync(sender);

        /// <summary>
        /// „Reparieren" und „Aktualisieren" laufen über denselben Weg: <see cref="IManagedTool.InstallAsync"/>
        /// entscheidet selbst über Neuinstallation oder Ersetzen (<see cref="ToolInstallStatus"/>).
        /// Der einzige Unterschied zwischen den beiden Knöpfen ist, wann sie überhaupt sichtbar
        /// sind — nicht, was beim Klick passiert.
        /// </summary>
        private async Task RunInstallActionAsync(object sender)
        {
            if (_busy || sender is not Button btn || btn.Tag is not string toolId)
                return;

            var tool = _tools.FirstOrDefault(t => t.Id == toolId);
            if (tool is null || !_rowByToolId.TryGetValue(toolId, out var row))
                return;

            var T = UITextDictionary.Get;
            var owner = Window.GetWindow(this);

            SetPageBusy(true);
            row.IsBusy = true;
            row.ProgressVisible = true;
            row.ProgressFraction = 0;
            row.StatusMessage = T("ToolsPage.Status.Checking");
            ApplyRowInteractivity(row);
            _cts = new CancellationTokenSource();

            try
            {
                var outcome = await _catalog.CheckAsync(tool, force: false, _cts.Token);
                var release = await _catalog.ResolveForInstallAsync(outcome, _cts.Token);

                var progress = new Progress<double>(v => row.ProgressFraction = v);
                var stage = new Progress<ToolInstallStage>(s => row.StatusMessage = StageText(s, tool.DisplayName, T));

                var result = await tool.InstallAsync(release, progress, stage, _cts.Token);

                if (!result.Success)
                {
                    if (result.Status == ToolInstallStatus.RolledBack)
                    {
                        FluentMessageBox.Show(string.Format(CultureInfo.CurrentCulture, T("StartupWindow.ToolUpdate.RolledBack"), tool.DisplayName),
                            icon: MessageBoxImage.Warning, owner: owner);
                    }
                    else if (result.Status != ToolInstallStatus.Canceled)
                    {
                        FluentMessageBox.Show(string.Format(CultureInfo.CurrentCulture, T("StartupWindow.Tool.InstallFailed"), tool.DisplayName),
                            icon: MessageBoxImage.Error, owner: owner);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Abbruch über die Zeile oder die Update-Vorprüfung der Anwendung - kein Fehler.
            }
            catch (Exception ex)
            {
                Log.Warn($"[{tool.Id}] Aktion auf der Werkzeuge-Seite fehlgeschlagen: {ex.Message}", ex);
                FluentMessageBox.Show(string.Format(CultureInfo.CurrentCulture, T("StartupWindow.Tool.InstallFailed"), tool.DisplayName),
                    icon: MessageBoxImage.Error, owner: owner);
            }
            finally
            {
                row.IsBusy = false;
                row.ProgressVisible = false;
                SetPageBusy(false);
                await RefreshOneAsync(tool, force: false, CancellationToken.None);
            }
        }

        private static string StageText(ToolInstallStage stage, string displayName, Func<string, string> T) =>
            string.Format(CultureInfo.CurrentCulture, stage switch
            {
                ToolInstallStage.Downloading => T("StartupWindow.Status.Downloading"),
                ToolInstallStage.Extracting => T("StartupWindow.Status.Extracting"),
                ToolInstallStage.Replacing => T("StartupWindow.Status.Replacing"),
                _ => T("StartupWindow.Status.Verifying"),
            }, displayName);

        private void btnCancelRow_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();

        // ── Deinstallieren ───────────────────────────────────────────────────────────

        private async void btnUninstall_Click(object sender, RoutedEventArgs e)
        {
            if (_busy || sender is not Button btn || btn.Tag is not string toolId)
                return;

            var tool = _tools.FirstOrDefault(t => t.Id == toolId);
            if (tool is null)
                return;

            var T = UITextDictionary.Get;
            var owner = Window.GetWindow(this);

            var result = FluentMessageBox.Show(
                BuildUninstallMessage(tool, T),
                T("ToolsPage.Uninstall.Title"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                owner);

            if (result != MessageBoxResult.Yes)
                return;

            var removal = tool.Uninstall();
            if (!removal.Success)
            {
                FluentMessageBox.Show(
                    string.Format(CultureInfo.CurrentCulture, T("ToolsPage.Uninstall.Failed"), tool.DisplayName),
                    icon: MessageBoxImage.Error, owner: owner);
            }

            await RefreshOneAsync(tool, force: false, CancellationToken.None);
        }

        /// <summary>Nennt bei einem für den Betrieb erforderlichen Werkzeug ausdrücklich, welche
        /// Funktionen danach nicht mehr verfügbar sind — dieselbe Zuordnung wie in
        /// <c>StartupWindow.BuildRequiredMessage</c>, hier für die Rückfrage statt für den
        /// Startablauf.</summary>
        private static string BuildUninstallMessage(IManagedTool tool, Func<string, string> T)
        {
            string question = string.Format(CultureInfo.CurrentCulture, T("ToolsPage.Uninstall.Question"), tool.DisplayName);

            string? consequence = tool.Id switch
            {
                "yt-dlp" => T("ToolsPage.Uninstall.Consequence.YtDlp"),
                "ffmpeg" => T("ToolsPage.Uninstall.Consequence.Ffmpeg"),
                _ => null,
            };

            return consequence is null ? question : question + "\n\n" + consequence;
        }

        // ── Speicherort öffnen ───────────────────────────────────────────────────────

        private void btnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string toolId)
                return;

            var tool = _tools.FirstOrDefault(t => t.Id == toolId);
            string? folder = tool is null ? null : Path.GetDirectoryName(tool.TargetPaths[0]);
            if (string.IsNullOrEmpty(folder))
                return;

            try
            {
                Directory.CreateDirectory(folder);
                Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
            {
                Log.Warn($"Werkzeugordner '{folder}' konnte nicht geöffnet werden: {ex.Message}");
            }
        }

        // ── Whisper-Modelle (schreibgeschützter Überblick) ──────────────────────────────

        private void btnManageModels_Click(object sender, RoutedEventArgs e)
        {
            var win = new WhisperModelsWindow { Owner = Window.GetWindow(this) };
            win.ShowDialog();
            RefreshModelsDisplay();
        }

        private sealed class ModelSummaryRow
        {
            public string Name { get; init; } = "";
            public string StateText { get; init; } = "";
            public string StateColor { get; init; } = "#94A3B8";
        }

        private void RefreshModelsDisplay()
        {
            var T = UITextDictionary.Get;
            string lang = UITextDictionary.CurrentLanguage;
            string modelsDir = WhisperService.ModelsDirectory;

            var rows = new List<ModelSummaryRow>();
            int complete = 0;

            foreach (var model in WhisperModelCatalog.All)
            {
                var state = WhisperModelStore.GetState(model, modelsDir);
                if (state == WhisperModelState.Complete)
                    complete++;

                var (text, color) = state switch
                {
                    WhisperModelState.Complete => (T("ToolsPage.State.Ok"), "#22C55E"),
                    WhisperModelState.Incomplete => (T("WhisperModels.Status.Incomplete"), "#F59E0B"),
                    _ => (T("ToolsPage.State.Missing"), "#94A3B8"),
                };

                rows.Add(new ModelSummaryRow
                {
                    Name = model.GetDisplayName(lang),
                    StateText = text,
                    StateColor = color,
                });
            }

            icModels.ItemsSource = rows;
            txtModelsSummary.Text = string.Format(
                CultureInfo.CurrentCulture, T("ToolsPage.Models.Summary"), complete, WhisperModelCatalog.All.Count);

            RefreshTotalSize();
        }

        /// <summary>Summe der tatsächlichen Größe aller Werkzeuge und Modelle — ohne diese
        /// Zeile merkt niemand, dass hier über die Zeit mehrere Gigabyte zusammenkommen
        /// können.</summary>
        private void RefreshTotalSize()
        {
            var T = UITextDictionary.Get;
            long toolsBytes = _tools.Sum(ToolCatalog.GetInstalledSize);
            long modelsBytes = WhisperModelCatalog.GetInstalledSize(WhisperService.ModelsDirectory);

            txtTotalSize.Text = string.Format(CultureInfo.CurrentCulture,
                T("ToolsPage.TotalSize"), WhisperModelCatalog.FormatSize(toolsBytes + modelsBytes));
        }
    }
}
