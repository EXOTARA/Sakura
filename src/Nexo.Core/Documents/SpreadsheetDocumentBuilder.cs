using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Nexo.Core.Assistant;

namespace Nexo.Core.Documents;

/// <summary>
/// 2026-09-15 — una respuesta como hoja de Excel (.xlsx), sin necesitar Excel instalado (Adler: «lo
/// mismo en Excel, PowerPoint…»). Igual que el Word, sale de la misma lectura del Markdown que dibuja
/// el chat.
///
/// Cada tabla de la respuesta es una hoja, con el nombre del título que la precede, la cabecera en
/// negrita sobre fondo oscuro fijada al desplazarse, filtros y columnas del ancho de su contenido. Las
/// cifras se guardan como números —y los porcentajes como porcentajes— para que se puedan sumar y
/// ordenar. Una respuesta sin tablas se guarda en una hoja con una fila por apartado, párrafo o punto.
/// </summary>
public static partial class SpreadsheetDocumentBuilder
{
    private sealed record Sheet(string Name, IReadOnlyList<IReadOnlyList<string>> Rows, bool HasHeader);

    public static byte[] BuildFromMarkdown(string title, string markdown)
    {
        var blocks = AnswerMarkdown.Parse(markdown);
        var sheets = new List<Sheet>();
        string? lastHeading = null;

        foreach (var block in blocks)
        {
            switch (block)
            {
                case AnswerHeading heading:
                    lastHeading = AnswerMarkdown.ToPlainText(heading.Spans).Trim();
                    break;
                case AnswerTable table:
                    var rows = new List<IReadOnlyList<string>>
                    {
                        table.Header.Select(cell => AnswerMarkdown.ToPlainText(cell).Trim()).ToList()
                    };
                    rows.AddRange(table.Rows.Select(row => (IReadOnlyList<string>)row.Select(cell => AnswerMarkdown.ToPlainText(cell).Trim()).ToList()));
                    sheets.Add(new Sheet(lastHeading ?? $"Tabla {sheets.Count + 1}", rows, HasHeader: true));
                    break;
            }
        }

        if (sheets.Count == 0)
        {
            sheets.Add(ContentSheet(blocks));
        }

        return Package(title, UniqueNames(sheets));
    }

    /// <summary>Sin tablas: una fila por pieza, con el apartado al que pertenece y la sangría de la lista.</summary>
    private static Sheet ContentSheet(IReadOnlyList<AnswerBlock> blocks)
    {
        var rows = new List<IReadOnlyList<string>> { new[] { "Apartado", "Contenido" } };
        var section = string.Empty;
        foreach (var block in blocks)
        {
            switch (block)
            {
                case AnswerHeading heading:
                    section = AnswerMarkdown.ToPlainText(heading.Spans).Trim();
                    break;
                case AnswerListItem item:
                    var marker = item.Number is { } number ? $"{number}. " : "• ";
                    rows.Add(new[] { section, new string(' ', item.Depth * 4) + marker + AnswerMarkdown.ToPlainText(item.Spans).Trim() });
                    break;
                case AnswerParagraph paragraph:
                    rows.Add(new[] { section, AnswerMarkdown.ToPlainText(paragraph.Spans).Trim() });
                    break;
                case AnswerQuote quote:
                    rows.Add(new[] { section, AnswerMarkdown.ToPlainText(quote.Spans).Trim() });
                    break;
                case AnswerCode code:
                    rows.Add(new[] { section, code.Text });
                    break;
            }
        }

        return new Sheet("Contenido", rows, HasHeader: true);
    }

    /// <summary>
    /// Excel exige nombres de hoja de hasta 31 caracteres, sin <c>: \ / ? * [ ]</c> y distintos entre sí
    /// sin mirar mayúsculas.
    /// </summary>
    private static List<Sheet> UniqueNames(List<Sheet> sheets)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<Sheet>();
        foreach (var sheet in sheets)
        {
            var clean = InvalidSheetCharacters().Replace(sheet.Name, " ").Trim().Trim('\'');
            if (clean.Length == 0)
            {
                clean = "Hoja";
            }

            clean = clean.Length > 31 ? clean[..31].TrimEnd() : clean;
            var name = clean;
            for (var number = 2; !used.Add(name); number++)
            {
                var suffix = $" ({number})";
                name = (clean.Length + suffix.Length > 31 ? clean[..(31 - suffix.Length)] : clean) + suffix;
            }

            result.Add(sheet with { Name = name });
        }

