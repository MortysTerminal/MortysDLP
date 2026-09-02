using MortysDLP.Services;

namespace MortysDLP.Tests;

/// <summary>
/// Prüft die reine Textauswertung von <see cref="MediaProbe"/> gegen echte, mit der
/// installierten ffprobe-Version aufgezeichnete Ausgaben (ein synthetisches Testvideo,
/// erzeugt mit <c>ffmpeg -f lavfi</c>, dagegen mit <c>ffprobe</c> von Hand aufgerufen) — nicht
/// gegen geratene Beispielwerte. Kein echter ffprobe-Aufruf im Testlauf selbst nötig, da die
/// Auswertung von der Prozessausführung getrennt ist (wie bei
/// <see cref="YtDlpProgressParser"/>).
/// </summary>
public class MediaProbeTests
{
    // ── ParseDuration ────────────────────────────────────────────────────────────

    [Fact]
    public void ParseDuration_EchteAusgabe_LiestSekunden()
    {
        // ffprobe -show_entries format=duration -of default=noprint_wrappers=1:nokey=1
        double? duration = MediaProbe.ParseDuration("5.000000\n");

        Assert.Equal(5.0, duration);
    }

    [Fact]
    public void ParseDuration_UngueltigeAusgabe_LiefertNull()
    {
        Assert.Null(MediaProbe.ParseDuration("N/A\n"));
    }

    [Fact]
    public void ParseDuration_LeereAusgabe_LiefertNull()
    {
        Assert.Null(MediaProbe.ParseDuration(""));
    }

    // ── ParseVideoStreamInfo ─────────────────────────────────────────────────────

    [Fact]
    public void ParseVideoStreamInfo_EchteSchluesselWertAusgabe_LiestCodecUndAufloesung()
    {
        // ffprobe -select_streams v:0 -show_entries stream=codec_name,width,height
        //         -of default=noprint_wrappers=1
        string output = "codec_name=h264\nwidth=640\nheight=360\n";

        var (codec, width, height) = MediaProbe.ParseVideoStreamInfo(output);

        Assert.Equal("h264", codec);
        Assert.Equal(640, width);
        Assert.Equal(360, height);
    }

    [Fact]
    public void ParseVideoStreamInfo_UndBatchDownloadPagesFruehereCsvAuswertung_LiefernDasselbeErgebnis()
    {
        // Dieselbe Datei, zwei echte ffprobe-Aufrufe mit unterschiedlichem -of - belegt, dass
        // die Vereinheitlichung auf Schlüssel=Wert (siehe Klassenkommentar) für den früheren,
        // inzwischen entfernten CSV-Anwendungsfall der Batch-Seite kein anderes Ergebnis
        // liefert - nicht angenommen, sondern an echten Ausgaben derselben Datei verglichen.
        const string keyValueOutput = "codec_name=h264\nwidth=640\nheight=360\n";
        const string csvOutput = "h264,640,360";

        var fromKeyValue = MediaProbe.ParseVideoStreamInfo(keyValueOutput);

        // Nachbildung der alten, jetzt entfernten BatchDownloadPage-Auswertung
        // (-of csv=p=0: codec,width,height in fester Spaltenreihenfolge).
        var parts = csvOutput.Trim().Split(',');
        string? csvCodec = parts.Length > 0 ? parts[0] : null;
        // Rückgabewert bewusst verworfen (_ =): Die alte Auswertung wertete ihn ebenfalls nicht
        // aus, sondern verließ sich auf den 0-Standardwert bei einem nicht lesbaren Feld. Genau
        // dieses Verhalten wird hier nachgebildet, nicht ein verbessertes.
        _ = int.TryParse(parts.Length > 1 ? parts[1] : "", out int csvWidth);
        _ = int.TryParse(parts.Length > 2 ? parts[2] : "", out int csvHeight);

        Assert.Equal(csvCodec, fromKeyValue.Codec);
        Assert.Equal(csvWidth, fromKeyValue.Width);
        Assert.Equal(csvHeight, fromKeyValue.Height);
    }

    [Fact]
    public void ParseVideoStreamInfo_AndereVideospur_LiestEbenfallsKorrekt()
    {
        // Zweite echte Aufzeichnung (VP9/webm) - belegt, dass die Auswertung nicht nur für
        // eine einzelne Codec-Zeile zufällig passt.
        string output = "codec_name=vp9\nwidth=320\nheight=240\n";

        var (codec, width, height) = MediaProbe.ParseVideoStreamInfo(output);

        Assert.Equal("vp9", codec);
        Assert.Equal(320, width);
        Assert.Equal(240, height);
    }

    [Fact]
    public void ParseVideoStreamInfo_UnbekannterWertAlsNA_WirdIgnoriertOhneAbsturz()
    {
        // Echte ffprobe-Ausgabe, wenn ein angefragtes Feld nicht ermittelbar ist - "N/A" als
        // Text, keine fehlende Zeile.
        string output = "codec_name=vp9\nwidth=320\nheight=240\nbit_rate=N/A\n";

        var (codec, width, height) = MediaProbe.ParseVideoStreamInfo(output);

        Assert.Equal("vp9", codec);
        Assert.Equal(320, width);
        Assert.Equal(240, height);
    }

    [Fact]
    public void ParseVideoStreamInfo_LeereAusgabe_LiefertNullUndNullen()
    {
        var (codec, width, height) = MediaProbe.ParseVideoStreamInfo("");

        Assert.Null(codec);
        Assert.Equal(0, width);
        Assert.Equal(0, height);
    }

    // ── ParseAudioStreamInfo ─────────────────────────────────────────────────────

    [Fact]
    public void ParseAudioStreamInfo_EchteAusgabe_LiestSamplerateKanaeleUndBitrate()
    {
        // ffprobe -select_streams a:0 -show_entries stream=sample_rate,channels,bit_rate
        //         -of default=noprint_wrappers=1
        string output = "sample_rate=44100\nchannels=1\nbit_rate=69329\n";

        var (sampleRate, channels, bitRateKbps) = MediaProbe.ParseAudioStreamInfo(output);

        Assert.Equal(44100, sampleRate);
        Assert.Equal(1, channels);
        Assert.Equal(69, bitRateKbps); // 69329 Bit/s -> 69 kbit/s (ganzzahlige Division wie bisher)
    }

    [Fact]
    public void ParseAudioStreamInfo_KeinAudiostream_LiefertDreiNullwerte()
    {
        // Echtes Verhalten von ffprobe bei -select_streams a:0 ohne Audiospur: leere Ausgabe,
        // kein Fehler.
        var (sampleRate, channels, bitRateKbps) = MediaProbe.ParseAudioStreamInfo("");

        Assert.Null(sampleRate);
        Assert.Null(channels);
        Assert.Null(bitRateKbps);
    }

    [Fact]
    public void ParseAudioStreamInfo_BitRateNichtErmittelbar_BleibtNullOhneDieAnderenWerteZuVerlieren()
    {
        string output = "sample_rate=48000\nchannels=2\nbit_rate=N/A\n";

        var (sampleRate, channels, bitRateKbps) = MediaProbe.ParseAudioStreamInfo(output);

        Assert.Equal(48000, sampleRate);
        Assert.Equal(2, channels);
        Assert.Null(bitRateKbps);
    }
}
