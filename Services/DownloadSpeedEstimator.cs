namespace MortysDLP.Services
{
    /// <summary>
    /// Schätzt eine ruhige Downloadgeschwindigkeit aus dem eigenen, über die Zeit beobachteten
    /// Byte-Zuwachs - statt die von yt-dlp pro Netzwerk-Fragment neu berechnete Momentanangabe
    /// direkt anzuzeigen. Diese schwankt beobachtet zwischen unter 1 MB/s und über 60 MB/s
    /// innerhalb von Sekundenbruchteilen, obwohl der tatsächliche Download gleichmäßig läuft -
    /// für eine lesbare Anzeige ungeeignet.
    /// </summary>
    internal sealed class DownloadSpeedEstimator
    {
        // Beide Werte sind ein Kompromiss aus Ruhe und Reaktionsfähigkeit, keine exakte Größe:
        // Ein kleineres Intervall reagiert schneller auf echte Tempowechsel, glättet aber
        // weniger; ein größerer Glättungsfaktor täte dasselbe in die andere Richtung.
        private const double MinIntervalSeconds = 0.25;
        private const double SmoothingFactor = 0.2;

        private long? _lastBytes;
        private double _lastElapsedSeconds;
        private double? _smoothedBytesPerSecond;

        /// <summary>Verarbeitet einen neuen Byte-Stand zum Zeitpunkt <paramref name="elapsedSeconds"/>
        /// (fortlaufend seit Beginn der Messung, z. B. aus einer laufenden Stoppuhr). Liegt die
        /// letzte Messung weniger als <see cref="MinIntervalSeconds"/> zurück, bleibt der
        /// zuletzt geglättete Wert unverändert - so verwässern mehrere Meldungen pro
        /// Zeitscheibe die Schätzung nicht. Liefert <c>null</c>, solange noch keine zweite,
        /// ausreichend weit auseinanderliegende Messung vorliegt.</summary>
        public double? Update(long bytes, double elapsedSeconds)
        {
            if (!_lastBytes.HasValue)
            {
                _lastBytes = bytes;
                _lastElapsedSeconds = elapsedSeconds;
                return _smoothedBytesPerSecond;
            }

            double deltaSeconds = elapsedSeconds - _lastElapsedSeconds;
            if (deltaSeconds < MinIntervalSeconds)
                return _smoothedBytesPerSecond;

            double sample = Math.Max(0, bytes - _lastBytes.Value) / deltaSeconds;
            _smoothedBytesPerSecond = _smoothedBytesPerSecond.HasValue
                ? _smoothedBytesPerSecond.Value + SmoothingFactor * (sample - _smoothedBytesPerSecond.Value)
                : sample;
            _lastBytes = bytes;
            _lastElapsedSeconds = elapsedSeconds;
            return _smoothedBytesPerSecond;
        }

        /// <summary>Setzt die Schätzung zurück - notwendig, wenn ein neuer Stream (z. B. der
        /// Wechsel von Video- auf Audiospur) wieder bei 0 Bytes zu zählen beginnt, damit dieser
        /// Sprung nicht als (negativer) Geschwindigkeitswert in die Glättung einfließt.</summary>
        public void Reset()
        {
            Resync();
            _smoothedBytesPerSecond = null;
        }

        /// <summary>Verwirft nur die <b>Zeitbasis</b>, nicht den bisher geglätteten Wert - für
        /// eine Unterbrechung, nach der derselbe Stream an derselben Byte-Position weiterläuft
        /// (yt-dlp-Neustart mit <c>--continue</c> nach einem Bandbreitenwechsel).
        ///
        /// <para>Ohne das würde die Pause während des Prozess-Neustarts als Messintervall
        /// zählen: wenige Bytes geteilt durch mehrere Sekunden ergäbe eine eingebrochene Rate,
        /// obwohl der Download gleich schnell weiterläuft. Ein voller <see cref="Reset"/> wäre
        /// hier ebenfalls falsch - er würde die Anzeige bis zur nächsten Messung leeren,
        /// obwohl ein gültiger Wert vorliegt.</para></summary>
        public void Resync()
        {
            _lastBytes = null;
            _lastElapsedSeconds = 0;
        }

        /// <summary>Berechnet aus geglätteter Geschwindigkeit und verbleibenden Bytes eine
        /// Restzeit, oder <c>null</c>, wenn eine der beiden Größen fehlt, null/negativ ist,
        /// oder das Ergebnis außerhalb des darstellbaren Bereichs liegt.</summary>
        public static TimeSpan? EstimateEta(long? remainingBytes, double? smoothedBytesPerSecond)
        {
            if (remainingBytes is not > 0 || smoothedBytesPerSecond is not > 0)
                return null;

            double etaSeconds = remainingBytes.Value / smoothedBytesPerSecond.Value;
            return etaSeconds <= TimeSpan.MaxValue.TotalSeconds ? TimeSpan.FromSeconds(etaSeconds) : null;
        }
    }
}
