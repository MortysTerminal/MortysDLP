using MortysDLP.Services.Tools;
using System.IO;

namespace MortysDLP.Tests.Tools;

/// <summary>
/// Prüft <see cref="WhisperModelStore.GetState"/> als reine Funktion: nicht vorhanden /
/// unvollständig / vollständig, über die Größentoleranz. Laden und Löschen brauchen einen
/// echten Netzzugriff bzw. sind triviale Dateioperationen und gehören deshalb in den
/// Handtestplan, nicht hierher.
/// </summary>
public class WhisperModelStoreTests : IDisposable
{
    private const long ExpectedSize = 100_000;
    private readonly string _tempDir;
    private readonly WhisperModelEntry _model;

    public WhisperModelStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "MortysDLP.Tests.WhisperModelStore", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        _model = new WhisperModelEntry(
            "test-model", "ggml-test.bin",
            "Test", "Test", "Test", "Test",
            "https://huggingface.co/x/y/resolve/main/ggml-test.bin",
            "https://hf-mirror.com/x/y/resolve/main/ggml-test.bin",
            ExpectedSize, Sha256: null);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* Best-Effort */ }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void DateiFehlt_GiltAlsNichtVorhanden()
    {
        Assert.Equal(WhisperModelState.NotPresent, WhisperModelStore.GetState(_model, _tempDir));
    }

    [Fact]
    public void LeereDatei_GiltAlsNichtVorhanden()
    {
        WriteFile(0);
        Assert.Equal(WhisperModelState.NotPresent, WhisperModelStore.GetState(_model, _tempDir));
    }

    [Fact]
    public void GenauErwarteteGroesse_GiltAlsVollstaendig()
    {
        WriteFile(ExpectedSize);
        Assert.Equal(WhisperModelState.Complete, WhisperModelStore.GetState(_model, _tempDir));
    }

    /// <summary>Die Toleranz ist großzügig genug für unterschiedliche Ablagen desselben
    /// Modells — knapp innerhalb von ±1 % zählt als vollständig.</summary>
    [Theory]
    [InlineData(ExpectedSize - 1_000)] // -1,0 %, genau an der Grenze
    [InlineData(ExpectedSize + 1_000)] // +1,0 %, genau an der Grenze
    [InlineData(ExpectedSize - 500)]
    [InlineData(ExpectedSize + 500)]
    public void GroesseInnerhalbDerToleranz_GiltAlsVollstaendig(long actualSize)
    {
        WriteFile(actualSize);
        Assert.Equal(WhisperModelState.Complete, WhisperModelStore.GetState(_model, _tempDir));
    }

    /// <summary>Der Fall, um den es in dieser Aufgabe geht: ein bei 40 % abgebrochener Download
    /// hinterlässt eine existierende, aber unvollständige Datei.</summary>
    [Theory]
    [InlineData(ExpectedSize - 1_001)] // knapp außerhalb der -1-%-Grenze
    [InlineData(ExpectedSize + 1_001)] // knapp außerhalb der +1-%-Grenze
    [InlineData(40_000)]               // 40 % - der Beispielfall aus der Aufgabenbeschreibung
    [InlineData(1)]
    public void GroesseAusserhalbDerToleranz_GiltAlsUnvollstaendig(long actualSize)
    {
        WriteFile(actualSize);
        Assert.Equal(WhisperModelState.Incomplete, WhisperModelStore.GetState(_model, _tempDir));
    }

    [Fact]
    public void ZuGrosseDatei_GiltEbenfallsAlsUnvollstaendig()
    {
        // Nicht nur ein abgebrochener Download ist unvollständig - eine zu große Datei (z. B.
        // aus einer fehlerhaften Ablage) ist ebenso wenig das erwartete Modell.
        WriteFile(ExpectedSize * 2);
        Assert.Equal(WhisperModelState.Incomplete, WhisperModelStore.GetState(_model, _tempDir));
    }

    private void WriteFile(long size)
    {
        string path = Path.Combine(_tempDir, _model.FileName);
        using var stream = new FileStream(path, FileMode.Create);
        stream.SetLength(size);
    }
}
