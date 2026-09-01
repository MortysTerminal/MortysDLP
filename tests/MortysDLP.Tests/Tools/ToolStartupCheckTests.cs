using MortysDLP.Models;
using MortysDLP.Services.Tools;

namespace MortysDLP.Tests.Tools;

/// <summary>
/// Prüft, ob ein erforderliches Werkzeug den Start blockiert — reine Logik, ohne Netzzugriff
/// und ohne Dialoge. Genau die Entscheidung, die sonst mitten im Startpfad verstreut würde.
/// </summary>
public class ToolStartupCheckTests
{
    [Fact]
    public void NichtInstalliert_MussGeladenWerden()
    {
        var status = new ToolStatus("test", Installed: false, MissingPaths: ["missing.exe"]);

        Assert.Equal(ToolStartupAction.MustInstall, ToolStartupDecision.For(status, ToolProbe.NotInstalled));
    }

    /// <summary>Datei da, antwortet aber nicht wie erwartet (kein Antwort oder ein fremdes
    /// Programm) — muss ebenso installiert (repariert) werden wie ein fehlendes Werkzeug.
    /// <c>ToolHealth</c> ist intern, deshalb hier über den Namen statt direkt als
    /// Parametertyp - eine öffentliche Testmethoden-Signatur darf keinen weniger
    /// zugänglichen Typ tragen (CS0051).</summary>
    [Theory]
    [InlineData("NoAnswer")]
    [InlineData("Foreign")]
    public void InstalliertAberNichtBrauchbar_MussGeladenWerden(string healthName)
    {
        var health = Enum.Parse<ToolHealth>(healthName);
        var status = new ToolStatus("test", Installed: true, MissingPaths: []);
        var probe = new ToolProbe(health, ToolVersion.Unknown, null);

        Assert.Equal(ToolStartupAction.MustInstall, ToolStartupDecision.For(status, probe));
    }

    [Fact]
    public void InstalliertUndBrauchbar_DarfStarten()
    {
        var status = new ToolStatus("test", Installed: true, MissingPaths: []);
        var probe = new ToolProbe(ToolHealth.Ok, ToolVersion.Parse("1.0"), "1.0");

        Assert.Equal(ToolStartupAction.CanProceed, ToolStartupDecision.For(status, probe));
    }
}
