using MortysDLP.Helpers;
using MortysDLP.Models;
using MortysDLP.Services.Releases;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MortysDLP.Services.Tools
{
    /// <param name="LocalVersion">Installierte Version. <see cref="ToolVersion.Unknown"/>, wenn
    /// das Werkzeug fehlt oder nicht geantwortet hat.</param>
    /// <param name="Release">Antwort der Quellenkette, oder <c>null</c>, wenn keine Quelle etwas
    /// hatte. Wird für die Installation gebraucht (Adresse, Größe, Prüfsumme).</param>
    /// <param name="RemoteVersion">Dieselbe Version wie in <paramref name="Release"/>, aber als
    /// <see cref="ToolVersion"/> — dem Begriff, mit dem hier verglichen wird.</param>
    /// <param name="FromCache">Ob die Antwort aus dem Zwischenspeicher kam, also ohne Netzzugriff.</param>
    internal sealed record ToolCheckOutcome(
        IManagedTool Tool,
        ToolStatus Status,
        ToolVersion LocalVersion,
        ReleaseInfo? Release,
        ToolVersion RemoteVersion,
        bool FromCache,
        ToolUpdateVerdict Verdict);

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
        /// Alle verwalteten Werkzeuge, in der Reihenfolge, in der sie beim Start behandelt werden:
        /// yt-dlp zuerst, weil ohne yt-dlp kein Download möglich ist und der Nutzer bei einer
        /// Ablehnung dann gar nicht erst nach ffmpeg gefragt werden soll.
        ///
        /// <para>whisper.cpp, TwitchDownloaderCLI und die Whisper-Modelle stehen bewusst noch nicht
        /// hier: Die beiden Werkzeuge folgen in einer eigenen Aufgabe, die Modelle haben gar keine
        /// Version und passen absichtlich nicht in <see cref="IManagedTool"/>.</para>
        /// </summary>
        public static IReadOnlyList<IManagedTool> CreateAll() => [new YtDlpTool(), new FfmpegTool()];

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

            var local = status.Installed ? await tool.GetLocalVersionAsync(ct) : ToolVersion.Unknown;

            var service = new ToolCheckService(
                new ResilientReleaseResolver(tool.CreateSources()), _cache, _now);

            var result = await service.CheckAsync(
                tool.Id, tool.CreateQuery(), ToAppVersionIfOrdering(local),
                ToolCheckService.ToolCacheLifetime, force, ct);

            var remote = ToolVersion.Parse(result.Info?.Version.ToString());

            var verdict = status.Installed
                ? ToolUpdateDecision.Evaluate(local, remote, tool.UpdatePolicy)
                : new ToolUpdateVerdict(false,
                    "nicht installiert - zuständig ist die Installation, nicht der Versionsvergleich");

            Log.Info($"[{tool.Id}] {verdict.Reason}" +
                (result.Info is not null ? $" [Quelle {result.Info.SourceName}" +
                    $"{(result.FromCache ? ", aus Cache" : "")}]" : ""));

            return new ToolCheckOutcome(tool, status, local, result.Info, remote, result.FromCache, verdict);
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
