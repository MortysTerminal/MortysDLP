namespace MortysDLP.Services
{
    /// <summary>
    /// Bildet den je Abschnitt gemeldeten Rohprozentsatz (0–100) auf einen Gesamtfortschritt
    /// ab, der über einen ganzen Download hinweg einmal durchläuft, statt bei jedem
    /// Abschnitt (Video-Stream, Audio-Stream, Zusammenführen, H.264-Nachkonvertierung, jedes
    /// Video einer Playlist) neu bei 0 zu beginnen. Reine Funktionen, ohne Oberflächen- oder
    /// Prozesszugriff.
    ///
    /// <para>Bewusst grob statt exakt: Ein Video-Stream ist typischerweise deutlich größer
    /// als der zugehörige Audio-Stream — die genaue Aufteilung hier ist trotzdem nur eine
    /// Annäherung, keine Berechnung aus echten Byte-Größen (die wären vor dem Download nicht
    /// bekannt). Wichtig ist, dass der Balken nie zurückspringt, nicht die exakte
    /// Prozentzahl an jeder Stelle.</para>
    /// </summary>
    internal static class DownloadProgressWeighting
    {
        /// <summary>Obergrenze für Stream-Download und Zusammenführen, wenn danach noch eine
        /// H.264-Nachkonvertierung folgen kann — lässt oben Platz dafür, unabhängig davon, ob
        /// sie am Ende tatsächlich läuft (der Quell-Codec kann schon H.264 sein).</summary>
        private const double CeilingWithPostConversion = 90.0;

        private const double FullCeiling = 100.0;

        /// <summary>Gesamtfortschritt während des Ladens eines von ein oder zwei Streams
        /// (Video und, bei Video+Audio, zusätzlich Audio). Bei zwei Streams bekommt jeder die
        /// Hälfte des verfügbaren Bereichs.</summary>
        /// <param name="streamIndex">0-basiert: 0 für den ersten (Video-)Stream, 1 für den
        /// zweiten (Audio-)Stream.</param>
        /// <param name="streamCount">1 (Audio-Only oder ein einzelner, bereits gemuxter
        /// Stream) oder 2 (Video und Audio getrennt).</param>
        /// <param name="reservePostConversion">Ob im Anschluss noch eine
        /// H.264-Nachkonvertierung folgen kann (Schnittmodus).</param>
        public static double ForStream(double rawPercent, int streamIndex, int streamCount, bool reservePostConversion)
        {
            double ceiling = reservePostConversion ? CeilingWithPostConversion : FullCeiling;
            double clampedRaw = Math.Clamp(rawPercent, 0, 100) / 100.0;

            if (streamCount <= 1)
                return clampedRaw * ceiling;

            double perStream = ceiling / streamCount;
            double streamStart = Math.Clamp(streamIndex, 0, streamCount - 1) * perStream;
            return Math.Min(ceiling, streamStart + clampedRaw * perStream);
        }

        /// <summary>Gesamtfortschritt, sobald yt-dlp die geladenen Streams zusammenführt.
        /// yt-dlp meldet dafür keinen eigenen Prozentsatz — der Balken springt deshalb einmal
        /// auf die Obergrenze, statt während des Zusammenführens weiterzulaufen.</summary>
        public static double ForMerge(bool reservePostConversion) =>
            reservePostConversion ? CeilingWithPostConversion : FullCeiling;

        /// <summary>Gesamtfortschritt während einer H.264-Nachkonvertierung — beginnt an der
        /// Obergrenze aus <see cref="ForStream"/>/<see cref="ForMerge"/> und läuft von dort
        /// bis 100.</summary>
        public static double ForPostConversion(double rawPercent) =>
            CeilingWithPostConversion + Math.Clamp(rawPercent, 0, 100) / 100.0 * (FullCeiling - CeilingWithPostConversion);

        /// <summary>Gesamtfortschritt einer Playlist aus dem Fortschritt des aktuell
        /// laufenden Videos — damit der Balken über die ganze Playlist hinweg einmal
        /// durchläuft, statt pro Video neu bei 0 zu beginnen.</summary>
        /// <param name="videoIndex">0-basiert: 0 für das erste Video.</param>
        /// <param name="videoCount">Gesamtzahl der Videos. Werte ≤ 0 geben
        /// <paramref name="perVideoPercent"/> unverändert zurück (kein Playlist-Kontext).</param>
        public static double ForPlaylist(double perVideoPercent, int videoIndex, int videoCount)
        {
            if (videoCount <= 0)
                return perVideoPercent;

            double clampedRaw = Math.Clamp(perVideoPercent, 0, 100) / 100.0;
            double clampedIndex = Math.Clamp(videoIndex, 0, videoCount - 1);
            return (clampedIndex + clampedRaw) / videoCount * 100.0;
        }
    }
}
