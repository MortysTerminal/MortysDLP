using MortysDLP.Helpers;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace MortysDLP.Services
{
    /// <summary>
    /// Baut den <c>-o</c>-Wert für yt-dlp so, dass er unter Windows sicher funktioniert — für alle
    /// drei Seiten, die yt-dlp aufrufen, an genau einer Stelle.
    ///
    /// <list type="bullet">
    /// <item>Jeder <b>wörtliche</b> Teil (Zielordner, eigener Name, Kennzeichnung) wird maskiert:
    /// yt-dlp liest <c>%</c> als Beginn eines Platzhalters, ein Literal muss <c>%%</c> sein.</item>
    /// <item>Ein eigener Name läuft durch <see cref="AppPaths.SanitizeFileName"/>; den Titel
    /// bereinigt yt-dlp selbst (<c>--windows-filenames</c>, siehe
    /// <see cref="YtDlpArgumentBuilder"/>).</item>
    /// <item>Name bzw. Titel werden auf <see cref="TitleBudget"/> begrenzt: höchstens
    /// <see cref="MaxTitleLength"/>, weniger bei langem Zielordner, damit der vollständige Pfad
    /// inklusive der Zwischenendungen von yt-dlp unter MAX_PATH bleibt. Windows zählt MAX_PATH in
    /// UTF-16-Einheiten; der Titel wird deshalb nach <b>Bytes</b> gekürzt (<c>%(title).NB</c>) —
    /// ein Zeichen braucht in UTF-8 nie weniger Bytes als in UTF-16 Einheiten, also kann auch ein
    /// Titel aus Emoji das Budget nicht sprengen. yt-dlp lässt dabei ein angeschnittenes Zeichen
    /// ganz weg. Ein eigener Name wird in UTF-16-Einheiten gekürzt (<c>string.Length</c>).</item>
    /// <item><c>$</c> in einem eigenen Namen wird durch <c>＄</c> ersetzt: yt-dlp setzt in der
    /// Vorlage Umgebungsvariablen ein, und dafür gibt es keine saubere Maskierung.</item>
    /// </list>
    /// </summary>
    internal static class YtDlpOutputTemplate
    {
        /// <summary>Höchstlänge von Titel (Bytes in UTF-8) bzw. eigenem Namen (UTF-16-Einheiten) —
        /// bei lateinischer Schrift ohne Umlaute sind das 120 Zeichen.</summary>
        public const int MaxTitleLength = 120;

        /// <summary>Untergrenze (gleiche Einheit wie <see cref="MaxTitleLength"/>), auch wenn der
        /// Zielordner sehr lang ist — darunter wäre der Name nicht mehr erkennbar. Ein so langer
        /// Ordner braucht dann aktivierte Langpfade.</summary>
        public const int MinTitleLength = 20;

        /// <summary>MAX_PATH (260) minus abschließendes Nullzeichen.</summary>
        public const int PathLimit = 259;

        // Was hinter Titel und Kennzeichnung noch kommt: "_" + Video-ID (YouTube 11 Zeichen,
        // andere Seiten länger) + "." + Endung, dazu die Zwischenendungen, die yt-dlp während des
        // Downloads anhängt (z. B. ".f399.webm.part", ".temp.mp4").
        private const int IdReserve = 24;
        private const int ExtensionReserve = 5;
        private const int TemporarySuffixReserve = 16;

        /// <summary>Zeichen, die nach Titel und Kennzeichnung höchstens noch folgen.</summary>
        public const int ReservedTailLength = 1 + IdReserve + 1 + ExtensionReserve + TemporarySuffixReserve;

        /// <summary>Maskiert ein Literal für eine yt-dlp-Ausgabevorlage (<c>%</c> → <c>%%</c>).</summary>
        public static string EscapeLiteral(string literal) =>
            literal.Replace("%", "%%", StringComparison.Ordinal);

        /// <summary>
        /// Vollständige Vorlage <c>&lt;Ordner&gt;\&lt;Name&gt;&lt;Kennzeichnung&gt;_%(id)s.%(ext)s</c>.
        /// </summary>
        /// <param name="directory">Zielordner, wörtlich.</param>
        /// <param name="literalBaseName">Eigener Name, wörtlich — oder <c>null</c> für den Titel des
        /// Videos.</param>
        /// <param name="literalSuffix">Kennzeichnung hinter dem Namen (z. B. <c>_q1080_mp4</c>),
        /// wörtlich; leer, wenn keine.</param>
        public static string Build(string directory, string? literalBaseName, string literalSuffix)
        {
            string dir = directory.TrimEnd('\\', '/');
            int budget = TitleBudget(dir, literalSuffix);

            string name = literalBaseName is null
                ? $"%(title).{budget}B"
                : EscapeLiteral(LiteralBaseName(dir, literalBaseName, literalSuffix));

            return $"{EscapeLiteral(dir)}\\{name}{EscapeLiteral(literalSuffix)}_%(id)s.%(ext)s";
        }

        /// <summary>Der eigene Name genau so, wie er im Dateinamen landet (noch unmaskiert) — für
        /// <see cref="Build"/> und für Anzeige/Protokoll, damit beide übereinstimmen.</summary>
        public static string LiteralBaseName(string directory, string literalBaseName, string literalSuffix) =>
            AppPaths.SanitizeFileName(
                literalBaseName.Replace('$', '\uFF04'),
                TitleBudget(directory, literalSuffix));

        /// <summary>Wie lang Titel (UTF-8-Bytes) bzw. Name (UTF-16-Einheiten) sein dürfen, damit
        /// <paramref name="directory"/> + Name + <paramref name="literalSuffix"/> + Rest unter
        /// <see cref="PathLimit"/> bleibt — zwischen <see cref="MinTitleLength"/> und
        /// <see cref="MaxTitleLength"/>.</summary>
        public static int TitleBudget(string directory, string literalSuffix)
        {
            int available = PathLimit - directory.TrimEnd('\\', '/').Length - 1 - literalSuffix.Length - ReservedTailLength;
            return Math.Clamp(available, MinTitleLength, MaxTitleLength);
        }

        /// <summary>Neuer, eindeutiger Pfad für <c>--print-to-file after_move:filepath</c>. Die Datei
        /// selbst legt yt-dlp an.</summary>
        public static string NewFinalPathFile() =>
            Path.Combine(Path.GetTempPath(), "MortysDLP", $"ytdlp-final-{Guid.NewGuid():N}.txt");

        /// <summary>
        /// Liest den von yt-dlp geschriebenen Endpfad: UTF-8, eine Zeile je fertiger Datei
        /// (<c>os.linesep</c>), angehängt — nach einem Neustart können es mehrere sein, die letzte
        /// nicht leere gilt. <c>null</c>, wenn die Datei fehlt, leer ist oder nicht lesbar.
        /// </summary>
        public static string? TryReadFinalPath(string file)
        {
            try
            {
                if (!File.Exists(file))
                    return null;

                return File.ReadAllLines(file, Encoding.UTF8)
                    .Select(l => l.Trim())
                    .LastOrDefault(l => l.Length > 0);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.Warn($"Pfaddatei von yt-dlp nicht lesbar: {file} ({ex.Message})");
                return null;
            }
        }

        /// <summary>Entfernt die Pfaddatei. Best-Effort.</summary>
        public static void DeleteFinalPathFile(string file)
        {
            try { if (File.Exists(file)) File.Delete(file); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.Warn($"Pfaddatei von yt-dlp nicht löschbar: {file} ({ex.Message})");
            }
        }
    }
}
