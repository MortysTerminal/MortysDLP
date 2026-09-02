using MortysDLP.Services;

namespace MortysDLP.Tests;

/// <summary>
/// Prüft <see cref="DownloadSpeedEstimator"/> — reine Logik, keine echten Zeitmessungen nötig.
/// Der zentrale Punkt: Eine einzelne, extrem abweichende Momentanmessung darf den angezeigten
/// Wert nicht auf einen Schlag dorthin springen lassen (das war das Verhalten, das yt-dlps
/// eigene Momentanangabe unlesbar machte).
/// </summary>
public class DownloadSpeedEstimatorTests
{
    [Fact]
    public void Update_ErsteMessung_LiefertNochKeinenWert()
    {
        var estimator = new DownloadSpeedEstimator();

        double? result = estimator.Update(1_000_000, elapsedSeconds: 0.0);

        Assert.Null(result);
    }

    [Fact]
    public void Update_ZweiteMessungNachAusreichenderZeit_LiefertRohrateOhneGlaettung()
    {
        var estimator = new DownloadSpeedEstimator();
        estimator.Update(0, elapsedSeconds: 0.0);

        // 1.000.000 Byte in 1 s = 1.000.000 Byte/s - erste echte Messung, noch nichts zu glätten.
        double? result = estimator.Update(1_000_000, elapsedSeconds: 1.0);

        Assert.Equal(1_000_000.0, result);
    }

    [Fact]
    public void Update_MessungZuFruehNachDerVorherigen_LiefertUnveraendertenWert()
    {
        var estimator = new DownloadSpeedEstimator();
        estimator.Update(0, elapsedSeconds: 0.0);
        double? erste = estimator.Update(1_000_000, elapsedSeconds: 1.0);

        // Nur 50 ms später - unter dem Mindestabstand, darf den Wert nicht verändern.
        double? zweite = estimator.Update(1_050_000, elapsedSeconds: 1.05);

        Assert.Equal(erste, zweite);
    }

    [Fact]
    public void Update_EinzelnerAusreisser_SchlaegtNichtVollAufDenAngezeigtenWertDurch()
    {
        var estimator = new DownloadSpeedEstimator();
        estimator.Update(0, elapsedSeconds: 0.0);
        double vorher = estimator.Update(1_000_000, elapsedSeconds: 1.0)!.Value;

        // Ein einzelner extremer Ausschlag (60x so schnell) wie im echten Log beobachtet.
        double? nachAusreisser = estimator.Update(61_000_000, elapsedSeconds: 2.0);

        Assert.NotNull(nachAusreisser);
        Assert.True(nachAusreisser.Value < 61_000_000.0,
            "Ein einzelner Ausreißer darf den geglätteten Wert nicht auf die Momentanrate springen lassen.");
        Assert.True(nachAusreisser.Value > vorher,
            "Der geglättete Wert soll sich trotzdem in Richtung des neuen Werts bewegen.");
    }

    [Fact]
    public void Update_GleichbleibendeRate_KonvergiertGegenDieseRate()
    {
        var estimator = new DownloadSpeedEstimator();
        double? letzter = null;
        for (int i = 0; i <= 20; i++)
        {
            letzter = estimator.Update(i * 1_000_000L, elapsedSeconds: i);
        }

        Assert.NotNull(letzter);
        Assert.Equal(1_000_000.0, letzter!.Value, precision: 3);
    }

    [Fact]
    public void Update_RueckgaengigeBytes_KlemmtDieMomentanrateAufNull()
    {
        var estimator = new DownloadSpeedEstimator();
        estimator.Update(1_000_000, elapsedSeconds: 0.0);

        // Kommt in der Praxis nicht vor, darf aber nicht zu einer negativen Rate oder einer
        // Ausnahme führen.
        double? result = estimator.Update(500_000, elapsedSeconds: 1.0);

        Assert.Equal(0.0, result);
    }

