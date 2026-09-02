using MortysDLP.Services;
using System.Globalization;

namespace MortysDLP.Tests;

/// <summary>
/// Prüft <see cref="YtDlpArgumentBuilder.Build"/> — die Argumentliste ist rein aus
/// <see cref="YtDlpJob"/> abgeleitet, ohne Dateisystem- oder Netzzugriff. Die drei
/// „Deckt alle heute vorkommenden Kombinationen ab"-Tests am Ende bilden konkret nach, was
/// <c>DownloadPage</c>, <c>BatchDownloadPage</c> und <c>TwitchPage</c> heute jeweils selbst
/// zusammenbauen (Stand bei der Erstellung dieser Tests) — Abweichungen dort sind der
/// eigentliche Zweck der Aufgabe, nicht Zufall.
/// </summary>
public class YtDlpArgumentBuilderTests
{
    private static YtDlpJob MinimalJob(string url = "https://example.com/watch", string output = @"C:\out\%(title)s_%(id)s.%(ext)s") =>
        new() { Url = url, OutputTemplate = output };

    [Fact]
    public void Build_EnthaeltImmerDieFestenFlags()
    {
        var args = YtDlpArgumentBuilder.Build(MinimalJob());

        Assert.Contains("--continue", args);
        Assert.Contains("--no-check-certificates", args);
        Assert.Contains("--no-mtime", args);
        Assert.Contains("--newline", args);
        Assert.Contains("--no-playlist", args);
    }

    [Fact]
    public void Build_OutputTemplateUndUrlStehenImmerDrin()
    {
        var job = MinimalJob(url: "https://example.com/x", output: @"D:\ziel\%(title)s.%(ext)s");
        var args = YtDlpArgumentBuilder.Build(job);

        int oIndex = args.ToList().IndexOf("-o");
        Assert.True(oIndex >= 0);
        Assert.Equal(@"D:\ziel\%(title)s.%(ext)s", args[oIndex + 1]);
        Assert.Equal("https://example.com/x", args[^1]);
    }

    [Fact]
    public void Build_NoPlaylistFalse_LaesstFlagWeg()
    {
        var job = MinimalJob() with { NoPlaylist = false };
        var args = YtDlpArgumentBuilder.Build(job);

        Assert.DoesNotContain("--no-playlist", args);
    }

    // ── Video ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Build_Video_SetztFormatselektorUndMergeFormat()
    {
        var job = MinimalJob() with
        {
            FormatSelector = "bestvideo+bestaudio/best",
            MergeOutputFormat = "mkv",
        };
        var args = YtDlpArgumentBuilder.Build(job).ToList();

        Assert.Equal("bestvideo+bestaudio/best", args[args.IndexOf("-f") + 1]);
        Assert.Equal("mkv", args[args.IndexOf("--merge-output-format") + 1]);
        Assert.DoesNotContain("--postprocessor-args", args);
    }

    [Fact]
    public void Build_Video_MergeStreamCopyFastStart_SetztPostprocessorArgs()
    {
        var job = MinimalJob() with
        {
            FormatSelector = "bestvideo+bestaudio/best",
            MergeOutputFormat = "mp4",
            MergeStreamCopyFastStart = true,
        };
        var args = YtDlpArgumentBuilder.Build(job).ToList();

        int idx = args.IndexOf("--postprocessor-args");
        Assert.True(idx >= 0);
        Assert.Equal("Merger:-c copy -movflags +faststart", args[idx + 1]);
    }

    [Fact]
    public void Build_Video_OhneFormatSelector_LaesstMinusFWeg()
    {
        var job = MinimalJob() with { MergeOutputFormat = "mp4" };
        var args = YtDlpArgumentBuilder.Build(job);

        Assert.DoesNotContain("-f", args);
    }

    // ── Audio ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Build_AudioOnly_SetztXUndAudioFormat()
    {
        var job = MinimalJob() with { IsAudioOnly = true, AudioFormat = "mp3" };
        var args = YtDlpArgumentBuilder.Build(job).ToList();

        Assert.Contains("-x", args);
        Assert.Equal("mp3", args[args.IndexOf("--audio-format") + 1]);
        Assert.DoesNotContain("-f", args);
    }

    [Fact]
    public void Build_AudioOnly_FesteBitrate_SetztAudioQualityGrossgeschrieben()
    {
        var job = MinimalJob() with { IsAudioOnly = true, AudioFormat = "mp3", AudioBitrate = "192k" };
        var args = YtDlpArgumentBuilder.Build(job).ToList();

        Assert.Equal("192K", args[args.IndexOf("--audio-quality") + 1]);
    }

