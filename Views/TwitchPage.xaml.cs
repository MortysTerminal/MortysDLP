using MortysDLP.Services;
using MortysDLP.UITexte;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;

namespace MortysDLP.Views
{
    public partial class TwitchPage : Page
    {
        private CancellationTokenSource? _cts;
        private bool _initialized = false;
        private readonly TwitchDownloaderService _service = new();
        private Process? _currentYtDlpProcess;
        private double   _activeRateLimitMBps = 0;
        private volatile bool _bandwidthKillPending = false;

        public TwitchPage()
        {
            InitializeComponent();
            Loaded += TwitchPage_Loaded;
        }

        private void TwitchPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (_initialized)
            {
                SetUITexts();
                return;
            }
            _initialized = true;
            SetUITexts();

            // Gespeicherten Twitch-Ausgabepfad laden; Fallback: globaler DownloadPath
            string savedPath = Properties.Settings.Default.TwitchDownloaderOutputPath;
            if (!string.IsNullOrEmpty(savedPath))
                tbOutputPath.Text = savedPath;
            else
            {
                string globalPath = Properties.Settings.Default.DownloadPath;
                if (!string.IsNullOrEmpty(globalPath))
                    tbOutputPath.Text = globalPath;
            }

            RefreshToolStatus();
            ValidateStartButton();

            // Hintergrund-Update-Check (nur wenn installiert, blockiert das UI nicht)
            if (TwitchDownloaderService.IsInstalled())
                _ = CheckForUpdateSilentlyAsync();
        }

        public void SetUITexts()
        {
            var T = UITextDictionary.Get;

            txtSectionInfo.Text        = T("TwitchPage.Section.Info");
            txtInfoText.Text           = T("TwitchPage.Info.Text");
            txtSectionTool.Text        = T("TwitchPage.Section.Tool");
            btnInstall.Content         = T("TwitchPage.Button.Install");
            btnUpdateAction.Content    = T("TwitchPage.Button.CheckingUpdate");
            btnUpdateAction.IsEnabled  = false;
            btnUninstall.Content       = T("TwitchPage.Button.Uninstall");
            txtSectionDownload.Text    = T("TwitchPage.Section.Download");
            lblURL.Content             = T("TwitchPage.Label.URL");
            tooltipURL.Content         = T("TwitchPage.Tooltip.URL");
            lblOutputPath.Content      = T("TwitchPage.Label.OutputPath");
            btnBrowseOutput.Content    = T("TwitchPage.Button.BrowseOutput");
            btnOpenOutput.Content      = T("TwitchPage.Button.OpenOutput");
            cbDownloadChat.Content      = "";
            txtDownloadChat.Text        = T("TwitchPage.CheckBox.DownloadChat");
            tooltipDownloadChat.Content = T("TwitchPage.Tooltip.DownloadChat");
            cbDownloadVideo.Content      = "";
            txtDownloadVideo.Text        = T("TwitchPage.CheckBox.DownloadVideo");
            tooltipDownloadVideo.Content = T("TwitchPage.Tooltip.DownloadVideo");
            // RadioButton-Content NICHT überschreiben (enthält StackPanel-Template);
            // nur die inneren TextBlöcke und Tooltips setzen
            txtChatJson.Text            = T("TwitchPage.RadioButton.ChatJson");
            tooltipChatJson.Content     = T("TwitchPage.Tooltip.ChatJson");
            txtChatRender.Text          = T("TwitchPage.RadioButton.ChatRender");
            tooltipChatRender.Content   = T("TwitchPage.Tooltip.ChatRender");
            txtGpuHint.Text             = T("TwitchPage.Hint.RenderGpu");
            lblRenderQuality.Content    = T("TwitchPage.Label.RenderQuality");
            cbiQualityStandard.Content  = T("TwitchPage.RenderQuality.Standard");
            cbiQualityHigh.Content      = T("TwitchPage.RenderQuality.High");
            cbiQualityUltra.Content     = T("TwitchPage.RenderQuality.Ultra");
            tooltipRenderQuality.Content = T("TwitchPage.Tooltip.RenderQuality");
            btnUseGlobalPath.Content    = T("TwitchPage.Button.UseGlobalPath");
            btnStart.Content           = T("TwitchPage.Button.Start");
            btnCancel.Content          = T("TwitchPage.Button.Cancel");
            expDebug.Header            = T("TwitchPage.Section.Debug");

            // Bandwidth-Hinweis
            double bw = Properties.Settings.Default.DownloadBandwidthMBps;
            if (bw > 0)
            {
                txtBandwidthHint.Text = string.Format(T("Global.BandwidthHint"), bw.ToString(System.Globalization.CultureInfo.InvariantCulture));
                borderBandwidthHint.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                borderBandwidthHint.Visibility = System.Windows.Visibility.Collapsed;
            }

            RefreshToolStatus();
            ApplyDebugMode();
        }

