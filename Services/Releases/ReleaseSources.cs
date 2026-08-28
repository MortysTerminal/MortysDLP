using System.Collections.Generic;

namespace MortysDLP.Services.Releases
{
    /// <summary>
    /// Feste Quellenketten je Werkzeug — <see cref="CreateAppChain"/> ist bisher die einzige.
    /// Reihenfolge „reichste Information zuerst": Die beiden API-Quellen liefern Changelog,
    /// Asset-Namen und Größen; der Atom-Feed liefert Text, aber keine Assets; die Weiterleitung
    /// liefert nur die Versionsnummer. Wer die Reihenfolge umdreht, verliert Informationen, ohne
    /// robuster zu werden. Noch nicht in den Startpfad eingebaut (→ W2-T06); Welle 4 ergänzt
    /// hier <c>CreateYtDlpChain()</c> und die übrigen Werkzeuge.
    /// </summary>
    internal static class ReleaseSources
    {
        public static IReadOnlyList<IReleaseSource> CreateAppChain() =>
        [
            new GitHubApiLatestSource(),
            new GitHubApiListSource(),
            new GitHubAtomFeedSource(),
            new GitHubRedirectSource(),
        ];
    }
}