    [Fact]
    public void Build_AudioOnly_HoechsteBitrate_SetztKeinAudioQualityFlag()
    {
        var job = MinimalJob() with { IsAudioOnly = true, AudioFormat = "mp3", AudioBitrate = null };
        var args = YtDlpArgumentBuilder.Build(job);

        Assert.DoesNotContain("--audio-quality", args);
    }

    [Fact]
    public void Build_AudioOnly_ForceReencode_SetztPostprocessorArgs()
    {
        var job = MinimalJob() with { IsAudioOnly = true, AudioFormat = "mp3", AudioForceReencode = true };
        var args = YtDlpArgumentBuilder.Build(job).ToList();

        int idx = args.IndexOf("--postprocessor-args");
        Assert.True(idx >= 0);
        Assert.Equal("ffmpeg:-ar 48000 -ac 2", args[idx + 1]);
    }

    [Fact]
    public void Build_AudioOnly_OhneReencode_SetztKeinPostprocessorArgs()
    {
        var job = MinimalJob() with { IsAudioOnly = true, AudioFormat = "mp3", AudioForceReencode = false };
        var args = YtDlpArgumentBuilder.Build(job);

        Assert.DoesNotContain("--postprocessor-args", args);
    }

    // ── Zeit ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void Build_Timespan_SetztDownloadSections()
    {
        var job = MinimalJob() with { Timespan = ("00:01:00", "00:02:30") };
        var args = YtDlpArgumentBuilder.Build(job).ToList();

        int idx = args.IndexOf("--download-sections");
        Assert.True(idx >= 0);
        Assert.Equal("*00:01:00-00:02:30", args[idx + 1]);
    }

    [Fact]
    public void Build_OhneTimespan_SetztKeinDownloadSections()
    {
        var args = YtDlpArgumentBuilder.Build(MinimalJob());

        Assert.DoesNotContain("--download-sections", args);
    }

    [Fact]
    public void Build_FirstSeconds_SetztDownloaderUndArgs()
    {
        var job = MinimalJob() with { FirstSecondsDuration = "30", FirstSecondsFfmpegPath = @"C:\Tools\ffmpeg.exe" };
        var args = YtDlpArgumentBuilder.Build(job).ToList();

        Assert.Equal(@"C:\Tools\ffmpeg.exe", args[args.IndexOf("--downloader") + 1]);
        Assert.Equal("ffmpeg:-t 30", args[args.IndexOf("--downloader-args") + 1]);
    }

    // ── Bandbreite ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    public void Build_KeinOderNegativesLimit_SetztKeinLimitRate(double mbps)
    {
        var job = MinimalJob() with { BandwidthLimitMBps = mbps };
        var args = YtDlpArgumentBuilder.Build(job);

        Assert.DoesNotContain("--limit-rate", args);
    }

