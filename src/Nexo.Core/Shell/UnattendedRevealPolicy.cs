namespace Nexo.Core.Shell;

/// <summary>
/// Diseño D58 — cuándo se retira solo algo que se abrió solo.
///
/// El gesto del borde abre el shell sin que nadie lo pida explícitamente: basta con quedarse un
/// momento en la franja, y eso pasa sin querer al ir a por la barra de desplazamiento o al cruzar
/// entre dos monitores. Abrirse por roce es aceptable **si también sabe irse por sí mismo**; si no,
/// cada roce deja una ventana encima del trabajo que hay que cerrar a mano, y eso es exactamente lo
/// que se siente como invasivo.
///
/// 2026-10 — Adler pidió quitar la excepción por «atención»: antes, teclear o tener el foco dentro
/// del shell cancelaba la retirada aunque el puntero ya estuviera fuera, y eso dejaba el panel
/// pegado hasta cerrarlo a mano. Ahora la regla es solo «no hay ratón encima», sin excepciones: el
/// puntero fuera durante la gracia retira el shell, se haya tocado algo o no.
///
/// Solo se aplica a lo que se abrió por roce. Lo que se abre a propósito —el atajo, el icono de la
/// bandeja— se queda hasta que se cierre a propósito: retirar algo que se acaba de pedir es
/// desobedecer.
/// </summary>
public static class UnattendedRevealPolicy
{
    /// <summary>
    /// Cuánto se espera desde que el puntero sale hasta retirarse.
    ///
    /// Es más largo que la gracia del cajón (650 ms) a propósito: el cajón es una consulta de un
    /// vistazo y el shell es donde se conversa, así que salirse un momento del shell —a por el
    /// teclado, a mirar otra ventana— tiene que caber sin perderlo. Pero sigue siendo corto: si
    /// pasan varios segundos sin puntero, sin teclas y sin foco, nadie lo está usando.
    /// </summary>
    public static readonly TimeSpan GraceAfterPointerLeaves = TimeSpan.FromSeconds(2.5);

    /// <summary>
    /// Si toca retirarse, dadas las señales de ahora.
    ///
    /// <paramref name="outsideSince"/> es desde cuándo el puntero está fuera, o <c>null</c> si está
    /// dentro.
    /// </summary>
    public static bool ShouldRetract(
        bool openedByHover,
        DateTimeOffset? outsideSince,
        DateTimeOffset now)
    {
        if (!openedByHover || outsideSince is not { } since)
        {
            return false;
        }

        // Un reloj que va hacia atrás no es una espera cumplida.
        var away = now - since;
        return away >= GraceAfterPointerLeaves;
    }
}
