using System.Globalization;
using Avalonia.Data.Converters;

namespace M2Hub.Desktop.Views;

/// Wie viele Tageskacheln des Run Trackers nebeneinander passen.
///
/// Wie `TileColumns` bei den Accounts: die Fensterbreite bestimmt nur die
/// *Anzahl* der Spalten, die Breite teilen sie unter sich auf - so bleibt
/// rechts keine Luecke stehen.
public sealed class DayTileColumns : IValueConverter
{
    public static readonly DayTileColumns Instance = new();

    /// Darunter passt „12 Runs · 108 Truhen" nicht mehr in eine Zeile.
    private const double MinTileWidth = 260;

    private const int MaxColumns = 5;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var width = value as double? ?? 0;
        return Math.Clamp((int)(width / MinTileWidth), 1, MaxColumns);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
