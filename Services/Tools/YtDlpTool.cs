using MortysDLP.Helpers;
using MortysDLP.Models;
using MortysDLP.Services.Releases;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MortysDLP.Services.Tools
{
    /// <summary>
    /// yt-dlp: eine einzelne EXE, eine ordnende Version (<c>2026.08.19</c>), vier
    /// Metadatenquellen und als einziges Werkzeug ein eigener Notausgang
    /// (<see cref="TrySelfUpdateAsync"/>).
    ///
    /// <para>Der Versionsvergleich läuft über <see cref="ToolUpdatePolicy.OnlyWhenNewer"/>. Der
    /// frühere Weg — „Zeichenketten unterschiedlich, also Update nötig" — bot in zwei Fällen ein
    /// Update an, in denen es falsch war: bei einem lokal installierten Nightly-Build, der
    /// <i>neuer</i> ist als der letzte Release (das Angebot wäre ein Downgrade), und wenn das
    /// Werkzeug auf <c>--version</c> gar nicht geantwortet hat (dann war die lokale Version
    /// <c>null</c> und galt als „ungleich").</para>
    /// </summary>
    internal sealed class YtDlpTool : ManagedToolBase
    {
        private const string Owner = "yt-dlp";
        private const string Repo = "yt-dlp";
        private const string ExeName = "yt-dlp.exe";

        /// <summary>Anhang mit den Prüfsummen aller Release-Dateien — bei yt-dlp Teil jedes
        /// Releases. Ohne diesen Griff bliebe der Download eines ausführbaren Programms ohne
        /// Prüfsumme, und das wäre eine bewusste Lücke (<c>02-BEST-PRACTICES.md</c>, Abschnitt 9).</summary>
        private const string ChecksumAssetName = "SHA2-256SUMS";

        /// <summary>Deterministische GitHub-Adresse für den Anhang eines bestimmten Tags — für die
        /// Quellen ohne eigene Asset-Liste (PyPI, Atom-Feed, Weiterleitung).</summary>
        private const string DownloadUrlTemplate =
            "https://github.com/{owner}/{repo}/releases/download/{tag}/" + ExeName;

        /// <summary>Letzte Rückfalladresse, wenn <b>keine</b> Quelle geantwortet hat: GitHubs
        /// eigene Weiterleitung auf den Anhang der neuesten Ausgabe. Sie liefert keine
        /// Versionsnummer und taugt deshalb nur zum Installieren, nicht zum Vergleichen.</summary>
        private const string LatestAssetUrl =
            "https://github.com/" + Owner + "/" + Repo + "/releases/latest/download/" + ExeName;

        /// <summary>Das Selbst-Update lädt und ersetzt sich in einem Zug und braucht deshalb
        /// deutlich mehr Luft als ein Versionsabruf.</summary>
        private static readonly TimeSpan SelfUpdateTimeout = TimeSpan.FromMinutes(3);

        // Grenzen des Datums-Versionsschemas von yt-dlp. Absichtlich weit gefasst - hier soll
        // kein Werkzeug ausgesperrt werden, das seit Jahren nicht aktualisiert wurde, und kein
        // Rechner mit falsch gestellter Uhr. Die Grenze trennt "Jahreszahl" von "Hauptversion",
        // nicht "alt" von "neu".
        private const int MinPlausibleYear = 2000;
        private const int MaxPlausibleYear = 2999;

        private readonly string[] _targetPaths = [AppPaths.YtDlp];

        public override string Id => "yt-dlp";

        public override string DisplayName => "yt-dlp";

        public override bool RequiredForOperation => true;

        public override ToolUpdatePolicy UpdatePolicy => ToolUpdatePolicy.OnlyWhenNewer;

        public override IReadOnlyList<string> TargetPaths => _targetPaths;

        public override IReadOnlyList<IReleaseSource> CreateSources() => ReleaseSources.CreateYtDlpChain();

        public override ReleaseQuery CreateQuery() => new(
            Owner, Repo,
            AssetPattern: ExeName,
            DownloadUrlTemplate: DownloadUrlTemplate,
            PackageName: "yt-dlp");

        protected override string VersionExecutable => AppPaths.YtDlp;

        protected override IReadOnlyList<string> VersionArguments => ["--version"];

        /// <summary>Namen, unter denen sich yt-dlp in seiner Versionsressource ausweist. Am
        /// 2026-08-31 an der ausgelieferten Datei geprüft: <c>ProductName</c> und
        /// <c>FileDescription</c> lauten beide <c>yt-dlp</c>, <c>FileVersion</c> ist
        /// <c>2026.08.19</c> — genau die Angabe, die auch <c>--version</c> ausgibt.</summary>
        private static readonly string[] ProductNames = ["yt-dlp"];

        /// <summary>
        /// yt-dlp trägt Version und Identität in seiner Versionsressource — der Prozessaufruf
        /// erübrigt sich damit im Normalfall. Das ist bei diesem Werkzeug kein Feinschliff: Als
        /// PyInstaller-Bündel fährt es bei <b>jedem</b> Aufruf einen vollständigen
        /// Python-Interpreter hoch und braucht dafür rund 3,7 Sekunden — auf den gemessenen
        /// Startpfad umgerechnet zwei Drittel der gesamten Startzeit, für eine einzige Zeile
        /// Ausgabe.
        ///
        /// <para>Derselbe Weg wird laut Entwurf auch für TwitchDownloaderCLI genutzt; das Muster
        /// ist also nicht neu, sondern hier nur an der Stelle angewandt, an der es am meisten
        /// spart.</para>
        /// </summary>
        protected override ToolProbe? TryProbeWithoutProcess() =>
            ProbeFromVersionResource(AppPaths.YtDlp, ProductNames, IsYtDlpVersion);

        protected override string? ExtractVersion(string output) => ExtractVersionLine(output);

        protected override bool IsOwnVersion(ToolVersion version) => IsYtDlpVersion(version);

        /// <summary>
        /// <c>yt-dlp.exe --version</c> gibt <b>genau eine Zeile mit nichts als der Version</b>
        /// aus, z. B. <c>2026.08.19</c>. Genau diese Enge ist hier der Nachweis: Ein fremdes
        /// Programm schreibt bereitwillig etwas wie <c>git version 2.47.1.windows.1</c> — mit
        /// Leerzeichen, und damit erkennbar nicht yt-dlp.
        /// </summary>
        internal static string? ExtractVersionLine(string output)
        {
            string? line = FirstNonEmptyLine(output);
            if (line is null)
                return null;

            foreach (char c in line)
            {
                if (char.IsWhiteSpace(c))
                    return null;
            }

            return line;
        }

        /// <summary>
        /// yt-dlp zählt nach Datum: Das erste Segment ist das Jahr, und die Angabe besteht
        /// ausschließlich aus Zahlen (auch bei Nightlies: <c>2026.08.19.232303</c>). Ein
        /// Programm, das <c>2.47.1</c> ausgibt, ist damit ebenso aussortiert wie eines, das
        /// gar keine Zahl liefert.
        /// </summary>
        internal static bool IsYtDlpVersion(ToolVersion version) =>
            version.IsOrdering && version.FirstSegment is >= MinPlausibleYear and <= MaxPlausibleYear;

        public override async Task<ToolInstallOutcome> InstallAsync(
            ReleaseInfo? release,
            IProgress<double>? progress,
            IProgress<ToolInstallStage>? stage,
            CancellationToken ct)
        {
            string target = AppPaths.YtDlp;
            string staged = target + ToolInstaller.StagedSuffix;

            var asset = ResolveAsset(release);
            if (asset.Url is null)
                return new ToolInstallOutcome(ToolInstallStatus.Failed, ToolVersion.Unknown,
                    "keine Download-Adresse ermittelbar");

            try
            {
                Directory.CreateDirectory(AppPaths.ToolsDir);

                string? sha256 = asset.Sha256 ?? await TryReadChecksumAsync(release, ct);

                stage?.Report(ToolInstallStage.Downloading);
                Log.Info($"[{Id}] Lade {ExeName} von {asset.Url} " +
                    $"({(sha256 is null ? "ohne bekannte Prüfsumme" : "mit Prüfsumme")}).");

                var verification = await VerifiedDownload.ToFileAsync(
                    asset.Url, staged, sha256, asset.Size, progress, ct);

                Log.Info($"[{Id}] {ExeName} geladen: {verification.Bytes} Byte, " +
                    $"Prüfsumme {(verification.ChecksumChecked ? "geprüft" : "nicht prüfbar")}, " +
                    $"Größe {(verification.SizeChecked ? "abgeglichen" : "nicht abgeglichen")}.");

                stage?.Report(ToolInstallStage.Replacing);
                bool hadPrevious = File.Exists(target);

                var replaceResult = await ToolInstaller.ReplaceAllAsync(
                    Id,
                    [new ToolInstaller.Replacement(target, staged)],
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

        /// <summary>
        /// Der Notausgang: <c>yt-dlp.exe -U</c>. yt-dlp bringt ein vollwertiges Selbst-Update mit
        /// und braucht dafür keine Metadatenquelle — der robusteste Weg überhaupt, wenn die ganze
        /// Kette schweigt.
        ///
        /// <para><b>Nicht harmlos, deshalb nie automatisch:</b> yt-dlp lädt dabei selbst aus dem
        /// Netz, ohne dass MortysDLP die Prüfsumme oder das Ziel sieht. Die Methode läuft
        /// ausschließlich auf ausdrückliche Auslösung durch den Nutzer, und die Protokollzeile hält
        /// fest, dass hier ohne eigene Prüfung aktualisiert wurde. Es gibt aus demselben Grund
        /// auch keine <c>.old</c>-Rückfallebene: Das Ersetzen macht yt-dlp selbst, MortysDLP kommt
        /// dazwischen nicht vor. Was bleibt, ist die Kontrolle danach.</para>
        /// </summary>
        /// <returns>Die nach dem Selbst-Update gelesene Version, oder
        /// <see cref="ToolVersion.Unknown"/>, wenn es nicht geklappt hat.</returns>
        public async Task<ToolVersion> TrySelfUpdateAsync(CancellationToken ct)
        {
            string path = AppPaths.YtDlp;

            if (!File.Exists(path))
            {
                Log.Warn($"[{Id}] Selbst-Update nicht möglich: {ExeName} ist nicht installiert.");
                return ToolVersion.Unknown;
            }

            var before = (await ProbeAsync(ct)).Version;

            Log.Warn($"[{Id}] Notausgang: {ExeName} -U wird ausgeführt. yt-dlp lädt dabei selbst " +
                "aus dem Netz - MortysDLP sieht dabei keine Prüfsumme und kann den Download nicht " +
                "verifizieren.");

            try
            {
                var result = await ProcessRunner.RunAsync(
                    path, ["-U"], timeout: SelfUpdateTimeout, workingDirectory: AppPaths.ToolsDir, ct: ct);

                if (!result.Success)
                {
                    Log.Warn($"[{Id}] Selbst-Update endete mit Exit-Code {result.ExitCode}.");
                    return ToolVersion.Unknown;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Warn($"[{Id}] Selbst-Update fehlgeschlagen: {ex.Message}", ex);
                return ToolVersion.Unknown;
            }

            var afterProbe = await ProbeAsync(ct);
            var after = afterProbe.Version;

            if (!afterProbe.Usable)
            {
                Log.Error($"[{Id}] Nach dem Selbst-Update ist {ExeName} nicht brauchbar " +
                    $"({DescribeProbe(afterProbe)}). Eine Rückfallebene gibt es hier nicht - das " +
                    "Werkzeug muss neu installiert werden.");
                return ToolVersion.Unknown;
            }

            Log.Info(after.IsSameRelease(before)
                ? $"[{Id}] Selbst-Update abgeschlossen, Version unverändert bei {after} - es gab nichts Neueres."
                : $"[{Id}] Selbst-Update abgeschlossen: {before} -> {after} (ohne eigene Prüfsummenkontrolle).");

            return after;
        }

        /// <summary>Wählt den Anhang, den es zu laden gilt. Die beiden GitHub-API-Quellen liefern
        /// eine Asset-Liste (dann entscheidet <see cref="AssetSelector"/> und die Größe ist
        /// bekannt), die übrigen nur eine Version — dort greift die Adressvorlage, und ohne jede
        /// Antwort die feste Weiterleitung auf die neueste Ausgabe.</summary>
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
                    if (AssetSelector.Select(release.Assets, ExeName) is { } selected)
                        return (selected.Url, selected.Size > 0 ? selected.Size : null, release.Sha256);
                }
                catch (AssetAmbiguousException ex)
                {
                    // Raten ist hier falsch: Ein zweiter Anhang, der auf das Muster passt, ist ein
                    // Hinweis darauf, dass sich am Release etwas geändert hat.
                    Log.Warn($"[{Id}] Anhang nicht eindeutig wählbar: {ex.Message}");
                }
            }

            if (!string.IsNullOrEmpty(release.DownloadUrl))
                return (release.DownloadUrl, release.ExpectedSize, release.Sha256);

            Log.Info($"[{Id}] Release {release.Version} nennt keinen Anhang - es wird über die " +
                "feste Adresse der neuesten Ausgabe geladen.");
            return (LatestAssetUrl, null, null);
        }

        /// <summary>Holt die Prüfsumme aus <c>SHA2-256SUMS</c> des Releases. <c>null</c>, wenn es
        /// diesen Anhang nicht gibt oder er nicht lesbar ist — das ist kein Grund, den Download zu
        /// verweigern, wird aber von <see cref="VerifiedDownload"/> protokolliert.</summary>
        private async Task<string?> TryReadChecksumAsync(ReleaseInfo? release, CancellationToken ct)
        {
            var checksumAsset = release?.Assets.FirstOrDefault(a =>
                string.Equals(a.Name, ChecksumAssetName, StringComparison.OrdinalIgnoreCase));

            if (checksumAsset is null)
                return null;

            try
            {
                UrlSafety.EnsureAllowed(new Uri(checksumAsset.Url));

                using var response = await Http.SendWithRetryAsync(
                    Http.Shared, () => new HttpRequestMessage(HttpMethod.Get, checksumAsset.Url), ct: ct);

                if (!response.IsSuccessStatusCode)
                    return null;

                string content = await response.Content.ReadAsStringAsync(ct);
                string? sha = ChecksumFile.Find(content, ExeName);

                if (sha is null)
                    Log.Warn($"[{Id}] {ChecksumAssetName} enthält keinen Eintrag für {ExeName}.");

                return sha;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Warn($"[{Id}] {ChecksumAssetName} konnte nicht gelesen werden: {ex.Message}", ex);
                return null;
            }
        }
    }
}