        public void ApplyDebugMode()
        {
            expDebug.Visibility = Properties.Settings.Default.DebugMode
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RefreshToolStatus()
        {
            var T = UITextDictionary.Get;
            bool installed = TwitchDownloaderService.IsInstalled();
            txtToolStatus.Text = installed
                ? T("TwitchPage.Tool.Installed")
                : T("TwitchPage.Tool.NotInstalled");

            // Dateigröße anzeigen
            double? sizeMb = TwitchDownloaderService.GetFileSizeMB();
            if (sizeMb.HasValue)
            {
                txtFileSize.Text       = string.Format(T("TwitchPage.Tool.FileSize"), sizeMb.Value);
                txtFileSize.Visibility = Visibility.Visible;
            }
            else
            {
                txtFileSize.Visibility = Visibility.Collapsed;
            }

            // Installieren-Button: nur sichtbar wenn NICHT installiert
            btnInstall.Visibility      = installed ? Visibility.Collapsed : Visibility.Visible;

            // Update/Check + Deinstallieren: nur sichtbar wenn installiert
            btnUpdateAction.Visibility = installed ? Visibility.Visible   : Visibility.Collapsed;
            btnUninstall.Visibility    = installed ? Visibility.Visible   : Visibility.Collapsed;

            borderDownload.IsEnabled   = installed;
        }

        // ── Ausgabeordner ──────────────────────────────────────────────────────────

        private void btnUseGlobalPath_Click(object sender, RoutedEventArgs e)
        {
            string globalPath = Properties.Settings.Default.DownloadPath;
            if (!string.IsNullOrEmpty(globalPath) && Directory.Exists(globalPath))
            {
                tbOutputPath.Text = globalPath;
                Properties.Settings.Default.TwitchDownloaderOutputPath = globalPath;
                Properties.Settings.Default.Save();
                ValidateStartButton();
            }
        }

        private void btnBrowseOutput_Click(object sender, RoutedEventArgs e)
        {
            using var dlg = new FolderBrowserDialog();
            if (!string.IsNullOrEmpty(tbOutputPath.Text) && Directory.Exists(tbOutputPath.Text))
                dlg.InitialDirectory = tbOutputPath.Text;

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                tbOutputPath.Text = dlg.SelectedPath;
                Properties.Settings.Default.TwitchDownloaderOutputPath = dlg.SelectedPath;
                Properties.Settings.Default.Save();
                ValidateStartButton();
            }
        }

        private void btnOpenOutput_Click(object sender, RoutedEventArgs e)
        {
            string path = tbOutputPath.Text;
            if (Directory.Exists(path))
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }

        // ── URL-Eingabe ────────────────────────────────────────────────────────────

        private void tbURL_TextChanged(object sender, TextChangedEventArgs e)
        {
            ValidateStartButton();
        }

        private void cbDownloadVideo_Changed(object sender, RoutedEventArgs e)
        {
            // Mindestens Video oder Chat muss aktiv sein
            if (cbDownloadVideo.IsChecked != true && cbDownloadChat?.IsChecked != true)
                cbDownloadChat.IsChecked = true;
            ValidateStartButton();
        }

