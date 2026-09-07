using MortysDLP.Services;

namespace MortysDLP.Tests;

public class MonotonicProgressTests
{
    [Fact]
    public void Advance_RohwertFaelltZurueck_AnzeigeBleibtStehen()
    {
        var p = new MonotonicProgress();
        p.Advance(0.30, alpha: 1.0, maxStep: 1.0);   // bei bekannter Größe: exakt

        double after = p.Advance(0.10, alpha: 1.0, maxStep: 1.0);

        Assert.Equal(0.30, after, precision: 6);
    }

    [Fact]
    public void Advance_BekannteGroesse_FolgtDemRohwertExakt()
    {
        var p = new MonotonicProgress();

        Assert.Equal(0.20, p.Advance(0.20, alpha: 1.0, maxStep: 1.0), precision: 6);
        Assert.Equal(0.55, p.Advance(0.55, alpha: 1.0, maxStep: 1.0), precision: 6);
        Assert.Equal(0.55, p.Advance(0.40, alpha: 1.0, maxStep: 1.0), precision: 6); // nie rückwärts
    }

    [Fact]
    public void Advance_SchaetzungSpringt_AnzeigeMachtNurKleineSchritteVorwaerts()
    {
        var p = new MonotonicProgress();
        p.Advance(0.10);   // Start ~2 % (0.10 * 0.2)

        // Ausreißer nach oben auf 90 %: darf die Anzeige nicht springen lassen.
        double afterSpike = p.Advance(0.90);
        Assert.True(afterSpike <= 0.10, $"Sprung zu groß: {afterSpike}");

        // Danach ein Ausreißer nach unten: Anzeige bleibt stehen.
        double afterDip = p.Advance(0.02);
        Assert.Equal(afterSpike, afterDip, precision: 6);
    }

    [Fact]
    public void Advance_VieleGuteMessungen_NaehernSichDemRohwert()
    {
        var p = new MonotonicProgress();
        for (int i = 0; i < 200; i++)
            p.Advance(0.6);

        Assert.True(p.Value > 0.59 && p.Value <= 0.6, $"konvergiert nicht: {p.Value}");
    }

    [Fact]
    public void Reset_SetztAufNull()
    {
        var p = new MonotonicProgress();
        p.Advance(0.8, alpha: 1.0, maxStep: 1.0);
        p.Reset();
        Assert.Equal(0.0, p.Value);
    }
}
