using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Nexo.App.Motion;
using Nexo.Core.Shell;

namespace Nexo.App.Views.Controls;

/// <summary>
/// El mando líquido de volumen y brillo (Adler, 2026-09-14, sobre un vídeo de referencia).
///
/// Una regla a la izquierda, una línea vertical que se abomba alrededor del pomo con un brillo detrás,
/// y el valor en grande al lado del pomo, del color que le toca a ese nivel. Se dibuja entero en
/// <see cref="OnRender"/> porque la comba cambia de forma en cada fotograma: con formas de XAML
/// habría que reconstruir su geometría igualmente, y además medir y colocar cada pieza.
///
/// Solo pide fotogramas mientras algo se mueve. En reposo no cuesta nada, que es lo que se espera de
/// un panel que pasa la mayor parte del tiempo cerrado o quieto.
///
/// La forma, el color y los muelles salen de <see cref="LiquidLevelMath"/>; esto solo los pinta.
///
/// Lo propio de Sakura está en el pomo: lleva dentro la flor de cuatro pétalos de la marca, que gira
/// como un dial al cambiar el valor (un cuarto de vuelta de 0 a 100, que con cuatro pétalos es una
/// vuelta completa del dibujo). La flor es la del icono, sacada de <c>IconSakuraMark</c>, no un
/// dibujo parecido.
/// </summary>
public sealed class LiquidLevelGauge : FrameworkElement
{
    public const double GaugeWidth = 132;
    public const double GaugeHeight = 272;

    // La escala va de TopInset a GaugeHeight - BottomInset, pero la línea sigue más allá por los dos
    // lados: sin ese tramo, al 0 % o al 100 % la comba se cortaba justo en el punto más abombado.
    private const double TopInset = 40;
    private const double BottomInset = 64;
    private const double LineTop = 6;
    private const double LineBottomInset = 34;
    private const double LineX = 56;
    private const double Amplitude = 15;
    private const double BumpRadius = 34;
    private const double KnobRadius = 12.5;

    private static readonly Typeface NumberFace = new(
        new FontFamily("Segoe UI Variable Display, Segoe UI"), FontStyles.Normal, FontWeights.Light, FontStretches.Normal);

