using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace M2Hub.Desktop.Services;

// Rechnet aus der verfuegbaren Breite eine Kartenbreite, sodass die Spalten
// die Zeile ausfuellen: passt die naechste Karte knapp nicht mehr, werden die
// vorhandenen etwas schmaler, statt dass eine Spalte umbricht und rechts eine
// Leerflaeche bleibt. Unter MinCard faellt eine Spalte weg, ueber MaxCard wird
// die Karte nicht weiter gedehnt (die Zeile ist dann zentriert).
public sealed class CardWidthConverter : IValueConverter
{
    private const double Gap = 10;      // Abstand zwischen den Karten (Margin)
    private const double MinCard = 340; // darunter lieber eine Spalte weniger
    private const double MaxCard = 520;  // breiter wird eine Karte nicht

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double w || w <= 0)
            return 400.0;

        var cols = Math.Max(1, (int)((w + Gap) / (MinCard + Gap)));
        var card = Math.Floor((w - cols * Gap) / cols);
        if (card > MaxCard) card = MaxCard;
        if (card < 200) card = 200;
        return card;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
