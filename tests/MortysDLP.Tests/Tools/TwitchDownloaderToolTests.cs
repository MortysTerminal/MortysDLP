using MortysDLP.Models;
using MortysDLP.Services.Releases;
using MortysDLP.Services.Tools;
using System.Linq;

namespace MortysDLP.Tests.Tools;

/// <summary>
/// Prüft TwitchDownloaderCLI: Aufbau des Werkzeugs, den Identitätsnachweis über die echte
/// <c>--version</c>-Ausgabe der installierten Datei (am 2026-09-01 abgeschrieben, nicht erfunden)
/// und die Asset-Auswahl gegen die echte Anhangsliste des Releases. Installation und
/// Versionsabruf brauchen ein echtes Werkzeug und eine echte Verbindung und gehören deshalb in
/// den Handtestplan, nicht hierher.
/// </summary>
public class TwitchDownloaderToolTests
{
    /// <summary>Erste Zeile von <c>TwitchDownloaderCLI --version</c>, an der lokal installierten
    /// Datei geprüft (2026-09-01).</summary>
    private const string RealOutput = "TwitchDownloaderCLI 1.56.5+f8335cabef7436c362b13703359d440a26065bc4";

    private readonly TwitchDownloaderTool _tool = new();

    [Fact]
    public void IstOptionalUndVergleichtOrdnend()
    {
        Assert.Equal("twitch-downloader", _tool.Id);
        Assert.False(_tool.RequiredForOperation);
        Assert.Equal(ToolUpdatePolicy.OnlyWhenNewer, _tool.UpdatePolicy);
    }

    [Fact]
    public void HatGenauEineZieldatei()
    {
        Assert.Single(_tool.TargetPaths);
        Assert.EndsWith("TwitchDownloaderCLI.exe", _tool.TargetPaths[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Quellenkette_HatDreiGitHubWege()
    {
        var names = _tool.CreateSources().Select(s => s.Name).ToList();

        Assert.Equal(["github-api-latest", "github-atom", "github-redirect"], names);
    }

    [Fact]
    public void Anfrage_NenntOwnerRepoUndPlatzhalterMuster()
    {
        var query = _tool.CreateQuery();

        Assert.Equal("lay295", query.Owner);
        Assert.Equal("TwitchDownloader", query.Repo);
        // Anders als bei yt-dlp und whisper.cpp trägt der Dateiname die Version - deshalb ein
        // Muster mit Platzhalter statt eines festen Namens.
        Assert.Contains('*', query.AssetPattern!);
        Assert.NotNull(query.DownloadUrlTemplate);
    }

    [Fact]
    public void Adressvorlage_LoestSichZuEinerErlaubtenAdresseAuf()
    {
        string? url = _tool.CreateQuery().ResolveDownloadUrl("1.56.5");

        Assert.Equal(
            "https://github.com/lay295/TwitchDownloader/releases/download/1.56.5/TwitchDownloaderCLI-1.56.5-Windows-x64.zip",
            url);
        Assert.True(MortysDLP.Helpers.UrlSafety.IsAllowed(new Uri(url!)));
    }

    /// <summary>Das Muster muss den passenden Windows-Anhang treffen und die anderen fünf
    /// Plattform-Anhänge sowie die GUI-Variante verwerfen - am echten Release vom 2026-09-01
    /// geprüft.</summary>
    [Fact]
    public void AssetMuster_TrifftNurDenWindowsCliAnhang()
    {
        var assets = new List<ReleaseAsset>
        {
            new("TwitchDownloaderCLI-1.56.5-Linux-x64.zip", "https://example.invalid/linux", 1),
            new("TwitchDownloaderCLI-1.56.5-LinuxAlpine-x64.zip", "https://example.invalid/alpine", 1),
            new("TwitchDownloaderCLI-1.56.5-MacOS-x64.zip", "https://example.invalid/mac", 1),
            new("TwitchDownloaderCLI-1.56.5-MacOSArm64.zip", "https://example.invalid/macarm", 1),
            new("TwitchDownloaderGUI-1.56.5-Windows-x64.zip", "https://example.invalid/gui", 1),
            new("TwitchDownloaderCLI-1.56.5-Windows-x64.zip", "https://example.invalid/win", 68979921),
        };

        var selected = AssetSelector.Select(assets, _tool.CreateQuery().AssetPattern!);

        Assert.NotNull(selected);
        Assert.Equal("TwitchDownloaderCLI-1.56.5-Windows-x64.zip", selected!.Name);
    }

    [Fact]
    public void EchteAusgabe_WirdAlsVersionGelesen()
    {
        string? version = TwitchDownloaderTool.ExtractVersionToken(RealOutput);

        Assert.Equal("1.56.5+f8335cabef7436c362b13703359d440a26065bc4", version);
        Assert.True(ToolVersion.Parse(version).HasNumericCore);
    }

    /// <summary>Die Datei-Version <c>1.56.5.0</c> aus der Versionsressource (viertes Segment
    /// <c>0</c>) muss als dieselbe Ausgabe gelten wie das vom Release gemeldete <c>1.56.5</c> -
    /// sonst böte MortysDLP der frisch installierten Datei sich selbst als Update an.</summary>
    [Fact]
    public void DateiVersionMitNullSegment_GiltAlsDieselbeAusgabeWieDerTag()
    {
        var local = ToolVersion.Parse("1.56.5.0");
        var remote = ToolVersion.Parse("1.56.5");

        Assert.True(local.IsSameRelease(remote));

        var verdict = ToolUpdateDecision.Evaluate(local, remote, _tool.UpdatePolicy);
        Assert.False(verdict.Offer);
    }

    /// <summary>Der Identitätsnachweis: Die Zeile muss mit <c>TwitchDownloaderCLI </c> beginnen.</summary>
    [Theory]
    [InlineData("git version 2.55.0.windows.3")]
    [InlineData("1.56.5+f8335cab")]
    [InlineData("TwitchDownloaderCLIx 1.56.5")]
    [InlineData("")]
    [InlineData("   ")]
    public void FremdeAusgabe_WirdNichtAlsVersionGelesen(string output)
    {
        Assert.Null(TwitchDownloaderTool.ExtractVersionToken(output));
    }
}
