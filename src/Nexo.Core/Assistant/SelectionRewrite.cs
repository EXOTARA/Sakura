namespace Nexo.Core.Assistant;

public enum SelectionAction
{
    Clearer,
    Fix,
    Shorter,
    Summarize,
    ToEnglish,
    ToSpanish
}

/// <summary>
/// 2026-09-16 — hacer algo con el texto seleccionado en cualquier aplicación (Alt+Shift+R).
///
/// La investigación (docs/research/QUE_SE_BUSCA_EN_UN_ASISTENTE.md) lo pone entre lo más pedido: no
/// tener que copiar, abrir otro programa, pegar y volver. Se pide solo el resultado, sin
/// explicaciones, porque el resultado puede reemplazar la selección tal cual.
/// </summary>
public static class SelectionRewrite
{
    public const int MaximumLength = 8000;

    public static IReadOnlyList<SelectionAction> All { get; } =
    [
        SelectionAction.Clearer,
        SelectionAction.Fix,
        SelectionAction.Shorter,
        SelectionAction.Summarize,
        SelectionAction.ToEnglish,
        SelectionAction.ToSpanish
    ];

    public static string Label(SelectionAction action) => action switch
    {
        SelectionAction.Clearer => "Más claro",
        SelectionAction.Fix => "Corregir",
        SelectionAction.Shorter => "Más corto",
        SelectionAction.Summarize => "Resumir",
        SelectionAction.ToEnglish => "Al inglés",
        SelectionAction.ToSpanish => "Al español",
        _ => action.ToString()
    };

    /// <summary>Si se puede trabajar con lo seleccionado; si no, por qué, dicho para la persona.</summary>
    public static (bool CanRun, string Detail) Check(string? text, bool aiEnabled)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return (false, "Selecciona un texto primero y vuelve a pulsar Alt+Shift+R.");
        }

        if (text.Length > MaximumLength)
        {
            return (false, $"La selección es muy larga ({text.Length} caracteres). Prueba con menos de {MaximumLength}.");
        }

        if (!aiEnabled)
        {
            return (false, "Para esto hace falta un modelo de IA. Actívalo en Personalizar → IA.");
        }

        return (true, string.Empty);
    }

    public static string BuildPrompt(SelectionAction action, string text)
    {
        var task = action switch
        {
            SelectionAction.Clearer => "Reescribe el texto para que se entienda mejor, con el mismo significado, tono e idioma.",
            SelectionAction.Fix => "Corrige ortografía, gramática y puntuación sin cambiar el estilo, el tono ni el idioma.",
            SelectionAction.Shorter => "Reescribe el texto más corto, conservando lo importante, el tono y el idioma.",
            SelectionAction.Summarize => "Resume el texto en pocas líneas, en el mismo idioma.",
            SelectionAction.ToEnglish => "Traduce el texto al inglés, natural y fiel.",
            SelectionAction.ToSpanish => "Traduce el texto al español, natural y fiel.",
            _ => "Reescribe el texto."
        };

        return task +
               " Devuelve solo el resultado, sin comillas, sin títulos y sin explicar lo que hiciste: " +
               "se va a pegar en lugar del original. Conserva los saltos de línea y los datos (nombres, cifras, enlaces) tal cual.\n\n" +
               "Texto:\n" + text;
    }

    /// <summary>Quita lo que los modelos a veces añaden aunque se les pida que no.</summary>
    public static string Clean(string result)
    {
        var cleaned = result.Trim();
        if (cleaned.Length >= 2 &&
            ((cleaned[0] == '"' && cleaned[^1] == '"') || (cleaned[0] == '«' && cleaned[^1] == '»')))
        {
            cleaned = cleaned[1..^1].Trim();
        }

        return cleaned;
    }
}
