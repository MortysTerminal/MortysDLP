using System;

namespace MortysDLP.Services
{
    /// <summary>
    /// Welcher Video-Codec darf beim <b>lokalen Konvertieren</b> per Stream-Copy
    /// (<c>-c:v copy</c>) in welchen Zielcontainer übernommen werden.
    ///
    /// <para>Das Gegenstück auf der Download-Seite ist
    /// <see cref="YtDlpArgumentBuilder.BuildYtDlpVideoFormatSelector"/> — dort wird über den
    /// yt-dlp-Formatfilter schon <i>vor</i> dem Download dafür gesorgt, dass für <c>mov</c>/
    /// <c>avi</c> nur H.264 geladen wird. Beim Konvertieren einer bereits vorhandenen Datei
    /// gibt es diesen Filter nicht: Eine AV1-Datei landet in <c>ConvertPage</c>, und ffmpeg
    /// bricht dann mit „av1 only supported in MP4 and AVIF" ab, bevor ein Frame geschrieben
    /// ist. Diese Regel fängt das ab — passt der Codec nicht, wird stattdessen zu H.264
    /// umkodiert.</para>
    /// </summary>
    internal static class VideoContainerRules
    {
        /// <summary>
        /// Kann <paramref name="sourceCodec"/> (ffprobe-<c>codec_name</c>, z. B. <c>av1</c>,
        /// <c>h264</c>, <c>vp9</c>) unverändert in einen Container mit der Endung
        /// <paramref name="targetExtension"/> (ohne Punkt, z. B. <c>mov</c>) übernommen werden?
        ///
        /// <para>Ein unbekannter Codec (<c>null</c>/leer) gilt als kopierbar — dann gibt es
        /// keinen belastbaren Grund, blind neu zu kodieren; scheitert es doch, meldet ffmpeg
        /// das wie bisher. Ein unbekannter Container gilt ebenfalls als unkritisch
        /// (Matroska-artig, nimmt alles).</para>
        /// </summary>
        public static bool CanCopyVideo(string? sourceCodec, string targetExtension)
        {
            if (string.IsNullOrWhiteSpace(sourceCodec))
                return true;

            string codec = sourceCodec.Trim().ToLowerInvariant();
            string ext = targetExtension.Trim().TrimStart('.').ToLowerInvariant();

            return ext switch
            {
                // Matroska nimmt praktisch jeden Video-Codec auf.
                "mkv" => true,

                // ISO-BMFF: H.264/H.265/AV1/MPEG-4 sind regulär. VP8/VP9/Theora nicht.
                "mp4" => codec is "h264" or "avc1" or "hevc" or "h265" or "hev1" or "hvc1"
                              or "av1" or "mpeg4" or "mp4v",

                // QuickTime: H.264/H.265, ProRes, MPEG-4 — kein AV1, kein VP8/VP9.
                "mov" => codec is "h264" or "avc1" or "hevc" or "h265" or "hev1" or "hvc1"
                              or "mpeg4" or "mp4v" or "prores" or "dnxhd",

                // AVI: praxistauglich nur H.264, MPEG-4 (Xvid/DivX), MJPEG.
                "avi" => codec is "h264" or "avc1" or "mpeg4" or "mp4v" or "msmpeg4v3" or "mjpeg",

                _ => true,
            };
        }
    }
}
