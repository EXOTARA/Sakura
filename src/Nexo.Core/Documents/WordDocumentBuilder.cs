using System.IO;
using System.IO.Compression;
using System.Globalization;
using System.Text;
using System.Xml;
using Nexo.Core.Assistant;

namespace Nexo.Core.Documents;

/// <summary>
/// Diseño D59 — escribe un .docx de verdad, sin depender de que Word esté instalado.
///
/// Un .docx es un ZIP con XML dentro (OOXML), así que se construye con lo que ya trae .NET. Se
/// descartó automatizar Word por COM, que era la vía obvia: exige tener Word, deja procesos
/// colgados cuando algo falla a medias, y falla de formas distintas según la versión. Sakura tiene
/// que poder dejarte el documento aunque no tengas Office — y si lo tienes, se abre igual.
///
/// 2026-09-15 — el documento sale con el formato de la respuesta (Adler, con un PDF del resultado: se
/// veían los asteriscos, los guiones y las rayas del Markdown). La respuesta se lee con
/// <see cref="AnswerMarkdown"/>, la misma lectura que dibuja el chat, y cada pieza se escribe con lo
/// que Word tiene para ella: títulos con estilos de título, listas numeradas y con viñetas de verdad
/// (anidadas), negrita, cursiva, enlaces que se pueden pulsar, tablas con cabecera y bloques de código.
/// Así lo que se ve en el chat y lo que queda en el documento no divergen.
/// </summary>
public static class WordDocumentBuilder
{
    /// <summary>Construye el documento a partir de la respuesta tal como la escribió el modelo.</summary>
    public static byte[] BuildFromMarkdown(string title, string markdown)
    {
        var writer = new DocumentWriter();
        var blocks = AnswerMarkdown.Parse(markdown);

        if (!string.IsNullOrWhiteSpace(title))
        {
            writer.Paragraph("Title", [new AnswerSpan(title.Trim(), AnswerSpanStyle.None)]);
        }

        var headingLevels = blocks.OfType<AnswerHeading>().Select(heading => heading.Level).Distinct().Order().ToList();
        var first = true;
        foreach (var block in blocks)
        {
            // Un título igual al del documento justo al empezar se omite: repetido se lee como error.
            if (first && block is AnswerHeading opening &&
                string.Equals(AnswerMarkdown.ToPlainText(opening.Spans).Trim(), title.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                first = false;
                continue;
            }

            first = false;
            switch (block)
            {
                case AnswerHeading heading:
                    var rank = Math.Min(3, headingLevels.IndexOf(heading.Level) + 1);
                    writer.EndList();
                    writer.Paragraph("Heading" + rank.ToString(CultureInfo.InvariantCulture), heading.Spans);
                    break;
                case AnswerListItem item:
                    writer.ListItem(item);
                    break;
                case AnswerTable table:
                    writer.EndList();
                    writer.Table(table);
                    break;
                case AnswerCode code:
                    writer.EndList();
                    foreach (var line in code.Text.Replace("\r\n", "\n").Split('\n'))
                    {
                        writer.Paragraph("CodeBlock", [new AnswerSpan(line, AnswerSpanStyle.None)]);
                    }

                    break;
                case AnswerQuote quote:
                    writer.EndList();
                    writer.Paragraph("Quote", quote.Spans);
                    break;
                case AnswerParagraph paragraph:
                    writer.EndList();
                    foreach (var line in SplitLines(paragraph.Spans))
                    {
                        writer.Paragraph(null, line);
                    }

                    break;
            }
        }

        return Package(title, writer);
    }

    /// <summary>
    /// Construye el documento a partir de apartados ya separados: cada título es un Heading1 y cada
    /// línea de su texto, un párrafo tal cual.
    /// </summary>
    public static byte[] Build(string title, IReadOnlyList<AnswerSection> sections)
    {
        ArgumentNullException.ThrowIfNull(sections);

        var writer = new DocumentWriter();
        if (!string.IsNullOrWhiteSpace(title))
        {
            writer.Paragraph("Title", [new AnswerSpan(title.Trim(), AnswerSpanStyle.None)]);
        }

        foreach (var section in sections)
        {
            if (section.Title.Length > 0)
            {
                writer.EndList();
                writer.Paragraph("Heading1", [new AnswerSpan(section.Title, AnswerSpanStyle.None)]);
            }

            // Cada línea es un párrafo propio: un salto de línea dentro de un párrafo de Word no
            // separa párrafos, y los pasos de una receta acabarían pegados en un ladrillo.
            foreach (var line in section.Body.Replace("\r\n", "\n").Split('\n'))
            {
                if (line.Length > 0)
                {
                    writer.EndList();
                    writer.Paragraph(null, [new AnswerSpan(line, AnswerSpanStyle.None)]);
                }
            }
        }

        return Package(title, writer);
    }

    private static IEnumerable<IReadOnlyList<AnswerSpan>> SplitLines(IReadOnlyList<AnswerSpan> spans)
    {
        var line = new List<AnswerSpan>();
        foreach (var span in spans)
        {
            var parts = span.Text.Split('\n');
            for (var i = 0; i < parts.Length; i++)
            {
                if (i > 0)
                {
                    yield return line;
                    line = [];
                }

                if (parts[i].Length > 0)
                {
                    line.Add(span with { Text = parts[i] });
                }
            }
        }

        if (line.Count > 0)
        {
            yield return line;
        }
    }

    private static byte[] Package(string title, DocumentWriter writer)
    {
        using var buffer = new MemoryStream();

        // Sin leaveOpen, el ZIP cierra el flujo al liberarse y el array sale vacío.
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(archive, "[Content_Types].xml", ContentTypes);
            Write(archive, "_rels/.rels", RootRelationships);
            Write(archive, "docProps/core.xml", CoreProperties(title));
            Write(archive, "word/_rels/document.xml.rels", writer.Relationships());
            Write(archive, "word/styles.xml", Styles);
            Write(archive, "word/numbering.xml", writer.Numbering());
            Write(archive, "word/document.xml", writer.Document());
        }

        return buffer.ToArray();
    }

