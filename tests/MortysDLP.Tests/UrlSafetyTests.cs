using MortysDLP.Helpers;
using System;
using System.Security;

namespace MortysDLP.Tests;

/// <summary>
/// Prüft die Zielprüfung für Netzwerkanfragen (<see cref="UrlSafety"/>) — reine Logik ohne
/// Netzzugriff.
/// </summary>
public class UrlSafetyTests
{
    [Theory]
    [InlineData("https://github.com/MortysTerminal/MortysDLP")]
    [InlineData("https://api.github.com/repos/x/y")]
    [InlineData("https://objects.githubusercontent.com/foo")]
    [InlineData("https://raw.githubusercontent.com/foo")]
    [InlineData("https://huggingface.co/foo")]
    [InlineData("https://hf-mirror.com/foo")]
    [InlineData("https://pypi.org/pypi/yt-dlp/json")]
    [InlineData("https://files.pythonhosted.org/foo")]
    [InlineData("https://www.gyan.dev/ffmpeg/builds/release-version")]
    public void IsAllowed_FreigegebenerHost_IstTrue(string url)
    {
        Assert.True(UrlSafety.IsAllowed(new Uri(url)));
    }

    [Theory]
    [InlineData("https://cdn-lfs.huggingface.co/foo")]
    [InlineData("https://objects.githubusercontent.com.evil.com/foo")]
    public void IsAllowed_UnterdomaeneEinesFreigegebenenHosts(string url)
    {
        // cdn-lfs.huggingface.co ist eine echte Unterdomäne und muss durchgelassen werden;
        // objects.githubusercontent.com.evil.com ist KEINE Unterdomäne (der freigegebene Host
        // steht nicht am Ende der Punkt-Grenze) und muss abgelehnt werden.
        bool expected = url.Contains("evil.com") is false;

        Assert.Equal(expected, UrlSafety.IsAllowed(new Uri(url)));
    }

    [Theory]
    [InlineData("http://github.com/x")]
    [InlineData("http://api.github.com/x")]
    public void IsAllowed_Http_IstFalse(string url)
    {
        Assert.False(UrlSafety.IsAllowed(new Uri(url)));
    }

    [Theory]
    [InlineData("https://evilgithub.com/x")]
    [InlineData("https://github.com.evil.com/x")]
    [InlineData("https://evil.com/x")]
    [InlineData("https://notgithub.com/x")]
    public void IsAllowed_FremderHost_IstFalse(string url)
    {
        Assert.False(UrlSafety.IsAllowed(new Uri(url)));
    }

    [Fact]
    public void IsAllowed_HomoglyphHost_IstFalse()
    {
        // "gіthub.com" sieht aus wie "github.com", das 'i' ist aber der kyrillische
        // Buchstabe U+0456 - ein anderer Host. uri.IdnHost wandelt ihn in Punycode um
        // ("xn--gthub-..."), das keinesfalls dem Klartext-Eintrag "github.com" entspricht.
        var uri = new Uri("https://gіthub.com/x");

        Assert.False(UrlSafety.IsAllowed(uri));
    }

    [Fact]
    public void IsAllowed_RelativeUri_IstFalse()
    {
        Assert.False(UrlSafety.IsAllowed(new Uri("/relative/path", UriKind.Relative)));
    }

    [Fact]
    public void IsAllowed_Null_IstFalse()
    {
        Assert.False(UrlSafety.IsAllowed(null));
    }

    [Fact]
    public void EnsureAllowed_FreigegebenerHost_WirftNicht()
    {
        var exception = Record.Exception(() => UrlSafety.EnsureAllowed(new Uri("https://github.com/x")));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAllowed_FremderHost_WirftSecurityException()
    {
        Assert.Throws<SecurityException>(() => UrlSafety.EnsureAllowed(new Uri("https://evil.com/x")));
    }
}
