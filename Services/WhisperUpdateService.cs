using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;

namespace MortysDLP.Services
{
    /// <summary>
    /// Prüft auf neue Versionen von whisper.cpp und lädt das Windows-Binary von GitHub herunter.
    /// Folgt dem gleichen Muster wie YtDlpUpdateService und FfmpegUpdateService.
    /// </summary>
    internal class WhisperUpdateService : IDownloadableToolService
    {
        private static readonly HttpClient _httpClient;
        private const string ReleaseApi = "https://api.github.com/repos/ggerganov/whisper.cpp/releases/latest";

        static WhisperUpdateService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MortysDLP-ToolUpdater");
            _httpClient.Timeout = TimeSpan.FromMinutes(10);
        }

        public async Task DownloadAssetAsync(string url, string targetPath, IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            await ToolDownloadHelper.DownloadAssetAsync(_httpClient, url, targetPath, progress, cancellationToken);
        }

        /// <summary>Ruft die neueste Release-Info von GitHub ab.</summary>
        public async Task<(string? Version, string? AssetUrl)> GetLatestReleaseInfoAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync(ReleaseApi);
                if (!response.IsSuccessStatusCode) return (null, null);

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                string? version = doc.RootElement.GetProperty("tag_name").GetString();
                string? assetUrl = null;

                if (doc.RootElement.TryGetProperty("assets", out var assets))
                {
                    // Suche nach CPU-ZIP: bevorzuge blas-bin-x64, Fallback bin-x64
                    // Kein CUDA/cuBLAS/OpenVINO (GPU-spezifisch, breite Kompatibilität wichtiger)
                    string? blasUrl = null;
                    string? plainUrl = null;

                    foreach (var asset in assets.EnumerateArray())
                    {
                        var name = asset.GetProperty("name").GetString() ?? "";
                        if (!name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;
                        if (name.Contains("cuda", StringComparison.OrdinalIgnoreCase)) continue;
                        if (name.Contains("cublas", StringComparison.OrdinalIgnoreCase)) continue;
                        if (name.Contains("openvino", StringComparison.OrdinalIgnoreCase)) continue;
                        if (!name.Contains("x64", StringComparison.OrdinalIgnoreCase) &&
                            !name.Contains("win", StringComparison.OrdinalIgnoreCase)) continue;

                        string? url = asset.GetProperty("browser_download_url").GetString();
                        if (name.Contains("blas", StringComparison.OrdinalIgnoreCase))
                            blasUrl = url;
                        else
                            plainUrl = url;
                    }

                    assetUrl = blasUrl ?? plainUrl;
                }

                return (version, assetUrl);
            }
            catch
            {
                return (null, null);
            }
        }

        /// <summary>
        /// Extrahiert das gesamte whisper.cpp-Paket aus dem ZIP in das Zielverzeichnis.
        /// Wichtig: whisper.cpp benötigt mehrere DLLs (ggml.dll, whisper.dll, openblas.dll u.a.)
        /// die alle im selben Ordner wie die whisper.exe liegen müssen.
        /// Das Haupt-Binary wird je nach Version 'main.exe' oder 'whisper-cli.exe' genannt
        /// und wird beim Extrahieren auf 'whisper.exe' umbenannt.
        /// </summary>
        public static async Task<bool> ExtractWhisperExeFromZipAsync(string zipPath, string targetExePath)
        {
            try
            {
                string? targetDir = Path.GetDirectoryName(targetExePath);
                if (string.IsNullOrEmpty(targetDir)) return false;
                if (!Directory.Exists(targetDir))
                    Directory.CreateDirectory(targetDir);

                using var archive = ZipFile.OpenRead(zipPath);
                bool foundExe = false;

                foreach (var entry in archive.Entries)
                {
                    // Ordner-Einträge überspringen
                    if (string.IsNullOrEmpty(entry.Name)) continue;

                    string fileName = entry.Name;

                    // Haupt-Binary erkennen und auf whisper.exe umbenennen
                    bool isMainExe =
                        string.Equals(fileName, "whisper-cli.exe", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(fileName, "main.exe", StringComparison.OrdinalIgnoreCase);

                    string destPath = isMainExe
                        ? targetExePath
                        : Path.Combine(targetDir, fileName);

                    // Nur .exe und .dll extrahieren (keine Docs/Beispiele)
                    string ext = Path.GetExtension(fileName).ToLowerInvariant();
                    if (ext != ".exe" && ext != ".dll") continue;

                    entry.ExtractToFile(destPath, overwrite: true);

                    if (isMainExe) foundExe = true;
                }

                return foundExe;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Liest die lokale Whisper-Version aus dem Binary aus.</summary>
        public static async Task<string?> GetLocalVersionAsync(string whisperPath)
        {
            if (!File.Exists(whisperPath)) return null;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = whisperPath,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null) return null;

                string output = await process.StandardOutput.ReadToEndAsync();
                string errOutput = await process.StandardError.ReadToEndAsync();

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try { await process.WaitForExitAsync(cts.Token); }
                catch (OperationCanceledException) { process.Kill(); }

                // Whisper gibt Version in stdout oder stderr aus
                string combined = output + errOutput;
                var match = System.Text.RegularExpressions.Regex.Match(combined, @"whisper\.cpp\s+version\s+([\d.]+)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success) return match.Groups[1].Value.Trim();

                // Fallback: erste nicht-leere Zeile
                var firstLine = combined.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                        .FirstOrDefault();
                return firstLine?.Trim();
            }
            catch
            {
                return null;
            }
        }

        public static bool IsUpdateRequired(string? localVersion, string? latestVersion)
        {
            if (string.IsNullOrWhiteSpace(localVersion) || string.IsNullOrWhiteSpace(latestVersion))
                return false; // Whisper ist optional – kein Update erzwingen wenn Version nicht lesbar
            latestVersion = latestVersion.TrimStart('v', 'V');
            return !string.Equals(localVersion, latestVersion, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Lädt ein Whisper-Modell von HuggingFace herunter.</summary>
        public async Task DownloadModelAsync(string downloadUrl, string targetPath,
            IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            string? dir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string tempPath = targetPath + ".download";
            try
            {
                await ToolDownloadHelper.DownloadAssetAsync(_httpClient, downloadUrl, tempPath, progress, cancellationToken);
                if (File.Exists(targetPath))
                    File.Delete(targetPath);
                File.Move(tempPath, targetPath);
            }
            finally
            {
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            }
        }
    }
}
