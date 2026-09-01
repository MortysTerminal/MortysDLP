using MortysDLP.Helpers;
using MortysDLP.Models;
using MortysDLP.Services.Releases;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace MortysDLP.Services.Tools
{
    /// <summary>
    /// ffmpeg und ffprobe — bewusst <b>ein</b> verwaltetes Werkzeug mit zwei Zieldateien, nicht
    /// zwei Werkzeuge. Sie kommen aus demselben ZIP, tragen dieselbe Version und sind einzeln
    /// nutzlos: Ein neues ffmpeg neben einem alten ffprobe ist ein Zustand, den niemand geprüft
    /// hat, und die Anwendung braucht ohnehin beide.
    ///
    /// <para>Der unbequeme Fall dieser Abstraktion. Drei Dinge sind hier anders als bei yt-dlp:
    /// Die Version ist <b>nicht ordnend</b> (<c>7.1-essentials_build-www.gyan.dev</c>), das Paket
    /// ist ein <b>ZIP</b> mit zwei herauszuholenden Dateien, und es gibt <b>keinen</b>
    /// Selbst-Update-Notausgang. Die Update-Politik ist deshalb
    /// <see cref="ToolUpdatePolicy.WhenDifferent"/>: anbieten, sobald es eine andere Ausgabe gibt —
    /// und nie erzwingen. ffmpeg ist die Komponente, bei der ein unnötiges Update am meisten
    /// kaputtmachen kann.</para>
    /// </summary>
    internal sealed class FfmpegTool : ManagedToolBase
    {
        private const string FfmpegExeName = "ffmpeg.exe";
        private const string FfprobeExeName = "ffprobe.exe";

        /// <summary>Der Versionsendpunkt des Anbieters: eine Textdatei, deren gesamter Inhalt die
        /// Versionsnummer des Pakets ist, das unter <see cref="PackageUrl"/> liegt.</summary>
        private const string VersionUrl = "https://www.gyan.dev/ffmpeg/builds/release-version";

        /// <summary>So viele nicht leere Zeilen werden nach der Versionszeile durchsucht. Genug
        /// für einen Build, der eine Warnung voranstellt — zu wenig, um in der langen
        /// Konfigurationsausgabe von ffmpeg zufällig etwas Passendes zu finden.</summary>
        private const int MaxScannedLines = 5;

        private readonly string[] _targetPaths = [AppPaths.Ffmpeg, AppPaths.Ffprobe];

        public override string Id => "ffmpeg";

        public override string DisplayName => "ffmpeg / ffprobe";

        public override bool RequiredForOperation => true;

        public override ToolUpdatePolicy UpdatePolicy => ToolUpdatePolicy.WhenDifferent;

        public override IReadOnlyList<string> TargetPaths => _targetPaths;

        public override IReadOnlyList<IReleaseSource> CreateSources() => ReleaseSources.CreateFfmpegChain();

        /// <summary>
        /// Owner/Repo sind bei <see cref="ReleaseQuery"/> Pflichtfelder, weil die GitHub-Quellen
        /// sie brauchen. In dieser Kette steckt keine GitHub-Quelle — die Werte dienen hier
        /// ausschließlich der Zuordnung in Protokollzeilen und werden von
        /// <see cref="PlainTextVersionSource"/> nicht gelesen. Eine Download-Vorlage gibt es
        /// bewusst nicht: Die Paketadresse ist fest und trägt keine Version (siehe
        /// <see cref="PackageUrl"/>).
        /// </summary>
        public override ReleaseQuery CreateQuery() => new(
            "gyan.dev", "ffmpeg-builds",
            PlainTextVersionUrl: VersionUrl);

        /// <summary>
        /// Die feste Paketadresse aus <c>Properties/Resources.resx</c>. Sie zeigt <b>immer</b> auf
        /// die aktuelle Ausgabe und enthält deshalb keine Versionsnummer — der Grund, warum die
        /// Version über einen eigenen Endpunkt kommen muss und nicht aus dem Dateinamen ablesbar
        /// ist. Zugleich der Grund, warum ein fehlendes ffmpeg auch ohne Antwort der
        /// Versionsquelle installierbar bleibt.
        /// </summary>
        private static string PackageUrl => Properties.Resources.URL_FFMPEG;

        protected override string VersionExecutable => AppPaths.Ffmpeg;

        /// <summary>Ein Bindestrich, nicht zwei — ffmpeg kennt <c>--version</c> nicht.</summary>
        protected override IReadOnlyList<string> VersionArguments => ["-version"];

        // Kein TryProbeWithoutProcess: Die ausgelieferten Builds tragen überhaupt keine
        // Versionsressource - am 2026-08-31 geprüft, ProductName und FileVersion sind bei
        // ffmpeg.exe und ffprobe.exe leer. Es fehlt hier also nichts, es ist nichts da. Nötig ist
        // es auch nicht: Als natives Programm antwortet ffmpeg in 51-67 ms, während yt-dlp für
        // dieselbe Frage rund 3,7 Sekunden braucht.

        protected override string? ExtractVersion(string output) =>
            ExtractVersionToken(output, "ffmpeg");

        /// <summary>Die Version trägt die Build-Bezeichnung des Anbieters mit und ist deshalb
        /// nicht ordnend — verlangt wird nur ein Zahlenkern. Den Identitätsnachweis leistet hier
        /// <see cref="ExtractVersionToken"/>: Es muss <c>ffmpeg version …</c> in der ersten Zeile
        /// stehen.</summary>
        protected override bool IsOwnVersion(ToolVersion version) => version.HasNumericCore;

        /// <summary>Erfolgskontrolle über <b>beide</b> Dateien. Ein Update, nach dem nur ffmpeg
        /// antwortet, ist kein halber Erfolg, sondern ein Fehlschlag: Die Anwendung braucht
        /// ffprobe für jede Analyse. Zusätzlich müssen beide dieselbe Ausgabe melden — sonst
        /// stammen sie aus verschiedenen Paketen.</summary>
        public override async Task<bool> VerifyAsync(CancellationToken ct)
        {
            var ffmpegProbe = await ProbeAsync(ct);
            if (!ffmpegProbe.Usable)
            {
                Log.Warn($"[{Id}] Erfolgskontrolle: {FfmpegExeName} {DescribeProbe(ffmpegProbe)}.");
                return false;
            }

            var ffprobeProbe = await ProbeAsync(
                AppPaths.Ffprobe,
                VersionArguments,
                output => ExtractVersionToken(output, "ffprobe"),
                IsOwnVersion,
                ct);

            if (!ffprobeProbe.Usable)
            {
                Log.Warn($"[{Id}] Erfolgskontrolle: {FfprobeExeName} {DescribeProbe(ffprobeProbe)}.");
                return false;
            }

            if (!ffmpegProbe.Version.IsSameRelease(ffprobeProbe.Version))
            {
                Log.Warn($"[{Id}] Erfolgskontrolle: {FfmpegExeName} meldet {ffmpegProbe.Version}, " +
                    $"{FfprobeExeName} aber {ffprobeProbe.Version} - die beiden Dateien gehören " +
                    "nicht zusammen.");
                return false;
            }

            return true;
        }

        public override async Task<ToolInstallOutcome> InstallAsync(
            ReleaseInfo? release,
            IProgress<double>? progress,
            IProgress<ToolInstallStage>? stage,
            CancellationToken ct)
        {
            string ffmpegTarget = AppPaths.Ffmpeg;
            string ffprobeTarget = AppPaths.Ffprobe;
            string ffmpegStaged = ffmpegTarget + ToolInstaller.StagedSuffix;
            string ffprobeStaged = ffprobeTarget + ToolInstaller.StagedSuffix;

            // release.DownloadUrl bleibt bei dieser Kette null (der Textendpunkt kennt kein
            // Paket) - die Zeile steht trotzdem hier, damit eine künftige Quelle mit eigener
            // Paketadresse nicht übersehen wird.
            string packageUrl = release?.DownloadUrl ?? PackageUrl;

            string tempDir = Path.Combine(Path.GetTempPath(), "MortysDLP");
            string tempZip = Path.Combine(tempDir, $"ffmpeg-{Guid.NewGuid():N}.zip");

            try
            {
                Directory.CreateDirectory(AppPaths.ToolsDir);
                Directory.CreateDirectory(tempDir);

                stage?.Report(ToolInstallStage.Downloading);
                Log.Info($"[{Id}] Lade Paket von {packageUrl}.");

                var verification = await VerifiedDownload.ToFileAsync(
                    packageUrl, tempZip, release?.Sha256, release?.ExpectedSize, progress, ct);

                Log.Info($"[{Id}] Paket geladen: {verification.Bytes} Byte, " +
                    $"Prüfsumme {(verification.ChecksumChecked ? "geprüft" : "nicht prüfbar")}, " +
                    $"Größe {(verification.SizeChecked ? "abgeglichen" : "nicht abgeglichen")}.");

                stage?.Report(ToolInstallStage.Extracting);

                // ZipFile ist rein rechnend und blockierend - gehört deshalb in Task.Run, sonst
                // friert die Oberfläche samt Ladeanimation für die Dauer des Entpackens ein
                // (02-BEST-PRACTICES.md, Abschnitt 4).
                var missing = await Task.Run(() => ExtractExecutables(
                    tempZip,
                    [(FfmpegExeName, ffmpegStaged), (FfprobeExeName, ffprobeStaged)]), ct);

                if (missing.Count > 0)
                {
                    ToolInstaller.DiscardStaged(Id, ffmpegStaged);
                    ToolInstaller.DiscardStaged(Id, ffprobeStaged);
                    Log.Error($"[{Id}] Im Paket fehlt: {string.Join(", ", missing)}. Nichts ersetzt.");
                    return new ToolInstallOutcome(ToolInstallStatus.Failed, ToolVersion.Unknown,
                        $"im Paket fehlt: {string.Join(", ", missing)}");
                }

                Log.Info($"[{Id}] {FfmpegExeName} und {FfprobeExeName} aus dem Paket geholt.");

                stage?.Report(ToolInstallStage.Replacing);
                bool hadPrevious = File.Exists(ffmpegTarget) || File.Exists(ffprobeTarget);

                var replaceResult = await ToolInstaller.ReplaceAllAsync(
                    Id,
                    [
                        new ToolInstaller.Replacement(ffmpegTarget, ffmpegStaged),
                        new ToolInstaller.Replacement(ffprobeTarget, ffprobeStaged),
                    ],
                    verification.ChecksumChecked,
                    verifyCt =>
                    {
                        stage?.Report(ToolInstallStage.Verifying);
                        return VerifyAsync(verifyCt);
                    },
                    ct);

                if (!replaceResult.Success)
                {
                    ToolInstaller.DiscardStaged(Id, ffmpegStaged);
                    ToolInstaller.DiscardStaged(Id, ffprobeStaged);
                    return new ToolInstallOutcome(
                        replaceResult.RolledBack ? ToolInstallStatus.RolledBack : ToolInstallStatus.Failed,
                        ToolVersion.Unknown,
                        replaceResult.Detail);
                }

                var probe = await ProbeAsync(ct);
                Log.Info($"[{Id}] {(hadPrevious ? "aktualisiert" : "installiert")} auf {probe.Version}.");

                return new ToolInstallOutcome(
                    hadPrevious ? ToolInstallStatus.Replaced : ToolInstallStatus.Installed,
                    probe.Version, replaceResult.Detail);
            }
            catch (OperationCanceledException)
            {
                ToolInstaller.DiscardStaged(Id, ffmpegStaged);
                ToolInstaller.DiscardStaged(Id, ffprobeStaged);
                Log.Info($"[{Id}] Installation vom Nutzer abgebrochen - die vorhandenen Dateien sind unberührt.");
                return new ToolInstallOutcome(ToolInstallStatus.Canceled, ToolVersion.Unknown, "abgebrochen");
            }
            catch (ChecksumMismatchException ex)
            {
                ToolInstaller.DiscardStaged(Id, ffmpegStaged);
                ToolInstaller.DiscardStaged(Id, ffprobeStaged);
                Log.Error($"[{Id}] Prüfsumme des Pakets stimmt nicht überein. Erwartet: " +
                    $"{ex.Expected}, tatsächlich: {ex.Actual}. Nichts ersetzt.");
                return new ToolInstallOutcome(ToolInstallStatus.Failed, ToolVersion.Unknown,
                    "Prüfsumme des Pakets stimmt nicht überein");
            }
            catch (Exception ex)
            {
                ToolInstaller.DiscardStaged(Id, ffmpegStaged);
                ToolInstaller.DiscardStaged(Id, ffprobeStaged);
                Log.Warn($"[{Id}] Installation fehlgeschlagen: {ex.Message}", ex);
                return new ToolInstallOutcome(ToolInstallStatus.Failed, ToolVersion.Unknown, ex.Message);
            }
            finally
            {
                // Das Paket ist mehrere Dutzend MB groß - es liegen zu lassen wäre ein
                // Speicherleck auf der Platte, unabhängig davon, ob ein späteres Aufräumen es
                // ohnehin einsammeln würde.
                try { if (File.Exists(tempZip)) File.Delete(tempZip); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Log.Warn($"[{Id}] Temporäres Paket '{tempZip}' konnte nicht gelöscht werden: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Holt genau die gesuchten Einträge aus dem Archiv — verglichen wird
        /// <see cref="ZipArchiveEntry.Name"/> (der reine Dateiname), weil das Paket die Dateien in
        /// einem versionsbenannten Unterordner führt (<c>ffmpeg-7.1-essentials_build/bin/</c>) und
        /// dieser Ordnername sich mit jeder Ausgabe ändert.
        ///
        /// <para>Dünne Hülle um <see cref="ZipPackageExtractor.ExtractNamedEntries"/> — die
        /// eigentliche Prüfung (Zip-Bomben-Grenzen) liegt jetzt dort, gemeinsam mit
        /// TwitchDownloaderCLI. Bleibt als eigene Methode stehen, damit
        /// <c>FfmpegToolTests</c> unverändert gegen <c>FfmpegTool</c> testet.</para>
        /// </summary>
        /// <returns>Die Namen der Einträge, die im Archiv nicht gefunden wurden. Leer heißt: alle
        /// da und geschrieben.</returns>
        internal static List<string> ExtractExecutables(
            string zipPath, IReadOnlyList<(string EntryName, string TargetPath)> wanted) =>
            ZipPackageExtractor.ExtractNamedEntries(zipPath, wanted);

        /// <summary>
        /// Zieht aus <c>ffmpeg version 7.1-essentials_build-www.gyan.dev Copyright …</c> das
        /// <c>7.1-essentials_build-www.gyan.dev</c> heraus.
        ///
        /// <para><paramref name="expectedProgram"/> ist gleichzeitig der Identitätsnachweis: Die
        /// Zeile muss <b>mit</b> <c>&lt;programm&gt; version </c> beginnen. Ein bloßes Vorkommen
        /// des Namens irgendwo in der Zeile genügt ausdrücklich nicht — jedes Werkzeug der
        /// ffmpeg-Familie schließt dieselbe Zeile mit <c>the FFmpeg developers</c> ab, auch
        /// ffprobe. Mit einem Enthalten-Test hätte ffprobe als ffmpeg durchgehen können.</para>
        ///
        /// <para>Gesucht wird in den ersten <see cref="MaxScannedLines"/> Zeilen, nicht nur in der
        /// ersten: Ein Build, der eine Warnung voranstellt, soll nicht als fremdes Programm
        /// gelten. Die Verwechslungsgefahr bleibt dabei aus, weil die Zeile mit dem Namen
        /// <i>beginnen</i> muss.</para>
        /// </summary>
        internal static string? ExtractVersionToken(string output, string expectedProgram)
        {
            string prefix = expectedProgram + " version ";
            int scanned = 0;

            foreach (string rawLine in output.Split('\n'))
            {
                string line = rawLine.Trim();
                if (line.Length == 0)
                    continue;

                if (++scanned > MaxScannedLines)
                    return null;

                if (!line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                string rest = line[prefix.Length..].TrimStart();
                int end = rest.IndexOf(' ');
                string token = end < 0 ? rest : rest[..end];

                return token.Length == 0 ? null : token;
            }

            return null;
        }
    }
}
