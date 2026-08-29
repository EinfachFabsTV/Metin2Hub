using System.Globalization;
using Avalonia.Data.Converters;

namespace M2Hub.Desktop.Views;

/// Wie viele Kacheln nebeneinander passen.
///
/// Vorher legte ein WrapPanel die Kacheln mit fester Breite nebeneinander.
/// Was danach uebrig blieb, blieb leer - bei Standardbreite fehlten keine
/// hundert Pixel zur dritten Spalte, und rechts stand eine Luecke. Jetzt
/// bestimmt die Fensterbreite nur noch die *Anzahl* der Spalten; die Breite
/// teilen sie unter sich auf, sodass die Zeile immer aufgeht.
public sealed class TileColumns : IValueConverter
{
    public static readonly TileColumns Instance = new();

    /// Unter dieser Breite wird eine Kachel unleserlich (Charaktertabelle).
    private const double MinTileWidth = 380;

    /// Darueber werden die Kacheln nur noch duenner, ohne mehr zu zeigen.
    private const int MaxColumns = 4;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var width = value as double? ?? 0;
        var fit = (int)(width / MinTileWidth);
        return Math.Clamp(fit, 1, MaxColumns);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
