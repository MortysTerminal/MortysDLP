using MortysDLP.Helpers;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace MortysDLP.Services
{
    internal class YtDlpUpdateService : IDownloadableToolService
    {
        private string LatestReleaseApi = Properties.Settings.Default.YtdlpReleaseURL;

        public async Task DownloadAssetAsync(string url, string targetPath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            await ToolDownloadHelper.DownloadAssetAsync(Http.Shared, url, targetPath, progress, cancellationToken);
        }

        public async Task<(string? version, string? assetUrl)> GetLatestReleaseInfoAsync()
        {
            if (GitHubRateLimit.IsExhausted(DateTimeOffset.UtcNow))
                return (null, null);

            using var response = await Http.SendWithRetryAsync(
                Http.Shared, () => Http.CreateGitHubApiRequest(LatestReleaseApi));
            GitHubRateLimit.Observe(response.Headers, DateTimeOffset.UtcNow);

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

        public async Task<string?> GetLocalVersionAsync(string toolPath)
        {
            if (!File.Exists(toolPath))
                return null;

            try
            {
                var result = await ProcessRunner.RunAsync(toolPath, ["--version"], timeout: TimeSpan.FromSeconds(15));
                return result.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
            }
            catch (Exception ex)
            {
                Log.Warn("Fehler beim Auslesen der lokalen Version", ex);
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