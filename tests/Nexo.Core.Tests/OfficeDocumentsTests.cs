using System.IO.Compression;
using System.Xml.Linq;
using Nexo.Core.Documents;

namespace Nexo.Core.Tests;

public sealed class OfficeDocumentsTests
{
    private const string Steps = """
        ## Plan de contenido

        Un plan sencillo para publicar con frecuencia.

        1. **Define objetivos y audiencia**
          - Anota qué quieres lograr.
          - Describe a tu público.
        2. **Crea un calendario editorial**
          - Usa [Trello](https://trello.com) o HubSpot.
        3. **Mide cada viernes**
          - Alcance e interacción.

        | Métrica | Meta | Actual |
        |---|---|---|
        | Alcance | +10 % | 1.250 |
        | Interacción | 2% | 3,5 |
        """;

    private static string Part(byte[] document, string path)
    {
        using var archive = new ZipArchive(new MemoryStream(document), ZipArchiveMode.Read);
        var entry = archive.GetEntry(path);
        Assert.True(entry is not null, $"Falta {path}");
        using var reader = new StreamReader(entry!.Open());
        return reader.ReadToEnd();
    }

    private static void AllPartsWellFormed(byte[] document)
    {
        using var archive = new ZipArchive(new MemoryStream(document), ZipArchiveMode.Read);
        foreach (var entry in archive.Entries.Where(entry => entry.FullName.EndsWith(".xml") || entry.FullName.EndsWith(".rels")))
        {
            using var reader = new StreamReader(entry.Open());
            Assert.Null(Record.Exception(() => XDocument.Parse(reader.ReadToEnd())));
        }
    }

    [Fact]
    public void Plan_CoverThenOneSlidePerStep_ThenTheTable()
    {
        var slides = PresentationPlan.From("Plan de contenido", Steps);

        Assert.True(slides[0].IsCover);
        Assert.Equal("Plan de contenido", slides[0].Title);
        Assert.Equal("Un plan sencillo para publicar con frecuencia.", slides[0].Subtitle);
        Assert.Equal("1. Define objetivos y audiencia", slides[1].Title);
        Assert.Equal(2, slides[1].Lines.Count);
        Assert.All(slides[1].Lines, line => Assert.Equal(0, line.Depth));
        Assert.Equal("3. Mide cada viernes", slides[3].Title);
        Assert.NotNull(slides[4].Table);
    }

    [Fact]
    public void Plan_WithHeadings_EachHeadingOpensASlide()
    {
        var slides = PresentationPlan.From("Informe", "# Informe\n\n## Contexto\nTexto.\n\n## Propuesta\n- Uno\n- Dos");

        Assert.Equal(["Informe", "Contexto", "Propuesta"], slides.Select(slide => slide.Title));
    }

    [Fact]
    public void Plan_TooMuchText_SplitsIntoContinuation()
    {
        var bullets = string.Join('\n', Enumerable.Range(1, 12).Select(i => $"- Punto número {i}"));
        var slides = PresentationPlan.From("Lista", "## Muchos puntos\n" + bullets);

        Assert.Equal(["Lista", "Muchos puntos", "Muchos puntos (cont.)"], slides.Select(slide => slide.Title));
        Assert.All(slides.Skip(1), slide => Assert.True(slide.Lines.Count <= PresentationPlan.MaximumLines));
    }

    [Fact]
    public void Presentation_IsACompleteWellFormedPackage()
    {
        var document = PresentationDocumentBuilder.BuildFromMarkdown("Plan de contenido", Steps);

        AllPartsWellFormed(document);
        Assert.Contains("<p:sldId id=\"256\"", Part(document, "ppt/presentation.xml"));
        Assert.Contains("Define objetivos y audiencia", Part(document, "ppt/slides/slide2.xml"));
        Assert.Contains("<a:tbl>", Part(document, "ppt/slides/slide5.xml"));
        Assert.Contains("TargetMode=\"External\"", Part(document, "ppt/slides/_rels/slide3.xml.rels"));
    }

    [Fact]
    public void Spreadsheet_EachTableIsASheet_WithNumbersAsNumbers()
    {
        var document = SpreadsheetDocumentBuilder.BuildFromMarkdown("Plan de contenido", Steps);
        AllPartsWellFormed(document);

        var sheet = Part(document, "xl/worksheets/sheet1.xml");
        Assert.Contains("name=\"Plan de contenido\"", Part(document, "xl/workbook.xml"));
        Assert.Contains("<pane ySplit=\"1\"", sheet);
        Assert.Contains("<autoFilter ref=\"A1:C3\"/>", sheet);
        Assert.Contains("<c r=\"B2\" s=\"3\"><v>0.1</v></c>", sheet);
        Assert.Contains("<c r=\"C2\" s=\"2\"><v>1250</v></c>", sheet);
        Assert.Contains("<c r=\"C3\" s=\"2\"><v>3.5</v></c>", sheet);
    }

    [Fact]
    public void Spreadsheet_WithoutTables_KeepsTheContentAsRows()
    {
        var document = SpreadsheetDocumentBuilder.BuildFromMarkdown("Lista", "## Compras\n- Pan\n- Leche");
        var sheet = Part(document, "xl/worksheets/sheet1.xml");

        Assert.Contains("Contenido", Part(document, "xl/workbook.xml"));
        Assert.Contains("• Pan", sheet);
        Assert.Contains("Compras", sheet);
    }

    [Theory]
    [InlineData("12", 12d, false)]
    [InlineData("-3.5", -3.5d, false)]
    [InlineData("1.234,5", 1234.5d, false)]
    [InlineData("1,234.5", 1234.5d, false)]
    [InlineData("0.125", 0.125d, false)]
    [InlineData("+10 %", 0.1d, true)]
    public void Spreadsheet_RecognisesNumbers(string text, double expected, bool percent)
    {
        var parsed = SpreadsheetDocumentBuilder.ParseNumber(text);
        Assert.NotNull(parsed);
        Assert.Equal(expected, (double)parsed.Value.Value, 6);
        Assert.Equal(percent, parsed.Value.IsPercent);
    }

    [Theory]
    [InlineData("2-3")]
    [InlineData("12 h")]
    [InlineData("+10 % aprox")]
    [InlineData("KB5039211")]
    public void Spreadsheet_LeavesAmbiguousCellsAsText(string text) =>
        Assert.Null(SpreadsheetDocumentBuilder.ParseNumber(text));
}
