using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Nexo.App.Motion;
using Nexo.App.Views;
using Nexo.Core.Ambient;
using Nexo.Core.Voice;
using Nexo.Core.Media;
using Nexo.Core.Metrics;
using Nexo.Core.Shell;
using Nexo.Windows.Media;
using Nexo.Windows.Shell;

namespace Nexo.App;

/// <summary>
/// Diseño D44 — el cajón del panel: baja del centro del borde de arriba, se queda sobre lo que
/// estés haciendo y se va solo al apartar el ratón.
///
/// Es una ventana y no un panel dentro de Sistema porque el gesto es el que define la pieza: en la
/// referencia se tira de ella desde el borde de la pantalla, no se navega hasta ella. Meterla en
/// una vista obligaría a abrir Sakura, ir a Sistema y volver — tres pasos para mirar la hora.
/// </summary>
public partial class DashboardWindow : Window
{
    /// <summary>
    /// Cuánto se tolera el ratón fuera antes de recoger el cajón. Cerrar en cuanto el puntero sale
    /// del borde lo haría desaparecer al ir a pulsar algo de dentro pasando por fuera de una
    /// esquina; esperar más lo dejaría colgando por toda la pantalla.
    /// </summary>
    private static readonly TimeSpan GraceAfterMouseLeaves = TimeSpan.FromMilliseconds(650);

    /// <summary>
    /// Treinta fotogramas por segundo. Sesenta no se distinguen en un anillo de rayos cortos y
    /// duplican el coste de una pieza puramente decorativa; menos de veinticinco y el movimiento
    /// deja de leerse como continuo.
    /// </summary>
    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(33);

    private readonly DispatcherTimer _mouseWatch;
    private readonly DispatcherTimer _audioFrames;
    private readonly WasapiSpectrumSource _spectrumSource = new();
    /// <summary>
    /// Treinta y dos bandas, que con el espejo son sesenta y cuatro rayos.
    ///
    /// Eran el doble y se veía como un fleco: en una circunferencia de unos quinientos ochenta
    /// píxeles, ciento veintiocho rayos de cuatro y medio de grosor no dejan hueco entre uno y
    /// otro, así que el anillo deja de leerse como rayos y pasa a leerse como un borde peludo. Con
    /// sesenta y cuatro, cada rayo tiene tanto espacio como ancho y se distingue del vecino.
    ///
    /// Resolución de frecuencia se pierde poca: treinta y dos bandas siguen siendo más de cuatro
    /// por octava en el rango que se representa, y el ojo no distingue más que eso en un anillo.
    /// </summary>
    private readonly AudioSpectrum _spectrum = new(bandCount: 32);

    private double _wavePhase;
    private DateTimeOffset? _outsideSince;
    private bool _isShown;
    private bool _isClosing;

    public DashboardWindow()
    {
        InitializeComponent();

        _mouseWatch = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(120)
        };
        _mouseWatch.Tick += (_, _) => CheckMouse();

