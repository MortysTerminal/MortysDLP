using System;
using System.Collections.Generic;
using System.Linq;

namespace MortysDLP.Services.Releases
{
    /// <summary>
    /// Liest eine <c>checksums.txt</c> im Format <c>&lt;sha256&gt;  &lt;dateiname&gt;</c> je
    /// Zeile — das Format von <c>sha256sum</c> bzw. <c>Get-FileHash</c>. Wirft nie: eine
    /// ungültige Zeile wird übersprungen, keine gültige Zeile heißt „keine Prüfsumme
    /// bekannt", nicht „Datei kaputt".
    /// </summary>
    internal static class ChecksumFile
    {
        /// <summary>Bildet Dateiname → SHA-256 (klein geschrieben) ab. Toleriert beliebigen
        /// Leerraum als Trenner, das <c>*dateiname</c>-Binärmodus-Präfix von
        /// <c>sha256sum</c>, Leerzeilen, <c>#</c>-Kommentare und sowohl CRLF als auch LF.
        /// Dateinamen werden ohne Groß-/Kleinschreibung verglichen.</summary>
        public static IReadOnlyDictionary<string, string> Parse(string content)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(content))
                return result;

            foreach (string rawLine in content.Split('\n'))
            {
                string line = rawLine.TrimEnd('\r').Trim();
                if (line.Length == 0 || line[0] == '#')
                    continue;

                int splitIndex = -1;
                for (int i = 0; i < line.Length; i++)
                {
                    if (char.IsWhiteSpace(line[i]))
                    {
                        splitIndex = i;
                        break;
                    }
                }

                if (splitIndex < 0)
                    continue;

                string sha = line[..splitIndex];
                if (!IsValidSha256(sha))
                    continue;

                string rest = line[splitIndex..].TrimStart();
                string fileName = rest.Length > 0 && rest[0] == '*' ? rest[1..] : rest;
                if (fileName.Length == 0)
                    continue;

                result[fileName] = sha.ToLowerInvariant();
            }

            return result;
        }

        /// <summary>Wie <see cref="Parse"/>, liefert aber direkt die Prüfsumme für
        /// <paramref name="fileName"/> — oder <c>null</c>, wenn keine dort steht.</summary>
        public static string? Find(string content, string fileName)
        {
            var entries = Parse(content);
            return entries.TryGetValue(fileName, out var sha) ? sha : null;
        }

        private static bool IsValidSha256(string value) =>
            value.Length == 64 && value.All(Uri.IsHexDigit);
    }
}
