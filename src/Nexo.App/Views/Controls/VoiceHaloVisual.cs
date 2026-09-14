using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Nexo.Core.Voice;

namespace Nexo.App.Views.Controls;

/// <summary>
/// Diseño D72 — dibuja el halo: la marca de Sakura respirando y los anillos que se expanden.
///
/// Es un solo elemento que pinta, no un árbol de figuras. La tentación en WPF es crear una
/// <c>Ellipse</c> por anillo y animar sus propiedades; con cinco anillos naciendo y muriendo cada
/// segundo eso son decenas de elementos entrando y saliendo del árbol visual, cada uno con su
/// medida, su organización y su animación. Aquí todo cabe en un <c>OnRender</c> que dibuja cinco
/// arcos y una silueta.
///
/// Las brochas y la geometría se congelan: una brocha sin congelar se comprueba por hilo en cada
/// acceso, y aquí se accede sesenta veces por segundo.
///
/// 2026-09-14 — la marca va dentro de una burbuja (Adler: «casi no se ve»). Era el contorno fino de
/// la flor en el color de acento, directamente sobre el escritorio: sobre un fondo claro o del mismo
/// tono desaparecía. Ahora es un círculo oscuro con borde y sombra, como las píldoras, con los pétalos
/// macizos y el destello dentro, y los anillos nacen desde su borde.
/// </summary>
public sealed class VoiceHaloVisual : FrameworkElement
{
    /// <summary>Radio en el que nace un anillo, como fracción del lado del control.</summary>
    private const double InnerRadiusRatio = 0.17;

    /// <summary>Radio al que llega antes de desaparecer.</summary>
    private const double OuterRadiusRatio = 0.46;

    /// <summary>Lo que crece la marca entre el silencio y el máximo.</summary>
    private const double MarkGrowth = 0.14;

    /// <summary>
    /// Diseño D73 — la marca va ladeada, quieta.
    ///
    /// La primera versión la hacía girar despacio y Adler lo corrigió: pedía inclinarla un poco, no
    /// ponerla a dar vueltas. Tenía razón — un giro continuo en mitad de la pantalla pide atención
    /// todo el rato, y lo que tiene que llamar la atención aquí son los anillos, que sí responden a
    /// algo. Una inclinación fija hace lo que él quería: quita la simetría perfecta, que es lo que
    /// hacía que la flor pareciera pegada en vez de posada.
    /// </summary>
    private const double MarkTilt = 14;

    /// <summary>Radio de la burbuja respecto al radio interior de los anillos.</summary>
    private const double BubbleRatio = 1.3;

    private readonly VoiceHaloPolicy _halo = new();
    private Geometry? _mark;
    private Geometry? _petals;
    private Geometry? _spark;
    private Brush _markBrush = Brushes.Transparent;
    private Color _ringColor = Colors.White;

    public VoiceHaloVisual()
    {
        IsHitTestVisible = false;
        Focusable = false;
    }

    /// <summary>Nivel crudo del micrófono, de 0 a 1. Lo escribe quien recibe el audio.</summary>
    public double RawLevel { get; set; }

    /// <summary>Nivel ya suavizado. Sirve para que la ventana sepa si aún hay algo que enseñar.</summary>
    public double Level => _halo.Level;

    /// <summary>
    /// 2026-09-14 — mientras se prepara la voz por primera vez: cuánto va la descarga, de 0 a 1. Con
    /// valor, la burbuja enseña un anillo de progreso en vez de las ondas de la voz, y
    /// <see cref="Caption"/> debajo. Nulo es el halo de siempre.
    /// </summary>
    public double? PreparingFraction { get; set; }

    /// <summary>El texto bajo la burbuja mientras se prepara la voz.</summary>
    public string? Caption { get; set; }

    private double _shownFraction;

