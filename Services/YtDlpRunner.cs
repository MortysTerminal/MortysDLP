using System.Diagnostics;

namespace MortysDLP.Services
{
    /// <summary>
    /// Führt einen yt-dlp-Lauf aus, verfolgt seine Ausgabe zeilenweise und behandelt einen
    /// Neustart nach Bandbreitenwechsel — die Schicht, die heute dreifach mit leicht
    /// abweichendem Verhalten in <c>DownloadPage</c>, <c>BatchDownloadPage</c> und
    /// <c>TwitchPage</c> steckt (<c>RunYtDlpAsync</c>/<c>RunDownloadAsync</c> samt jeweils
    /// eigener <c>ApplyBandwidthChange()</c>).
    ///
    /// <para>Eine Instanz gehört zu **einem** laufenden Auftrag (wie heute die privaten
    /// Felder <c>_ytDlpProcess</c>/<c>_bandwidthKillPending</c> je Seite) — nicht statisch,
    /// weil sie den gerade laufenden Prozess kennen muss, um ihn bei einer
    /// <see cref="RequestRestart"/>-Anfrage gezielt zu beenden.</para>
    ///
    /// <para><b>Der Neustart-Zyklus selbst bleibt beim Aufrufer</b> (nicht in dieser Klasse):
    /// Nur der Aufrufer weiß, mit welchem — möglicherweise geänderten — <see cref="YtDlpJob"/>
    /// erneut gestartet werden soll. Ein geändertes Bandbreitenlimit bedeutet einen neuen
    /// Job (<c>job with { BandwidthLimitMBps = … }</c>), nicht denselben — <c>TwitchPage</c>
    /// liest den aktuellen Wert heute genau deshalb bei jedem Schleifendurchlauf neu ein,
    /// nicht einmalig vor der Schleife.</para>
    /// </summary>
    internal sealed class YtDlpRunner
    {
        private Process? _process;
        private volatile bool _restartPending;

        /// <summary>Führt <paramref name="job"/> über yt-dlp aus.</summary>
        /// <returns><c>true</c>, wenn der Lauf wegen einer <see cref="RequestRestart"/>-Anfrage
        /// beendet wurde und mit einem (ggf. angepassten) Job erneut gestartet werden sollte;
        /// <c>false</c>, wenn er regulär durchgelaufen ist.</returns>
        /// <exception cref="InvalidOperationException">yt-dlp hat sich mit einem
        /// Fehler-Exitcode beendet, ohne dass ein Neustart angefordert wurde.</exception>
        public Task<bool> RunAsync(
            string ytDlpPath,
            YtDlpJob job,
            Action<string>? onStdOut = null,
            Action<string>? onStdErr = null,
            TimeSpan? idleTimeout = null,
            CancellationToken ct = default,
            Action<int>? onExitCode = null) =>
            RunCoreAsync(ytDlpPath, YtDlpArgumentBuilder.Build(job), onStdOut, onStdErr, idleTimeout, ct, onExitCode);

