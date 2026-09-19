using Nexo.Core.Settings;

namespace Nexo.Core.Shell;

/// <summary>Lo que hay que saber del ratón y de la pantalla para decidir si Sakura debe asomarse.</summary>
public sealed record EdgeRevealProbe(
    double CursorX,
    double CursorY,
    double WorkAreaLeft,
    double WorkAreaRight,
    double WorkAreaTop,
    double WorkAreaBottom,
    SidebarPosition Side,
    bool OnDesktopEdge = true);

/// <summary>
/// Diseño D27 — llevar el ratón al borde donde vive Sakura la hace aparecer, sin atajo ni clic.
///
/// La parte difícil no es detectar el borde sino no molestar. Un borde de pantalla es un sitio muy
/// concurrido: ahí están la barra de desplazamiento de cualquier ventana maximizada, el gesto de
/// acoplar ventanas de Windows, y en las esquinas el menú Inicio, el reloj y "mostrar escritorio".
/// Aparecer en cuanto el ratón roza el borde convertiría la función en un estorbo constante.
///
/// De ahí las tres condiciones: una franja estrecha, las esquinas excluidas, y una permanencia
/// mínima. Rozar el borde de paso no basta; hay que quedarse.
/// </summary>
public static class EdgeRevealPolicy
{
    /// <summary>
    /// Ancho de la franja sensible.
    ///
    /// Diseño D38 — eran 3 píxeles y era inservible con dos monitores, que es el caso que lo
    /// destapó: en el borde compartido entre dos pantallas el cursor no se detiene, cruza. Para
    /// quedarse dentro de una franja de 3 píxeles el tiempo que pide la permanencia había que
    /// clavar el ratón con una precisión que nadie tiene, y el intento normal acababa en el otro
    /// monitor. Dieciocho píxeles son aproximadamente medio centímetro en una pantalla típica: se
    /// alcanza sin apuntar y sigue siendo una franja, no media pantalla.
    ///
    /// El motivo original de que fuera estrecha —la barra de desplazamiento de una ventana
    /// maximizada— sigue siendo cierto, y por eso no se amplía más: lo que impide que estorbe no es
    /// tanto el ancho como la permanencia, que exige quedarse y no solo pasar.
    /// </summary>
    public const double HotZoneWidth = 18;

    /// <summary>
    /// 2026-09-18 — franja estrecha para el borde EXTERIOR del escritorio (izquierda del monitor más
    /// a la izquierda, derecha del más a la derecha, o cualquier lado con un solo monitor). Ahí el
    /// cursor se frena contra el límite: el problema de D38 no existe, y seis píxeles bastan para
    /// no exigir precisión. Se pidió que el panel salga lo más lejos posible de los bordes de
    /// ventana, sin robar clics a la barra de desplazamiento ni a lo que se arrastra cerca.
    ///
    /// En un borde compartido entre dos monitores se mantiene <see cref="HotZoneWidth"/>: allí el
    /// cursor cruza en vez de detenerse, y estrechar la franja devolvería el fallo de D38. Por eso
    /// el ancho depende de <see cref="EdgeRevealProbe.OnDesktopEdge"/>.
    /// </summary>
    public const double ExteriorHotZoneWidth = 6;

    /// <summary>
    /// Proporción del alto que se excluye arriba y abajo. Las esquinas pertenecen a Windows: el
    /// menú Inicio, el área del reloj y "mostrar escritorio" están todos ahí.
    /// </summary>
    public const double CornerExclusionRatio = 0.1;

    /// <summary>
    /// Cuánto hay que quedarse en la franja. Menos que esto se dispara solo al pasar.
    ///
    /// Diseño D38 — bajado de 280 a 160 ms. Con la franja estrecha, la espera larga era el segundo
    /// peaje: había que acertar Y esperar, y juntos hacían que el gesto se sintiera «lentísimo».
    /// Con la franja ancha, quedarse ya distingue de pasar sin necesidad de tanto tiempo.
    /// </summary>
    public static readonly TimeSpan Dwell = TimeSpan.FromMilliseconds(160);

