using System;
using System.Collections.Generic;
using System.Linq;

namespace MortysDLP.Services.Tools
{
    /// <summary>
    /// Die eine Stelle, die festhält, welche Funktion der Anwendung auf welches externe Werkzeug
    /// angewiesen ist. Wird gebraucht von der Werkzeuge-Seite (Erforderlich-Abzeichen und
    /// „Für: …"-Zeile), von der Sperr-Karte auf den Funktionsseiten
    /// (<c>ToolMissingNotice</c>), von der Deinstallations-Rückfrage und vom Startablauf
    /// (<c>StartupWindow.BuildRequiredMessage</c>). Ohne diese Karte stünde dieselbe Zuordnung
    /// mehrfach hartkodiert an verschiedenen Orten.
    /// </summary>
    internal static class ToolFeatureMap
    {
        /// <summary>UIText-Schlüssel der Funktionen, die <paramref name="toolId"/> braucht —
        /// Reihenfolge = Anzeigereihenfolge.</summary>
        public static IReadOnlyList<string> FeatureKeys(string toolId) => toolId switch
        {
            "yt-dlp" => new[] { "Feature.Download", "Feature.Batch", "Feature.TwitchVideo" },
            "ffmpeg" => new[]
            {
                "Feature.Download", "Feature.Batch", "Feature.Convert",
                "Feature.Gif", "Feature.TwitchVideo", "Feature.Transcribe",
            },
            "whisper" => new[] { "Feature.Transcribe" },
            "twitch-downloader" => new[] { "Feature.TwitchChat" },
            _ => Array.Empty<string>(),
        };

        /// <summary>Anzeigename des Werkzeugs — spiegelt <c>IManagedTool.DisplayName</c> der
        /// jeweiligen Werkzeugklasse, hier ohne Werkzeug-Instanz verfügbar (die Sperr-Karte
        /// kennt nur die Id).</summary>
        public static string DisplayName(string toolId) => toolId switch
        {
            "yt-dlp" => "yt-dlp",
            "ffmpeg" => "ffmpeg / ffprobe",
            "whisper" => "whisper.cpp",
            "twitch-downloader" => "TwitchDownloaderCLI",
            _ => toolId,
        };

        /// <summary>Menschlich lesbare Aufzählung der betroffenen Funktionen, z. B.
        /// „Download, Stapel-Download, Konvertieren". <paramref name="translate"/> ist üblicherweise
        /// <c>UITextDictionary.Get</c>.</summary>
        public static string Describe(string toolId, Func<string, string> translate) =>
            string.Join(", ", FeatureKeys(toolId).Select(translate));
    }
}
