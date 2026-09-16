namespace Nexo.Core.Resources;

/// <summary>
/// 2026-09-16 — hasta cuándo se le hace caso a una decisión del gobernador de recursos.
///
/// Encontrado con un fallo de Adler que no se podía reproducir: el panel lateral se abría, pero el
/// cajón de arriba y el mando de volumen no, sin tener nada a pantalla completa. Los dos solo salen al
/// rozar el borde, y ese camino se calla cuando la decisión dice «hay algo a pantalla completa». Si el
/// ciclo que refresca las métricas se queda colgado —una lectura del equipo que no vuelve, el motor de
/// voz ocupado—, la última decisión se queda congelada, y con ella el silencio: Sakura sigue creyendo
/// que hay un juego delante desde hace media hora.
///
/// La regla es no fiarse de una decisión que nadie ha vuelto a comprobar. Con la medida vieja, lo
/// prudente ya no es callarse: es volver a comportarse con normalidad, porque el coste de equivocarse
/// —asomarse una vez sobre un juego— es mucho menor que el de dejar media aplicación muda sin que
/// nadie entienda por qué.
/// </summary>
public static class ResourceGovernorFreshness
{
    /// <summary>
    /// Las métricas se leen cada dos segundos, así que quince es tiempo de sobra para varios intentos:
    /// si se llega ahí, es que el ciclo dejó de girar, no que vaya lento.
    /// </summary>
    public static readonly TimeSpan MaximumAge = TimeSpan.FromSeconds(15);

    /// <param name="age">Lo que hace que se leyó el equipo por última vez.</param>
    public static bool SuppressesOverlays(ResourceGovernorDecision decision, TimeSpan age)
    {
        ArgumentNullException.ThrowIfNull(decision);
        return decision.SuppressTransientOverlays && age < MaximumAge;
    }
}
