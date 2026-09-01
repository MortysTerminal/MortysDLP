using System.IO;
using System.Linq;
using System.Text;

namespace MortysDLP.Helpers
{
    /// <summary>
    /// Zentrale Anlaufstelle für alle Pfade der Anwendung. Löst niemals gegen das
    /// Arbeitsverzeichnis auf, sondern immer gegen <see cref="AppContext.BaseDirectory"/>
    /// bzw. das lokale Nutzerprofil.
    /// </summary>
    internal static class AppPaths
    {
        private static readonly string[] ReservedNames =
        [
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        ];

        /// <summary>Verzeichnis der Programmdateien. Immer nur lesend verwenden.</summary>
        public static string AppDir { get; } =
            AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);

        /// <summary>Verzeichnis für Nutzerdaten. Immer beschreibbar.</summary>
        public static string DataDir { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MortysDLP");

        /// <summary>Werkzeuge liegen unter dem Nutzerprofil, nicht im Programmverzeichnis —
        /// sonst scheitert jedes Werkzeug-Update, sobald jemand nach
        /// <c>C:\Program Files</c> entpackt (siehe <see cref="EnsureToolsDirAndMigrate"/>).</summary>
        public static string ToolsDir => Path.Combine(DataDir, "Tools");

        /// <summary>Alter Ablageort vor Welle 4. Nur noch als Quelle für die einmalige
        /// Übernahme vorhandener Werkzeuge relevant, siehe <see cref="EnsureToolsDirAndMigrate"/>.</summary>
        public static string LegacyToolsDir => Path.Combine(AppDir, "Tools");

        public static string LogsDir => Path.Combine(DataDir, "logs");
        public static string CacheDir => Path.Combine(DataDir, "cache");

        /// <summary>Eigener, exklusiver Ordner unter dem System-Temp-Verzeichnis für
        /// Werkzeug-Pakete während der Installation (<c>ffmpeg-*.zip</c> usw.) — nicht zu
        /// verwechseln mit dem Temp-Ordner des Selbst-Updates der Anwendung
        /// (<c>UpdateService.GetSafeTempDirectory</c>, anders benannt). Alles darin gehört
        /// MortysDLP; ein Aufräumdurchgang darf hier löschen, ohne fremde Temp-Inhalte zu
        /// gefährden.</summary>
        public static string ToolTempDir => Path.Combine(Path.GetTempPath(), "MortysDLP");

        public static string Tool(string exeName) => Path.Combine(ToolsDir, exeName);

        public static string YtDlp => Tool("yt-dlp.exe");
        public static string Ffmpeg => Tool("ffmpeg.exe");
        public static string Ffprobe => Tool("ffprobe.exe");
        public static string Whisper => Path.Combine(ToolsDir, "Whisper", "whisper.exe");
        public static string WhisperModels => Path.Combine(ToolsDir, "Whisper", "models");
        public static string TwitchCli => Tool("TwitchDownloaderCLI.exe");

        public static string HistoryFile => Path.Combine(DataDir, "download_history.json");

        /// <summary>Zwischenspeicher der Update-Prüfung. Bewusst unter
        /// <see cref="CacheDir"/> statt direkt unter <see cref="DataDir"/> — alle
        /// Zwischenspeicher sollen an einer Stelle liegen, damit sie sich gemeinsam leeren
        /// lassen, ohne dabei Belege wie den Update-Zustand mitzunehmen.</summary>
        public static string UpdateCacheFile => Path.Combine(CacheDir, "update-cache.json");

        /// <summary>Beleg dafür, dass ein Update tatsächlich angestoßen wurde.
        /// Bewusst direkt unter <see cref="DataDir"/>, **nicht** unter <see cref="CacheDir"/>:
        /// Anders als ein Zwischenspeicher darf diese Datei nicht mit dem Cache gemeinsam
        /// weggeräumt werden — sie ist der einzige Weg, ein fehlgeschlagenes von einem
        /// erfolgreichen Update zu unterscheiden.</summary>
        public static string UpdateStateFile => Path.Combine(DataDir, "update-state.json");

        /// <summary>Legt die Nutzerverzeichnisse an und übernimmt einen vorhandenen
        /// Verlauf vom alten Speicherort. Beim Start einmal aufrufen.</summary>
        public static void EnsureDataDirs()
        {
            try { Directory.CreateDirectory(DataDir); } catch { /* Best-Effort */ }
            try { Directory.CreateDirectory(LogsDir); } catch { /* Best-Effort */ }
            try { Directory.CreateDirectory(CacheDir); } catch { /* Best-Effort */ }

            MigrateHistoryFileIfNeeded();
            RemoveObsoleteStartupCacheIfPresent();
        }

        /// <summary>Legt <see cref="ToolsDir"/> an und übernimmt vorhandene Werkzeuge aus
        /// <see cref="LegacyToolsDir"/>, falls dort noch welche liegen. Bewusst **nicht**
        /// Teil von <see cref="EnsureDataDirs"/>: Diese läuft, bevor der Startbildschirm
        /// existiert, hier soll der Aufrufer aber bei einer spürbar langen Übernahme (große
        /// Whisper-Modelle über Laufwerksgrenzen hinweg) eine Statuszeile zeigen können,
        /// bevor er blockiert — siehe <c>StartupWindow.MigrateToolsAsync</c>.</summary>
        public static ToolsMigration.MigrationResult EnsureToolsDirAndMigrate()
        {
            try { Directory.CreateDirectory(ToolsDir); } catch { /* Best-Effort */ }
            return ToolsMigration.Migrate(LegacyToolsDir, ToolsDir);
        }

        /// <summary>Reine Pfadauswertung, kein Dateisystemzugriff — für den Aufrufer, der vor
        /// einer möglicherweise langen Übernahme entscheiden muss, ob überhaupt eine
        /// Statuszeile nötig ist.</summary>
        public static bool LegacyToolsDirHasContent()
        {
            try
            {
                return Directory.Exists(LegacyToolsDir) &&
                    Directory.EnumerateFiles(LegacyToolsDir, "*", SearchOption.AllDirectories)
                        .Any(f => !Path.GetFileName(f).StartsWith('.'));
            }
            catch { return false; }
        }

        /// <summary>Der nie instanziierte <c>StartupHealthCheckService</c> ist
        /// entfernt worden - eine vorhandene Datei aus früheren Installationen ist ab jetzt
        /// bedeutungslos und würde bei der Fehlersuche nur verwirren.</summary>
        private static void RemoveObsoleteStartupCacheIfPresent()
        {
            try
            {
                string path = Path.Combine(DataDir, "startup_cache.json");
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch { /* Best-Effort */ }
        }

        private static void MigrateHistoryFileIfNeeded()
        {
            if (File.Exists(HistoryFile)) return;

            foreach (string oldDir in new[] { AppDir, Directory.GetCurrentDirectory() })
            {
                string oldPath = Path.Combine(oldDir, "download_history.json");
                if (!File.Exists(oldPath)) continue;

                try { File.Move(oldPath, HistoryFile); }
                catch { /* Fehlschlag ist unkritisch – Verlauf startet dann leer. */ }
                return;
            }
        }

        /// <summary>Bereinigt einen Dateinamen für Windows: ungültige Zeichen,
        /// reservierte Namen (CON, NUL, COM1 …), abschließende Punkte/Leerzeichen,
        /// Längenbegrenzung.</summary>
        public static string SanitizeFileName(string name, int maxLength = 150)
        {
            if (string.IsNullOrWhiteSpace(name)) return "_";

            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(name.Length);
            foreach (char c in name)
                sb.Append(invalid.Contains(c) ? '_' : c);

            string cleaned = TrimTrailingDotsAndSpaces(sb.ToString().Trim());
            if (cleaned.Length == 0) cleaned = "_";

            if (cleaned.Length > maxLength)
                cleaned = TrimTrailingDotsAndSpaces(cleaned[..maxLength]);
            if (cleaned.Length == 0) cleaned = "_";

            string ext = Path.GetExtension(cleaned);
            string baseName = cleaned[..^ext.Length];
            if (Array.IndexOf(ReservedNames, baseName.ToUpperInvariant()) >= 0)
                cleaned = baseName + "_" + ext;

            return cleaned;
        }

        private static string TrimTrailingDotsAndSpaces(string s) => s.TrimEnd('.', ' ');
    }
}