    private static void Write(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();

        // Sin BOM: Word lo tolera, pero algunos lectores estrictos lo tratan como contenido y se
        // caen al leer la declaración XML.
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(content);
    }

    /// <summary>Escribe el cuerpo, las listas y los enlaces a medida que llegan los bloques.</summary>
    private sealed class DocumentWriter
    {
        private const int BulletAbstract = 0;
        private const int DecimalAbstract = 1;
        private const int BulletNumId = 1;

        private readonly StringBuilder _body = new();
        private readonly List<string> _hyperlinks = [];
        private readonly List<int> _numberedInstances = [];
        private int? _currentNumbered;
        private bool _inList;

        public void EndList()
        {
            _inList = false;
            _currentNumbered = null;
        }

        public void Paragraph(string? style, IReadOnlyList<AnswerSpan> spans)
        {
            _body.Append("<w:p>");
            if (style is not null)
            {
                _body.Append("<w:pPr><w:pStyle w:val=\"").Append(style).Append("\"/></w:pPr>");
            }

            Runs(spans);
            _body.Append("</w:p>");
        }

        public void ListItem(AnswerListItem item)
        {
            var level = Math.Clamp(item.Depth, 0, 2);
            int numId;
            if (item.Number is { } number)
            {
                // Una lista numerada nueva empieza en 1: cada una lleva su propia instancia para que
                // Word no siga contando desde la anterior.
                if (_currentNumbered is null || !_inList || number == 1 && level == 0)
                {
                    _currentNumbered = BulletNumId + 1 + _numberedInstances.Count;
                    _numberedInstances.Add(Math.Max(1, number));
                }

                numId = _currentNumbered.Value;
            }
            else
            {
                numId = BulletNumId;
            }

            _inList = true;
            _body.Append("<w:p><w:pPr><w:pStyle w:val=\"ListParagraph\"/><w:numPr><w:ilvl w:val=\"")
                .Append(level).Append("\"/><w:numId w:val=\"").Append(numId).Append("\"/></w:numPr></w:pPr>");
            Runs(item.Spans);
            _body.Append("</w:p>");
        }

