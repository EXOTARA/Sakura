using System.Text.Json;
using Nexo.Core.Documents;

namespace Nexo.Windows.Documents;

/// <summary>
/// 2026-09-16 — busca fuentes reales sobre el tema del documento (Adler: «fuentes actuales,
/// confiables, en español, y no solo lo que yo le di»).
///
/// Se pregunta a dos catálogos públicos, sin cuenta ni clave: OpenAlex encuentra los trabajos —sabe
/// filtrar por idioma y por año, que es justo lo que hacía falta— y Crossref devuelve la ficha
/// exacta de cada uno por su DOI, con los apellidos y los nombres separados, que es como APA los
/// pide. Ninguno inventa nada: lo que no viene, no se escribe.
///
/// Lo que sale del equipo es el tema a buscar. Qué fuentes valen lo decide
/// <see cref="SourcePolicy"/>, que se prueba sin red.
/// </summary>
public sealed class ScholarSourceService : IDocumentSourceSearch, IDisposable
{
    private const string OpenAlex = "https://api.openalex.org/works";
    private const string Crossref = "https://api.crossref.org/works/";

    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;

    public ScholarSourceService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _ownsClient = httpClient is null;
        WebLookup.Identify(_httpClient);
    }

    public async Task<IReadOnlyList<DocumentSource>> FindAsync(string topic, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            return [];
        }

        try
        {
            // Primero por título y resumen, que acierta mucho más; si no salen bastantes, en todo el texto.
            var found = await SearchAsync(topic, precise: true, cancellationToken);
            if (found.Count < SourcePolicy.Minimum)
            {
                var extra = await SearchAsync(topic, precise: false, cancellationToken);
                found = [.. found, .. extra];
            }

            var chosen = SourcePolicy.Choose(found);
            var detailed = new List<DocumentSource>();
            foreach (var source in chosen)
            {
                detailed.Add(await DetailAsync(source, cancellationToken) ?? source);
            }

            return detailed;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            // Un borrador sin lista de fuentes sigue sirviendo; uno que no se guarda, no.
            return [];
        }
    }

    private async Task<IReadOnlyList<DocumentSource>> SearchAsync(string topic, bool precise, CancellationToken cancellationToken)
    {
        var from = (DateTime.Now.Year - SourcePolicy.Years).ToString("0000") + "-01-01";
        var filter = "language:es,from_publication_date:" + from +
                     (precise ? ",title_and_abstract.search:" + Uri.EscapeDataString(topic) : string.Empty);
        var address = OpenAlex + "?per-page=12&sort=relevance_score:desc&filter=" + filter +
                      (precise ? string.Empty : "&search=" + Uri.EscapeDataString(topic));

        using var timeout = WebLookup.Timeout(cancellationToken, TimeSpan.FromSeconds(15));
        using var response = await _httpClient.GetAsync(address, timeout.Token);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));

        if (!document.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var sources = new List<DocumentSource>();
        foreach (var work in results.EnumerateArray())
        {
            var title = WebLookup.Text(work, "title");
            if (title.Length == 0)
            {
                continue;
            }

            var venue = work.TryGetProperty("primary_location", out var location) && location.ValueKind == JsonValueKind.Object &&
                        location.TryGetProperty("source", out var venueElement) && venueElement.ValueKind == JsonValueKind.Object
                ? WebLookup.Text(venueElement, "display_name")
                : string.Empty;

            var authors = new List<SourceAuthor>();
            if (work.TryGetProperty("authorships", out var authorships) && authorships.ValueKind == JsonValueKind.Array)
            {
                foreach (var authorship in authorships.EnumerateArray())
                {
                    if (authorship.TryGetProperty("author", out var author) && WebLookup.Text(author, "display_name") is { Length: > 0 } name)
                    {
                        authors.Add(Split(name));
                    }
                }
            }

            var kind = WebLookup.Text(work, "type") switch
            {
                "book" or "book-chapter" or "monograph" => SourceKind.Book,
                "article" or "journal-article" or "review" or "preprint" => SourceKind.Article,
                _ => SourceKind.Web
            };

            var openAccess = work.TryGetProperty("open_access", out var access) && access.ValueKind == JsonValueKind.Object
                ? WebLookup.Text(access, "oa_url")
                : string.Empty;

            sources.Add(new DocumentSource(
                authors,
                work.TryGetProperty("publication_year", out var year) && year.TryGetInt32(out var value) ? value : null,
                title,
                venue.Length > 0 ? venue : null,
                Doi: WebLookup.Text(work, "doi"),
                Url: openAccess.Length > 0 ? openAccess : WebLookup.Text(work, "id"),
                Kind: kind,
                Language: WebLookup.Text(work, "language")));
        }

        return sources;
    }

    /// <summary>La ficha exacta del DOI: apellidos y nombres separados, volumen, número y páginas.</summary>
    private async Task<DocumentSource?> DetailAsync(DocumentSource source, CancellationToken cancellationToken)
    {
        var doi = source.Doi?.Replace("https://doi.org/", string.Empty, StringComparison.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(doi))
        {
            return null;
        }

        try
        {
            using var timeout = WebLookup.Timeout(cancellationToken, TimeSpan.FromSeconds(10));
            using var response = await _httpClient.GetAsync(Crossref + Uri.EscapeDataString(doi), timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
            if (!document.RootElement.TryGetProperty("message", out var work) || work.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var authors = new List<SourceAuthor>();
            if (work.TryGetProperty("author", out var list) && list.ValueKind == JsonValueKind.Array)
            {
                foreach (var author in list.EnumerateArray())
                {
                    var family = WebLookup.Text(author, "family");
                    if (family.Length > 0)
                    {
                        authors.Add(new SourceAuthor(family, WebLookup.Text(author, "given") is { Length: > 0 } given ? given : null));
                    }
                }
            }

            return source with
            {
                Authors = authors.Count > 0 ? authors : source.Authors,
                // Crossref manda el título tal como lo maqueta la revista, a veces A GRITOS.
                Title = WebLookup.FirstText(work, "title") is { Length: > 0 } title ? SourcePolicy.Sentence(title) : source.Title,
                Container = WebLookup.FirstText(work, "container-title") is { Length: > 0 } container ? container : source.Container,
                Volume = WebLookup.Text(work, "volume") is { Length: > 0 } volume ? volume : null,
                Issue = WebLookup.Text(work, "issue") is { Length: > 0 } issue ? issue : null,
                Pages = WebLookup.Text(work, "page") is { Length: > 0 } pages ? pages : null,
                Doi = doi
            };
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Un nombre suelto repartido en apellido y nombre, solo cuando se puede saber: con dos palabras,
    /// la segunda es el apellido. Con tres o más no hay forma de acertar —«Juan Pérez García» y
    /// «Zapata Pérez Brahiam Steven» se escriben igual y significan cosas distintas— así que se deja
    /// tal cual vino. Preferimos una referencia que hay que retocar a una que afirma un apellido que
    /// no es. Cuando Crossref responde manda la separación de verdad y esta cuenta no se usa.
    /// </summary>
    public static SourceAuthor Split(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2
            ? new SourceAuthor(parts[1], parts[0])
            : new SourceAuthor(name.Trim(), null);
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _httpClient.Dispose();
        }
    }
}