        return result;
    }

    private enum CellKind { Text, Number, Percent }

    /// <summary>
    /// Qué es una celda: un número («12», «1.234,5», «-3.5»), un porcentaje («10 %», «+2%») o texto.
    /// Se es prudente: «2-3», «12 h» o «+10 % aprox» se quedan como texto, porque convertirlos cambiaría
    /// lo que dicen.
    /// </summary>
    public static (object Value, bool IsPercent)? ParseNumber(string text)
    {
        var value = text.Trim().Replace(' ', ' ');
        var percent = PercentValue().Match(value);
        if (percent.Success && TryNumber(percent.Groups["n"].Value, out var ratio))
        {
            return (ratio / 100d, true);
        }

        return NumberValue().IsMatch(value) && TryNumber(value, out var number) ? (number, false) : null;
    }

    private static bool TryNumber(string text, out double number)
    {
        var clean = text.Replace(" ", string.Empty).TrimStart('+');

        // Con coma y punto, el último es el decimal («1.234,5» o «1,234.5»); con solo uno de los dos y
        // tres cifras detrás, es separador de miles («1.234», «1,234»).
        var lastComma = clean.LastIndexOf(',');
        var lastDot = clean.LastIndexOf('.');
        if (lastComma >= 0 && lastDot >= 0)
        {
            var decimalMark = lastComma > lastDot ? ',' : '.';
            var thousands = decimalMark == ',' ? "." : ",";
            clean = clean.Replace(thousands, string.Empty).Replace(decimalMark, '.');
        }
        else if (lastComma >= 0 || lastDot >= 0)
        {
            var mark = lastComma >= 0 ? ',' : '.';
            var digitsAfter = clean.Length - clean.LastIndexOf(mark) - 1;
            clean = digitsAfter == 3 && clean.Count(c => c == mark) >= 1 && clean.IndexOf(mark) > 0 && !clean.StartsWith('0')
                ? clean.Replace(mark.ToString(), string.Empty)
                : clean.Replace(mark, '.');
        }

        return double.TryParse(clean, NumberStyles.Float, CultureInfo.InvariantCulture, out number);
    }

    private static byte[] Package(string title, IReadOnlyList<Sheet> sheets)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(archive, "[Content_Types].xml", ContentTypes(sheets.Count));
            Write(archive, "_rels/.rels", RootRelationships);
            Write(archive, "docProps/core.xml", CoreProperties(title));
            Write(archive, "xl/workbook.xml", Workbook(sheets));
            Write(archive, "xl/_rels/workbook.xml.rels", WorkbookRelationships(sheets.Count));
            Write(archive, "xl/styles.xml", Styles);
            for (var i = 0; i < sheets.Count; i++)
            {
                Write(archive, $"xl/worksheets/sheet{i + 1}.xml", Worksheet(sheets[i]));
            }
        }

        return buffer.ToArray();
    }

    private static string Worksheet(Sheet sheet)
    {
        var columns = sheet.Rows.Count == 0 ? 1 : Math.Max(1, sheet.Rows.Max(row => row.Count));
        var builder = new StringBuilder(XmlDeclaration)
            .Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" ")
            .Append("xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">");

        if (sheet.HasHeader && sheet.Rows.Count > 1)
        {
            builder.Append("<sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews>");
        }

        builder.Append("<cols>");
        for (var column = 0; column < columns; column++)
        {
            var longest = sheet.Rows.Select(row => column < row.Count ? row[column].Length : 0).DefaultIfEmpty(0).Max();
            var width = Math.Clamp(longest * 1.1 + 3, 10, 70);
            builder.Append("<col min=\"").Append(column + 1).Append("\" max=\"").Append(column + 1)
                .Append("\" width=\"").Append(width.ToString("0.#", CultureInfo.InvariantCulture)).Append("\" customWidth=\"1\"/>");
        }

        builder.Append("</cols><sheetData>");
        for (var rowIndex = 0; rowIndex < sheet.Rows.Count; rowIndex++)
        {
            var row = sheet.Rows[rowIndex];
            var header = sheet.HasHeader && rowIndex == 0;
            builder.Append("<row r=\"").Append(rowIndex + 1).Append("\">");
            for (var column = 0; column < columns; column++)
            {
                var reference = ColumnName(column) + (rowIndex + 1).ToString(CultureInfo.InvariantCulture);
                var text = column < row.Count ? row[column] : string.Empty;
                var number = header ? null : ParseNumber(text);

                if (number is { } parsed)
                {
                    builder.Append("<c r=\"").Append(reference).Append("\" s=\"").Append(parsed.IsPercent ? 3 : 2)
                        .Append("\"><v>").Append(((double)parsed.Value).ToString("R", CultureInfo.InvariantCulture)).Append("</v></c>");
                    continue;
                }

                builder.Append("<c r=\"").Append(reference).Append("\" t=\"inlineStr\" s=\"").Append(header ? 1 : 2)
                    .Append("\"><is><t xml:space=\"preserve\">").Append(Escape(text)).Append("</t></is></c>");
            }

            builder.Append("</row>");
        }

        builder.Append("</sheetData>");
        if (sheet.HasHeader && sheet.Rows.Count > 1)
        {
            builder.Append("<autoFilter ref=\"A1:").Append(ColumnName(columns - 1)).Append(sheet.Rows.Count).Append("\"/>");
        }

        builder.Append("<pageMargins left=\"0.6\" right=\"0.6\" top=\"0.75\" bottom=\"0.75\" header=\"0.3\" footer=\"0.3\"/></worksheet>");
        return builder.ToString();
    }

    private static string ColumnName(int index)
    {
        var name = string.Empty;
        for (index++; index > 0; index = (index - 1) / 26)
        {
            name = (char)('A' + ((index - 1) % 26)) + name;
        }

        return name;
    }

    private static string Workbook(IReadOnlyList<Sheet> sheets)
    {
        var builder = new StringBuilder(XmlDeclaration)
            .Append("<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" ")
            .Append("xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>");
        for (var i = 0; i < sheets.Count; i++)
        {
            builder.Append("<sheet name=\"").Append(EscapeAttribute(sheets[i].Name)).Append("\" sheetId=\"").Append(i + 1)
                .Append("\" r:id=\"rId").Append(i + 1).Append("\"/>");
        }

        builder.Append("</sheets>");
        var hasFilters = false;
        for (var i = 0; i < sheets.Count; i++)
        {
            if (sheets[i].HasHeader && sheets[i].Rows.Count > 1)
            {
                if (!hasFilters)
                {
                    builder.Append("<definedNames>");
                    hasFilters = true;
                }

                var columns = Math.Max(1, sheets[i].Rows.Max(row => row.Count));
                builder.Append("<definedName name=\"_xlnm._FilterDatabase\" localSheetId=\"").Append(i).Append("\" hidden=\"1\">'")
                    .Append(EscapeAttribute(sheets[i].Name.Replace("'", "''"))).Append("'!$A$1:$").Append(ColumnName(columns - 1)).Append('$')
                    .Append(sheets[i].Rows.Count).Append("</definedName>");
            }
        }

        if (hasFilters)
        {
            builder.Append("</definedNames>");
        }

        return builder.Append("</workbook>").ToString();
    }

    private static string WorkbookRelationships(int sheetCount)
    {
        var builder = new StringBuilder(XmlDeclaration)
            .Append("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
        for (var i = 0; i < sheetCount; i++)
        {
            builder.Append("<Relationship Id=\"rId").Append(i + 1)
                .Append("\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet")
                .Append(i + 1).Append(".xml\"/>");
        }

        builder.Append("<Relationship Id=\"rId").Append(sheetCount + 1)
            .Append("\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>");
        return builder.Append("</Relationships>").ToString();
    }

    private static string ContentTypes(int sheetCount)
    {
        var builder = new StringBuilder(XmlDeclaration)
            .Append("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">")
            .Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>")
            .Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>")
            .Append("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>")
            .Append("<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>")
            .Append("<Override PartName=\"/docProps/core.xml\" ContentType=\"application/vnd.openxmlformats-package.core-properties+xml\"/>");
        for (var i = 0; i < sheetCount; i++)
        {
            builder.Append("<Override PartName=\"/xl/worksheets/sheet").Append(i + 1)
                .Append(".xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
        }

        return builder.Append("</Types>").ToString();
    }

    private const string RootRelationships =
        XmlDeclaration +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
        "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties\" Target=\"docProps/core.xml\"/>" +
        "</Relationships>";

    /// <summary>
    /// Estilos de celda, por índice: 0 normal · 1 cabecera (negrita blanca sobre azul oscuro) · 2 dato
    /// con borde fino y ajuste de texto · 3 porcentaje con borde.
    /// </summary>
    private const string Styles =
        XmlDeclaration +
        "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
        "<fonts count=\"2\"><font><sz val=\"11\"/><color rgb=\"FF24292F\"/><name val=\"Calibri\"/></font>" +
        "<font><b/><sz val=\"11\"/><color rgb=\"FFFFFFFF\"/><name val=\"Calibri\"/></font></fonts>" +
        "<fills count=\"3\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill>" +
        "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF1F3A5F\"/><bgColor indexed=\"64\"/></patternFill></fill></fills>" +
        "<borders count=\"2\"><border><left/><right/><top/><bottom/><diagonal/></border>" +
        "<border><left style=\"thin\"><color rgb=\"FFD0D7DE\"/></left><right style=\"thin\"><color rgb=\"FFD0D7DE\"/></right>" +
        "<top style=\"thin\"><color rgb=\"FFD0D7DE\"/></top><bottom style=\"thin\"><color rgb=\"FFD0D7DE\"/></bottom><diagonal/></border></borders>" +
        "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
        "<cellXfs count=\"4\">" +
        "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
        "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment vertical=\"center\" wrapText=\"1\"/></xf>" +
        "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyBorder=\"1\" applyAlignment=\"1\"><alignment vertical=\"top\" wrapText=\"1\"/></xf>" +
        "<xf numFmtId=\"9\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment vertical=\"top\"/></xf>" +
        "</cellXfs><cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles></styleSheet>";

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

    private const string XmlDeclaration = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>";

    [GeneratedRegex(@"[:\\/?*\[\]]")]
    private static partial Regex InvalidSheetCharacters();

    [GeneratedRegex(@"^(?<n>[+-]?\d[\d.,]*)\s?%$")]
    private static partial Regex PercentValue();

    [GeneratedRegex(@"^[+-]?\d{1,3}([.,]\d{3})*([.,]\d+)?$|^[+-]?\d+([.,]\d+)?$")]
    private static partial Regex NumberValue();
}
