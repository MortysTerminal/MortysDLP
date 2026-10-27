using MortysDLP.Services;

namespace MortysDLP.Tests;

/// <summary>
/// Prüft <see cref="WhisperProgressParser"/> gegen die echten Zeilenformate von whisper-cli
/// (<c>fprintf(stderr, "%s: progress = %3d%%\n")</c>, Segmentzeilen auf stdout) und ffmpeg.
/// </summary>
public class WhisperProgressParserTests
{
    [Fact]
    public void TryParsePercent_EchteStderrZeile_Liefert42()
    {
        bool ok = WhisperProgressParser.TryParsePercent("whisper_print_progress_callback: progress =  42%", out double f);

        Assert.True(ok);
        Assert.Equal(0.42, f, precision: 6);
    }

    [Fact]
    public void TryParsePercent_EinstelligMitMehrerenLeerzeichen_Liefert5()
    {
        Assert.True(WhisperProgressParser.TryParsePercent("whisper_print_progress_callback: progress =   5%", out double f));
        Assert.Equal(0.05, f, precision: 6);
    }

    [Fact]
    public void TryParsePercent_Hundert_Liefert1()
    {
        Assert.True(WhisperProgressParser.TryParsePercent("whisper_print_progress_callback: progress = 100%", out double f));
        Assert.Equal(1.0, f, precision: 6);
    }

    [Fact]
    public void TryParsePercent_UeberHundert_WirdAbgelehnt()
    {
        Assert.False(WhisperProgressParser.TryParsePercent("whisper_print_progress_callback: progress = 250%", out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("[00:00:00.000 --> 00:00:04.640]   Hallo Welt")]
    [InlineData("progress = abc%")]
    [InlineData("progress = 42")]
    public void TryParsePercent_KeineFortschrittszeile_LiefertFalse(string? line)
    {
        Assert.False(WhisperProgressParser.TryParsePercent(line, out _));
    }

    [Fact]
    public void TryParseSegmentEnd_EchteSegmentzeile_LiefertEndzeit()
    {
        bool ok = WhisperProgressParser.TryParseSegmentEnd("[00:00:00.000 --> 00:00:04.640]   Hallo und willkommen.", out var end);

        Assert.True(ok);
        Assert.Equal(new TimeSpan(0, 0, 0, 4, 640), end);
    }

    [Fact]
    public void TryParseSegmentEnd_ZeileMitStunden_LiefertEndzeit()
    {
        Assert.True(WhisperProgressParser.TryParseSegmentEnd("[01:02:03.500 --> 01:02:09.250]  Text", out var end));
        Assert.Equal(new TimeSpan(0, 1, 2, 9, 250), end);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("whisper_print_progress_callback: progress =  42%")]
    [InlineData("[00:00:00.000 --> kaputt]")]
    [InlineData("[00:00:00.000 --> 00:99:04.640] Text")]
    public void TryParseSegmentEnd_KeineSegmentzeile_LiefertFalse(string? line)
    {
        Assert.False(WhisperProgressParser.TryParseSegmentEnd(line, out _));
    }

    [Fact]
    public void TryParseFfmpegTime_EchteStatuszeile_LiefertZeit()
    {
        string line = "size=    2616KiB time=00:01:23.45 bitrate= 256.0kbits/s speed= 321x";

        Assert.True(WhisperProgressParser.TryParseFfmpegTime(line, out var t));
        Assert.Equal(new TimeSpan(0, 0, 1, 23, 450), t);
    }

    [Fact]
    public void TryParseFfmpegTime_TimeNichtVerfuegbar_LiefertFalse()
    {
        Assert.False(WhisperProgressParser.TryParseFfmpegTime("size=       0KiB time=N/A bitrate=N/A speed=N/A", out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Input #0, mov,mp4, from 'a.mp4':")]
    [InlineData("size=0KiB time=-00:00:00.03 bitrate=N/A")]
    public void TryParseFfmpegTime_KeineStatuszeile_LiefertFalse(string? line)
    {
        Assert.False(WhisperProgressParser.TryParseFfmpegTime(line, out _));
    }

    [Fact]
    public void TryGetFraction_UnbekannteDauer_LiefertFalse()
    {
        Assert.False(WhisperProgressParser.TryGetFraction(TimeSpan.FromSeconds(5), null, out _));
        Assert.False(WhisperProgressParser.TryGetFraction(TimeSpan.FromSeconds(5), 0, out _));
    }

    [Fact]
    public void TryGetFraction_ZeitHinterDauer_WirdAufEinsGeklammert()
    {
        Assert.True(WhisperProgressParser.TryGetFraction(TimeSpan.FromSeconds(30), 20, out double f));
        Assert.Equal(1.0, f);
        Assert.True(WhisperProgressParser.TryGetFraction(TimeSpan.FromSeconds(5), 20, out f));
        Assert.Equal(0.25, f, precision: 6);
    }

    [Fact]
    public void ToOverall_MitExtraktion_ExtraktionBelegtErstesZehntel()
    {
        Assert.Equal(0.10, WhisperProgressParser.ToOverall(1.0, isExtractionPhase: true, hasExtraction: true), precision: 6);
        Assert.Equal(0.05, WhisperProgressParser.ToOverall(0.5, isExtractionPhase: true, hasExtraction: true), precision: 6);
    }

    [Fact]
    public void ToOverall_MitExtraktion_TranskriptionLaeuftVonZehnBisHundert()
    {
        Assert.Equal(0.10, WhisperProgressParser.ToOverall(0.0, isExtractionPhase: false, hasExtraction: true), precision: 6);
        Assert.Equal(0.55, WhisperProgressParser.ToOverall(0.5, isExtractionPhase: false, hasExtraction: true), precision: 6);
        Assert.Equal(1.0, WhisperProgressParser.ToOverall(1.0, isExtractionPhase: false, hasExtraction: true), precision: 6);
    }

    [Fact]
    public void ToOverall_PhasenanteilAusserhalbBereich_WirdGeklammert()
    {
        Assert.Equal(1.0, WhisperProgressParser.ToOverall(1.7, isExtractionPhase: false, hasExtraction: true), precision: 6);
        Assert.Equal(0.10, WhisperProgressParser.ToOverall(-0.5, isExtractionPhase: false, hasExtraction: true), precision: 6);
    }

    [Fact]
    public void ToOverall_OhneExtraktion_TranskriptionBelegtAllesVonNullBisHundert()
    {
        Assert.Equal(0.42, WhisperProgressParser.ToOverall(0.42, isExtractionPhase: false, hasExtraction: false), precision: 6);
    }
}
