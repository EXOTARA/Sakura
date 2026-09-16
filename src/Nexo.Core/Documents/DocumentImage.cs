using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Nexo.Core.Documents;

/// <summary>Una imagen ya descargada, lista para entrar en la presentación, con su autoría.</summary>
public sealed record DocumentImage(
    byte[] Bytes,
    string Extension,
    int Width,
    int Height,
    string Title,
    string Author,
    string License,
    string SourceUrl);

/// <summary>Una imagen que Wikimedia Commons ofrece para una búsqueda, antes de elegir y descargar.</summary>
public sealed record DocumentImageCandidate(
    string Title,
    string Mime,
    int Width,
    int Height,
    string DownloadUrl,
    string Artist,
    string License,
    string Restrictions,
    string PageUrl);

/// <summary>
/// 2026-09-16 — «Imagen: …» es como una respuesta pide una foto para esa parte del documento. Vive
/// aquí y no en el plan de la presentación porque ahora también lo usa el Word.
/// </summary>
public static partial class ImageDirective
{
    /// <summary>El término de un párrafo que pide imagen, o nulo si el párrafo no lo es.</summary>
    public static string? Query(string text)
    {
        var match = Prefix().Match(text.Trim());
        return match.Success && match.Groups["text"].Value.Trim() is { Length: > 0 } query ? query : null;
    }

