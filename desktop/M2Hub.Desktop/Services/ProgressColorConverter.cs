using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace M2Hub.Desktop.Services;

/// Faerbt den Prozent-Ring nach dem Fortschritt: unter 34 % Gefahr (rot),
/// unter 67 % Bernstein, darueber Erfolg (gruen). Liefert einen Theme-Brush
/// aus `Theme.axaml`; findet sich keiner, bleibt es bei Grau.
public sealed class ProgressColorConverter : IValueConverter
{
    public static readonly ProgressColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var p = value switch
        {
            double d => d,
            int i => i,
            _ => 0.0,
        };

        var key = p < 34 ? "DangerBrush" : p < 67 ? "AmberBrush" : "SuccessBrush";

        if (Application.Current is { } app
            && app.Resources.TryGetResource(key, app.ActualThemeVariant, out var res)
            && res is IBrush brush)
            return brush;

        return Brushes.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
