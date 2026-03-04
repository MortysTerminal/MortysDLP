using System.IO;
using System.Text.Json;

namespace MortysDLP.Services
{
    internal class StartupHealthCheckService
    {
        private static readonly string CacheFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MortysDLP", "startup_cache.json");

        private class StartupCache
        {
            public DateTime LastUpdateCheck { get; set; }
            public DateTime LastToolCheck { get; set; }
            public string? YtDlpVersion { get; set; }
            public bool FfmpegExists { get; set; }
            public bool FfprobeExists { get; set; }
        }

        public async Task<bool> ShouldCheckUpdateAsync()
        {
            var cache = await LoadCacheAsync();
            return (DateTime.UtcNow - cache.LastUpdateCheck).TotalHours >= 24;
        }

        public async Task<bool> ShouldCheckToolsAsync()
        {
            var cache = await LoadCacheAsync();
            return (DateTime.UtcNow - cache.LastToolCheck).TotalHours >= 24;
        }

        public async Task UpdateCacheAfterUpdateCheckAsync()
        {
            var cache = await LoadCacheAsync();
            cache.LastUpdateCheck = DateTime.UtcNow;
            await SaveCacheAsync(cache);
        }

        public async Task UpdateCacheAfterToolCheckAsync(string? ytDlpVersion, bool ffmpegExists, bool ffprobeExists)
        {
            var cache = await LoadCacheAsync();
            cache.LastToolCheck = DateTime.UtcNow;
            cache.YtDlpVersion = ytDlpVersion;
            cache.FfmpegExists = ffmpegExists;
            cache.FfprobeExists = ffprobeExists;
            await SaveCacheAsync(cache);
        }

        private async Task<StartupCache> LoadCacheAsync()
        {
            if (!File.Exists(CacheFilePath))
                return new StartupCache();

            try
            {
                var json = await File.ReadAllTextAsync(CacheFilePath);
                return JsonSerializer.Deserialize<StartupCache>(json) ?? new StartupCache();
            }
            catch
            {
                return new StartupCache();
            }
        }

        private async Task SaveCacheAsync(StartupCache cache)
        {
            try
            {
                var dir = Path.GetDirectoryName(CacheFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(cache);
                await File.WriteAllTextAsync(CacheFilePath, json);
            }
            catch
            {
            }
        }
    }
}
