using System.Net;
using System.Text;
using Nexo.Core.Documents;
using Nexo.Windows.Documents;

namespace Nexo.Windows.Tests.Documents;

public sealed class ScholarSourceServiceTests
{
    private static string Works(int count) =>
        "{\"results\":[" + string.Join(",", Enumerable.Range(1, count).Select(i =>
            "{\"title\":\"MÉTODOS NUMÉRICOS Y ERROR " + i + "\",\"publication_year\":2024,\"type\":\"article\",\"language\":\"es\"," +
            "\"doi\":\"https://doi.org/10.1/" + i + "\",\"id\":\"https://openalex.org/W" + i + "\"," +
            "\"primary_location\":{\"source\":{\"display_name\":\"Revista de Ejemplo\"}}," +
            "\"open_access\":{\"oa_url\":\"https://ejemplo.test/" + i + ".pdf\"}," +
            "\"authorships\":[{\"author\":{\"display_name\":\"Ana Pérez\"}}]}")) + "]}";

    private const string CrossrefWork = """
        {"message":{"title":["Métodos numéricos y error 1"],"container-title":["Revista de Ejemplo"],
         "volume":"7","issue":"3","page":"303-312",
         "author":[{"given":"Ana María","family":"Pérez Soto"}]}}
        """;

    [Fact]
    public async Task ItAsksForSpanishAndRecentWork_AndWritesTheReference()
    {
        var handler = new FakeHandler([Works(6), CrossrefWork]);
        using var service = new ScholarSourceService(new HttpClient(handler));

        var sources = await service.FindAsync("métodos numéricos error");

        Assert.Equal(6, sources.Count);
        Assert.Contains("language:es", handler.Requests[0]);
        Assert.Contains("from_publication_date:", handler.Requests[0]);
        // La ficha exacta llega de Crossref: apellidos, nombre, volumen y páginas.
        Assert.Equal("Pérez Soto, A. M. (2024). Métodos numéricos y error 1. Revista de Ejemplo, 7(3), 303–312. https://doi.org/10.1/1",
            ApaReference.Format(sources[0]));
        // El título a gritos se escribe como una frase.
        Assert.StartsWith("Métodos numéricos y error", sources[1].Title);
    }

    [Fact]
    public async Task WithFewResults_ItTriesAWiderSearch()
    {
        var handler = new FakeHandler([Works(2), Works(6)]);
        using var service = new ScholarSourceService(new HttpClient(handler));

        await service.FindAsync("métodos numéricos error");

        Assert.Contains("title_and_abstract.search", handler.Requests[0]);
        Assert.DoesNotContain("title_and_abstract.search", handler.Requests[1]);
    }

    [Fact]
    public async Task WhenTheCatalogueFails_TheDocumentIsStillSaved()
    {
        using var service = new ScholarSourceService(new HttpClient(new FakeHandler([], HttpStatusCode.ServiceUnavailable)));

        Assert.Empty(await service.FindAsync("métodos numéricos"));
    }

    [Theory]
    [InlineData("Ana Pérez", "Pérez, A.")]
    // Con tres o más palabras no hay forma de saber cuáles son los apellidos: se deja tal cual.
    [InlineData("Zapata Pérez Brahiam Steven", "Zapata Pérez Brahiam Steven")]
    [InlineData("Juan Carlos Pérez García", "Juan Carlos Pérez García")]
    public void ANameIsOnlySplitWhenItCanBeKnown(string name, string expected) =>
        Assert.Equal(expected, ApaReference.Authors([ScholarSourceService.Split(name)]));

    private sealed class FakeHandler(IReadOnlyList<string> responses, HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        private int _index;

        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.AbsoluteUri);
            var body = _index < responses.Count ? responses[_index++] : "{}";
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
