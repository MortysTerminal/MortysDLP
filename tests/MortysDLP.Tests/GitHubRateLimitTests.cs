using MortysDLP.Helpers;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;

namespace MortysDLP.Tests;

/// <summary>
/// Prüft die Auswertung der GitHub-Kontingent-Kopfzeilen. <c>now</c> wird in jedem Test
/// explizit übergeben — kein echtes Warten nötig. xUnit erzeugt pro Testmethode eine neue
/// Instanz dieser Klasse, der Konstruktor setzt deshalb vor jedem Test den (statischen)
/// Zustand von <see cref="GitHubRateLimit"/> zurück. Siehe <c>werkstatt/tasks/W2-T03.md</c>.
/// </summary>
public class GitHubRateLimitTests
{
    public GitHubRateLimitTests()
    {
        GitHubRateLimit.ResetForTests();
    }

    private static readonly DateTimeOffset Now =
        DateTimeOffset.Parse("2026-08-27T10:00:00Z", CultureInfo.InvariantCulture);

    private static HttpResponseHeaders MakeHeaders(params (string Name, string Value)[] pairs)
    {
        var response = new HttpResponseMessage();
        foreach (var (name, value) in pairs)
            response.Headers.TryAddWithoutValidation(name, value);
        return response.Headers;
    }

    [Fact]
    public void IsExhausted_OhneVorherigeBeobachtung_IstFalse()
    {
        Assert.False(GitHubRateLimit.IsExhausted(Now));
    }

    [Fact]
    public void Observe_RemainingNull_MarkiertBisResetAlsErschoepft()
    {
        long resetUnix = Now.AddMinutes(20).ToUnixTimeSeconds();
        var headers = MakeHeaders(
            ("X-RateLimit-Remaining", "0"),
            ("X-RateLimit-Reset", resetUnix.ToString(CultureInfo.InvariantCulture)));

        GitHubRateLimit.Observe(headers, Now);

        Assert.True(GitHubRateLimit.IsExhausted(Now));
        Assert.True(GitHubRateLimit.IsExhausted(Now.AddMinutes(19)));
    }

    [Fact]
    public void IsExhausted_NachResetZeitpunkt_WiederFrei()
    {
        long resetUnix = Now.AddMinutes(5).ToUnixTimeSeconds();
        var headers = MakeHeaders(
            ("X-RateLimit-Remaining", "0"),
            ("X-RateLimit-Reset", resetUnix.ToString(CultureInfo.InvariantCulture)));

        GitHubRateLimit.Observe(headers, Now);

        Assert.False(GitHubRateLimit.IsExhausted(Now.AddMinutes(5)));
        Assert.False(GitHubRateLimit.IsExhausted(Now.AddMinutes(6)));
    }

    [Fact]
    public void Observe_RemainingUeberSchwelle_AendertNichts()
    {
        var headers = MakeHeaders(("X-RateLimit-Remaining", "42"));

        GitHubRateLimit.Observe(headers, Now);

        Assert.False(GitHubRateLimit.IsExhausted(Now));
    }

    [Fact]
    public void Observe_GenauAufDerSchwelle_GiltNochNichtAlsErschoepft()
    {
        var headers = MakeHeaders(("X-RateLimit-Remaining", "5"));

        GitHubRateLimit.Observe(headers, Now);

        Assert.False(GitHubRateLimit.IsExhausted(Now));
    }

    [Fact]
    public void Observe_FehlendeKopfzeilen_AendernNichts()
    {
        var headers = MakeHeaders();

        GitHubRateLimit.Observe(headers, Now);

        Assert.False(GitHubRateLimit.IsExhausted(Now));
    }

    [Fact]
    public void Observe_NurRemainingOhneReset_BegrenztAufEineStunde()
    {
        var headers = MakeHeaders(("X-RateLimit-Remaining", "1"));

        GitHubRateLimit.Observe(headers, Now);

        Assert.True(GitHubRateLimit.IsExhausted(Now.AddMinutes(59)));
        Assert.False(GitHubRateLimit.IsExhausted(Now.AddHours(1).AddSeconds(1)));
    }

    [Fact]
    public void Observe_ResetWeiterAlsEineStundeEntfernt_WirdAufEineStundeGekappt()
    {
        long resetUnix = Now.AddHours(5).ToUnixTimeSeconds();
        var headers = MakeHeaders(
            ("X-RateLimit-Remaining", "0"),
            ("X-RateLimit-Reset", resetUnix.ToString(CultureInfo.InvariantCulture)));

        GitHubRateLimit.Observe(headers, Now);

        Assert.False(GitHubRateLimit.IsExhausted(Now.AddHours(1).AddSeconds(1)));
    }

    [Fact]
    public void Observe_UngueltigerRemainingWert_WirftKeineAusnahmeUndAendertNichts()
    {
        var headers = MakeHeaders(("X-RateLimit-Remaining", "nicht-numerisch"));

        var exception = Record.Exception(() => GitHubRateLimit.Observe(headers, Now));

        Assert.Null(exception);
        Assert.False(GitHubRateLimit.IsExhausted(Now));
    }
}
