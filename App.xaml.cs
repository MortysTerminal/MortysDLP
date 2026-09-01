using MortysDLP.Helpers;
using MortysDLP.Models;
using MortysDLP.Properties;
using MortysDLP.Services;
using MortysDLP.Services.Releases;
using MortysDLP.UITexte;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace MortysDLP
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Wenn beim Start ein Update gefunden wurde, wird hier die Info hinterlegt.
        /// Das MainWindow liest <c>Version</c>/<c>Changelog</c> für den Update-Banner.
        /// <c>Assets</c>/<c>Sha256</c>/<c>ExpectedSize</c> sind dabei, damit
        /// <see cref="StartUpdateCore"/> das richtige Asset wählen und den Download
        /// verifizieren kann, ohne die Update-Prüfung zu wiederholen. <c>AssetUrl</c> bleibt
        /// der Rückfallwert, wenn <c>Assets</c> leer ist (Atom-Feed, Weiterleitung,
        /// <c>version.json</c> kennen keine Asset-Liste).
        /// </summary>
        internal (string Version, string AssetUrl, string Changelog,
            IReadOnlyList<ReleaseAsset> Assets, string? Sha256, long? ExpectedSize)? PendingUpdateInfo
        { get; private set; }

        /// <summary>Ergebnis der Auswertung eines beim letzten Lauf angestoßenen Updates
        /// — <c>null</c>, wenn keine Zustandsdatei vorlag oder sie unklar/veraltet
        /// war. Das MainWindow zeigt dazu einmalig eine Erfolgs- bzw. Fehlermeldung.
        /// <c>Attempts</c> sagt der Meldung, ob der Schleifenschutz bereits greift (dann bietet
        /// sie „Trotzdem erneut versuchen" an). <c>Changelog</c> ist der Auslöser für den
        /// einmaligen „Was ist neu"-Hinweis — <c>null</c>, wenn er nicht vorliegt
        /// (z. B. beim Rückfall über <c>--updated-from</c> ohne Zustandsdatei).
        /// <c>UpdaterLogPath</c> ist im Fehlerfall der Pfad, in dem der Grund tatsächlich
        /// steht.</summary>
        internal (UpdateOutcome Outcome, string? ToVersion, int Attempts, string? Changelog,
            string? UpdaterLogPath)? PendingUpdateOutcome
        { get; private set; }

        /// <summary>Installationsort-Klassifizierung zum Zeitpunkt des letzten Update-Angebots
        /// — <c>null</c>, solange kein Update angeboten wird. <c>NeedsElevation</c>
        /// lässt den Banner weiterhin erscheinen, aber mit einem Warnhinweis vor dem Download;
        /// <c>ReadOnly</c>/<c>RunningFromArchive</c> führen stattdessen zu
        /// <see cref="BlockedUpdateInfo"/> (siehe <see cref="OnStartup"/>).</summary>
        internal InstallKind? PendingUpdateInstallKind { get; private set; }

        /// <summary>Ein Update, das es zwar gibt, das sich am aktuellen Installationsort aber
        /// nicht installieren lässt — schreibgeschützter Ordner oder Start aus der
        /// ZIP-Vorschau. Das Angebot wird bewusst unterdrückt (ein Download wäre sinnlos),
        /// der Nutzer erfährt aber, <b>dass</b> es eines gibt und <b>warum</b> es hier nicht
        /// geht: Ein Programm, das ohne jede Erklärung nie wieder ein Update anbietet, sieht
        /// aus wie ein Programm, das nicht mehr gepflegt wird.
        /// <c>ReasonKey</c> ist der Textschlüssel aus <see cref="InstallInfo.ReasonKey"/>.</summary>
        internal (string Version, InstallKind Kind, string ReasonKey)? BlockedUpdateInfo
        { get; private set; }

        /*
         * DEBUG
         * */
        private int DebugSleepTimer = 0; // 1000 = 1 Sekunde

        protected override async void OnStartup(StartupEventArgs e)
        {
            // Misst die Zeit bis zum sichtbaren Hauptfenster - die Zeile bleibt dauerhaft im
            // Protokoll, sonst ist eine künftige Regression unsichtbar.
            var startupStopwatch = Stopwatch.StartNew();

            RegisterGlobalExceptionHandlers();

            // Ganz früh, vor dem ersten Lesen einer Einstellung: Seit die AssemblyVersion pro
            // Release wechselt, wechselt auch der user.config-Ordner. Ohne diese
            // Übernahme stünde der Nutzer nach jedem Update vor Standardeinstellungen.
            ApplySettingsUpgradeIfNeeded();

            LogEnvironmentInfo();

            try
            {
                // Nutzerverzeichnisse anlegen und ggf. vorhandenen Verlauf übernehmen -
                // muss vor jedem Zugriff auf AppPaths.HistoryFile passiert sein.
                AppPaths.EnsureDataDirs();

                /* Sprachanpassung bei Window-Start - MUSS VOR ALLEM ANDEREN PASSIEREN */
                LanguageHelper.ApplyLanguage();

                Log.Debug($"Language set to: {UITexte.UITextDictionary.CurrentLanguage}");
                Log.Debug($"CurrentUICulture: {System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName}");

                var splash = new StartupWindow();
                splash.Show();

                // Splash: Logo und Titel (optional)
                // splash.SetLogo("Assets/dein_logo.png");
                // splash.SetTitle("Dein Produktname");

                // 1. Status: Nach Software-Update suchen — höchstens alle 6 Stunden, sonst aus
                // dem Zwischenspeicher. Kein direkter GitHub-Zugriff mehr bei jedem Start.
                // Zuerst: Hat ein beim letzten Lauf angestoßenes Update tatsächlich gewirkt?
                // Ohne diese Prüfung ist ein fehlgeschlagenes Update von einem erfolgreichen
                // nicht zu unterscheiden.
                var previousUpdateState = await EvaluateAndHandlePreviousUpdateAsync();
                ResetVersionSkipIfObsolete();

                // 1b. Werkzeuge aus einem alten Installationsort (vor Welle 4) einmalig
                // übernehmen - muss vor jedem Zugriff auf AppPaths.ToolsDir passiert sein.
                await splash.MigrateToolsAsync();

                // 2. Status: Voraussetzungen prüfen (nur Info, Download im MainWindow)
                await SetStatusTextAndWaitAsync(splash, UITexte.UITexte.Splash_CheckingTools, DebugSleepTimer);

                // Start des ToolUpdaters
                if (await splash.ToolUpdaterAsync())
                {
                    await SetStatusTextAndWaitAsync(splash, UITexte.UITexte.Splash_AllToolsOk, DebugSleepTimer);
                }
                else
                {
                    await SetStatusTextAndWaitAsync(splash, UITexte.UITexte.Splash_ToolsMissing, DebugSleepTimer);
                    splash.Close();
                    Application.Current.Shutdown();
                    return;
                }

                // 3. Splash schließen, MainWindow starten (dort werden Tools ggf. heruntergeladen)
                await SetStatusTextAndWaitAsync(splash, UITexte.UITexte.Splash_StartingApp, DebugSleepTimer);

                await Task.Delay(DebugSleepTimer); // Kurze Pause für den Splashscreen

                var mainWindow = new MainWindow();
                MainWindow = mainWindow;
                mainWindow.ContentRendered += LogWindowVisibleOnce;
                mainWindow.Show();
                mainWindow.Activate();

                void LogWindowVisibleOnce(object? sender, EventArgs e)
                {
                    mainWindow.ContentRendered -= LogWindowVisibleOnce;
                    Log.Info($"Hauptfenster sichtbar nach {startupStopwatch.ElapsedMilliseconds} ms.");
                }

                // 4. Update-Prüfung erst JETZT anstoßen, ohne sie zu erwarten — sie berührt
                // das Netz und darf das Fenster nicht länger aufhalten. Task.Run statt
                // eines einfachen Fire-and-Forget: Ohne ConfigureAwait(false) würden die
                // Fortsetzungen sonst über den UI-Dispatcher laufen (die Methode startet auf dem
                // UI-Thread) und mit dessen eigener Render-/Ereigniswarteschlange konkurrieren -
                // Task.Run schiebt die gesamte Prüfung auf den Threadpool. Eigenes try/catch
                // statt async void: Ein Fehlschlag (z. B. kein Internet) darf den Start nicht
                // stören und bleibt sonst eine unbeobachtete Task-Ausnahme. Sichtbar wird
                // höchstens eine Protokollzeile, nie ein Dialog.
                _ = Task.Run(() => RunBackgroundUpdateCheckAsync(mainWindow, previousUpdateState));

                // Aufräumen der temporären ffmpeg-/Entpack-Artefakte (nicht blockierend, Best-Effort)
                _ = CleanupTempArtifactsAsync();

                splash.Close(); // Splash explizit schließen
            }
            catch (Exception ex)
            {
                Log.Error("Start fehlgeschlagen", ex);
                Views.ErrorDialog.Show(ex, fatal: true);
                Shutdown();
            }
        }

        /// <summary>Registriert die drei globalen Ausnahmebehandler. Muss ganz am Anfang von
        /// <see cref="OnStartup"/> passieren, bevor irgendetwas fehlschlagen kann.</summary>
        private void RegisterGlobalExceptionHandlers()
        {
            DispatcherUnhandledException += (s, e) =>
            {
                Log.Error("Unbehandelte Ausnahme im UI-Thread", e.Exception);
                Views.ErrorDialog.Show(e.Exception, fatal: false);
                e.Handled = true; // Anwendung läuft weiter
            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                // Hier ist die Anwendung nicht mehr zu retten: nur protokollieren, Puffer leeren.
                if (e.ExceptionObject is Exception ex)
                    Log.Error("Unbehandelte Ausnahme (Anwendung wird beendet)", ex);
                else
                    Log.Error($"Unbehandelte Ausnahme (Anwendung wird beendet): {e.ExceptionObject}");
                Log.Flush(TimeSpan.FromSeconds(2));
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                Log.Warn("Unbeobachtete Task-Ausnahme", e.Exception);
                e.SetObserved();
            };
        }

        private static void LogEnvironmentInfo()
        {
            Log.Info($"MortysDLP {AppInfo.Current ?? "unbekannt"} startet " +
                $"(Windows {Environment.OSVersion.Version}, .NET {Environment.Version})");
            Log.Info($"AppDir={AppPaths.AppDir}");
            Log.Info($"DataDir={AppPaths.DataDir}");
            Log.Info($"ToolsDir={AppPaths.ToolsDir}");

            var installInfo = InstallLocation.Analyze();
            Log.Info($"Installationsort: {InstallLocation.DescribeForLog(installInfo)}");
        }

        /// <summary>Übernimmt einmalig Einstellungen der vorherigen Version, sobald sich der
        /// user.config-Ordner mit der AssemblyVersion ändert (siehe
        ///). Das Kennzeichen muss VOR
        /// dem Aufruf von <c>Upgrade()</c> gelesen werden, weil dieser es im Speicher mit dem
        /// Wert der Vorgängerversion überschreibt.</summary>
        private static void ApplySettingsUpgradeIfNeeded()
        {
            try
            {
                if (!Settings.Default.SettingsUpgradeRequired)
                    return;

                string? sourceDir = FindPreviousSettingsDirectory();

                Settings.Default.Upgrade();
                Settings.Default.SettingsUpgradeRequired = false;
                Settings.Default.Save();

                Log.Info(sourceDir != null
                    ? $"Einstellungen aus vorheriger Version übernommen: {sourceDir}"
                    : "Keine Einstellungen einer vorherigen Version gefunden - Standardwerte werden verwendet.");
            }
            catch (Exception ex)
            {
                // Eine defekte user.config darf den Start nicht verhindern - Standardwerte
                // gelten dann einfach weiter.
                Log.Warn("Einstellungen konnten nicht aus einer vorherigen Version übernommen werden", ex);
            }
        }

        /// <summary>Ermittelt best-effort das Verzeichnis, aus dem <c>Settings.Default.Upgrade()</c>
        /// vermutlich übernimmt - für die Protokollzeile, nicht für die Übernahme selbst (die
        /// erledigt .NET). Ohne diese Zeile ist ein „meine Einstellungen sind weg" später nicht
        /// zu klären.</summary>
        private static string? FindPreviousSettingsDirectory()
        {
            try
            {
                string? currentConfigPath = ConfigurationManager
                    .OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath;
                if (string.IsNullOrEmpty(currentConfigPath))
                    return null;

                string? versionDir = Path.GetDirectoryName(currentConfigPath);
                string? hashDir = versionDir != null ? Path.GetDirectoryName(versionDir) : null;
                if (versionDir == null || hashDir == null)
                    return null;

                if (!Version.TryParse(Path.GetFileName(versionDir), out var currentVersion))
                    return null;

                return SettingsUpgradeHelper.FindPreviousVersionDirectory(hashDir, currentVersion);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Trifft die ENTSCHEIDUNG, ob das Update angeboten wird — der Sachverhalt
        /// ("es gibt etwas Neueres") kommt bereits fertig aus <see cref="ToolCheckService"/>.
        /// Die eigentliche Regel (inkl. <c>VersionSkip</c> und Schleifenschutz) steckt in
        /// <see cref="UpdateDecision.ShouldOffer"/>; hier kommt nur noch die Prüfung auf eine
        /// tatsächlich nutzbare Download-Adresse dazu, da <see cref="ToolCheckService"/>
        /// reine Versionsermittlung ist. Protokolliert immer, wenn ein vorhandenes Update nicht
        /// angeboten wird — ohne diese Zeile ist später nicht erklärbar, warum kein Hinweis
        /// erscheint.</summary>
        private static bool ShouldOfferUpdate(ToolCheckResult result, UpdateStateData? previousUpdateState)
        {
            if (result.Info?.DownloadUrl is null || AppInfo.CurrentVersion is not { } current)
                return false;

            bool offer = UpdateDecision.ShouldOffer(
                current, result.Info.Version, Settings.Default.VersionSkip, previousUpdateState);

            if (!offer && result.Info.Version > current)
                Log.Info($"Update {result.Info.Version} verfügbar, aber vom Nutzer übersprungen " +
                    "oder durch den Schleifenschutz blockiert.");

            return offer;
        }

        /// <summary>Ob ein sonst angebotenes Update am aktuellen Installationsort komplett
        /// unterdrückt werden muss. <c>ReadOnly</c> kann nicht schreiben,
        /// <c>RunningFromArchive</c> verliert das Ergebnis beim Schließen — beides macht das
        /// Herunterladen sinnlos. <c>NeedsElevation</c> bleibt bewusst außen vor: Dort wird das
        /// Update weiterhin angeboten, nur mit einem Warnhinweis statt eines direkten
        /// Downloads (siehe <see cref="MainWindow"/>).</summary>
        internal static bool ShouldSuppressUpdateOffer(InstallKind kind) =>
            kind is InstallKind.ReadOnly or InstallKind.RunningFromArchive;

        /// <summary>Prüft im Hintergrund auf eine neue MortysDLP-Version, nachdem das
        /// Hauptfenster bereits sichtbar ist — vorher lief das hier synchron im Startpfad und
        /// hielt das Fenster auf. Läuft bewusst als eigene <c>Task</c> mit eigenem <c>try/catch</c> statt
        /// <c>async void</c>: Ein Fehlschlag (kein Internet, Zeitlimit) darf weder eine
        /// unbeobachtete Task-Ausnahme auslösen noch den Start beeinträchtigen — er erzeugt
        /// höchstens eine Protokollzeile. Das Ergebnis landet über den Dispatcher im Fenster,
        /// weil es auf einem Threadpool-Thread ankommt (<c>Progress&lt;T&gt;</c>-Regel,
        /// <c>02-BEST-PRACTICES.md</c> Abschnitt 8 gilt sinngemäß auch hier).</summary>
        private async Task RunBackgroundUpdateCheckAsync(MainWindow mainWindow, UpdateStateData? previousUpdateState)
        {
            try
            {
                if (AppInfo.CurrentVersion is null)
                {
                    // Die App muss ihre eigene Version immer kennen - anders als bei einem
                    // Werkzeug ist das hier kein Normalfall, sondern ein Zeichen für ein
                    // kaputtes Assembly. Ohne Vergleichswert wäre jede Antwort bedeutungslos.
                    Log.Error("Eigene Version nicht ermittelbar - Update-Prüfung übersprungen.");
                    return;
                }

                var toolCheckService = new ToolCheckService(
                    new ResilientReleaseResolver(ReleaseSources.CreateAppChain()),
                    new UpdateCache(),
                    () => DateTimeOffset.UtcNow);

                var checkResult = await toolCheckService.CheckAsync(
                    AppCacheKey, BuildAppQuery(), AppInfo.CurrentVersion, ToolCheckService.AppCacheLifetime,
                    force: false, CancellationToken.None);

                if (!ShouldOfferUpdate(checkResult, previousUpdateState))
                {
                    Log.Info("Update-Prüfung im Hintergrund abgeschlossen: kein Angebot.");
                    return;
                }

                var info = checkResult.Info!;

                // Vor dem Anbieten prüfen, ob am aktuellen Installationsort überhaupt
                // aktualisiert werden kann (§8) — nicht erst beim Klick auf "Aktualisieren",
                // sonst lädt der Nutzer ein Paket herunter, das er nie installieren kann.
                var installInfo = InstallLocation.Analyze();

                if (ShouldSuppressUpdateOffer(installInfo.Kind))
                {
                    Log.Info($"Update {info.Version} verfügbar, aber am aktuellen " +
                        $"Installationsort nicht anbietbar ({installInfo.Kind}).");
                    BlockedUpdateInfo = (info.Version.ToString(), installInfo.Kind, installInfo.ReasonKey);
                }
                else
                {
                    PendingUpdateInfo = (info.Version.ToString(), info.DownloadUrl!, info.Changelog ?? string.Empty,
                        info.Assets, info.Sha256, info.ExpectedSize);
                    PendingUpdateInstallKind = installInfo.Kind;
                }

                // BeginInvoke liefert eine (awaitbare) DispatcherOperation zurück - bewusst
                // verworfen, das Ergebnis wird hier nicht gebraucht.
                _ = mainWindow.Dispatcher.BeginInvoke(() => mainWindow.ApplyPendingUpdateOffer(this));
            }
            catch (Exception ex)
            {
                Log.Warn("Update-Prüfung im Hintergrund fehlgeschlagen", ex);
            }
        }

        /// <summary>Wertet eine beim letzten Lauf hinterlassene Update-Zustandsdatei aus
        /// und reagiert entsprechend. Liefert den Zustand nur im Fall
        /// <see cref="UpdateOutcome.Failed"/> zurück — genau den braucht
        /// <see cref="UpdateDecision.ShouldOffer"/> für den Schleifenschutz; in jedem anderen
        /// Fall ist die Datei bereits gelöscht und <c>null</c> die richtige Antwort.
        /// <c>update-state.json</c> bleibt dabei die Grundlage — <c>--updated-from</c>
        /// ist nur die Ergänzung für den Fall, dass die Zustandsdatei selbst fehlt.</summary>
        private async Task<UpdateStateData?> EvaluateAndHandlePreviousUpdateAsync()
        {
            string? updatedFrom = TryGetUpdatedFromArgument(Environment.GetCommandLineArgs());

            var state = await UpdateState.ReadAsync();
            var outcome = UpdateState.Evaluate(state, AppInfo.CurrentVersion, DateTimeOffset.UtcNow);

            switch (outcome)
            {
                case UpdateOutcome.None:
                    if (!string.IsNullOrEmpty(updatedFrom))
                    {
                        // Keine Zustandsdatei (z. B. Schreibfehler), aber der Neustart kam
                        // nachweislich vom Updater - ohne diesen Rückfall bliebe der Nutzer
                        // ganz ohne Rückmeldung, obwohl das Update tatsächlich gewirkt hat.
                        // Das Argument allein belegt aber nur "der Updater hat neu gestartet",
                        // nicht "es hat gewirkt": Hat er keine einzige Datei ersetzt, startet er
                        // trotzdem mit --updated-from. Deshalb muss sich die Version geändert
                        // haben, sonst wäre die Erfolgsmeldung eine Falschaussage - und genau
                        // das ist das Fehlerbild, das die Erfolgskontrolle beseitigen soll.
                        if (IsVersionChangeConfirmed(updatedFrom, AppInfo.Current))
                        {
                            Log.Info($"Update erfolgreich (bestätigt über --updated-from={updatedFrom}, " +
                                "keine Zustandsdatei vorhanden).");
                            PendingUpdateOutcome = (UpdateOutcome.Succeeded, AppInfo.Current, 0, null, null);
                            await new UpdateCache().ClearAsync(CancellationToken.None);
                            ClearVersionSkip();
                        }
                        else
                        {
                            Log.Warn($"Neustart über --updated-from={updatedFrom}, aber die laufende " +
                                $"Version ist unverändert ({AppInfo.Current ?? "unbekannt"}) - das Update " +
                                "hat nicht gewirkt. Keine Erfolgsmeldung.");
                        }
                    }
                    return null;

                case UpdateOutcome.Succeeded:
                    Log.Info($"Update erfolgreich: {state!.FromVersion} -> {state.ToVersion}." +
                        (updatedFrom != null ? " (bestätigt über --updated-from)" : ""));
                    PendingUpdateOutcome = (UpdateOutcome.Succeeded, state.ToVersion, state.Attempts,
                        state.Changelog, null);
                    await new UpdateCache().ClearAsync(CancellationToken.None);
                    ClearVersionSkip();
                    await UpdateState.DeleteAsync();
                    return null;

                case UpdateOutcome.Failed:
                    Log.Warn($"Update von {state!.FromVersion} nach {state.ToVersion} hat nicht " +
                        $"gewirkt (Versuch {state.Attempts}). Updater-Protokoll: " +
                        $"{state.UpdaterLogPath ?? "nicht aufgezeichnet"}.");
                    PendingUpdateOutcome = (UpdateOutcome.Failed, state.ToVersion, state.Attempts,
                        null, state.UpdaterLogPath);
                    return state;

                case UpdateOutcome.Stale:
                    Log.Warn($"Update-Zustand ist älter als 7 Tage oder liegt in der Zukunft " +
                        $"({state!.StartedUtc:u}) - wird verworfen.");
                    await UpdateState.DeleteAsync();
                    return null;

                case UpdateOutcome.Unclear:
                default:
                    Log.Warn("Update-Zustand unklar (weder Ziel- noch Ausgangsversion aktiv, " +
                        "oder die eigene Version ist unbekannt) - wird verworfen.");
                    await UpdateState.DeleteAsync();
                    return null;
            }
        }

        /// <summary>
        /// Ob sich die laufende Version tatsächlich von der Version unterscheidet, mit der der
        /// Updater gestartet wurde. Nur dann belegt ein Neustart mit <c>--updated-from</c> einen
        /// Erfolg; sind beide gleich, ist das Update wirkungslos geblieben.
        ///
        /// <para>Der Vergleich läuft über <see cref="AppVersion"/>, wo beide Seiten sich parsen
        /// lassen — sonst würden <c>2026.06.01</c> und <c>v2026.6.1</c> fälschlich als
        /// Versionswechsel gelten. Ist eine der beiden Angaben unbrauchbar, gilt der Wechsel als
        /// <b>nicht</b> belegt: Eine unsichere Erfolgsmeldung ist schlimmer als keine.</para>
        /// </summary>
        internal static bool IsVersionChangeConfirmed(string? updatedFrom, string? currentVersion)
        {
            if (string.IsNullOrWhiteSpace(updatedFrom) || string.IsNullOrWhiteSpace(currentVersion))
                return false;

            if (AppVersion.TryParse(updatedFrom, out var previous) &&
                AppVersion.TryParse(currentVersion, out var current))
                return !previous.Equals(current);

            return !string.Equals(updatedFrom.Trim(), currentVersion.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Sucht <c>--updated-from &lt;version&gt;</c> in den Kommandozeilenargumenten
        /// der App selbst — nicht zu verwechseln mit den Argumenten, die die App dem Updater
        /// übergibt. Reine Zeichenkettenauswertung, ohne Zugriff auf den Prozessstart.</summary>
        internal static string? TryGetUpdatedFromArgument(string[] args)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], "--updated-from", StringComparison.Ordinal))
                    return args[i + 1];
            }
            return null;
        }

        /// <summary>Leert <c>VersionSkip</c>. Best-Effort — ein Fehlschlag hier darf nirgends
        /// etwas verhindern.</summary>
        private static void ClearVersionSkip()
        {
            try
            {
                Settings.Default.VersionSkip = string.Empty;
                Settings.Default.Save();
            }
            catch (Exception ex)
            {
                Log.Warn("VersionSkip konnte nicht zurückgesetzt werden.", ex);
            }
        }

        /// <summary>Setzt <c>VersionSkip</c> zurück, sobald der Wert bedeutungslos geworden
        /// ist: Die übersprungene Version ist inzwischen installiert oder überholt. Ohne dieses
        /// Aufräumen bliebe ein alter Wert stehen, der nichts mehr über die Absicht des Nutzers
        /// aussagt.</summary>
        private static void ResetVersionSkipIfObsolete()
        {
            try
            {
                string? skipped = Settings.Default.VersionSkip;
                if (string.IsNullOrWhiteSpace(skipped))
                    return;

                if (AppInfo.CurrentVersion is not { } current)
                    return;

                if (!AppVersion.TryParse(skipped, out var skippedVersion))
                    return;

                if (current >= skippedVersion)
                {
                    ClearVersionSkip();
                    Log.Info($"VersionSkip zurückgesetzt (übersprungene Version {skippedVersion} " +
                        "ist installiert oder überholt).");
                }
            }
            catch (Exception ex)
            {
                Log.Warn("VersionSkip konnte nicht zurückgesetzt werden.", ex);
            }
        }

        /// <summary>Namensmuster für das App-Update-Paket. Platzhalterfrei ergibt es
        /// <c>"MortysDLP.zip"</c> — genau der Name, den <see cref="AssetSelector"/> bei
        /// mehreren Treffern bevorzugt (siehe dort).</summary>
        private const string MainAssetPattern = "MortysDLP*.zip";

        // Die App ist seit der Verallgemeinerung auf ToolCheckService nur noch ein Aufrufer
        // unter mehreren - diese vier Werte waren zuvor feste Konstanten im (entfernten)
        // UpdateCheckService, jetzt baut BuildAppQuery() daraus die eigene Anfrage.
        private const string AppCacheKey = "app";
        private const string AppOwner = "MortysTerminal";
        private const string AppRepo = "MortysDLP";

        // Deterministische GitHub-Konvention für die Adresse eines Release-Anhangs.
        // Übergangslösung: Für die beiden GitHub-API-Quellen bleibt ReleaseInfo.DownloadUrl
        // bewusst null (Assets werden dort nur befüllt, nicht ausgewertet) - ohne diese Vorlage
        // hätte "Jetzt aktualisieren" im Normalfall (API antwortet zuerst) nichts zum
        // Herunterladen.
        private const string AppDownloadUrlTemplate =
            "https://github.com/{owner}/{repo}/releases/download/{tag}/MortysDLP.zip";

        private static ReleaseQuery BuildAppQuery() =>
            new(AppOwner, AppRepo, DownloadUrlTemplate: AppDownloadUrlTemplate);

        public async Task StartUpdate()
        {
            if (!ConfirmActiveWorkOrCancel())
                return;

            // PendingUpdateInfo nutzen statt die Update-Prüfung zu wiederholen.
            if (PendingUpdateInfo is not { } info || string.IsNullOrEmpty(info.AssetUrl))
            {
                // Fallback: kein PendingUpdateInfo vorhanden (z. B. MainWindow ohne
                // vorangegangenen Startpfad) - erneut über dieselbe Ausweichkette prüfen,
                // erzwungen statt aus dem Zwischenspeicher.
                var toolCheckService = new ToolCheckService(
                    new ResilientReleaseResolver(ReleaseSources.CreateAppChain()),
                    new UpdateCache(),
                    () => DateTimeOffset.UtcNow);
                var result = await toolCheckService.CheckAsync(
                    AppCacheKey, BuildAppQuery(), AppInfo.CurrentVersion, ToolCheckService.AppCacheLifetime,
                    force: true, CancellationToken.None);

                if (result.Info?.DownloadUrl is not { } assetUrl)
                {
                    MessageBox.Show(
                        UITexte.UITexte.Error_UpdateNotAvailable,
                        UITexte.UITexte.Error,
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                await StartUpdateCore(result.Info.Version.ToString(), assetUrl, result.Info.Assets,
                    result.Info.Sha256, result.Info.ExpectedSize, result.Info.Changelog ?? string.Empty);
                return;
            }

            await StartUpdateCore(info.Version, info.AssetUrl, info.Assets, info.Sha256, info.ExpectedSize, info.Changelog);
        }

        /// <summary>Fragt nach, wenn gerade ein Download, eine Konvertierung oder eine
        /// Transkription läuft — der Updater fährt die Anwendung sonst mitten in
        /// laufender Arbeit herunter, ohne Vorwarnung. Liefert <c>false</c>, wenn der Nutzer
        /// das Update daraufhin abbricht; in diesem Fall bleibt alles unverändert.</summary>
        private bool ConfirmActiveWorkOrCancel()
        {
            if (MainWindow is not MainWindow mainWindow)
                return true;

            var busy = ActiveWorkHelper.FindBusy(mainWindow.ActiveWorkSources);
            if (busy.Count == 0)
                return true;

            var labelList = new List<string>(busy.Count);
            foreach (var work in busy)
                labelList.Add(work.BusyLabel);

            var T = UITextDictionary.Get;
            string labels = string.Join(", ", labelList);
            string message = T("Update.Confirm.ActiveWork").Replace("{0}", labels, StringComparison.Ordinal);

            var result = FluentMessageBox.Show(
                message,
                T("Update.Confirm.ActiveWork.Title"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                owner: mainWindow);

            if (result != MessageBoxResult.Yes)
            {
                Log.Info($"Update abgebrochen: Nutzer wollte laufende Vorgänge nicht beenden ({labels}).");
                return false;
            }

            foreach (var work in busy)
                work.RequestCancel();

            Log.Info($"Update fortgesetzt, laufende Vorgänge abgebrochen: {labels}.");
            return true;
        }

        /// <summary>Sucht das gemeinte Anhang in <paramref name="assets"/> (leer bei Quellen
        /// ohne Asset-Information — dann bleibt <paramref name="fallbackAssetUrl"/> gültig),
        /// beschafft dazu wenn möglich die Prüfsumme aus <c>checksums.txt</c> im Release und
        /// lädt erst danach herunter. <paramref name="knownSha256"/>/<paramref name="knownSize"/>
        /// kommen von <c>version.json</c>, falls diese Quelle geantwortet hat, und haben
        /// Vorrang vor <c>checksums.txt</c> nur, wenn beide vorhanden wären — praktisch
        /// schließen sie sich aus, da nur eine Quelle je Prüfung antwortet.
        /// <paramref name="toVersion"/> wird für den Update-Zustand gebraucht, nicht
        /// für die Auswahl selbst.</summary>
        private async Task StartUpdateCore(string toVersion,
            string fallbackAssetUrl, IReadOnlyList<ReleaseAsset> assets, string? knownSha256, long? knownSize,
            string changelog)
        {
            try
            {
                string assetUrl = fallbackAssetUrl;
                string? expectedSha256 = knownSha256;
                long? expectedSize = knownSize;

                if (assets.Count > 0)
                {
                    ReleaseAsset? selected;
                    try
                    {
                        selected = AssetSelector.Select(assets, MainAssetPattern);
                    }
                    catch (AssetAmbiguousException ex)
                    {
                        string candidateNames = string.Join(", ", ex.CandidateNames);
                        Log.Error($"Mehrere passende Update-Pakete gefunden: {candidateNames}");
                        MessageBox.Show(
                            UITexte.UITextDictionary.Get("Update.Error.AssetAmbiguous")
                                .Replace("{0}", candidateNames, StringComparison.Ordinal),
                            UITexte.UITexte.Error, MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    if (selected is null)
                    {
                        MessageBox.Show(
                            UITexte.UITextDictionary.Get("Update.Error.AssetNotFound"),
                            UITexte.UITexte.Error, MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    assetUrl = selected.Url;
                    if (expectedSize is null && selected.Size > 0)
                        expectedSize = selected.Size;
                    if (string.IsNullOrEmpty(expectedSha256))
                        expectedSha256 = await ReleaseChecksums.TryFetchAsync(
                            assets, "checksums.txt", selected.Name, "", CancellationToken.None);
                }

                if (string.IsNullOrEmpty(assetUrl))
                {
                    MessageBox.Show(
                        UITexte.UITexte.Error_UpdateNotAvailable,
                        UITexte.UITexte.Error, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 1. Sicheren Temp-Pfad ermitteln (mit Fallback-Verzeichnissen)
                string tempDir = UpdateService.GetSafeTempDirectory();
                string tempZipPath = Path.Combine(tempDir, Settings.Default.MortysDLPUpdateZipFile);

                // 2. Download mit Fortschrittsdialog, Retry, streamender Prüfsumme und
                // Größenabgleich - erst nach bestandener Prüfung trägt die Datei
                // ihren endgültigen Namen. using: der Dialog wird in jedem Fall geschlossen,
                // auch bei Erfolg. Dass er nicht-modal ist, bleibt bewusst unangetastet.
                using (var dialog = new DownloadProgressDialog(UITexte.UITextDictionary.Get("Update.Download.InProgress")))
                {
                    dialog.Owner = MainWindow;
                    dialog.Show();
                    var progress = new Progress<double>(dialog.SetProgress);

                    try
                    {
                        var verification = await VerifiedDownload.ToFileAsync(
                            assetUrl, tempZipPath, expectedSha256, expectedSize, progress, dialog.CancellationToken);

                        if (!verification.ChecksumChecked)
                            Log.Warn(UITexte.UITextDictionary.Get("Update.Warning.NoChecksum"));
                    }
                    catch (OperationCanceledException)
                    {
                        // Vom Nutzer über den Dialog abgebrochen - kein Fehler. Ein liegen
                        // gebliebenes .part hat VerifiedDownload bereits selbst entfernt; die
                        // Installation wurde an keiner Stelle berührt.
                        Log.Info("Update-Download vom Nutzer abgebrochen.");
                        return;
                    }
                    catch (ChecksumMismatchException ex)
                    {
                        Log.Error($"Update-Prüfsumme stimmt nicht überein. Erwartet: {ex.Expected}, " +
                            $"tatsächlich: {ex.Actual}");
                        MessageBox.Show(
                            UITexte.UITextDictionary.Get("Update.Error.ChecksumMismatch"),
                            UITexte.UITexte.Error, MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                // 3. ZIP-Grundprüfung: enthält den erwarteten Haupteintrag, nicht nur
                // "irgendeine.exe"
                if (!UpdateService.ValidateZipContainsMainExe(tempZipPath, Settings.Default.MortysDLPExeFile))
                {
                    try { if (File.Exists(tempZipPath)) File.Delete(tempZipPath); } catch { }
                    MessageBox.Show(
                        UITexte.UITexte.Error_UpdateZipCorrupt,
                        UITexte.UITexte.Error,
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 4. Updater in Temp kopieren
                string sourceUpdaterDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Settings.Default.MortysDLPUpdaterBaseFolderName);
                if (!Directory.Exists(sourceUpdaterDir))
                {
                    MessageBox.Show(
                        UITexte.UITexte.Error_UpdaterNotFound,
                        UITexte.UITexte.Error,
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string tempUpdaterDir = Path.Combine(tempDir, Settings.Default.MortysDLPUpdaterFolderName);
                CopyDirectory(sourceUpdaterDir, tempUpdaterDir);

                // 5. Benannte Argumente statt Position - der neue
                // Updater versteht ausschließlich --zip/--target/--exe/--pid/
                // --version/--log und lehnt alles andere mit Exit-Code 2 ab.
                string mainExeName = Settings.Default.MortysDLPExeFile;
                int currentPid = Environment.ProcessId;
                string targetDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string updaterLogPath = Path.Combine(
                    AppPaths.LogsDir, $"updater-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.log");

                string updaterExePath = Path.Combine(tempUpdaterDir, Settings.Default.MortysDLPUpdateExeFile);
                Log.Info($"Starte Updater: {updaterExePath}");
                Log.Info($"Ziel={targetDir}, Version={toVersion}, Pid={currentPid}, " +
                    $"Updater-Protokoll={updaterLogPath}");

                // 5b. Update-Zustand aufzeichnen - unmittelbar vor dem Start des
                // Updaters, nach bestandener Prüfsumme/ZIP-Prüfung. Das ist der einzige Beleg,
                // den der nächste Start hat, um ein gewirktes von einem wirkungslosen Update zu
                // unterscheiden - der Exit-Code des heutigen Updaters ist dafür nicht nutzbar.
                // Der Changelog-Text wandert mit, damit der "Was ist neu"-Hinweis nach
                // dem Neustart keinen zweiten Netzabruf braucht.
                if (AppInfo.Current is { } fromVersion)
                    await UpdateState.RecordAttemptAsync(fromVersion, toVersion, DateTimeOffset.UtcNow,
                        changelog: changelog, updaterLogPath: updaterLogPath);

                // 6. Updater starten – UseShellExecute = true für unabhängigen Prozess, der MortysDLP
                // überlebt. Läuft deshalb bewusst außerhalb von ProcessRunner (das immer
                // UseShellExecute=false und Streams umleitet) — ArgumentList schließt trotzdem die
                // Argument-Einschleusung aus, auch wenn diese Werte nicht von außen kommen.
                var psi = new ProcessStartInfo { FileName = updaterExePath, UseShellExecute = true };
                psi.ArgumentList.Add("--zip");
                psi.ArgumentList.Add(tempZipPath);
                psi.ArgumentList.Add("--target");
                psi.ArgumentList.Add(targetDir);
                psi.ArgumentList.Add("--exe");
                psi.ArgumentList.Add(mainExeName);
                psi.ArgumentList.Add("--pid");
                psi.ArgumentList.Add(currentPid.ToString(System.Globalization.CultureInfo.InvariantCulture));
                psi.ArgumentList.Add("--version");
                psi.ArgumentList.Add(toVersion);
                psi.ArgumentList.Add("--log");
                psi.ArgumentList.Add(updaterLogPath);
                var updaterProcess = Process.Start(psi);

                if (updaterProcess == null)
                {
                    MessageBox.Show(
                        "Der Updater-Prozess konnte nicht gestartet werden.",
                        UITexte.UITexte.Error,
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                Log.Info($"Updater gestartet (PID: {updaterProcess.Id}). Beende Hauptanwendung...");

                // 7. VersionSkip zurücksetzen - nach diesem Neustart ist ein übersprungener
                // Wert ohnehin überholt (siehe auch ResetVersionSkipIfObsolete für den
                // umgekehrten Fall: eine übersprungene Version, die inzwischen ohne dieses
                // Update installiert wurde, z. B. durch eine andere Quelle).
                ClearVersionSkip();

                // 8. App sicher beenden – Shutdown + Environment.Exit als Fallback
                Shutdown();
                // Kurze Verzögerung, damit Shutdown-Events verarbeitet werden
                await Task.Delay(500);
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                Log.Error("Update fehlgeschlagen", ex);
                MessageBox.Show(
                    string.Format(UITexte.UITexte.Error_UpdateFailed, ex.Message),
                    UITexte.UITexte.Error,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task SetStatusTextAndWaitAsync(StartupWindow windowWithText, string statusText, int delay)
        {
            windowWithText.SetStatus(statusText);
            if (delay > 0)
            {
                await Task.Delay(delay);
            }
        }

        public static void CopyDirectory(string sourceDir, string targetDir)
        {
            if (!Directory.Exists(sourceDir))
                throw new DirectoryNotFoundException(
                    string.Format(UITexte.UITexte.Error_DirectoryNotFound, sourceDir));

            Directory.CreateDirectory(targetDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(targetDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }
            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                string destDir = Path.Combine(targetDir, Path.GetFileName(dir));
                CopyDirectory(dir, destDir);
            }
        }

        private static Task CleanupTempArtifactsAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    string tempDir = Path.GetTempPath();

                    foreach (var file in Directory.EnumerateFiles(tempDir, "ffmpeg_download_*.zip", SearchOption.TopDirectoryOnly))
                    {
                        string name = Path.GetFileName(file);
                        if (FfmpegZipRegex().IsMatch(name))
                        {
                            TryDeleteFile(file);
                        }
                    }

                    foreach (var dir in Directory.EnumerateDirectories(tempDir, "extract_*", SearchOption.TopDirectoryOnly))
                    {
                        string name = Path.GetFileName(dir);
                        if (ExtractDirRegex().IsMatch(name))
                        {
                            TryDeleteDirectory(dir);
                        }
                    }
                }
                catch
                {
                    // Best-effort: Keine Exception nach außen
                }
            });
        }

        [GeneratedRegex(@"^ffmpeg_download_[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\.zip$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex FfmpegZipRegex();

        [GeneratedRegex(@"^extract_[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex ExtractDirRegex();

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    try { File.SetAttributes(path, FileAttributes.Normal); } catch { }
                    File.Delete(path);
                }
            }
            catch { }
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    ClearReadOnlyAttributes(path);
                    Directory.Delete(path, true);
                }
            }
            catch { }
        }

        private static void ClearReadOnlyAttributes(string rootDir)
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(rootDir, "*", SearchOption.AllDirectories))
                {
                    try { File.SetAttributes(file, FileAttributes.Normal); } catch { }
                }
            }
            catch { }
        }
    }
}
