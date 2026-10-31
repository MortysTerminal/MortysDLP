using System.Collections.Generic;
using System.Globalization;

namespace MortysDLP.Services
{
    /// <summary>
    /// Übersetzt einen <see cref="YtDlpJob"/> in eine yt-dlp-Kommandozeile — eine reine
    /// Funktion, ohne Dateisystem- oder Oberflächenzugriff, deterministisch für dieselbe
    /// Eingabe. Ersetzt die bislang dreifach vorhandene, leicht abweichende Argumentbau-Logik
    /// in <c>DownloadPage</c>, <c>BatchDownloadPage</c> und <c>TwitchPage</c> — vorerst nur als
    /// Baustein, ohne dass eine der drei Seiten bereits umgestellt ist.
    /// </summary>
    internal static class YtDlpArgumentBuilder
    {
        public static IReadOnlyList<string> Build(YtDlpJob job)
        {
            var args = new List<string>();

            if (job.IsAudioOnly)
            {
                args.Add("-x");
                args.Add("--audio-format");
                args.Add(job.AudioFormat ?? "");

                if (!string.IsNullOrEmpty(job.AudioBitrate))
                {
                    args.Add("--audio-quality");
                    args.Add(job.AudioBitrate.ToUpperInvariant());
                }

                if (job.AudioForceReencode)
                {
                    args.Add("--postprocessor-args");
                    args.Add("ffmpeg:-ar 48000 -ac 2");
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(job.FormatSelector))
                {
                    args.Add("-f");
                    args.Add(job.FormatSelector);
                }

                if (!string.IsNullOrEmpty(job.MergeOutputFormat))
                {
                    args.Add("--merge-output-format");
                    args.Add(job.MergeOutputFormat);
                }

                if (job.MergeStreamCopyFastStart)
                {
                    args.Add("--postprocessor-args");
                    args.Add("Merger:-c copy -movflags +faststart");
                }
            }

            args.Add("-o");
            args.Add(job.OutputTemplate);

            // Ausdrücklich statt auf yt-dlps Voreinstellung zu vertrauen: Titel werden für
            // Windows bereinigt (reservierte Zeichen, Gerätenamen), gleich wo yt-dlp läuft.
            args.Add("--windows-filenames");

            // --print-to-file statt --print: --print impliziert --quiet und würde Fortschritt
            // und Destination-Zeilen abschalten. Der Dateipfad folgt der Syntax der
            // Ausgabevorlage und wird deshalb ebenfalls maskiert.
            if (!string.IsNullOrEmpty(job.FinalPathFile))
            {
                args.Add("--print-to-file");
                args.Add("after_move:filepath");
                args.Add(YtDlpOutputTemplate.EscapeLiteral(job.FinalPathFile));
            }

            bool usesInfoJson = !string.IsNullOrEmpty(job.InfoJsonPath);

            if (job.Timespan is { } timespan && !usesInfoJson)
            {
                args.Add("--download-sections");
                args.Add($"*{timespan.From}-{timespan.To}");
            }

            if (!string.IsNullOrEmpty(job.FirstSecondsDuration) && !usesInfoJson)
            {
                args.Add("--downloader");
                args.Add(job.FirstSecondsFfmpegPath ?? "");
                args.Add("--downloader-args");
                args.Add($"ffmpeg:-t {job.FirstSecondsDuration}");
            }

            if (job.BandwidthLimitMBps > 0)
            {
                args.Add("--limit-rate");
                args.Add($"{job.BandwidthLimitMBps.ToString(CultureInfo.InvariantCulture)}M");
            }

            // In allen drei heutigen Aufrufern unverändert vorhanden — ein Neustart nach
            // Bandbreitenwechsel (siehe YtDlpRunner) setzt sonst am Anfang neu auf, statt
            // fortzusetzen.
            args.Add("--continue");
            args.Add("--no-check-certificates");
            args.Add("--no-mtime");
            args.Add("--newline");

            // Maschinenlesbarer Fortschritt statt der für Menschen gedachten Konsolenzeile,
            // siehe YtDlpProgressParser.
            args.Add("--progress-template");
            args.Add(YtDlpProgressParser.Template);

            if (job.NoPlaylist)
                args.Add("--no-playlist");

            if (usesInfoJson)
            {
                // Einzelne Segmente werden zeitweise mit 401 abgelehnt. Mit 8 parallelen
                // Verbindungen brach die Audiospur daran ab, mit 3 kamen die Wiederholungen durch.
                // Ohne die Abbruch-Option überspringt yt-dlp ein nicht ladbares Segment
                // stillschweigend, und die Datei hätte eine Lücke.
                args.Add("-N");
                args.Add("3");
                args.Add("--fragment-retries");
                args.Add("20");
                args.Add("--abort-on-unavailable-fragments");
                args.Add("--load-info-json");
                args.Add(job.InfoJsonPath!); // durch usesInfoJson geprüft
            }
            else
            {
                args.Add(job.Url);
            }

            return args;
        }

        /// <summary>
        /// Baut den yt-dlp Format-Selector aus dem Tag-Wert (sprachunabhängig).
        /// Container-Kompatibilität:
        ///   mp4 – ISOBMFF: H.264/H.265/AV1 + AAC. Filter: [ext=mp4]+[ext=m4a] (AV1 in mp4 ist gültig)
        ///   mov – QuickTime: H.264/H.265 + AAC. Kein AV1! Filter: [vcodec^=avc1]+[ext=m4a]
        ///   avi – RIFF: nur H.264 + AAC/MP3 praxistauglich. Kein AV1/VP9! Filter: [vcodec^=avc1]+[ext=m4a]
        ///   mkv – Matroska: universell, akzeptiert alle Codecs → kein Filter nötig
        ///
        /// <para>Aus <c>DownloadPage</c> hierher verlagert — unverändertes Verhalten,
        /// abgesichert durch <c>VideoFormatSelectorTests.cs</c>.</para>
        /// </summary>
        internal static string BuildYtDlpVideoFormatSelector(string qualityTag, string container)
        {
            bool isMp4 = container.Equals("mp4", System.StringComparison.OrdinalIgnoreCase);
            bool needsH264 = container.Equals("mov", System.StringComparison.OrdinalIgnoreCase)
                          || container.Equals("avi", System.StringComparison.OrdinalIgnoreCase);

            // mov/avi: Codec-Filter (H.264 = avc1), da AV1 in diesen Containern nicht funktioniert
            // mp4:     Container-Filter reicht (mp4 unterstützt AV1 nativ)
            // mkv:     kein Filter nötig
            string vFilter = needsH264 ? "[vcodec^=avc1]" : (isMp4 ? "[ext=mp4]" : "");
            string aFilter = (needsH264 || isMp4) ? "[ext=m4a]" : "";

            return qualityTag switch
            {
                "best" or "" =>
                    $"bestvideo{vFilter}+bestaudio{aFilter}/bestvideo+bestaudio/best",
                _ when int.TryParse(qualityTag, out int h) && h > 0 =>
                    $"bestvideo{vFilter}[height<={h}]+bestaudio{aFilter}/bestvideo[height<={h}]+bestaudio/best[height<={h}]",
                _ =>
                    $"bestvideo{vFilter}+bestaudio{aFilter}/bestvideo+bestaudio/best"
            };
        }
    }
}
