using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace M2Hub.Desktop.Services;

/// Wandelt einen Fortschritt 0–100 in die Bogen-Geometrie des Prozent-Rings
/// (Koordinaten 0–100, Mittelpunkt 50/50, Radius 42, oben beginnend, im
/// Uhrzeigersinn). Fuer `Path.Data` in der Goals-Ansicht.
public sealed class ProgressArcConverter : IValueConverter
{
    public static readonly ProgressArcConverter Instance = new();

    private const double Cx = 50, Cy = 50, R = 42;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var p = value switch
        {
            double d => d,
            int i => i,
            _ => 0.0,
        };

        if (p <= 0) return null;                 // nichts gezeichnet
        var sweep = Math.Min(359.999, p / 100.0 * 360.0);

        var rad = sweep * Math.PI / 180.0;
        var ex = Cx + R * Math.Sin(rad);
        var ey = Cy - R * Math.Cos(rad);
        var large = sweep > 180 ? 1 : 0;

        var inv = CultureInfo.InvariantCulture;
        var path = string.Format(inv, "M {0},{1} A {2},{2} 0 {3} 1 {4:F3},{5:F3}",
            Cx, Cy - R, R, large, ex, ey);

        return Geometry.Parse(path);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
