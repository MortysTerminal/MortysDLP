namespace MortysDLP.Services.Tools
{
    /// <summary>Was der Startpfad mit einem erforderlichen Werkzeug tun muss — reine Logik,
    /// getrennt von Netzzugriff und Dialogen, damit sie sich für sich allein testen lässt.</summary>
    internal enum ToolStartupAction
    {
        /// <summary>Fehlt oder ist nicht brauchbar (falsche/fremde Antwort, kein Antwort) —
        /// der Installationsdialog muss laufen, bevor die Anwendung weitermachen kann.</summary>
        MustInstall,

        /// <summary>Vorhanden und brauchbar. Die Anwendung kann sofort starten; ob es eine
        /// neuere Version gibt, wird erst danach im Hintergrund geprüft.</summary>
        CanProceed,
    }

    /// <summary>Entscheidet aus Existenz (<see cref="ToolStatus"/>, ohne Netzzugriff) und
    /// Ausweis (<see cref="ToolProbe"/>, ohne Netzzugriff, aber ggf. ein kurzer Programmstart),
    /// ob ein erforderliches Werkzeug den Start blockiert. Kennt bewusst keine Version aus dem
    /// Netz — „gibt es ein Update?" ist keine Frage, die den Start aufhalten darf.</summary>
    internal static class ToolStartupDecision
    {
        public static ToolStartupAction For(ToolStatus status, ToolProbe probe)
        {
            if (!status.Installed)
                return ToolStartupAction.MustInstall;

            return probe.Usable ? ToolStartupAction.CanProceed : ToolStartupAction.MustInstall;
        }
    }
}
