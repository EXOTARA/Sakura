namespace Nexo.Core.Ai;

/// <summary>
/// 2026-09-15 — qué modelo usar cuando una petición lleva imagen y el modelo elegido no las lee
/// (Adler, con Groq: su modelo, gpt-oss-120b, solo lee texto, así que Ctrl + Shift + Espacio y Lens
/// trabajaban solo con lo que el OCR sacaba de la pantalla).
///
/// No se cambia el modelo de la persona: solo esa petición va a un modelo del mismo proveedor que sí
/// lee imágenes, con la misma clave, y únicamente si aparece en la lista de modelos que el proveedor
/// dice tener. Los nombres cambian con el tiempo —Groq retiró los Llama 4 con visión en 2026—, por eso
/// se comprueba contra la lista y no se da nada por hecho.
/// </summary>
public static class AiVisionModelPolicy
{
    /// <summary>Modelos con visión conocidos por proveedor, del preferido al último recurso.</summary>
    public static IReadOnlyList<string> CandidatesFor(AiProviderKind provider) => provider switch
    {
        // Probado el 15 de septiembre de 2026 con la clave de Adler: qwen3.8-27b leyó el texto de una imagen.
        AiProviderKind.Groq =>
        [
            "qwen/qwen3.8-27b",
            "meta-llama/llama-4-scout-17b-16e-instruct",
            "meta-llama/llama-4-maverick-17b-128e-instruct"
        ],
        AiProviderKind.OpenRouter =>
        [
            "google/gemini-2.5-flash",
            "qwen/qwen2.5-vl-72b-instruct:free",
            "meta-llama/llama-4-scout:free"
        ],
        _ => []
    };

    /// <summary>
    /// Modelos que se sabe que solo leen texto: con ellos no merece la pena mandar la imagen primero y
    /// esperar el rechazo.
    /// </summary>
    public static bool IsKnownTextOnly(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return false;
        }

        string[] textOnly = ["gpt-oss", "allam", "compound", "whisper", "prompt-guard", "orpheus", "safeguard"];
        return textOnly.Any(part => model.Contains(part, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>El primer candidato que el proveedor tiene de verdad y que no es el modelo actual.</summary>
    public static string? Choose(AiProviderKind provider, string? currentModel, IReadOnlyCollection<string> availableModels)
    {
        ArgumentNullException.ThrowIfNull(availableModels);

        return CandidatesFor(provider).FirstOrDefault(candidate =>
            !string.Equals(candidate, currentModel, StringComparison.OrdinalIgnoreCase) &&
            availableModels.Contains(candidate, StringComparer.OrdinalIgnoreCase));
    }
}
