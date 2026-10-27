using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace MortysDLP.Tests;

/// <summary>
/// Hält die Oberfläche einheitlich: prüft die XAML- und Code-Dateien unter <c>Views/</c> gegen die
/// Regeln, nach denen sie angeglichen wurden (ein Knopfstil je Knopf, eine Balkenhöhe, eine
/// Symbolschrift, keine festen Farben, Namen für Symbolknöpfe). Liest die Quelldateien nur.
///
/// Ausnahmen stehen bewusst als Liste hier im Test: Wer eine neue braucht, muss sie sichtbar
/// begründen, statt dass eine Seite still auseinanderläuft.
/// </summary>
public class UiConventionTests
{
    // Eigene Vorlagen im Hauptfenster (Credits, Spende, Update-Banner) - werden in der
    // nächsten Gestaltungsrunde ersetzt und tragen bis dahin ihren eigenen Stil.
    private static readonly HashSet<string> UnstyledButtonsAllowed =
        ["btnCredits", "btnJokeDonate", "btnUpdateBanner", "btnDismissBanner"];

    // Feste Farben, die bis zur Gestaltungsrunde bleiben - je Datei genau dieser Wert, damit
    // eine neue feste Farbe in derselben Datei trotzdem auffällt: Spende-Herz, Banner-Hover und
    // Schatten des Info-Popups (Hauptfenster), Fehler-/Warnsymbole der beiden Meldungsdialoge.
    private static readonly HashSet<(string File, string Token)> FixedColorsAllowed =
    [
        ("MainWindow.xaml", "\"#e05c7a\""),
        ("MainWindow.xaml", "\"#28000000\""),
        ("MainWindow.xaml", "\"#3D000000\""),
        ("MainWindow.xaml", "\"#000000\""),
        ("ErrorDialog.xaml", "\"#22C42B1C\""),
        ("ErrorDialog.xaml", "\"#C42B1C\""),
        ("FluentMessageBox.xaml.cs", "Color.FromArgb"),
        ("FluentMessageBox.xaml.cs", "Color.FromRgb"),
    ];

