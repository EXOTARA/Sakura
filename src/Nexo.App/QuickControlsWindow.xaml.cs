using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Nexo.App.Motion;
using Nexo.Core.Settings;
using Nexo.Core.Shell;

namespace Nexo.App;

/// <summary>
/// Diseño D35 — volumen y brillo al llevar el ratón al borde contrario a Sakura.
///
/// Los mandos van en el borde opuesto por una razón concreta: Sakura ya ocupa el suyo, y compartirlo
/// obligaría a que el mismo gesto decidiera cuál de las dos cosas aparece. Separados, el reparto se
/// entiende sin explicarlo.
///
/// Se controla pulsando a la altura que se quiere, no arrastrando desde el pulgar, que es como
/// funciona el volumen de un teléfono: llegar al borde y pulsar donde toca son dos gestos seguidos,
/// y obligar a agarrar exactamente el pulgar convertiría un atajo en una tarea de puntería.
/// </summary>
public partial class QuickControlsWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;

    private const double TrackHeight = 164;
    private const double TrackWidth = 52;

    /// <summary>
    /// Se cierra solo al rato de dejar de usarlo. Sin esto se quedaría encima de todo hasta que
    /// alguien se acordara de apartarlo, que es justo lo que un panel de borde no debe pedir.
    /// </summary>
    private static readonly TimeSpan IdleDismiss = TimeSpan.FromSeconds(4);

    private readonly DispatcherTimer _idleTimer;
    private readonly Dictionary<QuickControlKind, Border> _fills = new();

    private bool _dismissing;
    private QuickControlKind? _dragging;

    /// <summary>
    /// Hacia dónde se va el panel al cerrarse: el mismo desplazamiento por el que entró, para que
    /// salga por donde vino.
    /// </summary>
    private double _edgeOffset;

    public QuickControlsWindow()
    {
        InitializeComponent();

        _idleTimer = new DispatcherTimer { Interval = IdleDismiss };
        _idleTimer.Tick += (_, _) => Dismiss();

        SourceInitialized += OnSourceInitialized;
        MouseEnter += (_, _) => _idleTimer.Stop();
        MouseLeave += (_, _) => RestartIdle();
        MouseLeftButtonUp += (_, _) =>
        {
            _dragging = null;
            FlushPendingWrite();
        };
        MouseMove += OnMouseMove;
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Dismiss();
            }
        };
    }

    /// <summary>Se pidió mover un mando. El porcentaje ya viene normalizado.</summary>
    public event EventHandler<QuickControlChangedEventArgs>? ControlChanged;

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        var styles = GetWindowLong(handle, GwlExStyle);
        SetWindowLong(handle, GwlExStyle, styles | WsExNoActivate | WsExToolWindow);
    }

    /// <summary>
    /// Enseña el panel con los mandos que de verdad pueden hacer algo. Si no hay ninguno no se
    /// muestra nada: un panel con dos barras muertas es peor que ningún panel, porque hay que
    /// probarlas para descubrir que no hacen nada.
    /// </summary>
    public void ShowControls(
        SidebarPosition kohanaSide,
        double volumePercent,
        bool volumeAvailable,
        double brightnessPercent,
        bool brightnessAvailable)
    {
        var kinds = QuickControlsPolicy.VisibleControls(volumeAvailable, brightnessAvailable);
        if (kinds.Count == 0)
        {
            return;
        }

        _dismissing = false;
        _dragging = null;
        IsHitTestVisible = true;

        BuildControls(kinds);
        SetLevel(QuickControlKind.Volume, volumePercent);
        SetLevel(QuickControlKind.Brightness, brightnessPercent);

        var edge = QuickControlsPolicy.ControlsEdgeFor(kohanaSide);
        Position(edge);

        if (!IsVisible)
        {
            Show();
        }

        PanelBorder.BeginAnimation(OpacityProperty, null);

        // Entra desde su propio borde, como Sakura desde el suyo: el movimiento dice de dónde viene.
        var offset = edge == SidebarPosition.Left ? -28d : 28d;
        _edgeOffset = offset;

        if (!SakuraMotion.AnimationsEnabled)
        {
            PanelBorder.Opacity = 1;
            PanelTranslate.X = 0;
        }
        else
        {
            PanelBorder.Opacity = 0;
            PanelTranslate.X = offset;
            PanelBorder.Animate(OpacityProperty, 1, SakuraMotion.Reveal, SakuraMotion.DecelerateCurve);
            PanelTranslate.AnimateTransform(
                TranslateTransform.XProperty, 0, SakuraMotion.Emphasized, SakuraMotion.EmphasizedCurve);
        }

        RestartIdle();
    }

    public void HideImmediately()
    {
        _idleTimer.Stop();
        _dragging = null;
        _dismissing = false;
        IsHitTestVisible = false;
        PanelBorder.BeginAnimation(OpacityProperty, null);
        Hide();
    }

    /// <summary>Refresca el nivel enseñado sin reconstruir nada, para eco tras un cambio externo.</summary>
    public void SetLevel(QuickControlKind kind, double percent) =>
        SetLevel(kind, percent, animate: true);

    /// <summary>
    /// Diseño D38 — mientras se arrastra, la barra sigue al dedo sin animación.
    ///
    /// Animar cada movimiento del ratón era la mitad del tirón: el puntero emite eventos decenas de
    /// veces por segundo y cada uno arrancaba una animación nueva sobre la misma propiedad, así que
    /// se pisaban unas a otras y la barra iba a rastras del cursor en vez de pegada a él. Una barra
    /// que se arrastra tiene que ir donde va el dedo, no interpolar hacia allí; la animación es para
    /// cuando el valor cambia solo —al abrir el panel o con la rueda—, que es cuando el movimiento
    /// explica algo.
    /// </summary>
    private void SetLevel(QuickControlKind kind, double percent, bool animate)
    {
        if (!_fills.TryGetValue(kind, out var fill))
        {
            return;
        }

        var normalized = QuickControlsPolicy.NormalizePercent(percent);
        var target = TrackHeight * normalized / 100.0;

        if (!animate || !SakuraMotion.AnimationsEnabled)
        {
            fill.BeginAnimation(HeightProperty, null);
            fill.Height = target;
            return;
        }

        var animation = SakuraMotion.CreateAnimation(
            target, SakuraMotion.Reveal, SakuraMotion.DecelerateCurve);
        fill.BeginAnimation(HeightProperty, animation);
    }

    private void BuildControls(IReadOnlyList<QuickControlKind> kinds)
    {
        ControlsPanel.Children.Clear();
        _fills.Clear();

        for (var index = 0; index < kinds.Count; index++)
        {
            var kind = kinds[index];

            var track = new Border
            {
                Style = (Style)FindResource("ControlTrackStyle"),
                Margin = new Thickness(0, index == 0 ? 0 : 12, 0, 0),
                Tag = kind,
                Cursor = Cursors.Hand,

                // Diseño D39 — el recorte es una geometría redondeada y no ClipToBounds.
                //
                // ClipToBounds recorta al RECTÁNGULO de la caja, sin enterarse del radio: el
                // relleno tiene sus esquinas redondeadas, pero al llegar arriba se le cortaban
                // contra un borde recto y la píldora se veía cuadrada por dos lados. Es justo el
                // defecto que se reportó. Una geometría con el mismo radio recorta por donde el
                // ojo espera.
                Clip = new RectangleGeometry(
                    new Rect(0, 0, TrackWidth, TrackHeight),
                    TrackWidth / 2,
                    TrackWidth / 2)
            };

            // El relleno va anclado abajo para que crezca hacia arriba, como el boceto. Su propio
            // radio es el mismo que el del carril: así, medio lleno, el borde de arriba es una
            // cúpula y no un corte.
            var fill = new Border
            {
                Width = TrackWidth,
                Height = 0,
                VerticalAlignment = VerticalAlignment.Bottom,
                CornerRadius = new CornerRadius(TrackWidth / 2),
                Background = (Brush)FindResource("BrushAccent")
            };

            // El altavoz es una silueta maciza y el sol son trazos: pintar los dos igual dejaba el
            // brillo como un punto, porque unos rayos dibujados como líneas no tienen interior que
            // rellenar.
            var isSolid = kind == QuickControlKind.Volume;
            var inkBrush = (Brush)FindResource("BrushTextPrimary");

            var icon = new Path
            {
                Width = 18,
                Height = 18,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 14),
                HorizontalAlignment = HorizontalAlignment.Center,
                Fill = isSolid ? inkBrush : null,
                Stroke = isSolid ? null : inkBrush,
                StrokeThickness = isSolid ? 0 : 2,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Data = Geometry.Parse(isSolid ? VolumeGlyph : BrightnessGlyph),
                IsHitTestVisible = false
            };

            var layers = new Grid();
            layers.Children.Add(fill);
            layers.Children.Add(icon);
            track.Child = layers;

            track.MouseLeftButtonDown += OnTrackPressed;
            track.MouseLeftButtonUp += OnTrackReleased;
            track.LostMouseCapture += OnTrackLostCapture;
            track.MouseWheel += OnTrackWheel;

            ControlsPanel.Children.Add(track);
            _fills[kind] = fill;
        }
    }

    /// <summary>
    /// Diseño D71 — pulsar en cualquier punto del carril es el principio de un arrastre, no un
    /// salto animado.
    ///
    /// Antes esta pulsación se trataba como un cambio suelto: la barra **se animaba** hacia el punto
    /// pulsado y, en cuanto el ratón se movía un píxel, el siguiente evento cancelaba esa animación
    /// y la pegaba al cursor. Animar y luego saltar es lo que Adler describió como "actúa raro" al
    /// pulsar fuera del círculo. Una barra que se agarra tiene que ir donde está el dedo desde el
    /// primer fotograma.
    ///
    /// Y se captura el ratón. El carril mide cincuenta y dos píxeles de ancho: soltar el botón un
    /// poco a la derecha era soltarlo fuera de la ventana, así que el <c>MouseLeftButtonUp</c> no
    /// llegaba nunca y el arrastre se quedaba abierto. El siguiente movimiento del puntero por
    /// encima del panel volvía a mover el volumen sin que nadie hubiera pulsado nada.
    /// </summary>
    private void OnTrackPressed(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { Tag: QuickControlKind kind } track)
        {
            return;
        }

        _dragging = kind;
        track.CaptureMouse();
        e.Handled = true;

        // Sigue al dedo sin animación, pero escribe al hardware ya: una pulsación suelta es una
        // orden completa, no un tramo intermedio de arrastre.
        SetLevel(kind, PercentFromPointer(track, e.GetPosition(track).Y), animate: false);
        WriteNow(kind);
    }

    private void OnTrackReleased(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border track && track.IsMouseCaptured)
        {
            track.ReleaseMouseCapture();
        }

        _dragging = null;
        FlushPendingWrite();
    }

    /// <summary>
    /// Si algo le quita la captura al carril —otra ventana, un cambio de escritorio, Alt+Tab— el
    /// arrastre termina aquí. Sin esto, la captura perdida dejaba <see cref="_dragging"/> puesto.
    /// </summary>
    private void OnTrackLostCapture(object sender, MouseEventArgs e)
    {
        _dragging = null;
        FlushPendingWrite();
    }

    private static double PercentFromPointer(Border track, double pointerY)
    {
        // Arriba del carril es el 100%: la coordenada crece hacia abajo, así que se invierte.
        var height = track.ActualHeight > 0 ? track.ActualHeight : TrackHeight;
        return (1 - (pointerY / height)) * 100.0;
    }

    private void WriteNow(QuickControlKind kind)
    {
        if (!_fills.TryGetValue(kind, out var fill))
        {
            return;
        }

        _lastHardwareWrite = DateTime.UtcNow;
        _pendingWrite = null;
        RestartIdle();
        ControlChanged?.Invoke(
            this,
            new QuickControlChangedEventArgs(
                kind,
                QuickControlsPolicy.NormalizePercent(fill.Height / TrackHeight * 100.0)));
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragging is not { } kind ||
            e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        foreach (Border track in ControlsPanel.Children)
        {
            if (track.Tag is QuickControlKind trackKind && trackKind == kind)
            {
                ApplyFromPointer(kind, track, e.GetPosition(track).Y, dragging: true);
                return;
            }
        }
    }

    private void OnTrackWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not Border { Tag: QuickControlKind kind } ||
            !_fills.TryGetValue(kind, out var fill))
        {
            return;
        }

        var current = fill.Height / TrackHeight * 100.0;
        var step = e.Delta > 0 ? 5 : -5;
        Apply(kind, current + step, dragging: false);
    }

    private void ApplyFromPointer(QuickControlKind kind, Border track, double pointerY, bool dragging) =>
        Apply(kind, PercentFromPointer(track, pointerY), dragging);

    /// <summary>
    /// Diseño D38 — la otra mitad del tirón, y la más grave: escribir al hardware en cada
    /// movimiento del ratón.
    ///
    /// Cambiar el brillo por DDC/CI es una conversación por el cable de vídeo y tarda decenas de
    /// milisegundos; hacerlo cien veces por segundo mientras se arrastra bloquea el hilo de
    /// interfaz y congela el panel entero, que es exactamente lo que se veía. Ahora la barra se
    /// mueve siempre —eso es inmediato— y la escritura al hardware se limita a una cada
    /// <see cref="HardwareWriteInterval"/>, con una final al soltar para que el valor acabe siendo
    /// exactamente donde se dejó el dedo y no donde cayó el último aviso.
    /// </summary>
    private static readonly TimeSpan HardwareWriteInterval = TimeSpan.FromMilliseconds(90);

    private DateTime _lastHardwareWrite = DateTime.MinValue;
    private (QuickControlKind Kind, int Percent)? _pendingWrite;

    private void Apply(QuickControlKind kind, double percent, bool dragging)
    {
        var normalized = QuickControlsPolicy.NormalizePercent(percent);
        SetLevel(kind, normalized, animate: !dragging);
        RestartIdle();

        var now = DateTime.UtcNow;
        if (!dragging || now - _lastHardwareWrite >= HardwareWriteInterval)
        {
            _lastHardwareWrite = now;
            _pendingWrite = null;
            ControlChanged?.Invoke(this, new QuickControlChangedEventArgs(kind, normalized));
            return;
        }

        // Se guarda para escribirlo al soltar: sin esto, el último tramo del arrastre se perdería y
        // el valor real quedaría un poco por detrás de donde está la barra.
        _pendingWrite = (kind, normalized);
    }

    private void FlushPendingWrite()
    {
        if (_pendingWrite is not { } pending)
        {
            return;
        }

        _pendingWrite = null;
        _lastHardwareWrite = DateTime.UtcNow;
        ControlChanged?.Invoke(this, new QuickControlChangedEventArgs(pending.Kind, pending.Percent));
    }

    private void RestartIdle()
    {
        _idleTimer.Stop();
        _idleTimer.Start();
    }

    private void Dismiss()
    {
        if (_dismissing)
        {
            return;
        }

        _dismissing = true;
        _idleTimer.Stop();
        _dragging = null;

        // Diseño D57 — deja de aceptar clics en cuanto se decide que se va, no cuando termina
        // de irse. Es la misma protección que D56 le puso al cajón, y esta ventana se oculta
        // igual: al acabar la animación de salida. Una animación puede no acabar nunca —basta
        // con que algo la reemplace a mitad de camino para que su Completed no llegue— y lo que
        // queda entonces es una ventana mostrada con opacidad cero que sigue quedándose con los
        // clics de su trozo de pantalla. Invisible y clicable es el peor estado posible de una
        // ventana: desde fuera se siente como que el ratón dejó de responder ahí, sin nada que
        // lo explique.
        IsHitTestVisible = false;

        // Lo que quedara sin escribir se escribe antes de irse: si no, cerrar el panel a media
        // corrección dejaría el valor real por detrás de la barra que se acaba de ver.
        FlushPendingWrite();

        if (!SakuraMotion.AnimationsEnabled)
        {
            Hide();
            _dismissing = false;
            return;
        }

        // Diseño D38 — al cerrarse vuelve por su borde además de desvanecerse.
        //
        // Antes solo bajaba la opacidad, y la entrada sí desliza: esa asimetría es lo que hacía que
        // el cierre se leyera como «desapareció» en vez de como que se fue. Un desvanecido corto y
        // sin movimiento sobre un panel pequeño casi no se ve; el mismo desvanecido acompañado del
        // gesto de retirada, sí.
        // Diseño D58 — vuelve a meterse en su pared, no se aparta veintiocho píxeles.
        //
        // La entrada llega desde el borde con un gesto corto, y para salir se usaba ese mismo
        // desplazamiento de vuelta. Veintiocho píxeles no se leen como «se ha escondido»: lo que
        // se veía era el panel apagándose en el sitio. La salida recorre ahora su propio ancho,
        // así que desaparece por donde vino.
        var retreat = PanelBorder.ActualWidth > 0 ? PanelBorder.ActualWidth : 260;
        var away = _edgeOffset < 0 ? -retreat : retreat;

        PanelTranslate.BeginAnimation(TranslateTransform.XProperty, null);
        PanelTranslate.AnimateTransform(
            TranslateTransform.XProperty,
            away,
            SakuraMotion.Exit,
            SakuraMotion.AccelerateCurve);

        PanelBorder.Animate(
            OpacityProperty,
            0,
            SakuraMotion.Exit,
            SakuraMotion.AccelerateCurve,
            completed: () =>
            {
                Hide();
                _dismissing = false;
            });
    }

    /// <summary>
    /// Hueco que la ventana reserva alrededor del panel para que quepa la sombra. Tiene que
    /// coincidir con el <c>Margin</c> de PanelBorder en el XAML.
    /// </summary>
    private const double ShadowMargin = 30;

    private void Position(SidebarPosition edge)
    {
        var workArea = SystemParameters.WorkArea;
        const double margin = 18;

        UpdateLayout();
        var height = ActualHeight > 0 ? ActualHeight : 260;

        // Se descuenta el hueco de la sombra: lo que tiene que quedar a la distancia pedida del
        // borde es el panel que se ve, no la ventana que lo contiene. Sin esto, ampliar la ventana
        // para dejar sitio a la sombra habría metido el panel treinta píxeles hacia dentro.
        Top = workArea.Top + Math.Max(0, (workArea.Height - height) / 2);
        Left = edge == SidebarPosition.Left
            ? workArea.Left + margin - ShadowMargin
            : workArea.Right - Width - margin + ShadowMargin;
    }

    private const string VolumeGlyph =
        "M3 9v6h4l5 5V4L7 9H3zm13.5 3c0-1.8-1-3.3-2.5-4v8c1.5-.7 2.5-2.2 2.5-4z";

    private const string BrightnessGlyph =
        "M12 7a5 5 0 1 0 0 10 5 5 0 0 0 0-10zm0-5v3m0 14v3M2 12h3m14 0h3M4.9 4.9l2.1 2.1m10 10 2.1 2.1M19.1 4.9 17 7M7 17l-2.1 2.1";

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr window, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr window, int index, int newLong);
}

public sealed class QuickControlChangedEventArgs(QuickControlKind kind, int percent) : EventArgs
{
    public QuickControlKind Kind { get; } = kind;

    public int Percent { get; } = percent;
}