    /// <summary>Todo lo que una respuesta pide, por orden y sin repetir.</summary>
    public static IReadOnlyList<string> Queries(string markdown) =>
        Assistant.AnswerMarkdown.Parse(markdown)
            .OfType<Assistant.AnswerParagraph>()
            .Select(paragraph => Query(Assistant.AnswerMarkdown.ToPlainText(paragraph.Spans)))
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    [GeneratedRegex(@"^(imagen|foto|fotograf[ií]a|ilustraci[oó]n)\s*:\s*(?<text>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex Prefix();
}

/// <summary>Busca una imagen de licencia libre para un documento.</summary>
public interface IDocumentImageSource
{
    /// <param name="query">Lo que hay que buscar, tal como lo escribió el modelo.</param>
    /// <returns>La imagen elegida, o nulo si no hay ninguna que valga.</returns>
    Task<DocumentImage?> FindAsync(string query, CancellationToken cancellationToken = default);
}

/// <summary>
/// 2026-09-15 — qué imagen de Wikimedia Commons vale para una diapositiva y cómo se cita (Adler:
/// «presentaciones con imágenes»).
///
/// Se busca en Commons porque no pide cuenta ni clave y todo lo que hay es de licencia libre. Aun así
/// libre no es «de nadie»: casi todas piden citar al autor y la licencia, así que la presentación
/// siempre lleva el crédito bajo la imagen y una diapositiva final con la ficha completa.
///
/// De lo que ofrece la búsqueda solo sirve una parte: PowerPoint no dibuja WebP ni SVG, una panorámica
/// de 11.000 × 2.300 no llena un hueco vertical sin quedarse en una franja, y un archivo con
/// restricciones (marcas, derechos de imagen) no se puede poner en un trabajo sin más. Por eso se
/// descarta en vez de recortar a la fuerza.
/// </summary>
public static partial class WikimediaImagePolicy
{
    private const int MinimumWidth = 700;
    private const int MinimumHeight = 500;

    /// <summary>Formatos que Office dibuja sin sorpresas.</summary>
    public static string? Extension(string mime) => mime.Trim().ToLowerInvariant() switch
    {
        "image/jpeg" or "image/jpg" => "jpg",
        "image/png" => "png",
        _ => null
    };

    /// <summary>
    /// Las búsquedas que se intentan, de la más fiel a la más abierta. Commons no entiende una frase
    /// entera: «paneles solares en un tejado» no devuelve nada y «paneles solares tejado» sí, así que
    /// si la primera no da resultados se prueba sin palabras de relleno y luego con lo esencial.
    /// </summary>
    public static IReadOnlyList<string> SearchTerms(string query)
    {
        var words = Spaces().Split(query.Trim()).Where(word => word.Length > 0).ToList();
        var meaningful = words.Where(word => !Filler.Contains(word.Trim(',', '.', ';', ':').ToLowerInvariant())).ToList();

        // Se va soltando la última palabra, no se salta a dos de golpe: «líneas alta tensión» todavía
        // es lo que se pedía; «líneas alta», ya casi cualquier cosa.
        var attempts = new List<string> { string.Join(' ', words) };
        foreach (var take in new[] { meaningful.Count, 3, 2 })
        {
            if (take > 0 && take <= meaningful.Count)
            {
                attempts.Add(string.Join(' ', meaningful.Take(take)));
            }
        }

        return attempts.Where(attempt => attempt.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Palabras que no dicen qué buscar.</summary>
    private static readonly HashSet<string> Filler =
    [
        "de", "del", "el", "la", "los", "las", "un", "una", "unos", "unas", "en", "al", "con", "por",
        "para", "y", "o", "sobre", "sus", "su", "desde", "entre", "como", "que", "foto", "imagen",
        "fotografía", "fotografia", "ilustración", "ilustracion", "the", "of", "a", "an", "in", "on"
    ];

    /// <summary>
    /// La primera imagen aprovechable, respetando el orden de la búsqueda: quien busca «turbinas
    /// eólicas» espera la que Commons considera más relevante, no la más grande.
    /// </summary>
    public static DocumentImageCandidate? Choose(IEnumerable<DocumentImageCandidate> candidates)
    {
        var usable = candidates.Where(candidate =>
            Extension(candidate.Mime) is not null &&
            string.IsNullOrWhiteSpace(candidate.Restrictions) &&
            candidate.Width >= MinimumWidth &&
            candidate.Height >= MinimumHeight &&
            Ratio(candidate) is >= 0.7 and <= 2.4).ToList();

        // Entre las que valen, primero una apaisada y grande: es la que llena un hueco de diapositiva
        // sin recortar media foto. Si no hay ninguna así, la primera que la búsqueda haya puesto antes.
        return usable.FirstOrDefault(candidate => candidate.Width >= 1000 && Ratio(candidate) is >= 1.2 and <= 2.0)
               ?? usable.FirstOrDefault();
    }

    private static double Ratio(DocumentImageCandidate candidate) =>
        candidate.Height == 0 ? 0 : (double)candidate.Width / candidate.Height;

    /// <summary>
    /// El crédito corto que va bajo la imagen: «Autor · CC BY 4.0». Commons devuelve el autor con
    /// etiquetas HTML dentro, y en una diapositiva eso se vería tal cual.
    /// </summary>
    public static string Credit(string? artist, string? license)
    {
        var author = CleanText(artist);
        var terms = CleanText(license);
        if (author.Length == 0 && terms.Length == 0)
        {
            return "Wikimedia Commons";
        }

        var parts = new List<string>();
        if (author.Length > 0)
        {
            parts.Add(author.Length > 60 ? author[..60].TrimEnd() + "…" : author);
        }

        parts.Add(terms.Length > 0 ? terms : "Wikimedia Commons");
        return string.Join(" · ", parts);
    }

    /// <summary>El nombre del archivo tal como se lee: «File:Wind turbines, Crimea.jpg» → «Wind turbines, Crimea».</summary>
    public static string FileTitle(string title)
    {
        var clean = title.Trim();
        if (clean.StartsWith("File:", StringComparison.OrdinalIgnoreCase) || clean.StartsWith("Archivo:", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean[(clean.IndexOf(':') + 1)..];
        }

        var dot = clean.LastIndexOf('.');
        if (dot > 0 && clean.Length - dot <= 5)
        {
            clean = clean[..dot];
        }

        return clean.Replace('_', ' ').Trim();
    }

    /// <summary>Texto plano a partir de lo que devuelve la API, que llega con HTML y entidades.</summary>
    public static string CleanText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var text = HtmlTag().Replace(html, " ");
        text = HtmlEntity().Replace(text, match =>
        {
            var value = match.Groups["name"].Value;
            if (value.StartsWith('#'))
            {
                var digits = value[1..];
                var number = digits.StartsWith('x') || digits.StartsWith('X')
                    ? int.TryParse(digits[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex) ? hex : -1
                    : int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code) ? code : -1;
                return number is > 0 and <= 0x10FFFF ? char.ConvertFromUtf32(number) : string.Empty;
            }

            return value.ToLowerInvariant() switch
            {
                "amp" => "&",
                "lt" => "<",
                "gt" => ">",
                "quot" => "\"",
                "apos" or "#39" => "'",
                "nbsp" => " ",
                _ => string.Empty
            };
        });

        return Spaces().Replace(text, " ").Trim();
    }

    /// <summary>La ficha para la diapositiva de créditos.</summary>
    public static string CreditLine(DocumentImage image)
    {
        var builder = new StringBuilder(image.Title);
        if (image.Author.Length > 0)
        {
            builder.Append(", de ").Append(image.Author);
        }

        if (image.License.Length > 0)
        {
            builder.Append(" (").Append(image.License).Append(')');
        }

        return builder.Append(". Wikimedia Commons: ").Append(image.SourceUrl).ToString();
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTag();

    [GeneratedRegex(@"&(?<name>#?[a-zA-Z0-9]{1,8});")]
    private static partial Regex HtmlEntity();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
