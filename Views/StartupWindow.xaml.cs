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
        /// Prüft alle verwalteten Werkzeuge über <see cref="ToolCatalog"/> und liefert
        /// <c>false</c>, wenn danach ein für den Betrieb erforderliches Werkzeug fehlt. Für jedes
        /// Werkzeug denselben Weg: vorhanden? → sonst anbieten und installieren; vorhanden, aber
        /// etwas Neueres da? → anbieten. Zwei getrennte Wege für yt-dlp und ffmpeg gab es hier
        /// früher, mit jeweils eigener Download-, Ersetzungs- und Fehlerbehandlung — und beide
        /// ersetzten Dateien ohne zu prüfen, ob das Werkzeug danach überhaupt noch antwortet.
        ///
        /// <para>Die Prüfungen laufen bewusst weiterhin nacheinander und vor dem Hauptfenster.
        /// Das Nebenläufigmachen und Verlegen in den Hintergrund ist eine eigene Aufgabe — sie
        /// setzt voraus, dass alle Werkzeuge über diese Abstraktion laufen, und wird sonst zweimal
        /// gemacht.</para>
        /// </summary>
        public async Task<bool> ToolUpdaterAsync(IProgress<string>? progress = null)
        {
            var T = UITexte.UITextDictionary.Get;

            try
            {
                WarnIfRunningFromArchive(T);

                var catalog = new ToolCatalog();
                var summary = new List<string>();
                bool allRequiredPresent = true;

                foreach (var tool in ToolCatalog.CreateAll())
                {
                    var (present, version) = await HandleToolAsync(catalog, tool, T);

                    summary.Add($"{tool.Id}={(present ? version.HasValue ? version.Raw : "vorhanden" : "fehlt")}");

                    if (present || !tool.RequiredForOperation)
                        continue;

                    // Ohne ein erforderliches Werkzeug abbrechen, statt die restlichen Dialoge
                    // noch abzufragen: Der Nutzer hat entweder abgelehnt (dann wurde die
                    // Anwendung schon beendet) oder die Installation ist gescheitert.
                    allRequiredPresent = false;
                    break;
                }

                Log.Info($"Werkzeuge: {string.Join(", ", summary)}");
                return allRequiredPresent;
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

        /// <summary>Ein Werkzeug vollständig behandeln: prüfen, ggf. installieren, ggf. Update
        /// anbieten. Liefert, ob es danach einsatzbereit ist, und die dann bekannte Version — der
        /// Rückgabewert erspart der Zusammenfassung im Protokoll einen zweiten Prozessstart nur
        /// zum Nachfragen.</summary>
        private async Task<(bool Present, ToolVersion Version)> HandleToolAsync(
            ToolCatalog catalog, IManagedTool tool, Func<string, string> T)
        {
            SetStatus(StatusChecking(tool, T));

            // Die reine Dateiprüfung kostet nichts und entscheidet, welche Statuszeile vor der
            // Netzabfrage sinnvoll ist.
            var initialStatus = tool.GetStatus();
            SetStatus(initialStatus.Installed
                ? StatusCheckingVersion(tool, T)
                : StatusNotFound(tool, T));

            var outcome = await catalog.CheckAsync(tool, force: false, CancellationToken.None);

            if (!outcome.Status.Installed)
                return await InstallMissingToolAsync(tool, outcome, T);

            if (!outcome.Verdict.Offer)
                return (true, outcome.LocalVersion);

            return await OfferToolUpdateAsync(tool, outcome, T);
        }

        /// <summary>Fragt nach und installiert. Lehnt der Nutzer ein für den Betrieb
        /// erforderliches Werkzeug ab, erklärt der Dialog, wie es von Hand nachgeholt werden kann,
        /// und die Anwendung beendet sich geordnet — unverändertes Verhalten.</summary>
        private async Task<(bool Present, ToolVersion Version)> InstallMissingToolAsync(
            IManagedTool tool, ToolCheckOutcome outcome, Func<string, string> T)
        {
            string title = Fmt(T("StartupWindow.Tool.MissingTitle"), tool.DisplayName);

            var answer = FluentMessageBox.Show(
                BuildIntroMessage(tool, T),
                title,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                this);

            if (answer != MessageBoxResult.Yes)
            {
                Log.Warn($"[{tool.Id}] Installation vom Nutzer abgelehnt.");

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

            var install = await RunInstallAsync(tool, outcome.Release, T);

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

            return (tool.GetStatus().Installed, ToolVersion.Unknown);
        }

        /// <summary>Bietet ein Update an — und führt es nie ohne Zustimmung durch. Schlägt es fehl,
        /// bleibt das Werkzeug in der vorherigen Fassung einsatzbereit; der Start läuft weiter.</summary>
        private async Task<(bool Present, ToolVersion Version)> OfferToolUpdateAsync(
            IManagedTool tool, ToolCheckOutcome outcome, Func<string, string> T)
        {
            string message = Fmt(T("StartupWindow.ToolUpdate.NewVersion"),
                tool.DisplayName, outcome.RemoteVersion, outcome.LocalVersion);

            var answer = FluentMessageBox.Show(
                message + T("StartupWindow.ToolUpdate.Question"),
                Fmt(T("StartupWindow.ToolUpdate.Title"), tool.DisplayName),
                MessageBoxButton.YesNo,
                MessageBoxImage.Information,
                this);

            if (answer != MessageBoxResult.Yes)
            {
                Log.Info($"[{tool.Id}] Update auf {outcome.RemoteVersion} vom Nutzer abgelehnt - " +
                    $"{outcome.LocalVersion} bleibt installiert.");
                return (true, outcome.LocalVersion);
            }

            var install = await RunInstallAsync(tool, outcome.Release, T);

            if (install.Success)
            {
                FluentMessageBox.Show(
                    Fmt(T("StartupWindow.ToolUpdate.Success"), tool.DisplayName),
                    T("StartupWindow.Title.DownloadComplete"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information,
                    this);

                return (true, install.NewVersion);
            }

            // Der zurückgenommene Fall bekommt eine eigene Meldung: „fehlgeschlagen" allein würde
            // offenlassen, ob das Werkzeug jetzt noch benutzbar ist - und genau das ist die
            // einzige Frage, die den Nutzer hier interessiert.
            if (install.Status == ToolInstallStatus.RolledBack)
            {
                FluentMessageBox.Show(
                    Fmt(T("StartupWindow.ToolUpdate.RolledBack"), tool.DisplayName),
                    T("StartupWindow.Title.Error"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning,
                    this);
            }
            else if (install.Status != ToolInstallStatus.Canceled)
            {
                FluentMessageBox.Show(
                    Fmt(T("StartupWindow.ToolUpdate.Failed"), tool.DisplayName),
                    T("StartupWindow.Title.Error"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning,
                    this);
            }

            return (tool.GetStatus().Installed, outcome.LocalVersion);
        }

        /// <summary>Führt die Installation mit Fortschrittsdialog und Statuszeile aus. Die
        /// Abschnittsmeldungen kommen als Aufzählungswert aus der Werkzeugschicht und werden erst
        /// hier zu Text — <c>Progress&lt;T&gt;</c> wird im UI-Thread erzeugt und meldet deshalb
        /// auch dorthin zurück.</summary>
        private async Task<ToolInstallOutcome> RunInstallAsync(
            IManagedTool tool, ReleaseInfo? release, Func<string, string> T)
        {
            string downloadText = Fmt(T("StartupWindow.Status.Downloading"), tool.DisplayName);

            using var dialog = new DownloadProgressDialog(downloadText);
            dialog.Owner = this;
            dialog.Show();

            var progress = new Progress<double>(dialog.SetProgress);
            var stage = new Progress<ToolInstallStage>(s => SetStatus(StageText(s, tool.DisplayName, T)));

            SetStatus(downloadText);

            var outcome = await tool.InstallAsync(release, progress, stage, dialog.CancellationToken);

            if (outcome.Status == ToolInstallStatus.Canceled)
                SetStatus(T("StartupWindow.Status.DownloadCanceled"));
            else if (!outcome.Success)
                SetStatus(T("StartupWindow.Status.DownloadFailed"));

            return outcome;
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
