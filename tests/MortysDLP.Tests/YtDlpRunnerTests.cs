using MortysDLP.Services;
using System.Globalization;
using System.IO;

namespace MortysDLP.Tests;

/// <summary>
/// Prüft <see cref="YtDlpRunner"/> gegen ein eigenes, kleines PowerShell-Testskript — die
/// Job-zu-Argumentliste-Übersetzung hat <c>YtDlpArgumentBuilderTests</c> bereits abgedeckt,
/// hier geht es ausschließlich um Ausführung, Ausgabe-Weiterleitung, Abbruch und die
/// Neustart-Semantik bei <see cref="YtDlpRunner.RequestRestart"/>.
///
/// <para>Bewusst über das automatische <c>$args</c> gelesen, <b>ohne</b> eigenen
/// <c>param()</c>-Block wie in <see cref="ProcessRunnerTests"/>: yt-dlp-artige Argumente
/// beginnen mit einem einzelnen Bindestrich (<c>-o</c>, <c>-f</c>, <c>-x</c>) — mit einem
/// deklarierten Parameter kollidiert PowerShell so etwas per Namens-Präfix mit seinen
/// eigenen gemeinsamen Parametern (<c>-OutVariable</c>/<c>-OutBuffer</c> für <c>-o</c>) und
/// bricht mit „Der Parameter kann nicht verarbeitet werden, da der Parametername ... nicht
/// eindeutig ist" ab, bevor das Skript überhaupt läuft. Ohne <c>param()</c>-Block gibt es
/// nichts, womit ein Argument kollidieren könnte.</para>
/// </summary>
public class YtDlpRunnerTests : IDisposable
{
    private readonly string _scriptPath;

    public YtDlpRunnerTests()
    {
        _scriptPath = Path.Combine(Path.GetTempPath(), $"YtDlpRunnerTests_{Guid.NewGuid():N}.ps1");
        File.WriteAllText(_scriptPath, """
            [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
            $exitCode = [int]$args[0]
            $sleepMs = [int]$args[1]
            for ($i = 2; $i -lt $args.Count; $i++) { Write-Output ("ARG:" + $args[$i]) }
            if ($sleepMs -gt 0) { Start-Sleep -Milliseconds $sleepMs }
            exit $exitCode
            """);
    }

    public void Dispose()
    {
        try { File.Delete(_scriptPath); } catch { /* Best-Effort */ }
        GC.SuppressFinalize(this);
    }

    private List<string> BuildArgs(int exitCode, int sleepMs, params string[] echoArgs)
    {
        var args = new List<string>
        {
            "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", _scriptPath,
            exitCode.ToString(CultureInfo.InvariantCulture), sleepMs.ToString(CultureInfo.InvariantCulture),
        };
        args.AddRange(echoArgs);
        return args;
    }

    [Fact]
    public async Task RunCoreAsync_RegulaererErfolg_LiefertFalse()
    {
        var runner = new YtDlpRunner();

        bool needsRestart = await runner.RunCoreAsync(
            "powershell.exe", BuildArgs(0, 0), onStdOut: null, onStdErr: null, idleTimeout: null, CancellationToken.None);

        Assert.False(needsRestart);
    }

    [Fact]
    public async Task RunCoreAsync_FehlerExitcode_WirftOhneNeustartanfrage()
    {
        var runner = new YtDlpRunner();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runner.RunCoreAsync("powershell.exe", BuildArgs(7, 0), onStdOut: null, onStdErr: null, idleTimeout: null, CancellationToken.None));

