using MortysDLP.Models;
using System.Reflection;

namespace MortysDLP.Helpers
{
    /// <summary>
    /// Liest die eigene Anzeigeversion einmalig aus der Assembly. Einzige Pflegestelle ist
    /// <c>&lt;ReleaseVersion&gt;</c> in <c>MortysDLP.csproj</c> — von dort leitet der Build
    /// <see cref="AssemblyInformationalVersionAttribute"/> automatisch ab.
    /// </summary>
    internal static class AppInfo
    {
        /// <summary>Anzeigeversion, exakt wie der GitHub-Tag (z. B. "2026.06.01").
        /// <c>null</c>, wenn sie nicht ermittelbar ist — das kann nur durch einen
        /// fehlerhaften Build entstehen.</summary>
        public static string? Current { get; } = ReadInformationalVersion();

        /// <summary>Dieselbe Version als vergleichbarer Typ. <c>null</c> bedeutet: unbekannt —
        /// dann darf KEIN Update angeboten werden. Kein Rückfall auf "0.0.0": Das würde die
        /// Anwendung für älter als jedes Release halten und ihr ein Update anbieten, das
        /// nichts ändert.</summary>
        public static AppVersion? CurrentVersion { get; } = ParseCurrent();

        private static string? ReadInformationalVersion()
        {
            string? raw = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            return StripSourceLinkSuffix(raw);
        }

        /// <summary>Entfernt den von SourceLink angehängten Commit-Hash ("+&lt;sha&gt;") aus
        /// der Informationsversion. Eigenständig testbar, ohne auf das Assembly-Attribut
        /// angewiesen zu sein.</summary>
        internal static string? StripSourceLinkSuffix(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            int plus = raw.IndexOf('+');
            string trimmed = (plus >= 0 ? raw[..plus] : raw).Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }

        private static AppVersion? ParseCurrent()
        {
            if (Current is null)
                return null;

            return AppVersion.TryParse(Current, out var version) ? version : null;
        }
    }
}
