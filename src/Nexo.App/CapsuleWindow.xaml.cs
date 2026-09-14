using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Nexo.App.Motion;
using Nexo.Core.Settings;

namespace Nexo.App;

public partial class CapsuleWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;

    private readonly DispatcherTimer _dismissTimer;
    private bool _isClosingAnimation;

    public bool SuppressTransientMessages { get; set; }

    /// <summary>Lo que hay puesto ahora mismo, para reconocer un mensaje repetido.</summary>
    private CapsuleKind _currentKind;
    private string _currentTitle = string.Empty;

    public CapsuleWindow()
    {
        InitializeComponent();
        Views.Controls.PopupKeyboardAccess.Attach(this, System.Windows.Input.Key.F7, () => HideImmediately());

        _dismissTimer = new DispatcherTimer();
        _dismissTimer.Tick += (_, _) =>
        {
            if (IsKeyboardFocusWithin) return;
            _dismissTimer.Stop();
            HideAnimated();
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var handle = new WindowInteropHelper(this).Handle;
        var styles = GetWindowLong(handle, GwlExStyle);
        SetWindowLong(handle, GwlExStyle, styles | WsExNoActivate | WsExToolWindow);
    }

    public void ShowMessage(
        CapsuleKind kind,
        string title,
        string detail,
        SidebarPosition sidebarPosition,
        TimeSpan? duration = null,
        bool force = false)
    {
        if (SuppressTransientMessages &&
            !force &&
            kind is CapsuleKind.Information or CapsuleKind.Processing or CapsuleKind.Success)
        {
            return;
        }

        // Diseño D77 — un mensaje que sustituye a otro igual se ACTUALIZA; no vuelve a entrar.
        //
        // Subir el volumen tres veces seguidas producía tres cápsulas, y cada una reiniciaba la
        // animación de entrada: la misma ventana saltaba y parpadeaba en el sitio con cada paso.
        // Lo que ve alguien no son tres avisos, es un aviso teniendo un tic.
        //
        // Se compara el tipo y el título, no el detalle: el detalle es justo lo que cambia
        // —«Volumen al 41 %», «al 42 %»— y es lo que hay que refrescar sin mover nada más.
        var isRepeat = IsSameMessageAsShowing(kind, title);

        _dismissTimer.Stop();
        _isClosingAnimation = false;
        IsHitTestVisible = true;

        _currentKind = kind;
        _currentTitle = title;

        TitleText.Text = title;
        DetailText.Text = detail;
        ApplyKind(kind);

        if (isRepeat)
        {
            // Solo se le da cuerda otra vez al temporizador: la ventana ya está donde toca, con su
            // opacidad y su posición correctas. Reposicionar aquí también la haría dar un salto.
            _dismissTimer.Interval = duration ?? GetDefaultDuration(kind);
            _dismissTimer.Start();
            return;
        }

        PositionWindow(sidebarPosition);

        if (!IsVisible)
        {
            Show();
        }

        Topmost = true;

        // Nace como burbuja arriba en el centro y se estira hasta el aviso (Adler, 2026-09-14).
        BubbleMotion.Inflate(CapsuleBorder, HorizontalAlignment.Center);

        _dismissTimer.Interval = duration ?? GetDefaultDuration(kind);
        _dismissTimer.Start();
    }

    /// <summary>
    /// Si lo que llega es el mismo mensaje que ya está puesto.
    ///
    /// Se compara el tipo y el título, nunca el detalle: el detalle es exactamente lo que cambia
    /// entre un paso y el siguiente —«Volumen al 41 %», «al 42 %»—, así que compararlo haría que
    /// ningún cambio de volumen fuese nunca «el mismo mensaje», que es justo el caso que esto viene
    /// a resolver.
    ///
    /// Una cápsula que se está yendo no cuenta como puesta: su animación de salida ya está en
    /// marcha y actualizarle el texto dejaría el mensaje nuevo desvaneciéndose.
    /// </summary>
    internal bool IsSameMessageAsShowing(CapsuleKind kind, string title) =>
        IsVisible &&
        !_isClosingAnimation &&
        kind == _currentKind &&
        string.Equals(title, _currentTitle, StringComparison.Ordinal);

    public void HideImmediately()
    {
        _dismissTimer.Stop();
        _isClosingAnimation = false;
        IsHitTestVisible = false;

        // La animación de salida se cancela además de ocultar. Sin esto seguía corriendo sobre una
        // ventana ya escondida y su Completed llegaba después, pisando el estado del mensaje
        // siguiente si entraba dentro de esos ciento treinta y cinco milisegundos.
        CapsuleBorder.BeginAnimation(OpacityProperty, null);
        CapsuleBorder.Opacity = 0;
        Hide();
    }

    private void HideAnimated()
    {
        if (!IsVisible || _isClosingAnimation)
        {
            return;
        }

        _isClosingAnimation = true;

        // Diseño D57 — deja de aceptar clics en cuanto se decide que se va, no cuando termina
        // de irse. Es la misma protección que D56 le puso al cajón, y esta ventana se oculta
        // igual: al acabar la animación de salida. Una animación puede no acabar nunca —basta
        // con que algo la reemplace a mitad de camino para que su Completed no llegue— y lo que
        // queda entonces es una ventana mostrada con opacidad cero que sigue quedándose con los
        // clics de su trozo de pantalla. Invisible y clicable es el peor estado posible de una
        // ventana: desde fuera se siente como que el ratón dejó de responder ahí, sin nada que
        // lo explique.
        IsHitTestVisible = false;

        // Se recoge en burbuja y desaparece. Si antes de acabar llega otro aviso, ShowMessage lo
        // vuelve a inflar y esta salida se cancela: su final ya no llega a ocultar la ventana.
        BubbleMotion.Deflate(CapsuleBorder, HorizontalAlignment.Center, () =>
        {
            Hide();
            _isClosingAnimation = false;
        });
    }

    private void PositionWindow(SidebarPosition sidebarPosition)
    {
        _ = sidebarPosition;

        var workArea = SystemParameters.WorkArea;

        // Se descuenta el margen de la sombra: lo que queda a 24 del borde es el aviso, no la ventana.
        Top = workArea.Top + 24 - CapsuleBorder.Margin.Top;
        Left = workArea.Left + Math.Max(0, (workArea.Width - Width) / 2);
    }

    private void ApplyKind(CapsuleKind kind)
    {
        var accentBrush = (Brush)FindResource("BrushAccent");
        var accentSoftBrush = (Brush)FindResource("BrushAccentSoft");

        switch (kind)
        {
            case CapsuleKind.Processing:
                StatusIcon.Text = "✦";
                StatusIcon.Foreground = accentBrush;
                BrandFlowerIcon.Foreground = accentBrush;
                StatusBadge.Background = accentSoftBrush;
                break;
            case CapsuleKind.Success:
                StatusIcon.Text = "✓";
                StatusIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#67D9A2"));
                BrandFlowerIcon.Foreground = StatusIcon.Foreground;
                StatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#203A32"));
                break;
            case CapsuleKind.Warning:
                StatusIcon.Text = "!";
                StatusIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3C969"));
                BrandFlowerIcon.Foreground = StatusIcon.Foreground;
                StatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3C3422"));
                break;
            case CapsuleKind.Error:
                StatusIcon.Text = "×";
                StatusIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF7D8A"));
                BrandFlowerIcon.Foreground = StatusIcon.Foreground;
                StatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#40262C"));
                break;
            default:
                StatusIcon.Text = "i";
                StatusIcon.Foreground = accentBrush;
                BrandFlowerIcon.Foreground = accentBrush;
                StatusBadge.Background = accentSoftBrush;
                break;
        }
    }

    private static TimeSpan GetDefaultDuration(CapsuleKind kind) => kind switch
    {
        CapsuleKind.Processing => TimeSpan.FromSeconds(4),
        CapsuleKind.Error => TimeSpan.FromSeconds(4),
        CapsuleKind.Warning => TimeSpan.FromSeconds(3.4),
        _ => TimeSpan.FromSeconds(2.7)
    };

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr windowHandle, int index);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr windowHandle, int index, int newLong);
}

public enum CapsuleKind
{
    Information,
    Processing,
    Success,
    Warning,
    Error
}
