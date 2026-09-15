using System.Globalization;
using System.Text;
using System.Xml;

namespace Nexo.Core.Documents;

/// <summary>
/// 2026-09-15 — la parte de una gráfica nativa de Office (chart.xml), la misma para PowerPoint y Excel.
/// Es una gráfica de verdad y no un dibujo: se cambia de tipo, de colores o de datos desde el propio
/// programa. Las cifras apuntan a una hoja con la tabla tal cual (etiquetas en la columna A desde la
/// fila 2, cada serie en su columna) y además llevan una copia, para verse sin abrir la hoja.
/// </summary>
public static class ChartXml
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.drawingml.chart+xml";
    public const string RelationshipType = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/chart";
    public const string Namespace = "http://schemas.openxmlformats.org/drawingml/2006/chart";

    /// <summary>Colores de las series, en orden: azul, el color de Sakura, azul claro, rosa, gris y azul oscuro.</summary>
    private static readonly string[] Palette = ["2E5A88", "8E3B62", "7FA7C9", "C06C8E", "8C959F", "1F3A5F"];

    /// <param name="spec">La gráfica.</param>
    /// <param name="sheetName">La hoja donde está la tabla.</param>
    /// <param name="fontSize">Tamaño del texto en centésimas de punto (1400 en una diapositiva, 1000 en Excel).</param>
    /// <param name="workbookRelationshipId">En PowerPoint, la relación con la hoja incrustada; nulo en Excel.</param>
    public static string Build(ChartSpec spec, string sheetName, int fontSize, string? workbookRelationshipId)
    {
        var sheet = "'" + sheetName.Replace("'", "''") + "'!";
        var last = spec.Categories.Count + 1;
        var builder = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>")
            .Append("<c:chartSpace xmlns:c=\"").Append(Namespace).Append("\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" ")
            .Append("xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">")
            .Append("<c:date1904 val=\"0\"/><c:lang val=\"es-ES\"/><c:roundedCorners val=\"0\"/>")
            .Append("<c:chart><c:autoTitleDeleted val=\"1\"/><c:plotArea><c:layout/>");

        var format = NumberFormat(spec);
        switch (spec.Kind)
        {
            case ChartKind.Pie:
                builder.Append("<c:pieChart><c:varyColors val=\"1\"/>");
                Series(builder, spec, spec.Series[0], 0, sheet, last, format, fontSize);
                builder.Append("<c:firstSliceAng val=\"0\"/></c:pieChart>");
                break;
            case ChartKind.Line:
                builder.Append("<c:lineChart><c:grouping val=\"standard\"/><c:varyColors val=\"0\"/>");
                for (var i = 0; i < spec.Series.Count; i++)
                {
                    Series(builder, spec, spec.Series[i], i, sheet, last, format, fontSize);
                }

                builder.Append("<c:marker val=\"1\"/><c:axId val=\"50010\"/><c:axId val=\"50020\"/></c:lineChart>");
                break;
            default:
                builder.Append("<c:barChart><c:barDir val=\"").Append(spec.Kind == ChartKind.Bar ? "bar" : "col")
                    .Append("\"/><c:grouping val=\"clustered\"/><c:varyColors val=\"0\"/>");
                for (var i = 0; i < spec.Series.Count; i++)
                {
                    Series(builder, spec, spec.Series[i], i, sheet, last, format, fontSize);
                }

                builder.Append("<c:gapWidth val=\"").Append(spec.Series.Count == 1 ? 70 : 110)
                    .Append("\"/><c:overlap val=\"").Append(spec.Series.Count == 1 ? 0 : -8)
                    .Append("\"/><c:axId val=\"50010\"/><c:axId val=\"50020\"/></c:barChart>");
                break;
        }

        if (spec.Kind != ChartKind.Pie)
        {
            Axes(builder, spec, format, fontSize);
        }

        builder.Append("<c:spPr><a:noFill/><a:ln><a:noFill/></a:ln></c:spPr></c:plotArea>");
        if (spec.Kind == ChartKind.Pie || spec.Series.Count > 1)
        {
            builder.Append("<c:legend><c:legendPos val=\"").Append(spec.Kind == ChartKind.Pie ? "r" : "b").Append("\"/><c:overlay val=\"0\"/>")
                .Append(TextProperties(fontSize, "57606A")).Append("</c:legend>");
        }

        builder.Append("<c:plotVisOnly val=\"1\"/><c:dispBlanksAs val=\"gap\"/></c:chart>")
            .Append("<c:spPr><a:noFill/><a:ln><a:noFill/></a:ln></c:spPr>")
            .Append(TextProperties(fontSize, "57606A"));

        if (workbookRelationshipId is not null)
        {
            builder.Append("<c:externalData r:id=\"").Append(workbookRelationshipId).Append("\"><c:autoUpdate val=\"0\"/></c:externalData>");
        }

        return builder.Append("</c:chartSpace>").ToString();
    }

    /// <summary>
    /// Con los decimales que de verdad tienen las cifras, hasta dos: «8,6» no se enseña como «8,60» ni
    /// «7» como «7,».
    /// </summary>
    private static string NumberFormat(ChartSpec spec)
    {
        var values = spec.Series.SelectMany(series => series.Values).ToList();
        var scale = spec.IsPercent ? 100 : 1;
        var decimals = Enumerable.Range(0, 3).FirstOrDefault(places =>
            values.All(value => Math.Abs((value * scale * Math.Pow(10, places)) - Math.Round(value * scale * Math.Pow(10, places))) < 1e-6), 2);
        var fraction = decimals == 0 ? string.Empty : "." + new string('0', decimals);
        return spec.IsPercent ? "0" + fraction + "%" : "#,##0" + fraction;
    }

    private static void Series(StringBuilder builder, ChartSpec spec, ChartSeries series, int index, string sheet, int last, string format, int fontSize)
    {
        var column = ColumnName(series.Column);
        var color = Palette[index % Palette.Length];

        builder.Append("<c:ser><c:idx val=\"").Append(index).Append("\"/><c:order val=\"").Append(index).Append("\"/>")
            .Append("<c:tx><c:strRef><c:f>").Append(Escape(sheet)).Append('$').Append(column).Append("$1</c:f>")
            .Append("<c:strCache><c:ptCount val=\"1\"/><c:pt idx=\"0\"><c:v>").Append(Escape(series.Name)).Append("</c:v></c:pt></c:strCache></c:strRef></c:tx>");

        switch (spec.Kind)
        {
            case ChartKind.Line:
                builder.Append("<c:spPr><a:ln w=\"34925\" cap=\"rnd\"><a:solidFill><a:srgbClr val=\"").Append(color)
                    .Append("\"/></a:solidFill><a:round/></a:ln></c:spPr>")
                    .Append("<c:marker><c:symbol val=\"circle\"/><c:size val=\"7\"/><c:spPr><a:solidFill><a:srgbClr val=\"FFFFFF\"/></a:solidFill>")
                    .Append("<a:ln w=\"22225\"><a:solidFill><a:srgbClr val=\"").Append(color).Append("\"/></a:solidFill></a:ln></c:spPr></c:marker>");
                break;
            case ChartKind.Pie:
                builder.Append("<c:spPr><a:ln w=\"19050\"><a:solidFill><a:srgbClr val=\"FFFFFF\"/></a:solidFill></a:ln></c:spPr>");
                for (var point = 0; point < spec.Categories.Count; point++)
                {
                    builder.Append("<c:dPt><c:idx val=\"").Append(point).Append("\"/><c:bubble3D val=\"0\"/><c:spPr><a:solidFill><a:srgbClr val=\"")
                        .Append(Palette[point % Palette.Length]).Append("\"/></a:solidFill><a:ln w=\"19050\"><a:solidFill><a:srgbClr val=\"FFFFFF\"/></a:solidFill></a:ln></c:spPr></c:dPt>");
                }

                break;
            default:
                builder.Append("<c:spPr><a:solidFill><a:srgbClr val=\"").Append(color).Append("\"/></a:solidFill><a:ln><a:noFill/></a:ln></c:spPr>")
                    .Append("<c:invertIfNegative val=\"0\"/>");
                break;
        }

        if (spec.ShowValues)
        {
            var position = spec.Kind switch
            {
                ChartKind.Pie => "inEnd",
                ChartKind.Line => "t",
                _ => "outEnd"
            };
            var labelColor = spec.Kind == ChartKind.Pie ? "FFFFFF" : "24292F";
            builder.Append("<c:dLbls><c:numFmt formatCode=\"").Append(Escape(format)).Append("\" sourceLinked=\"0\"/>")
                .Append("<c:spPr><a:noFill/><a:ln><a:noFill/></a:ln></c:spPr>")
                .Append(TextProperties(fontSize, labelColor, bold: true))
                .Append("<c:dLblPos val=\"").Append(position).Append("\"/>")
                .Append("<c:showLegendKey val=\"0\"/><c:showVal val=\"1\"/><c:showCatName val=\"0\"/><c:showSerName val=\"0\"/>")
                .Append("<c:showPercent val=\"0\"/><c:showBubbleSize val=\"0\"/></c:dLbls>");
        }

        builder.Append("<c:cat><c:strRef><c:f>").Append(Escape(sheet)).Append("$A$2:$A$").Append(last).Append("</c:f>")
            .Append("<c:strCache><c:ptCount val=\"").Append(spec.Categories.Count).Append("\"/>");
        for (var i = 0; i < spec.Categories.Count; i++)
        {
            builder.Append("<c:pt idx=\"").Append(i).Append("\"><c:v>").Append(Escape(spec.Categories[i])).Append("</c:v></c:pt>");
        }

        builder.Append("</c:strCache></c:strRef></c:cat>")
            .Append("<c:val><c:numRef><c:f>").Append(Escape(sheet)).Append('$').Append(column).Append("$2:$").Append(column).Append('$').Append(last).Append("</c:f>")
            .Append("<c:numCache><c:formatCode>").Append(Escape(format)).Append("</c:formatCode><c:ptCount val=\"").Append(series.Values.Count).Append("\"/>");
        for (var i = 0; i < series.Values.Count; i++)
        {
            builder.Append("<c:pt idx=\"").Append(i).Append("\"><c:v>").Append(series.Values[i].ToString("R", CultureInfo.InvariantCulture)).Append("</c:v></c:pt>");
        }

        builder.Append("</c:numCache></c:numRef></c:val>");
        if (spec.Kind == ChartKind.Line)
        {
            builder.Append("<c:smooth val=\"0\"/>");
        }

        builder.Append("</c:ser>");
    }

    private static void Axes(StringBuilder builder, ChartSpec spec, string format, int fontSize)
    {
        var horizontal = spec.Kind == ChartKind.Bar;
        const string axisLine = "<c:spPr><a:ln w=\"9525\"><a:solidFill><a:srgbClr val=\"D0D7DE\"/></a:solidFill></a:ln></c:spPr>";

        // En barras horizontales la primera fila de la tabla va arriba, como se lee.
        builder.Append("<c:catAx><c:axId val=\"50010\"/><c:scaling><c:orientation val=\"").Append(horizontal ? "maxMin" : "minMax")
            .Append("\"/></c:scaling><c:delete val=\"0\"/><c:axPos val=\"").Append(horizontal ? "l" : "b").Append("\"/>")
            .Append("<c:numFmt formatCode=\"General\" sourceLinked=\"1\"/><c:majorTickMark val=\"none\"/><c:minorTickMark val=\"none\"/><c:tickLblPos val=\"nextTo\"/>")
            .Append(axisLine).Append(TextProperties(fontSize, "57606A"))
            .Append("<c:crossAx val=\"50020\"/><c:crosses val=\"autoZero\"/><c:auto val=\"1\"/><c:lblAlgn val=\"ctr\"/><c:lblOffset val=\"100\"/><c:noMultiLvlLbl val=\"0\"/></c:catAx>");

        // Con la cifra escrita en cada barra, el eje de valores y sus líneas sobran.
        var hideValues = spec.ShowValues && spec.Kind != ChartKind.Line;
        builder.Append("<c:valAx><c:axId val=\"50020\"/><c:scaling><c:orientation val=\"minMax\"/></c:scaling><c:delete val=\"")
            .Append(hideValues ? 1 : 0).Append("\"/><c:axPos val=\"").Append(horizontal ? "t" : "l").Append("\"/>")
            .Append(hideValues ? string.Empty : "<c:majorGridlines><c:spPr><a:ln w=\"9525\"><a:solidFill><a:srgbClr val=\"E6EAEE\"/></a:solidFill></a:ln></c:spPr></c:majorGridlines>")
            .Append("<c:numFmt formatCode=\"").Append(Escape(format)).Append("\" sourceLinked=\"0\"/><c:majorTickMark val=\"none\"/><c:minorTickMark val=\"none\"/><c:tickLblPos val=\"nextTo\"/>")
            .Append("<c:spPr><a:ln><a:noFill/></a:ln></c:spPr>").Append(TextProperties(fontSize, "8C959F"))
            .Append("<c:crossAx val=\"50010\"/><c:crosses val=\"").Append(horizontal ? "max" : "autoZero").Append("\"/><c:crossBetween val=\"between\"/></c:valAx>");
    }

    private static string TextProperties(int size, string color, bool bold = false) =>
        "<c:txPr><a:bodyPr/><a:lstStyle/><a:p><a:pPr><a:defRPr sz=\"" + size + "\"" + (bold ? " b=\"1\"" : " b=\"0\"") + ">" +
        "<a:solidFill><a:srgbClr val=\"" + color + "\"/></a:solidFill><a:latin typeface=\"Calibri\"/><a:cs typeface=\"Calibri\"/></a:defRPr></a:pPr>" +
        "<a:endParaRPr lang=\"es-ES\"/></a:p></c:txPr>";

    internal static string ColumnName(int index)
    {
        var name = string.Empty;
        for (index++; index > 0; index = (index - 1) / 26)
        {
            name = (char)('A' + ((index - 1) % 26)) + name;
        }

        return name;
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

        return clean.ToString().Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }
}
