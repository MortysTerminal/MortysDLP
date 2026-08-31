using MortysDLP.Services.Releases;
using MortysDLP.Services.Tools;
using System.Linq;

namespace MortysDLP.Tests.Tools;

/// <summary>
/// Prüft den Aufbau des yt-dlp-Werkzeugs: Politik, Quellenkette und Anfrage. Installation und
/// Versionsabruf brauchen ein echtes Werkzeug und eine echte Verbindung und gehören deshalb in
/// den Handtestplan, nicht hierher.
/// </summary>
public class YtDlpToolTests
{
    private readonly YtDlpTool _tool = new();

    [Fact]
    public void IstFuerDenBetriebErforderlichUndVergleichtOrdnend()
    {
        Assert.Equal("yt-dlp", _tool.Id);
        Assert.True(_tool.RequiredForOperation);
        Assert.Equal(ToolUpdatePolicy.OnlyWhenNewer, _tool.UpdatePolicy);
    }

    [Fact]
    public void HatGenauEineZieldatei()
    {
        Assert.Single(_tool.TargetPaths);
        Assert.EndsWith("yt-dlp.exe", _tool.TargetPaths[0], StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Die Kette muss die GitHub-unabhängige PyPI-Quelle enthalten und sie vor den
    /// übrigen GitHub-Wegen fragen — sonst ist sie bei einem GitHub-Ausfall wirkungslos, obwohl sie
    /// genau dafür da ist.</summary>
    [Fact]
    public void Quellenkette_FragtPyPiVorDenUebrigenGitHubWegen()
    {
        var names = _tool.CreateSources().Select(s => s.Name).ToList();

        Assert.Equal("github-api-latest", names[0]);
        Assert.Equal("pypi", names[1]);
        Assert.Contains("github-atom", names);
        Assert.Contains("github-redirect", names);
        Assert.True(names.IndexOf("pypi") < names.IndexOf("github-atom"));
    }

    [Fact]
    public void Anfrage_NenntPaketnameUndAdressvorlage()
    {
        var query = _tool.CreateQuery();

        Assert.Equal("yt-dlp", query.Owner);
        Assert.Equal("yt-dlp", query.Repo);
        Assert.Equal("yt-dlp", query.PackageName);
        Assert.Equal("yt-dlp.exe", query.AssetPattern);
        Assert.NotNull(query.DownloadUrlTemplate);
        Assert.False(query.AllowPrerelease);
    }

    /// <summary>Die Adressvorlage muss sich mit dem Rohtag eines Releases zu einer nutzbaren
    /// Adresse auflösen — die Quellen ohne eigene Asset-Liste (PyPI, Atom, Weiterleitung) haben
    /// keine andere Möglichkeit, an das Paket zu kommen.</summary>
    [Fact]
    public void Adressvorlage_LoestSichZuEinerErlaubtenAdresseAuf()
    {
        string? url = _tool.CreateQuery().ResolveDownloadUrl("2026.08.19");

        Assert.Equal(
            "https://github.com/yt-dlp/yt-dlp/releases/download/2026.08.19/yt-dlp.exe",
            url);
        Assert.True(MortysDLP.Helpers.UrlSafety.IsAllowed(new Uri(url!)));
    }
}
