using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Reflection;

namespace MortysDLP.Services
{
    internal class UpdateService : IDisposable
    {
        private string GitHubApiUrl = Properties.Settings.Default.MortysDLPGitHubAPIURL;
        private readonly HttpClient _httpClient;
        private bool _disposed = false;

        public UpdateService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MortysDLP-Updater");
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
        }

        public async Task<(string? version, string? assetUrl)> GetLatestReleaseInfoAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync(GitHubApiUrl);
                if (!response.IsSuccessStatusCode)
                    return (null, null);

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                string? version = doc.RootElement.GetProperty("tag_name").GetString();
                string? assetUrl = null;

                if (doc.RootElement.TryGetProperty("assets", out var assets) && assets.GetArrayLength() > 0)
                {
                    assetUrl = assets[0].GetProperty("browser_download_url").GetString();
                }

                return (version, assetUrl);
            }
            catch
            {
                return (null, null);
            }
        }

        public Version GetCurrentVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0);
        }

        public bool IsNewerVersion(string latestVersion)
        {
            if (Version.TryParse(latestVersion.TrimStart('v', 'V'), out var latest))
            {
                return latest > GetCurrentVersion();
            }
            return false;
        }

        public bool IsNewerVersion(string latestVersion, string currentVersion)
        {
            // Entferne führendes "v" falls vorhanden
            latestVersion = latestVersion.TrimStart('v', 'V');
            DateTime latest, current;
            if (DateTime.TryParseExact(latestVersion, "yyyy.MM.dd", null, System.Globalization.DateTimeStyles.None, out latest) &&
                DateTime.TryParseExact(currentVersion, "yyyy.MM.dd", null, System.Globalization.DateTimeStyles.None, out current))
            {
                return latest > current;
            }
            return false;
        }

        public async Task DownloadAssetAsync(string url, string targetPath)
        {
            var data = await _httpClient.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(targetPath, data);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _httpClient?.Dispose();
                _disposed = true;
            }
        }
    }
}