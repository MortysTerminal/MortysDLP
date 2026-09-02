namespace MortysDLP.Services
{
    /// <summary>
    /// Beschreibt einen yt-dlp-Auftrag vollständig, unabhängig davon, welche Seite ihn stellt —
    /// das Datenobjekt, das <see cref="YtDlpArgumentBuilder.Build"/> in eine Kommandozeile
    /// übersetzt. Deckt die Vereinigung dessen ab, was <c>DownloadPage</c>,
    /// <c>BatchDownloadPage</c> und <c>TwitchPage</c> heute jeweils einzeln zusammenbauen —
    /// nicht nur den umfangreichsten der drei Fälle.
    ///
    /// <para>Formatselektoren (<see cref="FormatSelector"/>) kommen bereits fertig gebaut
    /// herein — wie ein Selektor aus Qualität und Container entsteht, ist eine eigene,
    /// wiederverwendbare Frage (<see cref="YtDlpArgumentBuilder.BuildYtDlpVideoFormatSelector"/>),
    /// keine des Jobs selbst. Das lässt offen, dass verschiedene Aufrufer unterschiedliche
    /// Selektor-Strategien verwenden können, ohne dass <see cref="YtDlpJob"/> das wissen
    /// müsste.</para>
    /// </summary>
    internal sealed record YtDlpJob
    {
        /// <summary>Die herunterzuladende Adresse.</summary>
        public required string Url { get; init; }

        /// <summary>Vollständiger <c>-o</c>-Wert (Zielordner **und** Namensmuster) — der
        /// Aufrufer setzt Variantenkennzeichnung und Sonderzeichen-Bereinigung bereits um,
        /// bevor der Job entsteht.</summary>
        public required string OutputTemplate { get; init; }

        /// <summary>Nur-Audio-Modus (<c>-x</c>) statt Video.</summary>
        public bool IsAudioOnly { get; init; }

        // ── Video (nur wenn !IsAudioOnly) ───────────────────────────────────────────

        /// <summary>Fertiger <c>-f</c>-Wert. Leer/<c>null</c> bedeutet: kein eigener
        /// Formatselektor, yt-dlp entscheidet selbst.</summary>
        public string? FormatSelector { get; init; }

        /// <summary><c>--merge-output-format</c>.</summary>
        public string? MergeOutputFormat { get; init; }

        /// <summary>Setzt <c>--postprocessor-args "Merger:-c copy -movflags +faststart"</c> —
        /// verlustfreies Zusammenführen (Stream-Copy) statt eines erneuten Encodes, mit
        /// Web-Streaming-Flag für MP4/MOV.</summary>
        public bool MergeStreamCopyFastStart { get; init; }

        // ── Audio (nur wenn IsAudioOnly) ─────────────────────────────────────────────

        /// <summary><c>--audio-format</c>.</summary>
        public string? AudioFormat { get; init; }

        /// <summary><c>--audio-quality</c>. <c>null</c>/leer heißt „höchste" — dafür wird
        /// bewusst **kein** Flag gesetzt, nicht ein Wert wie <c>0</c> — damit bleibt yt-dlp
        /// frei, die beste verfügbare Spur zu wählen.</summary>
        public string? AudioBitrate { get; init; }

        /// <summary>Erzwingt <c>--postprocessor-args "ffmpeg:-ar 48000 -ac 2"</c> — für eine
        /// Quelle mit zu niedriger Samplerate oder Mono-Ton, die sonst mit ihren
        /// Original-Werten übernommen würde.</summary>
        public bool AudioForceReencode { get; init; }

        // ── Zeitausschnitt (heute nur von DownloadPage genutzt) ─────────────────────

        /// <summary><c>--download-sections</c> als <c>*von-bis</c>.</summary>
        public (string From, string To)? Timespan { get; init; }

        /// <summary>Dauer in Sekunden für „nur die ersten N Sekunden" — setzt zusammen mit
        /// <see cref="FirstSecondsFfmpegPath"/> <c>--downloader</c>/<c>--downloader-args</c>,
        /// weil yt-dlps eigener Abschnitts-Download dafür ungeeignet ist.</summary>
        public string? FirstSecondsDuration { get; init; }

        /// <summary>Pfad zu ffmpeg, nur gesetzt, wenn <see cref="FirstSecondsDuration"/> gesetzt
        /// ist.</summary>
        public string? FirstSecondsFfmpegPath { get; init; }

        // ── Allgemein ─────────────────────────────────────────────────────────────

        /// <summary>Bandbreitenlimit in MB/s für <c>--limit-rate</c>. 0 oder negativ heißt
        /// „kein Limit" — dann wird das Flag ganz weggelassen, nicht mit einem Wert von
        /// <c>0</c> übergeben.</summary>
        public double BandwidthLimitMBps { get; init; }

        /// <summary><c>--no-playlist</c>. Heute in allen drei Aufrufern immer <c>true</c> —
        /// bewusst trotzdem ein Feld statt einer festen Konstante im Builder: Ein Auftrag, der
        /// eine Playlist als Ganzes laden soll, ist damit ausdrückbar, ohne den Builder ändern
        /// zu müssen.</summary>
        public bool NoPlaylist { get; init; } = true;
    }
}
