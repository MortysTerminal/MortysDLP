namespace MortysDLP.Models
{
    /// <summary>Farbton einer Statusanzeige. Die Farbe selbst steht nie im Code: Der Stil
    /// <c>StatusToneTextStyle</c> (<c>App.xaml</c>) bildet den Ton auf <c>NeutralBrush</c>,
    /// <c>SuccessBrush</c>, <c>WarningBrush</c> bzw. <c>ErrorBrush</c> ab - per
    /// <c>DynamicResource</c>, damit ein Wechsel zwischen hellem und dunklem Windows-Theme auch
    /// bereits angezeigte Zeilen sofort umfärbt.</summary>
    public enum StatusTone
    {
        Neutral,
        Success,
        Warning,
        Error,
    }
}
