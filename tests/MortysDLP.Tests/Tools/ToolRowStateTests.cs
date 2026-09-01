using MortysDLP.Models;
using MortysDLP.Services.Tools;

namespace MortysDLP.Tests.Tools;

/// <summary>
/// Prüft, welche Aktionen auf der Seite „Werkzeuge" in welchem Zustand erlaubt sind — reine
/// Logik, ohne Oberfläche und ohne echtes Werkzeug. Genau die Bedingungen, die sonst in
/// XAML-Sichtbarkeiten verstreut würden.
/// </summary>
public class ToolRowStateTests
{
    private static ToolCheckOutcome MakeOutcome(bool installed, ToolHealth health, bool offer)
    {
        var status = new ToolStatus("test", installed, installed ? [] : ["missing.exe"]);
        var probe = health == ToolHealth.Ok
            ? new ToolProbe(ToolHealth.Ok, ToolVersion.Parse("1.0"), "1.0")
            : new ToolProbe(health, ToolVersion.Unknown, null);
        var verdict = new ToolUpdateVerdict(offer, "test");

        return new ToolCheckOutcome(new YtDlpTool(), status, probe, null, ToolVersion.Unknown, false, verdict);
    }

    [Fact]
    public void NichtInstalliert_GiltAlsFehlend()
    {
        var outcome = MakeOutcome(installed: false, health: ToolHealth.NotInstalled, offer: false);

        Assert.Equal(ToolRowState.Missing, ToolRowActions.StateFor(outcome));
    }

    /// <summary>Eine Datei liegt da, antwortet aber nicht wie erwartet (kein Start, Zeitlimit,
    /// Exit-Code ungleich 0, oder ein fremdes Programm) — das gilt als „unvollständig", nicht
    /// als installiert. <c>ToolHealth</c> ist intern, deshalb hier über den Namen statt direkt
    /// als Parametertyp - ein öffentliches Testmethoden-Signatur darf keinen weniger
    /// zugänglichen Typ tragen.</summary>
    [Theory]
    [InlineData("NoAnswer")]
    [InlineData("Foreign")]
    public void InstalliertAberNichtBrauchbar_GiltAlsUnvollstaendig(string healthName)
    {
        var health = Enum.Parse<ToolHealth>(healthName);
        var outcome = MakeOutcome(installed: true, health: health, offer: false);

        Assert.Equal(ToolRowState.Broken, ToolRowActions.StateFor(outcome));
    }

    [Fact]
    public void BrauchbarOhneUpdate_GiltAlsOk()
    {
        var outcome = MakeOutcome(installed: true, health: ToolHealth.Ok, offer: false);

        Assert.Equal(ToolRowState.Ok, ToolRowActions.StateFor(outcome));
    }

    [Fact]
    public void BrauchbarMitUpdate_GiltAlsUpdateVerfuegbar()
    {
        var outcome = MakeOutcome(installed: true, health: ToolHealth.Ok, offer: true);

        Assert.Equal(ToolRowState.UpdateAvailable, ToolRowActions.StateFor(outcome));
    }

    [Theory]
    [InlineData("Missing", true, false, false, true)]
    [InlineData("Broken", true, false, true, true)]
    [InlineData("Ok", true, false, true, true)]
    [InlineData("UpdateAvailable", true, true, true, true)]
    public void For_LiefertErwarteteAktionenJeZustand(
        string stateName, bool repair, bool update, bool uninstall, bool openFolder)
    {
        var state = Enum.Parse<ToolRowState>(stateName);
        var actions = ToolRowActions.For(state);

        Assert.Equal(repair, actions.CanRepair);
        Assert.Equal(update, actions.CanUpdate);
        Assert.Equal(uninstall, actions.CanUninstall);
        Assert.Equal(openFolder, actions.CanOpenFolder);
    }
}
