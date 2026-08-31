using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace MortysDLP.Services
{
    /// <summary>
    /// Hilfsfunktionen rund um das App-Update, die nichts mit der Versionsermittlung zu tun
    /// haben (die läuft über <c>Services/Releases/*</c> und
    /// <see cref="ToolCheckService"/>). Download und Prüfung übernimmt
    /// <see cref="VerifiedDownload"/>.
    /// </summary>
    internal class UpdateService
    {
        /// <summary>
        /// Ermittelt ein sicheres, beschreibbares temporäres Verzeichnis mit Fallback-Kandidaten.
        /// </summary>
        public static string GetSafeTempDirectory(string subFolder = "MortysDLP_Update")
        {
            string[] candidates =
            [
                Path.GetTempPath(),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                AppDomain.CurrentDomain.BaseDirectory
            ];

            foreach (var basePath in candidates)
            {
                if (string.IsNullOrWhiteSpace(basePath))
                    continue;

                try
                {
                    string dir = Path.Combine(basePath, subFolder);
                    Directory.CreateDirectory(dir);

                    // Schreibtest
                    string testFile = Path.Combine(dir, ".write_test");
                    File.WriteAllText(testFile, "test");
                    File.Delete(testFile);

                    return dir;
                }
                catch
                {
                    // Nächsten Kandidaten versuchen
                }
            }

            throw new IOException("Kein beschreibbares Verzeichnis für das Update gefunden.");
        }

        /// <summary>
        /// Prüft, ob die heruntergeladene ZIP-Datei lesbar ist und den erwarteten Haupteintrag
        /// enthält. Ersetzt die frühere "irgendeine.exe"-Prüfung: Ein zweites
        /// Asset im Release (Screenshot, portable Variante) ließ diese bisher fälschlich
        /// bestehen. Weitergehende Sicherheitsprüfungen (Zip-Slip, Eintragsanzahl,
        /// Gesamtgröße) macht der Updater beim Entpacken — hier geht es nur darum,
        /// ein offensichtlich unbrauchbares Paket vor dem Neustart zu erkennen.
        /// </summary>
        public static bool ValidateZipContainsMainExe(string zipPath, string mainExeName)
        {
            try
            {
                using var archive = ZipFile.OpenRead(zipPath);
                return archive.Entries.Any(e =>
                    string.Equals(e.Name, mainExeName, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }
    }
}
