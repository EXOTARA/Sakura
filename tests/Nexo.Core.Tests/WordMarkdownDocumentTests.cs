using System.IO.Compression;
using System.Xml.Linq;
using Nexo.Core.Documents;

namespace Nexo.Core.Tests;

/// <summary>
/// 2026-09-15 — el Word que sale de una respuesta tiene el formato de la respuesta (Adler, con un PDF
/// en el que se veían los asteriscos, los guiones y las rayas del Markdown).
/// </summary>
public sealed class WordMarkdownDocumentTests
{
    private const string AdlersAnswer = """
        ## Pasos para evitar la falta de contenido constante y relevante
        *(lista lista para copiar)*

        ---

        1. **Define objetivos y audiencia**
          - Anota qué quieres lograr (p. ej., “aumentar el engagement + 2 % en 6 semanas”).
          - Describe a tu público.
        2. **Crea un calendario editorial**
          - Usa [Trello](https://trello.com) o HubSpot.

        ### Métricas

        | Métrica | Meta |
        |---|---|
        | Alcance | +10 % |
        """;

    private static string Part(byte[] document, string path)
    {
        using var archive = new ZipArchive(new MemoryStream(document), ZipArchiveMode.Read);
        var entry = archive.GetEntry(path);
        Assert.NotNull(entry);
        using var reader = new StreamReader(entry!.Open());
        return reader.ReadToEnd();
    }

    private static byte[] Build() =>
        WordDocumentBuilder.BuildFromMarkdown("Pasos para evitar la falta de contenido constante y relevante", AdlersAnswer);

    [Fact]
    public void NoMarkdownSymbolsReachTheDocument()
    {
        var text = string.Concat(XDocument.Parse(Part(Build(), "word/document.xml"))
            .Descendants(XName.Get("t", "http://schemas.openxmlformats.org/wordprocessingml/2006/main"))
            .Select(node => node.Value));

        Assert.DoesNotContain("**", text);
        Assert.DoesNotContain("---", text);
        Assert.DoesNotContain("|", text);
        Assert.DoesNotContain("](", text);
        Assert.Contains("Define objetivos y audiencia", text);
    }

    [Fact]
    public void BoldIsRealBold_AndListsAreRealLists()
    {
        var xml = Part(Build(), "word/document.xml");

        Assert.Contains("<w:b/></w:rPr><w:t xml:space=\"preserve\">Define objetivos y audiencia", xml);
        Assert.Contains("<w:numPr><w:ilvl w:val=\"0\"/><w:numId w:val=\"2\"/>", xml);
        Assert.Contains("<w:numPr><w:ilvl w:val=\"1\"/><w:numId w:val=\"1\"/>", xml);
        Assert.Contains("w:numFmt w:val=\"decimal\"", Part(Build(), "word/numbering.xml"));
    }

    [Fact]
    public void TheRepeatedOpeningHeadingIsNotDuplicated_AndSubheadingsKeepTheirRank()
    {
        var xml = Part(Build(), "word/document.xml");

        Assert.Single(System.Text.RegularExpressions.Regex.Matches(xml, "Pasos para evitar la falta de contenido constante y relevante"));
        Assert.Contains("w:val=\"Title\"", xml);
        Assert.Contains("<w:pStyle w:val=\"Heading2\"/></w:pPr><w:r><w:t xml:space=\"preserve\">Métricas", xml);
    }

    [Fact]
    public void LinksAreClickable_AndTablesAreTables()
    {
        var document = Build();
        var xml = Part(document, "word/document.xml");

        Assert.Contains("<w:hyperlink r:id=\"link1\"", xml);
        Assert.Contains("Target=\"https://trello.com/\" TargetMode=\"External\"", Part(document, "word/_rels/document.xml.rels"));
        Assert.Contains("<w:tbl>", xml);
        Assert.Contains("<w:tblHeader/>", xml);
    }

    [Fact]
    public void EveryPartIsWellFormed()
    {
        var document = Build();
        foreach (var part in new[]
                 {
                     "[Content_Types].xml", "_rels/.rels", "docProps/core.xml", "word/document.xml",
                     "word/_rels/document.xml.rels", "word/styles.xml", "word/numbering.xml"
                 })
        {
            Assert.Null(Record.Exception(() => XDocument.Parse(Part(document, part))));
        }
    }

    [Fact]
    public void ANewNumberedListStartsAgainAtOne()
    {
        var numbering = Part(WordDocumentBuilder.BuildFromMarkdown("x", "1. uno\n2. dos\n\nTexto\n\n1. otra\n2. más"), "word/numbering.xml");
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(numbering, "w:startOverride w:val=\"1\"").Count);
    }
}
