using System;
using System.Security;

namespace MortysDLP.Helpers
{
    /// <summary>
    /// Prüft, ob ein Netzwerkziel angefragt werden darf: nur <c>https</c> und eine feste
    /// Freigabeliste bekannter Update-Quellen. Geprüft wird beides — die URL vor dem Absenden
    /// UND <c>response.RequestMessage?.RequestUri</c> danach, das nach automatischen
    /// Weiterleitungen das tatsächlich erreichte Ziel ist.
    /// </summary>
    internal static class UrlSafety
    {
        private static readonly string[] AllowedHosts =
        [
            "github.com",
            "api.github.com",

            // Bewusst die Domäne, nicht einzelne Rechnernamen: GitHub liefert Release-Anhänge
            // über wechselnde Unterdomänen aus und hat sie mehrfach umbenannt
            // (objects. → github-releases. → release-assets.). Jeder dieser Namen ist
            // GitHub-eigene Infrastruktur, erreicht wird sie ausschließlich über eine
            // Weiterleitung von github.com bzw. api.github.com. Einzelne Namen zu pflegen hat
            // sich als Fehlerquelle erwiesen: Am 2026-08-31 scheiterte jeder Anhang-Download an
            // "Ziel nicht erlaubt", weil GitHub auf release-assets.githubusercontent.com
            // umgestellt hatte — das betraf nicht nur Werkzeuge, sondern auch das Selbst-Update
            // der Anwendung. raw.githubusercontent.com (version.json) ist damit mit abgedeckt.
            "githubusercontent.com",

            "huggingface.co",
            "hf-mirror.com",
            "pypi.org",
            "files.pythonhosted.org",
            "www.gyan.dev",
        ];

        /// <summary>true, wenn <paramref name="uri"/> ausschließlich <c>https</c> nutzt und der
        /// Host exakt einem Eintrag der Freigabeliste entspricht oder eine Unterdomäne davon
        /// ist. Vergleich über <see cref="Uri.IdnHost"/> und ordinal ohne
        /// Groß-/Kleinschreibung — sonst rutscht ein Unicode-Homoglyph-Host durch, der zwar wie
        /// ein erlaubter Host aussieht, aber ein anderer ist.</summary>
        public static bool IsAllowed(Uri? uri)
        {
            if (uri is null || !uri.IsAbsoluteUri)
                return false;

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                return false;

            string host = uri.IdnHost;

            foreach (string allowed in AllowedHosts)
            {
                if (string.Equals(host, allowed, StringComparison.OrdinalIgnoreCase))
                    return true;

                if (host.EndsWith("." + allowed, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>Wie <see cref="IsAllowed"/>, wirft aber <see cref="SecurityException"/>
        /// statt <c>false</c> zurückzugeben.</summary>
        public static void EnsureAllowed(Uri? uri)
        {
            if (!IsAllowed(uri))
                throw new SecurityException($"Ziel nicht erlaubt: {uri}");
        }
    }
}