    /// <summary>
    /// La marca y el color se pasan desde fuera para que este control no sepa nada del tema ni de
    /// los diccionarios de recursos: recibe una geometría y un color, y dibuja.
    /// </summary>
    public void Configure(Geometry mark, Color accent)
    {
        ArgumentNullException.ThrowIfNull(mark);

        _mark = mark.Clone();
        _mark.Freeze();

        // De la marca se usan la silueta exterior —pétalos macizos— y el destello, igual que en el
        // pomo de los mandos líquidos. Si llega otra geometría, se dibuja tal cual.
        _petals = null;
        _spark = null;
        if (mark is GeometryGroup { Children.Count: >= 2 } group &&
            group.Children[0] is PathGeometry { Figures.Count: > 0 } outline &&
            group.Children[1] is PathGeometry spark)
        {
            // Las piezas sueltas pierden la escala del grupo (de la retícula de 48 a la de 24), y sin
            // ella la flor salía al doble de tamaño y descentrada: se les devuelve.
            var petals = new PathGeometry([outline.Figures[0].Clone()]) { Transform = group.Transform?.Clone() };
            petals.Freeze();
            var sparkCopy = spark.Clone();
            sparkCopy.Transform = group.Transform?.Clone();
            sparkCopy.Freeze();
            _petals = petals;
            _spark = sparkCopy;
        }

        _ringColor = accent;

        var brush = new SolidColorBrush(accent);
        brush.Freeze();
        _markBrush = brush;
    }

    /// <summary>Avanza un fotograma y pide repintado. Lo llama <c>CompositionTarget.Rendering</c>.</summary>
    public void Advance(TimeSpan elapsed)
    {
        _halo.Advance(RawLevel, elapsed);

        // El anillo llega a su valor deslizándose: la descarga avisa a saltos de 4 MB, y dibujar esos
        // saltos tal cual hace que el progreso parezca atascarse y dar tirones.
        var target = PreparingFraction ?? 0;
        _shownFraction += (target - _shownFraction) * Math.Min(1, elapsed.TotalSeconds * 6);
        InvalidateVisual();
    }

    public void Reset()
    {
        _halo.Reset();
        RawLevel = 0;
        PreparingFraction = null;
        Caption = null;
        _shownFraction = 0;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);

        var side = Math.Min(ActualWidth, ActualHeight);
        if (side <= 0 || _mark is null)
        {
            return;
        }

        var centre = new Point(ActualWidth / 2, ActualHeight / 2);
        var level = Math.Clamp(_halo.Level, 0, 1);
        var bubble = side * InnerRadiusRatio * BubbleRatio * (1 + (0.06 * level));
        var inner = bubble;
        var outer = side * OuterRadiusRatio;

        foreach (var ring in PreparingFraction is null ? _halo.Rings : [])
        {
            // El radio avanza con una curva que empieza rápida y se frena: es como se comporta una
            // onda al abrirse, y evita que los anillos se vean equiespaciados como un blanco de tiro.
            var eased = 1 - Math.Pow(1 - ring.Progress, 3);
            var radius = inner + ((outer - inner) * eased);

            // Se desvanece con lo que le queda de vida y con la fuerza que tenía la voz al nacer.
            var opacity = (1 - ring.Progress) * 0.42 * Math.Clamp(0.35 + ring.Strength, 0, 1);
            if (opacity <= 0.01)
            {
                continue;
            }

            var pen = new Pen(
                new SolidColorBrush(_ringColor) { Opacity = opacity },
                Math.Max(1, side * 0.008 * (1 - ring.Progress)));
            pen.Freeze();

            drawingContext.DrawEllipse(null, pen, centre, radius, radius);
        }

        // El resplandor detrás crece con la voz. Siempre hay un poco, aunque haya silencio: es lo que
        // separa la burbuja de un fondo del mismo tono.
        var glow = new RadialGradientBrush(
            Color.FromArgb((byte)(60 + (90 * level)), _ringColor.R, _ringColor.G, _ringColor.B),
            Colors.Transparent);
        glow.Freeze();
        drawingContext.DrawEllipse(glow, null, centre, bubble * 1.9, bubble * 1.9);

        // La burbuja: sombra, cuerpo oscuro y un canto del color de acento.
        var shadow = new RadialGradientBrush(Color.FromArgb(120, 0, 0, 0), Colors.Transparent);
        shadow.Freeze();
        drawingContext.DrawEllipse(shadow, null, new Point(centre.X, centre.Y + (bubble * 0.14)), bubble * 1.22, bubble * 1.22);