        private void cbDownloadChat_Changed(object sender, RoutedEventArgs e)
        {
            bool active = cbDownloadChat.IsChecked == true;
            if (pnlChatOptions != null)
                pnlChatOptions.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
            if (!active && rbChatJson != null)
            {
                rbChatJson.IsChecked = true;
                if (pnlGpuHint != null)
                    pnlGpuHint.Visibility = Visibility.Collapsed;
            }
            // Mindestens Video oder Chat muss aktiv sein
            if (cbDownloadChat.IsChecked != true && cbDownloadVideo?.IsChecked != true)
                cbDownloadVideo.IsChecked = true;
            ValidateStartButton();
        }

        private void rbChatRender_Changed(object sender, RoutedEventArgs e)
        {
            bool render = rbChatRender.IsChecked == true;
            if (pnlGpuHint != null)
                pnlGpuHint.Visibility = render ? Visibility.Visible : Visibility.Collapsed;
            if (pnlRenderQuality != null)
                pnlRenderQuality.Visibility = render ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ValidateStartButton()
        {
            bool hasUrl        = !string.IsNullOrWhiteSpace(tbURL?.Text);
            bool isTwitchUrl   = hasUrl && tbURL.Text.Trim().Contains("twitch.tv", StringComparison.OrdinalIgnoreCase);
            bool hasOutput     = !string.IsNullOrWhiteSpace(tbOutputPath?.Text);
            bool chatInstalled = TwitchDownloaderService.IsInstalled();
            bool ytdlpExists   = File.Exists(Properties.Settings.Default.YtdlpPath);
            bool wantsVideo    = cbDownloadVideo?.IsChecked == true;
            bool wantsChat     = cbDownloadChat?.IsChecked  == true;
            bool hasAction     = wantsVideo || wantsChat;

            // Video-Download braucht yt-dlp, Chat-Download braucht TwitchDownloaderCLI
            bool toolsOk = (!wantsVideo || ytdlpExists) && (!wantsChat || chatInstalled);

            if (btnStart != null)
                btnStart.IsEnabled = isTwitchUrl && hasOutput && toolsOk && hasAction;

            if (btnOpenOutput != null)
                btnOpenOutput.IsEnabled = !string.IsNullOrEmpty(tbOutputPath?.Text) && Directory.Exists(tbOutputPath?.Text);
        }

        private void SetUiEnabled(bool enabled)
        {
            tbURL.IsEnabled            = enabled;
            tbOutputPath.IsEnabled     = enabled;
            btnUseGlobalPath.IsEnabled = enabled;
            btnBrowseOutput.IsEnabled  = enabled;
            cbDownloadVideo.IsEnabled  = enabled;
            cbDownloadChat.IsEnabled   = enabled;
            rbChatJson.IsEnabled       = enabled;
            rbChatRender.IsEnabled     = enabled;
            cbRenderQuality.IsEnabled  = enabled;
            btnInstall.IsEnabled       = enabled;
            btnUpdateAction.IsEnabled  = enabled && (btnUpdateAction.Tag != null);
            btnUninstall.IsEnabled     = enabled;
            if (!enabled)
                btnStart.IsEnabled = false;
            else
                ValidateStartButton();
        }

        // ── Install / Update ───────────────────────────────────────────────────────

        private async void btnInstall_Click(object sender, RoutedEventArgs e)
        {
            await InstallOrUpdateAsync();
        }

        private void btnUninstall_Click(object sender, RoutedEventArgs e)
        {
            var T = UITextDictionary.Get;
            var result = FluentMessageBox.Show(
                T("TwitchPage.Uninstall.Confirm"),
                T("TwitchPage.Uninstall.Title"),
                MessageBoxButton.YesNo, MessageBoxImage.Warning,
                owner: Window.GetWindow(this));

            if (result != MessageBoxResult.Yes) return;

            try
            {
                string path = System.IO.Path.GetFullPath(Properties.Settings.Default.TwitchDownloaderPath);
                if (File.Exists(path)) File.Delete(path);
                RefreshToolStatus();
                ValidateStartButton();
                FluentMessageBox.Show(
                    T("TwitchPage.Uninstall.Success"),
                    T("TwitchPage.Uninstall.Title"),
                    MessageBoxButton.OK, MessageBoxImage.Information,
                    owner: Window.GetWindow(this));
            }
            catch (Exception ex)
            {
                FluentMessageBox.Show(
                    string.Format(T("TwitchPage.Uninstall.Failed"), ex.Message),
                    T("TwitchPage.Uninstall.Title"),
                    MessageBoxButton.OK, MessageBoxImage.Error,
                    owner: Window.GetWindow(this));
            }
        }

        private async Task InstallOrUpdateAsync()
        {
            var T = UITextDictionary.Get;
            SetStatus(T("TwitchPage.Status.Installing"), true);
            btnInstall.IsEnabled = false;
            btnUpdateAction.IsEnabled = false;

            try
            {
                AppendDebug("[SETUP] Suche nach neuestem TwitchDownloaderCLI-Release...");
                var (version, assetUrl) = await _service.GetLatestReleaseInfoAsync();

                if (assetUrl == null)
                {
                    AppendDebug("[SETUP] Kein Asset gefunden.");
                    SetStatus(T("TwitchPage.Status.Error"), false);
                    FluentMessageBox.Show(
                        string.Format(T("TwitchPage.Error.InstallFailed"), "Kein passendes Windows-Binary im Release gefunden."),
                        T("TwitchPage.UpdateCheck.Title"),
                        MessageBoxButton.OK, MessageBoxImage.Error,
                        owner: Window.GetWindow(this));
                    return;
                }

                string targetPath = System.IO.Path.GetFullPath(Properties.Settings.Default.TwitchDownloaderPath);
                string? targetDir = System.IO.Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                    Directory.CreateDirectory(targetDir);

                AppendDebug($"[SETUP] Lade {assetUrl} → {targetPath}");

                // Immer zuerst in Temp-Datei laden, dann atomisch ersetzen
                // (vermeidet "file is being used by another process" beim Überschreiben)
                string tempTarget = targetPath + ".tmp";

                // Prüfen ob ZIP oder EXE
                if (assetUrl.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    string tempZip = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TwitchDownloaderCLI_dl.zip");
                    await _service.DownloadAssetAsync(assetUrl, tempZip);
                    AppendDebug("[SETUP] ZIP heruntergeladen, entpacke...");

                    using (var archive = ZipFile.OpenRead(tempZip))
                    {
                        foreach (var entry in archive.Entries)
                        {
                            if (entry.Name.Equals("TwitchDownloaderCLI.exe", StringComparison.OrdinalIgnoreCase))
                            {
                                entry.ExtractToFile(tempTarget, overwrite: true);
                                break;
                            }
                        }
                    }
                    File.Delete(tempZip);
                }
                else
                {
                    await _service.DownloadAssetAsync(assetUrl, tempTarget);
                }

                // Atomisches Ersetzen: alte EXE umbenennen, neue einsetzen
                if (File.Exists(targetPath))
                {
                    string backup = targetPath + ".old";
                    if (File.Exists(backup)) File.Delete(backup);
                    File.Move(targetPath, backup);
                    try
                    {
                        File.Move(tempTarget, targetPath);
                        File.Delete(backup);
                    }
                    catch
                    {
                        // Rollback
                        File.Move(backup, targetPath);
                        if (File.Exists(tempTarget)) File.Delete(tempTarget);
                        throw;
                    }
                }
                else
                {
                    File.Move(tempTarget, targetPath);
                }

                AppendDebug($"[SETUP] Installation abgeschlossen. Version: {version}");
                pnlUpdateHint.Visibility = Visibility.Collapsed;
                RefreshToolStatus();
                ValidateStartButton();
                SetStatus(T("TwitchPage.Status.Ready"), false);
            }
            catch (Exception ex)
            {
                AppendDebug($"[SETUP] Fehler: {ex.Message}");
                SetStatus(T("TwitchPage.Status.Error"), false);
                FluentMessageBox.Show(
                    string.Format(T("TwitchPage.Error.InstallFailed"), ex.Message),
                    T("TwitchPage.UpdateCheck.Title"),
                    MessageBoxButton.OK, MessageBoxImage.Error,
                    owner: Window.GetWindow(this));
            }
            finally
            {
                btnInstall.IsEnabled    = true;
                btnUpdateAction.IsEnabled = true;
                // Nach Installation: erneuten Hintergrundcheck anstoßen
                if (TwitchDownloaderService.IsInstalled())
                    _ = CheckForUpdateSilentlyAsync();
            }
        }

        // Stille Hintergrundprüfung beim Seitenaufruf
        private async Task CheckForUpdateSilentlyAsync()
        {
            var T = UITextDictionary.Get;
            btnUpdateAction.Content   = T("TwitchPage.Button.CheckingUpdate");
            btnUpdateAction.IsEnabled = false;
            SetButtonStyle(btnUpdateAction, primary: false);

            try
            {
                var (latestVersion, _) = await _service.GetLatestReleaseInfoAsync();
                string? localVersion   = await _service.GetLocalVersionAsync();

                // Versionen normalisieren: v1.56.4 → 1.56.4
                string localNorm  = (localVersion  ?? "").TrimStart('v', 'V').Trim();
                string latestNorm = (latestVersion ?? "").TrimStart('v', 'V').Trim();

                AppendDebug($"[UPDATE] Lokal: '{localNorm}'  |  Latest: '{latestNorm}'");

                bool updateRequired = _service.IsUpdateRequired(localNorm, latestNorm) && !string.IsNullOrEmpty(latestNorm);

                if (updateRequired)
                {
                    // Update verfügbar → Button in Primärfarbe orange
                    btnUpdateAction.Content   = string.Format(T("TwitchPage.Button.UpdateAvailable"), latestVersion);
                    btnUpdateAction.IsEnabled = true;
                    btnUpdateAction.Tag       = "update:" + latestNorm;
                    SetButtonStyle(btnUpdateAction, primary: true);
                    txtUpdateHint.Text       = string.Format(T("TwitchPage.Tool.UpdateAvailable"), latestVersion);
                    pnlUpdateHint.Visibility = Visibility.Visible;
                }
                else
                {
                    // Kein Update – Button normal, klickbar (löst erneuten Check aus)
                    btnUpdateAction.Content   = T("TwitchPage.Button.NoUpdateAvailable");
                    btnUpdateAction.IsEnabled = true;
                    btnUpdateAction.Tag       = "check";
                    SetButtonStyle(btnUpdateAction, primary: false);
                    pnlUpdateHint.Visibility  = Visibility.Collapsed;
                }
            }
            catch
            {
                // Netzwerkfehler – kein Dialog, Button bleibt klickbar für Retry
                btnUpdateAction.Content   = T("TwitchPage.Button.CheckUpdate");
                btnUpdateAction.IsEnabled = true;
                btnUpdateAction.Tag       = "check";
                SetButtonStyle(btnUpdateAction, primary: false);
            }
        }

        // Setzt den Button-Style: Primär (orange) oder Standard
        private void SetButtonStyle(System.Windows.Controls.Button btn, bool primary)
        {
            if (primary)
                btn.Style = (Style)FindResource("PrimaryButtonStyle");
            else
                btn.ClearValue(System.Windows.Controls.Button.StyleProperty);
        }

        // Klick auf den adaptiven Update/Check-Button
        private async void btnUpdateAction_Click(object sender, RoutedEventArgs e)
        {
            string? tag = btnUpdateAction.Tag as string;
            if (tag != null && tag.StartsWith("update:"))
                await InstallOrUpdateAsync();
            else
                await CheckForUpdateSilentlyAsync();
        }

        // ── Download ───────────────────────────────────────────────────────────────

        private async void btnStart_Click(object sender, RoutedEventArgs e)
        {
            var T = UITextDictionary.Get;

            string rawInput = tbURL.Text.Trim();
            var (contentId, contentType) = TwitchDownloaderService.ParseInput(rawInput);

            if (contentId == null)
            {
                FluentMessageBox.Show(
                    T("TwitchPage.Error.InvalidURL"),
                    "TwitchDownloader",
                    MessageBoxButton.OK, MessageBoxImage.Warning,
                    owner: Window.GetWindow(this));
                return;
            }

            string outputDir = tbOutputPath.Text.Trim();
            if (string.IsNullOrEmpty(outputDir))
            {
                FluentMessageBox.Show(
                    T("TwitchPage.Error.NoOutput"),
                    "TwitchDownloader",
                    MessageBoxButton.OK, MessageBoxImage.Warning,
                    owner: Window.GetWindow(this));
                return;
            }

            if (!TwitchDownloaderService.IsInstalled())
            {
                FluentMessageBox.Show(
                    T("TwitchPage.Error.ToolMissing"),
                    "TwitchDownloader",
                    MessageBoxButton.OK, MessageBoxImage.Warning,
                    owner: Window.GetWindow(this));
                return;
            }

            bool downloadVideo = cbDownloadVideo.IsChecked == true;
            bool downloadChat  = cbDownloadChat.IsChecked == true;
            bool renderChat    = downloadChat && rbChatRender.IsChecked == true;

            tbDebugOutput.Clear();
            SetStatus(T("TwitchPage.Status.Downloading"), true);
            SetUiEnabled(false);
            btnCancel.IsEnabled = true;

            _activeRateLimitMBps = Properties.Settings.Default.DownloadBandwidthMBps;
            _cts = new CancellationTokenSource();

            try
            {
                Directory.CreateDirectory(outputDir);

                bool isClip = contentType == TwitchDownloaderService.ContentType.Clip;

                // Videotitel über Twitch GQL abrufen, Fallback: ID/Slug
                string? rawTitle = null;
                try { rawTitle = await TwitchDownloaderService.GetContentTitleAsync(contentId, contentType, _cts.Token); }
                catch { /* Fallback auf ID */ }
                string safeTitle = string.IsNullOrWhiteSpace(rawTitle)
                    ? System.Text.RegularExpressions.Regex.Replace(contentId, @"[\\/:*?""<>|]", "_")
                    : System.Text.RegularExpressions.Regex.Replace(rawTitle.Trim(), @"[\\/:*?""<>|]", "_");
                // Mehrfache Leerzeichen und führende/nachfolgende Sonderzeichen bereinigen
                safeTitle = System.Text.RegularExpressions.Regex.Replace(safeTitle, @"\s+", " ").Trim(' ', '.');
                if (string.IsNullOrEmpty(safeTitle)) safeTitle = contentId;

                string fileBase = System.IO.Path.Combine(outputDir, safeTitle);
                string presetSuffix = renderChat
                    ? "_" + (cbRenderQuality.SelectedIndex switch { 1 => "high", 2 => "ultra", _ => "standard" })
                    : "";

                // Fortschrittsanzeige: Prozent und Geschwindigkeit aus Ausgabe parsen
                // yt-dlp:              "[download]  42.3% at 5.21MiB/s"
                // TwitchDownloaderCLI: "[STATUS] - 42%"
                var pctRegex      = new Regex(@"(\d{1,3}(?:[.,]\d+)?)\s*%");
                var speedYtRegex  = new Regex(@"at\s+([\d.]+)(KiB|MiB|GiB)/s", RegexOptions.IgnoreCase);
                var speedAltRegex = new Regex(@"([\d.,]+\s*(?:MiB|KiB|MB|KB|GiB|GB)/s)", RegexOptions.IgnoreCase);
                void UpdateProgress(string line)
                {
                    AppendDebug(line);
                    Dispatcher.Invoke(() =>
                    {
                        var m = pctRegex.Match(line);
                        if (m.Success && double.TryParse(m.Groups[1].Value.Replace(',', '.'),
                            NumberStyles.Any, CultureInfo.InvariantCulture, out double pct))
                        {
                            pbDownload.IsIndeterminate = false;
                            pbDownload.Value = Math.Min(100, pct);
                        }
                        var s = speedYtRegex.Match(line);
                        if (s.Success && double.TryParse(s.Groups[1].Value, NumberStyles.Any,
                            CultureInfo.InvariantCulture, out double spd))
                        {
                            double mbps = s.Groups[2].Value.ToUpperInvariant() switch
                            {
                                "KIB" => spd / 1024.0,
                                "GIB" => spd * 1024.0,
                                _     => spd
                            };
                            txtDownloadSpeed.Text = $"{mbps:F2} MiB/s";
                        }
                        else
                        {
                            var s2 = speedAltRegex.Match(line);
                            if (s2.Success) txtDownloadSpeed.Text = s2.Groups[1].Value;
                        }
                    });
                }
                var progress = new Progress<string>(UpdateProgress);

                // ── Video via yt-dlp ──────────────────────────────────────────────
                if (downloadVideo)
                {
                    string ytDlpPath    = Properties.Settings.Default.YtdlpPath;
                    string safeTitleForTemplate = safeTitle.Replace("\"", "'");
                    string outputTemplate = $"\"{outputDir}\\{safeTitleForTemplate}{presetSuffix}_%(id)s.%(ext)s\"";
                    string mergeArg  = isClip ? "" : "--merge-output-format mp4 ";
                    string formatArg = "best[ext=mp4]/bestvideo[ext=mp4]+bestaudio[ext=m4a]/bestvideo+bestaudio/best";

                    // Neustart-Schleife: bei Kill durch ApplyBandwidthChange wird mit neuem Limit + --continue fortgesetzt
                    while (!_cts.Token.IsCancellationRequested)
                    {
                        double bwMbps = _activeRateLimitMBps;
                        string bwArg  = bwMbps > 0
                            ? $"--limit-rate {bwMbps.ToString(CultureInfo.InvariantCulture)}M "
                            : "";

                        string args = $"-f \"{formatArg}\" {mergeArg}{bwArg}" +
                                      $"--no-check-certificates --no-mtime --newline --no-playlist " +
                                      $"-o {outputTemplate} \"{rawInput}\"";

                        AppendDebug($"[VIDEO] yt-dlp starten{(bwMbps > 0 ? $" (Limit: {bwMbps} MB/s)" : "")}: {args}");
                        SetStatus(T("TwitchPage.Status.Downloading"), true);
                        bool needsRestart = await RunYtDlpAsync(ytDlpPath, args, _cts.Token, progress);

                        if (!needsRestart) break; // erfolgreich fertig oder abgebrochen
                    }
                }

                // ── Chat via TwitchDownloaderCLI ──────────────────────────────────
                if (downloadChat)
                {
                    SetStatus(T("TwitchPage.Status.DownloadingChat"), true);
                    string chatOutput = fileBase + "_chat.json";
                    AppendDebug($"[CHAT] Starte Chat-Download: ID/Slug={contentId}");
                    await TwitchDownloaderService.DownloadChatAsync(contentId, chatOutput, progress, _cts.Token);

                    if (renderChat)
                    {
                        SetStatus(T("TwitchPage.Status.RenderingChat"), true);
                        string renderOutput = fileBase + "_chat" + presetSuffix + ".mp4";
                        var preset = cbRenderQuality.SelectedIndex switch
                        {
                            1 => TwitchDownloaderService.RenderQualityPreset.High,
                            2 => TwitchDownloaderService.RenderQualityPreset.Ultra,
                            _ => TwitchDownloaderService.RenderQualityPreset.Standard,
                        };
                        AppendDebug($"[RENDER] Starte Chat-Rendering ({preset}): {chatOutput} → {renderOutput}");
                        await TwitchDownloaderService.RenderChatAsync(chatOutput, renderOutput, preset, progress, _cts.Token);
                    }
                }

                SetStatus(T("TwitchPage.Status.Success"), false);
                Dispatcher.Invoke(() => { pbDownload.IsIndeterminate = false; pbDownload.Value = 100; txtDownloadSpeed.Text = ""; });
                btnOpenOutput.IsEnabled = true;
                AppendDebug("[FERTIG] Download abgeschlossen.");
            }
            catch (OperationCanceledException)
            {
                SetStatus(T("TwitchPage.Status.Canceled"), false);
                Dispatcher.Invoke(() => txtDownloadSpeed.Text = "");
                AppendDebug("[ABBRUCH] Download wurde abgebrochen.");
            }
            catch (Exception ex)
            {
                SetStatus(T("TwitchPage.Status.Error"), false);
                AppendDebug($"[FEHLER] {ex.Message}");
                FluentMessageBox.Show(
                    ex.Message,
                    T("TwitchPage.Status.Error"),
                    MessageBoxButton.OK, MessageBoxImage.Error,
                    owner: Window.GetWindow(this));
            }
            finally
            {
                SetUiEnabled(true);
                btnCancel.IsEnabled = false;
                _currentYtDlpProcess = null;
                _cts?.Dispose();
                _cts = null;
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
        }

        // ── Hilfsmethoden ──────────────────────────────────────────────────────────

        private void SetStatus(string text, bool isRunning)
        {
            txtDownloadStatus.Text = text;
            spLoadingbar.Visibility = Visibility.Visible;
            pbDownload.IsIndeterminate = isRunning;
            if (!isRunning && pbDownload.Value != 100)
                pbDownload.Value = 0;
        }

        private void AppendDebug(string line)
        {
            tbDebugOutput.AppendText(line + Environment.NewLine);
            tbDebugOutput.ScrollToEnd();
        }

        /// <summary>
        /// Führt yt-dlp aus. Gibt <c>true</c> zurück wenn ein Neustart mit neuem Limit nötig ist
        /// (Limit-Änderung während Download), <c>false</c> bei erfolgreichem Abschluss.
        /// </summary>
        private async Task<bool> RunYtDlpAsync(string ytDlpPath, string arguments, CancellationToken token,
            IProgress<string>? progress = null)
        {
            string args = arguments.Contains("--continue") ? arguments : "--continue " + arguments;

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName               = ytDlpPath,
                    Arguments              = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding  = Encoding.UTF8,
                }
            };

            process.OutputDataReceived += (_, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;
                progress?.Report(e.Data);
                Dispatcher.BeginInvoke(() => AppendDebug(e.Data));
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;
                progress?.Report(e.Data);
                Dispatcher.BeginInvoke(() => AppendDebug($"[STDERR] {e.Data}"));
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            _currentYtDlpProcess = process;

            await using var killReg = token.Register(() =>
            {
                try { process.Kill(entireProcessTree: true); } catch { }
            });

            await process.WaitForExitAsync(CancellationToken.None);
            process.WaitForExit();
            _currentYtDlpProcess = null;

            // Absichtlicher Kill wegen Limit-Änderung → kein Fehler, Neustart nötig
            if (_bandwidthKillPending)
            {
                _bandwidthKillPending = false;
                token.ThrowIfCancellationRequested();
                return true;
            }

            token.ThrowIfCancellationRequested();

            if (process.ExitCode != 0)
                throw new InvalidOperationException($"yt-dlp beendet mit Exit-Code {process.ExitCode}");

            return false;
        }

        /// <summary>
        /// Wird von der SettingsPage (oder intern) aufgerufen, wenn das Bandbreiten-Limit
        /// während eines laufenden Downloads geändert wird.
        /// Der laufende yt-dlp-Prozess wird mit Kill beendet; der übergeordnete
        /// Download-Task startet ihn mit dem neuen Limit und --continue neu.
        /// </summary>
        public void ApplyBandwidthChange()
        {
            double newLimit = Properties.Settings.Default.DownloadBandwidthMBps;
            if (Math.Abs(newLimit - _activeRateLimitMBps) < 0.001) return;

            _activeRateLimitMBps = newLimit;

            if (_currentYtDlpProcess != null && !_currentYtDlpProcess.HasExited)
            {
                _bandwidthKillPending = true;
                Dispatcher.BeginInvoke(() =>
                    AppendDebug($"[LIMIT] Bandbreite auf {(newLimit > 0 ? $"{newLimit} MB/s" : "unbegrenzt")} geändert – yt-dlp wird mit --continue neu gestartet"));
                try { _currentYtDlpProcess.Kill(entireProcessTree: true); } catch { }
            }
        }

        private void tbDebugOutput_TextChanged(object sender, TextChangedEventArgs e)
        {
            tbDebugOutput.ScrollToEnd();
        }
    }
}
