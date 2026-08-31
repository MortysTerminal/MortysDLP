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

        // Grenzen gegen ZIP-Bomben (02-BEST-PRACTICES.md, Abschnitt 9). Zip-Slip ist hier kein
        // Thema, weil nicht das Archiv in ein Verzeichnis entpackt wird, sondern genau zwei
        // namentlich gesuchte Einträge in einen von MortysDLP bestimmten Zielpfad.
        private const int MaxZipEntries = 10_000;
        private const long MaxExtractedBytes = 500L * 1024 * 1024;
        private const long MaxCompressionRatio = 100;

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

        /// <summary><c>ffmpeg -version</c> (ein Bindestrich, nicht zwei) gibt als erste Zeile
        /// <c>ffmpeg version 7.1-essentials_build-www.gyan.dev Copyright …</c> aus.</summary>
        public override Task<ToolVersion> GetLocalVersionAsync(CancellationToken ct) =>
            ReadVersionAsync(AppPaths.Ffmpeg, ["-version"], ExtractVersionToken, ct);

        /// <summary>Erfolgskontrolle über <b>beide</b> Dateien. Ein Update, nach dem nur ffmpeg
        /// antwortet, ist kein halber Erfolg, sondern ein Fehlschlag: Die Anwendung braucht
        /// ffprobe für jede Analyse.</summary>
        public override async Task<bool> VerifyAsync(CancellationToken ct)
        {
            var ffmpegVersion = await GetLocalVersionAsync(ct);
            if (!ffmpegVersion.HasNumericCore)
            {
                Log.Warn($"[{Id}] Erfolgskontrolle: {FfmpegExeName} meldet keine lesbare Version.");
                return false;
            }

            var ffprobeVersion = await ReadVersionAsync(
                AppPaths.Ffprobe, ["-version"], ExtractVersionToken, ct);

            if (!ffprobeVersion.HasNumericCore)
            {
                Log.Warn($"[{Id}] Erfolgskontrolle: {FfprobeExeName} meldet keine lesbare Version.");
                return false;
            }

            if (!ffmpegVersion.IsSameRelease(ffprobeVersion))
            {
                Log.Warn($"[{Id}] Erfolgskontrolle: {FfmpegExeName} meldet {ffmpegVersion}, " +
                    $"{FfprobeExeName} aber {ffprobeVersion} - die beiden Dateien gehören nicht zusammen.");
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

                var newVersion = await GetLocalVersionAsync(ct);
                Log.Info($"[{Id}] {(hadPrevious ? "aktualisiert" : "installiert")} auf {newVersion}.");

                return new ToolInstallOutcome(
                    hadPrevious ? ToolInstallStatus.Replaced : ToolInstallStatus.Installed,
                    newVersion, replaceResult.Detail);
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
        /// </summary>
        /// <returns>Die Namen der Einträge, die im Archiv nicht gefunden wurden. Leer heißt: alle
        /// da und geschrieben.</returns>
        internal static List<string> ExtractExecutables(
            string zipPath, IReadOnlyList<(string EntryName, string TargetPath)> wanted)
        {
            var missing = new List<string>();

            using var archive = ZipFile.OpenRead(zipPath);

            if (archive.Entries.Count > MaxZipEntries)
            {
                throw new InvalidDataException(
                    $"Das Paket enthält {archive.Entries.Count} Einträge und damit mehr als die " +
                    $"zulässigen {MaxZipEntries} - es wird nicht entpackt.");
            }

            long extractedBudget = MaxExtractedBytes;

            foreach (var (entryName, targetPath) in wanted)
            {
                ZipArchiveEntry? entry = null;
                foreach (var candidate in archive.Entries)
                {
                    if (string.Equals(candidate.Name, entryName, StringComparison.OrdinalIgnoreCase))
                    {
                        entry = candidate;
                        break;
                    }
                }

                if (entry is null)
                {
                    missing.Add(entryName);
                    continue;
                }

                if (entry.Length > extractedBudget)
                {
                    throw new InvalidDataException(
                        $"'{entryName}' würde entpackt {entry.Length} Byte belegen und überschreitet " +
                        "das Gesamtlimit - das Paket wird nicht entpackt.");
                }

                if (entry.CompressedLength > 0 && entry.Length / entry.CompressedLength > MaxCompressionRatio)
                {
                    throw new InvalidDataException(
                        $"'{entryName}' hat ein Kompressionsverhältnis über {MaxCompressionRatio}:1 " +
                        "- das Paket wird nicht entpackt.");
                }

                extractedBudget -= entry.Length;
                entry.ExtractToFile(targetPath, overwrite: true);
            }

            return missing;
        }

        /// <summary>
        /// Zieht aus <c>ffmpeg version 7.1-essentials_build-www.gyan.dev Copyright …</c> das
        /// <c>7.1-essentials_build-www.gyan.dev</c> heraus. Bewusst am Wort <c>version</c>
        /// ausgerichtet und nicht an einer festen Position: Die Zeile beginnt bei ffmpeg mit
        /// <c>ffmpeg</c>, bei ffprobe mit <c>ffprobe</c>, und manche Builds schieben davor noch
        /// etwas ein.
        /// </summary>
        internal static string? ExtractVersionToken(string output)
        {
            string? line = FirstNonEmptyLine(output);
            if (line is null)
                return null;

            const string marker = "version ";
            int index = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                return null;

            string rest = line[(index + marker.Length)..].TrimStart();
            int end = rest.IndexOf(' ');
            string token = end < 0 ? rest : rest[..end];

            return token.Length == 0 ? null : token;
        }
    }
}