        Assert.Contains("7", ex.Message);
    }

    [Fact]
    public async Task RunCoreAsync_LeitetAusgabezeilenWeiter()
    {
        var runner = new YtDlpRunner();
        var received = new List<string>();

        await runner.RunCoreAsync(
            "powershell.exe", BuildArgs(0, 0, "eins", "zwei"),
            onStdOut: line => { lock (received) received.Add(line); },
            onStdErr: null, idleTimeout: null, ct: CancellationToken.None);

        Assert.Equal(["ARG:eins", "ARG:zwei"], received);
    }

    [Fact]
    public async Task RequestRestart_WaehrendDesLaufs_LiefertTrueStattEinerAusnahme()
    {
        // Entspricht dem heutigen Fall "Bandbreite geändert, während ein Download läuft":
        // Der Prozess wird gezielt beendet, RunCoreAsync meldet "neu starten", nicht
        // "fehlgeschlagen" - obwohl der Prozess durch Kill und nicht regulär endet.
        var runner = new YtDlpRunner();

        var runTask = runner.RunCoreAsync(
            "powershell.exe", BuildArgs(0, 5000), onStdOut: null, onStdErr: null, idleTimeout: null, CancellationToken.None);

        await Task.Delay(300);
        runner.RequestRestart();

        bool needsRestart = await runTask;

        Assert.True(needsRestart);
    }

    [Fact]
    public async Task RequestRestart_OhneLaufendenProzess_TutNichtsUndLiefertFalse()
    {
        var runner = new YtDlpRunner();

        bool result = false;
        var ex = Record.Exception(() => result = runner.RequestRestart());

        Assert.Null(ex);
        Assert.False(result);
    }

    [Fact]
    public async Task RequestRestart_WaehrendDesLaufs_LiefertTrue()
    {
        var runner = new YtDlpRunner();
        var runTask = runner.RunCoreAsync(
            "powershell.exe", BuildArgs(0, 5000), onStdOut: null, onStdErr: null, idleTimeout: null, CancellationToken.None);
        await Task.Delay(300);

        bool result = runner.RequestRestart();

        Assert.True(result);
        await runTask;
    }

    [Fact]
    public async Task RequestRestart_NachAbgeschlossenemLauf_WirktNichtMehrAufDenNaechsten()
    {
        // Der Process-Verweis wird nach jedem Lauf verworfen (finally) - eine verspätete
        // RequestRestart-Anfrage darf sich nicht auf einen späteren, unabhängigen Lauf
        // auswirken.
        var runner = new YtDlpRunner();

        bool first = await runner.RunCoreAsync(
            "powershell.exe", BuildArgs(0, 0), onStdOut: null, onStdErr: null, idleTimeout: null, CancellationToken.None);
        Assert.False(first);

        runner.RequestRestart(); // kein laufender Prozess mehr - darf nichts bewirken

        bool second = await runner.RunCoreAsync(
            "powershell.exe", BuildArgs(0, 0), onStdOut: null, onStdErr: null, idleTimeout: null, CancellationToken.None);

        Assert.False(second);
    }

    [Fact]
    public async Task RunCoreAsync_RegulaererErfolg_RuftOnExitCodeMitDemCodeAuf()
    {
        var runner = new YtDlpRunner();
        int? reported = null;

        await runner.RunCoreAsync(
            "powershell.exe", BuildArgs(0, 0), onStdOut: null, onStdErr: null, idleTimeout: null,
            CancellationToken.None, onExitCode: code => reported = code);

        Assert.Equal(0, reported);
    }

    [Fact]
    public async Task RunCoreAsync_FehlerExitcode_RuftOnExitCodeVorDerAusnahmeAuf()
    {
        // Der Aufrufer soll den Exit-Code auch im Fehlerfall protokollieren können - siehe
        // Kommentar in RunCoreAsync dazu, warum der Aufruf bewusst vor dem throw steht.
        var runner = new YtDlpRunner();
        int? reported = null;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runner.RunCoreAsync(
                "powershell.exe", BuildArgs(7, 0), onStdOut: null, onStdErr: null, idleTimeout: null,
                CancellationToken.None, onExitCode: code => reported = code));

        Assert.Equal(7, reported);
    }

    [Fact]
    public async Task RunCoreAsync_NeustartAngefordert_RuftOnExitCodeNichtAuf()
    {
        var runner = new YtDlpRunner();
        bool called = false;

        var runTask = runner.RunCoreAsync(
            "powershell.exe", BuildArgs(0, 5000), onStdOut: null, onStdErr: null, idleTimeout: null,
            CancellationToken.None, onExitCode: _ => called = true);
        await Task.Delay(300);
        runner.RequestRestart();
        await runTask;

        Assert.False(called);
    }

    // Kein Test dafür, dass eine echte externe Abbruchanfrage Vorrang vor einer bereits
    // gesetzten Neustartanfrage hat (im Code: ct.ThrowIfCancellationRequested() läuft in
    // beiden Zweigen von RunCoreAsync, siehe dort). RequestRestart() beendet den Prozess als
    // Teil seiner eigenen Aufgabe - ein danach ausgelöster cts.Cancel() liefert sich ein
    // echtes Wettrennen mit dieser selbst ausgelösten Prozessbeendigung auf einem anderen
    // Thread, das sich mit einem echten Prozess nicht ohne einen eigenen Testdouble für den
    // Prozess deterministisch nachstellen lässt. Die Reihenfolge selbst ist unverändert aus
    // allen drei heutigen Implementierungen übernommen (siehe Klassenkommentar).

    /// <summary>Kein Test von <see cref="YtDlpRunner.RunAsync"/> selbst — die öffentliche
    /// Überladung lässt sich nicht gegen das Testskript führen, weil sie die komplette
    /// Argumentliste aus <see cref="YtDlpJob"/> baut und daher kein <c>-File</c> davorsetzen
    /// kann. Stattdessen: Die von <see cref="YtDlpArgumentBuilder.Build"/> erzeugten
    /// Argumente kommen als echte Prozessargumente unverändert an — insbesondere
    /// <c>--no-playlist</c> wird nicht als eigenständiges Skript-Argument zerlegt oder
    /// verschluckt. <c>RunAsync</c> selbst ist danach nur noch eine einzeilige Weiterleitung
    /// an <see cref="YtDlpRunner.RunCoreAsync"/>, die oben bereits geprüft ist.</summary>
    [Fact]
    public async Task Build_ErzeugteArgumente_KommenUnveraendertAlsEchteProzessargumenteAn()
    {
        var job = new YtDlpJob { Url = "ARG_URL", OutputTemplate = "ARG_OUTPUT" };
        var jobArgs = YtDlpArgumentBuilder.Build(job);
        var fullArgs = BuildArgs(0, 0).Concat(jobArgs).ToList();
        var received = new List<string>();

        bool needsRestart = await new YtDlpRunner().RunCoreAsync(
            "powershell.exe", fullArgs,
            onStdOut: line => { lock (received) received.Add(line); },
            onStdErr: null, idleTimeout: null, ct: CancellationToken.None);

        Assert.False(needsRestart);
        Assert.Contains("ARG:ARG_OUTPUT", received);
        Assert.Contains("ARG:ARG_URL", received);
        Assert.Contains("ARG:--no-playlist", received);
    }
}
