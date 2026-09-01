using MortysDLP.Helpers;
using MortysDLP.Models;
using MortysDLP.Services;
using MortysDLP.Services.Releases;
using MortysDLP.Services.Tools;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MortysDLP
{
    /// <summary>
    /// Interaktionslogik für StartupWindow.xaml
    /// </summary>
    public partial class StartupWindow : Window
    {
        // Die ffmpeg-Paketadresse steht nicht mehr hier: Sie gehört zum Werkzeug, nicht zum
        // Startbildschirm (siehe FfmpegTool).
        private readonly string YtDlpDocUrl = Properties.Resources.URL_YTDLP;

        public StartupWindow()
        {
            InitializeComponent();
            // Sprache wurde bereits in App.xaml.cs gesetzt - nicht nochmal aufrufen!
            Log.Debug($"Language: {UITexte.UITextDictionary.CurrentLanguage}");
            SetUITexts();
            StartLoadingAnimation();
        }

        private void SetUITexts()
        {
            var T = UITexte.UITextDictionary.Get;
            TitleText.Text = T("StartupWindow.Title");
        }

        /// <summary>Textbausteine für den Nutzer werden in der eingestellten Kultur
        /// zusammengesetzt — hier ausdrücklich statt beiläufig, damit die Absicht im Code steht und
        /// nicht vom Standardverhalten abhängt.</summary>
        private static string Fmt(string format, params object?[] args) =>
            string.Format(CultureInfo.CurrentCulture, format, args);

        private void StartLoadingAnimation()
        {
            var animation = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = TimeSpan.FromSeconds(2),
                RepeatBehavior = RepeatBehavior.Forever
            };
            LoadingRotation.BeginAnimation(RotateTransform.AngleProperty, animation);
        }

        public void SetStatus(string text)
        {
            if (!Dispatcher.CheckAccess())
                Dispatcher.Invoke(() => StatusText.Text = text);
            else
                StatusText.Text = text;
        }

        public void SetTitle(string text)
        {
            TitleText.Text = text;
        }

        public void SetLogo(string imagePath)
        {
            LogoImage.Source = new System.Windows.Media.Imaging.BitmapImage(new System.Uri(imagePath, System.UriKind.RelativeOrAbsolute));
        }

        /// <summary>Übernimmt einmalig Werkzeuge aus dem alten Installationsort
        /// (<see cref="AppPaths.LegacyToolsDir"/>) nach <see cref="AppPaths.ToolsDir"/>, falls
        /// dort noch welche liegen. Muss laufen, bevor <see cref="ToolUpdaterAsync"/>
        /// irgendetwas nach <see cref="AppPaths.ToolsDir"/> schreibt. Die Statuszeile
        /// erscheint bewusst nur, wenn es tatsächlich etwas zu übernehmen gibt — sonst bleibt
        /// der ganz überwiegende Fall (kein alter Ordner, oder bereits übernommen) unsichtbar
        /// und ohne spürbaren Zeitverlust.</summary>
        public async Task MigrateToolsAsync()
        {
            try
            {
                ToolsMigration.MigrationResult result;

                if (AppPaths.LegacyToolsDirHasContent())
                {
                    var T = UITexte.UITextDictionary.Get;
                    SetStatus(T("StartupWindow.Status.MigratingTools"));
                    // Kann bei Whisper-Modellen mehrere GB umfassen und, wenn Programm- und
                    // Nutzerordner auf verschiedenen Laufwerken liegen, spürbar dauern -
                    // deshalb wie ZipFile.ExtractToDirectory in Task.Run, sonst friert die
                    // Oberfläche samt Ladeanimation ein.
                    result = await Task.Run(AppPaths.EnsureToolsDirAndMigrate);
                }
                else
                {
                    result = AppPaths.EnsureToolsDirAndMigrate();
                }

                LogMigrationResult(result);
            }
            catch (Exception ex)
            {
                // Best-Effort: Eine fehlgeschlagene Übernahme darf den Start nicht verhindern -
                // die Werkzeuge werden dann regulär neu heruntergeladen.
                Log.Warn("Übernahme vorhandener Werkzeuge aus dem alten Installationsort fehlgeschlagen", ex);
            }
        }

        private static void LogMigrationResult(ToolsMigration.MigrationResult result)
        {
            if (result.MigratedFiles.Count == 0 && result.DuplicatedFiles.Count == 0 && result.FailedFiles.Count == 0)
            {
                Log.Info("Werkzeug-Übernahme: Im alten Installationsordner gab es nichts zu übernehmen.");
                return;
            }

            foreach (string file in result.MigratedFiles)
                Log.Info($"Werkzeug aus dem alten Installationsort übernommen: {file}");

            foreach (string file in result.DuplicatedFiles)
                Log.Warn($"Werkzeug '{file}' konnte nicht verschoben werden und wurde stattdessen " +
                    "kopiert - die alte Datei bleibt liegen, es existieren jetzt zwei Kopien.");

            Log.Info($"Werkzeug-Übernahme abgeschlossen: {result.MigratedFiles.Count} Datei(en) übernommen.");

            if (result.OldDirRemoved)
                Log.Info("Alter Werkzeugordner wurde entfernt.");
            else if (result.FailedFiles.Count > 0)
                Log.Warn("Alter Werkzeugordner bleibt bestehen, folgende Datei(en) blieben zurück: " +
                    string.Join(", ", result.FailedFiles));
        }

        /// <summary>
        /// Prüft die für den Betrieb <b>erforderlichen</b> Werkzeuge und liefert <c>false</c>,
        /// wenn danach eines davon fehlt. Zweigeteilt:
        ///
        /// <para><b>Sofort, ohne Netz:</b> Ist die Datei da, und antwortet sie als das
        /// erwartete Programm (<see cref="ToolStartupDecision"/>)? Läuft für alle
        /// erforderlichen Werkzeuge <b>gleichzeitig</b> (<see cref="ProbeRequiredToolsAsync"/>)
        /// statt nacheinander — eine langsame Prüfung hält die andere nicht mehr auf. Das
        /// entscheidet, ob die Anwendung überhaupt starten kann.</para>
        ///
        /// <para><b>Danach, im Hintergrund:</b> Ob es eine neuere Version gibt, prüft
        /// <c>App.RunBackgroundToolCheckAsync</c> erst, nachdem das Hauptfenster bereits
        /// sichtbar ist — dieselbe Aufteilung wie beim Update der Anwendung selbst
        /// (<c>App.RunBackgroundUpdateCheckAsync</c>). Ein Angebot zum Aktualisieren gibt es an
        /// dieser Stelle deshalb nicht mehr; wer aktualisieren will, tut das auf der Seite
        /// „Werkzeuge".</para>
        ///
        /// <para>Fehlt ein Werkzeug oder antwortet es nicht richtig, bleibt der bekannte Ablauf
        /// unverändert: nachfragen, laden, bei Ablehnung sauber beenden
        /// (<see cref="InstallMissingToolAsync"/>) — streng nacheinander, weil ein Dialog kein
        /// Netzvorgang ist, den man parallelisieren könnte.</para>
        ///
        /// <para>Auch whisper.cpp und TwitchDownloaderCLI stehen mittlerweile in
        /// <see cref="ToolCatalog.CreateAll"/> — deshalb hier die Einschränkung auf
        /// <see cref="IManagedTool.RequiredForOperation"/>: Beide sind optionale Funktionen und
        /// werden weiterhin erst auf ihrer jeweiligen Seite geprüft, nicht bei jedem Start.</para>
        /// </summary>
        public async Task<bool> ToolUpdaterAsync(IProgress<string>? progress = null)
        {
            var T = UITexte.UITextDictionary.Get;

            try
            {
                WarnIfRunningFromArchive(T);

                var catalog = new ToolCatalog();
                var requiredTools = ToolCatalog.CreateAll().Where(t => t.RequiredForOperation).ToList();

                var checkedByToolId = await ProbeRequiredToolsAsync(requiredTools, T);
                var (allPresent, summary) = await EnsureRequiredToolsPresentAsync(catalog, requiredTools, checkedByToolId, T);

                Log.Info($"Werkzeuge: {string.Join(", ", summary)}");
                return allPresent;
            }
            catch (Exception ex)
            {
                Log.Error("Werkzeug-Prüfung beim Start fehlgeschlagen", ex);
                Dispatcher.Invoke(() => FluentMessageBox.Show(
                    Fmt(T("StartupWindow.Error.ToolUpdate"), ex.Message),
                    T("StartupWindow.Title.Error"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error));
                return false;
            }
        }

        /// <summary>Existenz und Ausweis für alle erforderlichen Werkzeuge — ganz ohne
        /// Netzzugriff. Die Existenzprüfung (<see cref="IManagedTool.GetStatus"/>) kostet
        /// nichts und läuft deshalb einfach der Reihe nach; der Ausweis
        /// (<see cref="IManagedTool.ProbeAsync"/>, ein kurzer Programmstart bzw. eine
        /// Dateieigenschaft, aber kein Netzzugriff) läuft für alle vorhandenen Werkzeuge
        /// <b>gleichzeitig</b>, damit die Gesamtdauer der langsamsten einzelnen Prüfung
        /// entspricht, nicht ihrer Summe (02-BEST-PRACTICES.md, Abschnitt 3/4).</summary>
        private async Task<IReadOnlyDictionary<string, (ToolStatus Status, ToolProbe Probe)>> ProbeRequiredToolsAsync(
            IReadOnlyList<IManagedTool> requiredTools, Func<string, string> T)
        {
            var statuses = new Dictionary<string, ToolStatus>();

            foreach (var tool in requiredTools)
            {
                SetStatus(StatusChecking(tool, T));
                var status = tool.GetStatus();
                statuses[tool.Id] = status;
                SetStatus(status.Installed ? StatusCheckingVersion(tool, T) : StatusNotFound(tool, T));
            }

            var installedTools = requiredTools.Where(t => statuses[t.Id].Installed).ToList();
            var probes = await Task.WhenAll(installedTools.Select(t => t.ProbeAsync(CancellationToken.None)));
            var probeByToolId = installedTools
                .Select((tool, i) => (tool.Id, probes[i]))
                .ToDictionary(x => x.Id, x => x.Item2);

            return requiredTools.ToDictionary(
                tool => tool.Id,
                tool => (statuses[tool.Id], probeByToolId.GetValueOrDefault(tool.Id, ToolProbe.NotInstalled)));
        }

        /// <summary>Fragt nach und installiert für jedes Werkzeug, das laut
        /// <see cref="ToolStartupDecision"/> nicht sofort starten darf — streng nacheinander,
        /// in der Reihenfolge von <paramref name="requiredTools"/> (yt-dlp zuerst), und bricht
        /// beim ersten abgelehnten oder fehlgeschlagenen Werkzeug ab: Wer yt-dlp ablehnt, soll
        /// nicht im nächsten Atemzug auch noch nach ffmpeg gefragt werden.</summary>
        private async Task<(bool AllPresent, List<string> Summary)> EnsureRequiredToolsPresentAsync(
            ToolCatalog catalog, IReadOnlyList<IManagedTool> requiredTools,
            IReadOnlyDictionary<string, (ToolStatus Status, ToolProbe Probe)> checkedByToolId, Func<string, string> T)
        {
            var summary = new List<string>();

            foreach (var tool in requiredTools)
            {
                var (status, probe) = checkedByToolId[tool.Id];

                if (ToolStartupDecision.For(status, probe) == ToolStartupAction.CanProceed)
                {
                    summary.Add($"{tool.Id}={probe.Version.Raw}");
                    continue;
                }

                // Fehlt oder nicht brauchbar: jetzt einmal live fragen - der Ausweis allein
                // reicht für die Installation nicht, es fehlt die Bezugsadresse
                // (ToolCatalog.CheckAsync holt beides zusammen).
                var outcome = await catalog.CheckAsync(tool, force: false, CancellationToken.None);
                var (present, version) = await InstallMissingToolAsync(catalog, tool, outcome, T);

                summary.Add($"{tool.Id}={(present ? (version.HasValue ? version.Raw : "vorhanden") : "fehlt")}");

                if (!present)
                    return (false, summary);
            }

            return (true, summary);
        }

        /// <summary>Zeigt einen nicht blockierenden Hinweis, wenn MortysDLP erkennbar aus der
        /// ZIP-Vorschau des Explorers gestartet wurde — heruntergeladene Werkzeuge gingen sonst
        /// beim Schließen kommentarlos verloren. Die Erkennung ist heuristisch, deshalb wird
        /// nicht blockiert: der Nutzer entscheidet, ob er den Ordner öffnet oder fortfährt.</summary>
        private void WarnIfRunningFromArchive(Func<string, string> t)
        {
            var info = InstallLocation.Analyze();
            if (info.Kind != InstallKind.RunningFromArchive) return;

            var result = FluentMessageBox.Show(
                t("InstallLocation.Warning.Archive"),
                "",
                MessageBoxImage.Warning,
                this,
                (t("InstallLocation.Button.OpenFolder"), MessageBoxResult.Yes, true),
                (t("InstallLocation.Button.Continue"), MessageBoxResult.No, false));

            if (result == MessageBoxResult.Yes)
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = info.Path, UseShellExecute = true }); }
                catch (Exception ex) { Log.Warn("Installationsordner konnte nicht geöffnet werden", ex); }
            }
        }

        /// <summary>Fragt nach und installiert — sowohl für ein fehlendes als auch für ein
        /// vorhandenes, aber unbrauchbares Werkzeug. Lehnt der Nutzer ein für den Betrieb
        /// erforderliches Werkzeug ab, erklärt der Dialog, wie es von Hand nachgeholt werden kann,
        /// und die Anwendung beendet sich geordnet.</summary>
        private async Task<(bool Present, ToolVersion Version)> InstallMissingToolAsync(
            ToolCatalog catalog, IManagedTool tool, ToolCheckOutcome outcome, Func<string, string> T)
        {
            bool broken = outcome.Probe.Health is ToolHealth.NoAnswer or ToolHealth.Foreign;

            string title = broken
                ? Fmt(T("StartupWindow.Tool.BrokenTitle"), tool.DisplayName)
                : Fmt(T("StartupWindow.Tool.MissingTitle"), tool.DisplayName);

            string message = broken
                ? Fmt(T("StartupWindow.Tool.BrokenMessage"), tool.DisplayName) +
                    Fmt(T("StartupWindow.Tool.BrokenQuestion"), tool.DisplayName)
                : BuildIntroMessage(tool, T);

            var answer = FluentMessageBox.Show(
                message,
                title,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                this);

            if (answer != MessageBoxResult.Yes)
            {
                Log.Warn($"[{tool.Id}] Installation vom Nutzer abgelehnt " +
                    $"({ManagedToolBase.DescribeProbe(outcome.Probe)}).");

                FluentMessageBox.Show(
                    BuildRequiredMessage(tool, T),
                    title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error,
                    this);

                if (tool.RequiredForOperation)
                    Application.Current.Shutdown();

                return (false, ToolVersion.Unknown);
            }

            var install = await RunInstallAsync(catalog, tool, outcome, T);

            if (install.Success)
            {
                FluentMessageBox.Show(
                    Fmt(T("StartupWindow.Tool.InstallSuccess"), tool.DisplayName),
                    T("StartupWindow.Title.DownloadComplete"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information,
                    this);

                return (true, install.NewVersion);
            }

            if (install.Status != ToolInstallStatus.Canceled)
            {
                FluentMessageBox.Show(
                    Fmt(T("StartupWindow.Tool.InstallFailed"), tool.DisplayName),
                    T("StartupWindow.Title.Error"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error,
                    this);
            }

            // Nach einem Fehlschlag noch einmal nachfragen statt zu raten: Der Start darf nur
            // weiterlaufen, wenn dort jetzt wirklich das Werkzeug liegt.
            return await ConfirmUsableAsync(tool);
        }

        /// <summary>Fragt das Werkzeug erneut, ob es jetzt brauchbar ist — nach einem
        /// fehlgeschlagenen oder abgebrochenen Installationsversuch. Kostet einen Prozessstart und
        /// ist genau dann richtig: Der Start darf nur weiterlaufen, wenn dort tatsächlich das
        /// Werkzeug liegt, nicht nur eine Datei mit dem passenden Namen.</summary>
        private static async Task<(bool Present, ToolVersion Version)> ConfirmUsableAsync(IManagedTool tool)
        {
            var probe = await tool.ProbeAsync(CancellationToken.None);
            return (probe.Usable, probe.Version);
        }

        /// <summary>Führt die Installation mit Fortschrittsdialog und Statuszeile aus. Die
        /// Abschnittsmeldungen kommen als Aufzählungswert aus der Werkzeugschicht und werden erst
        /// hier zu Text — <c>Progress&lt;T&gt;</c> wird im UI-Thread erzeugt und meldet deshalb
        /// auch dorthin zurück.
        ///
        /// <para>Die Release-Antwort wird hier und nicht beim Aufrufer beschafft: Ein aus dem
        /// Zwischenspeicher gelesener Stand kennt keine Anhänge und damit keine Prüfsumme —
        /// <see cref="ToolCatalog.ResolveForInstallAsync"/> holt dafür einmal frisch.</para></summary>
        private async Task<ToolInstallOutcome> RunInstallAsync(
            ToolCatalog catalog, IManagedTool tool, ToolCheckOutcome outcome, Func<string, string> T)
        {
            var release = await catalog.ResolveForInstallAsync(outcome, CancellationToken.None);

            string downloadText = Fmt(T("StartupWindow.Status.Downloading"), tool.DisplayName);

            using var dialog = new DownloadProgressDialog(downloadText);
            dialog.Owner = this;
            dialog.Show();

            var progress = new Progress<double>(dialog.SetProgress);

            // Nach dem Download bleibt der Dialog offen (Entpacken, Ersetzen, Prüfen) - ohne
            // eigene Anzeige dieser Abschnitte sähe er bei 100 % eingefroren aus, obwohl im
            // Hintergrund weitergearbeitet wird. Die Statuszeile im Hintergrundfenster
            // (SetStatus) allein reicht nicht: Der modale Dialog verdeckt sie.
            var stage = new Progress<ToolInstallStage>(s =>
            {
                string text = StageText(s, tool.DisplayName, T);
                SetStatus(text);

                if (s != ToolInstallStage.Downloading)
                    dialog.SetStage(text);
            });

            SetStatus(downloadText);

            var installOutcome = await tool.InstallAsync(release, progress, stage, dialog.CancellationToken);

            if (installOutcome.Status == ToolInstallStatus.Canceled)
                SetStatus(T("StartupWindow.Status.DownloadCanceled"));
            else if (!installOutcome.Success)
                SetStatus(T("StartupWindow.Status.DownloadFailed"));

            return installOutcome;
        }

        private static string StageText(ToolInstallStage stage, string displayName, Func<string, string> T) =>
            stage switch
            {
                ToolInstallStage.Downloading => Fmt(T("StartupWindow.Status.Downloading"), displayName),
                ToolInstallStage.Extracting => Fmt(T("StartupWindow.Status.Extracting"), displayName),
                ToolInstallStage.Replacing => Fmt(T("StartupWindow.Status.Replacing"), displayName),
                _ => Fmt(T("StartupWindow.Status.Verifying"), displayName),
            };

        // Zuordnung Werkzeug -> Textschlüssel. Sie steht bewusst hier in der Oberfläche und nicht
        // in IManagedTool: Ein Dienst, der seine eigenen Dialogtexte kennt, ist kein Dienst mehr.
        // Ein Werkzeug ohne Eintrag bekommt keine erfundenen Texte, sondern eine Protokollwarnung
        // und die allgemeine Fassung - so fällt beim Ergänzen weiterer Werkzeuge auf, was fehlt,
        // statt dass yt-dlp-Texte für etwas anderes erscheinen.

        private static string StatusChecking(IManagedTool tool, Func<string, string> T) => tool.Id switch
        {
            "yt-dlp" => T("StartupWindow.Status.CheckingYtDlp"),
            "ffmpeg" => T("StartupWindow.Status.CheckingFfmpeg"),
            _ => Fmt(T("StartupWindow.Status.CheckingTool"), tool.DisplayName),
        };

        private static string StatusCheckingVersion(IManagedTool tool, Func<string, string> T) => tool.Id switch
        {
            "yt-dlp" => T("StartupWindow.Status.CheckingYtDlpVersion"),
            "ffmpeg" => T("StartupWindow.Status.CheckingFfmpegVersion"),
            _ => Fmt(T("StartupWindow.Status.CheckingToolVersion"), tool.DisplayName),
        };

        private static string StatusNotFound(IManagedTool tool, Func<string, string> T) => tool.Id switch
        {
            "yt-dlp" => T("StartupWindow.Status.YtDlpNotFound"),
            "ffmpeg" => T("StartupWindow.Status.FfmpegNotFound"),
            _ => Fmt(T("StartupWindow.Status.ToolNotFound"), tool.DisplayName),
        };

        private string BuildIntroMessage(IManagedTool tool, Func<string, string> T)
        {
            switch (tool.Id)
            {
                case "yt-dlp":
                    return Fmt(T("StartupWindow.YtDlp.Message"), YtDlpDocUrl) +
                        T("StartupWindow.YtDlp.Question");

                case "ffmpeg":
                    return T("StartupWindow.Ffmpeg.Message") + T("StartupWindow.Ffmpeg.Question");

                default:
                    Log.Warn($"[{tool.Id}] Für dieses Werkzeug gibt es noch keinen Einführungstext - " +
                        "es wird die allgemeine Fassung angezeigt.");
                    return Fmt(T("StartupWindow.Tool.MissingTitle"), tool.DisplayName);
            }
        }

        private string BuildRequiredMessage(IManagedTool tool, Func<string, string> T)
        {
            switch (tool.Id)
            {
                case "yt-dlp":
                    return Fmt(T("StartupWindow.YtDlp.Required"),
                        tool.DisplayName, Properties.Settings.Default.MortysDLPGitHubURL);

                case "ffmpeg":
                    return Fmt(T("StartupWindow.Ffmpeg.Required"),
                        Properties.Settings.Default.MortysDLPGitHubURL);

                default:
                    Log.Warn($"[{tool.Id}] Für dieses Werkzeug gibt es noch keinen Hinweistext zur " +
                        "manuellen Installation - es wird die allgemeine Fassung angezeigt.");
                    return Fmt(T("StartupWindow.Tool.InstallFailed"), tool.DisplayName);
            }
        }

        private void ShowError(string message) =>
            FluentMessageBox.Show(message, icon: MessageBoxImage.Error, owner: this);

    }
}
