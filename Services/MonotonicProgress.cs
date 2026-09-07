namespace MortysDLP.Services
{
    /// <summary>
    /// Glättet einen Fortschrittsanteil (0.0–1.0) für die Anzeige so, dass der Balken
    /// <b>nie rückwärts</b> läuft und ein einzelner Ausreißer nach oben ihn nicht springen
    /// lässt.
    ///
    /// <para>Hintergrund: Bei einem HLS-/Fragment-Download (z. B. YouTube ohne JS-Laufzeit)
    /// kennt yt-dlp die Gesamtgröße nicht, sondern schätzt sie pro Fragment neu — und diese
    /// Schätzung schwankt teils um mehr als das Dreifache. Der rohe Anteil
    /// <c>downloaded / estimate</c> zittert dadurch vor und zurück. Diese Klasse zieht den
    /// angezeigten Wert nur vorwärts und nur in kleinen Schritten nach.</para>
    ///
    /// <para>Für einen Download mit bekannter Gesamtgröße ist Zittern kein Thema; dort wird
    /// <see cref="Advance"/> mit <paramref name="alpha"/> = 1 und großem
    /// <paramref name="maxStep"/> aufgerufen und verhält sich wie eine reine
    /// „nie-rückwärts"-Sperre.</para>
    /// </summary>
    internal sealed class MonotonicProgress
    {
        private double _shown;

        /// <summary>Der zuletzt angezeigte Anteil (0.0–1.0).</summary>
        public double Value => _shown;

        /// <summary>Setzt die Glättung für einen neuen Stream / ein neues Video zurück.</summary>
        public void Reset() => _shown = 0.0;

        /// <summary>
        /// Nimmt einen rohen Anteil entgegen und gibt den geglätteten, nie kleiner werdenden
        /// Anzeigewert zurück.
        /// </summary>
        /// <param name="rawFraction">Roher Anteil 0.0–1.0 (wird geklammert).</param>
        /// <param name="alpha">Anteil der Lücke zum Rohwert, der pro Aufruf aufgeholt wird
        /// (0–1). Klein = träge/ruhig, 1 = sofort.</param>
        /// <param name="maxStep">Obergrenze, wie weit der Anzeigewert pro Aufruf steigen darf.
        /// Begrenzt den Effekt eines einzelnen Schätz-Ausreißers.</param>
        public double Advance(double rawFraction, double alpha = 0.2, double maxStep = 0.03)
        {
            double raw = System.Math.Clamp(rawFraction, 0.0, 1.0);
            double gap = raw - _shown;
            if (gap > 0.0)
                _shown += System.Math.Min(gap * alpha, maxStep);
            return _shown;
        }
    }
}
