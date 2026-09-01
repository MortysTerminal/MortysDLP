using MortysDLP.Helpers;
using MortysDLP.Models;
using System.IO;
using System.Text.Json;

namespace MortysDLP.Services
{
    public static class DownloadHistoryService
    {
        /// <summary>Nur für Tests überschreibbar.</summary>
        internal static string HistoryPath { get; set; } = AppPaths.HistoryFile;

        private static short MaxEntries =>
            (short)Math.Max((short)1, Properties.Settings.Default.DownloadHistoryFileMaxEntries);

        private static readonly SemaphoreSlim _lock = new(1, 1);

        public static async Task ClearAsync()
        {
            await _lock.WaitAsync();
            try
            {
                await WriteAtomicAsync("[]");
            }
            catch (Exception ex) when (IsRecoverable(ex))
            {
                Log.Warn("Fehler beim Löschen der Historie", ex);
            }
            finally
            {
                _lock.Release();
            }
        }

        public static async Task<List<DownloadHistoryEntry>> LoadAsync()
        {
            await _lock.WaitAsync();
            try
            {
                return await LoadInternalAsync();
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
            catch (Exception ex) when (IsRecoverable(ex))
            {
                Log.Warn("Fehler beim Speichern der Historie", ex);
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
            catch (Exception ex) when (IsRecoverable(ex))
            {
                Log.Warn("Fehler beim Speichern der Historie", ex);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>Liest den Verlauf. Muss immer innerhalb von <see cref="_lock"/> aufgerufen
        /// werden, damit die Existenzprüfung nicht mit einem gleichzeitigen Schreibvorgang
        /// wettläuft. Fängt defektes JSON und alle üblichen Dateizugriffsfehler ab.</summary>
        private static async Task<List<DownloadHistoryEntry>> LoadInternalAsync()
        {
            if (!File.Exists(HistoryPath))
                return new List<DownloadHistoryEntry>();

            try
            {
                var json = await File.ReadAllTextAsync(HistoryPath);
                return JsonSerializer.Deserialize<List<DownloadHistoryEntry>>(json) ?? new List<DownloadHistoryEntry>();
            }
            catch (Exception ex) when (IsRecoverable(ex))
            {
                Log.Warn($"Verlauf nicht lesbar: {ex.Message}", ex);
                TryBackupCorruptFile();
                return new List<DownloadHistoryEntry>();
            }
        }

        private static async Task SaveInternalAsync(List<DownloadHistoryEntry> entries)
        {
            var trimmed = entries.OrderByDescending(e => e.DownloadedAt).Take(MaxEntries).ToList();
            var json = JsonSerializer.Serialize(trimmed, new JsonSerializerOptions { WriteIndented = true });
            await WriteAtomicAsync(json);
        }

        /// <summary>Schreibt atomar: erst in eine temporäre Datei, dann Umbenennen. Ein Absturz
        /// oder Fehler während des Schreibens kann so die bestehende Datei nie beschädigen.</summary>
        private static async Task WriteAtomicAsync(string content)
        {
            string tempPath = HistoryPath + ".tmp";
            try
            {
                await File.WriteAllTextAsync(tempPath, content);
                File.Move(tempPath, HistoryPath, overwrite: true);
            }
            catch
            {
                // Aufräumen der Temp-Datei darf still scheitern - der eigentliche Fehler
                // wird bereits weitergeworfen und vom Aufrufer protokolliert.
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                throw;
            }
        }

        /// <summary>Benennt eine nicht lesbare Verlaufsdatei um, statt sie zu verwerfen, damit
        /// eine Rettung von Hand möglich bleibt. Die Anwendung startet danach mit leerem
        /// Verlauf statt denselben Fehler bei jedem Öffnen erneut auszulösen.</summary>
        private static void TryBackupCorruptFile()
        {
            try
            {
                if (!File.Exists(HistoryPath)) return;

                string dir = Path.GetDirectoryName(HistoryPath) ?? AppPaths.DataDir;
                string backupPath = Path.Combine(dir, $"download_history.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}.json");
                File.Move(HistoryPath, backupPath, overwrite: true);
                Log.Info($"Defekte Verlaufsdatei gesichert: {backupPath}");

                // Nur jetzt kann überhaupt ein Überschuss entstehen - nicht erst beim
                // nächsten Start abwarten (der allgemeine Aufräumdurchgang ruft dieselbe
                // Funktion trotzdem noch einmal auf, für Überschuss aus einer Zeit vor
                // dieser Obergrenze).
                foreach (var entry in PruneCorruptBackups(dir))
                    Log.Info($"Alte Korruptions-Sicherung entfernt: {entry.Path}");
            }
            catch (Exception ex)
            {
                Log.Warn("Sicherung der defekten Verlaufsdatei fehlgeschlagen", ex);
            }
        }

        /// <summary>Entfernt alle bis auf die
        /// <see cref="Tools.ToolHousekeeping.CorruptHistoryBackupsToKeep"/> jüngsten
        /// Korruptions-Sicherungen — Korruption ist selten, aber ohne Obergrenze wächst die
        /// Zahl der Sicherungen über Jahre der Nutzung unbegrenzt weiter. Protokolliert selbst
        /// nichts (Muster wie <see cref="Tools.ToolHousekeeping"/>) — der Aufrufer entscheidet,
        /// wie die Zeilen aussehen.</summary>
        internal static List<Tools.CleanupEntry> PruneCorruptBackups(string directory)
        {
            var result = new List<Tools.CleanupEntry>();

            List<string> backups;
            try
            {
                backups = [.. Directory.EnumerateFiles(directory, "download_history.corrupt-*.json")
                    .OrderByDescending(File.GetLastWriteTimeUtc)];
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.Warn($"Korruptions-Sicherungen in '{directory}' nicht auflistbar: {ex.Message}");
                return result;
            }

            foreach (string path in backups.Skip(Tools.ToolHousekeeping.CorruptHistoryBackupsToKeep))
            {
                try
                {
                    long size = new FileInfo(path).Length;
                    File.Delete(path);
                    result.Add(new Tools.CleanupEntry(path, size,
                        $"überzählige Korruptions-Sicherung (mehr als die {Tools.ToolHousekeeping.CorruptHistoryBackupsToKeep} jüngsten)"));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Log.Warn($"Alte Korruptions-Sicherung '{path}' konnte nicht entfernt werden: {ex.Message}");
                }
            }

            return result;
        }

        private static bool IsRecoverable(Exception ex) =>
            ex is JsonException or IOException or UnauthorizedAccessException or DirectoryNotFoundException;
    }
}
