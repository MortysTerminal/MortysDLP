using MortysDLP.Services;
using System.Globalization;
using System.IO;

namespace MortysDLP.Tests;

/// <summary>
/// Prüft <see cref="FfmpegRunner"/> gegen ein eigenes, kleines PowerShell-Testskript, das
/// vorgegebene Zeilen auf die Standardfehlerausgabe schreibt - genau dort meldet ffmpeg seinen
/// Fortschritt (<c>time=HH:MM:SS.ff</c>), keiner der vier ursprünglichen Aufrufer hatte dafür
/// zuvor einen eigenen Test.
///
/// <para>Bewusst über das automatische <c>$args</c> gelesen, ohne eigenen <c>param()</c>-Block
/// (siehe <see cref="YtDlpRunnerTests"/> für die Begründung).</para>
/// </summary>
public class FfmpegRunnerTests : IDisposable
{
    private readonly string _scriptPath;

    public FfmpegRunnerTests()
    {
        _scriptPath = Path.Combine(Path.GetTempPath(), $"FfmpegRunnerTests_{Guid.NewGuid():N}.ps1");
        File.WriteAllText(_scriptPath, """
            [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
            $exitCode = [int]$args[0]
            $sleepMs = [int]$args[1]
            for ($i = 2; $i -lt $args.Count; $i++) { [Console]::Error.WriteLine($args[$i]) }
            if ($sleepMs -gt 0) { Start-Sleep -Milliseconds $sleepMs }
            exit $exitCode
            """);
    }

    public void Dispose()
    {
        try { File.Delete(_scriptPath); } catch { /* Best-Effort */ }
        GC.SuppressFinalize(this);
    }

    private List<string> BuildArgs(int exitCode, int sleepMs, params string[] stdErrLines)
    {
        var args = new List<string>
        {
            "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", _scriptPath,
            exitCode.ToString(CultureInfo.InvariantCulture), sleepMs.ToString(CultureInfo.InvariantCulture),
        };
        args.AddRange(stdErrLines);
        return args;
    }

    [Fact]
    public async Task RunAsync_ZeitZeilenMitBekannterGesamtdauer_BerechnetProzentAusDerZeit()
    {
        var received = new List<double>();

        await FfmpegRunner.RunAsync(
            "powershell.exe",
            BuildArgs(0, 0, "time=00:00:01.00 bitrate=100kbits/s", "time=00:00:02.50 bitrate=100kbits/s"),
            totalSeconds: 5.0,
            onStdErrLine: null,
            onProgress: pct => received.Add(pct),
            CancellationToken.None);

        Assert.Equal([20.0, 50.0], received);
    }

    [Fact]
    public async Task RunAsync_GesamtdauerUnbekannt_RuftOnProgressNieAuf()
    {
        bool called = false;

        await FfmpegRunner.RunAsync(
            "powershell.exe",
            BuildArgs(0, 0, "time=00:00:01.00"),
            totalSeconds: 0,
            onStdErrLine: null,
            onProgress: _ => called = true,
            CancellationToken.None);

        Assert.False(called);
    }

    [Fact]
    public async Task RunAsync_ZeitUeberschreitetGesamtdauer_KlemmtProzentAufHundert()
    {
        double? received = null;

        await FfmpegRunner.RunAsync(
            "powershell.exe",
            BuildArgs(0, 0, "time=00:00:10.00"),
            totalSeconds: 5.0,
            onStdErrLine: null,
            onProgress: pct => received = pct,
            CancellationToken.None);

        Assert.Equal(100.0, received);
    }

    [Fact]
    public async Task RunAsync_ZeileOhneZeitangabe_WirdWeitergeleitetAberLoestKeinenFortschrittAus()
    {
        var lines = new List<string>();
        bool progressCalled = false;

        await FfmpegRunner.RunAsync(
            "powershell.exe",
            BuildArgs(0, 0, "frame=  120 fps=30 q=28.0 size=1024kB"),
            totalSeconds: 5.0,
            onStdErrLine: line => lines.Add(line),
            onProgress: _ => progressCalled = true,
            CancellationToken.None);

        Assert.Contains("frame=  120 fps=30 q=28.0 size=1024kB", lines);
        Assert.False(progressCalled);
    }

    [Fact]
    public async Task RunAsync_RegulaererErfolg_LiefertErfolgreichesErgebnis()
    {
        var result = await FfmpegRunner.RunAsync(
            "powershell.exe", BuildArgs(0, 0), totalSeconds: 0, onStdErrLine: null, onProgress: null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task RunAsync_FehlerExitcode_WirftNichtSelbstSondernLiefertDasErgebnis()
    {
        // FfmpegRunner entscheidet bewusst nicht selbst über Erfolg/Fehlschlag (siehe
        // Klassenkommentar) - ConvertPage braucht das rohe Ergebnis, um bei mehreren parallel
        // laufenden Dateien nur die betroffene zu markieren, statt die übrigen abzubrechen.
        var result = await FfmpegRunner.RunAsync(
            "powershell.exe", BuildArgs(7, 0), totalSeconds: 0, onStdErrLine: null, onProgress: null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(7, result.ExitCode);
    }

    [Fact]
    public async Task RunAsync_AbbruchWaehrendDesLaufs_WirftOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        var runTask = FfmpegRunner.RunAsync(
            "powershell.exe", BuildArgs(0, 5000), totalSeconds: 0, onStdErrLine: null, onProgress: null, cts.Token);

        await Task.Delay(300);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);
    }
}
