namespace Nexo.Core.Voice;

/// <summary>
/// Diseño D74 — decir "nada" cierra la escucha sin hacer nada.
///
/// Pasa constantemente: se dispara la palabra de activación sin querer, o uno se arrepiente a mitad
/// de la frase. Hasta ahora la única salida era esperar el silencio de un segundo y medio y dejar
/// que Sakura intentara entender lo que fuera que hubiera oído, con el riesgo de que lo entendiera.
/// Poder decir "nada" y que se vaya es la salida educada, y es la que la gente prueba primero
/// porque es lo que le diría a una persona.
///
/// Las frases se comparan **enteras**, nunca por contenido. "Nada" cancela; "abre la carpeta nada"
/// no, porque ahí es una palabra dentro de una orden. Esa distinción es todo el cuidado que hay que
/// tener aquí: una cancelación que se dispara por una palabra suelta en mitad de una frase es peor
/// que no tener cancelación.
/// </summary>
public static class VoiceDismissalPolicy
{
    private static readonly HashSet<string> Phrases =
        new(StringComparer.Ordinal)
        {
            "nada",
            "nada nada",
            "no nada",
            "nada gracias",
            "no gracias",
            "olvidalo",
            "olvidalo sakura",
            "dejalo",
            "dejalo asi",
            "ya no",
            "ya nada",
            "cancela",
            "cancelalo",
            "cancelar",
            "no era nada",
            "no importa",
            "nada importante",
            "perdon",
            "perdon nada",
            "nada perdon"
        };

    /// <summary>
    /// Cierto si lo que se oyó es una despedida y no una orden. La comparación se hace sobre el
    /// texto ya normalizado —sin acentos, sin signos, en minúsculas— con el mismo normalizador que
    /// usa la palabra de activación, para que "Nada." y "¡nada!" cuenten igual.
    /// </summary>
    public static bool IsDismissal(string? text)
    {
        var normalized = WakeWordTextMatcher.Normalize(text);
        if (normalized.Length == 0)
        {
            return false;
        }

        // Se quita un "sakura" al principio o al final: "sakura, nada" y "nada sakura" son la misma
        // despedida que "nada".
        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 1 && words[0] is "sakura" or "kohana")
        {
            words = words[1..];
        }

        if (words.Length > 1 && words[^1] is "sakura" or "kohana")
        {
            words = words[..^1];
        }

        return Phrases.Contains(string.Join(' ', words));
    }
}
