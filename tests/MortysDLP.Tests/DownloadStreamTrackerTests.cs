using MortysDLP.Services;

namespace MortysDLP.Tests;

/// <summary>
/// Prüft <see cref="DownloadStreamTracker"/> — die Unterscheidung „neuer Stream" vs. „derselbe
/// Stream wird fortgesetzt" aus den <c>[download] Destination: …</c>-Zeilen von yt-dlp.
///
/// <para>Die Zeilen in diesen Tests sind keine erfundenen Beispiele: Sie entsprechen der real
/// aufgezeichneten Ausgabe von yt-dlp bei einem Neustart mit <c>--continue</c> gegen eine
/// bereits halb geladene Datei — dort folgt auf <c>[download] Resuming download at byte N</c>
/// erneut eine <c>Destination</c>-Zeile mit demselben Ziel.</para>
/// </summary>
public class DownloadStreamTrackerTests
{
    private const string VideoStream = @"C:\Downloads\Titel_qbest_mp4_abc123.f137.mp4";
    private const string AudioStream = @"C:\Downloads\Titel_qbest_mp4_abc123.f140.m4a";

    [Fact]
    public void StreamIndex_VorDerErstenZieldatei_IstMinusEins()
    {
        var tracker = new DownloadStreamTracker();

        Assert.Equal(-1, tracker.StreamIndex);
    }

    [Fact]
    public void RegisterDestination_ErsteZieldatei_BeginntBeiNull()
    {
        var tracker = new DownloadStreamTracker();

        bool istNeu = tracker.RegisterDestination(VideoStream);

        Assert.True(istNeu);
        Assert.Equal(0, tracker.StreamIndex);
    }

    [Fact]
    public void RegisterDestination_ZweiteAndereZieldatei_ZaehltWeiter()
    {
        var tracker = new DownloadStreamTracker();
        tracker.RegisterDestination(VideoStream);

        bool istNeu = tracker.RegisterDestination(AudioStream);

        Assert.True(istNeu);
        Assert.Equal(1, tracker.StreamIndex);
    }

    [Fact]
    public void RegisterDestination_SelbeZieldateiErneut_ZaehltNichtWeiter()
    {
        // Der eigentliche Fehlerfall: Bandbreitenwechsel mitten im Video-Stream. yt-dlp startet
        // mit --continue neu und meldet dieselbe Zieldatei ein zweites Mal. Würde hier
        // weitergezählt, spränge der phasengewichtete Balken in den Bereich des nächsten
        // Streams - und beim echten Streamwechsel wieder zurück.
        var tracker = new DownloadStreamTracker();
        tracker.RegisterDestination(VideoStream);

        bool istNeu = tracker.RegisterDestination(VideoStream);

        Assert.False(istNeu);
        Assert.Equal(0, tracker.StreamIndex);
    }

    [Fact]
    public void RegisterDestination_MehrereNeustartsHintereinander_ZaehlenNichtWeiter()
    {
        var tracker = new DownloadStreamTracker();
        tracker.RegisterDestination(VideoStream);

        tracker.RegisterDestination(VideoStream);
        tracker.RegisterDestination(VideoStream);

        Assert.Equal(0, tracker.StreamIndex);
    }

    [Fact]
    public void RegisterDestination_NeustartVorDemStreamwechsel_ZaehltDenWechselTrotzdem()
    {
        // Wichtig: Der Neustart darf den echten, danach folgenden Streamwechsel nicht
        // verschlucken - sonst liefe der Audio-Stream im Bereich des Video-Streams.
        var tracker = new DownloadStreamTracker();
        tracker.RegisterDestination(VideoStream);
        tracker.RegisterDestination(VideoStream); // Neustart wegen Limitwechsel

        bool istNeu = tracker.RegisterDestination(AudioStream);

        Assert.True(istNeu);
        Assert.Equal(1, tracker.StreamIndex);
    }

    [Fact]
    public void RegisterDestination_GleicherPfadAndereGrossschreibung_GiltAlsDerselbeStream()
    {
        // Windows-Pfade sind nicht schreibungsempfindlich; ein Unterschied allein in der
        // Groß-/Kleinschreibung ist kein neuer Stream.
        var tracker = new DownloadStreamTracker();
        tracker.RegisterDestination(VideoStream);

        bool istNeu = tracker.RegisterDestination(VideoStream.ToUpperInvariant());

        Assert.False(istNeu);
        Assert.Equal(0, tracker.StreamIndex);
    }

    [Fact]
    public void Reset_NeuesVideo_BeginntDieZaehlungVonVorn()
    {
        var tracker = new DownloadStreamTracker();
        tracker.RegisterDestination(VideoStream);
        tracker.RegisterDestination(AudioStream);

        tracker.Reset();

        Assert.Equal(-1, tracker.StreamIndex);
        Assert.True(tracker.RegisterDestination(VideoStream));
        Assert.Equal(0, tracker.StreamIndex);
    }

    // ── Zusammenspiel mit der Gewichtung ────────────────────────────────────────

    [Fact]
    public void NeustartMittenImVideoStream_LaesstDenGesamtfortschrittNichtSpringen()
    {
        // Der Fehler in seiner sichtbaren Form: 40 % des Video-Streams, dann Bandbreitenwechsel.
        // Nach dem Neustart steht derselbe Stream bei denselben 40 % - der Balken muss dort
        // stehen bleiben, nicht in die zweite Hälfte springen.
        var tracker = new DownloadStreamTracker();
        tracker.RegisterDestination(VideoStream);

        double vorNeustart = DownloadProgressWeighting.ForStream(
            40, Math.Max(tracker.StreamIndex, 0), streamCount: 2, reservePostConversion: false);

        tracker.RegisterDestination(VideoStream); // Neustart mit --continue

        double nachNeustart = DownloadProgressWeighting.ForStream(
            40, Math.Max(tracker.StreamIndex, 0), streamCount: 2, reservePostConversion: false);

        Assert.Equal(20.0, vorNeustart);
        Assert.Equal(vorNeustart, nachNeustart);
    }
}
