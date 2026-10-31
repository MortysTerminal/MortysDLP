using System.Globalization;

namespace MortysDLP.Services
{
    /// <summary>
    /// Ein Zeitausschnitt ist fertig, aber offensichtlich unvollständig. yt-dlp meldet in diesem
    /// Fall Erfolg (Exit-Code 0). Ein eigener Typ, damit die Seite eine verständliche Meldung
    /// zeigen kann, statt „abgeschlossen" oder pauschal „Fehler beim Download".
    /// </summary>
    internal sealed class DownloadSectionTooShortException : System.Exception
    {
        public DownloadSectionTooShortException(double expectedSeconds, double actualSeconds)
            : base(string.Create(CultureInfo.InvariantCulture,
                $"Die Datei ist {actualSeconds:F0} s lang, der gewählte Abschnitt {expectedSeconds:F0} s."))
        {
        }
    }
}
