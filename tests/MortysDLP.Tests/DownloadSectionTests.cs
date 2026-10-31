using MortysDLP.Services;
using System.IO;
using System.Text;
using System.Text.Json;

namespace MortysDLP.Tests;

/// <summary>
/// Prüft die reine Logik für Zeitausschnitte: Zeiten lesen wie yt-dlp, Segmente berechnen,
/// Metadaten kürzen und unvollständige Ergebnisse erkennen. Die Metadaten bilden den Aufbau echter
/// yt-dlp-Ausgaben für einen noch verarbeiteten Stream nach (Segmente von 2 s).
/// </summary>
public class DownloadSectionTests
{
    private static YtDlpJob Job() => new() { Url = "https://example.com/watch", OutputTemplate = "out" };

    [Theory]
    [InlineData("90", 90)]
    [InlineData("01:32", 92)]
    [InlineData("1:32", 92)]
    [InlineData("01:32:40", 5560)]
    [InlineData("04:00:26", 14426)]
    [InlineData("00:00:01.5", 1.5)]
    public void ParseTime_LiestWieYtDlp(string text, double expected)
    {
        Assert.Equal(expected, DownloadSection.ParseTime(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("inf")]
    [InlineData("1:2:3:4")]
    [InlineData("-5")]
    [InlineData("1,5")]
    public void ParseTime_NichtAuswertbar_ErgibtNull(string? text)
    {
        Assert.Null(DownloadSection.ParseTime(text));
    }

    [Fact]
    public void FromJob_OhneAusschnitt_ErgibtNull()
    {
        Assert.Null(DownloadSection.FromJob(Job()));
    }

    [Fact]
    public void FromJob_Zeitspanne_ErgibtAusschnitt()
    {
        var job = Job() with { Timespan = ("01:32:40", "04:00:26") };

        Assert.Equal(new RequestedSection(5560, 14426), DownloadSection.FromJob(job));
    }

    [Fact]
    public void FromJob_EndeVorStart_ErgibtNull()
    {
        Assert.Null(DownloadSection.FromJob(Job() with { Timespan = ("00:10", "00:05") }));
    }

    [Fact]
    public void FromJob_ErsteSekunden_BeginntBeiNull()
    {
        var job = Job() with { FirstSecondsDuration = "30" };

        Assert.Equal(new RequestedSection(0, 30), DownloadSection.FromJob(job));
    }

    [Fact]
    public void ExpectedSeconds_EndeHinterVideoende_WirdBegrenzt()
    {
        Assert.Equal(60, DownloadSection.ExpectedSeconds(new RequestedSection(540, 1200), 600));
    }

    /// <summary>Ohne Gesamtdauer gibt es kein Urteil: Das Ende hinter dem Videoende ist erlaubt.</summary>
    [Fact]
    public void ExpectedSeconds_DauerUnbekannt_ErgibtNull()
    {
        Assert.Null(DownloadSection.ExpectedSeconds(new RequestedSection(0, 60), null));
    }

    [Fact]
    public void ExpectedSeconds_StartHinterVideoende_ErgibtNull()
    {
        Assert.Null(DownloadSection.ExpectedSeconds(new RequestedSection(700, 800), 600));
    }

    /// <summary>Der Fall, der den Fehler sichtbar gemacht hat: 2 s statt 8866 s.</summary>
    [Fact]
    public void IsTooShort_NurLetztesSegmentGeladen_IstZuKurz()
    {
        Assert.True(DownloadSection.IsTooShort(8866, 2));
    }

    [Fact]
    public void IsTooShort_ZehnSekundenAngefordertZweiGeliefert_IstZuKurz()
    {
        Assert.True(DownloadSection.IsTooShort(10, 2));
    }

    /// <summary>Schnitte an Keyframes weichen normal ab; das darf nie als Fehler enden.</summary>
    [Theory]
    [InlineData(8866, 8866)]
    [InlineData(60, 57)]
    [InlineData(60, 63)]
    [InlineData(60, 31)]
    [InlineData(4, 1)]
    public void IsTooShort_NormaleAbweichung_IstInOrdnung(double expected, double actual)
    {
        Assert.False(DownloadSection.IsTooShort(expected, actual));
    }

    [Fact]
    public void ParseSourceLine_PostLive_LiefertStatusFormateUndDauer()
    {
        var source = DownloadSection.ParseSourceLine("post_live|299+140|35312\r\n");

        Assert.NotNull(source);
        Assert.True(source.IsPostLive);
        Assert.Equal(["299", "140"], source.FormatIds);
        Assert.Equal(35312, source.DurationSeconds);
    }

    [Fact]
    public void ParseSourceLine_NormalesVideo_IstNichtPostLive()
    {
        var source = DownloadSection.ParseSourceLine("not_live|137+251|600.5");

        Assert.NotNull(source);
        Assert.False(source.IsPostLive);
        Assert.Equal(600.5, source.DurationSeconds);
    }

    [Fact]
    public void ParseSourceLine_DauerUnbekannt_BleibtNull()
    {
        var source = DownloadSection.ParseSourceLine("NA|22|NA");

        Assert.NotNull(source);
        Assert.Null(source.DurationSeconds);
    }

    [Theory]
    [InlineData("")]
    [InlineData("kaputt")]
    [InlineData("post_live|299")]
    [InlineData("not_live|22|60\nnot_live|18|90")]
    public void ParseSourceLine_NichtEindeutig_ErgibtNull(string stdOut)
    {
        Assert.Null(DownloadSection.ParseSourceLine(stdOut));
    }

    /// <summary>Bei genau passenden Zeiten: 5560 s ist Segment 2780, 14426 s endet mit Segment 7212.
    /// So auch an einem echten Stream nachgemessen.</summary>
    [Fact]
    public void ComputeFragmentRange_PassendeZeiten_TrifftExakt()
    {
        Assert.Equal((2780, 7212), DownloadSection.ComputeFragmentRange(new RequestedSection(5560, 14426), 2, 17658));
    }

    [Fact]
    public void ComputeFragmentRange_UngeradeZeiten_RundetNachAussen()
    {
        Assert.Equal((2, 5), DownloadSection.ComputeFragmentRange(new RequestedSection(5, 11), 2, 100));
    }

    [Fact]
    public void ComputeFragmentRange_EndeHinterLetztemSegment_WirdBegrenzt()
    {
        Assert.Equal((5, 9), DownloadSection.ComputeFragmentRange(new RequestedSection(10, 500), 2, 10));
    }

    [Theory]
    [InlineData(100, 200, 2, 10)]
    [InlineData(0, 10, 0, 10)]
    [InlineData(0, 10, 2, 0)]
    public void ComputeFragmentRange_NichtsUebrig_ErgibtNull(double start, double end, double segment, int count)
    {
        Assert.Null(DownloadSection.ComputeFragmentRange(new RequestedSection(start, end), segment, count));
    }

    private static string Fragments(string itag, int count) =>
        string.Join(',', Enumerable.Range(0, count).Select(i =>
            $$"""{"url":"https://host.example/{{itag}}?sq={{i}}","fragment_count":{{count}}}"""));

    private static string SampleInfo(int count = 20, double duration = 40, string target = "\"target_duration\":2,") => $$"""
        {
          "id": "abc", "title": "Titel mit Ü", "duration": {{duration.ToString(System.Globalization.CultureInfo.InvariantCulture)}},
          "live_status": "post_live", "filesize_approx": 999, "automatic_captions": {"de": []},
          "formats": [
            {"format_id":"139","protocol":"http_dash_segments",{{target}}"filesize":1,"fragments":[{{Fragments("139", count)}}]},
            {"format_id":"140","protocol":"http_dash_segments",{{target}}"filesize":2,"filesize_approx":3,"fragments":[{{Fragments("140", count)}}]},
            {"format_id":"299","protocol":"http_dash_segments",{{target}}"filesize":4,"fragments":[{{Fragments("299", count)}}]}
          ]
        }
        """;

    private static (TrimmedSectionInfo? Result, string Output) Trim(string info, double start, double end, params string[] ids)
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(info));
        using var output = new MemoryStream();
        var result = DownloadSection.TrimInfoJson(input, output, ids, new RequestedSection(start, end));
        return (result, Encoding.UTF8.GetString(output.ToArray()));
    }

