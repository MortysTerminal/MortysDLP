using System.Windows.Controls;
using System.Windows.Threading;

namespace MortysDLP.Helpers
{
    /// <summary>
    /// Sammelt Ausgabezeilen aus beliebigen Threads und schreibt sie gebündelt in eine
    /// <see cref="TextBox"/>. Begrenzt auf eine feste Zeilenzahl (Ringpuffer), damit die
    /// Anzeige bei langen Vorgängen (Playlists, lange Konvertierungen) nicht unbegrenzt
    /// wächst und die Oberfläche nicht bremst.
    /// </summary>
    internal sealed class LogBuffer : IDisposable
    {
        private readonly TextBox _target;
        private readonly int _maxLines;
        private readonly bool _alsoToFile;
        private readonly DispatcherTimer _timer;
        private readonly Queue<string> _lines;
        private readonly object _sync = new();
        private bool _dirty;

        public LogBuffer(TextBox target, int maxLines = 500, int flushIntervalMs = 150, bool alsoToFile = true)
        {
            _target = target;
            _maxLines = maxLines;
            _alsoToFile = alsoToFile;
            _lines = new Queue<string>(maxLines);

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(flushIntervalMs) };
            _timer.Tick += (_, _) => Flush();
            _timer.Start();
        }

        /// <summary>Reiht eine Zeile ein. Nicht blockierend, aus jedem Thread aufrufbar —
        /// insbesondere aus Prozess-Ereignisbehandlern.</summary>
        public void Append(string line)
        {
            if (_alsoToFile) Log.Debug(line);

            lock (_sync)
            {
                if (_lines.Count >= _maxLines)
                    _lines.Dequeue();
                _lines.Enqueue(line);
                _dirty = true;
            }
        }

        /// <summary>Leert den Puffer sofort, ohne auf den nächsten Timer-Tick zu warten.
        /// Muss vom UI-Thread aufgerufen werden (schreibt direkt in die <see cref="TextBox"/>,
        /// anders als <see cref="Append"/>).</summary>
        public void Clear()
        {
            lock (_sync)
            {
                _lines.Clear();
                _dirty = true;
            }
            Flush();
        }

        /// <summary>Gibt den vollständigen Pufferinhalt zurück, z. B. zum Kopieren.</summary>
        public string GetText()
        {
            lock (_sync)
            {
                return string.Join(Environment.NewLine, _lines);
            }
        }

        private void Flush()
        {
            string text;
            lock (_sync)
            {
                if (!_dirty) return;
                text = string.Join(Environment.NewLine, _lines);
                _dirty = false;
            }

            // Nur ans Ende scrollen, wenn der Nutzer bereits unten war - sonst reißt die
            // Aktualisierung ihn aus der Stelle, die er gerade liest.
            bool wasAtBottom = IsScrolledToBottom();
            _target.Text = text;
            if (wasAtBottom)
                _target.ScrollToEnd();
        }

        private bool IsScrolledToBottom() =>
            _target.VerticalOffset >= _target.ExtentHeight - _target.ViewportHeight - 1.0;

        public void Dispose()
        {
            _timer.Stop();
        }
    }
}
