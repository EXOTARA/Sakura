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
/// Diseño sobrio de 16:9: fondo blanco, título en azul oscuro con una barra corta del color de Sakura
/// debajo, puntos con viñetas del mismo color, negritas y enlaces que se pueden pulsar, tablas con la
/// cabecera oscura y el número de diapositiva abajo. La portada lleva una franja de color a la
/// izquierda. El texto se escribe en cuadros normales y no en marcadores de posición del patrón: así
/// la presentación se ve igual en PowerPoint, en Keynote o en Google Slides.
/// </summary>
public static class PresentationDocumentBuilder
{
    private const long SlideWidth = 12192000;
    private const long SlideHeight = 6858000;
    private const long Margin = 685800;
    private const string Navy = "1F3A5F";
    private const string Accent = "8E3B62";
    private const string Ink = "24292F";

    public static byte[] BuildFromMarkdown(string title, string markdown)
    {
        var slides = PresentationPlan.From(title, markdown);

        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(archive, "[Content_Types].xml", ContentTypes(slides.Count));
            Write(archive, "_rels/.rels", RootRelationships);
            Write(archive, "docProps/core.xml", CoreProperties(title));
            Write(archive, "ppt/presentation.xml", Presentation(slides.Count));
            Write(archive, "ppt/_rels/presentation.xml.rels", PresentationRelationships(slides.Count));
            Write(archive, "ppt/slideMasters/slideMaster1.xml", SlideMaster);
            Write(archive, "ppt/slideMasters/_rels/slideMaster1.xml.rels", SlideMasterRelationships);
            Write(archive, "ppt/slideLayouts/slideLayout1.xml", SlideLayout);
            Write(archive, "ppt/slideLayouts/_rels/slideLayout1.xml.rels", SlideLayoutRelationships);
            Write(archive, "ppt/theme/theme1.xml", Theme);

            for (var i = 0; i < slides.Count; i++)
            {
                var slide = new SlideWriter(i + 1, slides.Count);
                var xml = slides[i].IsCover ? slide.Cover(slides[i]) : slide.Content(slides[i]);
                Write(archive, $"ppt/slides/slide{i + 1}.xml", xml);
                Write(archive, $"ppt/slides/_rels/slide{i + 1}.xml.rels", slide.Relationships());
            }
        }

