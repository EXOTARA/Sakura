using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Nexo.App.Motion;
using Nexo.App.Views.Controls;
using Nexo.Core.Voice;

namespace Nexo.App;

/// <summary>
/// Diseño D72 — la ventana que enseña el halo cuando Sakura oye su nombre.
///
/// Aparece con la palabra de activación, respira mientras escucha y se va cuando hay respuesta. No
/// se queda flotando: un adorno permanente en mitad de la pantalla deja de ser una señal y pasa a
/// ser un estorbo.
///
/// El fotograma lo marca <see cref="CompositionTarget.Rendering"/> y no un temporizador. Un
/// <c>DispatcherTimer</c> a sesenta hercios compite con el render y no está sincronizado con el
/// refresco de la pantalla: se ven saltos aunque el trabajo por fotograma sea trivial. Rendering se
/// dispara justo antes de componer, que es exactamente cuando hay que mover algo.
/// </summary>
public partial class VoiceHaloWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExTransparent = 0x00000020;

    /// <summary>Lo que sube al entrar. Corto: un recorrido largo se lee como lentitud.</summary>
    private const double RiseDistance = 34;

    /// <summary>Separación con el borde inferior del área de trabajo.</summary>
    private const double BottomMargin = 28;

    private readonly VoiceHaloVisual _visual = new();
    private TimeSpan _lastFrame;
    private bool _running;
    private bool _dismissing;

    public VoiceHaloWindow()
    {
        InitializeComponent();
        HaloRoot.Children.Add(_visual);
    }

    /// <summary>
    /// Nivel crudo del micrófono. Lo escribe quien recibe el audio, desde el hilo que sea: es una
    /// escritura de un <c>double</c>, y el fotograma siguiente lo recoge. No hace falta marshalling
    /// para esto y sí lo haría falta para tocar cualquier propiedad de dependencia.
    /// </summary>
    public void ReportLevel(double level) => _visual.RawLevel = level;

    /// <summary>Coloca el halo y lo enciende. Idempotente: llamarlo dos veces no reinicia nada.</summary>
    public void ShowHalo(Geometry mark, Color accent)
    {
        _visual.Configure(mark, accent);

        if (_running)
        {
            return;
        }

        _dismissing = false;
        _running = true;
        _visual.Reset();
        PositionOverWorkArea();

        Show();

        // Diseño D73 — entra subiendo. Que aparezca desde abajo dice de dónde viene: del borde
        // donde vive el sistema, no de la nada en mitad de la pantalla. Es el mismo gesto con el
        // que entran las notificaciones de Windows y el que la referencia usa para el cajón, solo
        // que al revés.
        HaloRoot.BeginAnimation(OpacityProperty, null);
        HaloRise.BeginAnimation(TranslateTransform.YProperty, null);
        HaloRoot.Opacity = 0;
        HaloRise.Y = RiseDistance;

        HaloRoot.Animate(OpacityProperty, 1, SakuraMotion.Reveal, SakuraMotion.DecelerateCurve);
        SakuraMotion.AnimateTransform(
            HaloRise,
            TranslateTransform.YProperty,
            0,
            SakuraMotion.Reveal,
            SakuraMotion.DecelerateCurve);

        _lastFrame = TimeSpan.Zero;
        CompositionTarget.Rendering += OnRendering;
    }

    /// <summary>
    /// Lo apaga. La salida dura menos que la entrada, como el resto del movimiento de Sakura, y el
    /// bucle de fotogramas se corta en cuanto empieza a irse: seguir calculando anillos que nadie
    /// va a ver es trabajo por fotograma regalado.
    /// </summary>
    public void HideHalo()
    {
        if (!_running || _dismissing)
        {
            return;
        }

        _dismissing = true;
        CompositionTarget.Rendering -= OnRendering;

        if (!SakuraMotion.AnimationsEnabled)
        {
            Finish();
            return;
        }

        // Y se va por donde vino, con la salida más corta que la entrada.
        SakuraMotion.AnimateTransform(
            HaloRise,
            TranslateTransform.YProperty,
            RiseDistance * 0.6,
            SakuraMotion.Exit,
            SakuraMotion.AccelerateCurve);

        var fade = SakuraMotion.CreateAnimation(0, SakuraMotion.Exit, SakuraMotion.AccelerateCurve);
        fade.Completed += (_, _) => Finish();
        HaloRoot.BeginAnimation(OpacityProperty, fade);
    }

    public void HideImmediately()
    {
        CompositionTarget.Rendering -= OnRendering;
        Finish();
    }

    private void Finish()
    {
        _running = false;
        _dismissing = false;
        _visual.Reset();
        Hide();
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (e is not RenderingEventArgs args)
        {
            return;
        }

        // Rendering puede dispararse más de una vez con el mismo tiempo; sin este guardia se
        // avanzaría el halo dos veces en el mismo fotograma y respiraría al doble de velocidad.
        if (args.RenderingTime == _lastFrame)
        {
            return;
        }

        var elapsed = _lastFrame == TimeSpan.Zero
            ? TimeSpan.FromMilliseconds(16)
            : args.RenderingTime - _lastFrame;
        _lastFrame = args.RenderingTime;

        _visual.Advance(elapsed);
    }

    /// <summary>
    /// Centrado horizontalmente y en el tercio inferior del área de trabajo: donde no tapa lo que
    /// estés leyendo y donde el ojo ya busca las notificaciones del sistema.
    /// </summary>
    private void PositionOverWorkArea()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + ((workArea.Width - Width) / 2);

        // Diseño D73 — abajo del todo, con un margen igual al que deja cualquier notificación del
        // sistema. Estaba a dos tercios de la altura y Adler lo bajó: ahí en medio compite con lo
        // que estés leyendo, y pegado al borde inferior se lee como lo que es, un aviso.
        Top = workArea.Bottom - Height - BottomMargin;
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        // Sin activación, sin barra de tareas y transparente a los clics: el halo se ve y no
        // participa. WPF ya no acepta foco por IsHitTestVisible, pero eso no impide que la ventana
        // se lleve la pulsación a nivel de Windows; WS_EX_TRANSPARENT sí.
        var style = GetWindowLong(handle, GwlExStyle);
        SetWindowLong(handle, GwlExStyle, style | WsExNoActivate | WsExToolWindow | WsExTransparent);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);
}