        public void Table(AnswerTable table)
        {
            var columns = Math.Max(table.Header.Count, table.Rows.Count == 0 ? 0 : table.Rows.Max(row => row.Count));
            if (columns == 0)
            {
                return;
            }

            _body.Append("<w:tbl><w:tblPr><w:tblStyle w:val=\"SakuraTable\"/><w:tblW w:w=\"5000\" w:type=\"pct\"/>")
                .Append("<w:tblLook w:firstRow=\"1\" w:noHBand=\"0\" w:noVBand=\"1\"/></w:tblPr><w:tblGrid>");
            for (var i = 0; i < columns; i++)
            {
                _body.Append("<w:gridCol/>");
            }

            _body.Append("</w:tblGrid>");
            Row(table.Header, columns, header: true);
            foreach (var row in table.Rows)
            {
                Row(row, columns, header: false);
            }

            // Word exige un párrafo entre una tabla y lo que venga después.
            _body.Append("</w:tbl><w:p/>");
        }

        private void Row(IReadOnlyList<IReadOnlyList<AnswerSpan>> cells, int columns, bool header)
        {
            _body.Append("<w:tr>");
            if (header)
            {
                _body.Append("<w:trPr><w:tblHeader/></w:trPr>");
            }

            for (var i = 0; i < columns; i++)
            {
                _body.Append("<w:tc><w:tcPr>");
                if (header)
                {
                    _body.Append("<w:shd w:val=\"clear\" w:color=\"auto\" w:fill=\"EEF1F5\"/>");
                }

                _body.Append("</w:tcPr><w:p><w:pPr><w:spacing w:before=\"40\" w:after=\"40\"/></w:pPr>");
                var spans = i < cells.Count ? cells[i] : [];
                Runs(header ? [.. spans.Select(span => span with { Style = span.Style | AnswerSpanStyle.Bold })] : spans);
                _body.Append("</w:p></w:tc>");
            }

            _body.Append("</w:tr>");
        }

        private void Runs(IReadOnlyList<AnswerSpan> spans)
        {
            foreach (var span in spans)
            {
                if (span.Url is { } url)
                {
                    _hyperlinks.Add(url);
                    _body.Append("<w:hyperlink r:id=\"link").Append(_hyperlinks.Count).Append("\" w:history=\"1\">");
                    Run(span, "Hyperlink");
                    _body.Append("</w:hyperlink>");
                    continue;
                }

                Run(span, null);
            }
        }

        private void Run(AnswerSpan span, string? characterStyle)
        {
            _body.Append("<w:r>");
            var properties = new StringBuilder();
            if (characterStyle is not null)
            {
                properties.Append("<w:rStyle w:val=\"").Append(characterStyle).Append("\"/>");
            }

            if (span.Style.HasFlag(AnswerSpanStyle.Code))
            {
                properties.Append("<w:rFonts w:ascii=\"Consolas\" w:hAnsi=\"Consolas\" w:cs=\"Consolas\"/>")
                    .Append("<w:shd w:val=\"clear\" w:color=\"auto\" w:fill=\"F1F3F5\"/>");
            }

            if (span.Style.HasFlag(AnswerSpanStyle.Bold))
            {
                properties.Append("<w:b/>");
            }

            if (span.Style.HasFlag(AnswerSpanStyle.Italic))
            {
                properties.Append("<w:i/>");
            }

            if (properties.Length > 0)
            {
                _body.Append("<w:rPr>").Append(properties).Append("</w:rPr>");
            }

            // xml:space="preserve" conserva los espacios de los extremos de cada trozo; sin él, «**negrita** y»
            // quedaría pegado como «negritay».
            _body.Append("<w:t xml:space=\"preserve\">").Append(Escape(span.Text)).Append("</w:t></w:r>");
        }

        public string Document() =>
            XmlDeclaration +
            "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\" " +
            "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
            "<w:body>" + _body + SectionProperties + "</w:body></w:document>";

