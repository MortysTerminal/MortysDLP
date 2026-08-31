using MortysDLP.Models;
using MortysDLP.Services.Tools;

namespace MortysDLP.Tests.Tools;

/// <summary>
/// Prüft die Entscheidung „Update anbieten?" — reine Logik. Hier hängen die beiden Fehlerbilder
/// des früheren Zeichenkettenvergleichs (Downgrade-Angebot; Angebot, obwohl das Werkzeug nur
/// nicht geantwortet hat) und die ffmpeg-Politik „anbieten, nie erzwingen".
/// </summary>
public class ToolUpdateDecisionTests
{
    private static ToolVersion V(string text) => ToolVersion.Parse(text);

    [Fact]
    public void YtDlp_EntfernteVersionNeuer_WirdAngeboten()
    {
        var verdict = ToolUpdateDecision.Evaluate(
            V("2026.08.19"), V("2026.08.20"), ToolUpdatePolicy.OnlyWhenNewer);

        Assert.True(verdict.Offer);
    }

    [Fact]
    public void YtDlp_GleicheVersion_WirdNichtAngeboten()
    {
        var verdict = ToolUpdateDecision.Evaluate(
            V("2026.08.19"), V("2026.08.19"), ToolUpdatePolicy.OnlyWhenNewer);

        Assert.False(verdict.Offer);
    }

    /// <summary>Ein lokal installierter Nightly-Build ist neuer als der letzte Release. Der
    /// frühere Zeichenkettenvergleich hätte hier ein Update angeboten, das ein Downgrade gewesen
    /// wäre.</summary>
    [Fact]
    public void YtDlp_LokaleVersionNeuer_KeinDowngradeAngebot()
    {
        var verdict = ToolUpdateDecision.Evaluate(
            V("2026.08.19.232303"), V("2026.08.19"), ToolUpdatePolicy.OnlyWhenNewer);

        Assert.False(verdict.Offer);
        Assert.Contains("Downgrade", verdict.Reason, StringComparison.Ordinal);
    }

    /// <summary>Das Werkzeug hat auf die Versionsfrage nicht geantwortet. Früher war die lokale
    /// Version dann <c>null</c> und galt als „ungleich" — also als Grund für ein Update.</summary>
    [Fact]
    public void LokaleVersionUnbekannt_WirdNichtAngeboten()
    {
        foreach (var policy in new[] { ToolUpdatePolicy.OnlyWhenNewer, ToolUpdatePolicy.WhenDifferent })
        {
            var verdict = ToolUpdateDecision.Evaluate(ToolVersion.Unknown, V("2026.08.20"), policy);

            Assert.False(verdict.Offer);
            Assert.Contains("unbekannt", verdict.Reason, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void EntfernteVersionUnbekannt_WirdNichtAngeboten()
    {
        foreach (var policy in new[] { ToolUpdatePolicy.OnlyWhenNewer, ToolUpdatePolicy.WhenDifferent })
        {
            var verdict = ToolUpdateDecision.Evaluate(V("2026.08.19"), ToolVersion.Unknown, policy);

            Assert.False(verdict.Offer);
        }
    }

    /// <summary>Das Gegenstück zum Absicherungstest in <see cref="ToolVersionTests"/>, eine Ebene
    /// höher: Die tatsächliche Entscheidung für die installierte gyan.dev-Ausgabe gegen die vom
    /// Versionsendpunkt gemeldete Nummer lautet „kein Angebot" — sonst stünde es bei jedem Start
    /// wieder da.</summary>
    [Fact]
    public void Ffmpeg_GleicheAusgabeAndersGeschrieben_WirdNichtAngeboten()
    {
        var verdict = ToolUpdateDecision.Evaluate(
            V("7.1-essentials_build-www.gyan.dev"), V("7.1"), ToolUpdatePolicy.WhenDifferent);

        Assert.False(verdict.Offer);
        Assert.Contains("dieselbe Ausgabe", verdict.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Ffmpeg_NeuereAusgabe_WirdAngeboten()
    {
        var verdict = ToolUpdateDecision.Evaluate(
            V("7.1-essentials_build-www.gyan.dev"), V("7.2"), ToolUpdatePolicy.WhenDifferent);

        Assert.True(verdict.Offer);
        Assert.Contains("nie erzwingen", verdict.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Ffmpeg_AeltereAusgabeGemeldet_KeinDowngradeAngebot()
    {
        var verdict = ToolUpdateDecision.Evaluate(
            V("7.2-essentials_build-www.gyan.dev"), V("7.1"), ToolUpdatePolicy.WhenDifferent);

        Assert.False(verdict.Offer);
        Assert.Contains("Downgrade", verdict.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void OnlyWhenNewer_WeissNicht_FuehrtNieZuEinemAngebot()
    {
        // Unterschiedliche Zahlenkerne, aber die installierte Angabe ist nicht ordnend.
        var verdict = ToolUpdateDecision.Evaluate(
            V("7.1-essentials_build"), V("7.2"), ToolUpdatePolicy.OnlyWhenNewer);

        Assert.False(verdict.Offer);
    }

    [Fact]
    public void JedeEntscheidung_TraegtEineBegruendung()
    {
        var cases = new[]
        {
            (V("2026.08.19"), V("2026.08.20"), ToolUpdatePolicy.OnlyWhenNewer),
            (V("2026.08.19"), V("2026.08.19"), ToolUpdatePolicy.OnlyWhenNewer),
            (V("7.1-essentials"), V("7.1"), ToolUpdatePolicy.WhenDifferent),
            (ToolVersion.Unknown, V("7.1"), ToolUpdatePolicy.WhenDifferent),
            (V("7.1"), ToolVersion.Unknown, ToolUpdatePolicy.WhenDifferent),
        };

        foreach (var (local, remote, policy) in cases)
            Assert.False(string.IsNullOrWhiteSpace(ToolUpdateDecision.Evaluate(local, remote, policy).Reason));
    }
}
