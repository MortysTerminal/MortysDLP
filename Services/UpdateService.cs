using MortysDLP.Helpers;
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
    internal class UpdateService
    {
        private string GitHubApiUrl = Properties.Settings.Default.MortysDLPGitHubAPIURL;

        private const int DownloadBufferSize = 81920;

        public async Task<(string? version, string? assetUrl, string? changelog)> GetLatestReleaseInfoAsync()
        {
            if (GitHubRateLimit.IsExhausted(DateTimeOffset.UtcNow))
                return (null, null, null);

            try
            {
                using var response = await Http.SendWithRetryAsync(
                    Http.Shared, () => Http.CreateGitHubApiRequest(GitHubApiUrl));
                GitHubRateLimit.Observe(response.Headers, DateTimeOffset.UtcNow);

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

        /// <summary>
        /// Lädt ein Asset mit Fortschrittsanzeige herunter. Wiederholversuche laufen über
        /// <see cref="Http.SendWithRetryAsync"/> — nur für den Verbindungsaufbau/die
        /// Kopfzeilen, nicht mehr blind für jeden Fehler wie zuvor (ein 404 wurde früher
        /// dreimal wiederholt, ohne je zu einem anderen Ergebnis zu führen).
        /// </summary>
        public static async Task DownloadAssetAsync(string url, string targetPath, IProgress<double>? progress = null, CancellationToken ct = default)
        {
            using var response = await Http.SendWithRetryAsync(
                Http.Shared, () => new HttpRequestMessage(HttpMethod.Get, url), ct: ct);
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
    }
}