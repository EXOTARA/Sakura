namespace Nexo.Core.Ai;

/// <summary>
/// 2026-09-15 — lo que se enseña mientras Ollama descarga un modelo. Ollama manda sus pasos en inglés
/// y con el resumen de cada capa («pulling 6a0746a1ec1a», «verifying sha256 digest»), y el gestor de
/// modelos los copiaba tal cual. Aquí se convierten en una frase corta en castellano y, si hay
/// cifras, en cuánto va de cuánto.
/// </summary>
public static class OllamaPullStatusText
{
    public static string Describe(OllamaPullProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);

        var step = Step(progress.Status);
        if (progress.Completed is { } completed && progress.Total is > 0 and var total)
        {
            return $"{step} · {FormatBytes(completed)} de {FormatBytes(total)}";
        }

        return step;
    }

    public static string Step(string? status)
    {
        var text = status?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return "Descargando…";
        }

        if (text.Equals("pulling manifest", StringComparison.OrdinalIgnoreCase))
        {
            return "Preparando la descarga…";
        }

        if (text.StartsWith("pulling ", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("downloading", StringComparison.OrdinalIgnoreCase))
        {
            return "Descargando…";
        }

        if (text.StartsWith("verifying", StringComparison.OrdinalIgnoreCase))
        {
            return "Comprobando que llegó entero…";
        }

        if (text.StartsWith("writing manifest", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("removing", StringComparison.OrdinalIgnoreCase))
        {
            return "Guardando…";
        }

        if (text.Equals("success", StringComparison.OrdinalIgnoreCase))
        {
            return "Listo";
        }

        // Un paso que no conocemos se enseña tal cual: es mejor que ocultar lo que está pasando.
        return text;
    }

    private static string FormatBytes(long bytes)
    {
        var gigabytes = bytes / 1024d / 1024d / 1024d;
        return gigabytes >= 1
            ? $"{gigabytes:0.0} GB"
            : $"{bytes / 1024d / 1024d:0} MB";
    }
}
