using System.Text;
using System.Xml;

namespace Nexo.Core.Documents;

/// <summary>
/// 2026-09-16 — dibuja una fórmula en el formato del editor de ecuaciones de Office (OMML).
///
/// No es una imagen ni texto con superíndices falsos: es la misma ecuación que sale al escribirla a
/// mano en Word, así que se puede abrir, corregir y volver a guardar. Word no entiende LaTeX dentro
/// de un archivo —solo mientras se teclea—, de modo que la conversión la hace Sakura
/// (<see cref="MathNotation"/> la entiende, esto la escribe).
///
/// Cada trozo lleva la fuente Cambria Math, que es lo que hace que una fracción se vea como una
/// fracción: sin ella Word dibuja la estructura, pero con la letra del párrafo y se nota.
/// </summary>
public static class OfficeMath
{
    public const string Namespace = "http://schemas.openxmlformats.org/officeDocument/2006/math";

    /// <summary>La ecuación dentro de una línea de texto.</summary>
    public static string Inline(string formula) => "<m:oMath>" + Body(formula) + "</m:oMath>";

    /// <summary>La ecuación en su propio renglón, centrada, como se escriben las fórmulas importantes.</summary>
    public static string Display(string formula) =>
        "<m:oMathPara><m:oMathParaPr><m:jc m:val=\"center\"/></m:oMathParaPr><m:oMath>" +
        Body(formula) +
        "</m:oMath></m:oMathPara>";

    /// <summary>Si la fórmula se entendió entera; si no, se dibuja igual, pero conviene avisar.</summary>
    public static bool CanConvert(string formula) => MathNotation.TryParse(formula, out _);

    private static string Body(string formula)
    {
        MathNotation.TryParse(formula, out var node);
        var builder = new StringBuilder();
        Write(builder, node);
        return builder.ToString();
    }

    private static void Write(StringBuilder builder, MathNode node)
    {
        switch (node)
        {
            case MathRun run:
                Run(builder, run.Text, run.Upright);
                break;

            case MathSequence sequence:
                foreach (var part in sequence.Parts)
                {
                    Write(builder, part);
                }

                break;

            case MathScript { Sub: not null, Sup: not null } both:
                builder.Append("<m:sSubSup>");
                Element(builder, "m:e", both.Base);
                Element(builder, "m:sub", both.Sub);
                Element(builder, "m:sup", both.Sup);
                builder.Append("</m:sSubSup>");
                break;

            case MathScript { Sup: not null } superscript:
                builder.Append("<m:sSup>");
                Element(builder, "m:e", superscript.Base);
                Element(builder, "m:sup", superscript.Sup);
                builder.Append("</m:sSup>");
                break;

            case MathScript { Sub: not null } subscript:
                builder.Append("<m:sSub>");
                Element(builder, "m:e", subscript.Base);
                Element(builder, "m:sub", subscript.Sub);
                builder.Append("</m:sSub>");
                break;

            case MathScript plain:
                Write(builder, plain.Base);
                break;

            case MathFraction fraction:
                builder.Append("<m:f><m:fPr><m:type m:val=\"bar\"/></m:fPr>");
                Element(builder, "m:num", fraction.Numerator);
                Element(builder, "m:den", fraction.Denominator);
                builder.Append("</m:f>");
                break;

            case MathRadical radical:
                builder.Append("<m:rad><m:radPr>")
                    .Append(radical.Degree is null ? "<m:degHide m:val=\"1\"/>" : string.Empty)
                    .Append("</m:radPr>");
                if (radical.Degree is { } degree)
                {
                    Element(builder, "m:deg", degree);
                }
                else
                {
                    builder.Append("<m:deg/>");
                }

                Element(builder, "m:e", radical.Radicand);
                builder.Append("</m:rad>");
                break;

            case MathDelimited delimited:
                builder.Append("<m:d><m:dPr><m:begChr m:val=\"").Append(Attribute(delimited.Open))
                    .Append("\"/><m:endChr m:val=\"").Append(Attribute(delimited.Close))
                    .Append("\"/></m:dPr>");
                Element(builder, "m:e", delimited.Content);
                builder.Append("</m:d>");
                break;

            case MathNAry nary:
                // La sumatoria pone los límites arriba y abajo; la integral, al lado.
                var stacked = nary.Operator is "∑" or "∏";
                builder.Append("<m:nary><m:naryPr><m:chr m:val=\"").Append(Attribute(nary.Operator))
                    .Append("\"/><m:limLoc m:val=\"").Append(stacked ? "undOvr" : "subSup").Append("\"/>")
                    .Append(nary.Lower is null ? "<m:subHide m:val=\"1\"/>" : string.Empty)
                    .Append(nary.Upper is null ? "<m:supHide m:val=\"1\"/>" : string.Empty)
                    .Append("</m:naryPr>");
                Element(builder, "m:sub", nary.Lower);
                Element(builder, "m:sup", nary.Upper);
                Element(builder, "m:e", nary.Body);
                builder.Append("</m:nary>");
                break;

            case MathFunction function:
                builder.Append("<m:func><m:fName>");
                if (function.Under is { } under)
                {
                    // «lím» con su condición debajo, como se escribe un límite.
                    builder.Append("<m:limLow><m:limLowPr/><m:e>");
                    Run(builder, function.Name, upright: true);
                    builder.Append("</m:e>");
                    Element(builder, "m:lim", under);
                    builder.Append("</m:limLow>");
                }
                else
                {
                    Run(builder, function.Name, upright: true);
                }

                builder.Append("</m:fName>");
                Element(builder, "m:e", function.Argument);
                builder.Append("</m:func>");
                break;
        }
    }

    private static void Element(StringBuilder builder, string name, MathNode? node)
    {
        builder.Append('<').Append(name).Append('>');
        if (node is not null)
        {
            Write(builder, node);
        }

        builder.Append("</").Append(name).Append('>');
    }

    private static void Run(StringBuilder builder, string text, bool upright)
    {
        if (text.Length == 0)
        {
            return;
        }

        builder.Append("<m:r>");
        if (upright)
        {
            builder.Append("<m:rPr><m:sty m:val=\"p\"/></m:rPr>");
        }

        builder.Append("<w:rPr><w:rFonts w:ascii=\"Cambria Math\" w:hAnsi=\"Cambria Math\"/></w:rPr>")
            .Append("<m:t xml:space=\"preserve\">").Append(Escape(text)).Append("</m:t></m:r>");
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

    private static string Attribute(string text) => Escape(text).Replace("\"", "&quot;");
}
