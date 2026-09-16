using System.Text.Json;
using Nexo.Core.Documents;

namespace Nexo.Windows.Documents;

/// <summary>
/// 2026-09-15 — busca en Wikimedia Commons la imagen de una diapositiva (Adler: presentaciones «con
/// imágenes»).
///
/// Commons porque no pide cuenta ni clave —Pexels y Unsplash sí— y todo lo que hay se puede reutilizar
/// citando al autor. Qué imagen vale lo decide <see cref="WikimediaImagePolicy"/>, que se prueba sin
/// red; aquí solo se pregunta, se descarga y se corta a tiempo.
///
/// Lo que sale del equipo es el término de búsqueda, no la respuesta ni el documento. Se manda la
/// identificación que pide la API de Wikimedia para saber quién consulta.
/// </summary>
public sealed class WikimediaImageService : IDocumentImageSource, IDisposable
{
    private const string Endpoint = "https://commons.wikimedia.org/w/api.php";
    private const int MaximumBytes = 8 * 1024 * 1024;

    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;

    public WikimediaImageService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _ownsClient = httpClient is null;
        WebLookup.Identify(_httpClient);
    }

    public async Task<DocumentImage?> FindAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        try
        {
            DocumentImageCandidate? chosen = null;
            foreach (var attempt in WikimediaImagePolicy.SearchTerms(query))
            {
                chosen = WikimediaImagePolicy.Choose(await SearchAsync(attempt, cancellationToken));
                if (chosen is not null)
                {
                    break;
                }
            }

            if (chosen is null || WikimediaImagePolicy.Extension(chosen.Mime) is not { } extension)
            {
                return null;
            }

            using var timeout = WebLookup.Timeout(cancellationToken, TimeSpan.FromSeconds(25));
            using var response = await _httpClient.GetAsync(chosen.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > MaximumBytes)
            {
                return null;
            }

            using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, timeout.Token);
            if (buffer.Length is 0 or > MaximumBytes)
            {
                return null;
            }

            return new DocumentImage(
                buffer.ToArray(),
                extension,
                chosen.Width,
                chosen.Height,
                WikimediaImagePolicy.FileTitle(chosen.Title),
                WikimediaImagePolicy.CleanText(chosen.Artist),
                WikimediaImagePolicy.CleanText(chosen.License),
                chosen.PageUrl);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
        {
            // Una presentación sin foto sigue sirviendo; una que no se guarda, no.
            return null;
        }
    }

    private async Task<IReadOnlyList<DocumentImageCandidate>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        // «filetype:bitmap» deja fuera los SVG y los PDF, que en una diapositiva no son una foto.
        var address = Endpoint +
                      "?action=query&format=json&formatversion=2&generator=search&gsrnamespace=6&gsrlimit=10" +
                      "&prop=imageinfo&iiprop=url%7Cextmetadata%7Csize%7Cmime&iiurlwidth=1600" +
                      "&iiextmetadatafilter=Artist%7CLicenseShortName%7CRestrictions" +
                      "&gsrsearch=" + Uri.EscapeDataString("filetype:bitmap " + query.Trim());

        using var timeout = WebLookup.Timeout(cancellationToken, TimeSpan.FromSeconds(15));
        using var response = await _httpClient.GetAsync(address, timeout.Token);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));

        if (!document.RootElement.TryGetProperty("query", out var result) ||
            !result.TryGetProperty("pages", out var pages) ||
            pages.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var candidates = new List<(int Order, DocumentImageCandidate Candidate)>();
        foreach (var page in pages.EnumerateArray())
        {
            if (!page.TryGetProperty("imageinfo", out var infos) || infos.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var info in infos.EnumerateArray())
            {
                var mime = WebLookup.Text(info, "mime");
                var download = WebLookup.Text(info, "thumburl") is { Length: > 0 } thumb ? thumb : WebLookup.Text(info, "url");
                if (mime.Length == 0 || download.Length == 0)
                {
                    continue;
                }

                var metadata = info.TryGetProperty("extmetadata", out var extra) ? extra : default;
                candidates.Add((WebLookup.Number(page, "index"), new DocumentImageCandidate(
                    WebLookup.Text(page, "title"),
                    mime,
                    WebLookup.Number(info, "width"),
                    WebLookup.Number(info, "height"),
                    download,
                    Metadata(metadata, "Artist"),
                    Metadata(metadata, "LicenseShortName"),
                    Metadata(metadata, "Restrictions"),
                    WebLookup.Text(info, "descriptionurl"))));
                break;
            }
        }

        // La API no conserva el orden de la búsqueda; «index» sí dice cuál salió primero.
        return candidates.OrderBy(item => item.Order).Select(item => item.Candidate).ToList();
    }


    private static string Metadata(JsonElement metadata, string name) =>
        metadata.ValueKind == JsonValueKind.Object && metadata.TryGetProperty(name, out var entry)
            ? WebLookup.Text(entry, "value")
            : string.Empty;


    public void Dispose()
    {
        if (_ownsClient)
        {
            _httpClient.Dispose();
        }
    }
}
