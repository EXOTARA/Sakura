using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using Nexo.Core.Assistant;

namespace Nexo.Core.Documents;

/// <summary>
/// 2026-09-15 — una respuesta como presentación de PowerPoint (.pptx), sin necesitar PowerPoint
/// instalado. Qué va en cada diapositiva lo decide <see cref="PresentationPlan"/>; aquí solo se dibuja.
///
/// Diseño de 16:9 con los colores de Sakura: portada con un panel azul oscuro y círculos de color,
/// índice numerado, separadores de parte y conclusión sobre fondo oscuro, citas en grande, puntos con
/// viñetas del color de Sakura, dos columnas cuando hay muchos puntos cortos, tablas con la cabecera
/// oscura y gráficas nativas —que se editan desde PowerPoint porque llevan su hoja de datos dentro—.
/// Cada diapositiva lleva el título de la presentación y su número abajo, y las notas del orador que
/// haya escrito el modelo.
///
/// El texto se escribe en cuadros normales y no en marcadores de posición del patrón: así la
/// presentación se ve igual en PowerPoint, en Keynote o en Google Slides.
/// </summary>
public static class PresentationDocumentBuilder
{
    private const long SlideWidth = 12192000;
    private const long SlideHeight = 6858000;
    private const long Margin = 685800;
    private const long BodyTop = 1737360;
    private const long BodyBottom = 6126480;
    private const string Navy = "1F3A5F";
    private const string Blue = "2E5A88";
    private const string Accent = "8E3B62";
    private const string Pink = "C06C8E";
    private const string Ink = "24292F";
    private const string Muted = "57606A";
    private const string Soft = "F3F5F8";

    /// <param name="images">
    /// Las imágenes ya descargadas, por el texto que las pedía. Sin ellas la presentación sale igual,
    /// solo que sin fotos: una búsqueda que falle no puede costar el documento entero.
    /// </param>
    /// <param name="sources">Las fuentes encontradas, que van en su propia diapositiva.</param>
    public static byte[] BuildFromMarkdown(
        string title,
        string markdown,
        IReadOnlyDictionary<string, DocumentImage>? images = null,
        IReadOnlyList<DocumentSource>? sources = null)
    {
        var slides = PresentationPlan.From(title, markdown).ToList();
        var documentTitle = slides[0].Title;
        var charts = new List<(ChartSpec Spec, AnswerTable Table)>();

        // Solo las imágenes que de verdad se van a ver, en el orden en que salen.
        var used = new List<DocumentImage>();
        var perSlide = new Dictionary<int, DocumentImage>();
        for (var i = 0; i < slides.Count; i++)
        {
            if (Find(images, slides[i].ImageQuery) is { } image && PresentationPlan.AcceptsImage(slides[i].Layout))
            {
                perSlide[i] = image;
                if (!used.Contains(image))
                {
                    used.Add(image);
                }
            }
        }

        // 2026-09-16 — las fuentes, antes de los créditos de imágenes: una cosa es de dónde salió lo
        // que se dice y otra de dónde salieron las fotos.
        if (sources is { Count: > 0 })
        {
            slides.Add(new SlideSpec("Fuentes para consultar",
                sources.Select(source => new SlideLine([new AnswerSpan(ApaReference.Format(source), AnswerSpanStyle.None)], 0, null, IsBullet: true)).ToList(),
                SlideLayout.Credits));
        }

        if (used.Count > 0)
        {
            slides.Add(new SlideSpec("Créditos de imágenes",
                used.Select(image => new SlideLine([new AnswerSpan(WikimediaImagePolicy.CreditLine(image), AnswerSpanStyle.None)], 0, null, IsBullet: true)).ToList(),
                SlideLayout.Credits));
        }

        var notes = slides.Select((slide, index) => (slide, index)).Where(item => !string.IsNullOrWhiteSpace(item.slide.Notes)).Select(item => item.index + 1).ToList();

        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(archive, "[Content_Types].xml", ContentTypes(slides.Count, slides.Count(slide => slide.Chart is not null), notes, used));
            Write(archive, "_rels/.rels", RootRelationships);
            Write(archive, "docProps/core.xml", CoreProperties(title));
            Write(archive, "ppt/presentation.xml", Presentation(slides.Count, notes.Count > 0));
            Write(archive, "ppt/_rels/presentation.xml.rels", PresentationRelationships(slides.Count, notes.Count > 0));
            Write(archive, "ppt/slideMasters/slideMaster1.xml", SlideMaster);
            Write(archive, "ppt/slideMasters/_rels/slideMaster1.xml.rels", SlideMasterRelationships);
            Write(archive, "ppt/slideLayouts/slideLayout1.xml", SlideLayoutXml);
            Write(archive, "ppt/slideLayouts/_rels/slideLayout1.xml.rels", SlideLayoutRelationships);
            Write(archive, "ppt/theme/theme1.xml", Theme);

            for (var i = 0; i < slides.Count; i++)
            {
                var spec = slides[i];
                var slide = new SlideWriter(i + 1, slides.Count, documentTitle, charts,
                    perSlide.TryGetValue(i, out var image) ? (image, used.IndexOf(image) + 1) : null);
                var xml = spec.Layout switch
                {
                    SlideLayout.Cover => slide.Cover(spec),
                    SlideLayout.Agenda => slide.Agenda(spec),
                    SlideLayout.Process => slide.Process(spec),
                    SlideLayout.Section => slide.Section(spec),
                    SlideLayout.TwoColumns => slide.TwoColumns(spec),
                    SlideLayout.Table => slide.TableSlide(spec),
                    SlideLayout.Chart when spec.Chart is not null && spec.Table is not null => slide.ChartSlide(spec),
                    SlideLayout.Quote => slide.Quote(spec),
                    SlideLayout.Closing => slide.Closing(spec),
                    SlideLayout.Credits => slide.Credits(spec),
                    _ => slide.Content(spec)
                };
                Write(archive, $"ppt/slides/slide{i + 1}.xml", xml);
                Write(archive, $"ppt/slides/_rels/slide{i + 1}.xml.rels", slide.Relationships(hasNotes: notes.Contains(i + 1)));

                if (notes.Contains(i + 1))
                {
                    Write(archive, $"ppt/notesSlides/notesSlide{i + 1}.xml", NotesSlide(spec.Notes!));
                    Write(archive, $"ppt/notesSlides/_rels/notesSlide{i + 1}.xml.rels", NotesSlideRelationships(i + 1));
                }
            }

            for (var i = 0; i < used.Count; i++)
            {
                WriteBytes(archive, $"ppt/media/image{i + 1}.{used[i].Extension}", used[i].Bytes);
            }

            for (var i = 0; i < charts.Count; i++)
            {
                var number = i + 1;
                Write(archive, $"ppt/charts/chart{number}.xml", ChartXml.Build(charts[i].Spec, EmbeddedSheet, 1400, "rId1"));
                Write(archive, $"ppt/charts/_rels/chart{number}.xml.rels",
                    XmlDeclaration +
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                    "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/package\" " +
                    $"Target=\"../embeddings/Microsoft_Excel_Worksheet{number}.xlsx\"/></Relationships>");
                WriteBytes(archive, $"ppt/embeddings/Microsoft_Excel_Worksheet{number}.xlsx", SpreadsheetDocumentBuilder.BuildTableWorkbook(EmbeddedSheet, charts[i].Table));
            }

            if (notes.Count > 0)
            {
                Write(archive, "ppt/notesMasters/notesMaster1.xml", NotesMaster);
                Write(archive, "ppt/notesMasters/_rels/notesMaster1.xml.rels",
                    XmlDeclaration +
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                    "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/theme\" Target=\"../theme/theme2.xml\"/></Relationships>");
                Write(archive, "ppt/theme/theme2.xml", Theme);
            }
        }

