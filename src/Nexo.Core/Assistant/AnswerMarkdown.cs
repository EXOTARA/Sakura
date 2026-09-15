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

/// <summary>
/// Un trozo de texto con su estilo. <see cref="Url"/> lleva la dirección cuando el trozo es un enlace;
/// solo se aceptan direcciones http y https (ver <see cref="AnswerMarkdown.SafeWebUrl"/>).
/// </summary>
public readonly record struct AnswerSpan(string Text, AnswerSpanStyle Style, string? Url = null);

public abstract record AnswerBlock;

/// <summary>Un párrafo. Conserva los saltos de línea que el modelo puso dentro.</summary>
public sealed record AnswerParagraph(IReadOnlyList<AnswerSpan> Spans) : AnswerBlock;

/// <summary>Un título. <see cref="Level"/> es el número de almohadillas (1 a 6).</summary>
public sealed record AnswerHeading(IReadOnlyList<AnswerSpan> Spans, int Level = 2) : AnswerBlock;

/// <summary>Un punto de lista. <see cref="Number"/> es nulo en las listas con viñetas.</summary>
public sealed record AnswerListItem(IReadOnlyList<AnswerSpan> Spans, int? Number, int Depth) : AnswerBlock;

/// <summary>Una tabla: cabecera y filas, cada celda con sus trozos.</summary>
public sealed record AnswerTable(
    IReadOnlyList<IReadOnlyList<AnswerSpan>> Header,
    IReadOnlyList<IReadOnlyList<IReadOnlyList<AnswerSpan>>> Rows) : AnswerBlock;

public sealed record AnswerCode(string Text) : AnswerBlock;

/// <summary>Una cita o un aviso destacado («> …»).</summary>
public sealed record AnswerQuote(IReadOnlyList<AnswerSpan> Spans) : AnswerBlock;

