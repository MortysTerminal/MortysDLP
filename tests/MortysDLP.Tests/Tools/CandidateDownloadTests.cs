using MortysDLP.Helpers;
using MortysDLP.Services;
using MortysDLP.Services.Tools;
using MortysDLP.Tests.Releases;
using System.IO;
using System.Net;
using System.Net.Http;

namespace MortysDLP.Tests.Tools;

/// <summary>
/// Prüft <see cref="CandidateDownload"/> über einen gefälschten <see cref="HttpMessageHandler"/>:
/// wann auf die nächste Adresse ausgewichen wird — und wann bewusst nicht.
/// </summary>
public class CandidateDownloadTests : IDisposable
{
    // SHA-256 von "hello world", mit `printf 'hello world' | sha256sum` nachgerechnet.
    private const string Content = "hello world";
    private const string ContentSha256 = "b94d27b9934d3e08a52e52d7da7dabfac484efe37a5380ee9088f7ace2efcde9";

    // UrlSafety lässt nur bekannte Hosts zu - github.com steht auf der Freigabeliste.
    private const string FirstUrl = "https://github.com/o/r/releases/download/b2/paket.zip";
    private const string SecondUrl = "https://github.com/o/r/releases/download/b1/paket.zip";

    private readonly string _tempDir;
    private readonly string _targetPath;

    public CandidateDownloadTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "MortysDLP.Tests.CandidateDownload", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _targetPath = Path.Combine(_tempDir, "paket.zip");
    }

    public void Dispose()
    {
        Log.CloseForTests();
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* Best-Effort */ }
        GC.SuppressFinalize(this);
    }

    private static DownloadCandidate Candidate(string url, string? sha256 = ContentSha256) =>
        new(url, Content.Length, sha256);

    [Fact]
    public async Task ErsteAdresse404_ZweiteWirdGeladen()
    {
        var handler = new FakeHttpMessageHandler()
            .When("/b2/", status: HttpStatusCode.NotFound)
            .When("/b1/", content: Content);
        using var client = new HttpClient(handler);

        var result = await CandidateDownload.FirstReachableAsync(
            "test", [Candidate(FirstUrl), Candidate(SecondUrl)], _targetPath, null, CancellationToken.None, client);

        Assert.True(result.ChecksumChecked);
        Assert.Equal(Content, File.ReadAllText(_targetPath));
        Assert.Equal(1, handler.CallCount("/b1/"));
    }

    /// <summary>Eine falsche Prüfsumme ist ein Warnsignal — kein zweiter Versuch darf sie
    /// überdecken.</summary>
    [Fact]
    public async Task FalschePruefsumme_KeinAusweichen()
    {
        var handler = new FakeHttpMessageHandler()
            .When("/b2/", content: "manipuliert")
            .When("/b1/", content: Content);
        using var client = new HttpClient(handler);
        var wrongFirst = new DownloadCandidate(FirstUrl, null, ContentSha256);

        await Assert.ThrowsAsync<ChecksumMismatchException>(() => CandidateDownload.FirstReachableAsync(
            "test", [wrongFirst, Candidate(SecondUrl)], _targetPath, null, CancellationToken.None, client));

        Assert.Equal(0, handler.CallCount("/b1/"));
        Assert.False(File.Exists(_targetPath));
    }

    [Fact]
    public async Task LetzteAdresse404_WirftDieHttpAusnahme()
    {
        var handler = new FakeHttpMessageHandler()
            .When("/b2/", status: HttpStatusCode.NotFound)
            .When("/b1/", status: HttpStatusCode.NotFound);
        using var client = new HttpClient(handler);

        await Assert.ThrowsAsync<HttpRequestException>(() => CandidateDownload.FirstReachableAsync(
            "test", [Candidate(FirstUrl), Candidate(SecondUrl)], _targetPath, null, CancellationToken.None, client));

        Assert.Equal(1, handler.CallCount("/b1/"));
    }

    [Fact]
    public async Task ErsteAdresseErreichbar_ZweiteWirdNieAngefragt()
    {
        var handler = new FakeHttpMessageHandler()
            .When("/b2/", content: Content)
            .When("/b1/", content: Content);
        using var client = new HttpClient(handler);

        await CandidateDownload.FirstReachableAsync(
            "test", [Candidate(FirstUrl), Candidate(SecondUrl)], _targetPath, null, CancellationToken.None, client);

        Assert.Equal(0, handler.CallCount("/b1/"));
    }

    [Fact]
    public async Task OhneKandidaten_WirftArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => CandidateDownload.FirstReachableAsync(
            "test", [], _targetPath, null, CancellationToken.None));
    }
}
