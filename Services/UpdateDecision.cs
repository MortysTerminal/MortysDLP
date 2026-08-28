using MortysDLP.Helpers;
using MortysDLP.Models;

namespace MortysDLP.Services
{
    /// <summary>
    /// Entscheidet, ob ein Update angeboten wird — reine Logik, ohne Oberfläche und ohne
    /// direkten Settings-Zugriff. Der Sachverhalt ("es gibt etwas Neueres") kommt bereits
    /// fertig aus <see cref="UpdateCheckService"/>; hier kommt nur noch <c>VersionSkip</c>
    /// dazu. Genau diese Trennung fehlte beim ersten Anlauf: Die Prüfung stand im Startpfad,
    /// das Schreiben von <c>VersionSkip</c> nirgends (Befund K-07). Siehe
    /// <c>werkstatt/tasks/W2-T09.md</c>.
    /// </summary>
    internal static class UpdateDecision
    {
        /// <summary>Soll ein Update angeboten werden?</summary>
        /// <param name="current">Laufende Version.</param>
        /// <param name="latest">Neueste bekannte Version.</param>
        /// <param name="skipped">Rohwert aus <c>VersionSkip</c>; leer oder unlesbar bedeutet
        /// „nichts übersprungen" und wird nicht als Fehler behandelt — nur ein tatsächlich
        /// vorhandener, aber unlesbarer Wert wird protokolliert.</param>
        public static bool ShouldOffer(AppVersion current, AppVersion latest, string? skipped)
        {
            if (latest <= current)
                return false;

            if (string.IsNullOrWhiteSpace(skipped))
                return true;

            if (!AppVersion.TryParse(skipped, out var skippedVersion))
            {
                Log.Warn($"VersionSkip enthält einen unlesbaren Wert ('{skipped}') - wird ignoriert.");
                return true;
            }

            // <=, nicht ==: Wer 2026.09.01 überspringt, will auch nicht nach einer älteren
            // 2026.08.15 gefragt werden, falls eine Quelle die meldet. Eine tatsächlich
            // neuere Version fragt dagegen wieder.
            return latest > skippedVersion;
        }
    }
}