    [Fact]
    public void TrimInfoJson_BehaeltNurGewaehlteFormateInGewaehlterReihenfolge()
    {
        var (_, output) = Trim(SampleInfo(), 4, 10, "299", "140");
        using var doc = JsonDocument.Parse(output);

        var ids = doc.RootElement.GetProperty("formats").EnumerateArray()
            .Select(f => f.GetProperty("format_id").GetString());
        Assert.Equal(["299", "140"], ids);
    }

    [Fact]
    public void TrimInfoJson_KuerztSegmenteAufDenAusschnitt()
    {
        var (_, output) = Trim(SampleInfo(), 4, 10, "299");
        using var doc = JsonDocument.Parse(output);

        var urls = doc.RootElement.GetProperty("formats")[0].GetProperty("fragments").EnumerateArray()
            .Select(f => f.GetProperty("url").GetString());
        Assert.Equal(["https://host.example/299?sq=2", "https://host.example/299?sq=3", "https://host.example/299?sq=4"], urls);
    }

    [Fact]
    public void TrimInfoJson_SetztDauerAufDenAusschnitt()
    {
        var (_, output) = Trim(SampleInfo(), 4, 10, "299");
        using var doc = JsonDocument.Parse(output);

        Assert.Equal(6, doc.RootElement.GetProperty("duration").GetDouble());
    }