        var body = new RadialGradientBrush(Color.FromArgb(242, 0x2E, 0x26, 0x2F), Color.FromArgb(242, 0x17, 0x14, 0x1A))
        {
            GradientOrigin = new Point(0.35, 0.3)
        };
        body.Freeze();
        var rim = new Pen(new SolidColorBrush(_ringColor) { Opacity = 0.55 + (0.35 * level) }, Math.Max(1.5, side * 0.008));
        rim.Freeze();
        drawingContext.DrawEllipse(body, rim, centre, bubble, bubble);

        if (PreparingFraction is not null)
        {
            DrawPreparation(drawingContext, centre, bubble, side);
        }

        // La marca respira: entre el silencio y el máximo crece un catorce por ciento. Se escala
        // desde el centro, así que no toca la medida de nada.
        var scale = 1 + (MarkGrowth * level);
        var markSide = bubble * 1.12 * scale;

        // La inclinación va alrededor del centro de la marca, no del origen del control: rotar
        // sobre la esquina la mandaría de paseo por la pantalla.
        drawingContext.PushTransform(new RotateTransform(MarkTilt, centre.X, centre.Y));
        drawingContext.PushTransform(new TranslateTransform(
            centre.X - (markSide / 2),
            centre.Y - (markSide / 2)));
        drawingContext.PushTransform(new ScaleTransform(
            markSide / _mark.Bounds.Width,
            markSide / _mark.Bounds.Height));
        drawingContext.PushTransform(new TranslateTransform(-_mark.Bounds.X, -_mark.Bounds.Y));

        if (_petals is not null && _spark is not null)
        {
            drawingContext.DrawGeometry(_markBrush, null, _petals);
            var sparkBrush = new SolidColorBrush(Color.FromRgb(0x1A, 0x16, 0x1C));
            sparkBrush.Freeze();
            drawingContext.DrawGeometry(sparkBrush, null, _spark);
        }
        else
        {
            drawingContext.DrawGeometry(_markBrush, null, _mark);
        }

        drawingContext.Pop();
        drawingContext.Pop();
        drawingContext.Pop();
        drawingContext.Pop();
    }
    /// <summary>El anillo de la descarga alrededor de la burbuja, y el texto debajo.</summary>
    private void DrawPreparation(DrawingContext drawingContext, Point centre, double bubble, double side)
    {
        var radius = bubble + (side * 0.035);
        var thickness = Math.Max(3, side * 0.016);

        var track = new Pen(new SolidColorBrush(_ringColor) { Opacity = 0.2 }, thickness);
        track.Freeze();
        drawingContext.DrawEllipse(null, track, centre, radius, radius);

        var fraction = Math.Clamp(_shownFraction, 0, 0.999);
        if (fraction > 0.002)
        {
            var angle = fraction * 2 * Math.PI;
            var start = new Point(centre.X, centre.Y - radius);
            var end = new Point(centre.X + (radius * Math.Sin(angle)), centre.Y - (radius * Math.Cos(angle)));
            var arc = new StreamGeometry();
            using (var context = arc.Open())
            {
                context.BeginFigure(start, false, false);
                context.ArcTo(end, new Size(radius, radius), 0, fraction > 0.5, SweepDirection.Clockwise, true, false);
            }

            arc.Freeze();
            var pen = new Pen(new SolidColorBrush(_ringColor), thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            pen.Freeze();
            drawingContext.DrawGeometry(null, pen, arc);
        }

        if (string.IsNullOrWhiteSpace(Caption))
        {
            return;
        }

        var text = new FormattedText(
            Caption,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI Variable Text, Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
            12,
            Brushes.White,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        // Sobre una pastilla oscura: el texto va encima del escritorio, y sobre un fondo claro sin ella
        // no se leería.
        var top = centre.Y + radius + 10;
        var plate = new Rect(centre.X - (text.Width / 2) - 10, top, text.Width + 20, text.Height + 6);
        var plateBrush = new SolidColorBrush(Color.FromArgb(230, 0x1A, 0x16, 0x1C));
        plateBrush.Freeze();
        drawingContext.DrawRoundedRectangle(plateBrush, null, plate, plate.Height / 2, plate.Height / 2);
        drawingContext.DrawText(text, new Point(centre.X - (text.Width / 2), top + 3));
    }
}
