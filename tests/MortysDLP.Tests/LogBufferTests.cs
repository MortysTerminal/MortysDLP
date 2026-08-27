using MortysDLP.Helpers;
using System.Runtime.ExceptionServices;
using System.Windows.Controls;

namespace MortysDLP.Tests;

public class LogBufferTests
{
    /// <summary>WPF-Steuerelemente (<see cref="TextBox"/>, <see cref="System.Windows.Threading.DispatcherTimer"/>)
    /// verlangen einen STA-Thread. xUnit-Testthreads sind MTA, deshalb läuft jeder Testkörper
    /// auf einem eigens dafür gestarteten STA-Thread. Der Timer selbst tickt hier nie (kein
    /// Dispatcher.Run-Nachrichtenpump läuft) — <see cref="LogBuffer.Clear"/> und
    /// <see cref="LogBuffer.GetText"/> arbeiten unabhängig davon synchron auf dem internen
    /// Puffer, was genau das ist, was diese Tests prüfen sollen.</summary>
    private static void RunOnSta(Action action)
    {
        ExceptionDispatchInfo? capturedException = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { capturedException = ExceptionDispatchInfo.Capture(ex); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        capturedException?.Throw();
    }

    [Fact]
    public void Append_MehrereZeilen_GetTextLiefertSieInReihenfolge()
    {
        RunOnSta(() =>
        {
            using var buffer = new LogBuffer(new TextBox(), maxLines: 10, alsoToFile: false);

            buffer.Append("eins");
            buffer.Append("zwei");
            buffer.Append("drei");

            Assert.Equal(string.Join(Environment.NewLine, "eins", "zwei", "drei"), buffer.GetText());
        });
    }

    [Fact]
    public void Append_UeberschreitetObergrenze_AeltesteZeileFaelltHeraus()
    {
        RunOnSta(() =>
        {
            using var buffer = new LogBuffer(new TextBox(), maxLines: 3, alsoToFile: false);

            buffer.Append("eins");
            buffer.Append("zwei");
            buffer.Append("drei");
            buffer.Append("vier");

            Assert.Equal(string.Join(Environment.NewLine, "zwei", "drei", "vier"), buffer.GetText());
        });
    }

    [Fact]
    public void Append_ObergrenzeBleibtDauerhaftEingehalten()
    {
        RunOnSta(() =>
        {
            using var buffer = new LogBuffer(new TextBox(), maxLines: 5, alsoToFile: false);

            for (int i = 0; i < 1000; i++)
                buffer.Append($"Zeile {i}");

            int lineCount = buffer.GetText().Split(Environment.NewLine).Length;
            Assert.Equal(5, lineCount);
            Assert.Equal(string.Join(Environment.NewLine, "Zeile 995", "Zeile 996", "Zeile 997", "Zeile 998", "Zeile 999"),
                buffer.GetText());
        });
    }

    [Fact]
    public void Append_AusZehnThreadsGleichzeitig_WirftKeineAusnahme()
    {
        RunOnSta(() =>
        {
            using var buffer = new LogBuffer(new TextBox(), maxLines: 500, alsoToFile: false);

            var threads = Enumerable.Range(0, 10).Select(t => new Thread(() =>
            {
                for (int i = 0; i < 100; i++)
                    buffer.Append($"Thread {t} Zeile {i}");
            })).ToList();

            foreach (var thread in threads) thread.Start();
            foreach (var thread in threads) thread.Join();

            // 1000 Zeilen eingereiht, Ringpuffer auf 500 begrenzt -> exakt 500 übrig,
            // keine davon leer/beschädigt (kein verschachteltes Schreiben in die Queue).
            var lines = buffer.GetText().Split(Environment.NewLine);
            Assert.Equal(500, lines.Length);
            Assert.All(lines, line => Assert.False(string.IsNullOrEmpty(line)));
        });
    }

    [Fact]
    public void Append_600ZeilenBeiMaxLines500_GenauFuenfhundertUebrigAelstesteFehlt()
    {
        RunOnSta(() =>
        {
            using var buffer = new LogBuffer(new TextBox(), maxLines: 500, alsoToFile: false);

            for (int i = 0; i < 600; i++)
                buffer.Append($"Zeile {i}");

            var lines = buffer.GetText().Split(Environment.NewLine);
            Assert.Equal(500, lines.Length);
            Assert.Equal("Zeile 100", lines[0]);   // älteste 100 Zeilen (0..99) sind herausgefallen
            Assert.Equal("Zeile 599", lines[^1]);
        });
    }

    [Fact]
    public void Dispose_StopptDenTimer()
    {
        RunOnSta(() =>
        {
            var buffer = new LogBuffer(new TextBox(), maxLines: 10, alsoToFile: false);
            Assert.True(buffer.IsTimerRunningForTests);

            buffer.Dispose();

            Assert.False(buffer.IsTimerRunningForTests);
        });
    }

    [Fact]
    public void Clear_LeertDenPufferSofort()
    {
        RunOnSta(() =>
        {
            var box = new TextBox();
            using var buffer = new LogBuffer(box, maxLines: 10, alsoToFile: false);
            buffer.Append("eins");
            buffer.Append("zwei");

            buffer.Clear();

            Assert.Equal("", buffer.GetText());
            Assert.Equal("", box.Text);
        });
    }

    [Fact]
    public void Clear_GefolgtVonNeuenZeilen_StartetLeer()
    {
        RunOnSta(() =>
        {
            using var buffer = new LogBuffer(new TextBox(), maxLines: 10, alsoToFile: false);
            buffer.Append("alt");
            buffer.Clear();

            buffer.Append("neu");

            Assert.Equal("neu", buffer.GetText());
        });
    }

    [Fact]
    public void GetText_LeererPuffer_GibtLeereZeichenketteZurueck()
    {
        RunOnSta(() =>
        {
            using var buffer = new LogBuffer(new TextBox(), maxLines: 10, alsoToFile: false);

            Assert.Equal("", buffer.GetText());
        });
    }
}
