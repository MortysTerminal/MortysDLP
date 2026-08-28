using MortysDLP.Helpers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MortysDLP.Services.Releases
{
    /// <summary>
    /// Wählt aus der Asset-Liste eines Releases das gemeinte Paket — ein Release kann mehrere
    /// Anhänge tragen (`checksums.txt`, eine portable Variante, ein Screenshot). Kennt nichts
    /// Repository-Spezifisches: Muster und die bevorzugte Auflösung bei Mehrdeutigkeit kommen
    /// vollständig aus dem übergebenen Muster, damit Welle 4 dieselbe Klasse für yt-dlp,
    /// ffmpeg usw. wiederverwenden kann. Siehe <c>werkstatt/tasks/W2-T07.md</c>.
    /// </summary>
    internal static class AssetSelector
    {
        private static readonly ConcurrentDictionary<string, Regex> PatternCache = new();

        /// <summary>
        /// Wählt anhand von <paramref name="pattern"/> (<c>*</c>/<c>?</c>-Platzhalter, ohne
        /// Groß-/Kleinschreibung, kulturunabhängig). <c>checksums.txt</c> kommt nie infrage.
        /// Genau ein Treffer gewinnt. Bei mehreren Treffern gewinnt die exakte Übereinstimmung
        /// mit dem Muster ohne Platzhalter (z. B. macht <c>"MortysDLP*.zip"</c> den Namen
        /// <c>"MortysDLP.zip"</c> zum bevorzugten Kandidaten); gibt es die nicht, wird
        /// <see cref="AssetAmbiguousException"/> geworfen — raten ist hier falsch. Kein
        /// Treffer liefert <c>null</c> und protokolliert die vorhandenen Namen.
        /// </summary>
        public static ReleaseAsset? Select(IReadOnlyList<ReleaseAsset> assets, string pattern)
        {
            var matches = assets
                .Where(a => !string.Equals(a.Name, "checksums.txt", StringComparison.OrdinalIgnoreCase))
                .Where(a => MatchesPattern(a.Name, pattern))
                .ToList();

            if (matches.Count == 0)
            {
                Log.Warn($"Kein Asset passt zum Muster '{pattern}'. Vorhandene Assets: " +
                    string.Join(", ", assets.Select(a => a.Name)));
                return null;
            }

            if (matches.Count == 1)
                return matches[0];

            string preferredName = pattern.Replace("*", "", StringComparison.Ordinal)
                .Replace("?", "", StringComparison.Ordinal);
            var preferred = matches.FirstOrDefault(a =>
                string.Equals(a.Name, preferredName, StringComparison.OrdinalIgnoreCase));

            if (preferred != null)
                return preferred;

            throw new AssetAmbiguousException(matches.Select(a => a.Name).ToList());
        }

        private static bool MatchesPattern(string name, string pattern)
        {
            var regex = PatternCache.GetOrAdd(pattern, p =>
            {
                string escaped = Regex.Escape(p)
                    .Replace(@"\*", ".*", StringComparison.Ordinal)
                    .Replace(@"\?", ".", StringComparison.Ordinal);
                return new Regex($"^{escaped}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            });

            return regex.IsMatch(name);
        }
    }

    /// <summary>Mehrere Assets passen zum Muster, ohne dass eine exakte Übereinstimmung mit
    /// dem platzhalterfreien Muster eine eindeutige Wahl ermöglicht.</summary>
    internal sealed class AssetAmbiguousException(IReadOnlyList<string> candidateNames)
        : Exception($"Mehrere Assets passen zum Muster, keine eindeutige Auswahl möglich: " +
            $"{string.Join(", ", candidateNames)}")
    {
        public IReadOnlyList<string> CandidateNames { get; } = candidateNames;
    }
}
