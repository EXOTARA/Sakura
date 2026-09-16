using Nexo.Core.Documents;

namespace Nexo.Core.Tests;

public sealed class DocumentSourceTests
{
    private static DocumentSource Article(int year = 2023, string? doi = "10.37811/cl_rcm.v7i3.6186", string? language = "es") =>
        new(
            [new SourceAuthor("Guerra Andrade", "Luis Gonzalo"), new SourceAuthor("Terán Aguirre", "Gabriel Ricardo")],
            year,
            "Métodos numéricos aplicados a la experimentación física",
            "Ciencia Latina Revista Científica Multidisciplinar",
            Volume: "7",
            Issue: "3",
            Pages: "303-312",
            Doi: doi,
            Language: language);

    [Fact]
    public void AnArticle_IsWrittenAsApaSeven() =>
        Assert.Equal(
            "Guerra Andrade, L. G. y Terán Aguirre, G. R. (2023). Métodos numéricos aplicados a la experimentación física. " +
            "Ciencia Latina Revista Científica Multidisciplinar, 7(3), 303–312. https://doi.org/10.37811/cl_rcm.v7i3.6186",
            ApaReference.Format(Article()));

    [Fact]
    public void WhatIsMissing_IsLeftOut_NotInvented()
    {
        var book = new DocumentSource(
            [new SourceAuthor("Chapra", "Steven")],
            2020,
            "Métodos numéricos para ingenieros",
            "McGraw-Hill",
            Url: "https://ejemplo.test/libro",
            Kind: SourceKind.Book);

        // Sin volumen ni páginas: la referencia va sin ellos, no con huecos ni con datos puestos a dedo.
        Assert.Equal("Chapra, S. (2020). Métodos numéricos para ingenieros. McGraw-Hill. https://ejemplo.test/libro", ApaReference.Format(book));
    }

    [Fact]
    public void WithoutAYear_ItSaysSoTheWayApaDoes() =>
        Assert.Contains("(s. f.)", ApaReference.Format(Article(year: 0) with { Year = null }));

    [Theory]
    // Vieja: un trabajo de hace quince años no es una fuente «actual».
    [InlineData(2005, "es", "10.1/x", false)]
    // En otro idioma: se pidió en español.
    [InlineData(2024, "en", "10.1/x", false)]
    // Sin enlace ni DOI: no se puede comprobar, así que no se ofrece.
    [InlineData(2024, "es", null, false)]
    [InlineData(2024, "es", "10.1/x", true)]
    public void OnlyWhatCanBeCheckedIsOffered(int year, string language, string? doi, bool expected)
    {
        var source = Article(year, doi, language) with { Url = doi is null ? null : "https://ejemplo.test" };
        var chosen = SourcePolicy.Choose([source], currentYear: 2026);

        Assert.Equal(expected, chosen.Count == 1);
    }

    [Fact]
    public void TheSameWorkTwice_AppearsOnce()
    {
        var chosen = SourcePolicy.Choose([Article(), Article(), Article(doi: null) with { Url = "https://otra.test" }], currentYear: 2026);

        Assert.Equal(2, chosen.Count);
    }

    [Fact]
    public void ATitleInCapitals_IsWrittenAsASentence() =>
        Assert.Equal("Métodos de elementos finitos mixtos", SourcePolicy.Sentence("MÉTODOS DE ELEMENTOS FINITOS MIXTOS"));

    [Fact]
    public void TheWord_CarriesTheSourcesAtTheEnd()
    {
        var document = WordDocumentBuilder.BuildFromMarkdown(
            "Estudio de errores",
            "## Qué es el error\n\nLa diferencia entre el valor exacto y el aproximado.",
            images: null,
            sources: [Article()]);

        using var archive = new System.IO.Compression.ZipArchive(new MemoryStream(document));
        using var reader = new StreamReader(archive.GetEntry("word/document.xml")!.Open());
        var body = reader.ReadToEnd();

        Assert.Contains("Fuentes para consultar", body);
        Assert.Contains("Compruébalas antes de citarlas", body);
        Assert.Contains("Guerra Andrade, L. G.", body);
        // Sangría francesa, que es como se escribe una lista de referencias.
        Assert.Contains("<w:ind w:left=\"567\" w:hanging=\"567\"/>", body);
    }

    [Fact]
    public void WithoutSources_TheDocumentHasNoSuchSection()
    {
        var document = WordDocumentBuilder.BuildFromMarkdown("Estudio de errores", "## Qué es el error\n\nTexto.");

        using var archive = new System.IO.Compression.ZipArchive(new MemoryStream(document));
        using var reader = new StreamReader(archive.GetEntry("word/document.xml")!.Open());
        Assert.DoesNotContain("Fuentes para consultar", reader.ReadToEnd());
    }
}