        return buffer.ToArray();
    }

    private sealed class SlideWriter(int number, int total)
    {
        private readonly List<string> _links = [];
        private int _shapeId = 1;

        private int NextId() => ++_shapeId;

        public string Cover(SlideSpec spec)
        {
            var shapes = new StringBuilder()
                .Append(Rectangle(0, 0, 228600, SlideHeight, Accent))
                .Append(TextBox(Margin + 228600, 2057400, SlideWidth - (2 * Margin) - 228600, 1371600, "b",
                    Paragraph([new AnswerSpan(spec.Title, AnswerSpanStyle.Bold)], 4000, Navy, bullet: null)))
                .Append(Rectangle(Margin + 228600, 3520440, 1097280, 54864, Accent));

            if (!string.IsNullOrWhiteSpace(spec.Subtitle))
            {
                shapes.Append(TextBox(Margin + 228600, 3749040, SlideWidth - (2 * Margin) - 228600, 1143000, "t",
                    Paragraph([new AnswerSpan(spec.Subtitle, AnswerSpanStyle.None)], 2000, "57606A", bullet: null)));
            }

            return SlideXml(shapes.ToString());
        }

        public string Content(SlideSpec spec)
        {
            var shapes = new StringBuilder()
                .Append(TextBox(Margin, 457200, SlideWidth - (2 * Margin), 868680, "b",
                    Paragraph([new AnswerSpan(spec.Title, AnswerSpanStyle.Bold)], 2800, Navy, bullet: null)))
                .Append(Rectangle(Margin, 1371600, 914400, 45720, Accent));

            const long bodyTop = 1600200;
            var bodyHeight = SlideHeight - bodyTop - 731520;

            if (spec.Table is { } table)
            {
                shapes.Append(Table(table, Margin, bodyTop, SlideWidth - (2 * Margin)));
            }
            else
            {
                var body = new StringBuilder();
                foreach (var line in spec.Lines)
                {
                    // Con pocos puntos se escribe más grande: la diapositiva se llena y se lee desde lejos.
                    var few = spec.Lines.Count <= 4 && spec.Lines.Sum(item => item.Length) < 320;
                    var size = (line.Depth == 0 ? 2000 : 1800) + (few ? 400 : 0);
                    var bullet = line.IsBullet ? (line.Number, line.Depth) : ((int?, int)?)null;
                    body.Append(Paragraph(line.Spans, size, Ink, bullet));
                }

                shapes.Append(TextBox(Margin, bodyTop, SlideWidth - (2 * Margin), bodyHeight, "t", body.ToString(), autofit: true));
            }

            shapes.Append(TextBox(SlideWidth - Margin - 1828800, SlideHeight - 548640, 1828800, 320040, "ctr",
                "<a:p><a:pPr algn=\"r\"/>" + Run(new AnswerSpan($"{number} / {total}", AnswerSpanStyle.None), 1100, "8C959F") + "</a:p>"));

            return SlideXml(shapes.ToString());
        }

        public string Relationships()
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

            return builder.Append("</Relationships>").ToString();
        }

        private static string SlideXml(string shapes) =>
            XmlDeclaration +
            "<p:sld " + Namespaces + ">" +
            "<p:cSld><p:bg><p:bgPr><a:solidFill><a:srgbClr val=\"FFFFFF\"/></a:solidFill><a:effectLst/></p:bgPr></p:bg>" +
            "<p:spTree><p:nvGrpSpPr><p:cNvPr id=\"1\" name=\"\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>" +
            "<p:grpSpPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"0\" cy=\"0\"/><a:chOff x=\"0\" y=\"0\"/><a:chExt cx=\"0\" cy=\"0\"/></a:xfrm></p:grpSpPr>" +
            shapes + "</p:spTree></p:cSld><p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr></p:sld>";

        private string Rectangle(long x, long y, long width, long height, string color) =>
            "<p:sp><p:nvSpPr><p:cNvPr id=\"" + NextId() + "\" name=\"Adorno\"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>" +
            "<p:spPr>" + Transform(x, y, width, height) + "<a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom>" +
            "<a:solidFill><a:srgbClr val=\"" + color + "\"/></a:solidFill><a:ln><a:noFill/></a:ln></p:spPr></p:sp>";

        private string TextBox(long x, long y, long width, long height, string anchor, string paragraphs, bool autofit = false) =>
            "<p:sp><p:nvSpPr><p:cNvPr id=\"" + NextId() + "\" name=\"Texto\"/><p:cNvSpPr txBox=\"1\"/><p:nvPr/></p:nvSpPr>" +
            "<p:spPr>" + Transform(x, y, width, height) + "<a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom><a:noFill/></p:spPr>" +
            "<p:txBody><a:bodyPr wrap=\"square\" lIns=\"0\" tIns=\"0\" rIns=\"0\" bIns=\"0\" anchor=\"" + anchor + "\">" +
            (autofit ? "<a:normAutofit/>" : "<a:noAutofit/>") + "</a:bodyPr><a:lstStyle/>" +
            (paragraphs.Length == 0 ? "<a:p><a:endParaRPr lang=\"es-ES\"/></a:p>" : paragraphs) + "</p:txBody></p:sp>";

        private static string Transform(long x, long y, long width, long height) =>
            "<a:xfrm><a:off x=\"" + x + "\" y=\"" + y + "\"/><a:ext cx=\"" + width + "\" cy=\"" + height + "\"/></a:xfrm>";

        private string Paragraph(IReadOnlyList<AnswerSpan> spans, int size, string color, (int? Number, int Depth)? bullet)
        {
            var builder = new StringBuilder("<a:p>");
            if (bullet is { } list)
            {
                var indent = 342900 + (list.Depth * 342900);
                builder.Append("<a:pPr marL=\"").Append(indent).Append("\" indent=\"-285750\">")
                    .Append("<a:spcBef><a:spcPts val=\"").Append(list.Depth == 0 ? 900 : 300).Append("\"/></a:spcBef>")
                    .Append("<a:buClr><a:srgbClr val=\"").Append(Accent).Append("\"/></a:buClr>");
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
                builder.Append("<a:pPr marL=\"0\" indent=\"0\"><a:spcBef><a:spcPts val=\"600\"/></a:spcBef><a:buNone/></a:pPr>");
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

            builder.Append(" dirty=\"0\"><a:solidFill><a:srgbClr val=\"").Append(span.Url is not null ? "0B5CAD" : color).Append("\"/></a:solidFill>");
            var font = span.Style.HasFlag(AnswerSpanStyle.Code) ? "Consolas" : "Calibri";
            builder.Append("<a:latin typeface=\"").Append(font).Append("\"/><a:cs typeface=\"").Append(font).Append("\"/>");

            if (span.Url is { } url)
            {
                _links.Add(url);
                builder.Append("<a:hlinkClick r:id=\"link").Append(_links.Count).Append("\"/>");
            }

            return builder.Append("</a:rPr><a:t>").Append(Escape(span.Text)).Append("</a:t></a:r>").ToString();
        }

        private string Table(AnswerTable table, long x, long y, long width)
        {
            var columns = Math.Max(table.Header.Count, table.Rows.Count == 0 ? 0 : table.Rows.Max(row => row.Count));
            if (columns == 0)
            {
                return string.Empty;
            }

            const long rowHeight = 457200;
            var rows = Math.Min(table.Rows.Count, 9);
            var columnWidth = width / columns;
            var builder = new StringBuilder()
                .Append("<p:graphicFrame><p:nvGraphicFramePr><p:cNvPr id=\"").Append(NextId()).Append("\" name=\"Tabla\"/>")
                .Append("<p:cNvGraphicFramePr><a:graphicFrameLocks noGrp=\"1\"/></p:cNvGraphicFramePr><p:nvPr/></p:nvGraphicFramePr>")
                .Append("<p:xfrm><a:off x=\"").Append(x).Append("\" y=\"").Append(y).Append("\"/><a:ext cx=\"").Append(columnWidth * columns)
                .Append("\" cy=\"").Append(rowHeight * (rows + 1)).Append("\"/></p:xfrm>")
                .Append("<a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/table\"><a:tbl><a:tblPr firstRow=\"1\" bandRow=\"1\"/><a:tblGrid>");
            for (var i = 0; i < columns; i++)
            {
                builder.Append("<a:gridCol w=\"").Append(columnWidth).Append("\"/>");
            }

            builder.Append("</a:tblGrid>");
            TableRow(builder, table.Header, columns, rowHeight, header: true, shaded: false);
            for (var i = 0; i < rows; i++)
            {
                TableRow(builder, table.Rows[i], columns, rowHeight, header: false, shaded: i % 2 == 1);
            }

            return builder.Append("</a:tbl></a:graphicData></a:graphic></p:graphicFrame>").ToString();
        }

        private void TableRow(StringBuilder builder, IReadOnlyList<IReadOnlyList<AnswerSpan>> cells, int columns, long height, bool header, bool shaded)
        {
            builder.Append("<a:tr h=\"").Append(height).Append("\">");
            for (var i = 0; i < columns; i++)
            {
                var spans = i < cells.Count ? cells[i] : [];
                builder.Append("<a:tc><a:txBody><a:bodyPr/><a:lstStyle/><a:p>");
                foreach (var span in spans)
                {
                    builder.Append(Run(header ? span with { Style = span.Style | AnswerSpanStyle.Bold } : span, 1400, header ? "FFFFFF" : Ink));
                }

                builder.Append("<a:endParaRPr lang=\"es-ES\" sz=\"1400\"/></a:p></a:txBody>")
                    .Append("<a:tcPr marL=\"91440\" marR=\"91440\" marT=\"45720\" marB=\"45720\" anchor=\"ctr\">");
                foreach (var side in new[] { "lnL", "lnR", "lnT", "lnB" })
                {
                    builder.Append("<a:").Append(side).Append(" w=\"6350\"><a:solidFill><a:srgbClr val=\"D0D7DE\"/></a:solidFill></a:").Append(side).Append('>');
                }

                var fill = header ? Navy : shaded ? "F3F5F8" : "FFFFFF";
                builder.Append("<a:solidFill><a:srgbClr val=\"").Append(fill).Append("\"/></a:solidFill></a:tcPr></a:tc>");
            }

            builder.Append("</a:tr>");
        }
    }

    private static string Presentation(int slideCount)
    {
        var builder = new StringBuilder(XmlDeclaration)
            .Append("<p:presentation ").Append(Namespaces).Append(" saveSubsetFonts=\"1\">")
            .Append("<p:sldMasterIdLst><p:sldMasterId id=\"2147483648\" r:id=\"rId1\"/></p:sldMasterIdLst><p:sldIdLst>");
        for (var i = 0; i < slideCount; i++)
        {
            builder.Append("<p:sldId id=\"").Append(256 + i).Append("\" r:id=\"rId").Append(i + 3).Append("\"/>");
        }

        return builder.Append("</p:sldIdLst><p:sldSz cx=\"").Append(SlideWidth).Append("\" cy=\"").Append(SlideHeight)
            .Append("\"/><p:notesSz cx=\"6858000\" cy=\"9144000\"/><p:defaultTextStyle/></p:presentation>").ToString();
    }

    private static string PresentationRelationships(int slideCount)
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

        return builder.Append("</Relationships>").ToString();
    }

    private static string ContentTypes(int slideCount)
    {
        var builder = new StringBuilder(XmlDeclaration)
            .Append("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">")
            .Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>")
            .Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>")
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

    private const string SlideLayout =
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
        "<a:accent1><a:srgbClr val=\"8E3B62\"/></a:accent1><a:accent2><a:srgbClr val=\"1F3A5F\"/></a:accent2>" +
        "<a:accent3><a:srgbClr val=\"2E5A88\"/></a:accent3><a:accent4><a:srgbClr val=\"C06C8E\"/></a:accent4>" +
        "<a:accent5><a:srgbClr val=\"57606A\"/></a:accent5><a:accent6><a:srgbClr val=\"D0D7DE\"/></a:accent6>" +
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
