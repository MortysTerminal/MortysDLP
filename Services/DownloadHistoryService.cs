using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MortysDLP.Models;

namespace MortysDLP.Services
{
    public static class DownloadHistoryService
    {
        private static readonly string HistoryPath = "download_history.json";
        private const int MaxEntries = 30;

        public static List<DownloadHistoryEntry> Load()
        {
            if (!File.Exists(HistoryPath))
                return new List<DownloadHistoryEntry>();
            var json = File.ReadAllText(HistoryPath);
            return JsonSerializer.Deserialize<List<DownloadHistoryEntry>>(json) ?? new List<DownloadHistoryEntry>();
        }

        public static void Save(List<DownloadHistoryEntry> entries)
        {
            var trimmed = entries.OrderByDescending(e => e.DownloadedAt).Take(MaxEntries).ToList();
            var json = JsonSerializer.Serialize(trimmed, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(HistoryPath, json);
        }

        public static void Add(DownloadHistoryEntry entry)
        {
            var entries = Load();
            entries.Insert(0, entry);
            Save(entries);
        }

        public static void Clear()
        {
            File.WriteAllText(HistoryPath, "[]");
        }
    }
}
