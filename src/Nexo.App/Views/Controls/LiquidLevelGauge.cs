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

    // Muelles. El valor llega con un pequeño rebote; la comba tiembla más, que es lo que la hace
    // parecer líquida; el pomo se infla al aparecer y al pulsarlo.
    private const double ValueStiffness = 170;
    private const double ValueDamping = 0.74;
    private const double StretchStiffness = 260;
    private const double StretchDamping = 0.42;
    private const double KnobStiffness = 320;
    private const double KnobDamping = 0.5;

    private static readonly Typeface NumberFace = new(
        new FontFamily("Segoe UI Variable Display, Segoe UI"), FontStyles.Normal, FontWeights.Light, FontStretches.Normal);

    private static readonly Typeface LabelFace = new(
        new FontFamily("Segoe UI Variable Text, Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

    private readonly Geometry _glyph;
    private readonly bool _solidGlyph;

    private double _target;
    private double _value;
    private double _velocity;
    private double _stretch;
    private double _stretchVelocity;
    private double _stretchTarget;
    private double _knob = 1;
    private double _knobVelocity;
    private double _knobTarget = 1;
    private double _glow = 1;
    private double _glowVelocity;
    private double _glowTarget = 1;
    private bool _following;
    private bool _ticking;
    private TimeSpan _lastFrame = TimeSpan.MinValue;
    private DateTime _lastFollow = DateTime.MinValue;

    public LiquidLevelGauge(string glyphData, bool solidGlyph)
    {
        _glyph = Geometry.Parse(glyphData);
        _glyph.Freeze();
        _solidGlyph = solidGlyph;

        Width = GaugeWidth;
        Height = GaugeHeight;
        IsHitTestVisible = false;
        Unloaded += (_, _) => StopTicking();
    }

    /// <summary>El valor pedido, que es el que se escribe al equipo; lo dibujado puede ir un poco detrás.</summary>
    public double Target => _target;

    /// <summary>El porcentaje que corresponde a una altura dentro del mando, con el 100 % arriba.</summary>
    public static double PercentAt(double y)
    {
        var length = GaugeHeight - TopInset - BottomInset;
        return Math.Clamp((1 - ((y - TopInset) / length)) * 100, 0, 100);
    }

    /// <summary>
    /// Entrada al abrir el panel: el pomo se infla desde pequeño y el valor sube un tramo corto hasta
    /// su sitio, con el número contando. Un tramo corto y no desde cero: desde cero el volumen parecería
    /// pasar por todos los niveles, y lo que se quiere es que se asiente, no que se recorra.
    /// </summary>
    public void Reveal(double percent)
    {
        _target = Math.Clamp(percent, 0, 100);
        _following = false;

        if (!SakuraMotion.AnimationsEnabled)
        {
            SnapToRest();
            return;
        }

        _value = Math.Max(0, _target - 30);
        _velocity = 0;
        _knob = 0.35;
        _knobVelocity = 0;
        _knobTarget = 1;
        _glow = 0;
        _glowVelocity = 0;
        _glowTarget = 1;
        _stretch = 0;
        _stretchVelocity = 0;
        _stretchTarget = 0;
        StartTicking();
    }

    /// <summary>
    /// Pone un valor. Con <paramref name="follow"/> el pomo va exactamente donde está el dedo —D38: una
    /// barra que se arrastra no interpola— y lo único que se anima es la inercia de la comba.
    /// </summary>
    public void SetLevel(double percent, bool follow)
    {
        var clamped = Math.Clamp(percent, 0, 100);

        if (!SakuraMotion.AnimationsEnabled)
        {
            _target = clamped;
            SnapToRest();
            return;
        }

        if (follow)
        {
            var now = DateTime.UtcNow;
            var seconds = Math.Max((now - _lastFollow).TotalSeconds, 1d / 120);
            var speed = _following ? (clamped - _target) / seconds : 0;
            _lastFollow = now;
            _following = true;

            _target = clamped;
            _value = clamped;
            _velocity = 0;

            // Subir deja la gota detrás, por debajo del pomo; bajar, por encima.
            _stretchTarget = Math.Clamp(speed / 260, -1, 1);
        }
        else
        {
            _following = false;
            _target = clamped;
        }

        StartTicking();
    }

    /// <summary>El pomo se encoge un poco mientras se pulsa y se infla al soltar, como una burbuja.</summary>
    public void SetPressed(bool pressed)
    {
        if (!pressed)
        {
            _following = false;
        }

        if (!SakuraMotion.AnimationsEnabled)
        {
            return;
        }

        _knobTarget = pressed ? 0.86 : 1;
        _glowTarget = pressed ? 1.35 : 1;
        StartTicking();
    }

    private void SnapToRest()
    {
        StopTicking();
        _value = _target;
        _velocity = 0;
        _stretch = _stretchVelocity = _stretchTarget = 0;
        _knob = _knobTarget = 1;
        _knobVelocity = 0;
        _glow = _glowTarget = 1;
        _glowVelocity = 0;
        InvalidateVisual();
    }

    private void StartTicking()
    {
        if (!_ticking)
        {
            _ticking = true;
            _lastFrame = TimeSpan.MinValue;
            CompositionTarget.Rendering += OnRendering;
        }

        InvalidateVisual();
    }

    private void StopTicking()
    {
        if (_ticking)
        {
            _ticking = false;
            CompositionTarget.Rendering -= OnRendering;
        }
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var time = e is RenderingEventArgs rendering ? rendering.RenderingTime : TimeSpan.Zero;

        // Rendering puede avisar dos veces por fotograma con el mismo instante; el segundo no avanza nada.
        if (time == _lastFrame)
        {
            return;
        }

        var dt = _lastFrame == TimeSpan.MinValue ? 1d / 60 : (time - _lastFrame).TotalSeconds;
        _lastFrame = time;

        if (!_following)
        {
            (_value, _velocity) = LiquidLevelMath.SpringStep(_value, _velocity, _target, dt, ValueStiffness, ValueDamping);
            _stretchTarget = Math.Clamp(_velocity / 260, -1, 1);
        }
        else
        {
            // Si el dedo se para, la gota deja de estirarse aunque no llegue otro movimiento.
            _stretchTarget *= Math.Pow(0.02, dt);
        }

        (_stretch, _stretchVelocity) = LiquidLevelMath.SpringStep(_stretch, _stretchVelocity, _stretchTarget, dt, StretchStiffness, StretchDamping);
        (_knob, _knobVelocity) = LiquidLevelMath.SpringStep(_knob, _knobVelocity, _knobTarget, dt, KnobStiffness, KnobDamping);
        (_glow, _glowVelocity) = LiquidLevelMath.SpringStep(_glow, _glowVelocity, _glowTarget, dt, KnobStiffness, 0.9);

        InvalidateVisual();

        var settled =
            LiquidLevelMath.IsSettled(_value, _velocity, _target) &&
            LiquidLevelMath.IsSettled(_stretch, _stretchVelocity, _stretchTarget, 0.004) &&
            Math.Abs(_stretchTarget) < 0.004 &&
            LiquidLevelMath.IsSettled(_knob, _knobVelocity, _knobTarget, 0.003) &&
            LiquidLevelMath.IsSettled(_glow, _glowVelocity, _glowTarget, 0.003);

        if (settled)
        {
            _value = _target;
            _stretch = 0;
            _knob = _knobTarget;
            _glow = _glowTarget;
            StopTicking();
        }
    }

    private static double YFor(double percent) =>
        TopInset + ((1 - (percent / 100)) * (GaugeHeight - TopInset - BottomInset));

    protected override void OnRender(DrawingContext dc)
    {
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var shown = Math.Clamp(_value, 0, 100);
        var knobY = Math.Clamp(YFor(_value), TopInset - 6, GaugeHeight - BottomInset + 6);
        var accent = (FindResource("BrushAccent") as SolidColorBrush)?.Color ?? Color.FromRgb(0xE8, 0x73, 0x9E);
        var rgb = LiquidLevelMath.ColorAt(shown, new RgbColor(accent.R, accent.G, accent.B));
        var color = Color.FromRgb(rgb.R, rgb.G, rgb.B);
        var ink = (FindResource("BrushTextSecondary") as SolidColorBrush)?.Color ?? Colors.Gray;
        var lineTop = LineTop;
        var lineBottom = GaugeHeight - LineBottomInset;

        double Offset(double y) => LiquidLevelMath.LineOffset(y, knobY, Amplitude, BumpRadius, _stretch);

        // 1. El brillo, detrás de todo, en el hueco de la comba.
        var glowStrength = Math.Clamp(_glow, 0, 1.6);
        if (glowStrength > 0.01)
        {
            var glow = new RadialGradientBrush
            {
                GradientOrigin = new Point(0.5, 0.5),
                GradientStops =
                {
                    new GradientStop(WithAlpha(color, 0.62 * glowStrength), 0),
                    new GradientStop(WithAlpha(color, 0.2 * glowStrength), 0.45),
                    new GradientStop(WithAlpha(color, 0), 1)
                }
            };
            glow.Freeze();
            var center = new Point(LineX - Amplitude - 3, knobY + (BumpRadius * 0.2 * _stretch));
            dc.DrawEllipse(glow, null, center, 30 * Math.Min(glowStrength, 1.2), 40 * (1 + (0.3 * Math.Abs(_stretch))));
        }

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
        var radius = KnobRadius * Math.Max(0.1, _knob);
        var knobCenter = new Point(LineX, knobY);
        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(90, 0, 0, 0)), null, new Point(LineX, knobY + 2.5), radius + 1.5, radius + 1.5);
        var body = new LinearGradientBrush(Color.FromRgb(0x3B, 0x3A, 0x3F), Color.FromRgb(0x1C, 0x1B, 0x1F), 90);
        body.Freeze();
        var rim = new Pen(new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)), 1);
        rim.Freeze();
        dc.DrawEllipse(body, rim, knobCenter, radius, radius);

        DrawBlossom(dc, knobCenter, radius, shown, color);

        // 5. El número, grande y del color del nivel, que sigue al pomo.
        var number = new FormattedText(
            Math.Round(shown).ToString(CultureInfo.InvariantCulture),
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            NumberFace,
            30,
            new SolidColorBrush(WithAlpha(color, Math.Clamp(0.25 + (0.75 * _knob), 0, 1))),
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

    private static (Geometry Petals, Geometry Spark)? _blossom;

    /// <summary>
    /// Los pétalos macizos y el destello de la marca, en su retícula de 48. Del contorno de la marca
    /// solo se usa la silueta exterior: el contorno completo es un trazo hueco, y a este tamaño su
    /// grosor quedaría por debajo del píxel y se vería como una mancha borrosa.
    /// </summary>
    private (Geometry Petals, Geometry Spark)? Blossom()
    {
        if (_blossom is { } cached)
        {
            return cached;
        }

        if (TryFindResource("IconSakuraMark") is not GeometryGroup { Children.Count: >= 2 } mark ||
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

    private void DrawBlossom(DrawingContext dc, Point center, double knobRadius, double percent, Color color)
    {
        if (Blossom() is not { } blossom)
        {
            return;
        }

        // La marca ocupa de 2 a 46 en su retícula; se deja un anillo del pomo alrededor.
        var size = knobRadius * 1.5;
        var scale = size / 44;

        dc.PushTransform(new TranslateTransform(center.X, center.Y));
        dc.PushTransform(new RotateTransform(percent * 0.9));
        dc.PushTransform(new ScaleTransform(scale, scale));
        dc.PushTransform(new TranslateTransform(-24, -24));

        var petals = new SolidColorBrush(WithAlpha(color, Math.Clamp(0.5 + (0.2 * (_glow - 1)), 0.4, 0.75)));
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

    private static Color WithAlpha(Color color, double alpha) =>
        Color.FromArgb((byte)Math.Round(Math.Clamp(alpha, 0, 1) * 255), color.R, color.G, color.B);
}
