using MortysDLP.Models;

namespace MortysDLP.Services.Tools
{
    /// <param name="Offer">Ob dem Nutzer ein Update angeboten wird. Ein <c>true</c> heißt
    /// „fragen", nie „durchführen".</param>
    /// <param name="Reason">Ein Satz für das Protokoll. Wird <b>immer</b> geschrieben, auch wenn
    /// nichts angeboten wird — ohne diese Zeile ist später nicht erklärbar, warum ein vorhandenes
    /// Update nicht erscheint (dieselbe Lehre wie bei der App selbst).</param>
    internal sealed record ToolUpdateVerdict(bool Offer, string Reason);

    /// <summary>
    /// Entscheidet aus lokaler und entfernter Version, ob ein Werkzeug-Update angeboten wird.
    /// Reine Logik ohne Netz, Dateien oder Oberfläche — genau deshalb liegt sie hier und nicht in
    /// der jeweiligen Werkzeugklasse.
    ///
    /// <para>Zwei Fehlerbilder gibt diese Klasse den Ausschlag zu verhindern:
    /// ein <b>Downgrade-Angebot</b>, wenn lokal etwas Neueres liegt als der letzte Release
    /// (yt-dlp-Nightly), und ein <b>Angebot ohne Grundlage</b>, wenn das Werkzeug auf die
    /// Versionsfrage einfach nicht geantwortet hat. Der frühere Vergleich per
    /// Zeichenketten-Ungleichheit lieferte in beiden Fällen „Update nötig".</para>
    /// </summary>
    internal static class ToolUpdateDecision
    {
        /// <param name="local">Installierte Version. <see cref="ToolVersion.Unknown"/>, wenn das
        /// Werkzeug nicht geantwortet hat — dann wird nie ein Update angeboten. Für ein
        /// <b>fehlendes</b> Werkzeug ist nicht diese Entscheidung zuständig, sondern der
        /// Installationspfad des Aufrufers.</param>
        /// <param name="remote">Von der Quellenkette gemeldete Version.</param>
        public static ToolUpdateVerdict Evaluate(ToolVersion local, ToolVersion remote, ToolUpdatePolicy policy)
        {
            if (!remote.HasValue)
                return new ToolUpdateVerdict(false, "keine entfernte Version ermittelt - kein Angebot");

            if (!local.HasValue)
            {
                return new ToolUpdateVerdict(false,
                    $"installierte Version unbekannt (Werkzeug hat nicht brauchbar geantwortet), " +
                    $"entfernt {remote} - kein Angebot, weil sich ohne Vergleichswert nichts belegen lässt");
            }

            // Erst der Schutz gegen den Rückschritt, dann die Politik: Beide Politiken dürfen
            // niemals ein Downgrade anbieten, und der Zahlenkern reicht dafür auch dann aus, wenn
            // die Version im Ganzen nicht ordnend ist.
            if (local.CompareCore(remote) is int coreComparison && coreComparison > 0)
            {
                return new ToolUpdateVerdict(false,
                    $"installiert {local} ist neuer als entfernt {remote} - kein Angebot (Downgrade verhindert)");
            }

            return policy switch
            {
                ToolUpdatePolicy.OnlyWhenNewer => EvaluateOnlyWhenNewer(local, remote),
                _ => EvaluateWhenDifferent(local, remote),
            };
        }

        private static ToolUpdateVerdict EvaluateOnlyWhenNewer(ToolVersion local, ToolVersion remote)
        {
            return remote.IsNewerThan(local) switch
            {
                true => new ToolUpdateVerdict(true, $"entfernt {remote} ist neuer als installiert {local} - Angebot"),
                false => new ToolUpdateVerdict(false, $"installiert {local} ist aktuell (entfernt {remote}) - kein Angebot"),
                null => new ToolUpdateVerdict(false,
                    $"'neuer als' lässt sich für installiert {local} gegen entfernt {remote} nicht " +
                    "beantworten - kein Angebot, ein 'weiß nicht' ist kein Grund für ein Update"),
            };
        }

        private static ToolUpdateVerdict EvaluateWhenDifferent(ToolVersion local, ToolVersion remote)
        {
            if (local.IsSameRelease(remote))
                return new ToolUpdateVerdict(false, $"installiert {local} ist dieselbe Ausgabe wie entfernt {remote} - kein Angebot");

            return new ToolUpdateVerdict(true,
                $"installiert {local} unterscheidet sich von entfernt {remote} - Angebot " +
                "('neuer als' ist hier nicht beantwortbar, deshalb nur anbieten, nie erzwingen)");
        }
    }
}
