using Nexo.Core.Documents;

namespace Nexo.Core.Tests;

public sealed class DocumentImageTests
{
    private static DocumentImageCandidate Candidate(
        string title = "File:Ejemplo.jpg",
        string mime = "image/jpeg",
        int width = 1600,
        int height = 1000,
        string restrictions = "") =>
        new(title, mime, width, height, "https://upload.wikimedia.org/ejemplo.jpg", "Autora", "CC BY 4.0", restrictions, "https://commons.wikimedia.org/wiki/File:Ejemplo.jpg");

    [Fact]
    public void Choose_PrefersALandscapePhoto_OverWhatComesFirst()
    {
        var chosen = WikimediaImagePolicy.Choose([
            Candidate("File:Vertical.jpg", width: 900, height: 1200),
            Candidate("File:Apaisada.jpg", width: 1600, height: 1000)
        ]);

        Assert.Equal("File:Apaisada.jpg", chosen!.Title);
    }

    [Fact]
    public void Choose_WithoutALandscapePhoto_TakesTheFirstUsableOne()
    {
        var chosen = WikimediaImagePolicy.Choose([Candidate("File:Vertical.jpg", width: 900, height: 1200)]);

        Assert.Equal("File:Vertical.jpg", chosen!.Title);
    }

    [Theory]
    // PowerPoint no dibuja WebP ni SVG.
    [InlineData("image/webp", 1600, 1000, "")]
    [InlineData("image/svg+xml", 1600, 1000, "")]
    // Una panorámica se quedaría en una franja.
    [InlineData("image/jpeg", 11066, 2313, "")]
    // Demasiado pequeña para llenar media diapositiva.
    [InlineData("image/jpeg", 400, 300, "")]
    // Con restricciones no se puede usar sin más.
    [InlineData("image/jpeg", 1600, 1000, "trademarked")]
    public void Choose_SkipsWhatDoesNotServe(string mime, int width, int height, string restrictions) =>
        Assert.Null(WikimediaImagePolicy.Choose([Candidate(mime: mime, width: width, height: height, restrictions: restrictions)]));

    [Fact]
    public void SearchTerms_DropTheFillerAndThenTheTail()
    {
        Assert.Equal(
            ["líneas de alta tensión al atardecer", "líneas alta tensión atardecer", "líneas alta tensión", "líneas alta"],
            WikimediaImagePolicy.SearchTerms("líneas de alta tensión al atardecer"));

        // Lo que ya es corto se busca una sola vez.
        Assert.Equal(["parque eólico"], WikimediaImagePolicy.SearchTerms("parque eólico"));
    }

    [Fact]
    public void Credit_ReadsTheAuthorWithoutItsHtml()
    {
        var credit = WikimediaImagePolicy.Credit(
            "<a href=\"//commons.wikimedia.org/wiki/User:Argenberg\" title=\"User:Argenberg\">Vyacheslav &amp; Argenberg</a>",
            "CC BY 4.0");

        Assert.Equal("Vyacheslav & Argenberg · CC BY 4.0", credit);
    }

    [Fact]
    public void Credit_WithoutAuthor_StillSaysWhereSalio() =>
        Assert.Equal("Wikimedia Commons", WikimediaImagePolicy.Credit(null, null));

    [Fact]
    public void FileTitle_ReadsLikeAName() =>
        Assert.Equal("Wind turbines, Crimea", WikimediaImagePolicy.FileTitle("File:Wind_turbines,_Crimea.jpg"));

    [Fact]
    public void CreditLine_NamesTheAuthorTheLicenseAndThePage()
    {
        var line = WikimediaImagePolicy.CreditLine(new DocumentImage(
            [1], "jpg", 1600, 1000, "Parque solar", "TitiNicola", "CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Parque.jpg"));

        Assert.Equal("Parque solar, de TitiNicola (CC BY-SA 4.0). Wikimedia Commons: https://commons.wikimedia.org/wiki/File:Parque.jpg", line);
    }
}
