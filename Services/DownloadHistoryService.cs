using MortysDLP.Models;
using System.IO;
using System.Text.Json;

namespace MortysDLP.Services
{
    public static class DownloadHistoryService
    {
        private static readonly string HistoryPath = Properties.Settings.Default.DownloadHistoryFileName;
        private static short MaxEntries = Properties.Settings.Default.DownloadHistoryFileMaxEntries;

        public static async Task ClearAsync()
        {
            await File.WriteAllTextAsync(HistoryPath, "[]");
        }

        public static async Task<List<DownloadHistoryEntry>> LoadAsync()
        {
            if (!File.Exists(HistoryPath))
                return new List<DownloadHistoryEntry>();
            var json = await File.ReadAllTextAsync(HistoryPath);
            return JsonSerializer.Deserialize<List<DownloadHistoryEntry>>(json) ?? new List<DownloadHistoryEntry>();
        }

        internal static async Task AddAsync(DownloadHistoryEntry downloadHistoryEntry)
        {
            var entries = await LoadAsync();
            entries.Insert(0, downloadHistoryEntry);
            await SaveAsync(entries);
        }

        internal static async Task SaveAsync(List<DownloadHistoryEntry> entries)
        {
            var trimmed = entries.OrderByDescending(e => e.DownloadedAt).Take(MaxEntries).ToList();
            var json = JsonSerializer.Serialize(trimmed, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(HistoryPath, json);
        }
    }
}
