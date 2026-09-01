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
    /// TwitchDownloaderCLI — eine einzelne EXE, ordnende Version (<c>1.56.5</c>), aus
    /// <c>lay295/TwitchDownloader</c>. Der einfachste der vier Fälle: eine Zieldatei, ein Paket mit
    /// genau einem gesuchten Eintrag, kein Selbst-Update-Notausgang.
    ///
    /// <para><b>Am 2026-09-01 gegen das echte Release geprüft</b> (nicht angenommen): Der Anhang
    /// heißt <c>TwitchDownloaderCLI-{tag}-Windows-x64.zip</c> — anders als bei yt-dlp und
    /// whisper.cpp trägt der Dateiname hier die Version, deshalb ein Muster mit Platzhalter statt
    /// eines festen Namens, und deshalb gibt es <b>keine</b> versionslose Rückfalladresse wie
    /// <see cref="YtDlpTool"/>s <c>LatestAssetUrl</c>: Ohne eine geantwortete Quelle lässt sich der
    /// Dateiname nicht bilden. Das Release trägt außerdem keinen Prüfsummen-Anhang.</para>
    ///
    /// <para>Die installierte Datei trägt eine Versionsressource (<c>ProductName</c> und
    /// <c>FileDescription</c> beide <c>TwitchDownloaderCLI</c>, <c>FileVersion</c>
    /// <c>1.56.5.0</c>) — genau wie yt-dlp lohnt sich deshalb <see cref="TryProbeWithoutProcess"/>.
    /// Der Prozessweg bleibt als Rückfall bestehen, hat dabei aber eine eigene Unschärfe: Die
    /// echte EXE beendet <c>--version</c> mit Exit-Code 1, obwohl die Ausgabe stimmt — im
    /// Normalfall ohne Wirkung, weil die Versionsressource zuerst greift.</para>
    /// </summary>
    internal sealed class TwitchDownloaderTool : ManagedToolBase
    {
        private const string Owner = "lay295";
        private const string Repo = "TwitchDownloader";
        private const string ExeName = "TwitchDownloaderCLI.exe";

        private const string AssetPattern = "TwitchDownloaderCLI-*-Windows-x64.zip";

        private const string DownloadUrlTemplate =
            "https://github.com/{owner}/{repo}/releases/download/{tag}/TwitchDownloaderCLI-{tag}-Windows-x64.zip";

        /// <summary>Namen, unter denen sich TwitchDownloaderCLI in seiner Versionsressource
        /// ausweist — am 2026-09-01 an der ausgelieferten Datei geprüft.</summary>
        private static readonly string[] ProductNames = ["TwitchDownloaderCLI"];

        /// <summary>Erste Zeile von <c>TwitchDownloaderCLI --version</c>, am 2026-09-01 an der
        /// ausgelieferten Datei geprüft: <c>TwitchDownloaderCLI 1.56.5+f8335cab…</c>.</summary>
        private const string VersionPrefix = "TwitchDownloaderCLI ";

        private readonly string[] _targetPaths = [AppPaths.TwitchCli];

        public override string Id => "twitch-downloader";

        public override string DisplayName => "TwitchDownloaderCLI";

        /// <summary>Optionale Funktion (Twitch-Chat-Download) — ein fehlendes
        /// TwitchDownloaderCLI blockiert den Start nicht, siehe <see cref="WhisperTool.RequiredForOperation"/>
        /// für dieselbe Begründung.</summary>
        public override bool RequiredForOperation => false;

        public override ToolUpdatePolicy UpdatePolicy => ToolUpdatePolicy.OnlyWhenNewer;

        public override IReadOnlyList<string> TargetPaths => _targetPaths;

        public override IReadOnlyList<IReleaseSource> CreateSources() => ReleaseSources.CreateTwitchDownloaderChain();

        public override ReleaseQuery CreateQuery() => new(
            Owner, Repo,
            AssetPattern: AssetPattern,
            DownloadUrlTemplate: DownloadUrlTemplate);

        protected override string VersionExecutable => AppPaths.TwitchCli;

        protected override IReadOnlyList<string> VersionArguments => ["--version"];

        /// <summary>Derselbe Weg wie bei yt-dlp: Version und Identität kommen ohne Programmstart
        /// aus der Versionsressource. Bei TwitchDownloaderCLI ist das kein Geschwindigkeitsgewinn
        /// von mehreren Sekunden wie bei yt-dlp (die EXE ist ein gewöhnliches natives Programm),
        /// umgeht dafür aber den oben beschriebenen Exit-Code-Fund des Prozesswegs.</summary>
        protected override ToolProbe? TryProbeWithoutProcess() =>
            ProbeFromVersionResource(AppPaths.TwitchCli, ProductNames, IsOwnVersion);

        protected override string? ExtractVersion(string output) => ExtractVersionToken(output);

        protected override bool IsOwnVersion(ToolVersion version) => version.HasNumericCore;

        /// <summary>
        /// Zieht aus <c>TwitchDownloaderCLI 1.56.5+f8335cab…</c> das <c>1.56.5+f8335cab…</c>
        /// heraus. Die Zeile muss mit <c>TwitchDownloaderCLI </c> beginnen — der
        /// Identitätsnachweis, dieselbe Bauart wie bei yt-dlp und ffmpeg.
        /// </summary>
        internal static string? ExtractVersionToken(string output)
        {
            string? line = FirstNonEmptyLine(output);
            if (line is null)
                return null;

            if (!line.StartsWith(VersionPrefix, StringComparison.OrdinalIgnoreCase))
                return null;

            string rest = line[VersionPrefix.Length..].TrimStart();
            return rest.Length == 0 ? null : rest;
        }

        public override async Task<ToolInstallOutcome> InstallAsync(
            ReleaseInfo? release,
            IProgress<double>? progress,
            IProgress<ToolInstallStage>? stage,
            CancellationToken ct)
        {
            string target = AppPaths.TwitchCli;
            string staged = target + ToolInstaller.StagedSuffix;

            var asset = ResolveAsset(release);
            if (asset.Url is null)
            {
                Log.Warn($"[{Id}] Keine Download-Adresse ermittelbar - alle Quellen haben " +
                    "geschwiegen und der Dateiname trägt die Version, es gibt hier also keine " +
                    "versionslose Rückfalladresse.");
                return new ToolInstallOutcome(ToolInstallStatus.Failed, ToolVersion.Unknown,
                    "keine Download-Adresse ermittelbar");
            }

            try
            {
                Directory.CreateDirectory(AppPaths.ToolsDir);

                stage?.Report(ToolInstallStage.Downloading);
                Log.Info($"[{Id}] Lade Paket von {asset.Url} " +
                    $"({(asset.Sha256 is null ? "ohne bekannte Prüfsumme - dieser Anhang hat keine" : "mit Prüfsumme")}).");

                string tempDir = Path.Combine(Path.GetTempPath(), "MortysDLP");
                Directory.CreateDirectory(tempDir);
                string tempZip = Path.Combine(tempDir, $"twitch-downloader-{Guid.NewGuid():N}.zip");
                bool checksumChecked;

                try
                {
                    var verification = await VerifiedDownload.ToFileAsync(
                        asset.Url, tempZip, asset.Sha256, asset.Size, progress, ct);
                    checksumChecked = verification.ChecksumChecked;

                    Log.Info($"[{Id}] Paket geladen: {verification.Bytes} Byte, " +
                        $"Prüfsumme {(verification.ChecksumChecked ? "geprüft" : "nicht prüfbar")}, " +
                        $"Größe {(verification.SizeChecked ? "abgeglichen" : "nicht abgeglichen")}.");

                    stage?.Report(ToolInstallStage.Extracting);

                    // ZipFile ist rein rechnend und blockierend - gehört deshalb in Task.Run
                    // (02-BEST-PRACTICES.md, Abschnitt 4), auch wenn hier nur eine ~65-MB-Datei
                    // entpackt wird.
                    var missing = await Task.Run(() => ZipPackageExtractor.ExtractNamedEntries(
                        tempZip, [(ExeName, staged)]), ct);

                    if (missing.Count > 0)
                    {
                        ToolInstaller.DiscardStaged(Id, staged);
                        Log.Error($"[{Id}] Im Paket fehlt: {string.Join(", ", missing)}. Nichts ersetzt.");
                        return new ToolInstallOutcome(ToolInstallStatus.Failed, ToolVersion.Unknown,
                            $"im Paket fehlt: {string.Join(", ", missing)}");
                    }

                    Log.Info($"[{Id}] {ExeName} aus dem Paket geholt.");
                }
                finally
                {
                    try { if (File.Exists(tempZip)) File.Delete(tempZip); }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        Log.Warn($"[{Id}] Temporäres Paket '{tempZip}' konnte nicht gelöscht werden: {ex.Message}");
                    }
                }

                stage?.Report(ToolInstallStage.Replacing);
                bool hadPrevious = File.Exists(target);

                var replaceResult = await ToolInstaller.ReplaceAllAsync(
                    Id,
                    [new ToolInstaller.Replacement(target, staged)],
                    checksumChecked,
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
                ToolInstaller.DiscardStaged(Id, staged);
                Log.Info($"[{Id}] Installation vom Nutzer abgebrochen - die vorhandene Datei ist unberührt.");
                return new ToolInstallOutcome(ToolInstallStatus.Canceled, ToolVersion.Unknown, "abgebrochen");
            }
            catch (ChecksumMismatchException ex)
            {
                ToolInstaller.DiscardStaged(Id, staged);
                Log.Error($"[{Id}] Prüfsumme des Downloads stimmt nicht überein. Erwartet: " +
                    $"{ex.Expected}, tatsächlich: {ex.Actual}. Nichts ersetzt.");
                return new ToolInstallOutcome(ToolInstallStatus.Failed, ToolVersion.Unknown,
                    "Prüfsumme des Downloads stimmt nicht überein");
            }
            catch (Exception ex)
            {
                ToolInstaller.DiscardStaged(Id, staged);
                Log.Warn($"[{Id}] Installation fehlgeschlagen: {ex.Message}", ex);
                return new ToolInstallOutcome(ToolInstallStatus.Failed, ToolVersion.Unknown, ex.Message);
            }
        }

        /// <summary>Wählt den Anhang. Anders als bei yt-dlp und whisper.cpp gibt es hier
        /// <b>keine</b> versionslose Rückfalladresse: Der Dateiname trägt die Version
        /// (<c>TwitchDownloaderCLI-1.56.5-Windows-x64.zip</c>), und ohne eine geantwortete Quelle
        /// lässt sich dieser Name nicht bilden.</summary>
        private (string? Url, long? Size, string? Sha256) ResolveAsset(ReleaseInfo? release)
        {
            if (release is null)
                return (null, null, null);

            if (release.Assets.Count > 0)
            {
                try
                {
                    if (AssetSelector.Select(release.Assets, AssetPattern) is { } selected)
                        return (selected.Url, selected.Size > 0 ? selected.Size : null, release.Sha256);
                }
                catch (AssetAmbiguousException ex)
                {
                    Log.Warn($"[{Id}] Anhang nicht eindeutig wählbar: {ex.Message}");
                }
            }

            if (!string.IsNullOrEmpty(release.DownloadUrl))
                return (release.DownloadUrl, release.ExpectedSize, release.Sha256);

            return (null, null, null);
        }
    }
}
