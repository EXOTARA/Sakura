using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Nexo.App.Motion;
using Nexo.Core.Settings;
using Nexo.Core.Shell;

namespace Nexo.App;

/// <summary>
/// Diseño D34 — la respuesta a Ctrl+Espacio, en una esquina, sin abrir Sakura.
///
/// Quien pulsa Ctrl+Espacio no quiso abrir Sakura: estaba en otra aplicación y preguntó desde ahí.
/// Antes, la respuesta traía la ventana entera encima y la dejaba en la pestaña de chat, así que la
/// persona tenía que volver a donde estaba. Ahora la respuesta va donde estaba mirando y se va sola.
///
/// Dos cosas la hacen distinta de <see cref="CapsuleWindow"/>, que también es un aviso de esquina:
/// esta crece mientras el texto llega —una respuesta no se conoce entera cuando empieza a
/// mostrarse— y su duración depende de cuánto haya que leer, no de un temporizador fijo.
/// </summary>
public partial class AnswerPillWindow : Window
{
    private const int GwlExStyle = -20;

    /// <summary>
    /// Sin activación y como ventana de herramienta: la persona está escribiendo en otro programa y
    /// robarle el foco para enseñarle una respuesta le comería las siguientes teclas. Es la misma
    /// razón por la que Ctrl+Espacio ya no abre Sakura.
    /// </summary>
    private const int WsExNoActivate = 0x08000000;

    private const int WsExToolWindow = 0x00000080;

    /// <summary>
    /// Cada trozo que llega cambia el alto que hace falta. Animar en cada uno pondría decenas de
    /// animaciones a pelearse por la misma propiedad y el crecimiento se vería a saltos; se
    /// reconcilia a intervalos y queda un solo movimiento continuo.
    /// </summary>
    private static readonly TimeSpan GrowthInterval = TimeSpan.FromMilliseconds(70);

    /// <summary>Diferencia por debajo de la cual no vale la pena reanimar: el ojo no la ve.</summary>
    private const double GrowthThreshold = 2;

    /// <summary>
    /// Hueco que la ventana reserva alrededor de la píldora para que quepa la sombra. Tiene que
    /// coincidir con el <c>Margin</c> de PillBorder en el XAML: entra en el ancho disponible para
    /// el texto, en el alto que se anima y en dónde se coloca la ventana.
    /// </summary>
    private const double ShadowMargin = 30;

    /// <summary>
    /// Diseño D38 — vigila la tecla Escape mientras la píldora está visible.
    ///
    /// La píldora no toma el foco a propósito: aparece mientras se escribe en otro programa y
    /// robárselo le comería las teclas siguientes. Pero una ventana sin foco tampoco recibe
    /// <c>KeyDown</c>, así que el manejador que había no llegaba a ejecutarse nunca y la pista
    /// «Esc» prometía algo que no pasaba.
    ///
    /// Se consulta el estado de la tecla en vez de registrar un atajo global o instalar un enganche
    /// de teclado: un atajo global le quitaría Escape a todas las demás aplicaciones, y un enganche
    /// de bajo nivel haría pasar por aquí cada pulsación del sistema. Consultar no consume la tecla:
    /// quien esté escribiendo la recibe igual.
    /// </summary>
    private static readonly TimeSpan EscapeWatchInterval = TimeSpan.FromMilliseconds(50);

    private const int VirtualKeyEscape = 0x1B;

    private readonly DispatcherTimer _escapeWatcher;
    private readonly DispatcherTimer _growthTimer;
    private readonly DispatcherTimer _dismissTimer;
    private readonly StringBuilder _answer = new();

    private double _animatedHeight;
    private bool _streaming;
    private bool _dismissing;

    public AnswerPillWindow()
    {
        InitializeComponent();
        Views.Controls.PopupKeyboardAccess.Attach(this, System.Windows.Input.Key.F8, () => Dismiss());

        _growthTimer = new DispatcherTimer { Interval = GrowthInterval };
        _growthTimer.Tick += (_, _) => ReconcileHeight();

        _dismissTimer = new DispatcherTimer();
        _dismissTimer.Tick += (_, _) => { if (!IsKeyboardFocusWithin) Dismiss(); };

        _escapeWatcher = new DispatcherTimer { Interval = EscapeWatchInterval };
        _escapeWatcher.Tick += (_, _) =>
        {
            // El bit alto indica que la tecla está pulsada ahora mismo.
            if ((GetAsyncKeyState(VirtualKeyEscape) & 0x8000) != 0)
            {
                Dismiss();
            }
        };

        SourceInitialized += OnSourceInitialized;
        KeyDown += OnKeyDown;

        // Diseño D38 — el clic en cualquier parte de la píldora abría Sakura entera, y eso convertía
        // un gesto inofensivo —apartar la píldora, pinchar cerca sin querer— en lo único que la
        // píldora existe para evitar. Ahora solo abre el enlace que lo dice, y un clic en el resto
        // la descarta, que es lo que alguien espera de un aviso de esquina.
        OpenInSakuraText.Click += (_, e) =>
        {
            e.Handled = true;
            RaiseOpenRequested();
        };
        OpenInSakuraText.Cursor = Cursors.Hand;
        MouseLeftButtonUp += (_, _) => Dismiss();
    }

