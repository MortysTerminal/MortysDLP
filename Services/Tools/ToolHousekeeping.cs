using MortysDLP.Helpers;
using System;
using System.Collections.Generic;
using System.IO;

namespace MortysDLP.Services.Tools
{
    /// <summary>Ein entferntes Element, für das Protokoll des Aufrufers — Pfad, Größe und
    /// warum es weg ist. Die Funktionen hier protokollieren selbst nichts (Vorgabe:
    /// <see cref="Log.CleanupOldFiles"/> macht es vor), damit sie sich rein gegen ein
    /// Temp-Verzeichnis testen lassen.</summary>
    internal readonly record struct CleanupEntry(string Path, long Bytes, string Reason);

    /// <summary>
    /// Räumt auf, was die Werkzeugverwaltung über die Zeit hinterlässt — Downloads, die nie
    /// fertig wurden, Sicherungen, die niemand mehr braucht, eigene Reste im Temp-Verzeichnis.
    /// Jede Funktion ist rein (Verzeichnis, aktuelle Zeit und Frist kommen herein, die Liste
    /// der entfernten Pfade geht heraus) und Best-Effort: Eine gesperrte oder nicht löschbare
    /// Datei wird übersprungen und protokolliert, nie geworfen — ein Aufräumdurchgang darf nie
    /// der Grund sein, warum die Anwendung nicht startet.
    ///
    /// <para>Die Fristen sind absichtlich großzügig gewählt: Lieber eine Spur zu lange
    /// aufheben als sie zu früh zu verlieren, wenn sie beim nächsten Fehlschlag gebraucht
    /// würde — besonders bei <c>.old</c>-Sicherungen, der einzigen Rückfallebene, wenn ein
    /// Werkzeug-Update erst Tage später als kaputt auffällt.</para>
    /// </summary>
    internal static class ToolHousekeeping
    {
        /// <summary>Abgebrochene Downloads (<see cref="VerifiedDownload"/>) jünger als das
        /// könnten zu einem gerade laufenden Vorgang gehören.</summary>
        public static readonly TimeSpan PartFileMaxAge = TimeSpan.FromHours(24);

        /// <summary>Sicherungen (<see cref="ToolInstaller.BackupSuffix"/>) sind die einzige
        /// Rückfallebene, wenn ein Update erst Tage später als kaputt auffällt — deshalb
        /// deutlich länger als die übrigen Fristen.</summary>
        public static readonly TimeSpan BackupFileMaxAge = TimeSpan.FromDays(7);

        /// <summary>Eigene Reste unter <see cref="AppPaths.ToolTempDir"/> — heute Zip-Pakete,
        /// die ein abgebrochener Vorgang zurückgelassen hat (das Herunterladen räumt sein
        /// eigenes Paket im Erfolgs- <b>und</b> im Fehlerfall selbst weg; nur ein harter
        /// Abbruch, z. B. Task-Manager oder Stromausfall, lässt eines zurück).</summary>
        public static readonly TimeSpan OwnTempResidueMaxAge = TimeSpan.FromHours(24);

        /// <summary>Wie viele Sicherungen einer defekten Verlaufsdatei erhalten bleiben.</summary>
        public const int CorruptHistoryBackupsToKeep = 3;

        /// <summary>Löscht Dateien nach Namensmuster, die älter als <paramref name="maxAge"/>
        /// sind — rekursiv, weil Werkzeuge und Modelle in Unterordnern liegen (z. B.
        /// <c>Tools\Whisper\models\</c>). Jüngere Dateien bleiben unangetastet.</summary>
        public static List<CleanupEntry> CleanupFilesByAge(
            string directory, string searchPattern, DateTime now, TimeSpan maxAge, string reason)
        {
            var deleted = new List<CleanupEntry>();
            if (!Directory.Exists(directory))
                return deleted;

            foreach (string path in Directory.EnumerateFiles(directory, searchPattern, SearchOption.AllDirectories))
            {
                try
                {
                    var info = new FileInfo(path);
                    if (now - info.LastWriteTime <= maxAge)
                        continue;

                    long size = info.Length;
                    File.Delete(path);
                    deleted.Add(new CleanupEntry(path, size, reason));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Log.Warn($"Aufräumen: '{path}' konnte nicht gelöscht werden: {ex.Message}");
                }
            }

            return deleted;
        }

        /// <summary>Entfernt verwaiste eigene Reste direkt unter <paramref name="tempDir"/> —
        /// heute nur Dateien (Zip-Pakete), aber auch ganze Ordner, falls eine ältere Fassung
        /// von MortysDLP dort noch einen vollständig entpackten Ordner hinterlassen hat. Der
        /// gesamte Inhalt von <paramref name="tempDir"/> gehört MortysDLP allein (siehe
        /// <see cref="AppPaths.ToolTempDir"/>) — fremde Temp-Inhalte liegen außerhalb und
        /// werden nicht angefasst.</summary>
        public static List<CleanupEntry> CleanupOwnTempResidue(string tempDir, DateTime now, TimeSpan maxAge)
        {
            var deleted = new List<CleanupEntry>();
            if (!Directory.Exists(tempDir))
                return deleted;

            foreach (string path in Directory.EnumerateFileSystemEntries(tempDir))
            {
                try
                {
                    bool isDirectory = Directory.Exists(path);
                    DateTime lastWrite = isDirectory
                        ? Directory.GetLastWriteTime(path)
                        : File.GetLastWriteTime(path);

                    if (now - lastWrite <= maxAge)
                        continue;

                    long size = isDirectory ? DirectorySize(path) : new FileInfo(path).Length;

                    if (isDirectory)
                        Directory.Delete(path, recursive: true);
                    else
                        File.Delete(path);

                    deleted.Add(new CleanupEntry(
                        path, size, "verwaister Rest einer Werkzeug-Installation, älter als 24 Stunden"));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Log.Warn($"Aufräumen: '{path}' konnte nicht entfernt werden: {ex.Message}");
                }
            }

            return deleted;
        }

        private static long DirectorySize(string directory)
        {
            long total = 0;

            try
            {
                foreach (string file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
                {
                    try { total += new FileInfo(file).Length; }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Größe ist nur für die Protokollzeile gedacht - schlägt das Zählen fehl,
                // löscht Directory.Delete trotzdem; nur die ausgewiesene Größe wird ungenau.
            }

            return total;
        }

        /// <summary>Alle Kategorien in einem Durchgang, für den Aufruf beim Start. Reihenfolge
        /// ist ohne Bedeutung — jede Kategorie ist unabhängig von den anderen.</summary>
        public static List<CleanupEntry> RunAll(DateTime now)
        {
            var result = new List<CleanupEntry>();

            result.AddRange(CleanupFilesByAge(
                AppPaths.ToolsDir, "*.part", now, PartFileMaxAge,
                "abgebrochener Download, älter als 24 Stunden"));

            result.AddRange(CleanupFilesByAge(
                AppPaths.ToolsDir, $"*{ToolInstaller.BackupSuffix}", now, BackupFileMaxAge,
                "Sicherung eines Werkzeug-Updates, älter als 7 Tage"));

            result.AddRange(CleanupOwnTempResidue(AppPaths.ToolTempDir, now, OwnTempResidueMaxAge));

            return result;
        }
    }
}
