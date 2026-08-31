using MortysDLP.Helpers;
using MortysDLP.Services;
using MortysDLP.Tests.Releases;
using System.IO;
using System.Net;
using System.Net.Http;

namespace MortysDLP.Tests;

/// <summary>
/// Prüft <see cref="VerifiedDownload"/> über einen gefälschten <see cref="HttpMessageHandler"/>
/// — kein echter Netzzugriff, keine echte Datei außerhalb eines eigenen Temp-Verzeichnisses.
///
/// </summary>
public class VerifiedDownloadTests : IDisposable
{
    // SHA-256 von "hello world", mit `printf 'hello world' | sha256sum` nachgerechnet -
    // kein erfundener Wert.
    private const string Content = "hello world";
    private const string ContentSha256 = "b94d27b9934d3e08a52e52d7da7dabfac484efe37a5380ee9088f7ace2efcde9";

    // UrlSafety lässt nur bekannte Hosts zu - github.com steht auf der Freigabeliste.
    private const string DownloadUrl =
        "https://github.com/MortysTerminal/MortysDLP/releases/download/2026.06.01/MortysDLP.zip";

    private readonly string _tempDir;
    private readonly string _targetPath;

    public VerifiedDownloadTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "MortysDLP.Tests.VerifiedDownload", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _targetPath = Path.Combine(_tempDir, "MortysDLP.zip");
    }

    public void Dispose()
    {
        Log.CloseForTests();
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* Best-Effort */ }
        GC.SuppressFinalize(this);
    }

    private static HttpClient MakeClient(HttpStatusCode status = HttpStatusCode.OK) =>
        new(new FakeHttpMessageHandler().When(".*", status: status, content: Content));

    [Fact]
    public async Task ToFileAsync_PassendePruefsummeUndGroesse_DateiWirdVerifiziertAbgelegt()
    {
        using var client = MakeClient();

        var result = await VerifiedDownload.ToFileAsync(
            DownloadUrl, _targetPath, ContentSha256, Content.Length,
            progress: null, CancellationToken.None, client);

        Assert.True(File.Exists(_targetPath));
        Assert.False(File.Exists(_targetPath + ".part"));
        Assert.Equal(ContentSha256, result.Sha256);
        Assert.True(result.ChecksumChecked);
        Assert.True(result.SizeChecked);
        Assert.Equal(Content.Length, result.Bytes);
    }

    [Fact]
    public async Task ToFileAsync_FalschePruefsumme_WirftUndHinterlaesstKeineReste()
    {
        using var client = MakeClient();
        string falscheSha = new string('a', 64);

        var ex = await Assert.ThrowsAsync<ChecksumMismatchException>(() =>
            VerifiedDownload.ToFileAsync(
                DownloadUrl, _targetPath, falscheSha, null,
                progress: null, CancellationToken.None, client));

        Assert.Equal(falscheSha, ex.Expected);
        Assert.Equal(ContentSha256, ex.Actual);
        Assert.False(File.Exists(_targetPath));
        Assert.False(File.Exists(_targetPath + ".part"));
    }

    [Fact]
    public async Task ToFileAsync_FalscheGroesse_WirdErkanntUndHinterlaesstKeineReste()
    {
        using var client = MakeClient();

        await Assert.ThrowsAsync<IOException>(() =>
            VerifiedDownload.ToFileAsync(
                DownloadUrl, _targetPath, null, Content.Length + 5,
                progress: null, CancellationToken.None, client));

        Assert.False(File.Exists(_targetPath));
        Assert.False(File.Exists(_targetPath + ".part"));
    }

    [Fact]
    public async Task ToFileAsync_OhneErwartetePruefsumme_DateiBleibtUndWarnungWirdProtokolliert()
    {
        string tempLogDir = Path.Combine(_tempDir, "logs");
        Directory.CreateDirectory(tempLogDir);
        Log.LogsDirectory = tempLogDir;
        Log.MinLevel = LogLevel.Debug;

        using var client = MakeClient();

        var result = await VerifiedDownload.ToFileAsync(
            DownloadUrl, _targetPath, null, null,
            progress: null, CancellationToken.None, client);
        Log.CloseForTests();

        Assert.True(File.Exists(_targetPath));
        Assert.False(result.ChecksumChecked);
        Assert.False(result.SizeChecked);
        Assert.Contains("Kein erwarteter SHA-256", File.ReadAllText(Log.CurrentLogFile));
    }

    [Fact]
    public async Task ToFileAsync_AbbruchMittendrin_HinterlaesstKeineReste()
    {
        var handler = new FakeHttpMessageHandler().When(".*", content: Content, delay: TimeSpan.FromSeconds(5));
        using var client = new HttpClient(handler);
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(30));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            VerifiedDownload.ToFileAsync(
                DownloadUrl, _targetPath, ContentSha256, null,
                progress: null, cts.Token, client));

        Assert.False(File.Exists(_targetPath));
        Assert.False(File.Exists(_targetPath + ".part"));
    }

    [Fact]
    public async Task ToFileAsync_BerechneterSha256_StimmtMitBekanntemReferenzwertUeberein()
    {
        using var client = MakeClient();

        var result = await VerifiedDownload.ToFileAsync(
            DownloadUrl, _targetPath, null, null,
            progress: null, CancellationToken.None, client);

        Assert.Equal(ContentSha256, result.Sha256);
    }

    [Fact]
    public async Task ToFileAsync_FehlerStatus_WirftUndHinterlaesstKeineReste()
    {
        // 404 wird nicht wiederholt (RetryPolicy) - hält den Test schnell.
        using var client = MakeClient(HttpStatusCode.NotFound);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            VerifiedDownload.ToFileAsync(
                DownloadUrl, _targetPath, ContentSha256, null,
                progress: null, CancellationToken.None, client));

        Assert.False(File.Exists(_targetPath));
        Assert.False(File.Exists(_targetPath + ".part"));
    }
}
