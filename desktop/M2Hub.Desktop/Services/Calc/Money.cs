using System.Globalization;

namespace M2Hub.Desktop.Services.Calc;

/// Yang-Betraege einheitlich darstellen. Portiert aus
/// `Veraltet/shared/utils/calc/money.ts`, damit die App dieselben Zahlen
/// schreibt wie die Website vorher.
///
/// **Gerechnet wird ueberall in kk**, angezeigt wird ab 100 kk in w (Won).
/// Im Spiel ist ein kk eine Million Yang; 100 kk sind ein Won. Wer Betraege
/// ausgibt, nimmt `FormatYang` - nicht selbst gerundet und nicht selbst ein
/// „kk" angehaengt.
public static class Money
{
    /// 100 kk sind 1 w.
    public const int KkPerW = 100;

    /// Ein kk in Yang - nur fuer Erklaertexte, gerechnet wird in kk.
    public const long YangPerKk = 1_000_000;

    /// Unter 100 kk als kk, darueber als w. Hoechstens zwei Nachkommastellen,
    /// wie in der Vorlage.
    public static string FormatYang(double kk)
    {
        if (double.IsNaN(kk) || double.IsInfinity(kk)) return "—";

        return Math.Abs(kk) < KkPerW
            ? Number(kk) + " kk"
            : Number(kk / KkPerW) + " w";
    }

    /// Ein Betrag, der schon in w vorliegt.
    public static string FormatW(double w) => FormatYang(w * KkPerW);

    private static string Number(double value) =>
        value.ToString("#,0.##", CultureInfo.CurrentCulture);
}
