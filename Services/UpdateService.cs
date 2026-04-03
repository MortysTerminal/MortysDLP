using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;

namespace MortysDLP.Services
{
    internal class UpdateService : IDisposable
    {
        private string GitHubApiUrl = Properties.Settings.Default.MortysDLPGitHubAPIURL;
        private readonly HttpClient _httpClient;
        private bool _disposed = false;

        private const int DefaultMaxRetries = 3;
        private const int DownloadBufferSize = 81920;

        public UpdateService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MortysDLP-Updater");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task<(string? version, string? assetUrl, string? changelog)> GetLatestReleaseInfoAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync(GitHubApiUrl);
                if (!response.IsSuccessStatusCode)
                    return (null, null, null);

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                string? version = doc.RootElement.GetProperty("tag_name").GetString();
                string? assetUrl = null;
                string? changelog = null;

                if (doc.RootElement.TryGetProperty("assets", out var assets) && assets.GetArrayLength() > 0)
                {
                    assetUrl = assets[0].GetProperty("browser_download_url").GetString();
                }

                if (doc.RootElement.TryGetProperty("body", out var body))
                {
                    changelog = body.GetString();
                }

                return (version, assetUrl, changelog);
            }
            catch
            {
                return (null, null, null);
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

        /// <summary>
        /// Lädt ein Asset mit Fortschrittsanzeige und automatischem Retry bei Fehlern herunter.
        /// </summary>
        public async Task DownloadAssetAsync(string url, string targetPath, IProgress<double>? progress = null, CancellationToken ct = default, int maxRetries = DefaultMaxRetries)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                    response.EnsureSuccessStatusCode();

                    long totalBytes = response.Content.Headers.ContentLength ?? -1;
                    long bytesRead = 0;

                    await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
                    await using var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, DownloadBufferSize, useAsync: true);

                    var buffer = new byte[DownloadBufferSize];
                    int read;

                    while ((read = await contentStream.ReadAsync(buffer, ct)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
                        bytesRead += read;

                        if (totalBytes > 0)
                            progress?.Report((double)bytesRead / totalBytes * 100);
                    }

                    return; // Erfolg
                }
                catch (Exception) when (attempt < maxRetries)
                {
                    Debug.WriteLine($"[UpdateService] Download-Versuch {attempt}/{maxRetries} fehlgeschlagen, neuer Versuch...");

                    // Exponentielles Backoff
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);

                    try { if (File.Exists(targetPath)) File.Delete(targetPath); }
                    catch { /* Best-effort */ }
                }
            }
        }

        /// <summary>
        /// Ermittelt ein sicheres, beschreibbares temporäres Verzeichnis mit Fallback-Kandidaten.
        /// </summary>
        public static string GetSafeTempDirectory(string subFolder = "MortysDLP_Update")
        {
            string[] candidates =
            [
                Path.GetTempPath(),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                AppDomain.CurrentDomain.BaseDirectory
            ];

            foreach (var basePath in candidates)
            {
                if (string.IsNullOrWhiteSpace(basePath))
                    continue;

                try
                {
                    string dir = Path.Combine(basePath, subFolder);
                    Directory.CreateDirectory(dir);

                    // Schreibtest
                    string testFile = Path.Combine(dir, ".write_test");
                    File.WriteAllText(testFile, "test");
                    File.Delete(testFile);

                    return dir;
                }
                catch
                {
                    // Nächsten Kandidaten versuchen
                }
            }

            throw new IOException("Kein beschreibbares Verzeichnis für das Update gefunden.");
        }

        /// <summary>
        /// Prüft ob die heruntergeladene ZIP-Datei gültig ist und mindestens eine EXE enthält.
        /// </summary>
        public static bool ValidateZipIntegrity(string zipPath)
        {
            try
            {
                using var archive = ZipFile.OpenRead(zipPath);
                return archive.Entries.Any(e =>
                    e.FullName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
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