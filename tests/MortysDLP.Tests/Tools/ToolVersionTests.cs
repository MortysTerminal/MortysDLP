using MortysDLP.Models;

namespace MortysDLP.Tests.Tools;

/// <summary>
/// Prüft <see cref="ToolVersion"/> — reine Logik, kein Netz, keine Dateien.
///
/// <para>Der wichtigste Test dieser Klasse ist
/// <see cref="Ffmpeg_BuildVarianteGiltNichtAlsAelterAlsDieReineNummer"/>: Er sichert gegen ein
/// dauerhaftes, nie verschwindendes Update-Angebot für ffmpeg ab. Der Fall entsteht dadurch,
/// dass <see cref="AppVersion"/> die ffmpeg-Version syntaktisch akzeptiert und semantisch falsch
/// einordnet — deshalb steht dieser Vergleich hier direkt daneben. Wer
/// <see cref="ToolVersion"/> später vereinfacht, muss an diesem Test vorbei.</para>
/// </summary>
public class ToolVersionTests
{
    private const string FfmpegLocal = "7.1-essentials_build-www.gyan.dev";
    private const string FfmpegRemote = "7.1";

    [Fact]
    public void OhneWert_IstUnbekanntUndBeantwortetKeineFrage()
    {
        var unknown = ToolVersion.Parse(null);
        var known = ToolVersion.Parse("2026.08.19");

        Assert.False(unknown.HasValue);
        Assert.False(unknown.HasNumericCore);
        Assert.False(unknown.IsOrdering);
        Assert.Equal("unbekannt", unknown.ToString());

        Assert.Null(known.IsNewerThan(unknown));
        Assert.Null(unknown.IsNewerThan(known));
        Assert.False(known.IsSameRelease(unknown));
        Assert.False(unknown.IsSameRelease(known));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void LeereEingabe_IstUnbekannt(string text)
    {
        Assert.False(ToolVersion.Parse(text).HasValue);
    }

    [Theory]
    [InlineData("2026.08.19")]
    [InlineData("2026.08.19.232303")]
    [InlineData("7.1")]
    [InlineData("1.55.2")]
    [InlineData("v1.7.4")]
    public void ReineZahlenfolge_IstOrdnend(string text)
    {
        var version = ToolVersion.Parse(text);

        Assert.True(version.HasValue);
        Assert.True(version.HasNumericCore);
        Assert.True(version.IsOrdering);
        Assert.Null(version.Tag);
    }

    [Fact]
    public void FuehrendesV_WirdEntfernt_AberNurVorEinerZiffer()
    {
        Assert.Equal("1.7.4", ToolVersion.Parse("v1.7.4").Raw);
        Assert.Equal("version-unbekannt", ToolVersion.Parse("version-unbekannt").Raw);
    }

    [Fact]
    public void FfmpegVersion_HatZahlenkernAberIstNichtOrdnend()
    {
        var version = ToolVersion.Parse(FfmpegLocal);

        Assert.True(version.HasValue);
        Assert.True(version.HasNumericCore);
        Assert.False(version.IsOrdering);
        Assert.Equal("-essentials_build-www.gyan.dev", version.Tag);
    }

    [Fact]
    public void OhneZahlenAmAnfang_HatKeinenZahlenkern()
    {
        // So sehen die Tags der BtbN-Builds aus - ordnen lässt sich daran nichts.
        var version = ToolVersion.Parse("autobuild-2026-08-19-12-55");

        Assert.True(version.HasValue);
        Assert.False(version.HasNumericCore);
        Assert.False(version.IsOrdering);
    }

    /// <summary>
    /// Der Absicherungstest gegen das dauerhafte ffmpeg-Update-Angebot. Zum Vergleich steht
    /// dieselbe Frage an <see cref="AppVersion"/> daneben — die dort <b>falsch</b> beantwortet
    /// wird. Der Test hält beides fest, damit klar bleibt, warum es zwei Versionstypen gibt.
    /// </summary>
    [Fact]
    public void Ffmpeg_BuildVarianteGiltNichtAlsAelterAlsDieReineNummer()
    {
        var local = ToolVersion.Parse(FfmpegLocal);
        var remote = ToolVersion.Parse(FfmpegRemote);

        Assert.False(remote.IsNewerThan(local));
        Assert.False(local.IsNewerThan(remote));
        Assert.True(local.IsSameRelease(remote));

        // Genau diese Einordnung ist der Grund für ToolVersion: AppVersion liest den Teil hinter
        // dem Bindestrich als Vorab-Suffix und hält die installierte Version deshalb für kleiner.
        Assert.True(AppVersion.TryParse(FfmpegLocal, out var appLocal));
        Assert.True(AppVersion.TryParse(FfmpegRemote, out var appRemote));
        Assert.True(appLocal < appRemote);
    }

    [Fact]
    public void Ffmpeg_EchteNeuereAusgabe_GiltAlsUnterschiedlichAberNichtAlsNachweisbarNeuer()
    {
        var local = ToolVersion.Parse(FfmpegLocal);
        var remote = ToolVersion.Parse("7.2");

        Assert.False(local.IsSameRelease(remote));

        // "Neuer als" bleibt unbeantwortet, weil die installierte Angabe nicht ordnend ist -
        // ein Angebot ist damit erlaubt, ein automatisches Update nicht.
        Assert.Null(remote.IsNewerThan(local));
    }

    [Fact]
    public void OrdnendeVersionen_WerdenVerglichen()
    {
        var older = ToolVersion.Parse("2026.08.19");
        var newer = ToolVersion.Parse("2026.08.20");

        Assert.True(newer.IsNewerThan(older));
        Assert.False(older.IsNewerThan(newer));
        Assert.False(older.IsSameRelease(newer));
    }

    [Fact]
    public void NightlyMitViertemSegment_IstNeuerAlsDerRelease()
    {
        var nightly = ToolVersion.Parse("2026.08.19.232303");
        var release = ToolVersion.Parse("2026.08.19");

        Assert.True(nightly.IsNewerThan(release));
        Assert.False(release.IsNewerThan(nightly));
    }

    [Fact]
    public void FehlendeSegmente_ZaehlenAlsNull()
    {
        var shortForm = ToolVersion.Parse("7.1");
        var longForm = ToolVersion.Parse("7.1.0");

        Assert.True(shortForm.IsSameRelease(longForm));
        Assert.False(shortForm.IsNewerThan(longForm));
        Assert.False(longForm.IsNewerThan(shortForm));
    }

    [Fact]
    public void OhneZahlenkerne_WirdNurDerTextVerglichen()
    {
        var a = ToolVersion.Parse("autobuild-alpha");
        var b = ToolVersion.Parse("AUTOBUILD-ALPHA");
        var c = ToolVersion.Parse("autobuild-beta");

        Assert.True(a.IsSameRelease(b));
        Assert.False(a.IsSameRelease(c));
        Assert.Null(a.IsNewerThan(c));
    }

    [Fact]
    public void EinseitigerZahlenkern_IstNichtVergleichbar()
    {
        var withCore = ToolVersion.Parse("7.1");
        var withoutCore = ToolVersion.Parse("autobuild-2026");

        Assert.Null(withCore.CompareCore(withoutCore));
        Assert.Null(withCore.IsNewerThan(withoutCore));
        Assert.False(withCore.IsSameRelease(withoutCore));
    }

    [Fact]
    public void MehrAlsSechsSegmente_GeltenNichtMehrAlsOrdnend()
    {
        var version = ToolVersion.Parse("1.2.3.4.5.6.7");

        Assert.True(version.HasNumericCore);
        Assert.False(version.IsOrdering);
        Assert.Equal(".7", version.Tag);
    }

    [Fact]
    public void ZuGrosseZahl_BeendetDenZahlenkernStattZuWerfen()
    {
        var version = ToolVersion.Parse("99999999999.1");

        Assert.True(version.HasValue);
        Assert.False(version.HasNumericCore);
        Assert.Equal("99999999999.1", version.Raw);
    }

    [Fact]
    public void GleichheitIstZeichengenau_NichtAusgabengleich()
    {
        var local = ToolVersion.Parse(FfmpegLocal);
        var remote = ToolVersion.Parse(FfmpegRemote);

        Assert.NotEqual(local, remote);
        Assert.True(local.IsSameRelease(remote));

        Assert.Equal(ToolVersion.Parse("7.1"), ToolVersion.Parse(" 7.1 "));
        Assert.Equal(ToolVersion.Parse("7.1").GetHashCode(), ToolVersion.Parse("v7.1").GetHashCode());
    }
}
