using MortysDLP.Helpers;

namespace MortysDLP.Tests;

/// <summary>
/// Prüft die HTML→Markdown-Vorstufe von <see cref="MarkdownHelper"/>. GitHub-Release-Notizen
/// enthalten regelmäßig HTML (von Hand, aus einem WYSIWYG-Editor, oder GitHubs
/// auto-generierte Blöcke); vorher zeigte das Changelog-Fenster die Tags wörtlich an.
/// </summary>
public class MarkdownHelperTests
{
    [Fact]
    public void NormalizeHtml_ReinesMarkdown_BleibtUnveraendert()
    {
        const string md = "## Titel\n\n- Punkt eins\n- Punkt zwei\n\n**fett** und `code`.";
        Assert.Equal(md, MarkdownHelper.NormalizeHtml(md));
    }

    [Fact]
    public void NormalizeHtml_Ueberschriften_WerdenZuRauten()
    {
        string result = MarkdownHelper.NormalizeHtml("<h2>Großes Release</h2><h3>Sicherheit</h3>");
        Assert.Contains("## Großes Release", result, StringComparison.Ordinal);
        Assert.Contains("### Sicherheit", result, StringComparison.Ordinal);
        Assert.DoesNotContain("<h", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NormalizeHtml_Liste_WirdZuBindestrichen()
    {
        string result = MarkdownHelper.NormalizeHtml(
            "<ul>\n<li>Erster Punkt</li>\n<li>Zweiter Punkt</li>\n</ul>");
        var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Contains("- Erster Punkt", lines);
        Assert.Contains("- Zweiter Punkt", lines);
        Assert.DoesNotContain("<ul>", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<li>", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NormalizeHtml_InlineAuszeichnung_WirdZuMarkdown()
    {
        string result = MarkdownHelper.NormalizeHtml(
            "<p>Ein <strong>wichtiger</strong> und <em>kursiver</em> <code>Wert</code>.</p>");
        Assert.Contains("**wichtiger**", result, StringComparison.Ordinal);
        Assert.Contains("*kursiver*", result, StringComparison.Ordinal);
        Assert.Contains("`Wert`", result, StringComparison.Ordinal);
    }

    [Fact]
    public void NormalizeHtml_Link_BehaeltNurDenText()
    {
        string result = MarkdownHelper.NormalizeHtml(
            "Siehe <a href=\"https://example.com/x\">die Doku</a> dazu.");
        Assert.Contains("die Doku", result, StringComparison.Ordinal);
        Assert.DoesNotContain("href", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("example.com", result, StringComparison.Ordinal);
    }

    [Fact]
    public void NormalizeHtml_BrUndHr_WerdenUmbruchUndTrennlinie()
    {
        string result = MarkdownHelper.NormalizeHtml("Zeile eins<br>Zeile zwei<hr/>Abschnitt");
        Assert.Contains("Zeile eins\nZeile zwei", result, StringComparison.Ordinal);
        Assert.Contains("---", result, StringComparison.Ordinal);
    }

    [Fact]
    public void NormalizeHtml_UnbekannteTags_WerdenEntfernt()
    {
        string result = MarkdownHelper.NormalizeHtml(
            "<details><summary>Mehr</summary><div class=\"x\">Inhalt</div></details>");
        Assert.Contains("Mehr", result, StringComparison.Ordinal);
        Assert.Contains("Inhalt", result, StringComparison.Ordinal);
        Assert.DoesNotContain("<", result, StringComparison.Ordinal);
    }

    [Fact]
    public void NormalizeHtml_Entities_WerdenAufgeloest()
    {
        string result = MarkdownHelper.NormalizeHtml("<p>A &amp; B &quot;C&quot; &#39;D&#39;</p>");
        Assert.Contains("A & B \"C\" 'D'", result, StringComparison.Ordinal);
    }

    [Fact]
    public void NormalizeHtml_KeineUeberschuessigenLeerzeilen()
    {
        string result = MarkdownHelper.NormalizeHtml("<p>Eins</p><p>Zwei</p><p>Drei</p>");
        Assert.DoesNotContain("\n\n\n", result, StringComparison.Ordinal);
    }

    [Fact]
    public void ToFlowDocument_HtmlEingabe_ErzeugtBloeckeUndKeineWoertlichenTags()
    {
        var doc = MarkdownHelper.ToFlowDocument(
            "<h2>Neu</h2><ul><li>Ein Punkt</li></ul><p>Ein <strong>Absatz</strong>.</p>");

        Assert.NotEmpty(doc.Blocks);
        string text = new System.Windows.Documents.TextRange(
            doc.ContentStart, doc.ContentEnd).Text;
        Assert.DoesNotContain("<h2>", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<li>", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ein Punkt", text, StringComparison.Ordinal);
    }
}
