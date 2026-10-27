using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace MortysDLP.Helpers
{
    /// <summary>
    /// Hilfen für Bildschirmleser (UI Automation).
    ///
    /// <para><see cref="AnnounceChangesProperty"/>: Eine Statuszeile soll angesagt werden, sobald
    /// sich ihr Text ändert. WPF setzt dafür zwar <c>AutomationProperties.LiveSetting</c>, löst
    /// das zugehörige <c>LiveRegionChanged</c>-Ereignis aber <b>nicht</b> selbst aus — ohne
    /// diesen Aufruf hört der Bildschirmleser nichts. Deshalb hier einmal zentral statt auf
    /// jeder Seite: In XAML <c>helpers:A11y.AnnounceChanges="True"</c> an den
    /// <see cref="TextBlock"/> schreiben, fertig.</para>
    ///
    /// <para>Der Text-Beobachter hängt nur, solange das Element geladen ist
    /// (<c>Loaded</c>/<c>Unloaded</c>). <see cref="DependencyPropertyDescriptor.AddValueChanged"/>
    /// hält sonst eine starke Referenz und ein geschlossener Dialog bliebe im Speicher.</para>
    /// </summary>
    public static class A11y
    {
        public static readonly DependencyProperty AnnounceChangesProperty =
            DependencyProperty.RegisterAttached(
                "AnnounceChanges", typeof(bool), typeof(A11y),
                new PropertyMetadata(false, OnAnnounceChangesChanged));

        public static bool GetAnnounceChanges(DependencyObject d) => (bool)d.GetValue(AnnounceChangesProperty);
        public static void SetAnnounceChanges(DependencyObject d, bool value) => d.SetValue(AnnounceChangesProperty, value);

        /// <summary>Windows-Einstellung „Animationseffekte anzeigen". Ist sie aus, laufen keine
        /// Endlos-Animationen (Drehsymbole) - das Symbol steht dann still.</summary>
        public static bool AnimationsEnabled => SystemParameters.ClientAreaAnimation;

        // Zuletzt angesagter Text je Element - verhindert, dass beim Seitenwechsel (die ganze
        // Seite wird wieder sichtbar) eine längst bekannte Meldung erneut vorgelesen wird.
        private static readonly DependencyProperty LastAnnouncedProperty =
            DependencyProperty.RegisterAttached("LastAnnounced", typeof(string), typeof(A11y), new PropertyMetadata(null));

        private static readonly DependencyPropertyDescriptor TextDescriptor =
            DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));

        private static void OnAnnounceChangesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TextBlock tb)
                return;

            if ((bool)e.NewValue)
            {
                AutomationProperties.SetLiveSetting(tb, AutomationLiveSetting.Polite);
                tb.Loaded += OnLoaded;
                tb.Unloaded += OnUnloaded;
                tb.IsVisibleChanged += OnIsVisibleChanged;
                if (tb.IsLoaded)
                    TextDescriptor.AddValueChanged(tb, OnTextChanged);
            }
            else
            {
                tb.Loaded -= OnLoaded;
                tb.Unloaded -= OnUnloaded;
                tb.IsVisibleChanged -= OnIsVisibleChanged;
                TextDescriptor.RemoveValueChanged(tb, OnTextChanged);
                tb.ClearValue(AutomationProperties.LiveSettingProperty);
            }
        }

        private static void OnLoaded(object sender, RoutedEventArgs e)
        {
            // RemoveValueChanged vorweg: Loaded kann ohne dazwischenliegendes Unloaded mehrfach
            // kommen (z. B. beim Umhängen im Baum) - so hängt der Beobachter nie doppelt.
            var tb = (TextBlock)sender;
            TextDescriptor.RemoveValueChanged(tb, OnTextChanged);
            TextDescriptor.AddValueChanged(tb, OnTextChanged);
        }

        private static void OnUnloaded(object sender, RoutedEventArgs e) =>
            TextDescriptor.RemoveValueChanged((TextBlock)sender, OnTextChanged);

        private static void OnTextChanged(object? sender, System.EventArgs e)
        {
            if (sender is TextBlock tb)
                Announce(tb);
        }

        // Viele Aufrufer setzen erst den Text und blenden den Bereich danach ein (Update-Banner,
        // Twitch-Status). Ohne diesen Weg ginge die erste Meldung verloren, weil ein unsichtbares
        // Element nicht angesagt wird.
        private static void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Unsichtbar geworden: Wurde nur ein Bereich ausgeblendet (Fenster offen, Element
            // weiter im Baum), ist die gemerkte Meldung vergessen - ein zweiter Lauf mit gleichem
            // Text wird dann wieder angesagt. Bei einem Seitenwechsel verlässt die Seite den Baum
            // (IsLoaded = false); dann bleibt die Meldung gemerkt und wird bei der Rückkehr nicht
            // wiederholt. Verzögert, weil Unloaded erst nach IsVisibleChanged kommt.
            if (sender is TextBlock hidden && !(bool)e.NewValue)
            {
                hidden.Dispatcher.BeginInvoke(() =>
                {
                    if (hidden.IsLoaded && Window.GetWindow(hidden) is { IsVisible: true })
                        hidden.ClearValue(LastAnnouncedProperty);
                }, System.Windows.Threading.DispatcherPriority.Background);
                return;
            }

            if (sender is TextBlock tb && (bool)e.NewValue
                && !string.Equals(tb.Text, (string?)tb.GetValue(LastAnnouncedProperty), System.StringComparison.Ordinal))
                Announce(tb);
        }

        private static void Announce(TextBlock tb)
        {
            if (!tb.IsVisible || string.IsNullOrWhiteSpace(tb.Text))
                return;

            tb.SetValue(LastAnnouncedProperty, tb.Text);

            // Ohne angemeldeten Client (kein Bildschirmleser aktiv) kostet das nichts.
            if (!AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged))
                return;

            var peer = UIElementAutomationPeer.FromElement(tb) ?? UIElementAutomationPeer.CreatePeerForElement(tb);
            peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }
    }
}
