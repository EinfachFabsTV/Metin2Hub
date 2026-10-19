using Avalonia.Media;

namespace M2Hub.Desktop.Services;

/// Gezeichnete Symbole fuer die Helper-Kacheln (Vektor, keine PNG-Datei).
public static class HelperIcons
{
    /// Auto-Res: ein Monitor mit Groessen-Pfeil (Aufloesung).
    public static IImage AutoRes { get; } = BuildAutoRes();

    private static IImage BuildAutoRes()
    {
        var blue = new SolidColorBrush(Color.Parse("#3b82f6"));
        var group = new DrawingGroup();

        // Bildschirm
        group.Children.Add(new GeometryDrawing
        {
            Geometry = Geometry.Parse("M7,9 H41 A3,3 0 0 1 44,12 V32 A3,3 0 0 1 41,35 H7 A3,3 0 0 1 4,32 V12 A3,3 0 0 1 7,9 Z"),
            Brush = new SolidColorBrush(Color.Parse("#16233c")),
            Pen = new Pen(blue, 3) { LineJoin = PenLineJoin.Round },
        });

        // Standfuss
        group.Children.Add(new GeometryDrawing
        {
            Geometry = Geometry.Parse("M24,35 V40 M17,41 H31"),
            Pen = new Pen(blue, 3) { LineCap = PenLineCap.Round },
        });

        // Diagonaler Doppelpfeil = Groesse/Aufloesung
        group.Children.Add(new GeometryDrawing
        {
            Geometry = Geometry.Parse("M15,28 L29,15 M15,28 L15,22 M15,28 L21,28 M29,15 L23,15 M29,15 L29,21"),
            Pen = new Pen(Brushes.White, 2.5) { LineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round },
        });

        return new DrawingImage { Drawing = group };
    }
}
