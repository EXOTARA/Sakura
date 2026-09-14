using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Nexo.Core.AdaptiveEngine;
using Nexo.Core.Shell;
using Nexo.Windows.Shell;

namespace Nexo.App.Shell;

/// <summary>
/// Diseño D62 — lo que cada ventana tiene que hacer para que Windows le componga el fondo.
///
/// Son tres pasos y los tres importan: pedirle a DWM el fondo y las esquinas, dejar de pintar el
/// fondo de WPF si lo aceptó —mientras WPF pinte algo, aunque sea negro, tapa lo que DWM compone
/// debajo— y pintar la superficie con la transparencia que corresponda. Hacerlo en cada ventana por
/// separado era garantizar que alguna se quedara a medias: la primera versión del piloto olvidaba
/// el segundo paso y el acrílico estaba aplicado y no se veía.
///
/// La llamada va en <c>SourceInitialized</c>, que es el primer momento en el que la ventana tiene
/// identificador, y antes del primer pintado.
/// </summary>
public static class SakuraWindowChrome
{
    /// <summary>
    /// Transparencia de los paneles que no tienen un control propio. El shell usa el del usuario;
    /// estos son ventanas que aparecen, se usan y se van, y llevan texto denso encima.
    /// </summary>
    public const double PanelOpacity = 0.72;

    /// <summary>
    /// Modo de rendimiento vigente. Lo publica el shell al cargar las preferencias, porque las
    /// ventanas sueltas no las conocen y el modo eco tiene que valer para todas o para ninguna.
    /// </summary>
    public static HardwarePerformanceMode PerformanceMode { get; set; } = HardwarePerformanceMode.Automatic;

    /// <summary>
    /// Diseño D62 pedía esquinas rectas: las redondeadas de Windows traían encima el borde de DWM, y
    /// en el arco se leía como una mancha oscura. Después se quitó ese borde
    /// (<c>DWMWA_BORDER_COLOR</c> = <c>DWMWA_COLOR_NONE</c> en <see cref="WindowsDwmChrome"/>), pero la
    /// esquina se quedó recta sin que nadie volviera a mirarla.
    ///
    /// 2026-09-14: Adler preguntó por qué el panel de arriba es redondo y el shell no. Sin borde ya no
    /// hay mancha que evitar, así que se piden redondeadas.
    /// </summary>
    public const WindowCorner Corner = WindowCorner.Round;

    public static WindowBackdropDecision Apply(
        Window window,
        Border surface,
        string surfaceBrushKey = "BrushSurface",
        double opacity = PanelOpacity)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(surface);

        var handle = new WindowInteropHelper(window).Handle;
        var probe = WindowsDwmChrome.ReadProbe(PerformanceMode);
        var decision = WindowBackdropPolicy.Decide(probe, WindowBackdrop.Acrylic, Corner);

        // Manda lo que pasó de verdad, no lo que la política creía posible: si la llamada falló, la
        // ventana tiene que pintar su propio fondo o se queda negra.
        var effective = WindowsDwmChrome.TryApply(handle, decision)
            ? decision
            : decision with { Backdrop = WindowBackdrop.None, PaintOwnBackground = true };

        if (HwndSource.FromHwnd(handle)?.CompositionTarget is { } target)
        {
            target.BackgroundColor = effective.PaintOwnBackground ? Colors.Black : Colors.Transparent;
        }

        PaintSurface(window, surface, surfaceBrushKey, opacity, effective);
        return effective;
    }

    /// <summary>
    /// Solo el fondo del sistema, para ventanas que pintan su superficie con una brocha propia
    /// —un degradado, por ejemplo— y no con un color del tema.
    /// </summary>
    public static WindowBackdropDecision ApplyBackdropOnly(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        var handle = new WindowInteropHelper(window).Handle;
        var probe = WindowsDwmChrome.ReadProbe(PerformanceMode);
        var decision = WindowBackdropPolicy.Decide(probe, WindowBackdrop.Acrylic, Corner);

        var effective = WindowsDwmChrome.TryApply(handle, decision)
            ? decision
            : decision with { Backdrop = WindowBackdrop.None, PaintOwnBackground = true };

        if (HwndSource.FromHwnd(handle)?.CompositionTarget is { } target)
        {
            target.BackgroundColor = effective.PaintOwnBackground ? Colors.Black : Colors.Transparent;
        }

        return effective;
    }

    /// <summary>
    /// Pinta la superficie con el color del tema y el alfa que toque. Sin fondo del sistema detrás
    /// se pinta opaca: una superficie a medias sobre un escritorio sin desenfocar se lee peor que
    /// una sólida, y ese es el caso en el que no hay nada que enseñar a través.
    /// </summary>
    public static void PaintSurface(
        FrameworkElement themeSource,
        Border surface,
        string surfaceBrushKey,
        double opacity,
        WindowBackdropDecision decision)
    {
        ArgumentNullException.ThrowIfNull(themeSource);
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(decision);

        if (themeSource.TryFindResource(surfaceBrushKey) is not SolidColorBrush brush)
        {
            return;
        }

        var alpha = decision.PaintOwnBackground
            ? (byte)255
            : (byte)Math.Round(Math.Clamp(opacity, 0, 1) * 255);

        surface.Background = new SolidColorBrush(Color.FromArgb(
            alpha,
            brush.Color.R,
            brush.Color.G,
            brush.Color.B));
    }
}
