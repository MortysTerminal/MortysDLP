using MortysDLP.Models;
using System.Threading;
using System.Threading.Tasks;

namespace MortysDLP.Services.Releases
{
    /// <summary>
    /// Fragt mehrere <see cref="IReleaseSource"/>-Quellen ab und liefert eine einzige,
    /// abgestimmte Antwort — die konkrete Reihenfolge und die Regeln dafür liegen bei der
    /// jeweiligen Implementierung (siehe <see cref="ResilientReleaseResolver"/>).
    /// </summary>
    internal interface IReleaseResolver
    {
        /// <param name="current">Laufende Version, für die Regel gegen veraltete Antworten
        /// nicht-primärer Quellen. <c>null</c> = unbekannt, dann greift die Regel nicht.</param>
        Task<ReleaseInfo?> ResolveAsync(ReleaseQuery query, AppVersion? current, CancellationToken ct);
    }
}
