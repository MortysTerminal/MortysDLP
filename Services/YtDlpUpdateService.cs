using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace MortysDLP.Services
{
    internal class YtDlpUpdateService
    {
        private static readonly HttpClient _httpClient;

        private string LatestReleaseApi = Properties.Settings.Default.YtdlpReleaseURL;

        static YtDlpUpdateService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MortysDLP-ToolUpdater");
        }

        public async Task DownloadAssetAsync(string url, string targetPath, IProgress<double>? progress = null)
        {
            await ToolDownloadHelper.DownloadAssetAsync(_httpClient, url, targetPath, progress);
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
            catch (Exception ex)
            {
                // Logging einbauen, z.B. mit NLog, Serilog oder Debug.WriteLine
                Debug.WriteLine($"Fehler beim Auslesen der lokalen Version: {ex}");
                return null;
            }
        }

        public bool IsUpdateRequired(string? localVersion, string? latestVersion)
        {
            if (string.IsNullOrWhiteSpace(localVersion) || string.IsNullOrWhiteSpace(latestVersion))
                return true;

            // yt-dlp gibt die Version meist als "2024.07.02" oder ähnlich zurück
            // GitHub-Tag ist meist "2024.07.02" oder "v2024.07.02"
            latestVersion = latestVersion.TrimStart('v', 'V');
            return !string.Equals(localVersion, latestVersion, StringComparison.OrdinalIgnoreCase);
        }

        public bool ToolExists(string toolPath)
        {
            return File.Exists(toolPath);
        }

    }
}