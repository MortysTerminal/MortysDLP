using MortysDLP.Services;
using MortysDLP.Services.Tools;

namespace MortysDLP.Helpers
{
    /// <summary>Erkennt den besten verfügbaren H.264-Encoder (GPU > CPU) und baut ffmpeg-Argumente.</summary>
    public static class HwAccelHelper
    {
        /// <summary>H.264 GPU-Encoder (NVENC/QSV/AMF) unterstützen typischerweise max. 4096 px Breite/Höhe.</summary>
        private const int GpuH264MaxDimension = 4096;

        /// <summary>GPU-Encoder in Prüfreihenfolge. NVIDIA zuerst, dann Intel, dann AMD — die
        /// Reihenfolge ist Domänenwissen und bleibt, wie sie ist (siehe <see cref="BuildH264Args"/>).</summary>
        private static readonly string[] GpuEncoders = ["h264_nvenc", "h264_qsv", "h264_amf"];

        /// <summary>Ergab ein Durchgang keinen ansprechbaren GPU-Encoder, gilt das nur für diese
        /// Zeitspanne — danach wird bei Bedarf erneut geprüft. So zahlt ein Stapellauf nicht für
        /// jede Datei die Testprozesse, aber ein Fehlschlag zementiert die CPU auch nicht bis
        /// zum Programmende (ein zwischenzeitlich fehlendes ffmpeg darf das nicht auslösen).</summary>
        private static readonly TimeSpan NegativeRetryAfter = TimeSpan.FromMinutes(5);

        // Genau ein Durchgang zur Zeit. Ohne die Sperre könnten Hintergrund-Warmlauf und die
        // erste Konvertierung gleichzeitig je drei ffmpeg-Testprozesse starten.
        private static readonly SemaphoreSlim DetectLock = new(1, 1);
        private static readonly HwEncoderCache Cache = new();

        // _gpuEncoder != null  -> Treffer, endgültig (überlebt auch in der Ablage).
        // _gpuEncoder == null  -> noch kein Treffer; _negativeUntil sagt, bis wann ein
        //                         erneuter Test unterbleibt (default: sofort wieder erlaubt).
        private static string? _gpuEncoder;
        private static DateTime _negativeUntilUtc = DateTime.MinValue;

        /// <summary>Testet GPU-Encoder in Prioritätsreihenfolge und fällt auf libx264 (CPU) zurück.
        /// Berücksichtigt die Video-Auflösung: GPU-Encoder haben ein H.264-Limit von 4096 px.
        /// Reihenfolge: NVIDIA NVENC → Intel QuickSync → AMD AMF → libx264 (CPU).
        ///
        /// <para>Das Ergebnis wird gemerkt: über die Sitzung hinweg im Speicher, ein Treffer
        /// zusätzlich in <see cref="HwEncoderCache"/> für den nächsten Programmstart. Der Aufruf
        /// aus der Konvertierung findet das Ergebnis in der Regel schon vor, weil
        /// <c>App</c> die Erkennung nach dem Fenster im Hintergrund anstößt.</para></summary>
        public static async Task<string> DetectBestH264EncoderAsync(string ffmpegPath, int videoWidth = 0, int videoHeight = 0)
        {
            // GPU H.264-Encoder unterstützen typischerweise max. 4096 px Breite/Höhe.
            bool exceedsGpuLimits = videoWidth > GpuH264MaxDimension || videoHeight > GpuH264MaxDimension;
            if (exceedsGpuLimits)
                return "libx264";

            string? gpu = await ResolveGpuEncoderAsync(ffmpegPath).ConfigureAwait(false);
            return gpu ?? "libx264";
        }

        private static async Task<string?> ResolveGpuEncoderAsync(string ffmpegPath)
        {
            // Schnellpfad ohne Sperre: schon ein Treffer gemerkt.
            if (Volatile.Read(ref _gpuEncoder) is { } alreadyFound)
                return alreadyFound;

            await DetectLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_gpuEncoder != null)
                    return _gpuEncoder;

                if (DateTime.UtcNow < _negativeUntilUtc)
                    return null; // vorheriger Durchgang ohne Treffer, Sperrfrist läuft noch

                // Ohne die ffmpeg-Version lässt sich weder die Ablage abgleichen noch ein neuer
                // Eintrag sinnvoll schreiben. Kommt keine Version zustande - weil ffmpeg fehlt
                // oder nicht antwortet -, wird gar nichts gemerkt (auch kein negativer Merker):
                // Sobald ffmpeg wieder da ist, prüft der nächste Aufruf erneut.
                string? ffmpegVersion = await TryProbeFfmpegVersionAsync(ffmpegPath).ConfigureAwait(false);
                if (ffmpegVersion == null)
                {
                    Log.Debug("GPU-Encoder-Erkennung übersprungen: ffmpeg-Version nicht feststellbar " +
                        "(fehlt oder antwortet nicht) - wird beim nächsten Bedarf erneut versucht.");
                    return null;
                }

                var cached = await Cache.ReadAsync(CancellationToken.None).ConfigureAwait(false);
                if (HwEncoderCache.Matches(cached, ffmpegVersion))
                {
                    _gpuEncoder = cached!.Encoder;
                    Log.Debug($"GPU-Encoder aus der Ablage: {_gpuEncoder} (ffmpeg {ffmpegVersion}) - kein Testlauf nötig.");
                    return _gpuEncoder;
                }

