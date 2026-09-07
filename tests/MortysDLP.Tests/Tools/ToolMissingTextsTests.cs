using MortysDLP.Helpers;
using MortysDLP.UITexte;
using System.IO;

namespace MortysDLP.Tests.Tools;

/// <summary>
/// Prüft die Textschlüssel rund um „ein erforderliches Werkzeug fehlt" (Sperr-Karte,
/// Werkzeuge-Seite, phasengenaue Fehlermeldung) in <b>beiden</b> Sprachen — Schlüssel als
/// Literale, damit der Test nicht dieselbe Konstante liest wie der Code.
/// </summary>
public class ToolMissingTextsTests : IDisposable
{
    private readonly string _tempLogDir;

    public ToolMissingTextsTests()
    {
        _tempLogDir = Path.Combine(
            Path.GetTempPath(), "MortysDLP.Tests.ToolMissingTexts", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempLogDir);
        Log.LogsDirectory = _tempLogDir;
    }

    public void Dispose()
    {
        Log.CloseForTests();
        try { Directory.Delete(_tempLogDir, recursive: true); } catch { /* Best-Effort */ }
        GC.SuppressFinalize(this);
    }

    private static readonly string[] RequiredKeys =
    [
        "ToolMissingNotice.Title",
        "ToolMissingNotice.Body",
        "ToolMissingNotice.Button",
        "ToolsPage.Badge.Required",
        "ToolsPage.Row.UsedFor",
        "ToolsPage.Uninstall.Consequence",
        "DownloadPage.Status.ErrorConverting",
        "DownloadPage.Status.ErrorToolMissing",
        "BatchDownloadPage.Status.ErrorConverting",
        "BatchDownloadPage.Status.ErrorToolMissing",
    ];

    // Erwartete Zahl der {n}-Platzhalter je Schlüssel.
    private static readonly Dictionary<string, int> ExpectedPlaceholders = new()
    {
        ["ToolMissingNotice.Body"] = 2,
        ["ToolsPage.Row.UsedFor"] = 1,
        ["ToolsPage.Uninstall.Consequence"] = 2,
    };

    [Theory]
    [InlineData("de")]
    [InlineData("en")]
    public void AlleSchluessel_SindVorhanden(string language)
    {
        UITextDictionary.SetLanguage(language);

        var missing = RequiredKeys.Where(k => UITextDictionary.Get(k) == $"[{k}]").ToList();

        Assert.Empty(missing);
    }

    [Theory]
    [InlineData("de")]
    [InlineData("en")]
    public void PlatzhalterZahl_StimmtInBeidenSprachen(string language)
    {
        UITextDictionary.SetLanguage(language);

        foreach (var (key, expected) in ExpectedPlaceholders)
            Assert.Equal(expected, CountPlaceholders(UITextDictionary.Get(key)));
    }

    private static int CountPlaceholders(string text)
    {
        int count = 0;
        for (int index = 0; index < 4; index++)
        {
            if (text.Contains($"{{{index}}}", StringComparison.Ordinal))
                count++;
        }

        return count;
    }
}
