using Nexo.Core.Assistant;

namespace Nexo.Core.Documents;

/// <summary>Una línea de una diapositiva: un párrafo o un punto de lista, con su sangría.</summary>
public sealed record SlideLine(IReadOnlyList<AnswerSpan> Spans, int Depth, int? Number, bool IsBullet)
{
    public int Length => AnswerMarkdown.ToPlainText(Spans).Length;
}

/// <summary>Una diapositiva: su título, sus líneas o una tabla.</summary>
public sealed record SlideSpec(string Title, IReadOnlyList<SlideLine> Lines, AnswerTable? Table = null, string? Subtitle = null, bool IsCover = false);

/// <summary>
/// 2026-09-15 — cómo se reparte una respuesta en diapositivas. Va aparte del archivo porque es donde se
/// decide si la presentación se entiende, y así se prueba sin abrir PowerPoint.
///
/// · La primera es la portada, con el título y, si la respuesta empieza con una frase corta, esa frase.
/// · Si la respuesta tiene títulos, cada título abre una diapositiva.
/// · Si no, pero es una lista numerada de pasos con sus detalles (lo más habitual), cada paso es una
///   diapositiva con sus detalles como puntos.
/// · Cada tabla va en su propia diapositiva.
/// · Una diapositiva con demasiado texto se parte en otra con «(cont.)»: una diapositiva que hay que
///   leer entera no sirve para presentar.
/// </summary>
public static class PresentationPlan
{
    public const int MaximumLines = 7;
    public const int MaximumCharacters = 520;
    private const int MaximumSubtitleCharacters = 180;

    public static IReadOnlyList<SlideSpec> From(string title, string markdown)
    {
        var blocks = AnswerMarkdown.Parse(markdown).ToList();
        var slides = new List<SlideSpec>();
        var documentTitle = string.IsNullOrWhiteSpace(title) ? "Presentación" : title.Trim();

        // Un título igual al de la presentación al empezar ya está en la portada.
        if (blocks.FirstOrDefault() is AnswerHeading opening &&
            string.Equals(AnswerMarkdown.ToPlainText(opening.Spans).Trim(), documentTitle, StringComparison.OrdinalIgnoreCase))
        {
            blocks.RemoveAt(0);
        }

        string? subtitle = null;
        if (blocks.FirstOrDefault() is AnswerParagraph lead &&
            AnswerMarkdown.ToPlainText(lead.Spans).Trim() is { Length: > 0 and <= MaximumSubtitleCharacters } leadText)
        {
            subtitle = leadText;
            blocks.RemoveAt(0);
        }

        slides.Add(new SlideSpec(documentTitle, [], Subtitle: subtitle, IsCover: true));

        var byHeadings = blocks.OfType<AnswerHeading>().Any();
        var bySteps = !byHeadings && blocks.OfType<AnswerListItem>().Count(item => item.Depth == 0 && item.Number is not null) >= 2;

        var currentTitle = byHeadings || bySteps ? "Introducción" : documentTitle;
        var lines = new List<SlideLine>();

        void Flush()
        {
            AddSplit(slides, currentTitle, lines);
            lines = [];
        }

        foreach (var block in blocks)
        {
            switch (block)
            {
                case AnswerHeading heading when byHeadings:
                    Flush();
                    currentTitle = AnswerMarkdown.ToPlainText(heading.Spans).Trim();
                    break;
                case AnswerHeading heading:
                    lines.Add(new SlideLine(heading.Spans, 0, null, IsBullet: false));
                    break;
                case AnswerListItem { Depth: 0, Number: not null } step when bySteps:
                    Flush();
                    currentTitle = $"{step.Number}. {AnswerMarkdown.ToPlainText(step.Spans).Trim()}";
                    break;
                case AnswerListItem item:
                    var depth = bySteps ? Math.Max(0, item.Depth - 1) : item.Depth;
                    lines.Add(new SlideLine(item.Spans, Math.Min(depth, 2), bySteps && item.Depth == 0 ? null : item.Number, IsBullet: true));
                    break;
                case AnswerTable table:
                    Flush();
                    // En una lista de pasos, una tabla no pertenece al último paso: lleva el título del documento.
                    slides.Add(new SlideSpec(bySteps ? documentTitle : currentTitle, [], table));
                    break;
                case AnswerParagraph paragraph:
                    lines.Add(new SlideLine(paragraph.Spans, 0, null, IsBullet: false));
                    break;
                case AnswerQuote quote:
                    lines.Add(new SlideLine(quote.Spans, 0, null, IsBullet: false));
                    break;
                case AnswerCode code:
                    lines.Add(new SlideLine([new AnswerSpan(code.Text, AnswerSpanStyle.Code)], 0, null, IsBullet: false));
                    break;
            }
        }

        Flush();
        return slides;
    }

    private static void AddSplit(List<SlideSpec> slides, string title, List<SlideLine> lines)
    {
        if (lines.Count == 0)
        {
            return;
        }

        var page = new List<SlideLine>();
        var characters = 0;
        var part = 0;
        foreach (var line in lines)
        {
            if (page.Count > 0 && (page.Count >= MaximumLines || characters + line.Length > MaximumCharacters))
            {
                slides.Add(new SlideSpec(part++ == 0 ? title : title + " (cont.)", page));
                page = [];
                characters = 0;
            }

            page.Add(line);
            characters += line.Length;
        }

        slides.Add(new SlideSpec(part == 0 ? title : title + " (cont.)", page));
    }
}
