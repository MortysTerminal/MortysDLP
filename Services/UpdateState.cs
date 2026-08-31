using MortysDLP.Helpers;
using MortysDLP.Models;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace MortysDLP.Services
{
    /// <summary>Ergebnis der Auswertung eines zuvor angestoßenen Updates gegen die
    /// tatsächlich laufende Version.</summary>
    internal enum UpdateOutcome
    {
        /// <summary>Keine Zustandsdatei vorhanden — der Normalfall.</summary>
        None,
        /// <summary>Die laufende Version entspricht dem Ziel — das Update hat gewirkt.</summary>
        Succeeded,
        /// <summary>Die laufende Version entspricht weiterhin dem Ausgangspunkt — das Update
        /// hat nicht gewirkt.</summary>
        Failed,
        /// <summary>Weder Ziel- noch Ausgangsversion aktiv (Nutzer hat von Hand eingegriffen),
        /// oder die eigene Version ist unbekannt.</summary>
        Unclear,
        /// <summary>Der Eintrag ist älter als sieben Tage oder sein Zeitstempel liegt in der
        /// Zukunft — zu alt, um noch etwas zu bedeuten.</summary>
        Stale,
    }

    internal sealed class UpdateStateData
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; } = 1;

        [JsonPropertyName("fromVersion")]
        public string? FromVersion { get; set; }

        [JsonPropertyName("toVersion")]
        public string? ToVersion { get; set; }

        [JsonPropertyName("startedUtc")]
        public DateTimeOffset StartedUtc { get; set; }

        [JsonPropertyName("attempts")]
        public int Attempts { get; set; }

        /// <summary>Changelog-Text der Zielversion, mitgeschrieben beim Update-Start
        /// erspart nach einem erfolgreichen Neustart einen zweiten Netzabruf für den
        /// „Was ist neu"-Hinweis.</summary>
        [JsonPropertyName("changelog")]
        public string? Changelog { get; set; }

        /// <summary>Protokolldatei, die der Updater für diesen Versuch angelegt hat. Der Grund
        /// eines fehlgeschlagenen Updates (gesperrte Datei, voller Datenträger, Rollback) steht
        /// ausschließlich dort — das Protokoll der Anwendung endet beim Start des Updaters.
        /// Ohne diesen Pfad müsste der Nutzer erst das eine Protokoll öffnen, um den Namen des
        /// anderen zu finden.</summary>
        [JsonPropertyName("updaterLogPath")]
        public string? UpdaterLogPath { get; set; }
    }

    /// <summary>
    /// Der einzige Beleg dafür, dass ein Update tatsächlich angestoßen wurde — unter
    /// <see cref="AppPaths.UpdateStateFile"/>, bewusst **nicht** im Cache-Verzeichnis: Er ist
    /// kein Zwischenspeicher und darf nicht mit dem Cache gemeinsam weggeräumt werden. Darf nie
    /// werfen (fehlende/gesperrte/defekte/fremde Datei zählt wie „kein Zustand") und schreibt
    /// atomar (<c>.tmp</c> + <see cref="File.Move"/>).
    /// </summary>
    internal static class UpdateState
    {
        private const int CurrentSchemaVersion = 1;

        /// <summary>„Zwei Versuche" ist eine Abwägung, kein Naturgesetz: einer wäre zu streng
        /// (ein einzelner Absturz mitten im Update kann Zufall sein), drei eine Zumutung.</summary>
        internal const int MaxAttemptsBeforeBlocking = 2;

        private static readonly TimeSpan MaxAge = TimeSpan.FromDays(7);
        private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

        public static async Task<UpdateStateData?> ReadAsync(string? filePath = null, CancellationToken ct = default)
        {
            string path = filePath ?? AppPaths.UpdateStateFile;

            try
            {
                if (!File.Exists(path))
                    return null;

                string json = await File.ReadAllTextAsync(path, ct);
                var data = JsonSerializer.Deserialize<UpdateStateData>(json);

                if (data is null || data.SchemaVersion != CurrentSchemaVersion)
                    return null;

                return data;
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                Log.Warn("Update-Zustand nicht lesbar - wird wie 'kein Zustand' behandelt.", ex);
                return null;
            }
        }

        /// <summary>Zeichnet einen Update-Versuch auf. Existiert bereits ein Eintrag für
        /// dieselbe <paramref name="toVersion"/>, wird <c>attempts</c> erhöht statt neu
        /// angelegt; für eine andere Zielversion beginnt die Zählung wieder bei 1.</summary>
        public static async Task RecordAttemptAsync(
            string fromVersion, string toVersion, DateTimeOffset now,
            string? filePath = null, string? changelog = null, string? updaterLogPath = null,
            CancellationToken ct = default)
        {
            string path = filePath ?? AppPaths.UpdateStateFile;

            try
            {
                var existing = await ReadAsync(path, ct);
                int attempts = existing != null &&
                    string.Equals(existing.ToVersion, toVersion, StringComparison.OrdinalIgnoreCase)
                    ? existing.Attempts + 1
                    : 1;

                var data = new UpdateStateData
                {
                    FromVersion = fromVersion,
                    ToVersion = toVersion,
                    StartedUtc = now,
                    Attempts = attempts,
                    Changelog = changelog,
                    UpdaterLogPath = updaterLogPath,
                };

                await WriteAsync(data, path, ct);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.Warn("Update-Zustand konnte nicht geschrieben werden.", ex);
            }
        }

        public static Task DeleteAsync(string? filePath = null)
        {
            string path = filePath ?? AppPaths.UpdateStateFile;

            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    Log.Info($"Update-Zustand gelöscht: {path}");
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.Warn("Update-Zustand konnte nicht gelöscht werden.", ex);
            }

            return Task.CompletedTask;
        }

        private static async Task WriteAsync(UpdateStateData data, string path, CancellationToken ct)
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            string tmpPath = path + ".tmp";
            string json = JsonSerializer.Serialize(data, WriteOptions);
            await File.WriteAllTextAsync(tmpPath, json, ct);
            File.Move(tmpPath, path, overwrite: true);
        }

        /// <summary>Reine Klassifizierung, ohne Dateizugriff — testbar mit übergebener Zeit.
        /// Ein <paramref name="now"/> vor <c>startedUtc</c> (Zeitstempel aus der Zukunft) gilt
        /// wie ein zu alter Eintrag.</summary>
        public static UpdateOutcome Evaluate(UpdateStateData? state, AppVersion? current, DateTimeOffset now)
        {
            if (state is null)
                return UpdateOutcome.None;

            if (state.StartedUtc > now || now - state.StartedUtc > MaxAge)
                return UpdateOutcome.Stale;

            if (current is not { } currentVersion)
                return UpdateOutcome.Unclear;

            if (AppVersion.TryParse(state.ToVersion, out var to) && currentVersion.Equals(to))
                return UpdateOutcome.Succeeded;

            if (AppVersion.TryParse(state.FromVersion, out var from) && currentVersion.Equals(from))
                return UpdateOutcome.Failed;

            return UpdateOutcome.Unclear;
        }

        /// <summary>Schleifenschutz: Blockiert das automatische Angebot derselben Zielversion
        /// ab <see cref="MaxAttemptsBeforeBlocking"/> erfolglosen Versuchen. Eine tatsächlich
        /// neuere Version ist davon nicht betroffen. Reine Logik.</summary>
        public static bool IsBlocked(UpdateStateData? state, AppVersion targetVersion)
        {
            if (state is null)
                return false;

            return AppVersion.TryParse(state.ToVersion, out var to) &&
                to.Equals(targetVersion) &&
                state.Attempts >= MaxAttemptsBeforeBlocking;
        }
    }
}
