using MortysDLP.Helpers;
using System.IO;

namespace MortysDLP.Tests;

/// <summary>
/// Prüft <see cref="MarkOfTheWeb.TryRemove"/> gegen echte Dateien in einem Temp-Verzeichnis —
/// alternative Datenströme lassen sich nicht sinnvoll über einen gefälschten Handler simulieren.
/// Läuft nur auf NTFS/ReFS; auf einem Dateisystem ohne Datenstrom-Unterstützung (z. B. ein
/// FAT32-USB-Stick als TEMP-Ziel) kann der Strom gar nicht erst geschrieben werden — der
/// betroffene Test erkennt das beim Vorbereiten und überspringt sich selbst, statt
/// fehlzuschlagen (xUnit 2 kennt keinen dynamischen Skip zur Laufzeit).
/// </summary>
public class MarkOfTheWebTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _filePath;

    public MarkOfTheWebTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "MortysDLP.Tests.MarkOfTheWeb", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _filePath = Path.Combine(_tempDir, "test.exe");
        File.WriteAllText(_filePath, "inhalt");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* Best-Effort */ }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void TryRemove_VorhandeneKennzeichnung_WirdEntferntUndIstDanachWeg()
    {
        if (!TryTagWithZoneIdentifier())
            return; // Dateisystem ohne Datenstrom-Unterstützung - siehe Klassenkommentar.

        var result = MarkOfTheWeb.TryRemove(_filePath);

        Assert.Equal(MarkOfTheWebResult.Removed, result);
        Assert.False(HasZoneIdentifier());
    }

    /// <summary>Der Regelfall: Eine Datei ohne Kennzeichnung darf keinen Fehler auslösen -
    /// genau der Fall, den jeder Download über <see cref="Services.VerifiedDownload"/> heute
    /// erzeugt (am 2026-09-02 an echten, frisch heruntergeladenen Werkzeugen geprüft: keines
    /// trägt eine Internet-Kennzeichnung, weil ein reiner HttpClient-Download sie anders als ein
    /// Browser nie setzt).</summary>
    [Fact]
    public void TryRemove_KeineKennzeichnung_GiltAlsNichtVorhandenUndWirftNicht()
    {
        var result = MarkOfTheWeb.TryRemove(_filePath);

        Assert.Equal(MarkOfTheWebResult.NotPresent, result);
    }

    [Fact]
    public void TryRemove_DateiFehltGanz_WirftNicht()
    {
        string missingPath = Path.Combine(_tempDir, "existiert-nicht.exe");

        var exception = Record.Exception(() => MarkOfTheWeb.TryRemove(missingPath));

        Assert.Null(exception);
    }

    private bool TryTagWithZoneIdentifier()
    {
        try
        {
            File.WriteAllText(_filePath + ":Zone.Identifier", "[ZoneTransfer]\r\nZoneId=3\r\n");
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private bool HasZoneIdentifier()
    {
        try
        {
            File.ReadAllText(_filePath + ":Zone.Identifier");
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