        public string Relationships()
        {
            var builder = new StringBuilder(XmlDeclaration)
                .Append("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">")
                .Append("<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>")
                .Append("<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/numbering\" Target=\"numbering.xml\"/>");
            for (var i = 0; i < _hyperlinks.Count; i++)
            {
                builder.Append("<Relationship Id=\"link").Append(i + 1)
                    .Append("\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink\" Target=\"")
                    .Append(EscapeAttribute(_hyperlinks[i])).Append("\" TargetMode=\"External\"/>");
            }

            return builder.Append("</Relationships>").ToString();
        }

        public string Numbering()
        {
            var builder = new StringBuilder(XmlDeclaration)
                .Append("<w:numbering xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">");

            builder.Append("<w:abstractNum w:abstractNumId=\"").Append(BulletAbstract).Append("\"><w:multiLevelType w:val=\"hybridMultilevel\"/>");
            string[] bullets = ["•", "◦", "▪"];
            for (var level = 0; level < 3; level++)
            {
                builder.Append("<w:lvl w:ilvl=\"").Append(level).Append("\"><w:start w:val=\"1\"/><w:numFmt w:val=\"bullet\"/>")
                    .Append("<w:lvlText w:val=\"").Append(bullets[level]).Append("\"/><w:lvlJc w:val=\"left\"/>")
                    .Append("<w:pPr><w:ind w:left=\"").Append(360 + (level * 360)).Append("\" w:hanging=\"260\"/></w:pPr>")
                    .Append("<w:rPr><w:color w:val=\"8E3B62\"/></w:rPr></w:lvl>");
            }

            builder.Append("</w:abstractNum>");

            builder.Append("<w:abstractNum w:abstractNumId=\"").Append(DecimalAbstract).Append("\"><w:multiLevelType w:val=\"hybridMultilevel\"/>");
            (string Format, string Text)[] numbered = [("decimal", "%1."), ("lowerLetter", "%2)"), ("lowerRoman", "%3.")];
            for (var level = 0; level < 3; level++)
            {
                builder.Append("<w:lvl w:ilvl=\"").Append(level).Append("\"><w:start w:val=\"1\"/><w:numFmt w:val=\"")
                    .Append(numbered[level].Format).Append("\"/><w:lvlText w:val=\"").Append(numbered[level].Text)
                    .Append("\"/><w:lvlJc w:val=\"left\"/><w:pPr><w:ind w:left=\"")
                    .Append(360 + (level * 360)).Append("\" w:hanging=\"360\"/></w:pPr>")
                    .Append("<w:rPr><w:b/><w:color w:val=\"1F3A5F\"/></w:rPr></w:lvl>");
            }

            builder.Append("</w:abstractNum>");

            builder.Append("<w:num w:numId=\"").Append(BulletNumId).Append("\"><w:abstractNumId w:val=\"").Append(BulletAbstract).Append("\"/></w:num>");
            for (var i = 0; i < _numberedInstances.Count; i++)
            {
                builder.Append("<w:num w:numId=\"").Append(BulletNumId + 1 + i).Append("\"><w:abstractNumId w:val=\"")
                    .Append(DecimalAbstract).Append("\"/><w:lvlOverride w:ilvl=\"0\"><w:startOverride w:val=\"")
                    .Append(_numberedInstances[i]).Append("\"/></w:lvlOverride></w:num>");
            }

            return builder.Append("</w:numbering>").ToString();
        }
    }

    /// <summary>
    /// Escapa para XML y descarta lo que XML no admite.
    ///
    /// Un modelo puede devolver caracteres de control —restos de un token mal decodificado— y basta
    /// uno para que Word declare el archivo dañado y se niegue a abrirlo entero. Perder un carácter
    /// invisible es mejor que perder el documento.
    ///
    /// El ampersand va primero: si fuera después, convertiría en «&amp;lt;» los «&lt;» que acaban de
    /// escaparse.
    /// </summary>
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

        return clean.ToString()
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
    }

    private static string EscapeAttribute(string text) =>
        Escape(text).Replace("\"", "&quot;", StringComparison.Ordinal);

