using MortysDLP.Services;

namespace MortysDLP.Tests;

/// <summary>
/// Prüft <see cref="DownloadProgressWeighting"/> — reine Logik, kein echter Download nötig.
/// Der zentrale Punkt: Der zurückgegebene Gesamtfortschritt darf über eine ganze Sequenz
/// (Stream 1 → Stream 2 → Merge → ggf. Nachkonvertierung, ggf. über mehrere Playlist-Videos)
/// niemals zurückspringen.
/// </summary>
public class DownloadProgressWeightingTests
{
    // ── ForStream ────────────────────────────────────────────────────────────────

    [Fact]
    public void ForStream_EinStream_NutztVollenBereichOhneNachkonvertierung()
    {
        Assert.Equal(0.0, DownloadProgressWeighting.ForStream(0, streamIndex: 0, streamCount: 1, reservePostConversion: false));
        Assert.Equal(50.0, DownloadProgressWeighting.ForStream(50, streamIndex: 0, streamCount: 1, reservePostConversion: false));
        Assert.Equal(100.0, DownloadProgressWeighting.ForStream(100, streamIndex: 0, streamCount: 1, reservePostConversion: false));
    }

    [Fact]
    public void ForStream_EinStreamMitNachkonvertierung_BleibtUnterDerObergrenze()
    {
        double result = DownloadProgressWeighting.ForStream(100, streamIndex: 0, streamCount: 1, reservePostConversion: true);

        Assert.Equal(90.0, result);
    }

    [Fact]
    public void ForStream_ZweiStreams_ErsterStreamBelegtErsteHaelfte()
    {
        double atStart = DownloadProgressWeighting.ForStream(0, streamIndex: 0, streamCount: 2, reservePostConversion: false);
        double atEnd = DownloadProgressWeighting.ForStream(100, streamIndex: 0, streamCount: 2, reservePostConversion: false);

        Assert.Equal(0.0, atStart);
        Assert.Equal(50.0, atEnd);
    }

    [Fact]
    public void ForStream_ZweiStreams_ZweiterStreamSetztBeimEndeDesErstenAn()
    {
        // Kein Rücksprung: Der zweite Stream beginnt exakt dort, wo der erste endete.
        double endOfFirst = DownloadProgressWeighting.ForStream(100, streamIndex: 0, streamCount: 2, reservePostConversion: false);
        double startOfSecond = DownloadProgressWeighting.ForStream(0, streamIndex: 1, streamCount: 2, reservePostConversion: false);

        Assert.Equal(endOfFirst, startOfSecond);
    }

    [Fact]
    public void ForStream_ZweiStreamsMitNachkonvertierung_ZweiterStreamEndetAnDerObergrenze()
    {
        double result = DownloadProgressWeighting.ForStream(100, streamIndex: 1, streamCount: 2, reservePostConversion: true);

        Assert.Equal(90.0, result);
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(105)]
    public void ForStream_RohprozentAusserhalbDesBereichs_WirdBegrenzt(double rawPercent)
    {
        double result = DownloadProgressWeighting.ForStream(rawPercent, streamIndex: 0, streamCount: 1, reservePostConversion: false);

        Assert.InRange(result, 0.0, 100.0);
    }

    [Fact]
    public void ForStream_StreamIndexAusserhalbDesBereichs_WirdBegrenzt()
    {
        // Sollte nicht vorkommen (mehr Streams als erwartet), darf aber nicht über die
        // Obergrenze hinausschießen.
        double result = DownloadProgressWeighting.ForStream(100, streamIndex: 5, streamCount: 2, reservePostConversion: false);

        Assert.Equal(100.0, result);
    }

    // ── ForMerge ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ForMerge_OhneNachkonvertierung_ErreichtHundert()
    {
        Assert.Equal(100.0, DownloadProgressWeighting.ForMerge(reservePostConversion: false));
    }

    [Fact]
    public void ForMerge_MitNachkonvertierung_BleibtUnterHundert()
    {
        Assert.Equal(90.0, DownloadProgressWeighting.ForMerge(reservePostConversion: true));
    }

    // ── ForPostConversion ────────────────────────────────────────────────────────

    [Fact]
    public void ForPostConversion_BeginntDortWoDerDownloadAufgehoertHat()
    {
        double atStart = DownloadProgressWeighting.ForPostConversion(0);

        Assert.Equal(90.0, atStart);
    }

    [Fact]
    public void ForPostConversion_ErreichtHundertAmEnde()
    {
        double atEnd = DownloadProgressWeighting.ForPostConversion(100);

        Assert.Equal(100.0, atEnd);
    }

    [Fact]
    public void ForPostConversion_KeinRuecksprungGegenueberDemEndeDesDownloads()
    {
        double endOfDownload = DownloadProgressWeighting.ForStream(100, streamIndex: 1, streamCount: 2, reservePostConversion: true);
        double startOfConversion = DownloadProgressWeighting.ForPostConversion(0);

        Assert.Equal(endOfDownload, startOfConversion);
    }

    // ── ForPlaylist ──────────────────────────────────────────────────────────────

    [Fact]
    public void ForPlaylist_ErstesVonDreiVideos_NutztErstesDrittel()
    {
        Assert.Equal(0.0, DownloadProgressWeighting.ForPlaylist(0, videoIndex: 0, videoCount: 3));
        Assert.Equal(100.0 / 3, DownloadProgressWeighting.ForPlaylist(100, videoIndex: 0, videoCount: 3), precision: 6);
    }

    [Fact]
    public void ForPlaylist_ZweitesVideo_KnuepftOhneRuecksprungAnErstesAn()
    {
        double endOfFirst = DownloadProgressWeighting.ForPlaylist(100, videoIndex: 0, videoCount: 3);
        double startOfSecond = DownloadProgressWeighting.ForPlaylist(0, videoIndex: 1, videoCount: 3);

        Assert.Equal(endOfFirst, startOfSecond, precision: 6);
    }

    [Fact]
    public void ForPlaylist_LetztesVideoFertig_ErreichtHundert()
    {
        double result = DownloadProgressWeighting.ForPlaylist(100, videoIndex: 2, videoCount: 3);

        Assert.Equal(100.0, result, precision: 6);
    }

    [Fact]
    public void ForPlaylist_KeinPlaylistKontext_GibtWertUnveraendertZurueck()
    {
        double result = DownloadProgressWeighting.ForPlaylist(42, videoIndex: 0, videoCount: 0);

        Assert.Equal(42.0, result);
    }

    [Fact]
    public void ForPlaylist_VideoIndexAusserhalbDesBereichs_WirdBegrenzt()
    {
        double result = DownloadProgressWeighting.ForPlaylist(100, videoIndex: 10, videoCount: 3);

        Assert.Equal(100.0, result, precision: 6);
    }
}
