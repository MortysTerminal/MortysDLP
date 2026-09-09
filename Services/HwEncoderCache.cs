using MortysDLP.Helpers;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace MortysDLP.Services
{
    /// <summary>
    /// Merkt sich über Programmläufe hinweg, welcher H.264-GPU-Encoder auf diesem Rechner
    /// tatsächlich läuft (<see cref="AppPaths.HwEncoderCacheFile"/>). Gemerkt wird <b>nur ein
    /// Treffer</b> — ein Durchgang ohne ansprechbaren Encoder kann an einer vorübergehenden
    /// Ursache liegen (ffmpeg gerade beschäftigt, Treiber-Neuinstallation) und ist die
    /// dauerhafte Ablage nicht wert.
    ///
    /// <para>Der gemerkte Encoder ist eine Aussage über <i>dieses</i> ffmpeg, nicht über den
    /// Rechner: Nach einem ffmpeg-Update kann <c>h264_qsv</c> verschwinden oder dazukommen.
    /// Deshalb steht die ffmpeg-Version mit in der Datei und wird beim Lesen abgeglichen
    /// (<see cref="Matches"/>).</para>
    ///
    /// <para>Darf nie werfen: eine fehlende, gesperrte, defekte oder fremde Datei zählt wie
    /// „nichts gemerkt". Schreibt atomar (<c>.tmp</c> + <see cref="File.Move(string,string,bool)"/>)
    /// nach demselben Muster wie <see cref="UpdateCache"/>.</para>
    /// </summary>
    internal sealed class HwEncoderCache(string? filePath = null)
    {
        internal const int CurrentSchemaVersion = 1;

        private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

        private readonly string _filePath = filePath ?? AppPaths.HwEncoderCacheFile;

        /// <summary>Reine Prüfung ohne Dateisystemzugriff: Taugt der gemerkte Eintrag noch für
        /// das aktuelle ffmpeg? Ein Eintrag ohne Encoder oder ohne Versionsangabe zählt nicht,
        /// ebenso wenig eine unbekannte aktuelle Version.</summary>
        public static bool Matches(HwEncoderCacheEntry? entry, string? currentFfmpegVersion) =>
            entry is { Encoder.Length: > 0, FfmpegVersion.Length: > 0 } &&
            !string.IsNullOrEmpty(currentFfmpegVersion) &&
            string.Equals(entry.FfmpegVersion, currentFfmpegVersion, StringComparison.Ordinal);

        public async Task<HwEncoderCacheEntry?> ReadAsync(CancellationToken ct)
        {
            var data = await LoadAsync(ct);
            return data?.Entry;
        }

        public async Task WriteAsync(HwEncoderCacheEntry entry, CancellationToken ct)
        {
            var data = new HwEncoderCacheData { Entry = entry };
            await SaveAsync(data, ct);
        }

        /// <summary>Leert die Ablage. Für den Fall, dass der gemerkte Wert sofort überholt ist
        /// (etwa nach einem ffmpeg-Wechsel, den die Anwendung selbst angestoßen hat).</summary>
        public Task ClearAsync(CancellationToken ct)
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    File.Delete(_filePath);
                    Log.Info($"Encoder-Zwischenspeicher geleert: {_filePath}");
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.Warn("Encoder-Zwischenspeicher konnte nicht geleert werden.", ex);
            }

            return Task.CompletedTask;
        }

        private async Task<HwEncoderCacheData?> LoadAsync(CancellationToken ct)
        {
            try
            {
                if (!File.Exists(_filePath))
                    return null;

                string json = await File.ReadAllTextAsync(_filePath, ct);
                var data = JsonSerializer.Deserialize<HwEncoderCacheData>(json);

                if (data is null || data.SchemaVersion != CurrentSchemaVersion)
                    return null;

                return data;
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                Log.Warn("Encoder-Zwischenspeicher nicht lesbar - wird wie 'nichts gemerkt' behandelt.", ex);
                return null;
            }
        }

        private async Task SaveAsync(HwEncoderCacheData data, CancellationToken ct)
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
                Log.Warn("Encoder-Zwischenspeicher konnte nicht geschrieben werden.", ex);
            }
        }
    }

    internal sealed class HwEncoderCacheData
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; } = HwEncoderCache.CurrentSchemaVersion;

        [JsonPropertyName("entry")]
        public HwEncoderCacheEntry? Entry { get; set; }
    }

    internal sealed class HwEncoderCacheEntry
    {
        /// <summary>Der ffmpeg-Encoder-Bezeichner, z. B. <c>h264_nvenc</c>.</summary>
        [JsonPropertyName("encoder")]
        public string? Encoder { get; set; }

        /// <summary>Die ffmpeg-Version, gegen die geprüft wurde
        /// (z. B. <c>7.1-essentials_build-www.gyan.dev</c>). Passt sie beim Lesen nicht zum
        /// aktuell installierten ffmpeg, wird der Eintrag verworfen und neu geprüft.</summary>
        [JsonPropertyName("ffmpegVersion")]
        public string? FfmpegVersion { get; set; }

        [JsonPropertyName("detectedUtc")]
        public DateTimeOffset DetectedUtc { get; set; }
    }
}
