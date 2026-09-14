using System.Windows;
using System.Windows.Media;
using Nexo.Core.Shell;

namespace Nexo.App.Views.Controls;

/// <summary>
/// Lo que dibujan igual los dos mandos líquidos: el color del nivel a partir del acento y el pomo
/// oscuro con la flor de la marca, que gira con el valor.
/// </summary>
internal static class LiquidPainter
{
    private static (Geometry Petals, Geometry Spark)? _blossom;

    public static Color LevelColor(FrameworkElement owner, double percent)
    {
        var accent = (owner.TryFindResource("BrushAccent") as SolidColorBrush)?.Color ?? Color.FromRgb(0xE8, 0x73, 0x9E);
        var rgb = LiquidLevelMath.ColorAt(Math.Clamp(percent, 0, 100), new RgbColor(accent.R, accent.G, accent.B));
        return Color.FromRgb(rgb.R, rgb.G, rgb.B);
    }

    public static Color WithAlpha(Color color, double alpha) =>
        Color.FromArgb((byte)Math.Round(Math.Clamp(alpha, 0, 1) * 255), color.R, color.G, color.B);

    /// <summary>El brillo detrás de la comba.</summary>
    public static void DrawGlow(DrawingContext dc, Point center, double radiusX, double radiusY, Color color, double strength)
    {
        if (strength <= 0.01)
        {
            return;
        }

        var glow = new RadialGradientBrush
        {
            GradientOrigin = new Point(0.5, 0.5),
            GradientStops =
            {
                new GradientStop(WithAlpha(color, 0.62 * strength), 0),
                new GradientStop(WithAlpha(color, 0.2 * strength), 0.45),
                new GradientStop(WithAlpha(color, 0), 1)
            }
        };
        glow.Freeze();
        dc.DrawEllipse(glow, null, center, radiusX, radiusY);
    }

    /// <summary>El pomo, con su sombra, su canto y la flor dentro.</summary>
    public static void DrawKnob(DrawingContext dc, FrameworkElement owner, Point center, double radius, double percent, Color color, double glow)
    {
        var shadow = new SolidColorBrush(Color.FromArgb(90, 0, 0, 0));
        shadow.Freeze();
        dc.DrawEllipse(shadow, null, new Point(center.X, center.Y + 2.5), radius + 1.5, radius + 1.5);

        var body = new LinearGradientBrush(Color.FromRgb(0x3B, 0x3A, 0x3F), Color.FromRgb(0x1C, 0x1B, 0x1F), 90);
        body.Freeze();
        var rim = new Pen(new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)), 1);
        rim.Freeze();
        dc.DrawEllipse(body, rim, center, radius, radius);

        DrawBlossom(dc, owner, center, radius, percent, color, glow);
    }

    /// <summary>
    /// Los pétalos macizos y el destello de la marca, en su retícula de 48. Del contorno de la marca
    /// solo se usa la silueta exterior: el contorno completo es un trazo hueco, y a este tamaño su
    /// grosor quedaría por debajo del píxel y se vería como una mancha borrosa.
    /// </summary>
    private static (Geometry Petals, Geometry Spark)? Blossom(FrameworkElement owner)
    {
        if (_blossom is { } cached)
        {
            return cached;
        }

        if (owner.TryFindResource("IconSakuraMark") is not GeometryGroup { Children.Count: >= 2 } mark ||
            mark.Children[0] is not PathGeometry { Figures.Count: > 0 } outline ||
            mark.Children[1] is not PathGeometry spark)
        {
            return null;
        }

        var petals = new PathGeometry([outline.Figures[0].Clone()]);
        petals.Freeze();
        var sparkCopy = spark.Clone();
        sparkCopy.Transform = null;
        sparkCopy.Freeze();
        _blossom = (petals, sparkCopy);
        return _blossom;
    }

    /// <summary>
    /// La flor gira un cuarto de vuelta de 0 a 100: con cuatro pétalos eso es una vuelta completa del
    /// dibujo, así que el pomo se lee como un dial.
    /// </summary>
    private static void DrawBlossom(DrawingContext dc, FrameworkElement owner, Point center, double knobRadius, double percent, Color color, double glow)
    {
        if (Blossom(owner) is not { } blossom)
        {
            return;
        }

        // La marca ocupa de 2 a 46 en su retícula; se deja un anillo del pomo alrededor.
        var scale = knobRadius * 1.5 / 44;

        dc.PushTransform(new TranslateTransform(center.X, center.Y));
        dc.PushTransform(new RotateTransform(percent * 0.9));
        dc.PushTransform(new ScaleTransform(scale, scale));
        dc.PushTransform(new TranslateTransform(-24, -24));

        var petals = new SolidColorBrush(WithAlpha(color, Math.Clamp(0.5 + (0.2 * (glow - 1)), 0.4, 0.75)));
        petals.Freeze();
        var spark = new SolidColorBrush(Color.FromRgb(0x1C, 0x1B, 0x1F));
        spark.Freeze();

        // El destello va oscuro, del color del pomo, recortado sobre los pétalos: a este tamaño un
        // destello claro sobre pétalos claros se funde, y así la flor se lee como la del icono.
        var sparkPen = new Pen(spark, 0.9 / scale) { LineJoin = PenLineJoin.Round };
        sparkPen.Freeze();
        dc.DrawGeometry(petals, null, blossom.Petals);
        dc.DrawGeometry(spark, sparkPen, blossom.Spark);

        dc.Pop();
        dc.Pop();
        dc.Pop();
        dc.Pop();
    }
}
