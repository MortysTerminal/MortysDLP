using MortysDLP.Helpers;
using MortysDLP.UITexte;
using System.IO;

namespace MortysDLP.Tests.Tools;

/// <summary>
/// Prüft, dass die Textschlüssel des Werkzeug-Startablaufs in <b>beiden</b> Sprachen vorhanden
/// sind. <see cref="UITextDictionary.Get"/> liefert bei einem unbekannten Schlüssel
/// <c>[Schlüssel]</c> zurück — sichtbar, aber ohne Fehler, und damit genau die Art von Fehler, die
/// beim Bauen nicht auffällt und erst im laufenden Programm als eckige Klammer erscheint.
///
/// <para>Die Schlüssel werden hier bewusst als Literale wiederholt und nicht aus dem Produktivcode
/// gezogen: Ein Test, der dieselbe Konstante liest wie der Code, prüft nichts.</para>
/// </summary>
public class StartupToolTextsTests : IDisposable
{
    private readonly string _tempLogDir;

    public StartupToolTextsTests()
    {
        // SetLanguage protokolliert - ohne Umleitung landen Testzeilen im echten Protokoll.
        _tempLogDir = Path.Combine(
            Path.GetTempPath(), "MortysDLP.Tests.StartupToolTexts", Guid.NewGuid().ToString("N"));
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
        "StartupWindow.Status.CheckingYtDlp",
        "StartupWindow.Status.CheckingYtDlpVersion",
        "StartupWindow.Status.YtDlpNotFound",
        "StartupWindow.Status.CheckingFfmpeg",
        "StartupWindow.Status.CheckingFfmpegVersion",
        "StartupWindow.Status.FfmpegNotFound",
        "StartupWindow.Status.CheckingTool",
        "StartupWindow.Status.CheckingToolVersion",
        "StartupWindow.Status.ToolNotFound",
        "StartupWindow.Status.Downloading",
        "StartupWindow.Status.Extracting",
        "StartupWindow.Status.Replacing",
        "StartupWindow.Status.Verifying",
        "StartupWindow.Status.DownloadCanceled",
        "StartupWindow.Status.DownloadFailed",
        "StartupWindow.YtDlp.Message",
        "StartupWindow.YtDlp.Question",
        "StartupWindow.YtDlp.Required",
        "StartupWindow.Ffmpeg.Message",
        "StartupWindow.Ffmpeg.Question",
        "StartupWindow.Ffmpeg.Required",
        "StartupWindow.Tool.MissingTitle",
        "StartupWindow.Tool.BrokenTitle",
        "StartupWindow.Tool.BrokenMessage",
        "StartupWindow.Tool.BrokenQuestion",
        "StartupWindow.Tool.InstallSuccess",
        "StartupWindow.Tool.InstallFailed",
        "StartupWindow.ToolUpdate.RolledBack",
        "StartupWindow.Title.Error",
        "StartupWindow.Title.DownloadComplete",
        "StartupWindow.Error.ToolUpdate",
    ];

    [Theory]
    [InlineData("de")]
    [InlineData("en")]
    public void AlleSchluesselDesWerkzeugAblaufs_SindVorhanden(string language)
    {
        UITextDictionary.SetLanguage(language);

        var missing = new List<string>();
        foreach (string key in RequiredKeys)
        {
            if (UITextDictionary.Get(key) == $"[{key}]")
                missing.Add(key);
        }

        Assert.Empty(missing);
    }

    /// <summary>Die Platzhalter müssen in beiden Sprachen dieselbe Anzahl haben — sonst wirft
    /// <c>string.Format</c> in genau einer Sprache, und das fällt beim Entwickeln in der anderen
    /// nie auf.</summary>
    [Fact]
    public void PlatzhalterZahl_StimmtZwischenDenSprachen()
    {
        var german = new Dictionary<string, int>();

        UITextDictionary.SetLanguage("de");
        foreach (string key in RequiredKeys)
            german[key] = CountPlaceholders(UITextDictionary.Get(key));

        UITextDictionary.SetLanguage("en");
        foreach (string key in RequiredKeys)
            Assert.Equal(german[key], CountPlaceholders(UITextDictionary.Get(key)));
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
