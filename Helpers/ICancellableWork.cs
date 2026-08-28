using System.Collections.Generic;

namespace MortysDLP.Helpers
{
    /// <summary>Seiten mit einem abbrechbaren Hintergrundvorgang (Download, Konvertierung,
    /// Transkription, …) implementieren dies, damit die Update-Vorprüfung (W3-T02b) laufende
    /// Arbeit erkennen und auf Wunsch genau so abbrechen kann wie über den zugehörigen
    /// Abbrechen-Knopf in der Oberfläche.</summary>
    internal interface ICancellableWork
    {
        bool IsBusy { get; }

        /// <summary>Kurze, bereits lokalisierte Bezeichnung für die Nachfrage vor einem Update,
        /// z. B. „Download" oder „Transkription".</summary>
        string BusyLabel { get; }

        /// <summary>Bricht den laufenden Vorgang ab — identisch zum Abbrechen-Knopf der Seite.</summary>
        void RequestCancel();
    }

    /// <summary>Reine, ohne UI testbare Auswertung von <see cref="ICancellableWork"/>-Quellen
    /// (W3-T02b) — von der eigentlichen Nachfrage/dem Abbrechen bewusst getrennt.</summary>
    internal static class ActiveWorkHelper
    {
        public static IReadOnlyList<ICancellableWork> FindBusy(IEnumerable<ICancellableWork> sources)
        {
            var busy = new List<ICancellableWork>();
            foreach (var work in sources)
            {
                if (work.IsBusy)
                    busy.Add(work);
            }
            return busy;
        }
    }
}