    private static readonly string ViewsDir = Path.Combine(FindRepoRoot(), "Views");

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MortysDLP.csproj")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("MortysDLP.csproj nicht gefunden.");
    }

    private static string[] XamlFiles() => Directory.GetFiles(ViewsDir, "*.xaml");

    private static IEnumerable<(string File, string Attributes)> Buttons(string file) =>
        Regex.Matches(File.ReadAllText(file), @"<Button(?=[\s/>])([^>]*)>")
             .Select(m => (Path.GetFileName(file), m.Groups[1].Value));

    private static string? NameOf(string attributes) =>
        Regex.Match(attributes, @"(?<![.\w])(?:x:)?Name=""([^""]+)""") is { Success: true } m ? m.Groups[1].Value : null;

    [Fact]
    public void Knoepfe_TragenEinenDerDreiStile()
    {
        var verstoesse = XamlFiles().SelectMany(Buttons)
            .Where(b => !Regex.IsMatch(b.Attributes, @"Style=""\{StaticResource (Primary|Secondary|Icon)ButtonStyle\}"""))
            .Where(b => !UnstyledButtonsAllowed.Contains(NameOf(b.Attributes) ?? ""))
            .Select(b => $"{b.File}: {NameOf(b.Attributes) ?? Regex.Replace(b.Attributes.Trim(), @"\s+", " ")}")
            .ToList();

        Assert.True(verstoesse.Count == 0, "Knopf ohne Primary-/Secondary-/IconButtonStyle:\n" + string.Join("\n", verstoesse));
    }

    [Fact]
    public void Knoepfe_HabenKeineHandgesetztenMasse()
    {
        var verstoesse = XamlFiles().SelectMany(Buttons)
            .Where(b => Regex.IsMatch(b.Attributes, @"\s(Padding|Width|Height)="""))
            .Where(b => !UnstyledButtonsAllowed.Contains(NameOf(b.Attributes) ?? ""))
            .Select(b => $"{b.File}: {NameOf(b.Attributes) ?? "(ohne Namen)"}")
            .ToList();

        Assert.True(verstoesse.Count == 0, "Knopf mit Padding/Width/Height:\n" + string.Join("\n", verstoesse));
    }

    [Fact]
    public void Symbolknoepfe_HabenEinenNamenFuerBildschirmleser()
    {
        var verstoesse = new List<string>();
        foreach (var file in XamlFiles())
        {
            string codeBehind = File.Exists(file + ".cs") ? File.ReadAllText(file + ".cs") : "";
            foreach (var (name, attrs) in Buttons(file).Select(b => (NameOf(b.Attributes), b.Attributes)))
            {
                if (!attrs.Contains("IconButtonStyle", StringComparison.Ordinal))
                    continue;
                bool imXaml = attrs.Contains("AutomationProperties.Name", StringComparison.Ordinal);
                bool imCode = name is not null && Regex.IsMatch(codeBehind, $@"SetName\(\s*{Regex.Escape(name)}\s*,");
                if (!imXaml && !imCode)
                    verstoesse.Add($"{Path.GetFileName(file)}: {name ?? "(ohne Namen)"}");
            }
        }

        Assert.True(verstoesse.Count == 0, "Symbolknopf ohne AutomationProperties.Name:\n" + string.Join("\n", verstoesse));
    }

    [Fact]
    public void Fortschrittsbalken_NutzenDieGemeinsameHoehe()
    {
        var verstoesse = XamlFiles()
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"<ProgressBar(?=[\s/>])([^>]*)>")
                                  .Select(m => (File: Path.GetFileName(f), Attrs: m.Groups[1].Value)))
            .Where(p => !p.Attrs.Contains(@"Height=""{StaticResource ProgressBarHeight}""", StringComparison.Ordinal))
            .Select(p => $"{p.File}: {NameOf(p.Attrs) ?? "(ohne Namen)"}")
            .ToList();

        Assert.True(verstoesse.Count == 0, "ProgressBar ohne ProgressBarHeight:\n" + string.Join("\n", verstoesse));
    }

    [Fact]
    public void Views_NutzenDieSymbolschriftRessource()
    {
        var verstoesse = Directory.GetFiles(ViewsDir)
            .Where(f => f.EndsWith(".xaml", StringComparison.Ordinal) || f.EndsWith(".cs", StringComparison.Ordinal))
            .SelectMany(f => File.ReadLines(f).Select((line, i) => (File: Path.GetFileName(f), Line: i + 1, Text: line)))
            .Where(l => l.Text.Contains("Segoe MDL2 Assets", StringComparison.Ordinal) && !l.Text.TrimStart().StartsWith("//", StringComparison.Ordinal))
            .Select(l => $"{l.File}:{l.Line}")
            .ToList();

        Assert.True(verstoesse.Count == 0, "Segoe MDL2 Assets direkt statt IconFontFamily:\n" + string.Join("\n", verstoesse));
    }

    [Fact]
    public void Views_EnthaltenKeineFestenFarben()
    {
        const string xamlFarbe = @"""#[0-9A-Fa-f]{3,8}""";
        const string codeFarbe = @"Color\.FromA?Rgb|\b(?:Brushes|Colors)\.[A-Z]\w*|""#[0-9A-Fa-f]{3,8}""";

        var verstoesse = Directory.GetFiles(ViewsDir)
            .Where(f => f.EndsWith(".xaml", StringComparison.Ordinal) || f.EndsWith(".cs", StringComparison.Ordinal))
            .SelectMany(f => File.ReadLines(f).Select((line, i) => (File: Path.GetFileName(f), Line: i + 1, Text: line)))
            .Where(l => !l.Text.TrimStart().StartsWith("//", StringComparison.Ordinal))
            .SelectMany(l => Regex.Matches(l.Text, l.File.EndsWith(".cs", StringComparison.Ordinal) ? codeFarbe : xamlFarbe)
                                  .Select(m => (l.File, l.Line, Token: m.Value)))
            .Where(t => !FixedColorsAllowed.Contains((t.File, t.Token)))
            .Select(t => $"{t.File}:{t.Line} {t.Token}")
            .ToList();

        Assert.True(verstoesse.Count == 0, "Feste Farbe statt Theme-/Statusressource:\n" + string.Join("\n", verstoesse));
    }

    [Fact]
    public void Verweise_PerElementName_ZeigenAufVorhandeneElemente()
    {
        var verstoesse = new List<string>();
        foreach (var file in XamlFiles())
        {
            string xaml = File.ReadAllText(file);
            var namen = Regex.Matches(xaml, @"\b(?:x:)?Name=""([^""]+)""").Select(m => m.Groups[1].Value).ToHashSet();
            verstoesse.AddRange(Regex.Matches(xaml, @"ElementName=(\w+)")
                .Select(m => m.Groups[1].Value)
                .Where(n => !namen.Contains(n))
                .Select(n => $"{Path.GetFileName(file)}: {n}"));
        }

        Assert.True(verstoesse.Count == 0, "ElementName ohne passendes Element (scheitert erst zur Laufzeit, still):\n" + string.Join("\n", verstoesse));
    }
}
