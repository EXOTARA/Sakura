using System.IO.Compression;
using System.Xml.Linq;
using Nexo.Core.Assistant;
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
    public void Plan_CoverThenTheStepsInARow_ThenOneSlidePerStep_ThenTheTable()
    {
        var slides = PresentationPlan.From("Plan de contenido", Steps);

        Assert.True(slides[0].IsCover);
        Assert.Equal("Plan de contenido", slides[0].Title);
        Assert.Equal("Un plan sencillo para publicar con frecuencia.", slides[0].Subtitle);
        Assert.Equal(SlideLayout.Process, slides[1].Layout);
        Assert.Equal(["Define objetivos y audiencia", "Crea un calendario editorial", "Mide cada viernes"],
            slides[1].Lines.Select(line => AnswerMarkdown.ToPlainText(line.Spans)));
        Assert.Equal("1. Define objetivos y audiencia", slides[2].Title);
        Assert.Equal(2, slides[2].Lines.Count);
        Assert.All(slides[2].Lines, line => Assert.Equal(0, line.Depth));
        Assert.Equal("3. Mide cada viernes", slides[4].Title);
        // Metas en porcentaje y resultados en cantidades: no es una gráfica, es una tabla.
        Assert.Equal(SlideLayout.Table, slides[5].Layout);
        Assert.Equal("Plan de contenido", slides[5].Title);
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
        var bullets = string.Join('\n', Enumerable.Range(1, 12).Select(i => $"- Punto número {i}, que explica con bastante detalle algo que no cabe en media columna"));
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
        Assert.Contains("<a:tbl>", Part(document, "ppt/slides/slide6.xml"));
        Assert.Contains("TargetMode=\"External\"", Part(document, "ppt/slides/_rels/slide4.xml.rels"));
    }

    private const string Deck = """
        # Energías renovables

        Claro, aquí tienes la presentación:

        ## ¿Por qué importan?
        - Menos emisiones
        - Precios más bajos

        Notas: Empezar con una pregunta al público.

        ## Evolución de la capacidad solar
        La capacidad se ha multiplicado.

        | Año | GW |
        |---|---|
        | 2019 | 4,4 |
        | 2020 | 6 |
        | 2021 | 7 |

        ## Idea clave
        > La energía más barata es la que no se consume.

        ## Retos
        - Red saturada
        - Permisos lentos
        - Almacenamiento caro
        - Poca inversión
        - Falta de técnicos
        - Tarifas subsidiadas

        ## Conclusión
        - Invertir en transmisión
        - Agilizar permisos
        """;

    [Fact]
    public void Plan_ChoosesALayoutForEachKindOfSection()
    {
        var slides = PresentationPlan.From("Energías renovables", Deck);

        Assert.Equal(
            [SlideLayout.Cover, SlideLayout.Agenda, SlideLayout.Content, SlideLayout.Chart, SlideLayout.Quote, SlideLayout.TwoColumns, SlideLayout.Closing],
            slides.Select(slide => slide.Layout));
        // La frase del chat no es un subtítulo.
        Assert.Null(slides[0].Subtitle);
        Assert.Equal(["¿Por qué importan?", "Evolución de la capacidad solar", "Idea clave", "Retos", "Conclusión"],
            slides[1].Lines.Select(line => AnswerMarkdown.ToPlainText(line.Spans)));
        // Las notas van a las notas del orador, no a la diapositiva.
        Assert.Equal("Empezar con una pregunta al público.", slides[2].Notes);
        Assert.Equal(2, slides[2].Lines.Count);
        // La gráfica lleva al lado la frase que la explica.
        Assert.Equal(ChartKind.Line, slides[3].Chart!.Kind);
        Assert.Equal("La capacidad se ha multiplicado.", AnswerMarkdown.ToPlainText(Assert.Single(slides[3].Lines).Spans));
    }

    [Fact]
    public void Plan_WithParts_EachPartOpensWithANumberedDivider()
    {
        var slides = PresentationPlan.From("Transición", """
            # Parte 1: Contexto
            ## Historia
            - Primer parque en 1994
            # Parte 2: Futuro
            ## Metas
            - Más solar
            ## Riesgos
            - Red saturada
            """);

        Assert.Equal(
            [SlideLayout.Cover, SlideLayout.Agenda, SlideLayout.Section, SlideLayout.Content, SlideLayout.Section, SlideLayout.Content, SlideLayout.Content],
            slides.Select(slide => slide.Layout));
        Assert.Equal(("Contexto", 1), (slides[2].Title, slides[2].SectionNumber!.Value));
        Assert.Equal(("Futuro", 2), (slides[4].Title, slides[4].SectionNumber!.Value));
        Assert.Equal(["Contexto", "Historia", "Futuro", "Metas · Riesgos"], slides[1].Lines.Select(line => AnswerMarkdown.ToPlainText(line.Spans)));
    }

    [Fact]
    public void Plan_LongTable_ContinuesWithoutLosingRows()
    {
        var rows = string.Join('\n', Enumerable.Range(1, 11).Select(i => $"| Tarea {i} | Pendiente |"));
        var slides = PresentationPlan.From("Tareas", "## Lista\n| Tarea | Estado |\n|---|---|\n" + rows);

        Assert.Equal(["Tareas", "Lista", "Lista (cont.)"], slides.Select(slide => slide.Title));
        Assert.Equal(11, slides.Skip(1).Sum(slide => slide.Table!.Rows.Count));
    }

    [Fact]
    public void Presentation_WithChartAndNotes_IsACompleteWellFormedPackage()
    {
        var document = PresentationDocumentBuilder.BuildFromMarkdown("Energías renovables", Deck);
        AllPartsWellFormed(document);

        var chart = Part(document, "ppt/charts/chart1.xml");
        Assert.Contains("<c:lineChart>", chart);
        Assert.Contains("<c:f>'Hoja1'!$B$2:$B$4</c:f>", chart);
        Assert.Contains("<c:formatCode>#,##0.0</c:formatCode>", chart);
        Assert.Contains("r:id=\"chart1\"", Part(document, "ppt/slides/slide4.xml"));
        Assert.Contains("../embeddings/Microsoft_Excel_Worksheet1.xlsx", Part(document, "ppt/charts/_rels/chart1.xml.rels"));
        Assert.Contains("notesSlide3.xml", Part(document, "ppt/slides/_rels/slide3.xml.rels"));
        Assert.Contains("Empezar con una pregunta al público.", Part(document, "ppt/notesSlides/notesSlide3.xml"));
        Assert.Contains("<p:notesMasterIdLst>", Part(document, "ppt/presentation.xml"));

        using var archive = new ZipArchive(new MemoryStream(document), ZipArchiveMode.Read);
        using var embedded = new MemoryStream();
        using (var stream = archive.GetEntry("ppt/embeddings/Microsoft_Excel_Worksheet1.xlsx")!.Open())
        {
            stream.CopyTo(embedded);
        }

        var workbook = embedded.ToArray();
        AllPartsWellFormed(workbook);
        Assert.Contains("name=\"Hoja1\"", Part(workbook, "xl/workbook.xml"));
    }

    private const string Illustrated = """
        # Energías renovables

        Situación actual y retos.

        Imagen: parque solar fotovoltaico

        ## ¿Por qué importan?
        - Menos emisiones

        Imagen: paneles solares en un tejado

        ## Datos

        | Año | GW |
        |---|---|
        | 2020 | 6 |
        | 2021 | 7 |

        Imagen: turbinas eólicas
        """;

    [Fact]
    public void Plan_EachImageGoesToASlideThatCanShowIt()
    {
        var slides = PresentationPlan.From("Energías renovables", Illustrated);

        // La imagen suelta de antes del primer título no tiene diapositiva propia: es la de la portada.
        Assert.Equal("parque solar fotovoltaico", slides[0].ImageQuery);
        Assert.Equal(SlideLayout.Content, slides[1].Layout);
        Assert.Equal("paneles solares en un tejado", slides[1].ImageQuery);
        // En una gráfica no cabe una foto: se busca hacia atrás una diapositiva con sitio.
        Assert.Equal(SlideLayout.Chart, slides[2].Layout);
        Assert.Null(slides[2].ImageQuery);
        Assert.DoesNotContain("Imagen:", slides.SelectMany(slide => slide.Lines).Select(line => AnswerMarkdown.ToPlainText(line.Spans)));
        // «turbinas eólicas» se queda fuera: no queda ninguna diapositiva con sitio, y no se busca en
        // internet una imagen que después no se va a ver.
        Assert.Equal(["parque solar fotovoltaico", "paneles solares en un tejado"],
            PresentationPlan.ImageQueries("Energías renovables", Illustrated));
    }

    [Fact]
    public void Presentation_WithImages_EmbedsThemAndCreditsThem()
    {
        var images = new Dictionary<string, DocumentImage>(StringComparer.OrdinalIgnoreCase)
        {
            ["parque solar fotovoltaico"] = new([1, 2, 3], "jpg", 1600, 1000, "Parque solar", "TitiNicola", "CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Parque.jpg")
        };

        var document = PresentationDocumentBuilder.BuildFromMarkdown("Energías renovables", Illustrated, images);
        AllPartsWellFormed(document);

        var cover = Part(document, "ppt/slides/slide1.xml");
        Assert.Contains("<a:blip r:embed=\"image1\"/>", cover);
        Assert.Contains("TitiNicola · CC BY-SA 4.0", cover);
        Assert.Contains("../media/image1.jpg", Part(document, "ppt/slides/_rels/slide1.xml.rels"));
        Assert.Contains("<Default Extension=\"jpg\" ContentType=\"image/jpeg\"/>", Part(document, "[Content_Types].xml"));

        // La última diapositiva dice de dónde salió cada imagen, como piden las licencias.
        var credits = Part(document, $"ppt/slides/slide{PresentationPlan.From("Energías renovables", Illustrated).Count + 1}.xml");
        Assert.Contains("Créditos de imágenes", credits);
        Assert.Contains("de TitiNicola (CC BY-SA 4.0)", credits);

        using var archive = new ZipArchive(new MemoryStream(document), ZipArchiveMode.Read);
        Assert.NotNull(archive.GetEntry("ppt/media/image1.jpg"));
    }

    [Fact]
    public void Presentation_WithoutTheImages_IsTheSameDeckWithoutPhotos()
    {
        var document = PresentationDocumentBuilder.BuildFromMarkdown("Energías renovables", Illustrated);

        AllPartsWellFormed(document);
        Assert.DoesNotContain("<a:blip", Part(document, "ppt/slides/slide1.xml"));
        using var archive = new ZipArchive(new MemoryStream(document), ZipArchiveMode.Read);
        Assert.DoesNotContain(archive.Entries, entry => entry.FullName.StartsWith("ppt/media/"));
    }

    [Fact]
    public void Word_WithAnImageAndFigures_PlacesThemWithTheirCredit()
    {
        var images = new Dictionary<string, DocumentImage>(StringComparer.OrdinalIgnoreCase)
        {
            ["parque solar fotovoltaico"] = new([1, 2, 3], "jpg", 1600, 1000, "Parque solar", "TitiNicola", "CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Parque.jpg")
        };

        var document = WordDocumentBuilder.BuildFromMarkdown("Energías renovables", Illustrated, images);
        AllPartsWellFormed(document);

        var body = Part(document, "word/document.xml");
        Assert.Contains("<a:blip r:embed=\"image1\"/>", body);
        Assert.Contains("Figura 1. Parque solar — TitiNicola · CC BY-SA 4.0", body);
        // La línea «Imagen: …» no se imprime como texto.
        Assert.DoesNotContain("Imagen: parque solar", body);
        Assert.Contains("media/image1.jpg", Part(document, "word/_rels/document.xml.rels"));

        using var archive = new ZipArchive(new MemoryStream(document), ZipArchiveMode.Read);
        Assert.NotNull(archive.GetEntry("word/media/image1.jpg"));
    }

    [Fact]
    public void Word_AGeoGebraPlaceholder_LeavesAFramedGapWithItsNumberedCaption()
    {
        var document = WordDocumentBuilder.BuildFromMarkdown(
            "Bisección",
            "## Desarrollo por inciso\n\nPlantea la función.\n\n[Gráfica de GeoGebra: f(x) = x^3 - x - 2 en [1, 2]]\n");
        AllPartsWellFormed(document);

        var body = Part(document, "word/document.xml");
        Assert.Contains("Inserta aquí tu gráfica de GeoGebra", body);
        Assert.Contains("w:val=\"dashed\"", body);
        Assert.Contains("Figura 1. f(x) = x^3 - x - 2 en [1, 2]", body);
        Assert.DoesNotContain("[Gráfica de GeoGebra", body);
    }

    [Theory]
    [InlineData("[Gráfica de GeoGebra: parábola y = x^2]", "parábola y = x^2")]
    [InlineData("[grafica: raíces]", "raíces")]
    [InlineData("Una gráfica normal", null)]
    public void GraphPlaceholder_IsRecognized(string line, string? expected) =>
        Assert.Equal(expected, GraphPlaceholder.Description(line));

    [Fact]
    public void Word_ANumericTable_KeepsTheDataAndAddsItsChart()
    {
        var document = WordDocumentBuilder.BuildFromMarkdown("Energías renovables", Illustrated);
        AllPartsWellFormed(document);

        var body = Part(document, "word/document.xml");
        // La tabla se queda: la gráfica la acompaña, no la sustituye.
        Assert.Contains("<w:tbl>", body);
        Assert.Contains("r:id=\"chart1\"", body);
        Assert.Contains("<c:barChart>", Part(document, "word/charts/chart1.xml"));

        using var archive = new ZipArchive(new MemoryStream(document), ZipArchiveMode.Read);
        Assert.NotNull(archive.GetEntry("word/embeddings/Microsoft_Excel_Worksheet1.xlsx"));
    }

    [Fact]
    public void EveryDocument_SaysThatItWasWrittenWithAi()
    {
        var word = WordDocumentBuilder.BuildFromMarkdown("Informe", Illustrated);
        Assert.Contains(DocumentDisclosure.Short, Part(word, "word/footer1.xml"));
        Assert.Contains("footerReference", Part(word, "word/document.xml"));

        Assert.Contains(DocumentDisclosure.Short, Part(SpreadsheetDocumentBuilder.BuildFromMarkdown("Informe", Illustrated), "xl/worksheets/sheet1.xml"));

        var deck = PresentationDocumentBuilder.BuildFromMarkdown("Informe", Illustrated);
        var slides = PresentationPlan.From("Informe", Illustrated).Count;
        Assert.Contains(DocumentDisclosure.Long, Part(deck, $"ppt/slides/slide{slides}.xml"));
    }

    [Fact]
    public void Spreadsheet_ANumericTable_GetsItsChartNextToIt()
    {
        var document = SpreadsheetDocumentBuilder.BuildFromMarkdown("Energías renovables", Deck);
        AllPartsWellFormed(document);

        Assert.Contains("<drawing r:id=\"rId1\"/>", Part(document, "xl/worksheets/sheet1.xml"));
        Assert.Contains("<xdr:col>3</xdr:col>", Part(document, "xl/drawings/drawing1.xml"));
        Assert.Contains("'Evolución de la capacidad solar'!$A$2:$A$4", Part(document, "xl/charts/chart1.xml"));
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