    [Fact]
    public void Build_LimitRate_FormatiertKulturunabhaengig()
    {
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            var job = MinimalJob() with { BandwidthLimitMBps = 0.1 };
            var args = YtDlpArgumentBuilder.Build(job).ToList();

            Assert.Equal("0.1M", args[args.IndexOf("--limit-rate") + 1]);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    // ── Reale Kombinationen der drei heutigen Aufrufer ──────────────────────────

    /// <summary>Bildet <c>DownloadPage</c>s Video-Merge-Fall nach (Container mkv, „best",
    /// kein Zeitausschnitt, kein Bandbreitenlimit) — mkv bekommt heute keine
    /// <c>--postprocessor-args</c>, weil der Container ohnehin jeden Codec akzeptiert.</summary>
    [Fact]
    public void Build_DownloadPageArtigerVideoAuftrag_EntsprichtDemHeutigenVerhalten()
    {
        string selector = YtDlpArgumentBuilder.BuildYtDlpVideoFormatSelector("best", "mkv");
        var job = new YtDlpJob
        {
            Url = "https://example.com/watch?v=abc",
            OutputTemplate = @"C:\Downloads\%(title)s_qbest_mkv_%(id)s.%(ext)s",
            FormatSelector = selector,
            MergeOutputFormat = "mkv",
            MergeStreamCopyFastStart = false,
        };

        var args = YtDlpArgumentBuilder.Build(job);

        Assert.Equal(
        [
            "-f", "bestvideo+bestaudio/bestvideo+bestaudio/best",
            "--merge-output-format", "mkv",
            "-o", @"C:\Downloads\%(title)s_qbest_mkv_%(id)s.%(ext)s",
            "--continue", "--no-check-certificates", "--no-mtime", "--newline",
            "--progress-template", YtDlpProgressParser.Template,
            "--no-playlist",
            "https://example.com/watch?v=abc",
        ], args);
    }

    /// <summary>Bildet <c>DownloadPage</c>s „Schnittmodus" nach (x264-Nachbearbeitung): Der
    /// Formatselektor wird heute mit Container <c>"mp4"</c> gebaut, unabhängig vom tatsächlich
    /// gewählten Container — die H.264-Umwandlung passiert danach per ffmpeg, nicht schon in
    /// yt-dlps Formatauswahl. <c>--postprocessor-args</c> wird im Schnittmodus immer gesetzt,
    /// auch wenn der gewählte Container das sonst nicht bekäme.</summary>
    [Fact]
    public void Build_DownloadPageArtigerSchnittmodusAuftrag_EntsprichtDemHeutigenVerhalten()
    {
        string selector = YtDlpArgumentBuilder.BuildYtDlpVideoFormatSelector("1080", "mp4");
        var job = new YtDlpJob
        {
            Url = "https://example.com/watch?v=abc",
            OutputTemplate = @"C:\Downloads\%(title)s_q1080_avi_x264_%(id)s.%(ext)s",
            FormatSelector = selector,
            MergeOutputFormat = "mp4",
            MergeStreamCopyFastStart = true,
        };

        var args = YtDlpArgumentBuilder.Build(job);

        Assert.Equal(
        [
            "-f", "bestvideo[ext=mp4][height<=1080]+bestaudio[ext=m4a]/bestvideo[height<=1080]+bestaudio/best[height<=1080]",
            "--merge-output-format", "mp4",
            "--postprocessor-args", "Merger:-c copy -movflags +faststart",
            "-o", @"C:\Downloads\%(title)s_q1080_avi_x264_%(id)s.%(ext)s",
            "--continue", "--no-check-certificates", "--no-mtime", "--newline",
            "--progress-template", YtDlpProgressParser.Template,
            "--no-playlist",
            "https://example.com/watch?v=abc",
        ], args);
    }

    /// <summary>Bildet <c>BatchDownloadPage</c>s Audio-Fall nach — anders als bei
    /// <c>DownloadPage</c> gibt es dort heute **keine** Reencode-Erzwingung (kein
    /// <c>--postprocessor-args</c> bei abweichender Samplerate/Kanalzahl); das ist ein echter
    /// Verhaltensunterschied, kein Kopierfehler dieser Tests.</summary>
    [Fact]
    public void Build_BatchDownloadPageArtigerAudioAuftrag_EntsprichtDemHeutigenVerhalten()
    {
        var job = new YtDlpJob
        {
            Url = "https://example.com/watch?v=abc",
            OutputTemplate = @"C:\Downloads\%(title)s_%(id)s.%(ext)s",
            IsAudioOnly = true,
            AudioFormat = "mp3",
            AudioBitrate = "192k",
            AudioForceReencode = false,
        };

        var args = YtDlpArgumentBuilder.Build(job);

        Assert.Equal(
        [
            "-x", "--audio-format", "mp3",
            "--audio-quality", "192K",
            "-o", @"C:\Downloads\%(title)s_%(id)s.%(ext)s",
            "--continue", "--no-check-certificates", "--no-mtime", "--newline",
            "--progress-template", YtDlpProgressParser.Template,
            "--no-playlist",
            "https://example.com/watch?v=abc",
        ], args);
    }

    /// <summary>Bildet <c>TwitchPage</c>s Video-Fall nach: eigener, fest hinterlegter
    /// Formatselektor statt <see cref="YtDlpArgumentBuilder.BuildYtDlpVideoFormatSelector"/> —
    /// deshalb hier als reine Zeichenkette übergeben, nicht über den Helfer gebaut. Kein
    /// Zeitausschnitt, kein benutzerdefinierter Dateiname.</summary>
    [Fact]
    public void Build_TwitchPageArtigerVideoAuftrag_EntsprichtDemHeutigenVerhalten()
    {
        var job = new YtDlpJob
        {
            Url = "https://www.twitch.tv/videos/12345",
            OutputTemplate = @"C:\Downloads\Titel_%(id)s.%(ext)s",
            FormatSelector = "best[ext=mp4]/bestvideo[ext=mp4]+bestaudio[ext=m4a]/bestvideo+bestaudio/best",
            MergeOutputFormat = "mp4",
        };

        var args = YtDlpArgumentBuilder.Build(job);

        Assert.Equal(
        [
            "-f", "best[ext=mp4]/bestvideo[ext=mp4]+bestaudio[ext=m4a]/bestvideo+bestaudio/best",
            "--merge-output-format", "mp4",
            "-o", @"C:\Downloads\Titel_%(id)s.%(ext)s",
            "--continue", "--no-check-certificates", "--no-mtime", "--newline",
            "--progress-template", YtDlpProgressParser.Template,
            "--no-playlist",
            "https://www.twitch.tv/videos/12345",
        ], args);
    }
}
