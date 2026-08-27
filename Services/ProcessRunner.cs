using System.Diagnostics;
using System.IO;
using System.Text;

namespace MortysDLP.Services
{
    internal sealed record ProcessResult(int ExitCode, string StdOut, string StdErr, TimeSpan Duration)
    {
        public bool Success => ExitCode == 0;
    }

    /// <summary>
    /// Die einzige Stelle im Projekt, die externe Prozesse startet. Argumente gehen
    /// ausschließlich über <see cref="ProcessStartInfo.ArgumentList"/> — es gibt bewusst
    /// keinen Weg, eine fertige Kommandozeile als Zeichenkette zu übergeben, das verhindert
    /// Argument-Einschleusung. Jeder Aufruf hat ein Zeitlimit, jeder Abbruch verwendet
    /// <c>entireProcessTree: true</c>, beide Ströme laufen über UTF-8.
    /// </summary>
    internal static class ProcessRunner
    {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan IdlePollInterval = TimeSpan.FromMilliseconds(250);

        /// <summary>Führt aus und sammelt die gesamte Ausgabe. Für kurze Abfragen.
        /// Ohne eigenes Zeitlimit gilt ein Standardwert von 30 Sekunden.</summary>
        public static Task<ProcessResult> RunAsync(
            string exePath,
            IEnumerable<string> args,
            TimeSpan? timeout = null,
            string? workingDirectory = null,
            CancellationToken ct = default)
        {
            var stdOut = new StringBuilder();
            var stdErr = new StringBuilder();

            return RunCoreAsync(
                exePath, args,
                onStdOut: line => { lock (stdOut) stdOut.AppendLine(line); },
                onStdErr: line => { lock (stdErr) stdErr.AppendLine(line); },
                timeout: timeout ?? DefaultTimeout,
                idleTimeout: null,
                workingDirectory: workingDirectory,
                onStarted: null,
                buildResult: (exitCode, duration) =>
                    new ProcessResult(exitCode, stdOut.ToString(), stdErr.ToString(), duration),
                ct: ct);
        }

        /// <summary>Führt aus und meldet jede Ausgabezeile sofort. Für lange Vorgänge mit
        /// Fortschritt. Ohne <paramref name="timeout"/> gilt kein Gesamtlimit — dann sollte
        /// <paramref name="idleTimeout"/> gesetzt werden.</summary>
        public static Task<ProcessResult> RunStreamingAsync(
            string exePath,
            IEnumerable<string> args,
            Action<string>? onStdOut = null,
            Action<string>? onStdErr = null,
            TimeSpan? timeout = null,
            TimeSpan? idleTimeout = null,
            string? workingDirectory = null,
            Action<Process>? onStarted = null,
            CancellationToken ct = default)
        {
            return RunCoreAsync(
                exePath, args,
                onStdOut, onStdErr,
                timeout, idleTimeout, workingDirectory, onStarted,
                buildResult: (exitCode, duration) => new ProcessResult(exitCode, "", "", duration),
                ct: ct);
        }

        private static async Task<ProcessResult> RunCoreAsync(
            string exePath,
            IEnumerable<string> args,
            Action<string>? onStdOut,
            Action<string>? onStdErr,
            TimeSpan? timeout,
            TimeSpan? idleTimeout,
            string? workingDirectory,
            Action<Process>? onStarted,
            Func<int, TimeSpan, ProcessResult> buildResult,
            CancellationToken ct)
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            foreach (string a in args)
                psi.ArgumentList.Add(a);
            if (!string.IsNullOrEmpty(workingDirectory))
                psi.WorkingDirectory = workingDirectory;

            using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

            long lastOutputTicks = Environment.TickCount64;

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                Interlocked.Exchange(ref lastOutputTicks, Environment.TickCount64);
                onStdOut?.Invoke(e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                Interlocked.Exchange(ref lastOutputTicks, Environment.TickCount64);
                onStdErr?.Invoke(e.Data);
            };

            var stopwatch = Stopwatch.StartNew();

            if (!process.Start())
                throw new InvalidOperationException($"{Path.GetFileName(exePath)} konnte nicht gestartet werden.");

            onStarted?.Invoke(process);

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            bool killedByTimeout = false;
            bool killedByIdle = false;

            using var timeoutCts = new CancellationTokenSource();
            if (timeout.HasValue) timeoutCts.CancelAfter(timeout.Value);
            await using var timeoutReg = timeoutCts.Token.Register(() =>
            {
                killedByTimeout = true;
                TryKill(process);
            });

            await using var ctReg = ct.Register(() => TryKill(process));

            using var watchCts = new CancellationTokenSource();
            Task idleWatchTask = idleTimeout.HasValue
                ? WatchIdleAsync(process, idleTimeout.Value, () => Interlocked.Read(ref lastOutputTicks),
                    () => killedByIdle = true, watchCts.Token)
                : Task.CompletedTask;

            await process.WaitForExitAsync(CancellationToken.None); // NIE mit ct – sonst kein Exit-Code
            watchCts.Cancel();
            try { await idleWatchTask; } catch (OperationCanceledException) { /* erwartet beim Beenden */ }

            stopwatch.Stop();

            ct.ThrowIfCancellationRequested();
            if (killedByTimeout)
                throw new TimeoutException($"{Path.GetFileName(exePath)} überschritt das Zeitlimit von {timeout}.");
            if (killedByIdle)
                throw new TimeoutException($"{Path.GetFileName(exePath)} lieferte {idleTimeout} lang keine Ausgabe mehr.");

            return buildResult(process.ExitCode, stopwatch.Elapsed);
        }

        private static async Task WatchIdleAsync(
            Process process, TimeSpan idleTimeout, Func<long> getLastOutputTicks,
            Action markIdleTimeout, CancellationToken ct)
        {
            while (!process.HasExited)
            {
                await Task.Delay(IdlePollInterval, ct);
                long idleFor = Environment.TickCount64 - getLastOutputTicks();
                if (idleFor >= idleTimeout.TotalMilliseconds)
                {
                    markIdleTimeout();
                    TryKill(process);
                    return;
                }
            }
        }

        private static void TryKill(Process process)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch { /* bereits beendet oder kein Zugriff mehr */ }
        }
    }
}
