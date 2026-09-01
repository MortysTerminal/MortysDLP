using MortysDLP.Helpers;
using MortysDLP.Models;
using MortysDLP.Services.Releases;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MortysDLP.Services.Tools
{
    /// <param name="Probe">Ergebnis der Befragung des Werkzeugs — enthält auch, <b>warum</b> es
    /// gegebenenfalls nicht brauchbar ist (fehlt, schweigt, oder dort liegt ein fremdes Programm).</param>
    /// <param name="Release">Antwort der Quellenkette, oder <c>null</c>, wenn keine Quelle etwas
    /// hatte. Wird für die Installation gebraucht (Adresse, Größe, Prüfsumme).</param>
    /// <param name="RemoteVersion">Dieselbe Version wie in <paramref name="Release"/>, aber als
    /// <see cref="ToolVersion"/> — dem Begriff, mit dem hier verglichen wird.</param>
    /// <param name="FromCache">Ob die Antwort aus dem Zwischenspeicher kam, also ohne Netzzugriff.</param>
    internal sealed record ToolCheckOutcome(
        IManagedTool Tool,
        ToolStatus Status,
        ToolProbe Probe,
        ReleaseInfo? Release,
        ToolVersion RemoteVersion,
        bool FromCache,
        ToolUpdateVerdict Verdict)
    {
        /// <summary>Installierte Version, soweit sie verlässlich ist.</summary>
        public ToolVersion LocalVersion => Probe.Version;

        /// <summary>true nur, wenn dort tatsächlich dieses Werkzeug liegt und antwortet. Ein
        /// vorhandener Dateiname genügt ausdrücklich nicht.</summary>
        public bool Usable => Probe.Usable;
    }

    /// <summary>
    /// Die Liste der verwalteten Werkzeuge an einer Stelle — und die eine Stelle, an der die
    /// Versionsprüfung eines Werkzeugs stattfindet. Zwei Wege zum selben Ziel nebeneinander sind
    /// die Ursache dafür, dass einer davon nie mitgepflegt wird.
    /// </summary>
    internal sealed class ToolCatalog
    {
        private readonly UpdateCache _cache;
        private readonly Func<DateTimeOffset> _now;

        /// <param name="cache">Gemeinsamer Zwischenspeicher aller Prüflinge. Standard:
        /// <see cref="AppPaths.UpdateCacheFile"/>.</param>
        /// <param name="now">Zeitquelle — für Tests einsetzbar.</param>
        public ToolCatalog(UpdateCache? cache = null, Func<DateTimeOffset>? now = null)
        {
            _cache = cache ?? new UpdateCache();
            _now = now ?? (() => DateTimeOffset.UtcNow);
        }

        /// <summary>
        /// Alle vier verwalteten Werkzeuge. Die Reihenfolge ist die, in der
        /// <c>StartupWindow.ToolUpdaterAsync</c> die für den Betrieb erforderlichen Werkzeuge
        /// behandelt: yt-dlp zuerst, weil ohne yt-dlp kein Download möglich ist und der Nutzer bei
        /// einer Ablehnung dann gar nicht erst nach ffmpeg gefragt werden soll. whisper.cpp und
        /// TwitchDownloaderCLI stehen zwar hier im Katalog (<see cref="IManagedTool.RequiredForOperation"/>
        /// ist bei beiden <c>false</c>), werden aber bewusst <b>nicht</b> automatisch beim Start
        /// geprüft — das würde jeden Nutzer ohne Transkriptions- oder Twitch-Bedarf mit einer
        /// Installationsfrage für ein Werkzeug konfrontieren, das er nie anfasst. Ihre Prüfung
        /// bleibt an den Aufrufzeitpunkten ihrer jeweiligen Seite (<c>TranscribePage</c>/
        /// <c>WhisperModelsWindow</c>, <c>TwitchPage</c>) unverändert.
        ///
        /// <para>Die Whisper-<b>Modelle</b> stehen bewusst nicht hier: Sie haben gar keine Version
        /// und passen absichtlich nicht in <see cref="IManagedTool"/> — ein eigener Fall für eine
        /// künftige Aufgabe.</para>
        /// </summary>
        public static IReadOnlyList<IManagedTool> CreateAll() =>
            [new YtDlpTool(), new FfmpegTool(), new WhisperTool(), new TwitchDownloaderTool()];

        /// <summary>
        /// Ermittelt Zustand, installierte Version, entfernte Version und die Entscheidung
        /// „Update anbieten?" für <b>ein</b> Werkzeug.
        ///
        /// <para><b>Warum <see cref="ToolCheckResult.UpdateAvailable"/> hier nicht verwendet
        /// wird:</b> Diese Angabe vergleicht über <see cref="AppVersion"/>. Für die App ist das
        /// richtig, für ein Werkzeug nicht — bei ffmpeg würde genau dort das dauerhafte
        /// Update-Angebot entstehen. Der Sachverhalt („welche Version nennt die Quelle?") kommt aus
        /// <see cref="ToolCheckService"/>, die Entscheidung trifft
        /// <see cref="ToolUpdateDecision"/> über <see cref="ToolVersion"/>.</para>
        /// </summary>
        /// <param name="force">Zwingt eine Netzabfrage, auch wenn der Zwischenspeicher noch frisch
        /// ist — für „jetzt nach Updates suchen" auf Wunsch des Nutzers.</param>
        public async Task<ToolCheckOutcome> CheckAsync(IManagedTool tool, bool force, CancellationToken ct)
        {
            var status = tool.GetStatus();

            if (!status.Installed)
            {
                Log.Info($"[{tool.Id}] Nicht installiert - es fehlt: " +
                    string.Join(", ", status.MissingPaths));
            }

            // Befragen statt nur nachsehen: Erst hier kommt heraus, ob unter dem erwarteten
            // Dateinamen auch das erwartete Programm liegt.
            var probe = status.Installed ? await tool.ProbeAsync(ct) : ToolProbe.NotInstalled;

            var service = new ToolCheckService(
                new ResilientReleaseResolver(tool.CreateSources()), _cache, _now);

            var result = await service.CheckAsync(
                tool.Id, tool.CreateQuery(), ToAppVersionIfOrdering(probe.Version),
                ToolCheckService.ToolCacheLifetime, force, ct);

            var remote = ToolVersion.Parse(result.Info?.Version.ToString());

            var verdict = probe.Usable
                ? ToolUpdateDecision.Evaluate(probe.Version, remote, tool.UpdatePolicy)
                : new ToolUpdateVerdict(false,
                    $"{ManagedToolBase.DescribeProbe(probe)} - zuständig ist die Installation, " +
                    "nicht der Versionsvergleich");

            Log.Info($"[{tool.Id}] {verdict.Reason}" +
                (result.Info is not null ? $" [Quelle {result.Info.SourceName}" +
                    $"{(result.FromCache ? ", aus Cache" : "")}]" : ""));

            return new ToolCheckOutcome(tool, status, probe, result.Info, remote, result.FromCache, verdict);
        }

        /// <summary>Zeitlimit je Werkzeug innerhalb von <see cref="CheckAllAsync"/> — zusätzlich zu
        /// den Zeitlimits, die <see cref="IManagedTool.ProbeAsync"/> und die Quellenkette bereits
        /// selbst mitbringen. Eine einzelne hängende Prüfung darf trotzdem nicht die gesamte
        /// gleichzeitige Prüfung offenhalten.</summary>
        private static readonly TimeSpan PerToolTimeout = TimeSpan.FromSeconds(25);

        /// <summary>
        /// Prüft mehrere Werkzeuge <b>gleichzeitig</b> statt nacheinander — für die
        /// Hintergrundprüfung nach dem Start und für „Alle prüfen" auf der Seite „Werkzeuge".
        /// Liefert immer genau ein Ergebnis je übergebenem Werkzeug, in derselben Reihenfolge:
        /// Schlägt eine einzelne Prüfung fehl oder überschreitet ihr Zeitlimit, wird das
        /// protokolliert und für dieses eine Werkzeug ein Ergebnis mit
        /// <see cref="ToolHealth.NoAnswer"/> bzw. <see cref="ToolProbe.NotInstalled"/>
        /// zurückgegeben, statt die ganze Prüfung abzubrechen.
        ///
        /// <para>Bewusst kein einfaches <c>await Task.WhenAll(tasks)</c> über
        /// <see cref="CheckAsync"/> direkt: Jede einzelne Aufgabe fängt ihren eigenen Fehler
        /// bereits ab (s. u.), bevor <c>WhenAll</c> überhaupt etwas sehen könnte — sonst würde
        /// bei mehreren gleichzeitigen Fehlschlägen nur der erste sichtbar und die übrigen
        /// stillschweigend verschluckt (02-BEST-PRACTICES.md, Abschnitt 4).</para>
        /// </summary>
        public async Task<IReadOnlyList<ToolCheckOutcome>> CheckAllAsync(
            IReadOnlyList<IManagedTool> tools, bool force, CancellationToken ct)
        {
            var outcomes = await Task.WhenAll(tools.Select(t => CheckWithTimeoutAsync(t, force, ct)));
            return outcomes;
        }

        /// <summary>Eine einzelne Prüfung mit eigenem Zeitlimit, das den Aufrufer selbst nie
        /// mit einer Ausnahme verlässt (außer bei echtem Abbruch über <paramref name="ct"/>) —
        /// das macht <see cref="CheckAllAsync"/> robust, ohne dort jede Aufgabe einzeln
        /// nachträglich auf ihren Zustand prüfen zu müssen.</summary>
        private async Task<ToolCheckOutcome> CheckWithTimeoutAsync(IManagedTool tool, bool force, CancellationToken ct)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linked.CancelAfter(PerToolTimeout);

            try
            {
                return await CheckAsync(tool, force, linked.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                Log.Warn($"[{tool.Id}] Prüfung hat das Zeitlimit " +
                    $"({PerToolTimeout.TotalSeconds:0} s) überschritten.");
                return FailureOutcome(tool);
            }
            catch (Exception ex)
            {
                Log.Warn($"[{tool.Id}] Prüfung fehlgeschlagen: {ex.Message}", ex);
                return FailureOutcome(tool);
            }
        }

        private static ToolCheckOutcome FailureOutcome(IManagedTool tool)
        {
            var status = tool.GetStatus();
            var probe = status.Installed
                ? new ToolProbe(ToolHealth.NoAnswer, ToolVersion.Unknown, null)
                : ToolProbe.NotInstalled;

            return new ToolCheckOutcome(tool, status, probe, Release: null, RemoteVersion: ToolVersion.Unknown,
                FromCache: false, Verdict: new ToolUpdateVerdict(false, "Prüfung fehlgeschlagen oder Zeitlimit überschritten"));
        }

        /// <summary>
        /// Beschafft eine Release-Antwort, die zum <b>Installieren</b> taugt — nicht nur zum
        /// Vergleichen. Ein zwischengespeicherter Stand führt keine Anhangsliste
        /// (<c>UpdateCacheEntry</c> speichert sie nicht), und ohne Anhangsliste gibt es weder die
        /// erwartete Größe noch die Prüfsumme: Der Download eines ausführbaren Programms liefe dann
        /// ungeprüft. Deshalb wird für eine Installation einmal frisch gefragt, wenn der
        /// vorliegende Stand aus dem Zwischenspeicher kommt.
        ///
        /// <para>Das kostet genau eine Netzabfrage, und nur dann, wenn der Nutzer eine
        /// Installation tatsächlich bestätigt hat — nicht bei jedem Start. Der umgekehrte Weg
        /// (Anhänge mitspeichern) wäre eine Schemaänderung am gemeinsamen Zwischenspeicher der
        /// Anwendung und gehört nicht hierher.</para>
        /// </summary>
        public async Task<ReleaseInfo?> ResolveForInstallAsync(ToolCheckOutcome outcome, CancellationToken ct)
        {
            if (outcome.Release is { Assets.Count: > 0 } || !outcome.FromCache)
                return outcome.Release;

            Log.Info($"[{outcome.Tool.Id}] Der zwischengespeicherte Stand führt keine Anhangsliste - " +
                "für die Installation wird einmal frisch geprüft, sonst fehlt die Prüfsumme.");

            var fresh = await CheckAsync(outcome.Tool, force: true, ct);
            return fresh.Release ?? outcome.Release;
        }

        /// <summary>
        /// Summe der tatsächlichen Dateigrößen aller <see cref="IManagedTool.TargetPaths"/>, die
        /// existieren — für die Größenspalte der Seite „Werkzeuge". Zählt bewusst nur die
        /// nachverfolgten Zieldateien, nicht den gesamten Werkzeugordner: Bei whisper.cpp liegen
        /// daneben noch mehrere Laufzeitbibliotheken, die nicht Teil der Erfolgskontrolle sind
        /// (siehe <see cref="WhisperTool.TargetPaths"/>) — die angezeigte Größe unterschätzt den
        /// tatsächlichen Speicherbedarf dieses einen Werkzeugs deshalb bewusst, statt eine eigene,
        /// werkzeugspezifische Ordner-Logik in die Seite zu tragen.
        /// </summary>
        public static long GetInstalledSize(IManagedTool tool)
        {
            long total = 0;

            foreach (string path in tool.TargetPaths)
            {
                try
                {
                    var info = new FileInfo(path);
                    if (info.Exists)
                        total += info.Length;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Log.Warn($"[{tool.Id}] Größe von '{path}' nicht lesbar: {ex.Message}");
                }
            }

            return total;
        }

        /// <summary>
        /// Gibt die installierte Version nur dann als <see cref="AppVersion"/> weiter, wenn sie
        /// ordnend ist. Der Wert dient in der Quellenkette allein der Regel gegen veraltete
        /// Antworten nicht-primärer Quellen — eine nicht ordnende Version dort einzusetzen, würde
        /// dieselbe Falle aufstellen, die <see cref="ToolVersion"/> gerade vermeidet: Ein
        /// <c>7.1-essentials_build-www.gyan.dev</c> gilt für <see cref="AppVersion"/> als
        /// Vorabversion und damit als kleiner als jedes <c>7.1</c>.
        /// <c>null</c> heißt „unbekannt", und dann greift die Regel schlicht nicht.
        /// </summary>
        private static AppVersion? ToAppVersionIfOrdering(ToolVersion local)
        {
            if (!local.IsOrdering)
                return null;

            return AppVersion.TryParse(local.Raw, out var version) ? version : null;
        }
    }
}
