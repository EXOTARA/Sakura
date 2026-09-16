using Nexo.Core.Settings;

namespace Nexo.Core.Shell;

/// <summary>Qué mando se enseña en el panel rápido del borde.</summary>
public enum QuickControlKind
{
    Volume,
    Brightness
}

/// <summary>
/// Diseño D35 — las reglas del panel rápido que aparece al llevar el ratón al borde contrario a
/// Sakura.
///
/// El lado importa: Sakura ya ocupa su borde, y poner los mandos ahí obligaría a decidir cuál de las
/// dos cosas aparece con el mismo gesto. En el borde opuesto no compiten, y además queda el reparto
/// natural —Sakura de un lado, los mandos del otro— sin nada que recordar.
/// </summary>
public static class QuickControlsPolicy
{
    /// <summary>
    /// El borde donde viven los mandos: el contrario al de Sakura.
    /// </summary>
    public static SidebarPosition ControlsEdgeFor(SidebarPosition kohanaSide) =>
        kohanaSide == SidebarPosition.Right
            ? SidebarPosition.Left
            : SidebarPosition.Right;

    /// <summary>Vueltas de espera seguidas con el ratón quieto encima antes de irse igualmente.</summary>
    public const int MaximumStillTicks = 2;

    /// <summary>
    /// 2026-09-16 — si el panel sigue puesto al cumplirse la espera (Adler, en la 0.30.17: «sigue sin
    /// desaparecer»). El panel vive en el borde, justo donde descansa el ratón que lo abrió, así que
    /// «ratón encima» solo cuenta si el ratón se mueve: parado encima dos vueltas, se va. El teclado
    /// solo cuenta si la ventana está de verdad delante; si no, el foco que WPF recuerda es viejo.
    /// </summary>
    public static bool ShouldStay(bool dragging, bool keyboardInsideForeground, bool cursorOver, int stillTicks) =>
        dragging ||
        keyboardInsideForeground ||
        (cursorOver && stillTicks < MaximumStillTicks);

    /// <summary>
    /// Lleva un porcentaje al entero de 0 a 100 que esperan tanto el mezclador de audio como el
    /// protocolo del monitor. Existe como una sola función porque los dos mandos comparten el
    /// problema: un valor fuera de rango no da error, se acepta y deja el sistema en un estado que
    /// nadie pidió.
    /// </summary>
    public static int NormalizePercent(double percent)
    {
        if (double.IsNaN(percent))
        {
            return 0;
        }

        return (int)Math.Clamp(Math.Round(percent, MidpointRounding.AwayFromZero), 0, 100);
    }

    /// <summary>
    /// Qué mandos se enseñan. El brillo depende del monitor —por cable DDC/CI, que muchos monitores
    /// externos no traen o traen desactivado—, así que se omite en vez de enseñarse muerto: un
    /// control que no hace nada es peor que no tenerlo, porque hay que probarlo para descubrirlo.
    /// </summary>
    public static IReadOnlyList<QuickControlKind> VisibleControls(
        bool volumeAvailable,
        bool brightnessAvailable)
    {
        var controls = new List<QuickControlKind>(2);

        if (volumeAvailable)
        {
            controls.Add(QuickControlKind.Volume);
        }

        if (brightnessAvailable)
        {
            controls.Add(QuickControlKind.Brightness);
        }

        return controls;
    }
}
