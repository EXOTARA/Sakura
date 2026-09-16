using System.Net;
using Nexo.Windows.Documents;

namespace Nexo.Windows.Tests.Documents;

public sealed class WikimediaImageServiceTests
{
    private const string Results = """
        {"query":{"pages":[
          {"index":2,"title":"File:Panorámica.jpg","imageinfo":[{"mime":"image/jpeg","width":11066,"height":2313,
            "thumburl":"https://upload/pano.jpg","descriptionurl":"https://commons.wikimedia.org/wiki/File:Panor%C3%A1mica.jpg",
            "extmetadata":{"Artist":{"value":"<a href=\"#\">Alguien</a>"},"LicenseShortName":{"value":"CC BY 4.0"},"Restrictions":{"value":""}}}]},
          {"index":1,"title":"File:Parque_solar.jpg","imageinfo":[{"mime":"image/jpeg","width":1600,"height":1000,
            "thumburl":"https://upload/parque.jpg","descriptionurl":"https://commons.wikimedia.org/wiki/File:Parque_solar.jpg",
            "extmetadata":{"Artist":{"value":"<a href=\"#\">TitiNicola</a>"},"LicenseShortName":{"value":"CC BY-SA 4.0"},"Restrictions":{"value":""}}}]}
        ]}}
        """;

    private const string Empty = """{"batchcomplete":true}""";

    [Fact]
    public async Task FindAsync_DownloadsTheChosenImage_WithItsCredit()
    {
        var handler = new FakeHandler([(Results, "application/json"), ("imagen", "image/jpeg")]);
        using var service = new WikimediaImageService(new HttpClient(handler));

        var image = await service.FindAsync("parque solar");

        Assert.NotNull(image);
        Assert.Equal("Parque solar", image.Title);
        Assert.Equal("TitiNicola", image.Author);
        Assert.Equal("CC BY-SA 4.0", image.License);
        Assert.Equal("jpg", image.Extension);
        Assert.Equal("https://commons.wikimedia.org/wiki/File:Parque_solar.jpg", image.SourceUrl);
        Assert.Contains("gsrsearch=filetype%3Abitmap%20parque%20solar", handler.Requests[0]);
        Assert.Equal("https://upload/parque.jpg", handler.Requests[1]);
    }

    [Fact]
    public async Task FindAsync_WithoutResults_TriesAShorterSearch()
    {
        var handler = new FakeHandler([(Empty, "application/json"), (Results, "application/json"), ("imagen", "image/jpeg")]);
        using var service = new WikimediaImageService(new HttpClient(handler));

        Assert.NotNull(await service.FindAsync("paneles solares en un tejado"));
        Assert.Contains("paneles%20solares%20en%20un%20tejado", handler.Requests[0]);
        Assert.Contains("paneles%20solares%20tejado", handler.Requests[1]);
    }

    [Fact]
    public async Task FindAsync_WhenWikimediaFails_ReturnsNothing_InsteadOfThrowing()
    {
        using var service = new WikimediaImageService(new HttpClient(new FakeHandler([], HttpStatusCode.ServiceUnavailable)));

        Assert.Null(await service.FindAsync("parque solar"));
    }

    private sealed class FakeHandler(
        IReadOnlyList<(string Body, string ContentType)> responses,
        HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        private int _index;

        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.AbsoluteUri);
            var (body, contentType) = _index < responses.Count ? responses[_index++] : ("{}", "application/json");
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, contentType)
            });
        }
    }
}
