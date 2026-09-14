using System.Text;
using System.Text.RegularExpressions;

namespace Nexo.Core.Assistant;

[Flags]
public enum AnswerSpanStyle
{
    None = 0,
    Bold = 1,
    Italic = 2,
    Code = 4
}

/// <summary>Un trozo de texto con su estilo.</summary>
public readonly record struct AnswerSpan(string Text, AnswerSpanStyle Style);

public abstract record AnswerBlock;

/// <summary>Un párrafo. Conserva los saltos de línea que el modelo puso dentro.</summary>
public sealed record AnswerParagraph(IReadOnlyList<AnswerSpan> Spans) : AnswerBlock;

public sealed record AnswerHeading(IReadOnlyList<AnswerSpan> Spans) : AnswerBlock;

/// <summary>Un punto de lista. <see cref="Number"/> es nulo en las listas con viñetas.</summary>
public sealed record AnswerListItem(IReadOnlyList<AnswerSpan> Spans, int? Number, int Depth) : AnswerBlock;

/// <summary>Una tabla: cabecera y filas, cada celda con sus trozos.</summary>
public sealed record AnswerTable(
    IReadOnlyList<IReadOnlyList<AnswerSpan>> Header,
    IReadOnlyList<IReadOnlyList<IReadOnlyList<AnswerSpan>>> Rows) : AnswerBlock;

public sealed record AnswerCode(string Text) : AnswerBlock;

/// <summary>
/// Lee el Markdown con el que escriben los modelos para que el chat lo dibuje y no lo enseñe crudo
/// (Adler, 2026-09-14, con una captura: una tabla de ratones salía como filas de barras y guiones, y
/// las negritas con los asteriscos a la vista).
///
/// Solo lo que los modelos usan de verdad en una respuesta de chat: párrafos, títulos, negrita,
/// cursiva, código, listas con viñetas o números, tablas y bloques de código. Los enlaces se quedan
/// en su texto. Lo que no se reconoce se enseña tal cual: un asterisco suelto sigue siendo un
/// asterisco, porque perder un carácter que el modelo escribió es peor que ver uno de más.
///
/// Está en Core porque lo que se equivoca es la lectura, no el dibujo, y así se prueba sin ventana.
/// </summary>
public static partial class AnswerMarkdown
{
    public static IReadOnlyList<AnswerBlock> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var lines = text.Replace("\r\n", "\n").Split('\n');
        var blocks = new List<AnswerBlock>();
        var paragraph = new StringBuilder();

        void FlushParagraph()
        {
            var content = paragraph.ToString().Trim('\n');
            paragraph.Clear();
            if (content.Trim().Length > 0)
            {
                blocks.Add(new AnswerParagraph(ParseInline(content)));
            }
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();

            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                FlushParagraph();
                var code = new StringBuilder();
                i++;
                while (i < lines.Length && !lines[i].Trim().StartsWith("```", StringComparison.Ordinal))
                {
                    code.AppendLine(lines[i]);
                    i++;
                }

                blocks.Add(new AnswerCode(code.ToString().TrimEnd('\n', '\r')));
                continue;
            }

            if (trimmed.Length == 0)
            {
                FlushParagraph();
                continue;
            }

            if (HorizontalRule().IsMatch(trimmed))
            {
                FlushParagraph();
                continue;
            }

            if (IsTableRow(trimmed) && i + 1 < lines.Length && IsTableSeparator(lines[i + 1].Trim()))
            {
                FlushParagraph();
                var header = SplitRow(trimmed).Select(ParseInline).ToList();
                var rows = new List<IReadOnlyList<IReadOnlyList<AnswerSpan>>>();
                i += 2;
                while (i < lines.Length && IsTableRow(lines[i].Trim()))
                {
                    rows.Add(SplitRow(lines[i].Trim()).Select(ParseInline).ToList());
                    i++;
                }

                i--;
                blocks.Add(new AnswerTable(header, rows));
                continue;
            }

            var heading = Heading().Match(trimmed);
            if (heading.Success)
            {
                FlushParagraph();
                blocks.Add(new AnswerHeading(ParseInline(heading.Groups["text"].Value.Trim())));
                continue;
            }

            var depth = (line.Length - line.TrimStart().Length) / 2;
            var bullet = Bullet().Match(trimmed);
            if (bullet.Success)
            {
                FlushParagraph();
                blocks.Add(new AnswerListItem(ParseInline(bullet.Groups["text"].Value), null, depth));
                continue;
            }

            var numbered = Numbered().Match(trimmed);
            if (numbered.Success)
            {
                FlushParagraph();
                blocks.Add(new AnswerListItem(
                    ParseInline(numbered.Groups["text"].Value),
                    int.Parse(numbered.Groups["n"].Value, System.Globalization.CultureInfo.InvariantCulture),
                    depth));
                continue;
            }