    private static string CoreProperties(string title) =>
        XmlDeclaration +
        "<cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" " +
        "xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:dcterms=\"http://purl.org/dc/terms/\" " +
        "xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">" +
        "<dc:title>" + Escape(title ?? string.Empty) + "</dc:title><dc:creator>Sakura</dc:creator>" +
        "<dcterms:created xsi:type=\"dcterms:W3CDTF\">" +
        DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture) +
        "</dcterms:created></cp:coreProperties>";

    private const string XmlDeclaration =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>";

    private const string SectionProperties =
        "<w:sectPr><w:pgSz w:w=\"11906\" w:h=\"16838\"/>" +
        "<w:pgMar w:top=\"1418\" w:right=\"1304\" w:bottom=\"1418\" w:left=\"1304\" w:header=\"709\" w:footer=\"709\"/></w:sectPr>";

    private const string ContentTypes =
        XmlDeclaration +
        "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
        "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
        "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
        "<Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/>" +
        "<Override PartName=\"/word/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml\"/>" +
        "<Override PartName=\"/word/numbering.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.numbering+xml\"/>" +
        "<Override PartName=\"/docProps/core.xml\" ContentType=\"application/vnd.openxmlformats-package.core-properties+xml\"/>" +
        "</Types>";

    private const string RootRelationships =
        XmlDeclaration +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/>" +
        "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties\" Target=\"docProps/core.xml\"/>" +
        "</Relationships>";

