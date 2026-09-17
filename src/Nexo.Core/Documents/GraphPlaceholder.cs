using System.Text.RegularExpressions;

namespace Nexo.Core.Documents;

/// <summary>
/// 2026-09-16 — «[Gráfica de GeoGebra: …]» en un borrador: Sakura no dibuja la gráfica de la tarea
/// (la tiene que hacer la persona), pero deja el hueco enmarcado y el pie de figura ya numerado
/// (decidido con Adler en el Borrador de tarea).
/// </summary>
public static partial class GraphPlaceholder
{
    public static string? Description(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        var match = Pattern().Match(line.Trim());
        return match.Success ? match.Groups["what"].Value.Trim() : null;
    }

    [GeneratedRegex(@"^\[\s*gr[aá]fica(?:\s+de\s+geogebra)?\s*:\s*(?<what>.+?)\s*\]\.?$", RegexOptions.IgnoreCase)]
    private static partial Regex Pattern();
}
