using System.Globalization;
using System.Text.RegularExpressions;

namespace Nexo.Core.Ai;

/// <summary>
/// 2026-09-16 — los límites que un proveedor pone al tamaño de la respuesta, leídos de su propio
/// mensaje de error.
///
/// Encontrado en vivo (Adler, con Groq): tras explicar una ventana con Ctrl + Shift + Espacio, la
/// imagen se queda en el contexto y cada pregunta de seguimiento vuelve al modelo con visión. Ese
/// modelo, en su plan, admite 1000 tokens de salida por minuto, y Groq rechaza de entrada cualquier
/// petición cuya respuesta *estime* más larga: «Limit 1000, Requested 1935… reduce max_tokens». Como
/// Sakura no pedía ningún máximo, el proveedor suponía el del modelo y siempre se pasaba, así que el
/// chat se quedaba mudo con un error en inglés.
///
/// La respuesta del proveedor dice el número exacto, así que no hay que adivinarlo: se lee, se pide
/// un poco menos y se repite la petición. Pedir un máximo siempre, por si acaso, sería peor: cortaría
/// las respuestas largas —una presentación entera— en los proveedores que no tienen ese límite.
/// </summary>
public static partial class AiProviderLimits
{
    /// <summary>Margen para que la estimación del proveedor no vuelva a pasarse por poco.</summary>
    private const int Margin = 128;

    private const int Minimum = 256;

    /// <summary>
    /// El máximo de tokens que se puede pedir según lo que dijo el proveedor, o nulo si el error no
    /// hablaba de un límite de salida.
    /// </summary>
    public static int? OutputLimit(string? providerError)
    {
        if (string.IsNullOrWhiteSpace(providerError) || !IsOutputLimit(providerError))
        {
            return null;
        }

        var match = LimitNumber().Match(providerError);
        if (!match.Success || !int.TryParse(match.Groups["limit"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var limit) || limit <= 0)
        {
            return null;
        }

        return Math.Max(Minimum, limit - Margin);
    }

    /// <summary>Si el proveedor rechazó la petición por el tamaño de la respuesta, no por otra cosa.</summary>
    public static bool IsOutputLimit(string? providerError) =>
        providerError is { Length: > 0 } && OutputLimitWords().IsMatch(providerError);

    /// <summary>Qué decirle a quien lo está usando cuando ni siquiera el reintento cabe.</summary>
    public static string Explain(int? limit) =>
        limit is { } value
            ? $"Tu plan del proveedor solo admite respuestas de unos {value} tokens por minuto con este modelo. " +
              "Espera un minuto o pídeme algo más corto."
            : "El proveedor limitó el tamaño de la respuesta. Espera un minuto o pídeme algo más corto.";

    [GeneratedRegex(@"\blimit\s*:?\s*(?<limit>\d{2,7})\b", RegexOptions.IgnoreCase)]
    private static partial Regex LimitNumber();

    [GeneratedRegex(@"reduce\s+max_?tokens|max_?tokens|tokens?\s+per\s+minute|\botpm\b|request too large|too many tokens", RegexOptions.IgnoreCase)]
    private static partial Regex OutputLimitWords();
}
