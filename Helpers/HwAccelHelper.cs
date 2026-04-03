using System.Diagnostics;

namespace MortysDLP.Helpers
{
    /// <summary>Erkennt den besten verfügbaren H.264-Encoder (GPU > CPU) und baut ffmpeg-Argumente.</summary>
    public static class HwAccelHelper
    {
        private static string? _cachedGpuEncoder;
        private static bool _gpuTested;

        /// <summary>H.264 GPU-Encoder (NVENC/QSV/AMF) unterstützen typischerweise max. 4096 px Breite/Höhe.</summary>
        private const int GpuH264MaxDimension = 4096;

        /// <summary>Testet GPU-Encoder in Prioritätsreihenfolge und fällt auf libx264 (CPU) zurück.
        /// Berücksichtigt die Video-Auflösung: GPU-Encoder haben ein H.264-Limit von 4096 px.
        /// Reihenfolge: NVIDIA NVENC → Intel QuickSync → AMD AMF → libx264 (CPU).</summary>
        public static async Task<string> DetectBestH264EncoderAsync(string ffmpegPath, int videoWidth = 0, int videoHeight = 0)
        {
            // GPU H.264-Encoder unterstützen typischerweise max. 4096 px Breite/Höhe
            bool exceedsGpuLimits = videoWidth > GpuH264MaxDimension || videoHeight > GpuH264MaxDimension;

            if (!exceedsGpuLimits)
            {
                // GPU-Encoder einmalig testen und cachen
                if (!_gpuTested)
                {
                    _gpuTested = true;
                    string[] gpuEncoders = ["h264_nvenc", "h264_qsv", "h264_amf"];

                    foreach (var encoder in gpuEncoders)
                    {
                        if (await TestEncoderAsync(ffmpegPath, encoder))
                        {
                            _cachedGpuEncoder = encoder;
                            break;
                        }
                    }
                }

                if (_cachedGpuEncoder != null)
                    return _cachedGpuEncoder;
            }

            return "libx264";
        }

        /// <summary>Baut ffmpeg-Argumente für die H.264-Konvertierung mit dem gewählten Encoder.
        /// Audio wird immer zu AAC (48 kHz, Stereo) konvertiert für Schnittsoftware-Kompatibilität.</summary>
        public static string BuildH264Args(string encoder, string inputPath, string outputPath)
        {
            string videoArgs = encoder switch
            {
                "h264_nvenc" => "-c:v h264_nvenc -preset p4 -cq 20 -pix_fmt yuv420p",
                "h264_qsv"  => "-c:v h264_qsv -preset medium -global_quality 20",
                "h264_amf"  => "-c:v h264_amf -quality balanced -rc cqp -qp_i 20 -qp_p 20",
                _           => "-c:v libx264 -preset fast -crf 20 -pix_fmt yuv420p"
            };

            return $"-i \"{inputPath}\" {videoArgs} -c:a aac -ar 48000 -ac 2 -movflags +faststart -y \"{outputPath}\"";
        }

        /// <summary>Gibt einen lesbaren Namen für den Encoder zurück.</summary>
        public static string GetEncoderDisplayName(string encoder) => encoder switch
        {
            "h264_nvenc" => "NVIDIA NVENC (GPU)",
            "h264_qsv"  => "Intel QuickSync (GPU)",
            "h264_amf"  => "AMD AMF (GPU)",
            "libx264"   => "x264 (CPU)",
            _           => encoder
        };

        private static async Task<bool> TestEncoderAsync(string ffmpegPath, string encoder)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = $"-f lavfi -i nullsrc=s=256x256:d=1 -c:v {encoder} -frames:v 1 -f null -",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                if (proc == null) return false;
                await proc.WaitForExitAsync();
                return proc.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
