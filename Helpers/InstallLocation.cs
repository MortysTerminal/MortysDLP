using System.IO;

namespace MortysDLP.Helpers
{
    internal enum InstallKind
    {
        Writable,           // Normalfall
        NeedsElevation,     // Program Files, Windows, ProgramData
        ReadOnly,           // Netzlaufwerk, schreibgeschütztes Medium
        RunningFromArchive  // Start aus der ZIP-Vorschau
    }

    internal sealed record InstallInfo(
        InstallKind Kind,
        string Path,
        bool CanSelfUpdate,
        bool CanUpdateTools,
        string ReasonKey);

    /// <summary>
    /// Bewertet, ob die Anwendung sich selbst und ihre Werkzeuge am aktuellen Installationsort
    /// aktualisieren kann. Reine Vorarbeit für den Update-Ablauf (Welle 3) — hier wird nur
    /// ermittelt und protokolliert, nicht bereits gehandelt.
    /// </summary>
    internal static class InstallLocation
    {
        private static readonly TimeSpan WriteTestTimeout = TimeSpan.FromSeconds(3);
        private const string WriteTestFileName = ".mdlp-write-test";

        // Ergebnis für den eigenen Installationsort wird für die Sitzung zwischengespeichert -
        // der Schreibtest greift auf den Datenträger zu und muss nicht wiederholt werden.
        private static InstallInfo? _cached;

        public static InstallInfo Analyze(string? path = null)
        {
            if (path == null)
                return _cached ??= AnalyzeCore(AppPaths.AppDir);

            return AnalyzeCore(path);
        }

        private static InstallInfo AnalyzeCore(string dir)
        {
            if (IsRunningFromArchive(dir))
                return new InstallInfo(InstallKind.RunningFromArchive, dir, false, false, "InstallLocation.Warning.Archive");

            if (IsProtectedSystemFolder(dir))
                return new InstallInfo(InstallKind.NeedsElevation, dir, false, false, "InstallLocation.Warning.Elevation");

            if (CanWriteTo(dir))
                return new InstallInfo(InstallKind.Writable, dir, true, true, "");

            return new InstallInfo(InstallKind.ReadOnly, dir, false, false, "InstallLocation.Warning.ReadOnly");
        }

        /// <summary>Erkennt, ob der Pfad auf einen Explorer-Temporärordner für die
        /// ZIP-Vorschau hinweist. Reine Pfadauswertung, kein Dateisystemzugriff.</summary>
        internal static bool IsRunningFromArchive(string path)
        {
            string fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);

            string tempPath = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
            if (fullPath.Equals(tempPath, StringComparison.OrdinalIgnoreCase) ||
                fullPath.StartsWith(tempPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return true;

            foreach (string segment in fullPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            {
                if (segment.StartsWith("Temp", StringComparison.OrdinalIgnoreCase) &&
                    segment.Length > 4 && char.IsDigit(segment[4]) && segment.Contains('_'))
                    return true;
                if (segment.StartsWith("7z", StringComparison.OrdinalIgnoreCase))
                    return true;
                if (segment.StartsWith("Rar$", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>Vergleicht gegen die zur Laufzeit aufgelösten Pfade der geschützten
        /// Systemordner — nicht über feste Zeichenketten, die auf anderssprachigen oder
        /// umgeleiteten Systemen nicht stimmen. Reine Pfadauswertung, kein
        /// Dateisystemzugriff.</summary>
        internal static bool IsProtectedSystemFolder(string path)
        {
            string fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);

            Environment.SpecialFolder[] protectedFolders =
            [
                Environment.SpecialFolder.ProgramFiles,
                Environment.SpecialFolder.ProgramFilesX86,
                Environment.SpecialFolder.Windows,
                Environment.SpecialFolder.CommonProgramFiles,
            ];

            foreach (var folder in protectedFolders)
            {
                string root = Environment.GetFolderPath(folder);
                if (string.IsNullOrEmpty(root)) continue;

                string normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
                if (fullPath.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase) ||
                    fullPath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>Legt eine winzige Testdatei an, schreibt und löscht sie wieder. Die
        /// einzig verlässliche Prüfung — Schreibrechte hängen von Gruppenrichtlinien,
        /// Vererbung und Laufwerkstyp ab, Attribute allein sagen zu wenig. Läuft mit
        /// Zeitlimit auf einem eigenen Thread, damit ein getrenntes Netzlaufwerk den Start
        /// nicht blockiert; bei Überschreitung gilt der Ort als nicht beschreibbar.</summary>
        private static bool CanWriteTo(string dir)
        {
            string testFile = Path.Combine(dir, WriteTestFileName);
            bool succeeded = false;

            var thread = new Thread(() =>
            {
                try
                {
                    File.WriteAllText(testFile, string.Empty);
                    succeeded = true;
                }
                catch
                {
                    succeeded = false;
                }
                finally
                {
                    // Aufräumen darf still scheitern - der Schreibtest selbst hat sein
                    // Ergebnis (succeeded) bereits festgehalten.
                    try { if (File.Exists(testFile)) File.Delete(testFile); } catch { }
                }
            })
            { IsBackground = true };

            thread.Start();
            bool completed = thread.Join(WriteTestTimeout);
            return completed && succeeded;
        }

        /// <summary>Kurzbeschreibung für das Startprotokoll, inkl. Dateisystem, Laufwerkstyp
        /// (nur bei Netzlaufwerk/Wechseldatenträger) und freiem Speicherplatz. Rein
        /// informativ — schlägt der Laufwerkszugriff fehl (z. B. getrenntes Netzlaufwerk),
        /// wird nur der Installationsort ohne Laufwerksdetails gemeldet.</summary>
        public static string DescribeForLog(InstallInfo info)
        {
            try
            {
                string? root = Path.GetPathRoot(info.Path);
                if (string.IsNullOrEmpty(root))
                    return $"{info.Path} ({info.Kind})";

                // UNC-Wurzeln (\\server\share) akzeptiert DriveInfo nicht - der Konstruktor
                // wirft ArgumentException. Der Laufwerkstyp ist hier aber ohnehin bekannt:
                // ein UNC-Pfad ist immer ein Netzlaufwerk. Freier Platz und Dateisystem
                // blieben ein P/Invoke fuer eine Protokollzeile, der bei nicht erreichbarer
                // Freigabe ins Zeitlimit laufen wuerde - "Network" ist die wertvolle Angabe.
                if (root.StartsWith(@"\\", StringComparison.Ordinal))
                    return $"{info.Path} ({info.Kind}, {DriveType.Network})";

                var drive = new DriveInfo(root);
                if (!drive.IsReady)
                    return $"{info.Path} ({info.Kind})";

                string typeNote = drive.DriveType is DriveType.Network or DriveType.Removable
                    ? $"{drive.DriveType}, "
                    : "";
                double freeGb = drive.AvailableFreeSpace / 1_073_741_824.0;

                return $"{info.Path} ({info.Kind}, {typeNote}{drive.DriveFormat}, {freeGb:F0} GB frei)";
            }
            catch
            {
                return $"{info.Path} ({info.Kind})";
            }
        }
    }
}