        /// <summary>Der eigentliche Ausführungs- und Neustart-Kern, unabhängig von der
        /// Job-zu-Argumentliste-Übersetzung (die hat <c>YtDlpArgumentBuilderTests</c> bereits
        /// abgedeckt) — dadurch lässt sich diese Methode gegen ein beliebiges Testskript
        /// prüfen, ohne einen echten yt-dlp-Aufruf zu brauchen.</summary>
        internal async Task<bool> RunCoreAsync(
            string exePath,
            IEnumerable<string> args,
            Action<string>? onStdOut,
            Action<string>? onStdErr,
            TimeSpan? idleTimeout,
            CancellationToken ct,
            Action<int>? onExitCode = null)
        {
            ProcessResult result;
            bool restartRequested;
            try
            {
                result = await ProcessRunner.RunStreamingAsync(
                    exePath, args,
                    onStdOut: onStdOut,
                    onStdErr: onStdErr,
                    timeout: null,
                    idleTimeout: idleTimeout ?? TimeSpan.FromSeconds(120),
                    onStarted: p => _process = p,
                    ct: ct);
            }
            finally
            {
                _process = null;

                // Beides gehört in den finally-Zweig, nicht dahinter: Eine Neustartanfrage
                // gilt ausschließlich für den Lauf, während dem sie gestellt wurde. Endet
                // dieser Lauf über eine Ausnahme (Abbruch, Zeitüberschreitung), bliebe die
                // Anfrage sonst am Objekt hängen — und der nächste, völlig unabhängige Lauf
                // derselben Seite meldete grundlos „neu starten". Die Runner-Instanz lebt
                // so lange wie die Seite, der Fehler überlebte also den ganzen Download.
                restartRequested = _restartPending;
                _restartPending = false;
            }

            // Absichtlicher Kill wegen Limit-Änderung → kein Fehler, sondern das Signal an
            // den Aufrufer, mit einem (ggf. neuen) Job erneut zu starten. Kein eigener
            // Rückkanal von ProcessRunner nötig: Der Kill kommt von außen, direkt auf dem in
            // onStarted gemerkten Process (siehe RequestRestart) — das Feld hier wird nur
            // gelesen, nachdem der Prozess bereits beendet ist.
            //
            // Zusätzlich an einen Fehler-Exitcode gebunden: RequestRestart kann den Prozess
            // in dem Moment antreffen, in dem er ohnehin gerade regulär fertig wird — dann
            // greift der Kill ins Leere, der Lauf endet mit Exitcode 0, und ein Neustart
            // wäre ein überflüssiger zweiter yt-dlp-Aufruf für einen bereits abgeschlossenen
            // Download. Ein tatsächlich gekillter Prozess endet unter Windows nie mit 0.
            if (restartRequested && !result.Success)
            {
                ct.ThrowIfCancellationRequested();
                return true;
            }

            ct.ThrowIfCancellationRequested();

            // Wird bei einem fehlerhaften Exit-Code bewusst noch vor der Ausnahme aufgerufen -
            // der Aufrufer soll den Code auch im Fehlerfall protokollieren können, nicht nur
            // bei Erfolg.
            onExitCode?.Invoke(result.ExitCode);

            if (!result.Success)
                throw new InvalidOperationException($"yt-dlp beendet mit Exit-Code {result.ExitCode}");

            return false;
        }

        /// <summary>Beendet einen gerade laufenden Lauf gezielt und markiert ihn für einen
        /// Neustart — der Aufruf von <see cref="RunAsync"/>, der dadurch beendet wird, liefert
        /// <c>true</c> statt einer Ausnahme. Ohne laufenden Prozess bewirkt der Aufruf nichts.
        /// </summary>
        /// <returns><c>true</c>, wenn tatsächlich ein laufender Prozess beendet wurde -
        /// Aufrufer nutzen das, um z. B. nur dann eine Meldung zu protokollieren, wenn die
        /// Anfrage etwas bewirkt hat.</returns>
        public bool RequestRestart()
        {
            // Einmal in eine lokale Variable: Das Feld kann jederzeit vom Lauf selbst auf null
            // gesetzt werden, während diese Methode noch damit arbeitet.
            Process? process = _process;
            if (process is null)
                return false;

            try
            {
                if (process.HasExited)
                    return false;

                // Vor dem Kill setzen, nicht danach: Sonst gäbe es ein Fenster, in dem der
                // Prozess bereits beendet ist, RunCoreAsync den Fehler-Exitcode aber noch als
                // echten Fehlschlag wertet statt als angeforderten Neustart.
                _restartPending = true;
                process.Kill(entireProcessTree: true);
                return true;
            }
            catch (Exception ex) when (ex is InvalidOperationException
                                          or ObjectDisposedException
                                          or System.ComponentModel.Win32Exception
                                          or System.Runtime.InteropServices.COMException)
            {
                // Der Lauf ist in genau diesem Moment zu Ende gegangen. ProcessRunner entsorgt
                // sein Process-Objekt (using) schon, bevor RunCoreAsync das Feld hier nullen
                // kann - in diesem schmalen Fenster wirft bereits der Zugriff auf HasExited
                // („No process is associated with this object", auf manchen Systemen auch eine
                // COMException E_HANDLE), nicht erst der Kill. Ohne diesen Fang schlägt die
                // Ausnahme bis in den Ereignisbehandler der Einstellungsseite durch, der
                // ApplyBandwidthChange() für alle drei Seiten ungeschützt aufruft.
                //
                // Es gibt dann nichts mehr zu beenden, und weil der Lauf nicht durch uns
                // beendet wurde, ist es auch kein Neustartfall - eine eventuell schon gesetzte
                // Anfrage wird deshalb wieder zurückgenommen.
                _restartPending = false;
                return false;
            }
        }
    }
}
