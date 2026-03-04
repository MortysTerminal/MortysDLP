using MortysDLP.Models;
using System.IO;
using System.Text.Json;

namespace MortysDLP.Services
{
    public static class DownloadHistoryService
    {
        private static readonly string HistoryPath = Properties.Settings.Default.DownloadHistoryFileName;
        private static readonly short MaxEntries = Properties.Settings.Default.DownloadHistoryFileMaxEntries;
        private static readonly SemaphoreSlim _lock = new(1, 1);

        public static async Task ClearAsync()
        {
            await _lock.WaitAsync();
            try
            {
                await File.WriteAllTextAsync(HistoryPath, "[]");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler beim Löschen der Historie: {ex.Message}");
            }
            finally
            {
                _lock.Release();
            }
        }

        public static async Task<List<DownloadHistoryEntry>> LoadAsync()
        {
            if (!File.Exists(HistoryPath))
                return new List<DownloadHistoryEntry>();

            await _lock.WaitAsync();
            try
            {
                var json = await File.ReadAllTextAsync(HistoryPath);
                return JsonSerializer.Deserialize<List<DownloadHistoryEntry>>(json) ?? new List<DownloadHistoryEntry>();
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler beim Laden der Historie: {ex.Message}");
                return new List<DownloadHistoryEntry>();
            }
            finally
            {
                _lock.Release();
            }
        }

        internal static async Task AddAsync(DownloadHistoryEntry downloadHistoryEntry)
        {
            await _lock.WaitAsync();
            try
            {
                var entries = await LoadInternalAsync();
                entries.Insert(0, downloadHistoryEntry);
                await SaveInternalAsync(entries);
            }
            finally
            {
                _lock.Release();
            }
        }

        internal static async Task SaveAsync(List<DownloadHistoryEntry> entries)
        {
            await _lock.WaitAsync();
            try
            {
                await SaveInternalAsync(entries);
            }
            finally
            {
                _lock.Release();
            }
        }

        private static async Task<List<DownloadHistoryEntry>> LoadInternalAsync()
        {
            if (!File.Exists(HistoryPath))
                return new List<DownloadHistoryEntry>();

            var json = await File.ReadAllTextAsync(HistoryPath);
            return JsonSerializer.Deserialize<List<DownloadHistoryEntry>>(json) ?? new List<DownloadHistoryEntry>();
        }

        private static async Task SaveInternalAsync(List<DownloadHistoryEntry> entries)
        {
            var trimmed = entries.OrderByDescending(e => e.DownloadedAt).Take(MaxEntries).ToList();
            var json = JsonSerializer.Serialize(trimmed, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(HistoryPath, json);
        }
    }
}