    [Fact]
    public void Reset_SetztDieSchaetzungVollstaendigZurueck()
    {
        var estimator = new DownloadSpeedEstimator();
        estimator.Update(0, elapsedSeconds: 0.0);
        estimator.Update(1_000_000, elapsedSeconds: 1.0);

        estimator.Reset();
        double? nachReset = estimator.Update(9_999_999, elapsedSeconds: 0.0);

        Assert.Null(nachReset);
    }

    [Fact]
    public void Resync_BehaeltDenBisherGeglaettetenWert()
    {
        // Nach einem yt-dlp-Neustart mit --continue läuft derselbe Stream weiter. Die Anzeige
        // darf dabei nicht leer werden - anders als bei Reset bleibt der letzte gültige Wert
        // stehen, bis eine neue Messung vorliegt.
        var estimator = new DownloadSpeedEstimator();
        estimator.Update(0, elapsedSeconds: 0.0);
        double vorher = estimator.Update(1_000_000, elapsedSeconds: 1.0)!.Value;

        estimator.Resync();
        double? direktNachResync = estimator.Update(1_000_000, elapsedSeconds: 4.0);

        Assert.Equal(vorher, direktNachResync);
    }

    [Fact]
    public void Resync_PauseWaehrendDesNeustarts_ZaehltNichtAlsEingebrocheneRate()
    {
        // Der eigentliche Zweck: Zwischen Kill und erster Meldung des neuen Prozesses vergehen
        // Sekunden ohne Byte-Zuwachs. Ohne Resync wäre das ein Messintervall mit fast null
        // Bytes - die angezeigte Geschwindigkeit bräche ein, obwohl gleich schnell weiterge-
        // laden wird.
        var mitResync = new DownloadSpeedEstimator();
        var ohneResync = new DownloadSpeedEstimator();
        foreach (var e in new[] { mitResync, ohneResync })
        {
            e.Update(0, elapsedSeconds: 0.0);
            e.Update(1_000_000, elapsedSeconds: 1.0);
        }

        mitResync.Resync();

        // 3 s Neustart-Pause, danach läuft es mit unveränderten 1 MB/s weiter.
        double? mit = mitResync.Update(1_000_000, elapsedSeconds: 4.0);
        mit = mitResync.Update(2_000_000, elapsedSeconds: 5.0);
        double? ohne = ohneResync.Update(1_000_000, elapsedSeconds: 4.0);
        ohne = ohneResync.Update(2_000_000, elapsedSeconds: 5.0);

        Assert.NotNull(mit);
        Assert.NotNull(ohne);
        Assert.True(mit!.Value > ohne!.Value,
            "Ohne Resync zieht die Neustart-Pause die geglättete Rate nach unten.");
        Assert.Equal(1_000_000.0, mit.Value);
    }

    // ── EstimateEta ──────────────────────────────────────────────────────────────

    [Fact]
    public void EstimateEta_BekannteRestbytesUndGeschwindigkeit_BerechnetRestzeit()
    {
        TimeSpan? eta = DownloadSpeedEstimator.EstimateEta(remainingBytes: 10_000_000, smoothedBytesPerSecond: 1_000_000);

        Assert.Equal(TimeSpan.FromSeconds(10), eta);
    }

    [Fact]
    public void EstimateEta_GeschwindigkeitUnbekannt_LiefertNull()
    {
        Assert.Null(DownloadSpeedEstimator.EstimateEta(remainingBytes: 1000, smoothedBytesPerSecond: null));
    }

    [Fact]
    public void EstimateEta_RestbytesUnbekannt_LiefertNull()
    {
        Assert.Null(DownloadSpeedEstimator.EstimateEta(remainingBytes: null, smoothedBytesPerSecond: 1000));
    }

    [Fact]
    public void EstimateEta_GeschwindigkeitNull_LiefertNullOhneDivisionDurchNull()
    {
        Assert.Null(DownloadSpeedEstimator.EstimateEta(remainingBytes: 1000, smoothedBytesPerSecond: 0));
    }

    [Fact]
    public void EstimateEta_ExtremKleineGeschwindigkeit_LiefertNullStattUeberlauf()
    {
        Assert.Null(DownloadSpeedEstimator.EstimateEta(remainingBytes: long.MaxValue, smoothedBytesPerSecond: 0.0001));
    }
}
