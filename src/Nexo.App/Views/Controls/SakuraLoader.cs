using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Nexo.App.Motion;

namespace Nexo.App.Views.Controls;

/// <summary>
/// 2026-09-15 — lo que se ve mientras Sakura piensa (Adler, con un vídeo de cargadores CSS). Es el
/// quinto de aquel vídeo, llevado a la marca: cuatro puntos como los pétalos de la flor giran, se
/// juntan en el centro y vuelven a abrirse. Dos llevan el acento y dos van apagados, para que el giro
/// se lea.
///
/// La animación solo corre mientras el control se ve: una animación sin fin en algo oculto seguiría
/// pidiendo fotogramas. Con las animaciones desactivadas se queda quieto, abierto.
/// </summary>
public sealed class SakuraLoader : FrameworkElement
{
    private static readonly Duration Cycle = TimeSpan.FromMilliseconds(1250);

    public static readonly DependencyProperty PhaseProperty = DependencyProperty.Register(
        nameof(Phase), typeof(double), typeof(SakuraLoader),
        new FrameworkPropertyMetadata(0.25d, FrameworkPropertyMetadataOptions.AffectsRender));

    // Se crea antes que nada en el constructor: cambiar IsHitTestVisible o Focusable ya recorre los
    // hijos visuales.
    private readonly VisualCollection _children;
    private readonly Ellipse[] _dots = new Ellipse[4];

    public SakuraLoader()
    {
        _children = new VisualCollection(this);
        Width = 22;
        Height = 22;
        Focusable = false;
        IsHitTestVisible = false;

        for (var i = 0; i < _dots.Length; i++)
        {
            _dots[i] = new Ellipse { Width = 6, Height = 6 };
            _dots[i].SetResourceReference(Shape.FillProperty, i % 2 == 0 ? "BrushAccent" : "BrushTextTertiary");
            _children.Add(_dots[i]);
        }

        IsVisibleChanged += (_, _) => UpdateRunning();
        Loaded += (_, _) => UpdateRunning();
        Unloaded += (_, _) => BeginAnimation(PhaseProperty, null);
    }

    public double Phase
    {
        get => (double)GetValue(PhaseProperty);
        set => SetValue(PhaseProperty, value);
    }

    protected override int VisualChildrenCount => _children.Count;

    protected override Visual GetVisualChild(int index) => _children[index];

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var dot in _dots)
        {
            dot.Measure(availableSize);
        }

        return new Size(Width, Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var t = Phase - Math.Floor(Phase);
        var size = Math.Min(finalSize.Width, finalSize.Height);
        var dot = _dots[0].Width;
        var center = size / 2;

        // Abiertos al principio y al final del ciclo, juntos en la mitad; y un cuarto de vuelta por ciclo.
        var spread = (size / 2 - dot / 2) * (0.18 + 0.82 * Math.Abs(Math.Cos(Math.PI * t)));
        var turn = t * Math.PI / 2;

        for (var i = 0; i < _dots.Length; i++)
        {
            var angle = turn + i * Math.PI / 2 + Math.PI / 4;
            var x = center + spread * Math.Cos(angle) - dot / 2;
            var y = center + spread * Math.Sin(angle) - dot / 2;
            _dots[i].Arrange(new Rect(x, y, dot, dot));
        }

        return finalSize;
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == PhaseProperty)
        {
            InvalidateArrange();
        }
    }

    private void UpdateRunning()
    {
        BeginAnimation(PhaseProperty, null);
        if (!IsVisible || !IsLoaded || !SakuraMotion.AnimationsEnabled)
        {
            return;
        }

        BeginAnimation(PhaseProperty, new DoubleAnimation(0, 1, Cycle)
        {
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = SakuraMotion.StandardCurve
        });
    }
}