            if (paragraph.Length > 0)
            {
                paragraph.Append('\n');
            }

            paragraph.Append(trimmed);
        }

        FlushParagraph();
        return blocks;
    }

    /// <summary>Negrita, cursiva, código y enlaces dentro de una línea.</summary>
    public static IReadOnlyList<AnswerSpan> ParseInline(string text)
    {
        var spans = new List<AnswerSpan>();
        var plain = new StringBuilder();
        var i = 0;

        void FlushPlain(AnswerSpanStyle style = AnswerSpanStyle.None)
        {
            if (plain.Length > 0)
            {
                spans.Add(new AnswerSpan(plain.ToString(), style));
                plain.Clear();
            }
        }

        while (i < text.Length)
        {
            if (text[i] == '`')
            {
                var close = text.IndexOf('`', i + 1);
                if (close > i + 1)
                {
                    FlushPlain();
                    spans.Add(new AnswerSpan(text[(i + 1)..close], AnswerSpanStyle.Code));
                    i = close + 1;
                    continue;
                }
            }

            if (Starts(text, i, "**") || Starts(text, i, "__"))
            {
                var marker = text.Substring(i, 2);
                var close = text.IndexOf(marker, i + 2, StringComparison.Ordinal);
                if (close > i + 2)
                {
                    FlushPlain();
                    foreach (var inner in ParseInline(text[(i + 2)..close]))
                    {
                        spans.Add(inner with { Style = inner.Style | AnswerSpanStyle.Bold });
                    }

                    i = close + 2;
                    continue;
                }
            }

            // Cursiva solo con un asterisco pegado a letra por dentro: «*así*». Un asterisco con
            // espacio detrás es una multiplicación o una nota al pie, y se deja como está.
            if (text[i] == '*' && i + 1 < text.Length && !char.IsWhiteSpace(text[i + 1]) && text[i + 1] != '*')
            {
                var close = text.IndexOf('*', i + 1);
                if (close > i + 1 && !char.IsWhiteSpace(text[close - 1]))
                {
                    FlushPlain();
                    foreach (var inner in ParseInline(text[(i + 1)..close]))
                    {
                        spans.Add(inner with { Style = inner.Style | AnswerSpanStyle.Italic });
                    }

                    i = close + 1;
                    continue;
                }
            }

            if (text[i] == '[')
            {
                var link = Link().Match(text, i);
                if (link.Success && link.Index == i)
                {
                    plain.Append(link.Groups["text"].Value);
                    i += link.Length;
                    continue;
                }
            }

            plain.Append(text[i]);
            i++;
        }

        FlushPlain();
        return Merge(spans);
    }

    /// <summary>El texto sin marcas, para leerlo en voz alta o copiarlo limpio.</summary>
    public static string ToPlainText(IReadOnlyList<AnswerSpan> spans) =>
        string.Concat(spans.Select(span => span.Text));

    private static bool Starts(string text, int index, string marker) =>
        string.CompareOrdinal(text, index, marker, 0, marker.Length) == 0;

    private static List<AnswerSpan> Merge(List<AnswerSpan> spans)
    {
        var merged = new List<AnswerSpan>();
        foreach (var span in spans)
        {
            if (span.Text.Length == 0)
            {
                continue;
            }

            if (merged.Count > 0 && merged[^1].Style == span.Style)
            {
                merged[^1] = merged[^1] with { Text = merged[^1].Text + span.Text };
            }
            else
            {
                merged.Add(span);
            }
        }

        return merged;
    }

    private static bool IsTableRow(string line) =>
        line.Length > 1 && line[0] == '|' && line.Count(c => c == '|') >= 2;

    private static bool IsTableSeparator(string line) =>
        IsTableRow(line) && TableSeparator().IsMatch(line);

    private static List<string> SplitRow(string line)
    {
        var inner = line.Trim().Trim('|');
        return inner.Split('|').Select(cell => cell.Trim()).ToList();
    }

    [GeneratedRegex(@"^#{1,6}\s+(?<text>.+)$")]
    private static partial Regex Heading();

    [GeneratedRegex(@"^[-*+•]\s+(?<text>.+)$")]
    private static partial Regex Bullet();

    [GeneratedRegex(@"^(?<n>\d{1,3})[.)]\s+(?<text>.+)$")]
    private static partial Regex Numbered();

    [GeneratedRegex(@"^\|?\s*:?-{3,}:?\s*(\|\s*:?-{3,}:?\s*)*\|?$")]
    private static partial Regex TableSeparator();

    [GeneratedRegex(@"^(\*\s*){3,}$|^(-\s*){3,}$|^(_\s*){3,}$")]
    private static partial Regex HorizontalRule();

    [GeneratedRegex(@"\[(?<text>[^\]]+)\]\((?<url>[^)\s]+)\)")]
    private static partial Regex Link();
}
