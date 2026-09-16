using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Nexo.Core.Documents;

/// <summary>Un autor, con los apellidos y el nombre separados, que es como los pide APA.</summary>
public sealed record SourceAuthor(string Family, string? Given);

/// <summary>Qué clase de fuente es, porque APA la escribe distinto.</summary>
public enum SourceKind
{
    Article,
    Book,
    Web
}

/// <summary>Una fuente encontrada: lo justo para escribir su referencia y poder ir a leerla.</summary>
public sealed record DocumentSource(
    IReadOnlyList<SourceAuthor> Authors,
    int? Year,
    string Title,
    string? Container,
    string? Volume = null,
    string? Issue = null,
    string? Pages = null,
    string? Doi = null,
    string? Url = null,
    SourceKind Kind = SourceKind.Article,
    string? Language = null);

/// <summary>Busca fuentes sobre un tema.</summary>
public interface IDocumentSourceSearch
{
    Task<IReadOnlyList<DocumentSource>> FindAsync(string topic, CancellationToken cancellationToken = default);
}

/// <summary>
/// 2026-09-16 — qué fuentes valen para un borrador y cómo se escriben en APA 7 (Adler: «información
/// en español, fuentes actuales, fuentes confiables y no solo lo que yo le di… por lo menos 5»).
///
/// Vale una fuente que se pueda comprobar: publicada, con año, con autor y con una dirección a la que
/// ir. Se pide en español y de los últimos años porque es lo que sirve para un trabajo suyo, y se
/// descarta lo repetido y lo que no lleva ni DOI ni enlace: una referencia que no se puede abrir no
/// se puede verificar, y una que no se puede verificar no debería estar en un trabajo.
///
/// Sakura no afirma que estas fuentes digan lo que el borrador dice. Son puntos de partida para
/// leerlas; eso queda escrito en el propio documento.
/// </summary>
public static partial class SourcePolicy
{
    /// <summary>Lo que Adler pidió: cinco como mínimo cuando las haya.</summary>
    public const int Minimum = 5;

    public const int Maximum = 8;

    /// <summary>Años hacia atrás que se consideran «actuales».</summary>
    public const int Years = 8;

    public static IReadOnlyList<DocumentSource> Choose(IEnumerable<DocumentSource> candidates, int? currentYear = null)
    {
        var floor = (currentYear ?? DateTime.Now.Year) - Years;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var chosen = new List<DocumentSource>();

        foreach (var source in candidates)
        {
            if (source.Year is not { } year || year < floor ||
                string.IsNullOrWhiteSpace(source.Title) ||
                source.Authors.Count == 0 ||
                (string.IsNullOrWhiteSpace(source.Doi) && string.IsNullOrWhiteSpace(source.Url)) ||
                (source.Language is { Length: > 0 } language && !language.StartsWith("es", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (!seen.Add(source.Doi ?? Normalize(source.Title)))
            {
                continue;
            }

            chosen.Add(source with { Title = Sentence(source.Title) });
            if (chosen.Count == Maximum)
            {
                break;
            }
        }

        return chosen;
    }

    /// <summary>
    /// Un título que llega A GRITOS se escribe como una frase. Las revistas lo mandan así por su
    /// maquetación, no porque se cite así.
    /// </summary>
    public static string Sentence(string title)
    {
        var clean = Spaces().Replace(title.Trim(), " ").TrimEnd('.', ' ');
        var letters = clean.Where(char.IsLetter).ToList();
        if (letters.Count > 0 && letters.All(char.IsUpper))
        {
            clean = CultureInfo.GetCultureInfo("es-ES").TextInfo.ToLower(clean);
            clean = char.ToUpper(clean[0], CultureInfo.GetCultureInfo("es-ES")) + clean[1..];
        }

        return clean;
    }

    private static string Normalize(string text) => Spaces().Replace(text.Trim().ToLowerInvariant(), " ");

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}

/// <summary>
/// 2026-09-16 — la referencia de una fuente en APA 7ª edición, tal como se escribe en la lista final.
///
/// Se escribe aquí, y no se le pide al modelo, porque un modelo escribe referencias con el formato
/// correcto y los datos inventados. Estas salen de lo que devolvió el catálogo: si falta el volumen o
/// las páginas, la referencia va sin ellos en lugar de rellenarlos.
/// </summary>
public static class ApaReference
{
    public static string Format(DocumentSource source)
    {
        var builder = new StringBuilder(Authors(source.Authors));
        builder.Append(" (").Append(source.Year?.ToString(CultureInfo.InvariantCulture) ?? "s. f.").Append("). ");
        builder.Append(source.Title.TrimEnd('.')).Append('.');

        if (!string.IsNullOrWhiteSpace(source.Container))
        {
            builder.Append(' ').Append(source.Container!.Trim().TrimEnd('.'));
            if (source.Kind == SourceKind.Article)
            {
                if (!string.IsNullOrWhiteSpace(source.Volume))
                {
                    builder.Append(", ").Append(source.Volume!.Trim());
                    if (!string.IsNullOrWhiteSpace(source.Issue))
                    {
                        builder.Append('(').Append(source.Issue!.Trim()).Append(')');
                    }
                }

                if (!string.IsNullOrWhiteSpace(source.Pages))
                {
                    builder.Append(", ").Append(source.Pages!.Trim().Replace("-", "–"));
                }
            }

            builder.Append('.');
        }

        var link = !string.IsNullOrWhiteSpace(source.Doi)
            ? "https://doi.org/" + source.Doi!.Trim().Replace("https://doi.org/", string.Empty, StringComparison.OrdinalIgnoreCase)
            : source.Url;
        if (!string.IsNullOrWhiteSpace(link))
        {
            builder.Append(' ').Append(link!.Trim());
        }

        return builder.ToString();
    }

    /// <summary>Los autores como los escribe APA: «Vega Niño, M. A., Peralta Polo, J. M., y Caro, M. A.».</summary>
    public static string Authors(IReadOnlyList<SourceAuthor> authors)
    {
        if (authors.Count == 0)
        {
            return "Autor desconocido";
        }

        // APA 7 lista hasta veinte autores; con más, los diecinueve primeros, puntos suspensivos y el último.
        var listed = authors.Count <= 20
            ? authors.Select(Name).ToList()
            : authors.Take(19).Select(Name).Append("...").Append(Name(authors[^1])).ToList();

        if (listed.Count == 1)
        {
            return listed[0];
        }

        return string.Join(", ", listed.Take(listed.Count - 1)) + " y " + listed[^1];
    }

    private static string Name(SourceAuthor author)
    {
        var family = author.Family.Trim();
        if (string.IsNullOrWhiteSpace(author.Given))
        {
            return family;
        }

        var initials = author.Given!
            .Split([' ', '-', '.'], StringSplitOptions.RemoveEmptyEntries)
            .Where(part => char.IsLetter(part[0]))
            .Select(part => char.ToUpper(part[0], CultureInfo.GetCultureInfo("es-ES")) + ".");

        return family + ", " + string.Join(" ", initials);
    }
}
