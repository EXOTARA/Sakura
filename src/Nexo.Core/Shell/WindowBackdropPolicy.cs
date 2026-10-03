using Nexo.Core.AdaptiveEngine;

namespace Nexo.Core.Shell;

/// <summary>Fondo que compone el sistema detrás de una ventana.</summary>
public enum WindowBackdrop
{
    /// <summary>Nada: la ventana pinta su propio fondo opaco.</summary>
    None,

    /// <summary>Mica — desenfoca el fondo de escritorio, no lo que hay en medio.</summary>
    Mica,

    /// <summary>Acrílico — desenfoca de verdad lo que la ventana tiene detrás, sea lo que sea.</summary>
    Acrylic
}

/// <summary>Cómo debe redondear el sistema las esquinas de una ventana.</summary>
public enum WindowCorner
{
    /// <summary>Lo que Windows decida por su cuenta.</summary>
    System,

    /// <summary>Redondeadas con el radio normal de Windows 11.</summary>
    Round,

    /// <summary>Redondeadas con el radio pequeño, el de los menús.</summary>
    RoundSmall,

    /// <summary>En ángulo recto.</summary>
    Square
}

/// <summary>Lo que hay que saber del sistema para decidir el fondo de una ventana.</summary>
/// <param name="WindowsBuild">Compilación de Windows (<c>Environment.OSVersion.Version.Build</c>).</param>
/// <param name="TransparencyEnabled">Configuración → Colores → "Efectos de transparencia".</param>
/// <param name="HighContrast">Contraste alto activo.</param>
/// <param name="PerformanceMode">Modo de rendimiento elegido en Sakura.</param>
public sealed record WindowBackdropProbe(
    int WindowsBuild,
    bool TransparencyEnabled,
    bool HighContrast,
    HardwarePerformanceMode PerformanceMode);

/// <summary>Qué aplicarle a la ventana, y por qué.</summary>
/// <param name="PaintOwnBackground">
/// Cierto cuando no hay fondo del sistema y la ventana tiene que pintar el suyo opaco. Si es falso,
/// el fondo de WPF debe quedarse transparente o el acrílico no se ve.
/// </param>
public sealed record WindowBackdropDecision(
    WindowBackdrop Backdrop,
    WindowCorner Corner,
    bool PaintOwnBackground,
    string Reason);

/// <summary>
/// Diseño D62 — decide qué le pedimos a DWM para cada ventana.
///
/// Sakura dibujaba su propia transparencia: <c>AllowsTransparency</c> en las once ventanas, que
/// obliga a WPF a componer por software, más una sombra pintada a mano que hay que recalcular cada
/// vez que cambia algo dentro. El sistema hace las tres cosas —desenfoque, esquinas y sombra— en la
/// GPU y gratis, así que lo que queda es saber cuándo puede hacerlas.
///
/// Las esquinas redondeadas llegaron en Windows 11 (22000) y los fondos del sistema un año después
/// (22621). Debajo de eso no hay nada que pedir: no es un fallo, es una versión sin la función, y
/// la ventana se queda opaca con las esquinas del sistema, que es exactamente como se ve cualquier
/// otra aplicación en esa máquina.
///
/// Dos ajustes del usuario mandan sobre la estética. Si apagó los efectos de transparencia, no se
/// los devolvemos por la puerta de atrás. Si tiene contraste alto, el desenfoque es justo lo que
/// esa configuración existe para quitar.
/// </summary>
public static class WindowBackdropPolicy
{
    /// <summary>Primera compilación de Windows 11: esquinas redondeadas por el sistema.</summary>
    public const int RoundedCornersBuild = 22000;

    /// <summary>Windows 11 22H2: <c>DWMWA_SYSTEMBACKDROP_TYPE</c>, o sea Mica y acrílico.</summary>
    public const int SystemBackdropBuild = 22621;

    /// <param name="requestedCorner">
    /// Diseño D62 — la esquina la pide quien llama. Sakura pide cuadrada: en el arco de las
    /// redondeadas, el borde que DWM dibuja se lee como una mancha oscura, y Adler prefiere el
    /// canto recto a seguir persiguiendo ese filo. Sigue siendo una petición y no una constante
    /// porque en Windows 10 no hay nada que pedir.
    /// </param>
    public static WindowBackdropDecision Decide(
        WindowBackdropProbe probe,
        WindowBackdrop requested,
        WindowCorner requestedCorner = WindowCorner.Round)
    {
        ArgumentNullException.ThrowIfNull(probe);

        var corner = probe.WindowsBuild >= RoundedCornersBuild ? requestedCorner : WindowCorner.System;

        if (requested == WindowBackdrop.None)
        {
            return Solid(corner, "La ventana pide fondo propio.");
        }

        if (probe.WindowsBuild < SystemBackdropBuild)
        {
            return Solid(
                corner,
                $"Windows {probe.WindowsBuild} no tiene fondos del sistema (hacen falta {SystemBackdropBuild}).");
        }

        if (probe.HighContrast)
        {
            return Solid(corner, "Contraste alto: el desenfoque es lo que esa configuración quita.");
        }

        if (!probe.TransparencyEnabled)
        {
            return Solid(corner, "Los efectos de transparencia están apagados en Windows.");
        }

        // Eco no apaga el fondo, lo abarata: Mica muestrea el fondo de escritorio, que cambia
        // rara vez, mientras que el acrílico recompone lo que haya detrás mientras se mueva.
        if (probe.PerformanceMode == HardwarePerformanceMode.Eco && requested == WindowBackdrop.Acrylic)
        {
            return new WindowBackdropDecision(
                WindowBackdrop.Mica,
                corner,
                PaintOwnBackground: false,
                "Modo eco: Mica en vez de acrílico.");
        }

        return new WindowBackdropDecision(
            requested,
            corner,
            PaintOwnBackground: false,
            requested == WindowBackdrop.Acrylic ? "Acrílico del sistema." : "Mica del sistema.");
    }

    /// <summary>
    /// Cierto cuando la superficie de una ventana no debe dejar ver nada a través: contraste alto o
    /// «Efectos de transparencia» apagados en Windows.
    ///
    /// <see cref="Decide"/> ya lo tenía en cuenta para las ventanas con fondo del sistema, pero las de
    /// cristal transparente (el shell, la paleta, el Command Center, Peek) no pasan por ahí: pintan
    /// un color translúcido encima de un cristal sin desenfoque, y miraban solo el contraste alto.
    /// Apagar los efectos de transparencia no les hacía nada. Aquí queda la regla en un solo sitio.
    /// </summary>
    public static bool RequiresOpaqueSurface(WindowBackdropProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        return probe.HighContrast || !probe.TransparencyEnabled;
    }

    private static WindowBackdropDecision Solid(WindowCorner corner, string reason) =>
        new(WindowBackdrop.None, corner, PaintOwnBackground: true, reason);
}
