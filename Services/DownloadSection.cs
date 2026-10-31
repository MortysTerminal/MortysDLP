using MortysDLP.Helpers;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace MortysDLP.Services
{
    /// <summary>Der angeforderte Ausschnitt eines Videos, in Sekunden ab Videobeginn.</summary>
    internal readonly record struct RequestedSection(double StartSeconds, double EndSeconds);

    /// <summary>Was yt-dlp vor dem eigentlichen Download über die Quelle meldet.</summary>
    /// <param name="LiveStatus">yt-dlps <c>live_status</c>, z. B. <c>not_live</c> oder <c>post_live</c>.</param>
    /// <param name="FormatIds">Die vom Formatselektor gewählten Formate, z. B. <c>299</c> und <c>140</c>.</param>
    /// <param name="DurationSeconds">Gesamtdauer des Videos, <c>null</c>, wenn unbekannt.</param>
    internal sealed record SectionSource(string LiveStatus, IReadOnlyList<string> FormatIds, double? DurationSeconds)
    {
        public bool IsPostLive => LiveStatus == "post_live";
    }

    /// <summary>Eine auf den Ausschnitt gekürzte Metadatendatei für <c>--load-info-json</c>.
    /// Die Datei enthält signierte Adressen und wird deshalb mit <see cref="Dispose"/> samt
    /// ihrem Ordner entfernt — auch nach einem Abbruch oder Fehlschlag.</summary>
    internal sealed class PreparedSectionInfo(string directory, string infoJsonPath) : IDisposable
    {
        public string InfoJsonPath { get; } = infoJsonPath;

        public void Dispose() => DownloadSection.DeleteWorkDirectory(directory);
    }

    /// <summary>Ergebnis des Kürzens: Adresse und erwartete Startzeit des ersten Segments.</summary>
    internal sealed record TrimmedSectionInfo(string FirstFragmentUrl, double FirstFragmentSeconds, double SegmentSeconds);

    /// <summary>
    /// Zeitausschnitte bei Streams, deren Aufzeichnung noch verarbeitet wird, und die Prüfung,
    /// ob ein Ausschnitt tatsächlich in der gewünschten Länge angekommen ist.
    ///
    /// <para><b>Warum ein eigener Weg:</b> Solche Streams meldet yt-dlp als <c>post_live</c>; es
    /// gibt sie nur als Segmente von wenigen Sekunden. <c>--download-sections</c> und
    /// <c>--downloader ffmpeg</c> übergeben ffmpeg aber nur eine einzige Adresse, und die führt
    /// zum letzten Segment. yt-dlp endet trotzdem mit Exit-Code 0, das Ergebnis ist eine Datei
    /// von wenigen Sekunden. Deshalb wird die Segmentliste aus den Metadaten auf den Ausschnitt
    /// gekürzt und über <c>--load-info-json</c> von yt-dlp selbst geladen.</para>
    ///
    /// <para><b>Rückfall:</b> Alles hier ist eine Zusatzprüfung. Schlägt sie fehl oder passt die
    /// Quelle nicht ins Muster, läuft der Download so, wie er ohne diese Klasse liefe.</para>
    /// </summary>
    internal static class DownloadSection
    {
        private const string InfoFileName = "meta.info.json";
        private const string TrimmedFileName = "section.info.json";
        private const string AudioOnlySelector = "bestaudio/best";

        private static readonly TimeSpan SourceQueryTimeout = TimeSpan.FromSeconds(30);

        // Gemessen: 36 s für einen 10-Stunden-Stream mit 162 MB Metadaten.
        private static readonly TimeSpan InfoFileTimeout = TimeSpan.FromMinutes(3);

        // Felder, die nach dem Kürzen falsch wären oder nur unnötig Platz belegen.
        private static readonly HashSet<string> RootFieldsToDrop = new(StringComparer.Ordinal)
        {
            "requested_formats", "requested_downloads", "requested_subtitles",
            "automatic_captions", "subtitles", "filesize_approx",
            // Bei nur einem gewählten Format (Nur-Audio) legt yt-dlp dessen Felder zusätzlich auf
            // die oberste Ebene, darunter die vollständige Segmentliste.
            "fragments", "filesize",
        };

        private static readonly HashSet<string> FormatFieldsToDrop = new(StringComparer.Ordinal)
        {
            "fragments", "filesize", "filesize_approx",
        };

        /// <summary>Der Ausschnitt eines Auftrags: Zeitspanne oder „erste N Sekunden".
        /// <c>null</c>, wenn der Auftrag keinen hat oder die Zeiten nicht auswertbar sind.</summary>
        public static RequestedSection? FromJob(YtDlpJob job)
        {
            if (job.Timespan is { } timespan)
            {
                return ParseTime(timespan.From) is { } start && ParseTime(timespan.To) is { } end && end > start
                    ? new RequestedSection(start, end)
                    : null;
            }

            return job.FirstSecondsDuration is { } firstSeconds && ParseTime(firstSeconds) is { } seconds && seconds > 0
                ? new RequestedSection(0, seconds)
                : null;
        }

        /// <summary>Liest eine Zeit wie yt-dlp: <c>ss</c>, <c>mm:ss</c> oder <c>hh:mm:ss</c>, jeweils
        /// mit optionalen Nachkommastellen. <see cref="TimeSpan.TryParse(string, out TimeSpan)"/>
        /// passt hier nicht: Es liest <c>90</c> als 90 Tage und <c>1:32</c> als Stunde:Minute.</summary>
        internal static double? ParseTime(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            string[] parts = text.Trim().Split(':');
            if (parts.Length > 3)
                return null;

            double total = 0;
            foreach (string part in parts)
            {
                if (!double.TryParse(part, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double value))
                    return null;
                total = (total * 60) + value;
            }
            return total;
        }

        /// <summary>Wie lang das Ergebnis sein sollte: Der Ausschnitt, begrenzt durch das Videoende.
        /// <c>null</c>, wenn sich das nicht sagen lässt (Gesamtdauer unbekannt, Start hinter dem Ende).
        /// Ohne die Gesamtdauer gibt es bewusst kein Urteil: Eine Endzeit hinter dem Videoende
        /// ist eine gültige Eingabe und darf nicht als Fehlschlag gelten.</summary>
        internal static double? ExpectedSeconds(RequestedSection section, double? durationSeconds)
        {
            if (durationSeconds is not { } duration)
                return null;

            double expected = Math.Min(section.EndSeconds, duration) - section.StartSeconds;
            return expected > 0 ? expected : null;
        }

        /// <summary>Ist das Ergebnis offensichtlich unvollständig? Bewusst großzügig: Schnitte an
        /// Keyframes weichen normal um Sekunden ab, und ein funktionierender Download darf hier
        /// nie als Fehler enden. Es zählt nur, was unter der Hälfte liegt und mindestens fünf
        /// Sekunden fehlen lässt.</summary>
        internal static bool IsTooShort(double expectedSeconds, double actualSeconds) =>
            actualSeconds < expectedSeconds * 0.5 && expectedSeconds - actualSeconds >= 5;

        /// <summary>Liest die Ausgabe von <c>--print "%(live_status)s|%(format_id)s|%(duration)s"</c>.
        /// <c>null</c> bei allem außer genau einer auswertbaren Zeile, denn mehrere Zeilen bedeuten
        /// mehrere Videos, und dafür gilt ein einzelner Ausschnitt nicht eindeutig.</summary>
        internal static SectionSource? ParseSourceLine(string stdOut)
        {
            string[] lines = stdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (lines.Length != 1)
                return null;

            string[] fields = lines[0].Split('|');
            if (fields.Length != 3 || fields[0].Length == 0 || fields[1].Length == 0)
                return null;

            double? duration = double.TryParse(fields[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double d) && d > 0
                ? d
                : null;
            return new SectionSource(fields[0], fields[1].Split('+', StringSplitOptions.RemoveEmptyEntries), duration);
        }

        /// <summary>Welche Segmente einen Ausschnitt abdecken, nach außen gerundet: Es fehlt nichts
        /// vom Ausschnitt, dafür kann er je Seite um weniger als ein Segment länger werden. Ein
        /// Ende hinter dem letzten Segment wird begrenzt. <c>null</c>, wenn nichts übrig bleibt.</summary>
        internal static (int First, int Last)? ComputeFragmentRange(RequestedSection section, double segmentSeconds, int fragmentCount)
        {
            if (segmentSeconds <= 0 || fragmentCount <= 0)
                return null;

            int first = (int)Math.Floor(section.StartSeconds / segmentSeconds);
            int last = Math.Min((int)Math.Ceiling(section.EndSeconds / segmentSeconds) - 1, fragmentCount - 1);
            return first < fragmentCount && last >= first ? (first, last) : null;
        }

        /// <summary>
        /// Schreibt die Metadaten gekürzt auf <paramref name="formatIds"/> und den Ausschnitt.
        /// <c>null</c>, wenn die Quelle nicht ins Muster passt — dann wird nichts geschrieben.
        /// Das Muster: Jedes Format hat eine Segmentliste und eine Segmentdauer, und Anzahl mal
        /// Dauer entspricht der Gesamtdauer. Sonst ließe sich aus einer Zeit kein Segment berechnen.
        /// </summary>
        internal static TrimmedSectionInfo? TrimInfoJson(
            Stream input, Stream output, IReadOnlyList<string> formatIds, RequestedSection section)
        {
            // Liest die ganze Datei (bei langen Streams über 100 MB). Unbedenklich: Sie stammt von
            // yt-dlp, liegt im eigenen Temp-Ordner und ist nur für diesen Augenblick im Speicher.
            using JsonDocument document = JsonDocument.Parse(input);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("formats", out JsonElement formats)
                || formats.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            double? duration = root.TryGetProperty("duration", out JsonElement durationElement)
                && durationElement.ValueKind == JsonValueKind.Number
                    ? durationElement.GetDouble()
                    : null;

            var selected = new List<(JsonElement Format, JsonElement Fragments, int First, int Last, double Segment)>();
            foreach (string id in formatIds)
            {
                if (FindFormat(formats, id) is not { } format
                    || !format.TryGetProperty("fragments", out JsonElement fragments)
                    || fragments.ValueKind != JsonValueKind.Array
                    || !format.TryGetProperty("target_duration", out JsonElement target)
                    || target.ValueKind != JsonValueKind.Number)
                {
                    return null;
                }

                double segment = target.GetDouble();
                int count = fragments.GetArrayLength();
                if (ComputeFragmentRange(section, segment, count) is not { } range
                    || !MatchesDuration(count, segment, duration))
                {
                    return null;
                }
                selected.Add((format, fragments, range.First, range.Last, segment));
            }

            if (selected.Count == 0)
                return null;

            var firstSelected = selected[0];
            string? firstUrl = firstSelected.Fragments[firstSelected.First].TryGetProperty("url", out JsonElement urlElement)
                ? urlElement.GetString()
                : null;
            if (string.IsNullOrEmpty(firstUrl))
                return null;

            using var writer = new Utf8JsonWriter(output);
            writer.WriteStartObject();
            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (RootFieldsToDrop.Contains(property.Name))
                    continue;

                if (property.NameEquals("formats"))
                {
                    writer.WriteStartArray("formats");
                    foreach (var item in selected)
                        WriteTrimmedFormat(writer, item.Format, item.Fragments, item.First, item.Last);
                    writer.WriteEndArray();
                }
                else if (property.NameEquals("duration"))
                {
                    writer.WriteNumber("duration", (firstSelected.Last - firstSelected.First + 1) * firstSelected.Segment);
                }
                else
                {
                    property.WriteTo(writer);
                }
            }
            writer.WriteEndObject();
            writer.Flush();

            return new TrimmedSectionInfo(firstUrl, firstSelected.First * firstSelected.Segment, firstSelected.Segment);
        }

        /// <summary>Fragt Live-Status, Formate und Dauer ab. Jeder Fehlschlag außer dem Abbruch
        /// durch den Nutzer ergibt <c>null</c>: Der Download läuft dann wie bisher, und ein
        /// echtes Problem meldet er selbst.</summary>
        public static async Task<SectionSource?> QuerySourceAsync(
            string ytDlpPath, YtDlpJob job, Action<string> log, CancellationToken ct)
        {
            try
            {
                var result = await ProcessRunner.RunAsync(
                    ytDlpPath,
                    ["--no-warnings", "--no-check-certificates", "--no-playlist", "--skip-download",
                     "-f", SelectorOf(job), "--print", "%(live_status)s|%(format_id)s|%(duration)s", job.Url],
                    timeout: SourceQueryTimeout,
                    ct: ct);

                return result.Success ? ParseSourceLine(result.StdOut) : null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log($"[ABSCHNITT] Vorprüfung nicht möglich: {ex.Message}");
                Log.Warn("Vorprüfung für einen Zeitausschnitt fehlgeschlagen.", ex);
                return null;
            }
        }

        /// <summary>Holt die Metadaten, kürzt sie und prüft am ersten Segment, ob die Berechnung
        /// stimmt. Hatte ein Stream Aussetzer, passt die Zeit nicht mehr zur Segmentnummer — dann
        /// lieber kein Ergebnis als ein falscher Ausschnitt. <c>null</c> bedeutet: Dieser Weg steht
        /// nicht zur Verfügung.</summary>
        public static async Task<PreparedSectionInfo?> PrepareAsync(
            string ytDlpPath, YtDlpJob job, SectionSource source, RequestedSection section,
            Action<string> log, CancellationToken ct)
        {
            string directory = Path.Combine(AppPaths.ToolTempDir, $"section-{Guid.NewGuid():N}");
            try
            {
                Directory.CreateDirectory(directory);

                var result = await ProcessRunner.RunAsync(
                    ytDlpPath,
                    ["--no-warnings", "--no-check-certificates", "--no-playlist", "--skip-download", "--write-info-json",
                     "-f", SelectorOf(job),
                     "-o", YtDlpOutputTemplate.EscapeLiteral(Path.Combine(directory, "meta")) + ".%(ext)s",
                     job.Url],
                    timeout: InfoFileTimeout,
                    ct: ct);

                string infoPath = Path.Combine(directory, InfoFileName);
                if (!result.Success || !File.Exists(infoPath))
                {
                    log($"[ABSCHNITT] Metadaten nicht verfügbar (Exit-Code {result.ExitCode}).");
                    DeleteWorkDirectory(directory);
                    return null;
                }

                string trimmedPath = Path.Combine(directory, TrimmedFileName);
                TrimmedSectionInfo? trimmed = await Task.Run(() =>
                {
                    using FileStream input = File.OpenRead(infoPath);
                    using FileStream output = File.Create(trimmedPath);
                    return TrimInfoJson(input, output, source.FormatIds, section);
                }, ct);
                File.Delete(infoPath);

                if (trimmed is null)
                {
                    log("[ABSCHNITT] Segmentliste passt nicht zum Zeitausschnitt.");
                    DeleteWorkDirectory(directory);
                    return null;
                }

                if (!await FirstFragmentMatchesAsync(trimmed, log, ct))
                {
                    DeleteWorkDirectory(directory);
                    return null;
                }

                return new PreparedSectionInfo(directory, trimmedPath);
            }
            catch (OperationCanceledException)
            {
                DeleteWorkDirectory(directory);
                throw;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException
                                          or TimeoutException or InvalidOperationException or ToolMissingException)
            {
                log($"[ABSCHNITT] Segmentliste nicht verfügbar: {ex.Message}");
                Log.Warn("Segmentliste für einen Zeitausschnitt konnte nicht erstellt werden.", ex);
                DeleteWorkDirectory(directory);
                return null;
            }
        }

        /// <summary>Entfernt einen Arbeitsordner. Best-Effort: Reste liegen im eigenen Temp-Ordner,
        /// den der Aufräumdurchgang beim Programmstart mit erfasst.</summary>
        internal static void DeleteWorkDirectory(string directory)
        {
            try
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.Warn($"Arbeitsordner nicht löschbar: {directory} ({ex.Message})");
            }
        }

        private static async Task<bool> FirstFragmentMatchesAsync(TrimmedSectionInfo trimmed, Action<string> log, CancellationToken ct)
        {
            // Der Wert stammt aus Metadaten einer fremden Seite. Nur https geht an ffprobe weiter.
            if (!Uri.TryCreate(trimmed.FirstFragmentUrl, UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttps)
                return true;

            double? actual = await MediaProbe.GetFirstPacketSecondsAsync(AppPaths.Ffprobe, trimmed.FirstFragmentUrl, ct);
            if (actual is null)
            {
                log("[ABSCHNITT] Erstes Segment nicht prüfbar, fahre ohne diese Prüfung fort.");
                return true;
            }

            if (Math.Abs(actual.Value - trimmed.FirstFragmentSeconds) <= trimmed.SegmentSeconds * 2)
                return true;

            log($"[ABSCHNITT] Erstes Segment beginnt bei {actual.Value.ToString(CultureInfo.InvariantCulture)} s, " +
                $"erwartet waren {trimmed.FirstFragmentSeconds.ToString(CultureInfo.InvariantCulture)} s.");
            return false;
        }

        private static string SelectorOf(YtDlpJob job) =>
            job.IsAudioOnly || string.IsNullOrEmpty(job.FormatSelector) ? AudioOnlySelector : job.FormatSelector;

        private static JsonElement? FindFormat(JsonElement formats, string id)
        {
            foreach (JsonElement format in formats.EnumerateArray())
            {
                if (format.TryGetProperty("format_id", out JsonElement value) && value.ValueEquals(id))
                    return format;
            }
            return null;
        }

        // Ohne bekannte Gesamtdauer entscheidet allein die Prüfung am ersten Segment. Die Toleranz
        // deckt ab, was real vorkommt: 17658 Segmente zu 2 s bei einer gemeldeten Dauer von 35312 s.
        private static bool MatchesDuration(int count, double segmentSeconds, double? duration) =>
            duration is not { } d || Math.Abs((count * segmentSeconds) - d) <= Math.Max(segmentSeconds * 5, d * 0.01);

        private static void WriteTrimmedFormat(
            Utf8JsonWriter writer, JsonElement format, JsonElement fragments, int first, int last)
        {
            writer.WriteStartObject();
            foreach (JsonProperty property in format.EnumerateObject())
            {
                if (!FormatFieldsToDrop.Contains(property.Name))
                    property.WriteTo(writer);
            }

            writer.WriteStartArray("fragments");
            for (int i = first; i <= last; i++)
            {
                writer.WriteStartObject();
                foreach (JsonProperty property in fragments[i].EnumerateObject())
                {
                    // Die Angabe beschreibt die ungekürzte Liste und würde yt-dlp in die Irre führen.
                    if (!property.NameEquals("fragment_count"))
                        property.WriteTo(writer);
                }
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
    }
}