    private static readonly Typeface LabelFace = new(
        new FontFamily("Segoe UI Variable Text, Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

    private readonly Geometry _glyph;
    private readonly bool _solidGlyph;
    private readonly LiquidMotion _motion;

    public LiquidLevelGauge(string glyphData, bool solidGlyph)
    {
        _glyph = Geometry.Parse(glyphData);
        _glyph.Freeze();
        _solidGlyph = solidGlyph;
        _motion = new LiquidMotion(InvalidateVisual);

        Width = GaugeWidth;
        Height = GaugeHeight;
        IsHitTestVisible = false;
        Unloaded += (_, _) => _motion.Stop();
    }

    /// <summary>El valor pedido, que es el que se escribe al equipo; lo dibujado puede ir un poco detrás.</summary>
    public double Target => _motion.Target;

    /// <summary>El porcentaje que corresponde a una altura dentro del mando, con el 100 % arriba.</summary>
    public static double PercentAt(double y)
    {
        var length = GaugeHeight - TopInset - BottomInset;
        return Math.Clamp((1 - ((y - TopInset) / length)) * 100, 0, 100);
    }

    /// <inheritdoc cref="LiquidMotion.Reveal"/>
    public void Reveal(double percent) => _motion.Reveal(percent);

    /// <inheritdoc cref="LiquidMotion.SetLevel"/>
    public void SetLevel(double percent, bool follow) => _motion.SetLevel(percent, follow);

    /// <inheritdoc cref="LiquidMotion.SetPressed"/>
    public void SetPressed(bool pressed) => _motion.SetPressed(pressed);

    private static double YFor(double percent) =>
        TopInset + ((1 - (percent / 100)) * (GaugeHeight - TopInset - BottomInset));

    protected override void OnRender(DrawingContext dc)
    {
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var shown = Math.Clamp(_motion.Value, 0, 100);
        var knobY = Math.Clamp(YFor(_motion.Value), TopInset - 6, GaugeHeight - BottomInset + 6);
        var color = LiquidPainter.LevelColor(this, shown);
        var ink = (FindResource("BrushTextSecondary") as SolidColorBrush)?.Color ?? Colors.Gray;
        var lineTop = LineTop;
        var lineBottom = GaugeHeight - LineBottomInset;

        double Offset(double y) => LiquidLevelMath.LineOffset(y, knobY, Amplitude, BumpRadius, _motion.Stretch);

        // 1. El brillo, detrás de todo, en el hueco de la comba.
        var glowStrength = Math.Clamp(_motion.Glow, 0, 1.6);
        LiquidPainter.DrawGlow(
            dc,
            new Point(LineX - Amplitude - 3, knobY + (BumpRadius * 0.2 * _motion.Stretch)),
            30 * Math.Min(glowStrength, 1.2),
            40 * (1 + (0.3 * Math.Abs(_motion.Stretch))),
            color,
            glowStrength);

        // 2. La regla: marcas cada 5 % y números cada 25 %, empujadas por la comba y más claras cerca del pomo.
        for (var step = 0; step <= 20; step++)
        {
            var percent = step * 5;
            var y = YFor(percent);
            var push = Offset(y);
            var near = LiquidLevelMath.Bump((y - knobY) / (BumpRadius * 1.7));
            var major = percent % 25 == 0;
            var tickRight = LineX - 12 - (push * 0.95);
            var tickPen = new Pen(new SolidColorBrush(WithAlpha(ink, 0.22 + (0.4 * near))), 1);
            tickPen.Freeze();
            dc.DrawLine(tickPen, new Point(tickRight - (major ? 7 : 4), y), new Point(tickRight, y));

            if (major)
            {
                var label = new FormattedText(
                    percent.ToString(CultureInfo.InvariantCulture),
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    LabelFace,
                    10 + (1.5 * near),
                    new SolidColorBrush(WithAlpha(ink, 0.34 + (0.56 * near))),
                    dpi);
                dc.DrawText(label, new Point(tickRight - 11 - label.Width, y - (label.Height / 2)));
            }
        }

        // 3. La línea con su comba, más viva cerca del pomo que en los extremos.
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(LineX - Offset(lineTop), lineTop), false, false);
            for (var y = lineTop + 2; y <= lineBottom; y += 2)
            {
                context.LineTo(new Point(LineX - Offset(y), y), true, true);
            }
        }

        geometry.Freeze();
        var knobStop = Math.Clamp((knobY - lineTop) / (lineBottom - lineTop), 0, 1);
        var lineBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1),
            MappingMode = BrushMappingMode.RelativeToBoundingBox,
            GradientStops =
            {
                new GradientStop(WithAlpha(color, 0.35), 0),
                new GradientStop(WithAlpha(color, 0.7), Math.Max(0, knobStop - 0.28)),
                new GradientStop(WithAlpha(color, 1), knobStop),
                new GradientStop(WithAlpha(color, 0.7), Math.Min(1, knobStop + 0.28)),
                new GradientStop(WithAlpha(color, 0.35), 1)
            }
        };
        lineBrush.Freeze();
        var linePen = new Pen(lineBrush, 2) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        linePen.Freeze();
        dc.DrawGeometry(null, linePen, geometry);

        // 4. El pomo, oscuro con la flor de Sakura dentro, inflado según su muelle.
        LiquidPainter.DrawKnob(dc, this, new Point(LineX, knobY), KnobRadius * Math.Max(0.1, _motion.Knob), shown, color, _motion.Glow);

        // 5. El número, grande y del color del nivel, que sigue al pomo.
        var number = new FormattedText(
            Math.Round(shown).ToString(CultureInfo.InvariantCulture),
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            NumberFace,
            30,
            new SolidColorBrush(WithAlpha(color, Math.Clamp(0.25 + (0.75 * _motion.Knob), 0, 1))),
            dpi);
        dc.DrawText(number, new Point(LineX + KnobRadius + 9, knobY - (number.Height / 2)));

        // 6. Qué mando es, al pie de la regla.
        var glyphSize = 15d;
        var bounds = _glyph.Bounds;
        var scale = glyphSize / Math.Max(bounds.Width, bounds.Height);
        dc.PushTransform(new TranslateTransform(LineX - (glyphSize / 2) - (bounds.Left * scale), GaugeHeight - 26 - (bounds.Top * scale)));
        dc.PushTransform(new ScaleTransform(scale, scale));
        var glyphBrush = new SolidColorBrush(WithAlpha(ink, 0.8));
        glyphBrush.Freeze();
        if (_solidGlyph)
        {
            dc.DrawGeometry(glyphBrush, null, _glyph);
        }
        else
        {
            var glyphPen = new Pen(glyphBrush, 1.6 / scale) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            glyphPen.Freeze();
            dc.DrawGeometry(null, glyphPen, _glyph);
        }

        dc.Pop();
        dc.Pop();
    }

    private static Color WithAlpha(Color color, double alpha) => LiquidPainter.WithAlpha(color, alpha);
}
