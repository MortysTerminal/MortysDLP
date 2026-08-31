using MortysDLP.Helpers;
using MortysDLP.Models;
using MortysDLP.Services.Releases;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MortysDLP.Services.Tools
{
    /// <summary>
    /// Was für <b>jedes</b> verwaltete Werkzeug gleich ist: die Dateiprüfung ohne Netz, das
    /// Befragen über einen Prozessaufruf samt Beurteilung der Antwort, die Erfolgskontrolle und
    /// das Entfernen. Alles, was sich zwischen yt-dlp und ffmpeg unterscheidet, ist hier abstrakt
    /// und steht in der jeweiligen Klasse — genau die Grenze, an der die Abstraktion sich bewähren
    /// muss.
    ///
    /// <para>Die Klasse existiert nicht nur, um Code zu sparen: Der <b>Identitätsnachweis</b>
    /// (<see cref="IsOwnVersion"/>) ist hier ein Pflichtfeld jedes Werkzeugs und läuft in
    /// <see cref="ProbeAsync(CancellationToken)"/> immer mit. Ein Werkzeug kann ihn nicht
    /// vergessen, und ein neues Werkzeug muss sich dazu äußern, bevor es kompiliert.</para>
    /// </summary>
    internal abstract class ManagedToolBase : IManagedTool
    {
        /// <summary>Zeitlimit für einen reinen Versionsabruf. Ein hängendes Werkzeug darf den
        /// Start nie blockieren (<c>02-BEST-PRACTICES.md</c>, Abschnitt 3).</summary>
        protected static readonly TimeSpan VersionTimeout = TimeSpan.FromSeconds(15);

        /// <summary>Länge, auf die eine fremde Antwort für das Protokoll gekürzt wird.</summary>
        private const int MaxLoggedAnswerLength = 120;

        public abstract string Id { get; }

        public abstract string DisplayName { get; }

        public abstract bool RequiredForOperation { get; }

        public abstract ToolUpdatePolicy UpdatePolicy { get; }

        public abstract IReadOnlyList<string> TargetPaths { get; }

        public abstract IReadOnlyList<IReleaseSource> CreateSources();

        public abstract ReleaseQuery CreateQuery();

        public abstract Task<ToolInstallOutcome> InstallAsync(
            ReleaseInfo? release,
            IProgress<double>? progress,
            IProgress<ToolInstallStage>? stage,
            CancellationToken ct);

        /// <summary>Die Datei, die nach der Version gefragt wird. Bei mehreren Zieldateien die
        /// führende (ffmpeg, nicht ffprobe).</summary>
        protected abstract string VersionExecutable { get; }

        /// <summary>Argumente für die Versionsfrage — <c>--version</c> bei yt-dlp,
        /// <c>-version</c> (ein Bindestrich) bei ffmpeg.</summary>
        protected abstract IReadOnlyList<string> VersionArguments { get; }

        /// <summary>Zieht die Versionsangabe aus der Ausgabe. <c>null</c> heißt hier
        /// ausdrücklich <b>nicht</b> „keine Version gefunden", sondern <b>„diese Ausgabe stammt
        /// nicht von diesem Werkzeug"</b> — die Methode ist die erste Hälfte des
        /// Identitätsnachweises und darf deshalb wählerisch sein.</summary>
        protected abstract string? ExtractVersion(string output);

        /// <summary>Die zweite Hälfte: Passt die gelesene Angabe zum Versionsschema
        /// <i>dieses</i> Werkzeugs? Ohne diese Frage genügt eine beliebige Zahl in der Ausgabe
        /// eines fremden Programms, um als installiertes Werkzeug durchzugehen.</summary>
        protected abstract bool IsOwnVersion(ToolVersion version);

        /// <summary>Vorhanden heißt: <b>alle</b> Zieldateien da und größer als 0 Byte. Die
        /// Größenprüfung ist nicht übertrieben — ein abgebrochener Download der Vorgängerversion
        /// (oder ein leeres Platzhalterexemplar) hinterlässt genau eine 0-Byte-Datei, und die
        /// gilt für <c>File.Exists</c> als installiertes Werkzeug.</summary>
        public ToolStatus GetStatus()
        {
            var missing = new List<string>();

            foreach (string path in TargetPaths)
            {
                try
                {
                    var info = new FileInfo(path);
                    if (!info.Exists || info.Length == 0)
                        missing.Add(path);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Nicht lesbar ist für diese Frage dasselbe wie nicht vorhanden - das
                    // Werkzeug ließe sich so oder so nicht starten.
                    Log.Warn($"[{Id}] Zustand von '{path}' nicht lesbar: {ex.Message}");
                    missing.Add(path);
                }
            }

            return new ToolStatus(Id, missing.Count == 0, missing);
        }

        public Task<ToolProbe> ProbeAsync(CancellationToken ct) =>
            ProbeAsync(VersionExecutable, VersionArguments, ExtractVersion, IsOwnVersion, ct);

        public ToolRemovalResult Uninstall()
        {
            var removed = new List<string>();
            var failed = new List<string>();

            foreach (string path in TargetPaths)
            {
                try
                {
                    if (!File.Exists(path))
                        continue;

                    File.Delete(path);
                    removed.Add(path);
                    Log.Info($"[{Id}] {Path.GetFileName(path)} entfernt.");
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Log.Warn($"[{Id}] {Path.GetFileName(path)} konnte nicht entfernt werden: {ex.Message}", ex);
                    failed.Add(path);
                }
            }

            if (removed.Count == 0 && failed.Count == 0)
                Log.Info($"[{Id}] Nichts zu entfernen - es war nichts installiert.");

            return new ToolRemovalResult(removed, failed);
        }

        /// <summary>
        /// Die Erfolgskontrolle nach einem Ersetzen: Das Werkzeug wird tatsächlich aufgerufen und
        /// muss sich als <i>dieses</i> Werkzeug ausweisen. Werkzeuge mit mehreren Zieldateien
        /// überschreiben das und prüfen jede davon.
        ///
        /// <para>Die Prüfung ist absichtlich nicht „Exit-Code 0" und auch nicht „irgendeine Zahl
        /// in der Ausgabe": Eine abgeschnittene EXE kann mit 0 enden, und ein fremdes Programm
        /// gibt bereitwillig seine eigene Version aus. Was zählt, ist eine Antwort, die zu
        /// diesem Werkzeug passt.</para>
        /// </summary>
        public virtual async Task<bool> VerifyAsync(CancellationToken ct)
        {
            var probe = await ProbeAsync(ct);
            if (probe.Usable)
                return true;

            Log.Warn($"[{Id}] Erfolgskontrolle nicht bestanden: {DescribeProbe(probe)}.");
            return false;
        }

        /// <summary>Beschreibt ein Prüfergebnis in einem Satz — für Protokoll und Bericht.
        /// Nennt bei einer fremden Antwort immer auch, <b>was</b> geantwortet hat; ohne diese
        /// Angabe ist ein „passt nicht" nicht zu klären.</summary>
        public static string DescribeProbe(ToolProbe probe) => probe.Health switch
        {
            ToolHealth.Ok => $"antwortet als Version {probe.Version}",
            ToolHealth.NotInstalled => "nicht installiert",
            ToolHealth.NoAnswer => "vorhanden, aber nicht befragbar (kein Start, Zeitlimit oder Exit-Code ungleich 0)",
            _ => $"vorhanden, antwortet aber nicht wie erwartet (gemeldet: {Quote(probe.Answer)})",
        };

        /// <summary>
        /// Ruft <paramref name="exePath"/> mit <paramref name="args"/> auf und beurteilt die
        /// Antwort in drei Stufen: Hat der Prozess überhaupt geantwortet
        /// (<see cref="ToolHealth.NoAnswer"/>)? Sieht die Ausgabe nach diesem Werkzeug aus
        /// (<paramref name="extract"/>)? Passt die gelesene Angabe zu seinem Versionsschema
        /// (<paramref name="isOwn"/>)? Ein Abbruch über <paramref name="ct"/> geht als
        /// <see cref="OperationCanceledException"/> durch — er ist kein Befund über das Werkzeug.
        /// </summary>
        protected async Task<ToolProbe> ProbeAsync(
            string exePath,
            IReadOnlyList<string> args,
            Func<string, string?> extract,
            Func<ToolVersion, bool> isOwn,
            CancellationToken ct)
        {
            string name = Path.GetFileName(exePath);

            var fileInfo = TryGetFileInfo(exePath);
            if (fileInfo is null || !fileInfo.Exists || fileInfo.Length == 0)
            {
                Log.Info($"[{Id}] {name} ist nicht installiert - keine Version zu lesen.");
                return ToolProbe.NotInstalled;
            }

            ProcessResult result;
            try
            {
                result = await ProcessRunner.RunAsync(
                    exePath, args, timeout: VersionTimeout, workingDirectory: AppPaths.ToolsDir, ct: ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (TimeoutException)
            {
                Log.Warn($"[{Id}] {name} hat das Zeitlimit von {VersionTimeout.TotalSeconds:0} s " +
                    "für die Versionsfrage überschritten.");
                return new ToolProbe(ToolHealth.NoAnswer, ToolVersion.Unknown, null);
            }
            catch (Exception ex)
            {
                Log.Warn($"[{Id}] {name} ließ sich nicht starten: {ex.Message}", ex);
                return new ToolProbe(ToolHealth.NoAnswer, ToolVersion.Unknown, null);
            }

            // Die Ausgabe wird auch im Fehlerfall festgehalten: Bei einem fremden Programm steht
            // dort meist genau, welches es ist.
            string answer = Shorten(FirstNonEmptyLine(result.StdOut) ?? FirstNonEmptyLine(result.StdErr));

            if (!result.Success)
            {
                Log.Warn($"[{Id}] {name} beantwortet die Versionsfrage mit Exit-Code " +
                    $"{result.ExitCode} (Ausgabe: {Quote(answer)}).");
                return new ToolProbe(ToolHealth.NoAnswer, ToolVersion.Unknown, answer);
            }

            string? raw = extract(result.StdOut);
            var version = ToolVersion.Parse(raw);

            if (!version.HasValue || !isOwn(version))
            {
                Log.Warn($"[{Id}] {name} hat geantwortet, aber nicht wie {DisplayName}: " +
                    $"{Quote(answer)}. Die Datei gilt damit als nicht brauchbar - ein Dateiname " +
                    "allein ist kein Nachweis dafür, welches Programm dort liegt.");
                return new ToolProbe(ToolHealth.Foreign, ToolVersion.Unknown, answer);
            }

            Log.Info($"[{Id}] {name} meldet Version {version} " +
                $"({(version.IsOrdering ? "ordnend" : "nicht ordnend")}).");
            return new ToolProbe(ToolHealth.Ok, version, answer);
        }

        /// <summary>Erste nicht leere Zeile der Ausgabe — das übliche Format eines
        /// <c>--version</c>-Aufrufs.</summary>
        protected static string? FirstNonEmptyLine(string output)
        {
            foreach (string line in output.Split('\n'))
            {
                string trimmed = line.Trim();
                if (trimmed.Length > 0)
                    return trimmed;
            }

            return null;
        }

        private static FileInfo? TryGetFileInfo(string path)
        {
            try { return new FileInfo(path); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                return null;
            }
        }

        private static string Shorten(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            string trimmed = text.Trim();
            return trimmed.Length > MaxLoggedAnswerLength
                ? trimmed[..MaxLoggedAnswerLength] + "…"
                : trimmed;
        }

        private static string Quote(string? answer) =>
            string.IsNullOrWhiteSpace(answer) ? "keine Ausgabe" : $"'{answer}'";
    }
}
