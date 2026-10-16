using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace M2Hub.Desktop.Services;

/// Wandelt einen Prozentwert (0-100) in einen Kreisbogen fuer den
/// Ziel-Fortschrittsring. Fester Mittelpunkt (60,60) und Radius 50, passend
/// zu einem 120x120-Feld. Oben (−90°) beginnend, im Uhrzeigersinn.
public sealed class ArcConverter : IValueConverter
{
    private const double Cx = 60, Cy = 60, R = 50;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var pct = Math.Clamp(value is double d ? d : 0, 0, 100);
        var geo = new StreamGeometry();
        if (pct <= 0) return geo;

        var sweep = Math.Min(359.999, pct / 100.0 * 360.0);
        var start = At(-90);
        var end = At(-90 + sweep);

        using var ctx = geo.Open();
        ctx.BeginFigure(start, false);
        ctx.ArcTo(end, new Size(R, R), 0, sweep > 180, SweepDirection.Clockwise);
        ctx.EndFigure(false);
        return geo;
    }

    private static Point At(double deg)
    {
        var a = deg * Math.PI / 180;
        return new Point(Cx + R * Math.Cos(a), Cy + R * Math.Sin(a));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
