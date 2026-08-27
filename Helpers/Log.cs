using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;

namespace MortysDLP.Helpers
{
    internal enum LogLevel { Debug, Info, Warn, Error }

    /// <summary>
    /// Dateiprotokoll mit Rotation. Schreibt über einen einzelnen Hintergrund-Thread,
    /// damit Aufrufer nie auf Datenträger-I/O warten und nie kaputte, verschachtelte
    /// Zeilen entstehen. Darf unter keinen Umständen selbst eine Ausnahme werfen.
    /// </summary>
    internal static class Log
    {
        private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB
        private static readonly TimeSpan MaxAge = TimeSpan.FromDays(14);

        private static readonly BlockingCollection<Action> _queue = new();
        private static readonly Thread _writerThread;

        // Nur vom Schreiber-Thread berührt – braucht deshalb kein Lock.
        private static string? _openFilePath;
        private static StreamWriter? _writer;

        static Log()
        {
            _writerThread = new Thread(WriterLoop) { IsBackground = true, Name = "MortysDLP-Log" };
            _writerThread.Start();
        }

        public static LogLevel MinLevel { get; set; } =
            Properties.Settings.Default.DebugMode ? LogLevel.Debug : LogLevel.Info;

        /// <summary>Zielverzeichnis für Protokolldateien. Nur für Tests überschreibbar.</summary>
        internal static string LogsDirectory { get; set; } = AppPaths.LogsDir;

        public static string CurrentLogFile =>
            Path.Combine(LogsDirectory, $"mortysdlp-{DateTime.Now:yyyy-MM-dd}.log");

        public static void Debug(string message,
            [CallerMemberName] string? member = null, [CallerFilePath] string? file = null) =>
            Enqueue(LogLevel.Debug, message, null, member, file);

        public static void Info(string message,
            [CallerMemberName] string? member = null, [CallerFilePath] string? file = null) =>
            Enqueue(LogLevel.Info, message, null, member, file);

        public static void Warn(string message, Exception? ex = null,
            [CallerMemberName] string? member = null, [CallerFilePath] string? file = null) =>
            Enqueue(LogLevel.Warn, message, ex, member, file);

        public static void Error(string message, Exception? ex = null,
            [CallerMemberName] string? member = null, [CallerFilePath] string? file = null) =>
            Enqueue(LogLevel.Error, message, ex, member, file);

        /// <summary>Öffnet den Protokollordner im Explorer. Best-Effort.</summary>
        public static void OpenLogFolder()
        {
            try
            {
                Directory.CreateDirectory(LogsDirectory);
                Process.Start(new ProcessStartInfo { FileName = LogsDirectory, UseShellExecute = true });
            }
            catch { /* Best-Effort */ }
        }

        /// <summary>Wartet, bis alle bis jetzt eingereihten Zeilen geschrieben sind — für den
        /// nicht mehr zu rettenden Fall in AppDomain.UnhandledException ("Puffer leeren").</summary>
        public static void Flush(TimeSpan timeout)
        {
            using var done = new ManualResetEventSlim(false);
            try { _queue.Add(() => done.Set()); }
            catch { return; }
            try { done.Wait(timeout); } catch { /* Best-Effort */ }
        }

        /// <summary>Schließt die aktuell offene Protokolldatei. Nur für Tests, damit ihr
        /// Temp-Verzeichnis danach gefahrlos gelöscht werden kann.</summary>
        internal static void CloseForTests()
        {
            using var done = new ManualResetEventSlim(false);
            try { _queue.Add(() => { CloseWriter(); done.Set(); }); }
            catch { return; }
            try { done.Wait(TimeSpan.FromSeconds(5)); } catch { /* Best-Effort */ }
        }

        private static void Enqueue(LogLevel level, string message, Exception? ex, string? member, string? file)
        {
            if (level < MinLevel) return;

            string line = FormatLine(level, message, ex, member, file);
            try { _queue.Add(() => WriteLine(line)); }
            catch { /* Warteschlange geschlossen o.ä. – Protokollieren darf nie werfen */ }
        }

