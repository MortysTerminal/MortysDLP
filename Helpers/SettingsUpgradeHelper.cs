using System.IO;

namespace MortysDLP.Helpers
{
    /// <summary>
    /// Reine Verzeichnislogik rund um <c>Settings.Default.Upgrade()</c> — testbar ohne echte
    /// <c>user.config</c>. Die eigentliche Übernahme erledigt weiterhin
    /// <c>ApplicationSettingsBase.Upgrade()</c> selbst; diese Klasse ermittelt nur, aus welchem
    /// Verzeichnis sie vermutlich stammt, für die Protokollzeile in <c>App.OnStartup</c> (siehe
    /// <c>werkstatt/04-UPDATE-ARCHITEKTUR.md</c>, Abschnitt 11.1).
    /// </summary>
    internal static class SettingsUpgradeHelper
    {
        /// <summary>
        /// Sucht unterhalb von <paramref name="hashDir"/> (dem Ordner
        /// <c>&lt;Exe&gt;_Url_&lt;hash&gt;</c>, der die Versionsunterordner enthält) die höchste
        /// Versionsnummer, die kleiner ist als <paramref name="currentVersion"/> — dieselbe
        /// Regel, nach der <c>ApplicationSettingsBase.Upgrade()</c> die Vorgängerversion sucht.
        /// Liefert <c>null</c>, wenn der Ordner fehlt oder keine passende Unterversion existiert.
        /// </summary>
        internal static string? FindPreviousVersionDirectory(string hashDir, Version currentVersion)
        {
            if (!Directory.Exists(hashDir))
                return null;

            string? best = null;
            Version? bestVersion = null;

            foreach (string dir in Directory.EnumerateDirectories(hashDir))
            {
                if (!Version.TryParse(Path.GetFileName(dir), out var candidate))
                    continue;

                if (candidate >= currentVersion)
                    continue;

                if (bestVersion == null || candidate > bestVersion)
                {
                    bestVersion = candidate;
                    best = dir;
                }
            }

            return best;
        }
    }
}
