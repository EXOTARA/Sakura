using System.Text;
using Nexo.Core.Ai;

namespace Nexo.Core.Vision;

/// <summary>
/// Diseño D5.6 (Fase 2 — Sakura Lens) — combina el título de la ventana activa, el resultado de
/// OCR y los elementos de UI Automation en un <see cref="LensContext"/> listo para enviar, con la
/// pregunta y el <see cref="AiRequestMode"/> que corresponden al modo elegido.
///
/// Función pura: no captura nada, no redacta nada. Quien llame es responsable de haber pasado
/// <paramref name="redactedOcr"/> y <paramref name="redactedElements"/> por
/// <see cref="SensitiveContentRedactor"/> antes — este builder solo compone lo que ya es seguro
/// de incluir.
/// </summary>
public static class LensContextBuilder
{
    private const int MaxOcrCharacters = 3000;
    private const int MaxUiElements = 40;

    public static LensContext Build(
        LensMode mode,
        string windowTitle,
        OcrResult redactedOcr,
        IReadOnlyList<UiAutomationElement> redactedElements)
    {
        ArgumentNullException.ThrowIfNull(redactedOcr);
        ArgumentNullException.ThrowIfNull(redactedElements);

        return new LensContext(
            BuildPrompt(mode),
            BuildSystemContext(mode, windowTitle, redactedOcr, redactedElements),
            ResolveRequestMode(mode, redactedOcr));
    }

    /// <summary>
    /// Lo que se pide al explicar una ventana. Cuatro apartados fijos, en este orden, para que la
    /// respuesta se lea de un vistazo en la píldora; y la advertencia de no inventar un problema
    /// cuando no lo hay, que es el fallo típico de pedir «cómo resolverlo» sobre una ventana normal.
    /// </summary>
    public const string ExplainPrompt =
        "Explícame esta ventana. Responde con estos cuatro apartados, en este orden, cada uno con su " +
        "título en negrita: **Qué es** (qué aplicación o página es y para qué sirve lo que se ve), " +
        "**Qué está pasando** (lo importante: un error, un aviso, algo a medias o lo que se está haciendo), " +
        "**Cómo resolverlo** (si hay algo que resolver; si no, qué se puede hacer aquí) y **Pasos** " +
        "(numerados y concretos, con los nombres de botones y menús tal como aparecen en la ventana). " +
        "Sé breve. Si no hay ningún problema, dilo claramente y no inventes uno.";

    private static string BuildPrompt(LensMode mode) => mode switch
    {
        LensMode.Soporte => "¿Qué problema hay en esta ventana y cómo lo resuelvo?",
        LensMode.Estudio => "¿Qué es esto y cómo funciona? Explícamelo paso a paso.",
        LensMode.Desarrollo => "Analiza el código o error visible aquí y dime qué corregir.",
        LensMode.Explicar => ExplainPrompt,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Modo de Lens no reconocido.")
    };

    private static AiRequestMode ResolveRequestMode(LensMode mode, OcrResult redactedOcr) => mode switch
    {
        LensMode.Desarrollo => AiRequestMode.VisionTechnicalDiagnostic,
        LensMode.Estudio => AiRequestMode.VisionGeneral,
        LensMode.Soporte or LensMode.Explicar => VisionIntentPolicy.Resolve(redactedOcr.FullText, hasImages: true),
        _ => AiRequestMode.VisionGeneral
    };

    private static string BuildSystemContext(
        LensMode mode,
        string windowTitle,
        OcrResult redactedOcr,
        IReadOnlyList<UiAutomationElement> redactedElements)
    {
        var builder = new StringBuilder();
        builder.Append("Modo Sakura Lens: ").Append(ModeLabel(mode)).Append('\n');
        builder.Append("Ventana activa: ")
            .Append(string.IsNullOrWhiteSpace(windowTitle) ? "(sin título)" : windowTitle)
            .Append('\n');

        if (redactedOcr.IsSuccess && !string.IsNullOrWhiteSpace(redactedOcr.FullText))
        {
            var ocrText = redactedOcr.FullText.Length > MaxOcrCharacters
                ? redactedOcr.FullText[..MaxOcrCharacters] + "…"
                : redactedOcr.FullText;
            builder.Append("\nTexto detectado en pantalla (OCR):\n").Append(ocrText).Append('\n');
        }

        var namedElements = redactedElements
            .Where(element => !string.IsNullOrWhiteSpace(element.Name))
            .Take(MaxUiElements)
            .ToArray();

        if (namedElements.Length > 0)
        {
            builder.Append("\nElementos de interfaz visibles:\n");
            foreach (var element in namedElements)
            {
                builder.Append("- ").Append(element.Name)
                    .Append(" (").Append(FormatControlType(element.ControlType)).Append(")\n");
            }
        }

        builder.Append(
            "\nLa imagen y este texto ya pasaron por redacción de contenido sensible evidente " +
            "(contraseñas, tarjetas, tokens) antes de llegar aquí.");

        return builder.ToString();
    }

    private static string ModeLabel(LensMode mode) => mode switch
    {
        LensMode.Soporte => "Soporte",
        LensMode.Estudio => "Estudio",
        LensMode.Desarrollo => "Desarrollo",
        LensMode.Explicar => "Explicar",
        _ => mode.ToString()
    };

    private static string FormatControlType(string controlType) =>
        controlType.StartsWith("ControlType.", StringComparison.Ordinal)
            ? controlType["ControlType.".Length..]
            : controlType;
}