    /// <summary>Se pidió ver la respuesta completa en Sakura.</summary>
    public event EventHandler? OpenInSakuraRequested;

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        var styles = GetWindowLong(handle, GwlExStyle);
        SetWindowLong(handle, GwlExStyle, styles | WsExNoActivate | WsExToolWindow);
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Dismiss();
        }
    }

    /// <summary>
    /// Empieza una respuesta. La pregunta se enseña arriba porque la píldora aparece encima de otra
    /// cosa: sin ella, a los pocos segundos hay un texto flotando sin saber a qué contesta.
    /// </summary>
    public void BeginAnswer(string question, SidebarPosition side)
    {
        _dismissTimer.Stop();
        _dismissing = false;
        _streaming = true;
        IsHitTestVisible = true;
        _answer.Clear();

        QuestionText.Text = question.Trim();
        AnswerText.Text = "Pensando…";
        AnswerText.Visibility = Visibility.Visible;
        AnswerRich.Content = null;
        AnswerRich.Visibility = Visibility.Collapsed;
        OpenInSakuraText.Visibility = Visibility.Collapsed;
        HintText.Text = "Esc";

        Position(side);

        if (!IsVisible)
        {
            Show();
        }

        // Nace como burbuja desde el lado de Sakura y se estira hacia el centro (Adler, 2026-09-14).
        _side = side;
        BubbleMotion.Inflate(PillBorder, AnchorFor(side));

        _animatedHeight = Height;
        _growthTimer.Start();
        _escapeWatcher.Start();
    }

    /// <summary>Añade un trozo de respuesta. El alto se ajusta solo, sin saltos.</summary>
    public void AppendAnswer(string chunk)
    {
        if (!_streaming || string.IsNullOrEmpty(chunk))
        {
            return;
        }

        if (_answer.Length == 0)
        {
            AnswerText.Text = string.Empty;
        }

        _answer.Append(chunk);
        AnswerText.Text = _answer.ToString();

        // El aviso se enseña en cuanto la respuesta ya no cabe, sin esperar a que termine: si
        // apareciera solo al final, reservaría su hueco de golpe y el texto ya leído daría un salto
        // justo cuando la persona está terminando de leerlo.
        if (OpenInSakuraText.Visibility != Visibility.Visible &&
            AnswerPillPolicy.DeservesOpeningInSakura(AnswerText.Text))
        {
            OpenInSakuraText.Visibility = Visibility.Visible;
        }
    }

    /// <summary>
    /// Cierra la respuesta y arranca la cuenta atrás, que depende de cuánto haya que leer. Se llama
    /// también cuando la consulta falla, con el mensaje de error como texto: una píldora que se
    /// queda en «Pensando…» para siempre sería peor que decir que salió mal.
    /// </summary>
    public void CompleteAnswer(string? finalText = null)
    {
        if (!_streaming)
        {
            return;
        }

        _streaming = false;

        if (!string.IsNullOrWhiteSpace(finalText))
        {
            _answer.Clear();
            _answer.Append(finalText);
            AnswerText.Text = finalText;
        }

        var answer = _answer.ToString();

        if (AnswerPillPolicy.DeservesOpeningInSakura(answer))
        {
            OpenInSakuraText.Visibility = Visibility.Visible;
        }

        if (answer.Trim().Length > 0)
        {
            AnswerRich.Content = Views.Controls.AnswerRenderer.Render(answer.Trim());
            AnswerRich.Visibility = Visibility.Visible;
            AnswerText.Visibility = Visibility.Collapsed;
        }

        ReconcileHeight();
        _growthTimer.Stop();

        _dismissTimer.Interval = AnswerPillPolicy.ReadingTimeFor(answer);
        _dismissTimer.Start();
    }

    public void HideImmediately()
    {
        _growthTimer.Stop();
        _dismissTimer.Stop();
        _escapeWatcher.Stop();
        _streaming = false;
        _dismissing = false;
        IsHitTestVisible = false;
        PillBorder.BeginAnimation(OpacityProperty, null);
        PillBorder.Opacity = 0;
        Hide();
    }

    private SidebarPosition _side = SidebarPosition.Right;

    private static HorizontalAlignment AnchorFor(SidebarPosition side) =>
        side == SidebarPosition.Left ? HorizontalAlignment.Left : HorizontalAlignment.Right;

    private void RaiseOpenRequested()
    {
        OpenInSakuraRequested?.Invoke(this, EventArgs.Empty);
        Dismiss();
    }

    private void Dismiss()
    {
        if (_dismissing)
        {
            return;
        }

        _dismissing = true;
        _dismissTimer.Stop();
        _growthTimer.Stop();
        _escapeWatcher.Stop();
        _streaming = false;

        // Diseño D57 — deja de aceptar clics en cuanto se decide que se va, no cuando termina
        // de irse. Es la misma protección que D56 le puso al cajón, y esta ventana se oculta
        // igual: al acabar la animación de salida. Una animación puede no acabar nunca —basta
        // con que algo la reemplace a mitad de camino para que su Completed no llegue— y lo que
        // queda entonces es una ventana mostrada con opacidad cero que sigue quedándose con los
        // clics de su trozo de pantalla. Invisible y clicable es el peor estado posible de una
        // ventana: desde fuera se siente como que el ratón dejó de responder ahí, sin nada que
        // lo explique.
        IsHitTestVisible = false;

        if (!SakuraMotion.AnimationsEnabled)
        {
            Hide();
            _dismissing = false;
            return;
        }

        BubbleMotion.Deflate(PillBorder, AnchorFor(_side), () =>
        {
            Hide();
            _dismissing = false;
        });
    }

    /// <summary>
    /// Lleva el alto de la ventana al que pide el contenido. Se mide con el ancho real disponible
    /// para que el ajuste de línea del texto sea el mismo que se va a ver; medir con ancho infinito
    /// daría una sola línea larguísima y un alto que no se corresponde con nada.
    /// </summary>
    private void ReconcileHeight()
    {
        var available = Width - (ShadowMargin * 2) -
                        PillBorder.Padding.Left - PillBorder.Padding.Right -
                        PillBorder.BorderThickness.Left - PillBorder.BorderThickness.Right;
        available = Math.Max(1, available);

        var chrome = (ShadowMargin * 2) +
                     PillBorder.Padding.Top + PillBorder.Padding.Bottom +
                     PillBorder.BorderThickness.Top + PillBorder.BorderThickness.Bottom;

        // El texto de la respuesta es lo único que se recorta cuando no cabe todo. La cabecera —que
        // dice a qué se está contestando— y el aviso de abrirla en Sakura se miden aparte y se
        // reservan siempre: son las dos cosas que explican lo que se está viendo, y recortarlas
        // dejaría un texto cortado sin decir de qué es ni cómo ver el resto.
        HeaderRow.Measure(new Size(available, double.PositiveInfinity));
        OpenInSakuraText.Measure(new Size(available, double.PositiveInfinity));

        var reserved = HeaderRow.DesiredSize.Height + OpenInSakuraText.DesiredSize.Height + chrome;
        var budget = MaximumHeight() - reserved - AnswerText.Margin.Top;

        AnswerText.MaxHeight = Math.Max(40, budget);
        AnswerRich.MaxHeight = AnswerText.MaxHeight;

        ContentPanel.Measure(new Size(available, double.PositiveInfinity));

        var target = ContentPanel.DesiredSize.Height + chrome;
        target = Math.Max(MinHeight, Math.Min(target, MaximumHeight()));

        if (Math.Abs(target - _animatedHeight) < GrowthThreshold)
        {
            return;
        }

        _animatedHeight = target;

        if (!SakuraMotion.AnimationsEnabled)
        {
            BeginAnimation(HeightProperty, null);
            Height = target;
            return;
        }

        var animation = SakuraMotion.CreateAnimation(
            target, SakuraMotion.Reveal, SakuraMotion.DecelerateCurve);
        BeginAnimation(HeightProperty, animation);
    }

    /// <summary>
    /// Tope de alto: la píldora es un vistazo, no una ventana. Una respuesta que no cabe se corta
    /// aquí y se ofrece abrirla en Sakura, que es donde se lee entera.
    /// </summary>
    private static double MaximumHeight() =>
        Math.Max(200, SystemParameters.WorkArea.Height * 0.45);

    /// <summary>
    /// Se coloca en la esquina superior del lado donde vive Sakura. Ir siempre al mismo lado da
    /// igual dónde esté acoplada rompería la idea de que la respuesta viene de ahí.
    /// </summary>
    private void Position(SidebarPosition side)
    {
        var workArea = SystemParameters.WorkArea;
        const double margin = 24;

        // Se descuenta el hueco de la sombra: lo que tiene que quedar a la distancia pedida del
        // borde es la píldora que se ve, no la ventana que la contiene.
        Top = workArea.Top + margin - ShadowMargin;
        Left = side == SidebarPosition.Left
            ? workArea.Left + margin - ShadowMargin
            : workArea.Right - Width - margin + ShadowMargin;
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr window, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr window, int index, int newLong);
}
