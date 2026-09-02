namespace MortysDLP.Services
{
    /// <summary>
    /// Zählt, den wievielten Stream eines Downloads yt-dlp gerade lädt — abgeleitet
    /// ausschließlich aus den <c>[download] Destination: …</c>-Zeilen.
    ///
    /// <para>Der Grund für eine eigene Klasse statt eines schlichten <c>++</c> am Aufrufort:
    /// yt-dlp gibt diese Zeile beim <b>Fortsetzen</b> eines bereits begonnenen Streams
    /// (<c>--continue</c> nach einem Bandbreitenwechsel) ein zweites Mal aus — direkt nach
    /// <c>[download] Resuming download at byte N</c>, mit demselben Ziel. Ein blindes
    /// Hochzählen deutet das als „nächster Stream" und lässt den phasengewichteten Balken
    /// (<see cref="DownloadProgressWeighting"/>) nach vorn springen und beim tatsächlichen
    /// Streamwechsel wieder zurück. Nur ein <b>anderer</b> Zielname bedeutet einen neuen
    /// Stream.</para>
    /// </summary>
    internal sealed class DownloadStreamTracker
    {
        private string? _currentDestination;

        /// <summary>0-basierter Index des laufenden Streams; <c>-1</c>, solange noch keine
        /// Zieldatei gemeldet wurde.</summary>
        public int StreamIndex { get; private set; } = -1;

        /// <summary>Meldet den Zielpfad aus einer <c>[download] Destination: …</c>-Zeile.</summary>
        /// <returns><c>true</c>, wenn damit ein neuer Stream beginnt (Index wurde erhöht);
        /// <c>false</c>, wenn derselbe Stream nur fortgesetzt wird.</returns>
        public bool RegisterDestination(string destination)
        {
            if (string.Equals(destination, _currentDestination, StringComparison.OrdinalIgnoreCase))
                return false;

            _currentDestination = destination;
            StreamIndex++;
            return true;
        }

        /// <summary>Setzt die Zählung für ein neues Video zurück — nicht für einen
        /// Prozess-Neustart innerhalb desselben Videos.</summary>
        public void Reset()
        {
            _currentDestination = null;
            StreamIndex = -1;
        }
    }
}
