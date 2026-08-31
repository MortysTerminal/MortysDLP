using MortysDLP.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MortysDLP.Services.Tools
{
    /// <param name="Success">true nur, wenn ersetzt <b>und</b> die Erfolgskontrolle bestanden
    /// wurde.</param>
    /// <param name="RolledBack">true, wenn bereits ersetzt worden war und der vorherige Stand
    /// zurückgeholt wurde. Für den Aufrufer der Unterschied zwischen „nichts passiert" und
    /// „passiert und wieder rückgängig gemacht".</param>
    /// <param name="Detail">Ein Satz für Protokoll und Dialog.</param>
    internal sealed record ToolReplaceResult(bool Success, bool RolledBack, string Detail);

    /// <summary>
    /// Ersetzt Werkzeugdateien mit Rückfallebene und Erfolgskontrolle. Kennt kein bestimmtes
    /// Werkzeug, kein Netz und keine Oberfläche — Zieldateien, bereitgestellte Dateien und die
    /// Prüfung selbst kommen vollständig herein. Genau deshalb lässt sich das Verhalten gegen ein
    /// Temp-Verzeichnis und einen erfundenen Prüf-Aufruf testen, ohne ein echtes Werkzeug.
    ///
    /// <para><b>Warum <c>.old</c> statt löschen und überschreiben:</b> Unter Windows lässt sich
    /// eine laufende EXE <i>umbenennen</i>, aber nicht überschreiben. Läuft gerade eine
    /// Konvertierung, ist das der Unterschied zwischen „Update klappt" und „Update scheitert
    /// mitten drin, Datei weg". Die Sicherung ist zugleich die Rückfallebene für den zweiten,
    /// tückischeren Fall: Ein Update, das die Datei ersetzt, aber ein unbrauchbares Werkzeug
    /// hinterlässt, ist von einem erfolgreichen sonst nicht zu unterscheiden.</para>
    ///
    /// <para><b>Erfolgsfälle werden protokolliert, nicht nur Fehlschläge</b> — gesichert,
    /// eingesetzt, Kontrolle bestanden, Sicherung gelöscht. Ein Protokoll, das nur bei Fehlern
    /// etwas sagt, beweist im Erfolgsfall nichts.</para>
    /// </summary>
    internal static class ToolInstaller
    {
        /// <summary>Endung der Sicherung. Bewusst dieselbe Bezeichnung wie im Updater der
        /// Anwendung selbst — ein Muster, zwei Ebenen.</summary>
        public const string BackupSuffix = ".old";

        /// <summary>Endung der bereitgestellten, noch nicht eingesetzten Datei.</summary>
        public const string StagedSuffix = ".new";

        /// <param name="TargetPath">Endgültiger Pfad der Werkzeugdatei.</param>
        /// <param name="StagedPath">Bereits heruntergeladene und geprüfte Datei, die dorthin soll.</param>
        internal sealed record Replacement(string TargetPath, string StagedPath);

        /// <summary>
        /// Ersetzt <b>alle</b> <paramref name="replacements"/> gemeinsam, prüft danach einmal über
        /// <paramref name="verifyAsync"/> und nimmt bei einer durchgefallenen Prüfung
        /// <b>alle</b> zurück. Das gemeinsame Zurücknehmen ist der Grund, warum ffmpeg und
        /// ffprobe nicht zwei getrennte Vorgänge sind: Ein neues ffmpeg neben einem alten ffprobe
        /// ist ein Zustand, den niemand geprüft hat.
        /// </summary>
        /// <param name="toolId">Für das Protokoll — jede Zeile ist einem Werkzeug zuzuordnen.</param>
        /// <param name="verifyAsync">Die Erfolgskontrolle: ruft das Werkzeug tatsächlich auf und
        /// sagt, ob die Antwort brauchbar ist. Wirft sie, gilt das als „nicht bestanden".</param>
        public static async Task<ToolReplaceResult> ReplaceAllAsync(
            string toolId,
            IReadOnlyList<Replacement> replacements,
            Func<CancellationToken, Task<bool>> verifyAsync,
            CancellationToken ct)
        {
            if (replacements.Count == 0)
                return new ToolReplaceResult(false, false, "keine Datei zum Einsetzen angegeben");

            foreach (var replacement in replacements)
            {
                if (!File.Exists(replacement.StagedPath))
                {
                    return new ToolReplaceResult(false, false,
                        $"bereitgestellte Datei fehlt: {Path.GetFileName(replacement.StagedPath)}");
                }
            }

            var applied = new List<AppliedReplacement>(replacements.Count);

            try
            {
                foreach (var replacement in replacements)
                {
                    ct.ThrowIfCancellationRequested();

                    string backupPath = replacement.TargetPath + BackupSuffix;
                    string name = Path.GetFileName(replacement.TargetPath);

                    // Ein liegen gebliebenes .old aus einem früheren Fehlschlag würde das
                    // Umbenennen scheitern lassen - es ist zu diesem Zeitpunkt wertlos, weil der
                    // aktuelle Stand die Zieldatei ist.
                    TryDelete(backupPath, $"[{toolId}] alte Sicherung {name}{BackupSuffix}");

                    bool hadPrevious = File.Exists(replacement.TargetPath);
                    if (hadPrevious)
                    {
                        File.Move(replacement.TargetPath, backupPath);
                        Log.Info($"[{toolId}] {name}: vorherige Datei nach {name}{BackupSuffix} gesichert.");
                    }

                    File.Move(replacement.StagedPath, replacement.TargetPath);
                    Log.Info($"[{toolId}] {name}: neue Datei eingesetzt.");

                    applied.Add(new AppliedReplacement(replacement.TargetPath, backupPath, hadPrevious));
                }
            }
            catch (OperationCanceledException)
            {
                RollBack(toolId, applied);
                throw;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.Warn($"[{toolId}] Einsetzen fehlgeschlagen: {ex.Message}", ex);
                bool rolledBack = RollBack(toolId, applied);
                return new ToolReplaceResult(false, rolledBack, $"Einsetzen fehlgeschlagen: {ex.Message}");
            }

            bool verified;
            try
            {
                verified = await verifyAsync(ct);
            }
            catch (OperationCanceledException)
            {
                RollBack(toolId, applied);
                throw;
            }
            catch (Exception ex)
            {
                // Eine werfende Erfolgskontrolle ist eine durchgefallene Erfolgskontrolle - der
                // Unterschied ist für das Ergebnis bedeutungslos, für das Protokoll nicht.
                Log.Warn($"[{toolId}] Erfolgskontrolle hat eine Ausnahme ausgelöst: {ex.Message}", ex);
                verified = false;
            }

            if (!verified)
            {
                Log.Warn($"[{toolId}] Erfolgskontrolle nicht bestanden - der vorherige Stand wird zurückgeholt.");
                bool rolledBack = RollBack(toolId, applied);
                return new ToolReplaceResult(false, rolledBack,
                    rolledBack
                        ? "Erfolgskontrolle nicht bestanden - der vorherige Stand wurde zurückgeholt"
                        : "Erfolgskontrolle nicht bestanden - es gab keinen vorherigen Stand, der zurückgeholt werden konnte");
            }

            Log.Info($"[{toolId}] Erfolgskontrolle bestanden.");

            foreach (var entry in applied)
            {
                if (!entry.HadPrevious)
                    continue;

                string name = Path.GetFileName(entry.BackupPath);
                if (TryDelete(entry.BackupPath, $"[{toolId}] Sicherung {name}"))
                    Log.Info($"[{toolId}] Sicherung {name} gelöscht.");
            }

            bool anyPrevious = applied.Exists(a => a.HadPrevious);
            return new ToolReplaceResult(true, false,
                anyPrevious
                    ? "ersetzt, Erfolgskontrolle bestanden, Sicherung entfernt"
                    : "neu eingerichtet, Erfolgskontrolle bestanden");
        }

        /// <summary>Entfernt eine bereitgestellte Datei, die nicht mehr gebraucht wird (Abbruch,
        /// Fehlschlag vor dem Einsetzen). Best-Effort — ein Rest hier ist unschön, aber kein
        /// neuer Fehler; die eigentliche Ursache ist bereits unterwegs nach oben.</summary>
        public static void DiscardStaged(string toolId, string stagedPath) =>
            TryDelete(stagedPath, $"[{toolId}] bereitgestellte Datei {Path.GetFileName(stagedPath)}");

        /// <summary>Holt alle bereits ersetzten Dateien zurück. Liefert true, wenn es überhaupt
        /// etwas zurückzuholen gab. Läuft in umgekehrter Reihenfolge, damit der Zustand bei einem
        /// Fehlschlag mitten in der Rücknahme so weit wie möglich dem Ausgangszustand
        /// entspricht.</summary>
        private static bool RollBack(string toolId, List<AppliedReplacement> applied)
        {
            bool hadSomething = false;

            for (int i = applied.Count - 1; i >= 0; i--)
            {
                var entry = applied[i];
                string name = Path.GetFileName(entry.TargetPath);

                try
                {
                    if (File.Exists(entry.TargetPath))
                        File.Delete(entry.TargetPath);

                    if (entry.HadPrevious)
                    {
                        File.Move(entry.BackupPath, entry.TargetPath);
                        Log.Info($"[{toolId}] {name}: vorherige Datei aus {name}{BackupSuffix} zurückgeholt.");
                        hadSomething = true;
                    }
                    else
                    {
                        Log.Info($"[{toolId}] {name}: Neuinstallation zurückgenommen, es gab keinen vorherigen Stand.");
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Der einzige Fall in dieser Klasse, der wirklich schlecht ausgeht: Die neue
                    // Datei lässt sich nicht entfernen oder die Sicherung nicht zurückschieben.
                    // Deshalb Error und nicht Warn - und mit dem Pfad der Sicherung, damit sich
                    // das von Hand richten lässt.
                    Log.Error($"[{toolId}] {name} konnte nicht zurückgeholt werden. Die Sicherung liegt " +
                        $"unter '{entry.BackupPath}' und muss ggf. von Hand eingesetzt werden.", ex);
                }
            }

            return hadSomething;
        }

        private static bool TryDelete(string path, string description)
        {
            try
            {
                if (!File.Exists(path))
                    return false;

                File.Delete(path);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.Warn($"{description} konnte nicht gelöscht werden: {ex.Message}");
                return false;
            }
        }

        private sealed record AppliedReplacement(string TargetPath, string BackupPath, bool HadPrevious);
    }
}
