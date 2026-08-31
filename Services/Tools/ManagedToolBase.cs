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
    /// Auslesen der Version über einen Prozessaufruf, die Erfolgskontrolle und das Entfernen.
    /// Alles, was sich zwischen yt-dlp und ffmpeg unterscheidet, ist hier abstrakt und steht in
    /// der jeweiligen Klasse — genau die Grenze, an der die Abstraktion sich bewähren muss.
    ///
    /// <para>Die Klasse existiert, damit die Werkzeuge diese Schritte nicht je einmal führen.
    /// Dreimal derselbe Prozessstart mit leicht abweichendem Verhalten ist das Muster, das dieses
    /// Projekt an anderer Stelle schon teuer bezahlt hat.</para>
    /// </summary>
    internal abstract class ManagedToolBase : IManagedTool
    {
        /// <summary>Zeitlimit für einen reinen Versionsabruf. Ein hängendes Werkzeug darf den
        /// Start nie blockieren (<c>02-BEST-PRACTICES.md</c>, Abschnitt 3).</summary>
        protected static readonly TimeSpan VersionTimeout = TimeSpan.FromSeconds(15);

        public abstract string Id { get; }

        public abstract string DisplayName { get; }

        public abstract bool RequiredForOperation { get; }

        public abstract ToolUpdatePolicy UpdatePolicy { get; }

        public abstract IReadOnlyList<string> TargetPaths { get; }

        public abstract IReadOnlyList<IReleaseSource> CreateSources();

        public abstract ReleaseQuery CreateQuery();

        public abstract Task<ToolVersion> GetLocalVersionAsync(CancellationToken ct);

        public abstract Task<ToolInstallOutcome> InstallAsync(
            ReleaseInfo? release,
            IProgress<double>? progress,
            IProgress<ToolInstallStage>? stage,
            CancellationToken ct);

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

            return new ToolStatus(Id, missing.Count == 0, ToolVersion.Unknown, missing);
        }

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
        /// muss eine Version melden, die einen Zahlenkern hat. Standardmäßig reicht dafür
        /// <see cref="GetLocalVersionAsync"/>; Werkzeuge mit mehreren Zieldateien überschreiben
        /// das und prüfen jede davon.
        ///
        /// <para>Die Prüfung ist absichtlich nicht „Exit-Code 0" allein: Eine abgeschnittene oder
        /// mit dem falschen Inhalt geschriebene EXE kann durchaus mit 0 enden und trotzdem nichts
        /// Brauchbares ausgeben. Was zählt, ist eine <i>lesbare Version</i>.</para>
        /// </summary>
        public virtual async Task<bool> VerifyAsync(CancellationToken ct)
        {
            var version = await GetLocalVersionAsync(ct);
            if (version.HasNumericCore)
                return true;

            Log.Warn($"[{Id}] Erfolgskontrolle: keine lesbare Version " +
                $"(gemeldet: {(version.HasValue ? $"'{version.Raw}'" : "nichts")}).");
            return false;
        }

        /// <summary>
        /// Ruft <paramref name="exePath"/> mit <paramref name="args"/> auf und lässt
        /// <paramref name="extract"/> aus der Standardausgabe die Versionsangabe herausziehen.
        /// Liefert <see cref="ToolVersion.Unknown"/> und eine Protokollzeile, wenn die Datei fehlt,
        /// der Aufruf das Zeitlimit überschreitet, der Exit-Code nicht 0 ist oder die Ausgabe
        /// nichts Brauchbares enthält. Ein Abbruch über <paramref name="ct"/> geht als
        /// <see cref="OperationCanceledException"/> durch — er ist kein Fehler des Werkzeugs.
        /// </summary>
        protected async Task<ToolVersion> ReadVersionAsync(
            string exePath, IReadOnlyList<string> args, Func<string, string?> extract, CancellationToken ct)
        {
            string name = Path.GetFileName(exePath);

            if (!File.Exists(exePath))
            {
                Log.Info($"[{Id}] {name} ist nicht installiert - keine Version zu lesen.");
                return ToolVersion.Unknown;
            }

            try
            {
                var result = await ProcessRunner.RunAsync(
                    exePath, args, timeout: VersionTimeout, workingDirectory: AppPaths.ToolsDir, ct: ct);

                if (!result.Success)
                {
                    Log.Warn($"[{Id}] {name} beantwortet die Versionsfrage mit Exit-Code " +
                        $"{result.ExitCode} - Version gilt als unbekannt.");
                    return ToolVersion.Unknown;
                }

                string? raw = extract(result.StdOut);
                var version = ToolVersion.Parse(raw);

                if (!version.HasValue)
                {
                    Log.Warn($"[{Id}] {name} hat geantwortet, aber ohne lesbare Version - " +
                        "Version gilt als unbekannt.");
                    return ToolVersion.Unknown;
                }

                Log.Info($"[{Id}] {name} meldet Version {version} " +
                    $"({(version.IsOrdering ? "ordnend" : "nicht ordnend")}).");
                return version;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (TimeoutException)
            {
                Log.Warn($"[{Id}] {name} hat das Zeitlimit von {VersionTimeout.TotalSeconds:0} s " +
                    "für die Versionsfrage überschritten - Version gilt als unbekannt.");
                return ToolVersion.Unknown;
            }
            catch (Exception ex)
            {
                Log.Warn($"[{Id}] {name} konnte nicht nach seiner Version gefragt werden: {ex.Message}", ex);
                return ToolVersion.Unknown;
            }
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
    }
}