        return buffer.ToArray();
    }

    private const string EmbeddedSheet = "Hoja1";

    private static DocumentImage? Find(IReadOnlyDictionary<string, DocumentImage>? images, string? query)
    {
        if (images is null || string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        if (images.TryGetValue(query, out var exact))
        {
            return exact;
        }

        return images.FirstOrDefault(pair => string.Equals(pair.Key, query, StringComparison.OrdinalIgnoreCase)).Value;
    }

    private sealed class SlideWriter(int number, int total, string documentTitle, List<(ChartSpec Spec, AnswerTable Table)> charts, (DocumentImage Image, int Number)? picture)
    {
        private readonly List<string> _links = [];
        private readonly List<int> _charts = [];
        private int _shapeId = 1;
        private bool _dark;
        private bool _usesPicture;

        private int NextId() => ++_shapeId;

        public string Cover(SlideSpec spec)
        {
            const long panel = 7772400;
            const long textWidth = panel - (2 * Margin) - 457200;
            var titleSize = spec.Title.Length <= 40 ? 4000 : spec.Title.Length <= 70 ? 3400 : 2800;

            // Con foto, la foto es el panel; sin ella, el panel oscuro con sus círculos.
            var shapes = new StringBuilder();
            if (picture is not null)
            {
                shapes.Append(Picture(panel, 0, SlideWidth - panel, SlideHeight))
                    .Append(Shape("rect", panel, SlideHeight - 457200, SlideWidth - panel, 457200, Navy, alpha: 75,
                        text: "<a:p><a:pPr algn=\"ctr\"/>" + PlainRun(Credit(), 900, "E6EDF5") + "</a:p>"))
                    .Append(Shape("rect", panel, 0, 45720, SlideHeight, Accent));
            }
            else
            {
                shapes.Append(Shape("rect", panel, 0, SlideWidth - panel, SlideHeight, Navy))
                    .Append(Shape("ellipse", SlideWidth - 2743200, -914400, 3657600, 3657600, Blue, alpha: 55))
                    .Append(Shape("ellipse", panel - 1143000, 3886200, 2286000, 2286000, Accent))
                    .Append(Shape("ellipse", SlideWidth - 1600200, SlideHeight - 1371600, 685800, 685800, Pink, alpha: 80));
            }

            shapes
                .Append(TextBox(Margin, 914400, textWidth, 2971800, "b",
                    "<a:p><a:pPr><a:spcAft><a:spcPts val=\"1200\"/></a:spcAft><a:buNone/></a:pPr>" + PlainRun("PRESENTACIÓN", 1400, Accent, bold: true, spacing: 300) + "</a:p>" +
                    Paragraph([new AnswerSpan(spec.Title, AnswerSpanStyle.Bold)], titleSize, Navy, bullet: null, lineSpacing: 90)))
                .Append(Shape("rect", Margin, 4069080, 1097280, 54864, Accent));

            if (!string.IsNullOrWhiteSpace(spec.Subtitle))
            {
                shapes.Append(TextBox(Margin, 4297680, textWidth, 1371600, "t",
                    Paragraph([new AnswerSpan(spec.Subtitle, AnswerSpanStyle.None)], 2000, Muted, bullet: null)));
            }

            var date = DateTime.Now.ToString("MMMM 'de' yyyy", CultureInfo.GetCultureInfo("es-ES"));
            shapes.Append(TextBox(Margin, SlideHeight - 822960, textWidth, 320040, "b",
                "<a:p>" + PlainRun(char.ToUpperInvariant(date[0]) + date[1..], 1200, "8C959F") + "</a:p>"));

            return SlideXml(shapes.ToString(), "FFFFFF");
        }

        public string Agenda(SlideSpec spec)
        {
            var shapes = new StringBuilder(Header(spec.Title));
            // Cada entrada es una línea de primer nivel; la de segundo nivel que la sigue, sus apartados.
            var entries = new List<(SlideLine Entry, SlideLine? Detail)>();
            foreach (var line in spec.Lines)
            {
                if (line.Depth > 0 && entries.Count > 0)
                {
                    entries[^1] = (entries[^1].Entry, line);
                }
                else
                {
                    entries.Add((line, null));
                }
            }

            var count = entries.Count;
            var perColumn = count <= 4 ? count : (int)Math.Ceiling(count / 2d);
            var columns = count <= 4 ? 1 : 2;
            const long gap = 457200;
            var columnWidth = (SlideWidth - (2 * Margin) - ((columns - 1) * gap)) / columns;
            var rowHeight = Math.Min(960120, (BodyBottom - BodyTop) / perColumn);
            const long circle = 548640;

            for (var i = 0; i < count; i++)
            {
                var column = i / perColumn;
                var row = i % perColumn;
                var x = Margin + (column * (columnWidth + gap));
                var y = BodyTop + (row * rowHeight);
                shapes.Append(Shape("ellipse", x, y + ((rowHeight - circle) / 2), circle, circle, i % 2 == 0 ? Accent : Blue,
                    text: "<a:p><a:pPr algn=\"ctr\"/>" + PlainRun((i + 1).ToString(CultureInfo.InvariantCulture), 1600, "FFFFFF", bold: true) + "</a:p>"));
                var text = Paragraph(entries[i].Entry.Spans, count <= 4 ? 2400 : 2000, Ink, bullet: null, spaceBefore: 0);
                if (entries[i].Detail is { } detail)
                {
                    text += Paragraph(detail.Spans, 1400, Muted, bullet: null, spaceBefore: 200);
                }

                shapes.Append(TextBox(x + circle + 228600, y, columnWidth - circle - 228600, rowHeight, "ctr", text));
            }

            return SlideXml(shapes.Append(Footer()).ToString(), "FFFFFF");
        }

        public string Process(SlideSpec spec)
        {
            var shapes = new StringBuilder(Header(spec.Title));
            var count = Math.Max(1, spec.Lines.Count);
            var slot = (SlideWidth - (2 * Margin)) / count;
            const long circle = 822960;
            const long top = 2834640;
            var centerY = top + (circle / 2);

            if (count > 1)
            {
                shapes.Append(Shape("rect", Margin + (slot / 2), centerY - 13716, slot * (count - 1), 27432, "D0D7DE"));
            }

            for (var i = 0; i < spec.Lines.Count; i++)
            {
                var x = Margin + (i * slot);
                shapes.Append(Shape("ellipse", x + ((slot - circle) / 2), top, circle, circle, i % 2 == 0 ? Accent : Blue,
                    text: "<a:p><a:pPr algn=\"ctr\"/>" + PlainRun((i + 1).ToString(CultureInfo.InvariantCulture), 2400, "FFFFFF", bold: true) + "</a:p>"));
                shapes.Append(TextBox(x + 91440, top + circle + 274320, slot - 182880, 1737360, "t",
                    Paragraph(spec.Lines[i].Spans.Select(span => span with { Style = span.Style | AnswerSpanStyle.Bold }).ToList(),
                        count <= 4 ? 2000 : 1800, Navy, bullet: null, spaceBefore: 0, align: "ctr")));
            }

            return SlideXml(shapes.Append(Footer()).ToString(), "FFFFFF");
        }

        public string Section(SlideSpec spec)
        {
            _dark = true;
            var titleSize = spec.Title.Length <= 40 ? 4000 : 3200;
            var shapes = new StringBuilder();

            // Con foto, ocupa toda la diapositiva con el azul por encima: el texto se sigue leyendo.
            if (picture is not null)
            {
                shapes.Append(Picture(0, 0, SlideWidth, SlideHeight))
                    .Append(Shape("rect", 0, 0, SlideWidth, SlideHeight, Navy, alpha: 80))
                    .Append(TextBox(SlideWidth - Margin - 4572000, SlideHeight - 822960, 4572000, 320040, "b",
                        "<a:p><a:pPr algn=\"r\"/>" + PlainRun(Credit(), 900, "9FB3C8") + "</a:p>"));
            }
            else
            {
                shapes.Append(Shape("ellipse", SlideWidth - 3657600, SlideHeight - 3200400, 4572000, 4572000, Blue, alpha: 45))
                    .Append(Shape("ellipse", SlideWidth - 4206240, 822960, 1188720, 1188720, Accent, alpha: 85));
            }

            shapes
                .Append(TextBox(Margin, 1463040, 3657600, 1280160, "b",
                    "<a:p>" + PlainRun((spec.SectionNumber ?? 1).ToString("00", CultureInfo.InvariantCulture), 6000, Pink, bold: true) + "</a:p>"))
                .Append(Shape("rect", Margin, 2834640, 1097280, 54864, Pink))
                .Append(TextBox(Margin, 3063240, SlideWidth - (2 * Margin) - 3017520, 2011680, "t",
                    Paragraph([new AnswerSpan(spec.Title, AnswerSpanStyle.Bold)], titleSize, "FFFFFF", bullet: null, lineSpacing: 90)))
                .Append(Footer());

            return SlideXml(shapes.ToString(), Navy);
        }

        public string Content(SlideSpec spec)
        {
            // Con foto, ocupa la franja derecha de arriba abajo y el texto se queda en la izquierda.
            var width = SlideWidth - (2 * Margin);
            var shapes = new StringBuilder();
            if (picture is not null)
            {
                const long photo = 4937760;
                width = SlideWidth - photo - Margin - 457200;
                shapes.Append(Picture(SlideWidth - photo, 0, photo, SlideHeight))
                    .Append(Shape("rect", SlideWidth - photo, SlideHeight - 365760, photo, 365760, Navy, alpha: 70,
                        text: "<a:p><a:pPr algn=\"ctr\"/>" + PlainRun(Credit(), 900, "E6EDF5") + "</a:p>"));
            }

            shapes.Append(Header(spec.Title, width))
                .Append(TextBox(Margin, BodyTop, width, BodyBottom - BodyTop, "t", Body(spec.Lines, Ink), autofit: true))
                .Append(Footer(picture is not null));
            return SlideXml(shapes.ToString(), "FFFFFF");
        }

        /// <summary>De dónde salió cada imagen: sin esto, una licencia libre se estaría incumpliendo.</summary>
        public string Credits(SlideSpec spec)
        {
            var shapes = new StringBuilder(Header(spec.Title))
                .Append(TextBox(Margin, BodyTop, SlideWidth - (2 * Margin), BodyBottom - BodyTop - 457200, "t",
                    string.Concat(spec.Lines.Select(line => Paragraph(line.Spans, 1400, Muted, (null, 0), spaceBefore: 600))), autofit: true))
                .Append(TextBox(Margin, BodyBottom - 320040, SlideWidth - (2 * Margin), 320040, "b",
                    "<a:p>" + PlainRun("Imágenes de Wikimedia Commons con licencia libre.", 1200, "8C959F") + "</a:p>"))
                .Append(Footer());
            return SlideXml(shapes.ToString(), "FFFFFF");
        }

        public string TwoColumns(SlideSpec spec)
        {
            const long gap = 548640;
            var width = (SlideWidth - (2 * Margin) - gap) / 2;
            var half = (int)Math.Ceiling(spec.Lines.Count / 2d);
            var shapes = new StringBuilder(Header(spec.Title))
                .Append(TextBox(Margin, BodyTop, width, BodyBottom - BodyTop, "t", Body(spec.Lines.Take(half).ToList(), Ink, size: 2200, roomy: half <= 5), autofit: true))
                .Append(TextBox(Margin + width + gap, BodyTop, width, BodyBottom - BodyTop, "t", Body(spec.Lines.Skip(half).ToList(), Ink, size: 2200, roomy: half <= 5), autofit: true))
                .Append(Footer());
            return SlideXml(shapes.ToString(), "FFFFFF");
        }

        public string TableSlide(SlideSpec spec)
        {
            var shapes = new StringBuilder(Header(spec.Title));
            if (spec.Table is { } table)
            {
                shapes.Append(Table(table, Margin, BodyTop, SlideWidth - (2 * Margin)));
            }

            return SlideXml(shapes.Append(Footer()).ToString(), "FFFFFF");
        }

        public string ChartSlide(SlideSpec spec)
        {
            var shapes = new StringBuilder(Header(spec.Title));
            var chartX = Margin;
            if (spec.Lines.Count > 0)
            {
                const long textWidth = 3749040;
                shapes.Append(TextBox(Margin, BodyTop + 91440, textWidth, BodyBottom - BodyTop - 91440, "ctr", Body(spec.Lines, Ink, size: 2000), autofit: true));
                chartX = Margin + textWidth + 365760;
            }

            charts.Add((spec.Chart!, spec.Table!));
            _charts.Add(charts.Count);
            shapes.Append("<p:graphicFrame><p:nvGraphicFramePr><p:cNvPr id=\"").Append(NextId()).Append("\" name=\"Gráfica\"/>")
                .Append("<p:cNvGraphicFramePr><a:graphicFrameLocks noGrp=\"1\"/></p:cNvGraphicFramePr><p:nvPr/></p:nvGraphicFramePr>")
                .Append("<p:xfrm><a:off x=\"").Append(chartX).Append("\" y=\"").Append(BodyTop).Append("\"/><a:ext cx=\"").Append(SlideWidth - Margin - chartX)
                .Append("\" cy=\"").Append(BodyBottom - BodyTop).Append("\"/></p:xfrm>")
                .Append("<a:graphic><a:graphicData uri=\"").Append(ChartXml.Namespace).Append("\"><c:chart xmlns:c=\"").Append(ChartXml.Namespace)
                .Append("\" r:id=\"chart").Append(charts.Count).Append("\"/></a:graphicData></a:graphic></p:graphicFrame>");

            return SlideXml(shapes.Append(Footer()).ToString(), "FFFFFF");
        }

        public string Quote(SlideSpec spec)
        {
            var text = spec.Lines.Count > 0 ? spec.Lines[0].Spans : [];
            var length = AnswerMarkdown.ToPlainText(text).Length;
            var size = length <= 60 ? 4000 : length <= 120 ? 3200 : length <= 200 ? 2800 : 2400;
            var italic = text.Select(span => span with { Style = span.Style | AnswerSpanStyle.Italic }).ToList();

            var shapes = new StringBuilder()
                .Append(Shape("rect", 0, 0, 182880, SlideHeight, Accent))
                .Append(TextBox(Margin + 182880, 548640, SlideWidth - (2 * Margin), 457200, "t",
                    "<a:p>" + PlainRun(spec.Title.ToUpperInvariant(), 1400, Accent, bold: true, spacing: 200) + "</a:p>"))
                .Append(TextBox(Margin + 182880, 1371600, 1371600, 1188720, "b",
                    "<a:p>" + PlainRun("“", 12000, Pink, bold: true, font: "Georgia") + "</a:p>"))
                .Append(TextBox(Margin + 182880, 2651760, SlideWidth - (2 * Margin) - 1097280, 2926080, "t",
                    Paragraph(italic, size, Navy, bullet: null, lineSpacing: 105, spaceBefore: 0)))
                .Append(Footer());

            return SlideXml(shapes.ToString(), Soft);
        }

        public string Closing(SlideSpec spec)
        {
            _dark = true;
            var shapes = new StringBuilder();
            if (picture is not null)
            {
                shapes.Append(Picture(0, 0, SlideWidth, SlideHeight))
                    .Append(Shape("rect", 0, 0, SlideWidth, SlideHeight, Navy, alpha: 82))
                    .Append(TextBox(SlideWidth - Margin - 4572000, SlideHeight - 822960, 4572000, 320040, "b",
                        "<a:p><a:pPr algn=\"r\"/>" + PlainRun(Credit(), 900, "9FB3C8") + "</a:p>"));
            }

            shapes
                .Append(Shape("ellipse", SlideWidth - 2560320, SlideHeight - 2286000, 3657600, 3657600, Blue, alpha: picture is null ? 40 : 20))
                .Append(TextBox(Margin, 457200, SlideWidth - (2 * Margin), 868680, "b",
                    Paragraph([new AnswerSpan(spec.Title, AnswerSpanStyle.Bold)], TitleSize(spec.Title), "FFFFFF", bullet: null)))
                .Append(Shape("rect", Margin, 1371600, 914400, 45720, Pink))
                .Append(TextBox(Margin, BodyTop, SlideWidth - (2 * Margin) - 1828800, BodyBottom - BodyTop, "t", Body(spec.Lines, "E6EDF5"), autofit: true))
                .Append(Footer());

            return SlideXml(shapes.ToString(), Navy);
        }

        public string Relationships(bool hasNotes)
        {
            var builder = new StringBuilder(XmlDeclaration)
                .Append("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">")
                .Append("<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideLayout\" Target=\"../slideLayouts/slideLayout1.xml\"/>");
            for (var i = 0; i < _links.Count; i++)
            {
                builder.Append("<Relationship Id=\"link").Append(i + 1)
                    .Append("\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink\" Target=\"")
                    .Append(EscapeAttribute(_links[i])).Append("\" TargetMode=\"External\"/>");
            }

            if (_usesPicture && picture is { } item)
            {
                builder.Append("<Relationship Id=\"image").Append(item.Number)
                    .Append("\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/image\" Target=\"../media/image")
                    .Append(item.Number).Append('.').Append(item.Image.Extension).Append("\"/>");
            }

            foreach (var chart in _charts)
            {
                builder.Append("<Relationship Id=\"chart").Append(chart).Append("\" Type=\"").Append(ChartXml.RelationshipType)
                    .Append("\" Target=\"../charts/chart").Append(chart).Append(".xml\"/>");
            }

            if (hasNotes)
            {
                builder.Append("<Relationship Id=\"notes1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/notesSlide\" Target=\"../notesSlides/notesSlide")
                    .Append(number).Append(".xml\"/>");
            }

            return builder.Append("</Relationships>").ToString();
        }

        private static int TitleSize(string title) => title.Length <= 45 ? 2800 : title.Length <= 75 ? 2400 : 2000;

        private string Header(string title, long? width = null) =>
            TextBox(Margin, 457200, width ?? (SlideWidth - (2 * Margin)), 868680, "b",
                Paragraph([new AnswerSpan(title, AnswerSpanStyle.Bold)], TitleSize(title), Navy, bullet: null)) +
            Shape("rect", Margin, 1371600, 914400, 45720, Accent);

        /// <param name="narrow">Con una foto a la derecha, el pie se queda en la mitad izquierda.</param>
        private string Footer(bool narrow = false)
        {
            var color = _dark ? "9FB3C8" : "8C959F";
            var right = narrow ? (SlideWidth / 2) - Margin : SlideWidth - Margin - 1828800;
            var footer = TextBox(Margin, SlideHeight - 548640, (SlideWidth / 2) - Margin - (narrow ? 1828800 : 0), 320040, "ctr",
                             "<a:p>" + PlainRun(documentTitle, 1100, color) + "</a:p>") +
                         TextBox(right, SlideHeight - 548640, 1828800, 320040, "ctr",
                             "<a:p><a:pPr algn=\"r\"/>" + PlainRun($"{number} / {total}", 1100, color) + "</a:p>");

            // 2026-09-16 — la última diapositiva declara el uso de IA (DocumentDisclosure).
            return number == total
                ? footer + TextBox(Margin, SlideHeight - 868680, SlideWidth - (2 * Margin), 274320, "b",
                      "<a:p>" + PlainRun(DocumentDisclosure.Long, 900, color) + "</a:p>")
                : footer;
        }

        /// <summary>El crédito corto que acompaña a la foto en la propia diapositiva.</summary>
        private string Credit() =>
            picture is { } item ? WikimediaImagePolicy.Credit(item.Image.Author, item.Image.License) : string.Empty;

        /// <summary>
        /// La foto, recortada por el centro para llenar el hueco sin deformarse: una imagen estirada
        /// canta más que una recortada.
        /// </summary>
        private string Picture(long x, long y, long width, long height)
        {
            if (picture is not { } item)
            {
                return string.Empty;
            }

            _usesPicture = true;
            var box = (double)width / height;
            var image = item.Image.Height == 0 ? box : (double)item.Image.Width / item.Image.Height;
            var crop = image > box
                ? $"<a:srcRect l=\"{Percent((1 - (box / image)) / 2)}\" r=\"{Percent((1 - (box / image)) / 2)}\"/>"
                : $"<a:srcRect t=\"{Percent((1 - (image / box)) / 2)}\" b=\"{Percent((1 - (image / box)) / 2)}\"/>";

            return "<p:pic><p:nvPicPr><p:cNvPr id=\"" + NextId() + "\" name=\"" + EscapeAttribute(item.Image.Title) + "\" descr=\"" +
                   EscapeAttribute(WikimediaImagePolicy.Credit(item.Image.Author, item.Image.License)) + "\"/>" +
                   "<p:cNvPicPr><a:picLocks noChangeAspect=\"1\"/></p:cNvPicPr><p:nvPr/></p:nvPicPr>" +
                   "<p:blipFill><a:blip r:embed=\"image" + item.Number + "\"/>" + crop + "<a:stretch><a:fillRect/></a:stretch></p:blipFill>" +
                   "<p:spPr>" + Transform(x, y, width, height) + "<a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></p:spPr></p:pic>";
        }

        private static int Percent(double fraction) => (int)Math.Round(Math.Clamp(fraction, 0, 0.45) * 100000);

        /// <summary>
        /// El texto de una diapositiva. Con pocas líneas va más grande para llenar y leerse de lejos; con
        /// muchas, más pequeño. Un párrafo que abre una lista de puntos va destacado, como entradilla.
        /// </summary>
        private string Body(IReadOnlyList<SlideLine> lines, string color, int? size = null, bool? roomy = null)
        {
            var characters = lines.Sum(item => item.Length);
            // Con pocas líneas, más aire entre ellas: la diapositiva no se queda con media página vacía.
            var spacious = roomy ?? (lines.Count <= 5 && characters < 360);
            var baseSize = size ?? (lines.Count <= 4 && characters < 320 ? 2400 : characters > 420 ? 1800 : 2000);
            var hasLead = lines.Count > 1 && !lines[0].IsBullet && lines.Skip(1).Any(line => line.IsBullet);
            var body = new StringBuilder();
            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                var lineSize = baseSize - (line.Depth * 200);
                var bullet = line.IsBullet ? (line.Number, line.Depth) : ((int?, int)?)null;
                var lineColor = i == 0 && hasLead && !_dark ? Blue : color;
                var before = i == 0 ? 0 : spacious ? (line.Depth == 0 ? 1800 : 600) : (line.Depth == 0 ? 1000 : 300);
                if (i == 1 && hasLead)
                {
                    before += 600;
                }

                body.Append(Paragraph(line.Spans, i == 0 && hasLead ? lineSize + 200 : lineSize, lineColor, bullet, spaceBefore: before));
            }

            return body.ToString();
        }

        private static string SlideXml(string shapes, string background) =>
            XmlDeclaration +
            "<p:sld " + Namespaces + ">" +
            "<p:cSld><p:bg><p:bgPr><a:solidFill><a:srgbClr val=\"" + background + "\"/></a:solidFill><a:effectLst/></p:bgPr></p:bg>" +
            "<p:spTree><p:nvGrpSpPr><p:cNvPr id=\"1\" name=\"\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>" +
            "<p:grpSpPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"0\" cy=\"0\"/><a:chOff x=\"0\" y=\"0\"/><a:chExt cx=\"0\" cy=\"0\"/></a:xfrm></p:grpSpPr>" +
            shapes + "</p:spTree></p:cSld><p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr></p:sld>";

        /// <summary>Una forma de color (rectángulo o círculo), opcionalmente semitransparente y con texto centrado.</summary>
        private string Shape(string geometry, long x, long y, long width, long height, string color, int alpha = 100, string? text = null)
        {
            var fill = "<a:srgbClr val=\"" + color + "\">" + (alpha < 100 ? "<a:alpha val=\"" + (alpha * 1000) + "\"/>" : string.Empty) + "</a:srgbClr>";
            return "<p:sp><p:nvSpPr><p:cNvPr id=\"" + NextId() + "\" name=\"Adorno\"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>" +
                   "<p:spPr>" + Transform(x, y, width, height) + "<a:prstGeom prst=\"" + geometry + "\"><a:avLst/></a:prstGeom>" +
                   "<a:solidFill>" + fill + "</a:solidFill><a:ln><a:noFill/></a:ln></p:spPr>" +
                   (text is null
                       ? string.Empty
                       : "<p:txBody><a:bodyPr wrap=\"none\" lIns=\"0\" tIns=\"0\" rIns=\"0\" bIns=\"0\" anchor=\"ctr\"><a:noAutofit/></a:bodyPr><a:lstStyle/>" + text + "</p:txBody>") +
                   "</p:sp>";
        }

        private string TextBox(long x, long y, long width, long height, string anchor, string paragraphs, bool autofit = false) =>
            "<p:sp><p:nvSpPr><p:cNvPr id=\"" + NextId() + "\" name=\"Texto\"/><p:cNvSpPr txBox=\"1\"/><p:nvPr/></p:nvSpPr>" +
            "<p:spPr>" + Transform(x, y, width, height) + "<a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom><a:noFill/></p:spPr>" +
            "<p:txBody><a:bodyPr wrap=\"square\" lIns=\"0\" tIns=\"0\" rIns=\"0\" bIns=\"0\" anchor=\"" + anchor + "\">" +
            (autofit ? "<a:normAutofit/>" : "<a:noAutofit/>") + "</a:bodyPr><a:lstStyle/>" +
            (paragraphs.Length == 0 ? "<a:p><a:endParaRPr lang=\"es-ES\"/></a:p>" : paragraphs) + "</p:txBody></p:sp>";

        private static string Transform(long x, long y, long width, long height) =>
            "<a:xfrm><a:off x=\"" + x + "\" y=\"" + y + "\"/><a:ext cx=\"" + width + "\" cy=\"" + height + "\"/></a:xfrm>";

        private string Paragraph(IReadOnlyList<AnswerSpan> spans, int size, string color, (int? Number, int Depth)? bullet, int lineSpacing = 100, int spaceBefore = 600, string align = "l")
        {
            var builder = new StringBuilder("<a:p>");
            var spacing = lineSpacing == 100 ? string.Empty : "<a:lnSpc><a:spcPct val=\"" + (lineSpacing * 1000) + "\"/></a:lnSpc>";
            if (bullet is { } list)
            {
                var indent = 342900 + (list.Depth * 342900);
                builder.Append("<a:pPr marL=\"").Append(indent).Append("\" indent=\"-285750\">").Append(spacing)
                    .Append("<a:spcBef><a:spcPts val=\"").Append(spaceBefore).Append("\"/></a:spcBef>")
                    .Append("<a:buClr><a:srgbClr val=\"").Append(_dark ? Pink : Accent).Append("\"/></a:buClr>");
                if (list.Number is { } value)
                {
                    builder.Append("<a:buFont typeface=\"+mj-lt\"/><a:buAutoNum type=\"arabicPeriod\" startAt=\"").Append(Math.Max(1, value)).Append("\"/>");
                }
                else
                {
                    builder.Append("<a:buFont typeface=\"Arial\"/><a:buChar char=\"").Append(list.Depth == 0 ? "•" : "–").Append("\"/>");
                }

                builder.Append("</a:pPr>");
            }
            else
            {
                builder.Append("<a:pPr marL=\"0\" indent=\"0\" algn=\"").Append(align).Append("\">").Append(spacing)
                    .Append("<a:spcBef><a:spcPts val=\"").Append(spaceBefore).Append("\"/></a:spcBef><a:buNone/></a:pPr>");
            }

            foreach (var span in spans)
            {
                foreach (var piece in span.Text.Split('\n'))
                {
                    if (piece.Length > 0)
                    {
                        builder.Append(Run(span with { Text = piece }, size, color));
                    }
                }
            }

            return builder.Append("<a:endParaRPr lang=\"es-ES\" sz=\"").Append(size).Append("\"/></a:p>").ToString();
        }

        private static string PlainRun(string text, int size, string color, bool bold = false, int spacing = 0, string font = "Calibri") =>
            "<a:r><a:rPr lang=\"es-ES\" sz=\"" + size + "\"" + (bold ? " b=\"1\"" : string.Empty) + (spacing != 0 ? " spc=\"" + spacing + "\"" : string.Empty) +
            " dirty=\"0\"><a:solidFill><a:srgbClr val=\"" + color + "\"/></a:solidFill><a:latin typeface=\"" + font + "\"/><a:cs typeface=\"" + font + "\"/></a:rPr>" +
            "<a:t>" + Escape(text) + "</a:t></a:r>";

        private string Run(AnswerSpan span, int size, string color)
        {
            var builder = new StringBuilder("<a:r><a:rPr lang=\"es-ES\" sz=\"").Append(size).Append('"');
            if (span.Style.HasFlag(AnswerSpanStyle.Bold))
            {
                builder.Append(" b=\"1\"");
            }

            if (span.Style.HasFlag(AnswerSpanStyle.Italic))
            {
                builder.Append(" i=\"1\"");
            }

            if (span.Url is not null)
            {
                builder.Append(" u=\"sng\"");
            }

            var linkColor = _dark ? "9CC3F0" : "0B5CAD";
            builder.Append(" dirty=\"0\"><a:solidFill><a:srgbClr val=\"").Append(span.Url is not null ? linkColor : color).Append("\"/></a:solidFill>");
            var font = span.Style.HasFlag(AnswerSpanStyle.Code) ? "Consolas" : "Calibri";
            builder.Append("<a:latin typeface=\"").Append(font).Append("\"/><a:cs typeface=\"").Append(font).Append("\"/>");

            if (span.Url is { } url)
            {
                _links.Add(url);
                builder.Append("<a:hlinkClick r:id=\"link").Append(_links.Count).Append("\"/>");
            }

            return builder.Append("</a:rPr><a:t>").Append(Escape(span.Text)).Append("</a:t></a:r>").ToString();
        }

        /// <summary>Una tabla con columnas del ancho de su contenido: una columna de cifras no ocupa lo mismo que una de frases.</summary>
        private string Table(AnswerTable table, long x, long y, long width)
        {
            var columns = Math.Max(table.Header.Count, table.Rows.Count == 0 ? 0 : table.Rows.Max(row => row.Count));
            if (columns == 0)
            {
                return string.Empty;
            }

            var rows = table.Rows.Count;
            var rowHeight = rows <= 5 ? 548640L : 457200L;
            var fontSize = rows <= 5 ? 1600 : 1400;
            var weights = Enumerable.Range(0, columns).Select(column =>
                Math.Clamp(new[] { table.Header }.Concat(table.Rows)
                    .Select(row => column < row.Count ? AnswerMarkdown.ToPlainText(row[column]).Length : 0)
                    .DefaultIfEmpty(0).Max(), 8, 40)).ToList();
            var totalWeight = weights.Sum();
            var widths = weights.Select(weight => width * weight / totalWeight).ToList();

            var builder = new StringBuilder()
                .Append("<p:graphicFrame><p:nvGraphicFramePr><p:cNvPr id=\"").Append(NextId()).Append("\" name=\"Tabla\"/>")
                .Append("<p:cNvGraphicFramePr><a:graphicFrameLocks noGrp=\"1\"/></p:cNvGraphicFramePr><p:nvPr/></p:nvGraphicFramePr>")
                .Append("<p:xfrm><a:off x=\"").Append(x).Append("\" y=\"").Append(y).Append("\"/><a:ext cx=\"").Append(widths.Sum())
                .Append("\" cy=\"").Append(rowHeight * (rows + 1)).Append("\"/></p:xfrm>")
                .Append("<a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/table\"><a:tbl><a:tblPr firstRow=\"1\" bandRow=\"1\"/><a:tblGrid>");
            foreach (var columnWidth in widths)
            {
                builder.Append("<a:gridCol w=\"").Append(columnWidth).Append("\"/>");
            }

            builder.Append("</a:tblGrid>");
            TableRow(builder, table.Header, columns, rowHeight, fontSize, header: true, shaded: false);
            for (var i = 0; i < rows; i++)
            {
                TableRow(builder, table.Rows[i], columns, rowHeight, fontSize, header: false, shaded: i % 2 == 1);
            }

            return builder.Append("</a:tbl></a:graphicData></a:graphic></p:graphicFrame>").ToString();
        }

        private void TableRow(StringBuilder builder, IReadOnlyList<IReadOnlyList<AnswerSpan>> cells, int columns, long height, int fontSize, bool header, bool shaded)
        {
            builder.Append("<a:tr h=\"").Append(height).Append("\">");
            for (var i = 0; i < columns; i++)
            {
                var spans = i < cells.Count ? cells[i] : [];
                builder.Append("<a:tc><a:txBody><a:bodyPr/><a:lstStyle/><a:p>");
                foreach (var span in spans)
                {
                    builder.Append(Run(header ? span with { Style = span.Style | AnswerSpanStyle.Bold } : span, fontSize, header ? "FFFFFF" : Ink));
                }

                builder.Append("<a:endParaRPr lang=\"es-ES\" sz=\"").Append(fontSize).Append("\"/></a:p></a:txBody>")
                    .Append("<a:tcPr marL=\"128016\" marR=\"128016\" marT=\"45720\" marB=\"45720\" anchor=\"ctr\">");
                foreach (var side in new[] { "lnL", "lnR", "lnT", "lnB" })
                {
                    builder.Append("<a:").Append(side).Append(" w=\"6350\"><a:solidFill><a:srgbClr val=\"D0D7DE\"/></a:solidFill></a:").Append(side).Append('>');
                }

                var fill = header ? Navy : shaded ? Soft : "FFFFFF";
                builder.Append("<a:solidFill><a:srgbClr val=\"").Append(fill).Append("\"/></a:solidFill></a:tcPr></a:tc>");
            }

            builder.Append("</a:tr>");
        }
    }

    private static string NotesSlide(string notes)
    {
        var paragraphs = new StringBuilder();
        foreach (var line in notes.Split('\n'))
        {
            paragraphs.Append("<a:p><a:r><a:rPr lang=\"es-ES\" dirty=\"0\"/><a:t>").Append(Escape(line.Trim())).Append("</a:t></a:r></a:p>");
        }

        return XmlDeclaration +
               "<p:notes " + Namespaces + "><p:cSld><p:spTree><p:nvGrpSpPr><p:cNvPr id=\"1\" name=\"\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>" +
               "<p:grpSpPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"0\" cy=\"0\"/><a:chOff x=\"0\" y=\"0\"/><a:chExt cx=\"0\" cy=\"0\"/></a:xfrm></p:grpSpPr>" +
               "<p:sp><p:nvSpPr><p:cNvPr id=\"2\" name=\"Imagen de diapositiva\"/><p:cNvSpPr><a:spLocks noGrp=\"1\" noRot=\"1\" noChangeAspect=\"1\"/></p:cNvSpPr>" +
               "<p:nvPr><p:ph type=\"sldImg\"/></p:nvPr></p:nvSpPr><p:spPr/></p:sp>" +
               "<p:sp><p:nvSpPr><p:cNvPr id=\"3\" name=\"Notas\"/><p:cNvSpPr><a:spLocks noGrp=\"1\"/></p:cNvSpPr><p:nvPr><p:ph type=\"body\" idx=\"1\"/></p:nvPr></p:nvSpPr>" +
               "<p:spPr/><p:txBody><a:bodyPr/><a:lstStyle/>" + paragraphs + "</p:txBody></p:sp>" +
               "</p:spTree></p:cSld><p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr></p:notes>";
    }

    private static string NotesSlideRelationships(int slide) =>
        XmlDeclaration +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/notesMaster\" Target=\"../notesMasters/notesMaster1.xml\"/>" +
        "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/slide\" Target=\"../slides/slide" + slide + ".xml\"/>" +
        "</Relationships>";

    private const string NotesMaster =
        XmlDeclaration +
        "<p:notesMaster " + Namespaces + "><p:cSld><p:bg><p:bgRef idx=\"1001\"><a:schemeClr val=\"bg1\"/></p:bgRef></p:bg>" +
        "<p:spTree><p:nvGrpSpPr><p:cNvPr id=\"1\" name=\"\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>" +
        "<p:grpSpPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"0\" cy=\"0\"/><a:chOff x=\"0\" y=\"0\"/><a:chExt cx=\"0\" cy=\"0\"/></a:xfrm></p:grpSpPr>" +
        "<p:sp><p:nvSpPr><p:cNvPr id=\"2\" name=\"Imagen de diapositiva\"/><p:cNvSpPr><a:spLocks noGrp=\"1\" noRot=\"1\" noChangeAspect=\"1\"/></p:cNvSpPr>" +
        "<p:nvPr><p:ph type=\"sldImg\" idx=\"2\"/></p:nvPr></p:nvSpPr><p:spPr><a:xfrm><a:off x=\"381000\" y=\"685800\"/><a:ext cx=\"6096000\" cy=\"3429000\"/></a:xfrm>" +
        "<a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom><a:noFill/><a:ln w=\"12700\"><a:solidFill><a:srgbClr val=\"D0D7DE\"/></a:solidFill></a:ln></p:spPr></p:sp>" +
        "<p:sp><p:nvSpPr><p:cNvPr id=\"3\" name=\"Notas\"/><p:cNvSpPr><a:spLocks noGrp=\"1\"/></p:cNvSpPr><p:nvPr><p:ph type=\"body\" sz=\"quarter\" idx=\"3\"/></p:nvPr></p:nvSpPr>" +
        "<p:spPr><a:xfrm><a:off x=\"685800\" y=\"4400550\"/><a:ext cx=\"5486400\" cy=\"3600450\"/></a:xfrm><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></p:spPr>" +
        "<p:txBody><a:bodyPr vert=\"horz\" lIns=\"91440\" tIns=\"45720\" rIns=\"91440\" bIns=\"45720\" rtlCol=\"0\"/><a:lstStyle/><a:p><a:pPr lvl=\"0\"/><a:endParaRPr lang=\"es-ES\"/></a:p></p:txBody></p:sp>" +
        "</p:spTree></p:cSld>" +
        "<p:clrMap bg1=\"lt1\" tx1=\"dk1\" bg2=\"lt2\" tx2=\"dk2\" accent1=\"accent1\" accent2=\"accent2\" accent3=\"accent3\" accent4=\"accent4\" accent5=\"accent5\" accent6=\"accent6\" hlink=\"hlink\" folHlink=\"folHlink\"/>" +
        "<p:notesStyle><a:lvl1pPr marL=\"0\" algn=\"l\" defTabSz=\"914400\" rtl=\"0\" eaLnBrk=\"1\" latinLnBrk=\"0\" hangingPunct=\"1\">" +
        "<a:defRPr sz=\"1200\" kern=\"1200\"><a:solidFill><a:schemeClr val=\"tx1\"/></a:solidFill><a:latin typeface=\"+mn-lt\"/><a:ea typeface=\"+mn-ea\"/><a:cs typeface=\"+mn-cs\"/></a:defRPr>" +
        "</a:lvl1pPr></p:notesStyle></p:notesMaster>";

    private static string Presentation(int slideCount, bool hasNotes)
    {
        var builder = new StringBuilder(XmlDeclaration)
            .Append("<p:presentation ").Append(Namespaces).Append(" saveSubsetFonts=\"1\">")
            .Append("<p:sldMasterIdLst><p:sldMasterId id=\"2147483648\" r:id=\"rId1\"/></p:sldMasterIdLst>");
        if (hasNotes)
        {
            builder.Append("<p:notesMasterIdLst><p:notesMasterId r:id=\"rId").Append(slideCount + 3).Append("\"/></p:notesMasterIdLst>");
        }

        builder.Append("<p:sldIdLst>");
        for (var i = 0; i < slideCount; i++)
        {
            builder.Append("<p:sldId id=\"").Append(256 + i).Append("\" r:id=\"rId").Append(i + 3).Append("\"/>");
        }

        return builder.Append("</p:sldIdLst><p:sldSz cx=\"").Append(SlideWidth).Append("\" cy=\"").Append(SlideHeight)
            .Append("\"/><p:notesSz cx=\"6858000\" cy=\"9144000\"/><p:defaultTextStyle/></p:presentation>").ToString();
    }

    private static string PresentationRelationships(int slideCount, bool hasNotes)
    {
        var builder = new StringBuilder(XmlDeclaration)
            .Append("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">")
            .Append("<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideMaster\" Target=\"slideMasters/slideMaster1.xml\"/>")
            .Append("<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/theme\" Target=\"theme/theme1.xml\"/>");
        for (var i = 0; i < slideCount; i++)
        {
            builder.Append("<Relationship Id=\"rId").Append(i + 3)
                .Append("\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/slide\" Target=\"slides/slide")
                .Append(i + 1).Append(".xml\"/>");
        }

        if (hasNotes)
        {
            builder.Append("<Relationship Id=\"rId").Append(slideCount + 3)
                .Append("\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/notesMaster\" Target=\"notesMasters/notesMaster1.xml\"/>");
        }

        return builder.Append("</Relationships>").ToString();
    }

    private static string ContentTypes(int slideCount, int chartCount, IReadOnlyList<int> notes, IReadOnlyList<DocumentImage> images)
    {
        var builder = new StringBuilder(XmlDeclaration)
            .Append("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">")
            .Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>")
            .Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>")
            .Append("<Default Extension=\"xlsx\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet\"/>")
            .Append(images.Any(image => image.Extension == "jpg") ? "<Default Extension=\"jpg\" ContentType=\"image/jpeg\"/>" : string.Empty)
            .Append(images.Any(image => image.Extension == "png") ? "<Default Extension=\"png\" ContentType=\"image/png\"/>" : string.Empty)
            .Append("<Override PartName=\"/ppt/presentation.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml\"/>")
            .Append("<Override PartName=\"/ppt/slideMasters/slideMaster1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.slideMaster+xml\"/>")
            .Append("<Override PartName=\"/ppt/slideLayouts/slideLayout1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.slideLayout+xml\"/>")
            .Append("<Override PartName=\"/ppt/theme/theme1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.theme+xml\"/>")
            .Append("<Override PartName=\"/docProps/core.xml\" ContentType=\"application/vnd.openxmlformats-package.core-properties+xml\"/>");
        for (var i = 0; i < slideCount; i++)
        {
            builder.Append("<Override PartName=\"/ppt/slides/slide").Append(i + 1)
                .Append(".xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.slide+xml\"/>");
        }

        for (var i = 0; i < chartCount; i++)
        {
            builder.Append("<Override PartName=\"/ppt/charts/chart").Append(i + 1).Append(".xml\" ContentType=\"").Append(ChartXml.ContentType).Append("\"/>");
        }

        if (notes.Count > 0)
        {
            builder.Append("<Override PartName=\"/ppt/notesMasters/notesMaster1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.notesMaster+xml\"/>")
                .Append("<Override PartName=\"/ppt/theme/theme2.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.theme+xml\"/>");
            foreach (var slide in notes)
            {
                builder.Append("<Override PartName=\"/ppt/notesSlides/notesSlide").Append(slide)
                    .Append(".xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.notesSlide+xml\"/>");
            }
        }

        return builder.Append("</Types>").ToString();
    }

    private const string Namespaces =
        "xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" " +
        "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" " +
        "xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\"";

    private const string XmlDeclaration = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>";

    private const string RootRelationships =
        XmlDeclaration +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"ppt/presentation.xml\"/>" +
        "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties\" Target=\"docProps/core.xml\"/>" +
        "</Relationships>";

    private const string EmptyTree =
        "<p:spTree><p:nvGrpSpPr><p:cNvPr id=\"1\" name=\"\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>" +
        "<p:grpSpPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"0\" cy=\"0\"/><a:chOff x=\"0\" y=\"0\"/><a:chExt cx=\"0\" cy=\"0\"/></a:xfrm></p:grpSpPr></p:spTree>";

    private const string SlideMaster =
        XmlDeclaration +
        "<p:sldMaster " + Namespaces + "><p:cSld><p:bg><p:bgRef idx=\"1001\"><a:schemeClr val=\"bg1\"/></p:bgRef></p:bg>" + EmptyTree + "</p:cSld>" +
        "<p:clrMap bg1=\"lt1\" tx1=\"dk1\" bg2=\"lt2\" tx2=\"dk2\" accent1=\"accent1\" accent2=\"accent2\" accent3=\"accent3\" accent4=\"accent4\" accent5=\"accent5\" accent6=\"accent6\" hlink=\"hlink\" folHlink=\"folHlink\"/>" +
        "<p:sldLayoutIdLst><p:sldLayoutId id=\"2147483649\" r:id=\"rId1\"/></p:sldLayoutIdLst>" +
        "<p:txStyles><p:titleStyle/><p:bodyStyle/><p:otherStyle/></p:txStyles></p:sldMaster>";

    private const string SlideMasterRelationships =
        XmlDeclaration +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideLayout\" Target=\"../slideLayouts/slideLayout1.xml\"/>" +
        "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/theme\" Target=\"../theme/theme1.xml\"/>" +
        "</Relationships>";

    private const string SlideLayoutXml =
        XmlDeclaration +
        "<p:sldLayout " + Namespaces + " preserve=\"1\"><p:cSld name=\"En blanco\">" + EmptyTree + "</p:cSld>" +
        "<p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr></p:sldLayout>";

    private const string SlideLayoutRelationships =
        XmlDeclaration +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideMaster\" Target=\"../slideMasters/slideMaster1.xml\"/>" +
        "</Relationships>";

    /// <summary>Un tema completo: PowerPoint repara el archivo si falta cualquiera de sus listas.</summary>
    private const string Theme =
        XmlDeclaration +
        "<a:theme xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" name=\"Sakura\"><a:themeElements>" +
        "<a:clrScheme name=\"Sakura\"><a:dk1><a:srgbClr val=\"24292F\"/></a:dk1><a:lt1><a:srgbClr val=\"FFFFFF\"/></a:lt1>" +
        "<a:dk2><a:srgbClr val=\"1F3A5F\"/></a:dk2><a:lt2><a:srgbClr val=\"F3F5F8\"/></a:lt2>" +
        "<a:accent1><a:srgbClr val=\"2E5A88\"/></a:accent1><a:accent2><a:srgbClr val=\"8E3B62\"/></a:accent2>" +
        "<a:accent3><a:srgbClr val=\"7FA7C9\"/></a:accent3><a:accent4><a:srgbClr val=\"C06C8E\"/></a:accent4>" +
        "<a:accent5><a:srgbClr val=\"8C959F\"/></a:accent5><a:accent6><a:srgbClr val=\"1F3A5F\"/></a:accent6>" +
        "<a:hlink><a:srgbClr val=\"0B5CAD\"/></a:hlink><a:folHlink><a:srgbClr val=\"6E40C9\"/></a:folHlink></a:clrScheme>" +
        "<a:fontScheme name=\"Sakura\"><a:majorFont><a:latin typeface=\"Calibri\"/><a:ea typeface=\"\"/><a:cs typeface=\"\"/></a:majorFont>" +
        "<a:minorFont><a:latin typeface=\"Calibri\"/><a:ea typeface=\"\"/><a:cs typeface=\"\"/></a:minorFont></a:fontScheme>" +
        "<a:fmtScheme name=\"Sakura\"><a:fillStyleLst>" +
        "<a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill>" +
        "</a:fillStyleLst><a:lnStyleLst>" +
        "<a:ln w=\"6350\"><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill></a:ln>" +
        "<a:ln w=\"12700\"><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill></a:ln>" +
        "<a:ln w=\"19050\"><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill></a:ln>" +
        "</a:lnStyleLst><a:effectStyleLst><a:effectStyle><a:effectLst/></a:effectStyle><a:effectStyle><a:effectLst/></a:effectStyle><a:effectStyle><a:effectLst/></a:effectStyle></a:effectStyleLst>" +
        "<a:bgFillStyleLst><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill></a:bgFillStyleLst>" +
        "</a:fmtScheme></a:themeElements><a:objectDefaults/><a:extraClrSchemeLst/></a:theme>";

    private static void Write(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(content);
    }

    private static void WriteBytes(ZipArchive archive, string path, byte[] content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(content);
    }

    private static string Escape(string text)
    {
        var clean = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            if (XmlConvert.IsXmlChar(character))
            {
                clean.Append(character);
            }
        }

        return clean.ToString().Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }

    private static string EscapeAttribute(string text) => Escape(text).Replace("\"", "&quot;");

    private static string CoreProperties(string title) =>
        XmlDeclaration +
        "<cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" " +
        "xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:dcterms=\"http://purl.org/dc/terms/\" " +
        "xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">" +
        "<dc:title>" + Escape(title ?? string.Empty) + "</dc:title><dc:creator>Sakura</dc:creator>" +
        "<dcterms:created xsi:type=\"dcterms:W3CDTF\">" +
        DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture) +
        "</dcterms:created></cp:coreProperties>";
}