        private static string FormatLine(LogLevel level, string message, Exception? ex, string? member, string? file)
        {
            string levelTag = level switch
            {
                LogLevel.Debug => "DEBUG",
                LogLevel.Info => "INFO ",
                LogLevel.Warn => "WARN ",
                LogLevel.Error => "ERROR",
                _ => "?    "
            };

            // Path.GetFileNameWithoutExtension entfernt nur die letzte Endung – bei
            // "App.xaml.cs" bliebe "App.xaml" stehen. Deshalb bis zum ersten Punkt kürzen.
            string className = string.IsNullOrEmpty(file)
                ? "?"
                : Path.GetFileName(file).Split('.')[0];
            string context = string.IsNullOrEmpty(member) ? className : $"{className}.{member}";
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);

            string line = $"{timestamp} [{levelTag}] {context}: {message}";
            return ex != null ? line + Environment.NewLine + ex : line;
        }

        private static void WriterLoop()
        {
            foreach (var action in _queue.GetConsumingEnumerable())
            {
                try { action(); }
                catch { /* Ein einzelner Schreibfehler darf den Schreiber-Thread nie beenden */ }
            }
        }

        private static void WriteLine(string line)
        {
            try
            {
                Directory.CreateDirectory(LogsDirectory);

                string path = CurrentLogFile;
                if (_openFilePath != path)
                {
                    CloseWriter();
                    _openFilePath = path;
                }

                if (_writer == null)
                {
                    TryRotateOversizeFile(path, MaxFileSizeBytes);
                    _writer = new StreamWriter(
                        new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
                    { AutoFlush = false };
                    CleanupOldFiles(LogsDirectory, DateTime.Now, MaxAge);
                }

                _writer.WriteLine(line);
                _writer.Flush();

                if (new FileInfo(path).Length >= MaxFileSizeBytes)
                    CloseWriter(); // nächster Schreibvorgang rotiert und öffnet neu
            }
            catch { /* Protokollfehler dürfen die Anwendung nie beeinträchtigen */ }
        }

        private static void CloseWriter()
        {
            try { _writer?.Flush(); } catch { /* Best-Effort */ }
            try { _writer?.Dispose(); } catch { /* Best-Effort */ }
            _writer = null;
        }

        /// <summary>Benennt eine übergroße Datei um und macht so Platz für eine neue.
        /// Rein für sich testbar, ohne den Schreib-Thread zu berühren.</summary>
        internal static bool TryRotateOversizeFile(string filePath, long maxBytes)
        {
            try
            {
                if (!File.Exists(filePath)) return false;
                if (new FileInfo(filePath).Length < maxBytes) return false;

                string? dir = Path.GetDirectoryName(filePath);
                string rotatedName = Path.Combine(
                    dir ?? "",
                    $"{Path.GetFileNameWithoutExtension(filePath)}_{DateTime.Now:HHmmss}{Path.GetExtension(filePath)}");

                File.Move(filePath, rotatedName, overwrite: false);
                return true;
            }
            catch { return false; }
        }

        /// <summary>Löscht Protokolldateien, die älter als <paramref name="maxAge"/> sind.
        /// Rein für sich testbar, ohne den Schreib-Thread zu berühren.</summary>
        internal static void CleanupOldFiles(string directory, DateTime now, TimeSpan maxAge)
        {
            try
            {
                if (!Directory.Exists(directory)) return;

                foreach (string f in Directory.EnumerateFiles(directory, "mortysdlp-*.log"))
                {
                    try
                    {
                        if (now - File.GetLastWriteTime(f) > maxAge)
                            File.Delete(f);
                    }
                    catch { /* einzelne Datei überspringen */ }
                }
            }
            catch { /* Best-Effort */ }
        }
    }
}
