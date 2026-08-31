using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MortysDLP.Helpers
{
    /// <summary>
    /// Übernimmt einmalig Werkzeuge aus einem alten Ablageort in einen neuen. Reine Funktion:
    /// Quell- und Zielordner kommen als Parameter herein, es wird nicht protokolliert — das
    /// erledigt der Aufrufer anhand des Ergebnisses (gleiches Muster wie
    /// <see cref="Log.CleanupOldFiles"/>).
    /// </summary>
    internal static class ToolsMigration
    {
        /// <summary><c>MigratedFiles</c> wurden verschoben, im alten Ordner existieren sie
        /// nicht mehr. <c>DuplicatedFiles</c> konnten nicht verschoben werden (Datei gesperrt,
        /// Programmordner schreibgeschützt) und wurden stattdessen kopiert — die alte Datei
        /// bleibt bewusst liegen, es existieren jetzt zwei Kopien. <c>FailedFiles</c> konnten
        /// weder verschoben noch kopiert werden, oder im Zielordner lag bereits eine Datei
        /// gleichen Namens; sie bleiben unangetastet im alten Ordner. <c>OldDirRemoved</c>
        /// ist nur dann wahr, wenn der alte Ordner nach der Übernahme vollständig leer war
        /// und deshalb entfernt wurde.</summary>
        internal sealed record MigrationResult(
            IReadOnlyList<string> MigratedFiles,
            IReadOnlyList<string> DuplicatedFiles,
            IReadOnlyList<string> FailedFiles,
            bool OldDirRemoved)
        {
            internal static readonly MigrationResult Empty = new([], [], [], false);
        }

        /// <summary>Übernimmt alle Dateien aus <paramref name="oldDir"/> nach
        /// <paramref name="newDir"/>. Prüft zuerst nur, ob <paramref name="oldDir"/>
        /// überhaupt existiert — gibt es nichts zu übernehmen, kostet der Aufruf keine
        /// nennenswerte Zeit. Repo-/Installations-Marker ohne Werkzeugbedeutung (Dateien, die
        /// mit einem Punkt beginnen, z. B. <c>.gitkeep</c>) werden übersprungen und
        /// aufgeräumt, damit sie die Erkennung „alter Ordner ist jetzt leer" nicht dauerhaft
        /// blockieren.</summary>
        internal static MigrationResult Migrate(string oldDir, string newDir)
        {
            if (!Directory.Exists(oldDir))
                return MigrationResult.Empty;

            var migrated = new List<string>();
            var duplicated = new List<string>();
            var failed = new List<string>();

            foreach (string oldFile in Directory.EnumerateFiles(oldDir, "*", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(oldFile).StartsWith('.'))
                {
                    // Kein Werkzeug, sondern ein Repo-Marker (.gitkeep) aus einer älteren
                    // Installation - löschen, damit er den alten Ordner nicht dauerhaft
                    // "nicht leer" hält. Scheitert das, ist es kein Problem: der Ordner bleibt
                    // dann eben stehen, wie bei jedem anderen Rest auch.
                    try { File.Delete(oldFile); } catch { /* Best-Effort */ }
                    continue;
                }

                string relative = Path.GetRelativePath(oldDir, oldFile);
                string newFile = Path.Combine(newDir, relative);

                if (File.Exists(newFile))
                {
                    // Zieldatei existiert schon - z. B. Rest eines vorherigen Teil-Laufs oder
                    // ein zwischenzeitlich regulär heruntergeladenes Werkzeug. Nicht automatisch
                    // entscheiden, welche Version gilt - das ist ein echter Rest.
                    failed.Add(relative);
                    continue;
                }

                try
                {
                    string? newFileDir = Path.GetDirectoryName(newFile);
                    if (!string.IsNullOrEmpty(newFileDir))
                        Directory.CreateDirectory(newFileDir);

                    File.Move(oldFile, newFile);
                    migrated.Add(relative);
                }
                catch
                {
                    try
                    {
                        File.Copy(oldFile, newFile, overwrite: false);
                        duplicated.Add(relative);
                    }
                    catch
                    {
                        try { if (File.Exists(newFile)) File.Delete(newFile); } catch { /* Best-Effort */ }
                        failed.Add(relative);
                    }
                }
            }

            bool oldDirRemoved = TryRemoveIfEmpty(oldDir);
            return new MigrationResult(migrated, duplicated, failed, oldDirRemoved);
        }

        private static bool TryRemoveIfEmpty(string dir)
        {
            try
            {
                RemoveEmptySubdirectories(dir);
                if (Directory.EnumerateFileSystemEntries(dir).Any())
                    return false;

                Directory.Delete(dir);
                return true;
            }
            catch { return false; }
        }

        private static void RemoveEmptySubdirectories(string dir)
        {
            foreach (string sub in Directory.EnumerateDirectories(dir))
            {
                RemoveEmptySubdirectories(sub);
                try
                {
                    if (!Directory.EnumerateFileSystemEntries(sub).Any())
                        Directory.Delete(sub);
                }
                catch { /* Best-Effort - nächster Start versucht es erneut */ }
            }
        }
    }
}
