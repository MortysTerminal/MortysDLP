namespace MortysDLP.Services
{
    /// <summary>
    /// Wird ausgelöst, wenn ein externes Werkzeug gestartet werden soll, dessen ausführbare
    /// Datei nicht (mehr) vorhanden ist. Bewusst ein eigener Typ statt des nackten
    /// <see cref="System.ComponentModel.Win32Exception"/>: Die aufrufenden Seiten sollen den
    /// Fall „Werkzeug fehlt" von einem echten Verarbeitungsfehler unterscheiden können, um eine
    /// verständliche Meldung zu zeigen und zur Werkzeuge-Seite weiterzuleiten, statt pauschal
    /// „Fehler beim Download" auszugeben.
    /// </summary>
    internal sealed class ToolMissingException : System.Exception
    {
        /// <summary>Der Pfad der erwarteten, aber fehlenden ausführbaren Datei.</summary>
        public string ExecutablePath { get; }

        public ToolMissingException(string executablePath)
            : base($"Werkzeug nicht gefunden: {executablePath}")
            => ExecutablePath = executablePath;
    }
}