                foreach (string encoder in GpuEncoders)
                {
                    var outcome = await TestEncoderAsync(ffmpegPath, encoder).ConfigureAwait(false);

                    if (outcome == EncoderTestResult.CannotTest)
                    {
                        // ffmpeg mittendrin verschwunden o. Ä. - kein "GPU kann nicht", sondern
                        // "konnte nicht geprüft werden". Nichts merken.
                        Log.Debug("GPU-Encoder-Erkennung abgebrochen: ffmpeg nicht mehr aufrufbar.");
                        return null;
                    }

                    if (outcome == EncoderTestResult.Works)
                    {
                        _gpuEncoder = encoder;
                        await Cache.WriteAsync(
                            new HwEncoderCacheEntry
                            {
                                Encoder = encoder,
                                FfmpegVersion = ffmpegVersion,
                                DetectedUtc = DateTimeOffset.UtcNow,
                            },
                            CancellationToken.None).ConfigureAwait(false);
                        Log.Info($"GPU-H.264-Encoder erkannt: {GetEncoderDisplayName(encoder)} (ffmpeg {ffmpegVersion}).");
                        return _gpuEncoder;
                    }
                }

                // Kein Encoder ansprang. Nur kurz merken, nicht auf Platte.
                _negativeUntilUtc = DateTime.UtcNow + NegativeRetryAfter;
                Log.Debug("Kein GPU-H.264-Encoder verfügbar - x264 (CPU). Erneute Prüfung frühestens in " +
                    $"{NegativeRetryAfter.TotalMinutes:F0} Minuten.");
                return null;
            }
            finally
            {
                DetectLock.Release();
            }
        }

        /// <summary>Baut ffmpeg-Argumente für die H.264-Konvertierung mit dem gewählten Encoder.
        /// Audio wird immer zu AAC (48 kHz, Stereo) konvertiert für Schnittsoftware-Kompatibilität.</summary>
        public static List<string> BuildH264Args(string encoder, string inputPath, string outputPath)
        {
            string[] videoArgs = encoder switch
            {
                "h264_nvenc" => ["-c:v", "h264_nvenc", "-preset", "p4", "-cq", "20", "-pix_fmt", "yuv420p"],
                "h264_qsv" => ["-c:v", "h264_qsv", "-preset", "medium", "-global_quality", "20"],
                "h264_amf" => ["-c:v", "h264_amf", "-quality", "balanced", "-rc", "cqp", "-qp_i", "20", "-qp_p", "20"],
                _ => ["-c:v", "libx264", "-preset", "fast", "-crf", "20", "-pix_fmt", "yuv420p"]
            };

            List<string> args = ["-i", inputPath, .. videoArgs, "-c:a", "aac", "-ar", "48000", "-ac", "2", "-movflags", "+faststart", "-y", outputPath];
            return args;
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

        private enum EncoderTestResult
        {
            /// <summary>Der Encoder hat einen Testframe erzeugt - er ist auf diesem Rechner nutzbar.</summary>
            Works,
            /// <summary>ffmpeg lief, der Encoder aber nicht (kein Treiber, keine passende GPU, Encoder im Build fehlt).</summary>
            DoesNotWork,
            /// <summary>Der Test konnte gar nicht stattfinden, weil ffmpeg fehlte - keine Aussage über die GPU.</summary>
            CannotTest,
        }

        private static async Task<EncoderTestResult> TestEncoderAsync(string ffmpegPath, string encoder)
        {
            try
            {
                var result = await ProcessRunner.RunAsync(
                    ffmpegPath,
                    ["-f", "lavfi", "-i", "nullsrc=s=256x256:d=1", "-c:v", encoder, "-frames:v", "1", "-f", "null", "-"],
                    timeout: TimeSpan.FromSeconds(20));
                return result.Success ? EncoderTestResult.Works : EncoderTestResult.DoesNotWork;
            }
            catch (ToolMissingException)
            {
                // Kein Fehlschlag des Encoders, sondern "nicht prüfbar" - diese Unterscheidung
                // wichtige Unterscheidung: ein fehlendes ffmpeg darf die GPU nicht aussperren.
                return EncoderTestResult.CannotTest;
            }
            catch (Exception ex)
            {
                // Konkret protokolliert statt leer verschluckt (02-BEST-PRACTICES.md §6). Breit
                // gefangen, weil ein kaputter Treiber den ffmpeg-Prozess auf viele Arten
                // sprengen kann (Absturz, Timeout-Kill, Zugriffsverletzung in einer GPU-DLL) -
                // keine davon soll die Prüfung der übrigen Encoder verhindern. Ergebnis: dieser
                // Encoder gilt als nicht nutzbar, die CPU bleibt als Rückfall.
                Log.Debug($"GPU-Encoder-Test '{encoder}' fehlgeschlagen: {ex.Message}");
                return EncoderTestResult.DoesNotWork;
            }
        }

        /// <summary>Fragt <c>ffmpeg -version</c> ab und zieht den Versions-Token heraus
        /// (z. B. <c>7.1-essentials_build-www.gyan.dev</c>). Als natives Programm antwortet
        /// ffmpeg in wenigen Dutzend Millisekunden. Liefert <c>null</c>, wenn ffmpeg fehlt oder
        /// nicht verwertbar antwortet - der Aufrufer merkt sich dann nichts.</summary>
        private static async Task<string?> TryProbeFfmpegVersionAsync(string ffmpegPath)
        {
            try
            {
                var result = await ProcessRunner.RunAsync(
                    ffmpegPath, ["-version"], timeout: TimeSpan.FromSeconds(20));
                if (!result.Success)
                    return null;

                return FfmpegTool.ExtractVersionToken(result.StdOut + "\n" + result.StdErr, "ffmpeg");
            }
            catch (Exception ex) when (ex is ToolMissingException or TimeoutException or System.ComponentModel.Win32Exception)
            {
                return null;
            }
        }
    }
}
