using MortysDLP.Services;

namespace MortysDLP.Tests;

/// <summary>
/// Prüft <see cref="VideoContainerRules.CanCopyVideo"/> — welcher Quell-Codec beim lokalen
/// Konvertieren per <c>-c:v copy</c> in welchen Zielcontainer darf. Der auslösende Fall:
/// AV1 in <c>.mov</c> lässt ffmpeg mit „av1 only supported in MP4 and AVIF" scheitern.
/// </summary>
public class VideoContainerRulesTests
{
    [Theory]
    // Der gemeldete Fehlerfall: AV1 nach .mov / .avi -> muss umkodiert werden.
    [InlineData("av1", "mov", false)]
    [InlineData("av1", "avi", false)]
    // AV1 in .mp4 ist regulär.
    [InlineData("av1", "mp4", true)]
    // AV1 in .mkv geht immer.
    [InlineData("av1", "mkv", true)]
    // VP9/VP8 nur in Matroska (bzw. hier nicht in mp4/mov/avi).
    [InlineData("vp9", "mov", false)]
    [InlineData("vp9", "mp4", false)]
    [InlineData("vp9", "mkv", true)]
    // H.264 passt überall.
    [InlineData("h264", "mov", true)]
    [InlineData("h264", "mp4", true)]
    [InlineData("h264", "avi", true)]
    [InlineData("h264", "mkv", true)]
    // H.265 in mp4/mov ok, in avi nicht.
    [InlineData("hevc", "mov", true)]
    [InlineData("hevc", "mp4", true)]
    [InlineData("hevc", "avi", false)]
    public void CanCopyVideo_CodecContainerPairs(string codec, string ext, bool expected)
    {
        Assert.Equal(expected, VideoContainerRules.CanCopyVideo(codec, ext));
    }

    [Fact]
    public void CanCopyVideo_UnknownCodec_IsCopyable()
    {
        // Kein belastbarer Grund, blind neu zu kodieren - scheitert es doch, meldet ffmpeg das.
        Assert.True(VideoContainerRules.CanCopyVideo(null, "mov"));
        Assert.True(VideoContainerRules.CanCopyVideo("", "mov"));
        Assert.True(VideoContainerRules.CanCopyVideo("   ", "mov"));
    }

    [Fact]
    public void CanCopyVideo_UnknownContainer_IsCopyable()
    {
        Assert.True(VideoContainerRules.CanCopyVideo("av1", "webm"));
    }

    [Theory]
    [InlineData("AV1", "MOV")]
    [InlineData("Av1", ".mov")]
    public void CanCopyVideo_IstUnabhaengigVonSchreibweiseUndPunkt(string codec, string ext)
    {
        Assert.False(VideoContainerRules.CanCopyVideo(codec, ext));
    }
}