    /// <summary>
    /// Los estilos, declarados de verdad en vez de imitados con negrita y tamaño. Un Heading1 real es
    /// lo que hace que Word construya el panel de navegación y el índice automático. Letra Calibri de
    /// 11 puntos con interlineado cómodo, títulos en azul oscuro y un filete bajo el título del
    /// documento: sobrio, como un informe.
    /// </summary>
    private const string Styles =
        XmlDeclaration +
        "<w:styles xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">" +
        "<w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii=\"Calibri\" w:hAnsi=\"Calibri\" w:eastAsia=\"Calibri\" w:cs=\"Calibri\"/>" +
        "<w:sz w:val=\"22\"/><w:szCs w:val=\"22\"/><w:color w:val=\"24292F\"/><w:lang w:val=\"es-ES\"/></w:rPr></w:rPrDefault>" +
        "<w:pPrDefault><w:pPr><w:spacing w:after=\"140\" w:line=\"288\" w:lineRule=\"auto\"/></w:pPr></w:pPrDefault></w:docDefaults>" +
        "<w:style w:type=\"paragraph\" w:default=\"1\" w:styleId=\"Normal\"><w:name w:val=\"Normal\"/><w:qFormat/></w:style>" +
        "<w:style w:type=\"paragraph\" w:styleId=\"Title\"><w:name w:val=\"Title\"/><w:basedOn w:val=\"Normal\"/><w:next w:val=\"Normal\"/><w:qFormat/>" +
        "<w:pPr><w:pBdr><w:bottom w:val=\"single\" w:sz=\"8\" w:space=\"6\" w:color=\"8E3B62\"/></w:pBdr><w:spacing w:after=\"320\"/></w:pPr>" +
        "<w:rPr><w:rFonts w:ascii=\"Calibri Light\" w:hAnsi=\"Calibri Light\"/><w:b/><w:color w:val=\"1F3A5F\"/><w:sz w:val=\"48\"/></w:rPr></w:style>" +
        "<w:style w:type=\"paragraph\" w:styleId=\"Heading1\"><w:name w:val=\"heading 1\"/><w:basedOn w:val=\"Normal\"/><w:next w:val=\"Normal\"/><w:qFormat/>" +
        "<w:pPr><w:keepNext/><w:outlineLvl w:val=\"0\"/><w:spacing w:before=\"320\" w:after=\"120\"/></w:pPr>" +
        "<w:rPr><w:b/><w:color w:val=\"1F3A5F\"/><w:sz w:val=\"32\"/></w:rPr></w:style>" +
        "<w:style w:type=\"paragraph\" w:styleId=\"Heading2\"><w:name w:val=\"heading 2\"/><w:basedOn w:val=\"Normal\"/><w:next w:val=\"Normal\"/><w:qFormat/>" +
        "<w:pPr><w:keepNext/><w:outlineLvl w:val=\"1\"/><w:spacing w:before=\"240\" w:after=\"80\"/></w:pPr>" +
        "<w:rPr><w:b/><w:color w:val=\"2E5A88\"/><w:sz w:val=\"26\"/></w:rPr></w:style>" +
        "<w:style w:type=\"paragraph\" w:styleId=\"Heading3\"><w:name w:val=\"heading 3\"/><w:basedOn w:val=\"Normal\"/><w:next w:val=\"Normal\"/><w:qFormat/>" +
        "<w:pPr><w:keepNext/><w:outlineLvl w:val=\"2\"/><w:spacing w:before=\"200\" w:after=\"60\"/></w:pPr>" +
        "<w:rPr><w:b/><w:color w:val=\"444C56\"/><w:sz w:val=\"23\"/></w:rPr></w:style>" +
        "<w:style w:type=\"paragraph\" w:styleId=\"ListParagraph\"><w:name w:val=\"List Paragraph\"/><w:basedOn w:val=\"Normal\"/><w:qFormat/>" +
        "<w:pPr><w:spacing w:after=\"60\"/><w:contextualSpacing/></w:pPr></w:style>" +
        "<w:style w:type=\"paragraph\" w:styleId=\"CodeBlock\"><w:name w:val=\"Código\"/><w:basedOn w:val=\"Normal\"/>" +
        "<w:pPr><w:shd w:val=\"clear\" w:color=\"auto\" w:fill=\"F1F3F5\"/><w:spacing w:after=\"0\" w:line=\"240\" w:lineRule=\"auto\"/><w:ind w:left=\"200\" w:right=\"200\"/></w:pPr>" +
        "<w:rPr><w:rFonts w:ascii=\"Consolas\" w:hAnsi=\"Consolas\" w:cs=\"Consolas\"/><w:sz w:val=\"19\"/></w:rPr></w:style>" +
        "<w:style w:type=\"paragraph\" w:styleId=\"Quote\"><w:name w:val=\"Quote\"/><w:basedOn w:val=\"Normal\"/><w:qFormat/>" +
        "<w:pPr><w:pBdr><w:left w:val=\"single\" w:sz=\"18\" w:space=\"8\" w:color=\"8E3B62\"/></w:pBdr><w:ind w:left=\"240\"/><w:spacing w:before=\"120\" w:after=\"160\"/></w:pPr>" +
        "<w:rPr><w:i/><w:color w:val=\"57606A\"/></w:rPr></w:style>" +
        "<w:style w:type=\"character\" w:styleId=\"Hyperlink\"><w:name w:val=\"Hyperlink\"/><w:rPr><w:color w:val=\"0B5CAD\"/><w:u w:val=\"single\"/></w:rPr></w:style>" +
        "<w:style w:type=\"table\" w:styleId=\"SakuraTable\"><w:name w:val=\"Tabla de Sakura\"/><w:tblPr>" +
        "<w:tblBorders><w:top w:val=\"single\" w:sz=\"4\" w:color=\"D0D7DE\"/><w:left w:val=\"single\" w:sz=\"4\" w:color=\"D0D7DE\"/>" +
        "<w:bottom w:val=\"single\" w:sz=\"4\" w:color=\"D0D7DE\"/><w:right w:val=\"single\" w:sz=\"4\" w:color=\"D0D7DE\"/>" +
        "<w:insideH w:val=\"single\" w:sz=\"4\" w:color=\"D0D7DE\"/><w:insideV w:val=\"single\" w:sz=\"4\" w:color=\"D0D7DE\"/></w:tblBorders>" +
        "<w:tblCellMar><w:left w:w=\"100\" w:type=\"dxa\"/><w:right w:w=\"100\" w:type=\"dxa\"/></w:tblCellMar></w:tblPr></w:style>" +
        "</w:styles>";
}
