using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Nexo.App.Motion;
using Nexo.Core.Shell;

namespace Nexo.App.Views.Controls;

/// <summary>
/// El dibujo del deslizador líquido horizontal, para los volúmenes de la pestaña Audio (Adler,
/// 2026-09-14: «los del panel derecho en el apartado volumen» también, no solo los controles rápidos).
///
/// Es solo el dibujo: va dentro de la plantilla <c>LiquidSliderStyle</c> de un <see cref="Slider"/>
/// normal, que sigue poniendo el teclado, la rueda, el lector de pantalla y el arrastre con su
/// <see cref="Track"/>. Así la pestaña Audio no cambia ni una línea de su lógica: el valor, los
/// eventos y el guardado siguen siendo los del Slider de siempre.
///
/// La línea se abomba hacia abajo alrededor del pomo, con el tramo ya recorrido del color del nivel y
/// el resto apagado. Mientras se arrastra, el pomo va exactamente donde está el puntero; cuando el
/// valor cambia solo —al refrescar el mezclador— llega con el mismo muelle que los controles rápidos.
/// </summary>
public sealed class LiquidSliderTrack : FrameworkElement
{
    /// <summary>
    /// Ancho del pulgar invisible del Track. El pomo se dibuja en el centro de ese pulgar, así que el
    /// recorrido del dibujo coincide con el del arrastre.
    /// </summary>
    public const double ThumbWidth = 26;

    private const double LineY = 14;
    private const double KnobRadius = 10;
    private const double Amplitude = 14;
    private const double BumpRadius = 32;

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(LiquidSliderTrack), new PropertyMetadata(0d, OnRangeChanged));

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum), typeof(double), typeof(LiquidSliderTrack), new PropertyMetadata(0d, OnRangeChanged));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(LiquidSliderTrack), new PropertyMetadata(100d, OnRangeChanged));

    private readonly LiquidMotion _motion;
    private Slider? _owner;
    private bool _shownOnce;

    public LiquidSliderTrack()
    {
        _motion = new LiquidMotion(InvalidateVisual);
        IsHitTestVisible = false;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    private double Percent =>
        Maximum > Minimum ? Math.Clamp((Value - Minimum) / (Maximum - Minimum) * 100, 0, 100) : 0;

    private static void OnRangeChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is not LiquidSliderTrack track)
        {
            return;
        }

        if (!track._shownOnce)
        {
            track._motion.SnapTo(track.Percent);
            return;
        }

        // Si la persona lo está moviendo, el pomo va con ella; si cambia solo, llega con muelle.
        var follow = track._owner is { } owner && (owner.IsMouseCaptureWithin || owner.IsKeyboardFocusWithin);
        track._motion.SetLevel(track.Percent, follow);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _owner = TemplatedParent as Slider;
        if (_owner is not null)
        {
            _owner.PreviewMouseLeftButtonDown += OnOwnerPressed;
            _owner.LostMouseCapture += OnOwnerReleased;
            _owner.PreviewMouseLeftButtonUp += OnOwnerReleased;
        }

        _motion.SnapTo(Percent);
        _shownOnce = true;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _motion.Stop();
        if (_owner is not null)
        {
            _owner.PreviewMouseLeftButtonDown -= OnOwnerPressed;
            _owner.LostMouseCapture -= OnOwnerReleased;
            _owner.PreviewMouseLeftButtonUp -= OnOwnerReleased;
        }

        _shownOnce = false;
    }

    private void OnOwnerPressed(object sender, MouseButtonEventArgs e) => _motion.SetPressed(true);

    private void OnOwnerReleased(object sender, MouseEventArgs e) => _motion.SetPressed(false);

    protected override Size MeasureOverride(Size availableSize) => new(0, LineY + Amplitude + 14);

    protected override void OnRender(DrawingContext dc)
    {
        var width = ActualWidth;
        if (width <= ThumbWidth)
        {
            return;
        }

        var shown = Math.Clamp(_motion.Value, 0, 100);
        var start = ThumbWidth / 2;
        var end = width - (ThumbWidth / 2);
        var knobX = start + ((end - start) * Math.Clamp(_motion.Value, -2, 102) / 100);
        var color = LiquidPainter.LevelColor(this, shown);
        var dim = (TryFindResource("BrushTextSecondary") as SolidColorBrush)?.Color ?? Colors.Gray;

        // Horizontal: se pasan las x con el signo cambiado, para que al subir el valor —el pomo va a la
        // derecha— la cola de la gota quede a la izquierda, detrás de él.
        double Offset(double x) => LiquidLevelMath.LineOffset(-x, -knobX, Amplitude, BumpRadius, _motion.Stretch);

        // 1. El brillo, bajo la comba.
        var glowStrength = Math.Clamp(_motion.Glow, 0, 1.6);
        LiquidPainter.DrawGlow(
            dc,
            new Point(knobX - (BumpRadius * 0.2 * _motion.Stretch), LineY + Amplitude + 2),
            34 * (1 + (0.3 * Math.Abs(_motion.Stretch))),
            16 * Math.Min(glowStrength, 1.2),
            color,
            glowStrength * 0.8);

        // 2. La línea: recorrida del color del nivel hasta el pomo, apagada después.
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(0, LineY + Offset(0)), false, false);
            for (var x = 2d; x <= width; x += 2)
            {
                context.LineTo(new Point(x, LineY + Offset(x)), true, true);
            }
        }

        geometry.Freeze();
        var split = Math.Clamp(knobX / width, 0, 1);
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 0),
            MappingMode = BrushMappingMode.RelativeToBoundingBox,
            GradientStops =
            {
                new GradientStop(LiquidPainter.WithAlpha(color, 0.75), 0),
                new GradientStop(LiquidPainter.WithAlpha(color, 1), split),
                new GradientStop(LiquidPainter.WithAlpha(dim, 0.28), split),
                new GradientStop(LiquidPainter.WithAlpha(dim, 0.2), 1)
            }
        };
        brush.Freeze();
        var pen = new Pen(brush, 2.5) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        pen.Freeze();
        dc.DrawGeometry(null, pen, geometry);

        // 3. El pomo con la flor.
        LiquidPainter.DrawKnob(dc, this, new Point(knobX, LineY), KnobRadius * Math.Max(0.1, _motion.Knob), shown, color, _motion.Glow);
    }
}
