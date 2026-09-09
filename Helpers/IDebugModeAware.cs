namespace MortysDLP.Helpers
{
    /// <summary>Eine Seite mit einem Debug-Bereich, der sich beim Umschalten des
    /// Debug-Modus (Einstellungen) sofort aktualisieren muss — ohne dass der Nutzer
    /// erst den Tab wechseln muss. <see cref="MortysDLP.MainWindow.RefreshDebugMode"/>
    /// ruft <see cref="ApplyDebugMode"/> auf allen bereits erzeugten Seiten auf, die
    /// das implementieren; eine handgepflegte Aufzählung entfällt damit.</summary>
    internal interface IDebugModeAware
    {
        /// <summary>Blendet den Debug-Bereich der Seite nach dem aktuellen Wert von
        /// <c>Settings.Default.DebugMode</c> ein oder aus. Darf mehrfach und aus jedem
        /// Zustand aufgerufen werden.</summary>
        void ApplyDebugMode();
    }
}