        _audioFrames = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = FrameInterval
        };
        _audioFrames.Tick += (_, _) => RenderAudioFrame();
    }

    /// <summary>
    /// Diseño D46 — el anillo y la onda solo se mueven cuando se ven y hay música.
    ///
    /// La captura de audio se para junto con ellos, y eso es lo que importa: escuchar la salida del
    /// sistema cuesta poco, pero mantenerlo abierto con el cajón cerrado sería cobrar ese poco las
    /// veinticuatro horas por algo que nadie está mirando. En una aplicación cuya pestaña de al
    /// lado mide el consumo del equipo, eso sería difícil de defender.
    /// </summary>
    private void RenderAudioFrame()
    {
        if (!_isShown || !Dashboard.WantsAnimationFrames)
        {
            StopAudioFrames();
            return;
        }

        if (!_spectrumSource.Fill(_spectrum))
        {
            // Sin bloque nuevo todavía: se deja caer lo que hay en vez de repetir el anterior, que
            // congelaría el anillo unos fotogramas y se vería como un tirón.
            _spectrum.Decay();
        }

        // La onda avanza con el reloj y no con la posición de la canción: es una señal de que algo
        // suena, no una segunda medida del tiempo, y a la velocidad de la pista se movería tan
        // despacio que parecería quieta.
        _wavePhase = (_wavePhase + 1.1) % 1000;

        Dashboard.RenderAudioFrame(_spectrum.Levels, _wavePhase);
    }

    private void EnsureAudioFrames()
    {
        if (_isClosing || !_isShown || !Dashboard.WantsAnimationFrames)
        {
            StopAudioFrames();
            return;
        }

        if (!_audioFrames.IsEnabled)
        {
            _spectrumSource.Start();
            _audioFrames.Start();
        }
    }

    private void StopAudioFrames()
    {
        if (_audioFrames.IsEnabled)
        {
            _audioFrames.Stop();
        }

        if (_spectrumSource.IsRunning)
        {
            _spectrumSource.Stop();
            _spectrum.Reset();
        }
    }

    /// <summary>Se dispara al recogerse, para que el vigilante del borde se silencie un momento.</summary>
    public event EventHandler? Dismissed;

    public DashboardView View => Dashboard;

    public bool IsRevealed => _isShown;

    /// <summary>
    /// Baja el cajón sobre el monitor indicado. Recibe el área en vez de calcularla porque con dos
    /// pantallas «arriba y en medio» depende de dónde esté el ratón, y eso solo lo sabe quien lo
    /// vigila.
    /// </summary>
    public void Reveal(TopRevealArea area)
    {
        if (_isClosing)
        {
            return;
        }

        var panelWidth = TopRevealPolicy.ResolveWidth(area.Width);

        // El ancho de la ventana incluye el margen que reserva la sombra a los dos lados; si no se
        // suma, el panel sale más estrecho de lo pedido y descentrado respecto al borde.
        var shadowMargin = PanelBorder.Margin.Left + PanelBorder.Margin.Right;

        Width = panelWidth + shadowMargin;
        Left = area.Left + ((area.Width - Width) / 2);
        Top = area.Top;

        Dashboard.PrepareForReveal();

        // Si ya está abierto, no se vuelve a animar. El vigilante del borde sigue avisando mientras
        // el ratón se quede arriba —cada vez que se cumple el silencio, unos novecientos
        // milisegundos—, así que sin esta guarda el cajón se relanzaba solo una y otra vez: la
        // animación se reiniciaba desde arriba con el panel ya en pantalla y parecía una sacudida
        // periódica sin motivo.
        if (_isShown)
        {
            return;
        }

        _isShown = true;
        IsHitTestVisible = true;
        Show();
        Activate();

        // Hay que medir antes de animar: la altura la decide el contenido (SizeToContent) y sin
        // este paso la primera apertura arrancaría desde cero y el cajón aparecería de golpe en su
        // sitio en vez de caer.
        UpdateLayout();

        PlayRevealAnimation();

        _outsideSince = null;
        _mouseWatch.Start();
        EnsureAudioFrames();
    }

    /// <summary>
    /// La caída, con el asentamiento del final.
    ///
    /// El rebote es proporcional al recorrido, y ahí estuvo el error de la primera versión: con la
    /// curva <c>MotionSpring</c>, que sobrepasa un 10%, un cajón de casi seiscientos píxeles se
    /// pasaba de largo sesenta. En un botón eso son tres píxeles y se lee como vida; en una ventana
    /// entera es una sacudida. Con <c>MotionSpringSubtle</c> el exceso queda en unos quince
    /// píxeles: se nota que se asienta y no que salta.
    ///
    /// La escala ya no rebota, solo frena. Dos muelles a la vez sobre la misma pieza se leen como
    /// un temblor, porque no llegan al reposo en el mismo instante.
    /// </summary>
    private void PlayRevealAnimation()
    {
        var travel = PanelBorder.ActualHeight > 0 ? PanelBorder.ActualHeight : Height;

        if (!SakuraMotion.AnimationsEnabled)
        {
            PanelTranslate.BeginAnimation(TranslateTransform.YProperty, null);
            PanelScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            PanelBorder.BeginAnimation(OpacityProperty, null);
            PanelTranslate.Y = 0;
            PanelScale.ScaleY = 1;
            PanelBorder.Opacity = 1;
            return;
        }

        PanelTranslate.BeginAnimation(TranslateTransform.YProperty, null);
        PanelTranslate.Y = -travel;
        PanelScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        PanelScale.ScaleY = 0.94;
        PanelBorder.Opacity = 0;

        PanelTranslate.AnimateTransform(
            TranslateTransform.YProperty,
            0,
            SakuraMotion.Emphasized,
            SakuraMotion.SubtleSpringCurve);

        PanelScale.AnimateTransform(
            ScaleTransform.ScaleYProperty,
            1,
            SakuraMotion.Emphasized,
            SakuraMotion.DecelerateCurve);

        // La opacidad entra antes que el movimiento y con su propia curva: si durase lo mismo, el
        // cajón terminaría de aparecer justo cuando rebota y el rebote no se vería.
        PanelBorder.Animate(
            OpacityProperty,
            1,
            SakuraMotion.Reveal,
            SakuraMotion.DecelerateCurve);
    }

    public void Dismiss()
    {
        if (!_isShown)
        {
            return;
        }

        _isShown = false;
        _mouseWatch.Stop();
        StopAudioFrames();
        _outsideSince = null;

        // Diseño D56 — deja de aceptar clics en cuanto se decide que se va, no cuando termina de
        // irse.
        //
        // El cajón se oculta al acabar la animación de salida, y una animación puede no acabar
        // nunca: basta con que algo la reemplace a mitad de camino para que su Completed no llegue
        // y la ventana se quede mostrada con opacidad cero. Invisible y clicable es el peor estado
        // posible de una ventana, así que no puede depender de que una animación termine bien.
        IsHitTestVisible = false;

        if (!SakuraMotion.AnimationsEnabled)
        {
            HideImmediately();
            Dismissed?.Invoke(this, EventArgs.Empty);
            return;
        }

        // Al recogerse no hay rebote: sube y se va. Un rebote a la salida retiene en pantalla algo
        // que la persona acaba de decidir quitar de en medio.
        // Diseño D58 — se recoge entero hacia el techo, no un tercio.
        //
        // Con un tercio del recorrido y una salida corta, lo que se veía no era el cajón
        // subiendo sino el cajón esfumándose: el desvanecido terminaba mucho antes de que el
        // desplazamiento contara nada. Si se va por arriba, tiene que irse por arriba del todo,
        // que es la única forma de que el gesto explique dónde ha quedado y por dónde vuelve.
        var travel = PanelBorder.ActualHeight > 0 ? PanelBorder.ActualHeight : Height;

        PanelTranslate.AnimateTransform(
            TranslateTransform.YProperty,
            -travel,
            SakuraMotion.Exit,
            SakuraMotion.AccelerateCurve);

        PanelBorder.Animate(
            OpacityProperty,
            0,
            SakuraMotion.Exit,
            SakuraMotion.AccelerateCurve,
            completed: () =>
            {
                HideImmediately();
                Dismissed?.Invoke(this, EventArgs.Empty);
            });
    }

    public void HideImmediately()
    {
        _isShown = false;
        _mouseWatch.Stop();
        StopAudioFrames();
        _outsideSince = null;
        IsHitTestVisible = false;

        PanelBorder.BeginAnimation(OpacityProperty, null);
        PanelTranslate.BeginAnimation(TranslateTransform.YProperty, null);
        PanelScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        PanelBorder.Opacity = 0;

        Hide();
    }

    public void PrepareForShutdown()
    {
        _isClosing = true;
        _mouseWatch.Stop();
        StopAudioFrames();
        _spectrumSource.Dispose();
    }

    /// <summary>
    /// Se vigila la posición real del cursor en vez de usar <c>MouseLeave</c>: el cajón está lleno
    /// de controles hijos, y salir de uno para entrar en otro genera un MouseLeave por el camino.
    /// Preguntar dónde está el puntero cada 120 ms no se equivoca y cuesta una llamada barata.
    /// </summary>
    private void CheckMouse()
    {
        if (!_isShown)
        {
            return;
        }

        // No se tiene en cuenta el foco del teclado. El cajón se activa al abrirse, así que el foco
        // queda dentro siempre y, con esa condición, apartar el ratón dejaba de cerrarlo: solo se iba
        // con Escape. Quien llega por teclado sigue teniendo Escape para salir.
        if (IsPointerInside())
        {
            _outsideSince = null;
            return;
        }

        _outsideSince ??= DateTimeOffset.UtcNow;

        if (DateTimeOffset.UtcNow - _outsideSince.Value >= GraceAfterMouseLeaves)
        {
            Dismiss();
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Dismiss();
        }
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        if (!_isShown)
        {
            return;
        }

        // Perder el foco no basta para irse: hay que perderlo Y tener el ratón fuera.
        //
        // La ventana principal de Sakura es «siempre visible», y al bajar el cajón se lo devuelve
        // en cuanto se muestra. Con la regla ingenua —desactivarse es marcharse— el cajón se
        // recogía en el mismo instante en que aparecía: llegaba a estar en pantalla, medía bien, y
        // desaparecía antes de que a nadie le diera tiempo a verlo.
        if (!IsPointerInside())
        {
            Dismiss();
        }
    }

    private bool IsPointerInside()
    {
        if (!GetCursorPos(out var cursor))
        {
            // Sin saber dónde está el puntero, se prefiere dejarlo abierto: cerrarlo por si acaso
            // hace desaparecer algo que la persona está mirando.
            return true;
        }

        return cursor.X >= Left && cursor.X <= Left + Width &&
               cursor.Y >= Top && cursor.Y <= Top + ActualHeight;
    }

    // ---------- Datos ----------

    public void UpdateSnapshot(SystemSnapshot snapshot) => Dashboard.UpdateSnapshot(snapshot);

    public void UpdateThroughput(
        double downloadBytesPerSecond,
        double uploadBytesPerSecond,
        long? driveUsedBytes,
        long? driveTotalBytes,
        string? driveName) =>
        Dashboard.UpdateThroughput(
            downloadBytesPerSecond,
            uploadBytesPerSecond,
            driveUsedBytes,
            driveTotalBytes,
            driveName);

    public void UpdateMedia(MediaSnapshot media)
    {
        Dashboard.UpdateMedia(media);
        EnsureAudioFrames();
    }

    public void UpdateHardwareNames(string? processorName, string? graphicsName) =>
        Dashboard.UpdateHardwareNames(processorName, graphicsName);

    public void UpdateSession(string? profileName, TimeSpan? uptime) =>
        Dashboard.UpdateSession(profileName, uptime);

    /// <summary>Diseño D80 — el vistazo de «Ahora», ya resuelto por la política.</summary>
    public void UpdateNow(NowGlance glance) => Dashboard.UpdateNow(glance);

    /// <summary>Diseño D81 — el estado de la voz, ya resuelto por la política.</summary>
    public void UpdateVoice(IReadOnlyList<VoiceGlanceRow> rows) => Dashboard.UpdateVoice(rows);

    public void RefreshClock() => Dashboard.RefreshClock();

    public void UpdateDailySummary(
        string? taskValue,
        string? taskDetail,
        string? focusValue,
        string? focusDetail,
        string? routineValue,
        string? routineDetail) =>
        Dashboard.UpdateDailySummary(
            taskValue,
            taskDetail,
            focusValue,
            focusDetail,
            routineValue,
            routineDetail);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }
}
