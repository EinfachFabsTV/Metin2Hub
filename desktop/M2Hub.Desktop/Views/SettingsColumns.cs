using System.Globalization;
using Avalonia.Data.Converters;

namespace M2Hub.Desktop.Views;

/// Ob die Einstellungen ein- oder zweispaltig stehen.
///
/// Die Karten standen alle untereinander: viel Scrollen, waehrend rechts die
/// halbe Seite leer blieb. Ab genug Breite stehen sie deshalb in zwei Spalten,
/// darunter wieder in einer - sonst wuerde jede Karte zu schmal.
///
/// Mehr als zwei Spalten gibt es nicht: die Seite hat genau zwei Stapel, bei
/// drei Spalten bliebe die dritte leer.
public sealed class SettingsColumns : IValueConverter
{
    public static readonly SettingsColumns Instance = new();

    /// Darunter wird eine Spalte zu schmal fuer die Auswahlfelder.
    private const double MinColumnWidth = 520;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        (value as double? ?? 0) >= MinColumnWidth * 2 ? 2 : 1;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
