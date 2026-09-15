namespace Nexo.Core.Ai;

/// <summary>
/// 2026-09-15 — cómo se reconoce una clave guardada sin enseñarla (Adler: «mi llave de Groq se borra
/// cada que abro Kohana»). No se borraba: el cuadro de la clave aparece vacío a propósito, para que la
/// clave no salga en pantalla ni en una captura, y vacío parecía «no hay clave». Ahora se dice que hay
/// una y en qué termina, como hacen los bancos con una tarjeta.
/// </summary>
public static class ApiKeyHint
{
    /// <summary>Por debajo de este largo no se enseña ningún carácter: cuatro serían demasiado de la clave.</summary>
    public const int MinimumLengthToShowEnding = 16;

    /// <summary>«…a1B2» para una clave guardada; «…» si es corta; nulo si no hay clave.</summary>
    public static string? Ending(string? apiKey)
    {
        var key = apiKey?.Trim();
        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        return key.Length >= MinimumLengthToShowEnding
            ? "…" + key[^4..]
            : "…";
    }
}