    /// <summary>
    /// Tras ocultar Sakura, el ratón suele quedarse justo donde estaba: encima del borde. Sin esta
    /// pausa, cerrarla la volvería a abrir de inmediato y no habría forma de quitarla de en medio.
    /// </summary>
    public static readonly TimeSpan CooldownAfterHide = TimeSpan.FromMilliseconds(900);

    /// <summary>
    /// 2026-09-16 — cuánto tiene que apartarse el ratón para que el borde vuelva a poder abrir algo.
    ///
    /// Encontrado con Adler: el panel de volumen «tendía a no quitarse». Usaba un navegador con las
    /// pestañas en vertical, pegadas al borde izquierdo, justo el borde del panel. Cada vez que iba a
    /// una pestaña el panel salía, se iba a los cuatro segundos, y como el ratón seguía ahí volvía a
    /// salir casi enseguida. Desde fuera: un panel que no se quita.
    ///
    /// La regla es una aparición por visita al borde. Tras abrir algo, el borde no vuelve a abrir nada
    /// hasta que el ratón se aparta de verdad —fuera de esta franja, más ancha que la sensible para que
    /// temblar en la frontera no cuente como irse y volver—. Se calcula sobre la franja ancha: sigue
    /// siendo mayor que la exterior (54 frente a 6 px), así que el margen solo crece.
    /// </summary>
    public const double RearmDistance = HotZoneWidth * 3;

    /// <summary>
    /// Si en este borde vale la franja estrecha. Exige dos cosas: que sea el límite exterior del
    /// escritorio virtual, y que el área de trabajo llegue hasta el borde del monitor. Si hay una
    /// barra de tareas (u otra reserva) en ese borde, rcWork empieza más adentro y una franja de
    /// 4-6 px desde ahí sería mucho más difícil de alcanzar que los 18-20 px de antes; en ese
    /// caso se usa la franja ancha. Una reserva en OTRO borde (p. ej. barra abajo) no afecta.
    /// </summary>
    public static bool UsesNarrowStrip(bool outerDesktopEdge, double monitorEdge, double workEdge) =>
        outerDesktopEdge && monitorEdge == workEdge;

    /// <summary>Si el ratón está lo bastante lejos del borde como para que la próxima llegada cuente como nueva.</summary>
    public static bool HasLeftEdge(EdgeRevealProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);

        return probe.Side == SidebarPosition.Left
            ? probe.CursorX > probe.WorkAreaLeft + RearmDistance
            : probe.CursorX < probe.WorkAreaRight - RearmDistance;
    }

    public static bool IsInHotZone(EdgeRevealProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);

        var height = probe.WorkAreaBottom - probe.WorkAreaTop;
        if (height <= 0 || probe.WorkAreaRight <= probe.WorkAreaLeft)
        {
            return false;
        }

        var margin = height * CornerExclusionRatio;
        if (probe.CursorY < probe.WorkAreaTop + margin ||
            probe.CursorY > probe.WorkAreaBottom - margin)
        {
            return false;
        }

        // WorkAreaRight es exclusivo (el último píxel es Right - 1), así que con enteros la franja
        // derecha usa >= para contar el ancho real y no uno menos que la izquierda.
        // El borde exterior se incluye y el interior no: la franja es [borde, borde + ancho) por la
        // izquierda y (borde - ancho, borde] por la derecha. Así una franja de 3 píxeles son
        // exactamente 3 píxeles, y el píxel que está justo fuera nunca cuenta como dentro.
        var width = probe.OnDesktopEdge ? ExteriorHotZoneWidth : HotZoneWidth;

        return probe.Side == SidebarPosition.Right
            ? probe.CursorX >= probe.WorkAreaRight - width &&
              probe.CursorX <= probe.WorkAreaRight
            : probe.CursorX >= probe.WorkAreaLeft &&
              probe.CursorX < probe.WorkAreaLeft + width;
    }
}
