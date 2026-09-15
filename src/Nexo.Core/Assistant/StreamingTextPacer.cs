namespace Nexo.Core.Assistant;

/// <summary>
/// Cuánto texto enseñar de una respuesta que va llegando (Adler, 2026-09-15: «la respuesta fue como
/// a tirones»).
///
/// El modelo no escribe a ritmo constante: manda ráfagas de varias palabras, se para, vuelve a mandar.
/// Enseñar cada ráfaga en cuanto llega es lo que se ve a tirones. En su lugar se enseña un poco en
/// cada paso, más deprisa cuanto más texto queda pendiente, y cortando siempre al final de una palabra
/// para que nunca aparezca media palabra. Así las pausas del modelo se reparten y el texto fluye.
/// </summary>
public static class StreamingTextPacer
{
    /// <summary>Lo mínimo que avanza cada paso mientras quede algo pendiente.</summary>
    public const int MinimumStep = 2;

    /// <summary>
    /// Hasta dónde enseñar en el siguiente paso.
    /// </summary>
    /// <param name="text">Todo lo recibido hasta ahora.</param>
    /// <param name="shown">Cuántos caracteres se ven ya.</param>
    /// <param name="finished">Si el modelo ya terminó: entonces se alcanza el final más deprisa.</param>
    public static int NextLength(string text, int shown, bool finished)
    {
        ArgumentNullException.ThrowIfNull(text);

        shown = Math.Clamp(shown, 0, text.Length);
        var pending = text.Length - shown;
        if (pending == 0)
        {
            return shown;
        }

        // Una fracción de lo pendiente: con poco atrasado va despacio y parejo; con mucho, se pone al
        // día sin quedarse varios segundos por detrás del modelo.
        var step = Math.Max(MinimumStep, (int)Math.Ceiling(pending * (finished ? 0.35 : 0.18)));
        var target = Math.Min(text.Length, shown + step);

        if (target >= text.Length)
        {
            return text.Length;
        }

        // Se termina la palabra en curso, si no está muy lejos.
        var limit = Math.Min(text.Length, target + 16);
        while (target < limit && !char.IsWhiteSpace(text[target]))
        {
            target++;
        }

        // Si la palabra sigue sin cerrar y el modelo no ha terminado, se espera a que llegue entera en
        // vez de enseñar un trozo.
        if (target < text.Length && !char.IsWhiteSpace(text[target]) && !finished)
        {
            var lastBreak = text.LastIndexOfAny([' ', '\n', '\t'], target - 1, target - shown);
            return lastBreak >= shown ? lastBreak + 1 : shown;
        }

        return target;
    }
}
