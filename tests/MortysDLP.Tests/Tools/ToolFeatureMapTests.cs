using MortysDLP.Helpers;
using MortysDLP.Services.Tools;
using MortysDLP.UITexte;
using System.IO;

namespace MortysDLP.Tests.Tools;

/// <summary>
/// Prüft <see cref="ToolFeatureMap"/> — die zentrale Zuordnung Werkzeug → Funktionen, die von
/// der Werkzeuge-Seite, der Sperr-Karte und dem Startablauf gemeinsam genutzt wird. Ein
/// fehlender Textschlüssel würde dort als <c>[Feature.Xyz]</c> in der Oberfläche auftauchen.
/// </summary>
public class ToolFeatureMapTests : IDisposable
{
    private readonly string _tempLogDir;

    public ToolFeatureMapTests()
    {
        // SetLanguage protokolliert - ohne Umleitung landen Testzeilen im echten Protokoll.
        _tempLogDir = Path.Combine(
            Path.GetTempPath(), "MortysDLP.Tests.ToolFeatureMap", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempLogDir);
        Log.LogsDirectory = _tempLogDir;
    }

    public void Dispose()
    {
        Log.CloseForTests();
        try { Directory.Delete(_tempLogDir, recursive: true); } catch { /* Best-Effort */ }
        GC.SuppressFinalize(this);
    }

    private static readonly string[] AllToolIds =
        ["yt-dlp", "ffmpeg", "whisper", "twitch-downloader"];

    [Theory]
    [InlineData("yt-dlp")]
    [InlineData("ffmpeg")]
    [InlineData("whisper")]
    [InlineData("twitch-downloader")]
    public void FeatureKeys_JedesWerkzeug_HatMindestensEineFunktion(string toolId)
    {
        Assert.NotEmpty(ToolFeatureMap.FeatureKeys(toolId));
    }

    [Fact]
    public void FeatureKeys_UnbekanntesWerkzeug_IstLeer()
    {
        Assert.Empty(ToolFeatureMap.FeatureKeys("gibt-es-nicht"));
    }

    [Theory]
    [InlineData("de")]
    [InlineData("en")]
    public void AlleFunktionsschluessel_SindInBeidenSprachenVorhanden(string language)
    {
        UITextDictionary.SetLanguage(language);

        var missing = new List<string>();
        foreach (string toolId in AllToolIds)
        {
            foreach (string key in ToolFeatureMap.FeatureKeys(toolId))
            {
                if (UITextDictionary.Get(key) == $"[{key}]")
                    missing.Add(key);
            }
        }

        Assert.Empty(missing);
    }

    [Theory]
    [InlineData("yt-dlp")]
    [InlineData("ffmpeg")]
    [InlineData("whisper")]
    [InlineData("twitch-downloader")]
    public void DisplayName_IstNichtLeer(string toolId)
    {
        Assert.False(string.IsNullOrWhiteSpace(ToolFeatureMap.DisplayName(toolId)));
    }

    [Fact]
    public void Describe_FuehrtDieUebersetztenFunktionenAlsListe()
    {
        UITextDictionary.SetLanguage("de");

        string described = ToolFeatureMap.Describe("yt-dlp", UITextDictionary.Get);

        Assert.Contains(UITextDictionary.Get("Feature.Download"), described, StringComparison.Ordinal);
        Assert.Contains(", ", described, StringComparison.Ordinal);
        Assert.DoesNotContain("[Feature", described, StringComparison.Ordinal);
    }
}
