using MortysDLP.Helpers;
using MortysDLP.Models;
using MortysDLP.Services.Releases;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MortysDLP.Services.Tools
{
    /// <summary>
    /// whisper.cpp — eine EXE (<c>whisper-cli.exe</c>, unter <c>Tools\Whisper\whisper.exe</c>
    /// eingesetzt) mit einem knappen Dutzend Laufzeitbibliotheken aus demselben ZIP. Bewusst nur
    /// der Bau aus dem CPU-BLAS-Anhang, als Entscheidung und nicht als stiller Filter — die
    /// GPU-Variante würde ein zweites, größeres Paket mit anderer Laufzeitumgebung bedeuten und
    /// ist einer künftigen Aufgabe vorbehalten.
    ///
    /// <para><b>Am 2026-09-01 gegen das echte Release geprüft</b> (Download, Entpacken,
    /// Programmstart — nicht angenommen): Das Repository heißt inzwischen
    /// <c>ggml-org/whisper.cpp</c>, nicht mehr <c>ggerganov/whisper.cpp</c> (siehe
    /// <see cref="ReleaseSources.CreateWhisperChain"/>). <c>whisper-cli.exe</c> trägt <b>keine</b>
    /// Versionsressource (anders als yt-dlp, aber wie ffmpeg) — <see cref="TryProbeWithoutProcess"/>
    /// bleibt deshalb bewusst die Basisfassung (kein Programmstart gespart, weil es hier nichts zu
    /// lesen gibt). Der Programmstart selbst antwortet dafür zuverlässig: <c>--version</c> schreibt
    /// genau eine Zeile <c>whisper.cpp version: 1.9.3</c> nach <c>stdout</c> (Diagnosezeilen zum
    /// geladenen Backend gehen nach <c>stderr</c>) und endet mit Exit-Code 0.</para>
    /// </summary>
    internal sealed class WhisperTool : ManagedToolBase
    {
        private const string Owner = "ggml-org";
        private const string Repo = "whisper.cpp";

        /// <summary>Name des Anhangs, wie er am 2026-09-01 im echten Release stand — trägt keine
        /// Versionsnummer, anders als bei TwitchDownloaderCLI.</summary>
        private const string AssetName = "whisper-blas-bin-x64.zip";

        private const string DownloadUrlTemplate =
            "https://github.com/{owner}/{repo}/releases/download/{tag}/" + AssetName;

        /// <summary>Letzte Rückfallebene: ein fest hinterlegtes, geprüftes Paket (Build <c>b5454</c>
        /// = whisper.cpp 1.9.5; Größe und SHA-256 aus der GitHub-API, am 2026-10-26 gegen die
        /// heruntergeladene Datei abgeglichen). Die frühere versionslose Adresse
        /// <c>releases/latest/download/…</c> taugt nicht mehr: <c>latest</c> zeigt seit dem
        /// Schemawechsel auf ein Release ohne Anhänge und antwortet mit <c>404</c>. Diese Adresse
        /// veraltet absichtlich — sie ist kein Update-Weg, sondern sorgt nur dafür, dass eine
        /// Installation auch ohne Antwort der Quellenkette gelingt. Eine neuere Installation wird
        /// darüber nie ersetzt, siehe <see cref="ResolveDownloadCandidates"/>.</summary>
        internal static readonly DownloadCandidate PinnedPackage = new(
            "https://github.com/" + Owner + "/" + Repo + "/releases/download/b5454/" + AssetName,
            21_715_607,
            "b1158bd10edf92c9799149a6fc5d3e58da3f495241d6fb3423088130e88ffbf3");

        /// <summary>Die Version, die <see cref="PinnedPackage"/> installiert.</summary>
        internal const string PinnedVersion = "1.9.5";

        /// <summary>Namen, unter denen das Paket im Lauf der whisper.cpp-Geschichte sein
        /// Hauptprogramm ausliefert. <c>whisper-cli.exe</c> ist der aktuelle Name (2026-09-01
        /// geprüft); <c>main.exe</c> ist die ältere Bezeichnung, mit der auch der Vorgängercode
        /// gearbeitet hat.</summary>
        private static readonly string[] MainExeNames = ["whisper-cli.exe", "main.exe"];

        /// <summary>Erste Zeile von <c>whisper-cli.exe --version</c> — geprüft an der echten,
        /// frisch heruntergeladenen Datei.</summary>
        private const string VersionPrefix = "whisper.cpp version:";

        // Dieselben Grenzen wie bei ffmpeg (02-BEST-PRACTICES.md, Abschnitt 9) - das Paket bringt
        // mit libopenblas.dll allein schon über 50 MB mit.
        private const int MaxZipEntries = 10_000;
        private const long MaxExtractedBytes = 500L * 1024 * 1024;
        private const long MaxCompressionRatio = 100;

        private readonly string[] _targetPaths = [AppPaths.Whisper];

        public override string Id => "whisper";

        public override string DisplayName => "whisper.cpp";

        /// <summary>Whisper ist eine optionale Funktion (Transkription) — anders als yt-dlp und
        /// ffmpeg blockiert ein fehlendes whisper.cpp den Start nicht. Deshalb bewusst nicht in
        /// der Prüfschleife von <c>StartupWindow.ToolUpdaterAsync</c>, obwohl es im Katalog steht
        /// (<see cref="ToolCatalog.CreateAll"/>) — die Installation läuft weiterhin über die
        /// Transkriptions-Seite, ausgelöst vom Nutzer.</summary>
        public override bool RequiredForOperation => false;

        /// <summary>Nur auf Verschiedenheit geprüft: Die installierte Version (<c>1.9.5</c>, aus dem
        /// Programm selbst) und die gemeldete (aus dem <c>vX.Y.Z</c>-Tag, siehe
        /// <see cref="ReleaseSources.CreateWhisperChain"/>) passen heute zusammen, der Anbieter hat
        /// sein Tag-Schema aber schon zweimal geändert — ein ordnender Vergleich würde beim
        /// nächsten Wechsel falsche Schlüsse ziehen. Derselbe Grundsatz wie bei ffmpeg
        /// (<see cref="FfmpegTool"/>).</summary>
        public override ToolUpdatePolicy UpdatePolicy => ToolUpdatePolicy.WhenDifferent;

        // Nur die eine Zieldatei, nicht die Laufzeitbibliotheken daneben: Eine fehlende oder
        // beschädigte DLL macht den Programmstart in ProbeAsync fehlschlagen (ToolHealth.NoAnswer)
        // und damit das Werkzeug als "nicht brauchbar" kenntlich - das ist die Prüfung, die hier
        // zählt. Eine feste Liste aller DLL-Namen wäre brüchig: Das Paket bringt je Release
        // unterschiedliche CPU-Varianten mit (ggml-cpu-*.dll), und diese Liste würde bei jeder
        // Änderung upstream veralten, ohne dass es hier auffiele.
        public override IReadOnlyList<string> TargetPaths => _targetPaths;

        public override IReadOnlyList<IReleaseSource> CreateSources() => ReleaseSources.CreateWhisperChain();

        public override ReleaseQuery CreateQuery() => new(
            Owner, Repo,
            AssetPattern: AssetName,
            DownloadUrlTemplate: DownloadUrlTemplate);

        protected override string VersionExecutable => AppPaths.Whisper;

        protected override IReadOnlyList<string> VersionArguments => ["--version"];

        protected override string? ExtractVersion(string output) => ExtractVersionToken(output);

        protected override bool IsOwnVersion(ToolVersion version) => version.HasNumericCore;

        /// <summary>
        /// Zieht aus <c>whisper.cpp version: 1.9.3</c> das <c>1.9.3</c> heraus. Die Zeile muss mit
        /// <c>whisper.cpp version:</c> beginnen — derselbe Identitätsnachweis wie bei ffmpeg, nur
        /// mit Doppelpunkt statt zweitem Leerzeichen als Trenner.
        /// </summary>
        internal static string? ExtractVersionToken(string output)
        {
            string? line = FirstNonEmptyLine(output);
            if (line is null)
                return null;

            if (!line.StartsWith(VersionPrefix, StringComparison.OrdinalIgnoreCase))
                return null;

            string rest = line[VersionPrefix.Length..].Trim();
            return rest.Length == 0 ? null : rest;
        }

        public override async Task<ToolInstallOutcome> InstallAsync(
            ReleaseInfo? release,
            IProgress<double>? progress,
            IProgress<ToolInstallStage>? stage,
            CancellationToken ct)
        {
            string target = AppPaths.Whisper;
            string? whisperDir = Path.GetDirectoryName(target);
            if (string.IsNullOrEmpty(whisperDir))
                return new ToolInstallOutcome(ToolInstallStatus.Failed, ToolVersion.Unknown,
                    "kein Zielordner für whisper.cpp ermittelbar");

            string stagedExe = target + ToolInstaller.StagedSuffix;

            string tempDir = Path.Combine(Path.GetTempPath(), "MortysDLP");
            string tempZip = Path.Combine(tempDir, $"whisper-{Guid.NewGuid():N}.zip");

            try
            {
                // Im try, damit ein Abbruch während der Versionsprüfung wie jeder andere Abbruch
                // als "abgebrochen" endet und nicht als Ausnahme beim Aufrufer.
                var candidates = ResolveDownloadCandidates(release, await ProbeAsync(ct));
                if (candidates.Count == 0)
                    return new ToolInstallOutcome(ToolInstallStatus.Failed, ToolVersion.Unknown,
                        "keine Download-Adresse ermittelbar");

                Directory.CreateDirectory(whisperDir);
                Directory.CreateDirectory(tempDir);

                stage?.Report(ToolInstallStage.Downloading);
                var verification = await CandidateDownload.FirstReachableAsync(Id, candidates, tempZip, progress, ct);

                Log.Info($"[{Id}] Paket geladen: {verification.Bytes} Byte, " +
                    $"Prüfsumme {(verification.ChecksumChecked ? "geprüft" : "nicht prüfbar")}, " +
                    $"Größe {(verification.SizeChecked ? "abgeglichen" : "nicht abgeglichen")}.");

                stage?.Report(ToolInstallStage.Extracting);

                // ZipFile ist rein rechnend und blockierend - gehört deshalb in Task.Run, sonst
                // friert die Oberfläche für die Dauer des Entpackens ein
                // (02-BEST-PRACTICES.md Abschnitt 4). Das Paket bringt mit libopenblas.dll allein
                // über 50 MB mit; ein synchrones Entpacken war hier am ehesten spürbar.
                bool foundMainExe = await Task.Run(
                    () => ExtractPackage(tempZip, whisperDir, stagedExe), ct);

                if (!foundMainExe)
                {
                    ToolInstaller.DiscardStaged(Id, stagedExe);
                    Log.Error($"[{Id}] Im Paket fehlt das Hauptprogramm " +
                        $"({string.Join(" oder ", MainExeNames)}). Nichts ersetzt.");
                    return new ToolInstallOutcome(ToolInstallStatus.Failed, ToolVersion.Unknown,
                        "Hauptprogramm nicht im Paket gefunden");
                }

                Log.Info($"[{Id}] Programm und Laufzeitbibliotheken aus dem Paket geholt.");

                stage?.Report(ToolInstallStage.Replacing);
                bool hadPrevious = File.Exists(target);

                var replaceResult = await ToolInstaller.ReplaceAllAsync(
                    Id,
                    [new ToolInstaller.Replacement(target, stagedExe)],
                    verification.ChecksumChecked,
                    verifyCt =>
                    {
                        stage?.Report(ToolInstallStage.Verifying);
                        return VerifyAsync(verifyCt);
                    },
                    ct);

                if (!replaceResult.Success)
                {
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
                ToolInstaller.DiscardStaged(Id, stagedExe);
                Log.Info($"[{Id}] Installation vom Nutzer abgebrochen - die vorhandene Datei ist unberührt.");
                return new ToolInstallOutcome(ToolInstallStatus.Canceled, ToolVersion.Unknown, "abgebrochen");
            }
            catch (ChecksumMismatchException ex)
            {
                ToolInstaller.DiscardStaged(Id, stagedExe);
                Log.Error($"[{Id}] Prüfsumme des Pakets stimmt nicht überein. Erwartet: " +
                    $"{ex.Expected}, tatsächlich: {ex.Actual}. Nichts ersetzt.");
                return new ToolInstallOutcome(ToolInstallStatus.Failed, ToolVersion.Unknown,
                    "Prüfsumme des Pakets stimmt nicht überein");
            }
            catch (Exception ex)
            {
                ToolInstaller.DiscardStaged(Id, stagedExe);
                Log.Warn($"[{Id}] Installation fehlgeschlagen: {ex.Message}", ex);
                return new ToolInstallOutcome(ToolInstallStatus.Failed, ToolVersion.Unknown, ex.Message);
            }
            finally
            {
                try { if (File.Exists(tempZip)) File.Delete(tempZip); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Log.Warn($"[{Id}] Temporäres Paket '{tempZip}' konnte nicht gelöscht werden: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Die Adressen, die nacheinander versucht werden: zuerst die aus der Release-Antwort
        /// (falls sie eine nennt), danach <see cref="PinnedPackage"/> — aber nur, wenn das nichts
        /// verschlechtert: whisper.cpp fehlt, antwortet nicht brauchbar, meldet keine lesbare
        /// Version oder ist älter als <see cref="PinnedVersion"/>. Eine neuere Installation wird
        /// nie über die feste Adresse zurückgestuft. Eine Adresse wird nie geraten — ein Release mit
        /// Version, aber ohne Anhang führt direkt zur festen Adresse (oder zu keiner).
        /// </summary>
        internal IReadOnlyList<DownloadCandidate> ResolveDownloadCandidates(ReleaseInfo? release, ToolProbe installed)
        {
            var candidates = new List<DownloadCandidate>(2);

            if (ResolveAsset(release) is { } fromRelease)
                candidates.Add(fromRelease);

            bool alreadyListed = candidates.Any(c => string.Equals(c.Url, PinnedPackage.Url, StringComparison.OrdinalIgnoreCase));
            if (alreadyListed)
                return candidates;

            if (PinnedPackageWouldNotDowngrade(installed))
            {
                if (candidates.Count == 0)
                    Log.Info($"[{Id}] Keine Paketadresse aus der Release-Antwort - es wird das fest " +
                        "hinterlegte, geprüfte Paket geladen.");
                candidates.Add(PinnedPackage);
            }
            else
            {
                Log.Info($"[{Id}] Installiert ist {installed.Version.Raw}, neuer als das fest hinterlegte " +
                    $"Paket ({PinnedVersion}) - es dient deshalb nicht als Rückfall.");
            }

            return candidates;
        }

        private static bool PinnedPackageWouldNotDowngrade(ToolProbe installed) =>
            !installed.Usable ||
            !AppVersion.TryParse(installed.Version.Raw, out var current) ||
            !AppVersion.TryParse(PinnedVersion, out var pinned) ||
            current < pinned;

        /// <summary>Wählt den Anhang aus der Release-Antwort, oder <c>null</c>, wenn sie keinen
        /// nennt. Die Prüfsumme kommt aus der Quelle (GitHub-<c>digest</c>), sofern sie eine
        /// liefert.</summary>
        private DownloadCandidate? ResolveAsset(ReleaseInfo? release)
        {
            if (release is null)
                return null;

            if (release.Assets.Count > 0)
            {
                try
                {
                    if (AssetSelector.Select(release.Assets, AssetName) is { } selected)
                        return new DownloadCandidate(selected.Url, selected.Size > 0 ? selected.Size : null, release.Sha256);
                }
                catch (AssetAmbiguousException ex)
                {
                    Log.Warn($"[{Id}] Anhang nicht eindeutig wählbar: {ex.Message}");
                }
            }

            return string.IsNullOrEmpty(release.DownloadUrl)
                ? null
                : new DownloadCandidate(release.DownloadUrl, release.ExpectedSize, release.Sha256);
        }

        /// <summary>
        /// Entpackt genau die Dateien, die <c>whisper-cli.exe</c> zum Laufen braucht — Programm
        /// und Bibliotheken mit dem Namenspräfix <c>whisper</c>/<c>ggml</c> sowie
        /// <c>libopenblas.dll</c> (die BLAS-Laufzeit, deshalb die bewusste Wahl des BLAS-Anhangs).
        /// Das Paket bringt daneben ein knappes Dutzend fremder Beispielprogramme
        /// und -bibliotheken mit (u. a. <c>llama.dll</c>, <c>SDL2.dll</c>, mehrere
        /// <c>parakeet-*</c>/<c>test-*</c>-Programme, am 2026-09-01 gegen das echte Release
        /// geprüft) — die werden bewusst <b>nicht</b> mit ausgepackt: MortysDLP ruft nur die eine
        /// EXE auf, und ungenutzte Programme im Werkzeugordner sind kein Zusatznutzen.
        /// </summary>
        /// <param name="whisperDir">Zielordner für alles außer dem Hauptprogramm.</param>
        /// <param name="stagedMainExePath">Zielpfad für das umbenannte Hauptprogramm - eine
        /// bereitgestellte Datei, die <see cref="ToolInstaller"/> danach atomar einsetzt.</param>
        /// <returns>true, wenn eines der in <see cref="MainExeNames"/> gesuchten Hauptprogramme
        /// im Paket war.</returns>
        internal static bool ExtractPackage(string zipPath, string whisperDir, string stagedMainExePath)
        {
            using var archive = ZipFile.OpenRead(zipPath);

            if (archive.Entries.Count > MaxZipEntries)
            {
                throw new InvalidDataException(
                    $"Das Paket enthält {archive.Entries.Count} Einträge und damit mehr als die " +
                    $"zulässigen {MaxZipEntries} - es wird nicht entpackt.");
            }

            long extractedBudget = MaxExtractedBytes;
            bool foundMainExe = false;

            foreach (var entry in archive.Entries)
            {
                // Ordnereinträge und alles außer Programmen/Bibliotheken überspringen.
                if (string.IsNullOrEmpty(entry.Name))
                    continue;

                string ext = Path.GetExtension(entry.Name);
                bool isExe = string.Equals(ext, ".exe", StringComparison.OrdinalIgnoreCase);
                bool isDll = string.Equals(ext, ".dll", StringComparison.OrdinalIgnoreCase);
                if (!isExe && !isDll)
                    continue;

                bool isMainExe = IsMainExeName(entry.Name);
                if (!isMainExe && !IsRuntimeDependency(entry.Name))
                    continue;

                if (entry.Length > extractedBudget)
                {
                    throw new InvalidDataException(
                        $"'{entry.Name}' würde entpackt {entry.Length} Byte belegen und " +
                        "überschreitet das Gesamtlimit - das Paket wird nicht entpackt.");
                }

                if (entry.CompressedLength > 0 && entry.Length / entry.CompressedLength > MaxCompressionRatio)
                {
                    throw new InvalidDataException(
                        $"'{entry.Name}' hat ein Kompressionsverhältnis über {MaxCompressionRatio}:1 " +
                        "- das Paket wird nicht entpackt.");
                }

                extractedBudget -= entry.Length;

                string destPath = isMainExe ? stagedMainExePath : Path.Combine(whisperDir, entry.Name);
                entry.ExtractToFile(destPath, overwrite: true);

                if (isMainExe)
                    foundMainExe = true;
            }

            return foundMainExe;
        }

        private static bool IsMainExeName(string fileName)
        {
            foreach (string candidate in MainExeNames)
            {
                if (string.Equals(fileName, candidate, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>Laufzeitbibliotheken und Nebenprogramme, die zum Hauptprogramm gehören - am
        /// echten Paket geprüft: <c>whisper-cli.exe</c> startet mit genau dieser Auswahl (ohne
        /// <c>llama.dll</c>, <c>parakeet.dll</c>, <c>SDL2.dll</c> und die übrigen Beispiele).</summary>
        private static bool IsRuntimeDependency(string fileName) =>
            fileName.StartsWith("whisper", StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith("ggml", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "libopenblas.dll", StringComparison.OrdinalIgnoreCase);
    }
}
