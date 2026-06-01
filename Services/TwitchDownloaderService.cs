using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MortysDLP.Services
{
    /// <summary>
    /// Kapselt alle Operationen rund um TwitchDownloaderCLI:
    /// Update-Check, Installation und VOD/Chat-Download.
    /// </summary>
    internal class TwitchDownloaderService : IDownloadableToolService
    {
        private static readonly HttpClient _httpClient;

        static TwitchDownloaderService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MortysDLP-ToolUpdater");
            _httpClient.Timeout = TimeSpan.FromMinutes(10);
        }

        // ── Pfade ────────────────────────────────────────────────────────────────

        public static string CliExePath =>
            NormalizePath(Properties.Settings.Default.TwitchDownloaderPath);

        private static string NormalizePath(string path) =>
            string.IsNullOrEmpty(path) ? path : System.IO.Path.GetFullPath(path);

        public static bool IsInstalled() => File.Exists(CliExePath);

        // ── GitHub-Release ───────────────────────────────────────────────────────

        public async Task DownloadAssetAsync(string url, string targetPath, IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            await ToolDownloadHelper.DownloadAssetAsync(_httpClient, url, targetPath, progress, cancellationToken);
        }

        public async Task<(string? Version, string? AssetUrl)> GetLatestReleaseInfoAsync()
        {
            try
            {
                string api = Properties.Settings.Default.TwitchDownloaderReleaseURL;
                var response = await _httpClient.GetAsync(api);
                if (!response.IsSuccessStatusCode) return (null, null);

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                string? version = doc.RootElement.GetProperty("tag_name").GetString();
                string? assetUrl = null;

                if (doc.RootElement.TryGetProperty("assets", out var assets))
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        var name = asset.GetProperty("name").GetString() ?? "";
                        // Suche nach TwitchDownloaderCLI-Windows-Binary (keine Linux/Mac)
                        if (name.Contains("TwitchDownloaderCLI", StringComparison.OrdinalIgnoreCase) &&
                            (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                             (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
                              name.Contains("Windows", StringComparison.OrdinalIgnoreCase))))
                        {
                            assetUrl = asset.GetProperty("browser_download_url").GetString();
                            // .exe bevorzugen
                            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                                break;
                        }
                    }
                }

                return (version, assetUrl);
            }
            catch
            {
                return (null, null);
            }
        }

        public async Task<string?> GetLocalVersionAsync()
        {
            if (!IsInstalled()) return null;
            try
            {
                var fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(CliExePath);

                // FileVersion ist z.B. "1.56.4.0" – zuverlässiger als ProductVersion,
                // da ProductVersion einen Commit-Hash-Suffix enthält ("1.56.4+abc1234").
                string? ver = fvi.FileVersion;
                if (!string.IsNullOrWhiteSpace(ver))
                {
                    // "1.56.4.0" → "1.56.4": letztes ".0"-Segment entfernen
                    var parts = ver.Trim().TrimStart('v', 'V').Split('.');
                    if (parts.Length == 4 && parts[3] == "0")
                        return string.Join(".", parts[0], parts[1], parts[2]);
                    return ver.Trim().TrimStart('v', 'V');
                }

                // Fallback: ProductVersion ohne Commit-Suffix
                ver = fvi.ProductVersion;
                if (!string.IsNullOrWhiteSpace(ver))
                {
                    // "1.56.4+abc1234" → "1.56.4"
                    int plusIdx = ver.IndexOf('+');
                    if (plusIdx >= 0) ver = ver[..plusIdx];
                    return ver.Trim().TrimStart('v', 'V');
                }

                // Letzter Fallback: CLI-Aufruf
                var psi = new ProcessStartInfo
                {
                    FileName = CliExePath,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var process = Process.Start(psi);
                if (process == null) return null;

                string? output = await process.StandardOutput.ReadLineAsync();
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try { await process.WaitForExitAsync(cts.Token); }
                catch (OperationCanceledException) { process.Kill(); }
                return output?.Trim().TrimStart('v', 'V');
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TwitchDownloaderService] Fehler beim Auslesen der Version: {ex}");
                return null;
            }
        }

        /// <summary>Gibt die Dateigröße der EXE in MB zurück, oder null wenn nicht installiert.</summary>
        public static double? GetFileSizeMB()
        {
            if (!IsInstalled()) return null;
            try
            {
                var info = new FileInfo(CliExePath);
                return Math.Round(info.Length / 1_048_576.0, 1);
            }
            catch { return null; }
        }

        public bool IsUpdateRequired(string? localVersion, string? latestVersion)
        {
            if (string.IsNullOrWhiteSpace(localVersion) || string.IsNullOrWhiteSpace(latestVersion))
                return true;
            latestVersion = latestVersion.TrimStart('v', 'V');
            localVersion = localVersion.TrimStart('v', 'V');
            return !string.Equals(localVersion, latestVersion, StringComparison.OrdinalIgnoreCase);
        }

        // ── Inhaltstyp-Erkennung ─────────────────────────────────────────────────

        public enum ContentType { Vod, Clip }

        /// <summary>
        /// Parst eine Twitch-URL oder ID/Slug und gibt ID + Typ zurück.
        /// Unterstützte Formate:
        ///   VOD  – reine Zahl, twitch.tv/videos/&lt;id&gt;
        ///   Clip – twitch.tv/clips/&lt;slug&gt;, clips.twitch.tv/&lt;slug&gt;, reiner Slug (nicht rein numerisch)
        /// </summary>
        public static (string? Id, ContentType Type) ParseInput(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return (null, ContentType.Vod);

            input = input.Trim();

            // Query-String und Fragment entfernen (z.B. ?filter=clips&range=7d)
            var qIdx = input.IndexOfAny(['?', '#']);
            if (qIdx >= 0) input = input[..qIdx].TrimEnd('/');

            // Reine Zahl → VOD-ID
            if (Regex.IsMatch(input, @"^\d+$"))
                return (input, ContentType.Vod);

            // twitch.tv/videos/<id>
            var vodMatch = Regex.Match(input, @"twitch\.tv/videos/(\d+)", RegexOptions.IgnoreCase);
            if (vodMatch.Success)
                return (vodMatch.Groups[1].Value, ContentType.Vod);

            // twitch.tv/<user>/clip/<slug>  (mit Benutzername, singular "clip")
            var clipUserMatch = Regex.Match(input, @"twitch\.tv/[^/]+/clip/([A-Za-z0-9_\-]+)", RegexOptions.IgnoreCase);
            if (clipUserMatch.Success)
                return (clipUserMatch.Groups[1].Value, ContentType.Clip);

            // twitch.tv/clips/<slug>  (ohne Benutzername, plural "clips")
            var clipMatch = Regex.Match(input, @"twitch\.tv/clips/([A-Za-z0-9_\-]+)", RegexOptions.IgnoreCase);
            if (clipMatch.Success)
                return (clipMatch.Groups[1].Value, ContentType.Clip);

            // clips.twitch.tv/<slug>
            var clipsSubMatch = Regex.Match(input, @"clips\.twitch\.tv/([A-Za-z0-9_\-]+)", RegexOptions.IgnoreCase);
            if (clipsSubMatch.Success)
                return (clipsSubMatch.Groups[1].Value, ContentType.Clip);

            // Reiner Slug (enthält Buchstaben → kein VOD)
            if (Regex.IsMatch(input, @"^[A-Za-z0-9_\-]+$"))
                return (input, ContentType.Clip);

            return (null, ContentType.Vod);
        }

        /// <summary>Rückwärtskompatibel: gibt nur die VOD-ID zurück.</summary>
        public static string? ExtractVodId(string input)
        {
            var (id, type) = ParseInput(input);
            return type == ContentType.Vod ? id : null;
        }

        // ── Download ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Lädt ein Twitch-VOD herunter.
        /// </summary>
        /// <summary>
        /// Lädt ein Twitch-VOD herunter.
        /// </summary>
        /// <param name="bandwidthKibs">Max. Bandbreite pro Thread in KiB/s, -1 = unbegrenzt.</param>
        public static async Task DownloadVodAsync(
            string vodId,
            string outputPath,
            int bandwidthKibs = -1,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            string bwArg = bandwidthKibs > 0 ? $" --bandwidth {bandwidthKibs}" : "";
            string args = $"videodownload --id {vodId} --collision Overwrite{bwArg} -o \"{outputPath}\"";
            await RunCliAsync(args, progress, cancellationToken);
        }

        /// <summary>
        /// Lädt einen Twitch-Clip herunter.
        /// </summary>
        /// <param name="bandwidthKibs">Max. Bandbreite in KiB/s, -1 = unbegrenzt.</param>
        public static async Task DownloadClipAsync(
            string clipSlug,
            string outputPath,
            int bandwidthKibs = -1,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            string bwArg = bandwidthKibs > 0 ? $" --bandwidth {bandwidthKibs}" : "";
            string args = $"clipdownload --id {clipSlug} --collision Overwrite{bwArg} -o \"{outputPath}\"";
            await RunCliAsync(args, progress, cancellationToken);
        }

        /// <summary>
        /// Ruft den Titel eines VODs oder Clips über die Twitch GQL API ab.
        /// Gibt null zurück, falls der Abruf fehlschlägt.
        /// </summary>
        public static async Task<string?> GetContentTitleAsync(
            string contentId,
            ContentType contentType,
            CancellationToken cancellationToken = default)
        {
            try
            {
                const string gqlUrl  = "https://gql.twitch.tv/gql";
                const string clientId = "kimne78kx3ncx6brgo4mv6wki5h1ko";

                string query = contentType == ContentType.Clip
                    ? $"[{{\"query\":\"query {{ clip(slug: \\\"{contentId}\\\") {{ title }} }}\"}}]"
                    : $"[{{\"query\":\"query {{ video(id: \\\"{contentId}\\\") {{ title }} }}\"}}]";

                using var req = new HttpRequestMessage(HttpMethod.Post, gqlUrl);
                req.Headers.Add("Client-Id", clientId);
                req.Content = new StringContent(query, System.Text.Encoding.UTF8, "application/json");

                using var resp = await _httpClient.SendAsync(req, cancellationToken);
                if (!resp.IsSuccessStatusCode) return null;

                var json = await resp.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(json);

                var data = doc.RootElement[0].GetProperty("data");
                var node = contentType == ContentType.Clip ? data.GetProperty("clip") : data.GetProperty("video");
                return node.GetProperty("title").GetString();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Lädt den Chat eines Twitch-VODs herunter.
        /// </summary>
        public static async Task DownloadChatAsync(
            string contentId,
            string outputPath,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            string args = $"chatdownload --id {contentId} --collision Overwrite -o \"{outputPath}\"";
            await RunCliAsync(args, progress, cancellationToken);
        }

        public enum RenderQualityPreset { Standard, High, Ultra }

        public static async Task RenderChatAsync(
            string chatJsonPath,
            string outputVideoPath,
            RenderQualityPreset quality,
            IProgress<string>? progress,
            CancellationToken cancellationToken)
        {
            // Standard: 350×600, 30fps, font 12
            // High:     525×900, 60fps, font 14
            // Ultra:    700×1200, 60fps, font 16, --outline
            var (w, h, fps, fontSize, outline) = quality switch
            {
                RenderQualityPreset.High  => (525,  900, 60, 14, false),
                RenderQualityPreset.Ultra => (700, 1200, 60, 16, true),
                _                         => (350,  600, 30, 12, false),
            };
            string outlineArg = outline ? " --outline" : "";
            string args = $"chatrender -i \"{chatJsonPath}\" -w {w} -h {h} --framerate {fps} --font-size {fontSize}{outlineArg} --collision Overwrite -o \"{outputVideoPath}\"";
            await RunCliAsync(args, progress, cancellationToken);
        }

        private static async Task RunCliAsync(
            string arguments,
            IProgress<string>? progress,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var psi = new ProcessStartInfo
            {
                FileName = CliExePath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            process.Start();

            // Sofortiger Kill bei Cancel – wichtig damit ReadLineAsync() deblockt
            using var killReg = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);
                }
                catch { /* Prozess schon beendet */ }
            });

            var outputTask = Task.Run(async () =>
            {
                try
                {
                    string? line;
                    while ((line = await process.StandardOutput.ReadLineAsync(cancellationToken)) != null)
                        if (!string.IsNullOrWhiteSpace(line))
                            progress?.Report(line);
                }
                catch (OperationCanceledException) { }
            }, CancellationToken.None);  // eigener Cancel-Scope → Task soll sauber enden

            var errorTask = Task.Run(async () =>
            {
                try
                {
                    string? line;
                    while ((line = await process.StandardError.ReadLineAsync(cancellationToken)) != null)
                        if (!string.IsNullOrWhiteSpace(line))
                            progress?.Report($"[ERR] {line}");
                }
                catch (OperationCanceledException) { }
            }, CancellationToken.None);

            await Task.WhenAll(outputTask, errorTask);

            // Auf Prozessende warten (nach Kill in der Regel sofort)
            try
            {
                await process.WaitForExitAsync(CancellationToken.None);
            }
            catch { /* ignorieren */ }

            cancellationToken.ThrowIfCancellationRequested();

            if (process.ExitCode != 0)
                throw new Exception($"TwitchDownloaderCLI beendete sich mit Exitcode {process.ExitCode}.");
        }
    }
}
