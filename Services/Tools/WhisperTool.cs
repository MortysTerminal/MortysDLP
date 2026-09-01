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

        /// <summary>Rückfalladresse ohne Versionsangabe. Bei whisper.cpp nicht der Ausnahmefall,
        /// sondern heute der <b>einzig erreichbare</b> Weg, siehe
        /// <see cref="ReleaseSources.CreateWhisperChain"/>: Keine der dortigen Quellen liefert
        /// aktuell eine brauchbare Version.</summary>
        private const string LatestAssetUrl =
            "https://github.com/" + Owner + "/" + Repo + "/releases/latest/download/" + AssetName;

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

        /// <summary>Nicht ordnend vergleichbar: Die installierte Version (<c>1.9.3</c>, aus dem
        /// Programm selbst) und die von der Quellenkette gemeldete (aktuell keine, siehe
        /// <see cref="ReleaseSources.CreateWhisperChain"/>) stammen aus unterschiedlichen
        /// Zählweisen desselben Anbieters — derselbe Grundsatz wie bei ffmpeg
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

            var asset = ResolveAsset(release);
            if (asset.Url is null)
                return new ToolInstallOutcome(ToolInstallStatus.Failed, ToolVersion.Unknown,
                    "keine Download-Adresse ermittelbar");

            string tempDir = Path.Combine(Path.GetTempPath(), "MortysDLP");
            string tempZip = Path.Combine(tempDir, $"whisper-{Guid.NewGuid():N}.zip");

            try
            {
                Directory.CreateDirectory(whisperDir);
                Directory.CreateDirectory(tempDir);

                stage?.Report(ToolInstallStage.Downloading);
                Log.Info($"[{Id}] Lade Paket von {asset.Url} " +
                    $"({(asset.Sha256 is null ? "ohne bekannte Prüfsumme - dieser Anhang hat keine" : "mit Prüfsumme")}).");

                var verification = await VerifiedDownload.ToFileAsync(
                    asset.Url, tempZip, asset.Sha256, asset.Size, progress, ct);

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

        /// <summary>Wählt den Anhang. Anders als bei yt-dlp gibt es hier keinen Anhang mit
        /// Prüfsummen (am 2026-09-01 gegen das echte Release geprüft) - <see cref="ReleaseAsset.Size"/>
        /// bleibt deshalb die einzige Kontrolle neben der reinen Erreichbarkeit.</summary>
        private (string? Url, long? Size, string? Sha256) ResolveAsset(ReleaseInfo? release)
        {
            if (release is null)
            {
                Log.Info($"[{Id}] Keine Release-Antwort - es wird über die feste Adresse der " +
                    "neuesten Ausgabe geladen.");
                return (LatestAssetUrl, null, null);
            }

            if (release.Assets.Count > 0)
            {
                try
                {
                    if (AssetSelector.Select(release.Assets, AssetName) is { } selected)
                        return (selected.Url, selected.Size > 0 ? selected.Size : null, release.Sha256);
                }
                catch (AssetAmbiguousException ex)
                {
                    Log.Warn($"[{Id}] Anhang nicht eindeutig wählbar: {ex.Message}");
                }
            }

            if (!string.IsNullOrEmpty(release.DownloadUrl))
                return (release.DownloadUrl, release.ExpectedSize, release.Sha256);

            Log.Info($"[{Id}] Release {release.Version} nennt keinen Anhang - es wird über die " +
                "feste Adresse der neuesten Ausgabe geladen.");
            return (LatestAssetUrl, null, null);
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
