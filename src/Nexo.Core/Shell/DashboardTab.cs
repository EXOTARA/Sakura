namespace Nexo.Core.Shell;

/// <summary>
/// Diseño D43 — las pestañas del panel superior, en el orden de la referencia.
/// </summary>
public enum DashboardTab
{
    Panel = 0,
    Media = 1,
    Performance = 2,

    /// <summary>Diseño D81 — el estado de la voz de un vistazo, y los dos atajos.</summary>
    Voice = 3
}

/// <summary>
/// Diseño D43 — qué pestaña se enseña al abrir Sistema.
///
/// La regla no es «la última que usaste» sino «la que tiene algo que contar»: si hay música
/// sonando, Media; si no, la que el usuario dejó abierta. Un panel que siempre vuelve a la primera
/// pestaña obliga a dos clics cada vez para llegar a lo mismo, y uno que recuerda a ciegas enseña
/// un reproductor vacío cuando no hay nada que reproducir.
/// </summary>
public static class DashboardTabPolicy
{
    public static DashboardTab Resolve(DashboardTab lastUsed, bool somethingIsPlaying)
    {
        if (somethingIsPlaying && lastUsed == DashboardTab.Media)
        {
            return DashboardTab.Media;
        }

        // Sin música, Media no se ofrece como punto de partida aunque fuera la última vista.
        return !somethingIsPlaying && lastUsed == DashboardTab.Media
            ? DashboardTab.Panel
            : lastUsed;
    }

    /// <summary>
    /// Diseño D58 — hacia dónde entra la pestaña nueva, como se pasa la hoja de un libro.
    ///
    /// Devuelve <c>+1</c> cuando se avanza —la página nueva llega desde la derecha—, <c>-1</c>
    /// cuando se retrocede, y <c>0</c> cuando no hay movimiento que contar.
    ///
    /// La dirección importa porque es la única señal de dónde estás. Las tres pestañas se parecen
    /// entre sí y todas entraban igual, de abajo arriba: el movimiento decía «ha cambiado algo»
    /// pero no «te has movido a la derecha», así que no ayudaba a orientarse. Con la dirección, la
    /// tira de arriba y el contenido cuentan lo mismo.
    /// </summary>
    public static int EnterDirection(DashboardTab from, DashboardTab to) =>
        Math.Sign((int)to - (int)from);
}
