using MortysDLP.Services;

namespace MortysDLP.Tests;

/// <summary>
/// Prüft <see cref="YtDlpProgressParser"/> gegen echte, mit der installierten yt-dlp-Version
/// (2026.08.19) aufgezeichnete <c>--progress-template</c>-Zeilen — nicht gegen geratene
/// Beispielwerte.
/// </summary>
public class YtDlpProgressParserTests
{
    [Fact]
    public void TryParse_EchteZeileMitBekannterGesamtgroesse_LiestAnteilEtaUndGeschwindigkeit()
    {
        // Echte Zeile aus einem realen Download (yt-dlp 2026.08.19): 58.1 %, ETA 0 s.
        string line = "MDLPPROGRESS|130048|223779|NA|0|9663752.508539438|downloading";

        bool ok = YtDlpProgressParser.TryParse(line, out var progress);

        Assert.True(ok);
        Assert.NotNull(progress.Fraction);
        Assert.Equal(130048.0 / 223779.0, progress.Fraction!.Value, precision: 6);
        Assert.Equal(TimeSpan.Zero, progress.Eta);
        Assert.Equal(9663752.508539438, progress.SpeedBytesPerSecond);
        Assert.Equal(130048L, progress.DownloadedBytes);
        Assert.Equal(223779L - 130048L, progress.RemainingBytes);
    }

    [Fact]
    public void TryParse_ZeileMitEtaGroesserNull_LiestRestzeitKorrekt()
    {
        // Echte Zeile aus demselben Download, kurz nach dem Start: ETA 1 s.
        string line = "MDLPPROGRESS|7168|223779|NA|1|175104.6679712981|downloading";

        bool ok = YtDlpProgressParser.TryParse(line, out var progress);

        Assert.True(ok);
        Assert.Equal(TimeSpan.FromSeconds(1), progress.Eta);
    }

    [Fact]
    public void TryParse_AbgeschlosseneZeile_EtaUndGeschwindigkeitKoennenNAsein()
    {
        // Echte Zeile beim Abschluss eines Downloads: ETA und Speed als "NA".
        string line = "MDLPPROGRESS|223779|223779|NA|NA|NA|finished";

        bool ok = YtDlpProgressParser.TryParse(line, out var progress);

        Assert.True(ok);
        Assert.Equal(1.0, progress.Fraction);
        Assert.Null(progress.Eta);
        Assert.Null(progress.SpeedBytesPerSecond);
        Assert.Equal(223779L, progress.DownloadedBytes);
        Assert.Equal(0L, progress.RemainingBytes);
    }

    [Fact]
    public void TryParse_GesamtgroesseUnbekannt_NutztSchaetzungAlsRueckfall()
    {
        string line = "MDLPPROGRESS|500|NA|1000|5|100|downloading";

        bool ok = YtDlpProgressParser.TryParse(line, out var progress);

        Assert.True(ok);
        Assert.Equal(0.5, progress.Fraction);
    }

    [Fact]
    public void TryParse_WederGesamtgroesseNochSchaetzungBekannt_FractionIstNull()
    {
        string line = "MDLPPROGRESS|500|NA|NA|5|100|downloading";

        bool ok = YtDlpProgressParser.TryParse(line, out var progress);

        Assert.True(ok);
        Assert.Null(progress.Fraction);
        Assert.Equal(100.0, progress.SpeedBytesPerSecond);
        Assert.Equal(500L, progress.DownloadedBytes);
        Assert.Null(progress.RemainingBytes);
    }

    [Fact]
    public void TryParse_AndereYtDlpZeile_LiefertFalseOhneAusnahme()
    {
        bool ok = YtDlpProgressParser.TryParse(
            "[download] Destination: C:\\Downloads\\Video.mp4", out var progress);

        Assert.False(ok);
        Assert.Equal(default, progress);
    }

    [Fact]
    public void TryParse_MergerZeile_LiefertFalseOhneAusnahme()
    {
        bool ok = YtDlpProgressParser.TryParse(
            "[Merger] Merging formats into \"C:\\Downloads\\Video.webm\"", out _);

        Assert.False(ok);
    }

    [Fact]
    public void TryParse_LeereZeile_LiefertFalseOhneAusnahme()
    {
        bool ok = YtDlpProgressParser.TryParse("", out _);

        Assert.False(ok);
    }

    [Fact]
    public void TryParse_ZuWenigeFelder_LiefertFalseOhneAusnahme()
    {
        bool ok = YtDlpProgressParser.TryParse("MDLPPROGRESS|1|2|3", out _);

        Assert.False(ok);
    }

    [Fact]
    public void TryParse_ZuVieleFelder_LiefertFalseOhneAusnahme()
    {
        bool ok = YtDlpProgressParser.TryParse("MDLPPROGRESS|1|2|3|4|5|6|7", out _);

        Assert.False(ok);
    }

    [Fact]
    public void TryParse_UngueltigeZahlInEinemFeld_LiefertTrotzdemErgebnisOhneDiesesFeld()
    {
        // Ein einzelnes kaputtes Feld darf nicht die ganze Zeile verwerfen - die übrigen
        // Werte bleiben brauchbar.
        string line = "MDLPPROGRESS|500|1000|NA|kaputt|100|downloading";

        bool ok = YtDlpProgressParser.TryParse(line, out var progress);

        Assert.True(ok);
        Assert.Equal(0.5, progress.Fraction);
        Assert.Null(progress.Eta);
        Assert.Equal(100.0, progress.SpeedBytesPerSecond);
    }

    [Fact]
    public void TryParse_FractionWirdAufEinsBegrenzt()
    {
        // downloaded > total kann bei Rundungsungenauigkeiten der Schätzung vorkommen.
        string line = "MDLPPROGRESS|1100|1000|NA|0|100|downloading";

        bool ok = YtDlpProgressParser.TryParse(line, out var progress);

        Assert.True(ok);
        Assert.Equal(1.0, progress.Fraction);
    }

    [Fact]
    public void Template_EnthaeltKeineNutzerdatenfelder()
    {
        // Der Hinweis in der Aufgabe: keine Titel-/Dateinamen-Platzhalter in der Vorlage,
        // damit kein Trennzeichen aus einem Videotitel das Parsen durcheinanderbringen kann.
        Assert.DoesNotContain("%(title)", YtDlpProgressParser.Template);
        Assert.DoesNotContain("%(filename)", YtDlpProgressParser.Template);
    }
}
