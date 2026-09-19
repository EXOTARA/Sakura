namespace Nexo.Core.Shell;

/// <summary>Lo que hay que saber del ratón y de la pantalla para decidir si el cajón debe bajar.</summary>
public sealed record TopRevealProbe(
    double CursorX,
    double CursorY,
    double WorkAreaLeft,
    double WorkAreaRight,
    double WorkAreaTop,
    bool OnDesktopEdge = true);

/// <summary>
/// Diseño D44 — llevar el ratón al centro del borde de arriba baja el cajón del panel.
///
/// Es el mismo gesto que <see cref="EdgeRevealPolicy"/> hace en los lados, con dos diferencias que
/// vienen del sitio: arriba no se puede usar todo el borde, porque ahí están la barra de título de
/// cualquier ventana maximizada y sus botones de cerrar. Por eso la zona es solo el tercio central,
/// que es donde una ventana maximizada no tiene nada con lo que competir.
///
/// 2026-09-18 — la franja era de 20 px y se abría sola al arrastrar una ventana hacia arriba. La
/// causa de fondo se corrige en el vigilante (con un botón pulsado no se abre nada); además la
/// franja se estrecha a lo mínimo que sigue siendo fiable, para que solo cuente el borde extremo.
/// </summary>
public static class TopRevealPolicy
{
    /// <summary>
    /// Alto de la franja sensible, desde el borde superior del área de trabajo. Cuatro píxeles y no
    /// uno: empujar el cursor hacia arriba lo frena contra el límite del escritorio, así que llega
    /// a la primera fila sin precisión, y los píxeles de más absorben el error de escalado en
    /// pantallas con zoom. Antes eran 20 y bajar una ventana por su barra de título rozaba la
    /// franja constantemente.
    ///
    /// Si el monitor tiene otro apilado encima, ese borde es compartido y el cursor cruza sin
    /// frenar (el mismo problema que D38 en los lados); se acepta porque es un montaje raro y el
    /// cajón se puede abrir también con el atajo.
    /// </summary>
    public const double HotZoneHeight = 4;

    /// <summary>
    /// Alto de la franja cuando el borde de arriba es COMPARTIDO con otro monitor apilado encima:
    /// ahí el cursor cruza sin frenar (D38 en vertical) y 4 px serían inalcanzables, así que se
    /// conservan los 20 px de siempre. Solo el borde superior del escritorio virtual usa 4.
    /// </summary>
    public const double SharedHotZoneHeight = 20;

    /// <summary>
    /// Hasta dónde llega la zona «cercana» que desarma el borde si se pulsa un botón dentro de
    /// ella. Mayor que cualquier franja, para que temblar en la frontera no cuente como irse.
    /// </summary>
    public const double RearmDistance = SharedHotZoneHeight * 3;

    /// <summary>
    /// Qué parte del ancho ocupa la zona, centrada. Un tercio es suficiente para alcanzarla sin
    /// apuntar y deja libres las dos esquinas de arriba, que son de Windows y de la ventana activa.
    /// </summary>
    public const double CenterWidthRatio = 1d / 3d;

    /// <summary>
    /// Cuánto hay que quedarse. Igual que en los lados: lo que distingue asomarse de pasar de
    /// largo no es la precisión, es quedarse.
    /// </summary>
    public static readonly TimeSpan Dwell = TimeSpan.FromMilliseconds(160);

    /// <summary>
    /// Tras cerrar el cajón el ratón suele seguir arriba, justo donde estaba. Sin esta pausa,
    /// cerrarlo volvería a abrirlo al instante.
    /// </summary>
    public static readonly TimeSpan CooldownAfterHide = TimeSpan.FromMilliseconds(900);

    public static bool IsInHotZone(TopRevealProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);

        // El borde superior se incluye y el inferior de la franja no: [borde, borde + alto). Así
        // la franja mide exactamente su alto, y el primero de fuera nunca cuenta como dentro.
        var height = probe.OnDesktopEdge ? HotZoneHeight : SharedHotZoneHeight;
        return IsInBand(probe, height);
    }

    /// <summary>
    /// Si el cursor está cerca de la franja (el tercio central y hasta <see cref="RearmDistance"/>
    /// desde arriba). Sirve para desarmar el borde al pulsar un botón por aquí y rearmarlo solo
    /// cuando el cursor se aparta de verdad.
    /// </summary>
    public static bool IsNearHotZone(TopRevealProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        return IsInBand(probe, RearmDistance);
    }

    private static bool IsInBand(TopRevealProbe probe, double height)
    {
        var width = probe.WorkAreaRight - probe.WorkAreaLeft;
        if (width <= 0)
        {
            return false;
        }

        if (probe.CursorY < probe.WorkAreaTop || probe.CursorY >= probe.WorkAreaTop + height)
        {
            return false;
        }

        var centerWidth = width * CenterWidthRatio;
        var left = probe.WorkAreaLeft + ((width - centerWidth) / 2);

        return probe.CursorX >= left && probe.CursorX <= left + centerWidth;
    }

    /// <summary>
    /// Ancho del cajón: la mitad larga de la pantalla, como en la referencia, con topes para que no
    /// quede ridículo en una pantalla muy ancha ni se salga en una pequeña.
    /// </summary>
    public static double ResolveWidth(double workAreaWidth)
    {
        if (workAreaWidth <= 0)
        {
            return MinimumWidth;
        }

        var width = workAreaWidth * 0.53;
        return Math.Clamp(width, MinimumWidth, MaximumWidth);
    }

    public const double MinimumWidth = 620;

    public const double MaximumWidth = 1180;
}