/// <summary>
/// Lee el Markdown con el que escriben los modelos para que el chat lo dibuje y no lo enseñe crudo
/// (Adler, 2026-09-14, con una captura: una tabla de ratones salía como filas de barras y guiones, y
/// las negritas con los asteriscos a la vista).
///
/// Solo lo que los modelos usan de verdad en una respuesta de chat: párrafos, títulos, negrita,
/// cursiva, código, listas con viñetas o números, tablas, bloques de código y enlaces. Lo que no se
/// reconoce se enseña tal cual: un asterisco suelto sigue siendo un
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

            // 2026-09-15 — una cita («> Consejo: …») se dibuja como cita, sin el signo.
            if (trimmed.StartsWith('>'))
            {
                FlushParagraph();
                var quote = new StringBuilder(trimmed.TrimStart('>').Trim());
                while (i + 1 < lines.Length && lines[i + 1].Trim().StartsWith('>'))
                {
                    i++;
                    quote.Append('\n').Append(lines[i].Trim().TrimStart('>').Trim());
                }

                blocks.Add(new AnswerQuote(ParseInline(quote.ToString())));
                continue;
            }

            var heading = Heading().Match(trimmed);
            if (heading.Success)
            {
                FlushParagraph();
                blocks.Add(new AnswerHeading(
                    ParseInline(heading.Groups["text"].Value.Trim()), heading.Groups["hashes"].Value.Length));
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

        // 2026-09-15 — los enlaces se pueden pulsar (Adler pidió «pásame los links» y salían como
        // texto entre < >). Tres formas: [texto](url), <https://…> y una dirección suelta.
        bool TryAddLink(string display, string url, int length)
        {
            if (SafeWebUrl(url) is not { } safe)
            {
                return false;
            }

            FlushPlain();
            spans.Add(new AnswerSpan(display, AnswerSpanStyle.None, safe));
            i += length;
            return true;
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
                    // Muchos modelos ponen la propia dirección como texto: se enseña acortada.
                    var display = link.Groups["text"].Value;
                    if (display.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        display = LinkDisplay(display);
                    }

                    if (!TryAddLink(display, link.Groups["url"].Value, link.Length))
                    {
                        // Una dirección que no es web se queda en su texto, sin enlace.
                        plain.Append(link.Groups["text"].Value);
                        i += link.Length;
                    }

                    continue;
                }
            }

            if (text[i] == '<')
            {
                var close = text.IndexOf('>', i + 1);
                if (close > i + 1 &&
                    TryAddLink(LinkDisplay(text[(i + 1)..close]), text[(i + 1)..close], close - i + 1))
                {
                    continue;
                }
            }

            if ((text[i] == 'h' || text[i] == 'H') && (i == 0 || !char.IsLetterOrDigit(text[i - 1])))
            {
                var bare = BareUrl().Match(text, i);
                if (bare.Success && bare.Index == i)
                {
                    var url = TrimTrailingPunctuation(bare.Value);
                    if (TryAddLink(LinkDisplay(url), url, url.Length))
                    {
                        continue;
                    }
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

            if (merged.Count > 0 && merged[^1].Style == span.Style && merged[^1].Url is null && span.Url is null)
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

    /// <summary>
    /// La dirección si es una web (http o https) bien formada; nulo en cualquier otro caso. Un enlace
    /// que escribe un modelo no debe poder abrir un archivo del equipo ni ejecutar nada.
    /// </summary>
    public static string? SafeWebUrl(string? url)
    {
        var candidate = url?.Trim();
        if (string.IsNullOrEmpty(candidate) ||
            !Uri.TryCreate(candidate, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrEmpty(uri.Host))
        {
            return null;
        }

        return uri.AbsoluteUri;
    }

    /// <summary>
    /// Lo que se enseña de una dirección suelta: el sitio y el camino, sin «https://» ni «www.», y
    /// acortado si es largo. La dirección entera sigue en el enlace y en su ayuda emergente.
    /// </summary>
    public static string LinkDisplay(string url, int maximum = 48)
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host))
        {
            return url;
        }

        var host = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host;
        var path = uri.PathAndQuery == "/" ? string.Empty : uri.PathAndQuery.TrimEnd('/');
        var display = host + path;
        return display.Length <= maximum ? display : display[..(maximum - 1)] + "…";
    }

    /// <summary>
    /// Mientras una respuesta llega, cierra lo que el modelo aún no ha cerrado en su último trozo —una
    /// negrita, un código o un bloque de código a medias— para dibujarla ya con formato sin que se vean
    /// los asteriscos hasta que llegue el cierre. Solo para enseñar; el texto guardado no se toca.
    /// </summary>
    public static string CloseDanglingMarks(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        if (CountOccurrences(text, "```") % 2 == 1)
        {
            return text.EndsWith('\n') ? text + "```" : text + "\n```";
        }

        // Un enlace a medio escribir —«[texto](https://ejem» o «<https://ejem»— se guarda hasta que
        // llegue entero: a medias se verían los corchetes y media dirección.
        var lineStart = text.LastIndexOf('\n') + 1;
        var openLink = text.LastIndexOf('[');
        if (openLink >= lineStart)
        {
            var rest = text[openLink..];
            var closedLabel = rest.IndexOf("](", StringComparison.Ordinal);
            if (!rest.Contains(']') || (closedLabel >= 0 && rest.IndexOf(')', closedLabel) < 0))
            {
                text = text[..openLink];
            }
        }

        var openAngle = text.LastIndexOf('<');
        if (openAngle >= lineStart && text.IndexOf('>', openAngle) < 0 &&
            "<https".StartsWith(text[openAngle..Math.Min(text.Length, openAngle + 6)], StringComparison.OrdinalIgnoreCase))
        {
            text = text[..openAngle];
        }

        var lastLine = text[lineStart..];
        var beforeCode = lastLine;
        var suffix = string.Empty;

        if (lastLine.Count(c => c == '`') % 2 == 1)
        {
            suffix = "`";
            beforeCode = lastLine[..lastLine.LastIndexOf('`')];
        }

        if (CountOccurrences(beforeCode, "**") % 2 == 1)
        {
            // Un «**» recién abierto, sin nada detrás todavía, no es negrita de nada: se esconde.
            if (suffix.Length == 0 && lastLine.EndsWith("**", StringComparison.Ordinal))
            {
                return text[..^2];
            }

            suffix = "**" + suffix;
        }

        return text + suffix;
    }

    private static int CountOccurrences(string text, string marker)
    {
        var count = 0;
        for (var index = text.IndexOf(marker, StringComparison.Ordinal);
             index >= 0;
             index = text.IndexOf(marker, index + marker.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    private static string TrimTrailingPunctuation(string url)
    {
        var end = url.Length;
        while (end > 0 && ".,;:!?\"'".Contains(url[end - 1]))
        {
            end--;
        }

        // Un paréntesis de cierre es parte de la dirección solo si abrió uno dentro de ella.
        while (end > 0 && url[end - 1] == ')' && url[..end].Count(c => c == '(') < url[..end].Count(c => c == ')'))
        {
            end--;
        }

        return url[..end];
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

    [GeneratedRegex(@"^(?<hashes>#{1,6})\s+(?<text>.+)$")]
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

    [GeneratedRegex(@"https?://[^\s<>\[\]`*|]+", RegexOptions.IgnoreCase)]
    private static partial Regex BareUrl();
}
