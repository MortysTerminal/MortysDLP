using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace MortysDLP.Services
{
    internal class YtDlpUpdateService
    {
        private const string LatestReleaseApi = "https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest";
        private readonly HttpClient _httpClient;

        public YtDlpUpdateService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MortysDLP-ToolUpdater");
        }

        /// <summary>
        /// Prüft, ob yt-dlp.exe existiert.
        /// </summary>
        public bool ToolExists(string toolPath)
        {
            return File.Exists(toolPath);
        }

        public async Task<(string? version, string? assetUrl)> GetLatestReleaseInfoAsync()
        {
            var response = await _httpClient.GetAsync(LatestReleaseApi);
            if (!response.IsSuccessStatusCode)
                return (null, null);

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            string? version = doc.RootElement.GetProperty("tag_name").GetString();
            string? assetUrl = null;

            if (doc.RootElement.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString();
                    if (name != null && name.Contains("yt-dlp.exe"))
                    {
                        assetUrl = asset.GetProperty("browser_download_url").GetString();
                        break;
                    }
                }
            }

            return (version, assetUrl);
        }

        /// <summary>
        /// Liest die lokale Version von yt-dlp.exe aus, indem das Tool mit --version aufgerufen wird.
        /// </summary>
        public string? GetLocalVersion(string toolPath)
        {
            if (!File.Exists(toolPath))
                return null;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = toolPath,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var process = Process.Start(psi);
                if (process == null)
                    return null;

                string? output = process.StandardOutput.ReadLine();
                process.WaitForExit(3000); // max 3 Sekunden warten
                return output?.Trim();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Vergleicht die lokale Version mit der neuesten GitHub-Version.
        /// </summary>
        public bool IsUpdateRequired(string? localVersion, string? latestVersion)
        {
            if (string.IsNullOrWhiteSpace(localVersion) || string.IsNullOrWhiteSpace(latestVersion))
                return true;

            // yt-dlp gibt die Version meist als "2024.07.02" oder ähnlich zurück
            // GitHub-Tag ist meist "2024.07.02" oder "v2024.07.02"
            latestVersion = latestVersion.TrimStart('v', 'V');
            return !string.Equals(localVersion, latestVersion, StringComparison.OrdinalIgnoreCase);
        }

        public async Task DownloadAssetAsync(string url, string targetPath, IProgress<double>? progress = null)
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength ?? -1L;
            var canReportProgress = total != -1 && progress != null;

            using var stream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[81920];
            long totalRead = 0;
            int read;
            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, read);
                totalRead += read;
                if (canReportProgress)
                    progress!.Report((double)totalRead / total);
            }
        }
    }
}