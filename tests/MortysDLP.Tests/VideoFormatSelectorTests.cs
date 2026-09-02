using MortysDLP.Services;

namespace MortysDLP.Tests;

/// <summary>Hält das Ist-Verhalten von <see cref="YtDlpArgumentBuilder.BuildYtDlpVideoFormatSelector"/> fest.
/// Erwartungswerte sind 1:1 aus dem aktuellen Code abgeleitet, nicht aus einer Spezifikation.</summary>
public class VideoFormatSelectorTests
{
    [Theory]
    // mp4: AV1 im Container erlaubt -> Container-Filter genügt
    [InlineData("best", "mp4", "bestvideo[ext=mp4]+bestaudio[ext=m4a]/bestvideo+bestaudio/best")]
    // mov: kein AV1 -> H.264 (avc1) erzwingen
    [InlineData("best", "mov", "bestvideo[vcodec^=avc1]+bestaudio[ext=m4a]/bestvideo+bestaudio/best")]
    // avi: wie mov, kein AV1/VP9 praxistauglich
    [InlineData("best", "avi", "bestvideo[vcodec^=avc1]+bestaudio[ext=m4a]/bestvideo+bestaudio/best")]
    // mkv: akzeptiert alle Codecs -> kein Filter
    [InlineData("best", "mkv", "bestvideo+bestaudio/bestvideo+bestaudio/best")]
    public void Selektor_FiltertCodecsPassendZumContainer(string qualitaetsTag, string container, string erwartet)
    {
        var ergebnis = YtDlpArgumentBuilder.BuildYtDlpVideoFormatSelector(qualitaetsTag, container);

        Assert.Equal(erwartet, ergebnis);
    }

    [Theory]
    [InlineData("1080", "mp4", "bestvideo[ext=mp4][height<=1080]+bestaudio[ext=m4a]/bestvideo[height<=1080]+bestaudio/best[height<=1080]")]
    [InlineData("720", "mkv", "bestvideo[height<=720]+bestaudio/bestvideo[height<=720]+bestaudio/best[height<=720]")]
    [InlineData("480", "mov", "bestvideo[vcodec^=avc1][height<=480]+bestaudio[ext=m4a]/bestvideo[height<=480]+bestaudio/best[height<=480]")]
    public void Selektor_SetztHoehenbegrenzung(string qualitaetsTag, string container, string erwartet)
    {
        var ergebnis = YtDlpArgumentBuilder.BuildYtDlpVideoFormatSelector(qualitaetsTag, container);

        Assert.Equal(erwartet, ergebnis);
    }

    [Theory]
    [InlineData("", "mp4")]
    [InlineData("unbekannt", "mp4")]
    public void Selektor_BehandeltLeeresUndUngueltigesTagWieBest(string qualitaetsTag, string container)
    {
        var ergebnis = YtDlpArgumentBuilder.BuildYtDlpVideoFormatSelector(qualitaetsTag, container);
        var erwartet = YtDlpArgumentBuilder.BuildYtDlpVideoFormatSelector("best", container);

        Assert.Equal(erwartet, ergebnis);
    }

    [Fact]
    public void Selektor_HatImmerEinenDreifachenRueckfall()
    {
        var ergebnis = YtDlpArgumentBuilder.BuildYtDlpVideoFormatSelector("best", "mp4");

        // Jede Ausgabe muss zwei "/" enthalten, damit yt-dlp bei nicht
        // verfügbarem Format über zwei Stufen ausweichen kann.
        Assert.Equal(2, ergebnis.Count(c => c == '/'));
    }
}