    /// <summary>Diese Angaben beschreiben das ungekürzte Video; yt-dlp würde damit falsche
    /// Größen und einen falschen Fortschritt rechnen.</summary>
    [Fact]
    public void TrimInfoJson_EntferntAngabenZurUngekuerztenFassung()
    {
        var (_, output) = Trim(SampleInfo(), 4, 10, "140");

        Assert.DoesNotContain("filesize", output, StringComparison.Ordinal);
        Assert.DoesNotContain("fragment_count", output, StringComparison.Ordinal);
        Assert.DoesNotContain("automatic_captions", output, StringComparison.Ordinal);
    }

    /// <summary>Bei einem einzelnen Format (Nur-Audio) steht die volle Liste auch auf oberster Ebene.</summary>
    [Fact]
    public void TrimInfoJson_EntferntSegmentlisteAufObersterEbene()
    {
        string info = SampleInfo().Replace("\"id\": \"abc\",", $"\"id\": \"abc\", \"fragments\": [{Fragments("140", 20)}], \"filesize\": 5,", StringComparison.Ordinal);

        var (_, output) = Trim(info, 4, 10, "140");
        using var doc = JsonDocument.Parse(output);

        Assert.False(doc.RootElement.TryGetProperty("fragments", out _));
        Assert.False(doc.RootElement.TryGetProperty("filesize", out _));
    }

    [Fact]
    public void TrimInfoJson_UebernimmtUebrigeFelderUnveraendert()
    {
        var (_, output) = Trim(SampleInfo(), 4, 10, "299");
        using var doc = JsonDocument.Parse(output);

        Assert.Equal("Titel mit Ü", doc.RootElement.GetProperty("title").GetString());
        Assert.Equal("post_live", doc.RootElement.GetProperty("live_status").GetString());
        Assert.Equal("http_dash_segments", doc.RootElement.GetProperty("formats")[0].GetProperty("protocol").GetString());
    }

    [Fact]
    public void TrimInfoJson_MeldetErstesSegmentFuerDieGegenprobe()
    {
        var (result, _) = Trim(SampleInfo(), 4, 10, "299", "140");

        Assert.NotNull(result);
        Assert.Equal("https://host.example/299?sq=2", result.FirstFragmentUrl);
        Assert.Equal(4, result.FirstFragmentSeconds);
        Assert.Equal(2, result.SegmentSeconds);
    }

    /// <summary>Die Toleranz muss den echten Fall tragen: 17658 × 2 s bei gemeldeten 35312 s.</summary>
    [Fact]
    public void TrimInfoJson_KleineAbweichungZurGesamtdauer_WirdAkzeptiert()
    {
        var (result, _) = Trim(SampleInfo(count: 17658, duration: 35312), 5560, 5580, "299");

        Assert.NotNull(result);
    }

