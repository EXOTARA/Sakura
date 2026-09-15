using Nexo.Core.Ai;

namespace Nexo.Core.Vision;

/// <summary>
/// Diseño D5.7 (Fase 2 — Sakura Lens) — decide qué resaltar en pantalla a partir de la respuesta
/// de la IA: no hay un mecanismo de citas/referencias estructuradas todavía (eso requeriría pedirle
/// a la IA una salida estructurada, un cambio mayor de prompt), así que esta primera versión usa
/// una heurística honesta y simple — si el texto de una línea de OCR o el nombre de un elemento de
/// UI Automation aparece tal cual dentro de la respuesta, se resalta esa región. Puede fallar en
/// ambas direcciones (no resaltar algo relevante que la IA parafraseó, o resaltar una coincidencia
/// casual) — es guía visual aproximada, no una prueba de que "esto es exactamente a lo que se
/// refiere la IA".
///
/// Función pura: no captura nada, no llama a la IA. Espera que <paramref name="redactedOcr"/> y
/// <paramref name="redactedElements"/> ya hayan pasado por <see cref="SensitiveContentRedactor"/>
/// — un placeholder "[REDACTADO]" nunca genera un resaltado.
/// </summary>
public static partial class LensHighlightMatcher
{
    private const int MinimumMatchLength = 4;

    public static IReadOnlyList<LensHighlightRegion> FindMatches(
        string? answerText,
        OcrResult redactedOcr,
        IReadOnlyList<UiAutomationElement> redactedElements,
        int windowLeft,
        int windowTop)
    {
        ArgumentNullException.ThrowIfNull(redactedOcr);
        ArgumentNullException.ThrowIfNull(redactedElements);

        if (string.IsNullOrWhiteSpace(answerText) || !redactedOcr.IsSuccess)
        {
            return [];
        }

        var normalizedAnswer = VisionIntentPolicy.Normalize(answerText);
        var regions = new List<LensHighlightRegion>();

        foreach (var line in redactedOcr.Lines)
        {
            if (IsMatch(line.Text, normalizedAnswer))
            {
                regions.Add(new LensHighlightRegion(
                    windowLeft + line.Left, windowTop + line.Top, line.Width, line.Height));
            }
        }

        foreach (var element in redactedElements)
        {
            // Las coordenadas de UI Automation ya son absolutas de pantalla (a diferencia de las
            // líneas de OCR, relativas a la captura) — no se suma windowLeft/windowTop de nuevo.
            if (IsMatch(element.Name, normalizedAnswer))
            {
                regions.Add(new LensHighlightRegion(
                    element.Left, element.Top, element.Width, element.Height));
            }
        }

        return regions.Distinct().ToArray();
    }

    /// <summary>Tope de recuadros al explicar una ventana: más de tres ya no guían, tapan.</summary>
    public const int MaximumActionTargets = 3;

    private static readonly string[] ActionControlTypes =
        ["Button", "MenuItem", "Hyperlink", "TabItem", "CheckBox", "RadioButton", "ComboBox", "SplitButton"];

    /// <summary>Los botones de la barra de título: nunca son un paso, y se nombran en cualquier explicación.</summary>
    private static readonly string[] CaptionButtons = ["minimizar", "maximizar", "restaurar", "cerrar", "minimize", "maximize", "restore", "close"];

    /// <summary>
    /// 2026-09-15 — los recuadros de Ctrl + Shift + Espacio. Con la regla general se resaltaba todo lo
    /// que la respuesta nombraba —el logo, la barra de direcciones, pestañas, párrafos enteros— y la
    /// ventana quedaba llena de marcas sin sentido (Adler, con una captura). Al explicar solo importa
    /// dónde hay que pulsar, así que se exige mucho más:
    /// · solo en los apartados «Cómo resolverlo» y «Pasos»;
    /// · solo nombres que la respuesta destaca, en negrita o entre comillas, y que coinciden enteros con
    ///   el nombre de un control (en la primera prueba, «configuración del sistema» marcaba el engranaje
    ///   del Bloc de notas);
    /// · solo controles que se pulsan, nunca los de la barra de título;
    /// · como mucho tres, en el orden de los pasos.
    /// </summary>
    public static IReadOnlyList<LensHighlightRegion> FindActionTargets(
        string? answerText,
        IReadOnlyList<UiAutomationElement> redactedElements)
    {
        ArgumentNullException.ThrowIfNull(redactedElements);

        var actionText = ActionSections(answerText);
        if (actionText.Length == 0)
        {
            return [];
        }

        var named = HighlightedNames().Matches(actionText)
            .Select(match => (name: CleanName(match.Groups.Cast<System.Text.RegularExpressions.Group>().Skip(1).First(group => group.Success).Value), match.Index))
            .Where(item => item.name.Length >= MinimumMatchLength)
            .ToList();

        var regions = new List<(int Position, LensHighlightRegion Region)>();
        foreach (var element in redactedElements)
        {
            if (element.Width <= 0 || element.Height <= 0 ||
                string.IsNullOrWhiteSpace(element.Name) ||
                element.Name.Contains(SensitiveContentRedactor.Placeholder, StringComparison.Ordinal) ||
                !ActionControlTypes.Any(type => element.ControlType.EndsWith(type, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var elementName = CleanName(VisionIntentPolicy.Normalize(element.Name));
            if (CaptionButtons.Contains(elementName))
            {
                continue;
            }

            var hit = named.FirstOrDefault(item => item.name == elementName);
            if (hit.name is not null)
            {
                regions.Add((hit.Index, new LensHighlightRegion(element.Left, element.Top, element.Width, element.Height)));
            }
        }

        return regions
            .OrderBy(item => item.Position)
            .Select(item => item.Region)
            .Distinct()
            .Take(MaximumActionTargets)
            .ToArray();
    }

    private static string CleanName(string value) =>
        value.Trim().Trim('.', ',', ':', ';', '>', '→', ' ');

    [System.Text.RegularExpressions.GeneratedRegex(@"\*\*(.+?)\*\*|«(.+?)»|""(.+?)""|“(.+?)”")]
    private static partial System.Text.RegularExpressions.Regex HighlightedNames();

    /// <summary>El texto desde el apartado «Cómo resolverlo» (o «Pasos») hasta el final.</summary>
    private static string ActionSections(string? answerText)
    {
        if (string.IsNullOrWhiteSpace(answerText))
        {
            return string.Empty;
        }

        var normalized = VisionIntentPolicy.Normalize(answerText);
        var start = normalized.IndexOf("como resolverlo", StringComparison.Ordinal);
        if (start < 0)
        {
            start = normalized.IndexOf("pasos", StringComparison.Ordinal);
        }

        // Se devuelve ya normalizado (sin tildes y en minúsculas): así se compara con los nombres de
        // los controles, normalizados igual.
        return start < 0 ? string.Empty : normalized[start..];
    }

    private static bool IsMatch(string? candidateText, string normalizedAnswer)
    {
        if (string.IsNullOrWhiteSpace(candidateText) ||
            candidateText.Contains(SensitiveContentRedactor.Placeholder, StringComparison.Ordinal))
        {
            return false;
        }

        var normalizedCandidate = VisionIntentPolicy.Normalize(candidateText);
        return normalizedCandidate.Length >= MinimumMatchLength &&
            normalizedAnswer.Contains(normalizedCandidate, StringComparison.Ordinal);
    }
}
