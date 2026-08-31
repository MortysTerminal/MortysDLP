using MortysDLP.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace MortysDLP.Services
{
    /// <summary>
    /// Liest und schreibt den Zwischenspeicher der Update-Prüfung
    /// (<see cref="AppPaths.UpdateCacheFile"/>). Darf nie werfen: Eine fehlende, gesperrte,
    /// defekte oder fremde Datei zählt wie „kein Zwischenspeicher" — eine fehlgeschlagene
    /// Update-Prüfung darf höchstens eine überflüssige Netzabfrage kosten, nie einen
    /// Startfehler. Schreibt atomar (<c>.tmp</c> + <see cref="File.Move"/>), damit ein
    /// Abbruch mittendrin nie eine defekte Datei hinterlässt.
    /// </summary>
    internal sealed class UpdateCache(string? filePath = null)
    {
        private const int CurrentSchemaVersion = 1;

        private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

        private readonly string _filePath = filePath ?? AppPaths.UpdateCacheFile;

        public async Task<UpdateCacheEntry?> ReadAsync(string key, CancellationToken ct)
        {
            var data = await LoadAsync(ct);
            return data != null && data.Entries.TryGetValue(key, out var entry) ? entry : null;
        }

        public async Task WriteAsync(string key, UpdateCacheEntry entry, CancellationToken ct)
        {
            var data = await LoadAsync(ct) ?? new UpdateCacheData();
            data.Entries[key] = entry;
            await SaveAsync(data, ct);
        }

        /// <summary>Leert den gesamten Zwischenspeicher (alle Schlüssel). Für den Fall, dass
        /// ein Wert sofort überholt ist, statt erst nach Ablauf seiner Laufzeit — z. B. direkt
        /// nach einem tatsächlich durchgeführten Update.</summary>
        public Task ClearAsync(CancellationToken ct)
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    File.Delete(_filePath);
                    Log.Info($"Update-Zwischenspeicher geleert: {_filePath}");
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.Warn("Update-Zwischenspeicher konnte nicht geleert werden.", ex);
            }

            return Task.CompletedTask;
        }

        private async Task<UpdateCacheData?> LoadAsync(CancellationToken ct)
        {
            try
            {
                if (!File.Exists(_filePath))
                    return null;

                string json = await File.ReadAllTextAsync(_filePath, ct);
                var data = JsonSerializer.Deserialize<UpdateCacheData>(json);

                if (data is null || data.SchemaVersion != CurrentSchemaVersion)
                    return null;

                return data;
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                Log.Warn("Update-Zwischenspeicher nicht lesbar - wird wie 'kein Zwischenspeicher' behandelt.", ex);
                return null;
            }
        }

        private async Task SaveAsync(UpdateCacheData data, CancellationToken ct)
        {
            try
            {
                string? dir = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                string tmpPath = _filePath + ".tmp";
                string json = JsonSerializer.Serialize(data, WriteOptions);
                await File.WriteAllTextAsync(tmpPath, json, ct);
                File.Move(tmpPath, _filePath, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.Warn("Update-Zwischenspeicher konnte nicht geschrieben werden.", ex);
            }
        }
    }

    internal sealed class UpdateCacheData
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; } = 1;

        [JsonPropertyName("entries")]
        public Dictionary<string, UpdateCacheEntry> Entries { get; set; } = [];
    }

    /// <summary>Ein einzelner Eintrag, z. B. unter dem Schlüssel <c>"app"</c>. Das Format ist
    /// für weitere Schlüssel (Werkzeuge, Welle 4) vorbereitet, wird aber vorerst nur für
    /// <c>"app"</c> befüllt.</summary>
    internal sealed class UpdateCacheEntry
    {
        [JsonPropertyName("checkedUtc")]
        public DateTimeOffset CheckedUtc { get; set; }

        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("downloadUrl")]
        public string? DownloadUrl { get; set; }

        [JsonPropertyName("changelog")]
        public string? Changelog { get; set; }

        [JsonPropertyName("etag")]
        public string? ETag { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }
    }
}