    [Theory]
    [InlineData("""{"formats":[]}""")]
    [InlineData("""{"id":"ohne Formate"}""")]
    [InlineData("""{"formats":[{"format_id":"299","url":"https://host.example/ohne-segmente"}]}""")]
    [InlineData("""{"formats":[{"format_id":"299","target_duration":2,"fragments":[]}]}""")]
    [InlineData("""{"formats":[{"format_id":"299","fragments":[{"url":"https://host.example/1"}]}]}""")]
    public void TrimInfoJson_QuellePasstNichtInsMuster_SchreibtNichts(string info)
    {
        var (result, output) = Trim(info, 0, 2, "299");

        Assert.Null(result);
        Assert.Empty(output);
    }

    [Fact]
    public void TrimInfoJson_FormatFehlt_SchreibtNichts()
    {
        var (result, output) = Trim(SampleInfo(), 4, 10, "299", "251");

        Assert.Null(result);
        Assert.Empty(output);
    }

    /// <summary>Viel weniger Segmente als die Dauer verlangt: Der Stream hatte Lücken oder eine
    /// andere Einteilung. Aus einer Zeit ließe sich dann kein Segment mehr berechnen.</summary>
    [Fact]
    public void TrimInfoJson_SegmenteDeckenDauerNichtAb_SchreibtNichts()
    {
        var (result, output) = Trim(SampleInfo(count: 20, duration: 400), 4, 10, "299");

        Assert.Null(result);
        Assert.Empty(output);
    }

    [Fact]
    public void TrimInfoJson_StartHinterLetztemSegment_SchreibtNichts()
    {
        var (result, output) = Trim(SampleInfo(), 100, 110, "299");

        Assert.Null(result);
        Assert.Empty(output);
    }

    [Theory]
    [InlineData("5560.000000,\r\n5560.016667,\r\n", 5560)]
    [InlineData("5559.994921\n", 5559.994921)]
    public void ParseFirstPacketSeconds_LiestErsteZeile(string stdOut, double expected)
    {
        Assert.Equal(expected, MediaProbe.ParseFirstPacketSeconds(stdOut));
    }

    [Theory]
    [InlineData("")]
    [InlineData("N/A\n")]
    public void ParseFirstPacketSeconds_KeinWert_ErgibtNull(string stdOut)
    {
        Assert.Null(MediaProbe.ParseFirstPacketSeconds(stdOut));
    }

    [Fact]
    public void Build_MitInfoJson_LaedtAusDerDateiStattVonDerAdresse()
    {
        var job = Job() with { Timespan = ("01:32:40", "04:00:26"), InfoJsonPath = @"C:\Temp\section.info.json" };

        var args = YtDlpArgumentBuilder.Build(job).ToList();

        Assert.Equal(@"C:\Temp\section.info.json", args[args.IndexOf("--load-info-json") + 1]);
        Assert.DoesNotContain("https://example.com/watch", args);
        Assert.DoesNotContain("--download-sections", args);
    }

    [Fact]
    public void Build_MitInfoJson_VerzichtetAufFfmpegAlsDownloader()
    {
        var job = Job() with { FirstSecondsDuration = "30", FirstSecondsFfmpegPath = "ffmpeg.exe", InfoJsonPath = "x.json" };

        var args = YtDlpArgumentBuilder.Build(job);

        Assert.DoesNotContain("--downloader", args);
        Assert.DoesNotContain("--downloader-args", args);
    }

    /// <summary>Ohne die Option würde yt-dlp ein nicht ladbares Segment überspringen.</summary>
    [Fact]
    public void Build_MitInfoJson_BrichtBeiFehlendemSegmentAb()
    {
        var args = YtDlpArgumentBuilder.Build(Job() with { InfoJsonPath = "x.json" }).ToList();

        Assert.Contains("--abort-on-unavailable-fragments", args);
        Assert.Equal("3", args[args.IndexOf("-N") + 1]);
    }

    /// <summary>Der bisherige Weg bleibt Zeichen für Zeichen derselbe.</summary>
    [Fact]
    public void Build_OhneInfoJson_BleibtUnveraendert()
    {
        var args = YtDlpArgumentBuilder.Build(Job() with { Timespan = ("00:10", "00:20") }).ToList();

        Assert.Equal("*00:10-00:20", args[args.IndexOf("--download-sections") + 1]);
        Assert.Equal("https://example.com/watch", args[^1]);
        Assert.DoesNotContain("--load-info-json", args);
        Assert.DoesNotContain("-N", args);
        Assert.DoesNotContain("--abort-on-unavailable-fragments", args);
    }
}
