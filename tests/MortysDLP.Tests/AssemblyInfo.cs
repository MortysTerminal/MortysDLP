using Xunit;

// Log ist eine globale statische Klasse (LogsDirectory, MinLevel, ein einzelner
// Schreiber-Thread). Mehrere Testklassen berühren sie - LogTests direkt, andere wie
// DownloadHistoryServiceTests indirekt über Fehlerpfade der Produktionslogik, die intern
// Log.Warn aufrufen. Liefe das parallel zu LogTests, würden fremde Zeilen im selben globalen
// Zustand landen und dessen Zeilenzählung verfälschen. Testklassen deshalb sequenziell
// ausführen statt einzelne Klassen einzeln zu isolieren - robuster gegenüber künftigen
// Testklassen, die ebenfalls Log berühren.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
