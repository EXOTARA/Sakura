using System.Text.RegularExpressions;
using Nexo.Core.Assistant;

namespace Nexo.Core.Documents;

/// <summary>Una línea de una diapositiva: un párrafo o un punto de lista, con su sangría.</summary>
public sealed record SlideLine(IReadOnlyList<AnswerSpan> Spans, int Depth, int? Number, bool IsBullet)
{
    public int Length => AnswerMarkdown.ToPlainText(Spans).Length;
}

/// <summary>El diseño de una diapositiva.</summary>
public enum SlideLayout
{
    /// <summary>La primera: título y subtítulo.</summary>
    Cover,

    /// <summary>El índice, con los apartados numerados.</summary>
    Agenda,

    /// <summary>Los pasos de un proceso en fila, numerados y unidos por una línea.</summary>
    Process,

    /// <summary>La que abre una parte de la presentación, sobre fondo oscuro.</summary>
    Section,

    /// <summary>Título y texto.</summary>
    Content,

    /// <summary>Título y una lista de puntos cortos repartida en dos columnas.</summary>
    TwoColumns,

    /// <summary>Título y una tabla.</summary>
    Table,

    /// <summary>Título, una gráfica y, si hay, unas líneas que la explican al lado.</summary>
    Chart,

    /// <summary>Una cita o una idea destacada, en grande.</summary>
    Quote,

    /// <summary>La conclusión, al final, sobre fondo oscuro.</summary>
    Closing
}

/// <summary>Una diapositiva: su diseño, su título, sus líneas, su tabla o gráfica y las notas del orador.</summary>
public sealed record SlideSpec(
    string Title,
    IReadOnlyList<SlideLine> Lines,
    SlideLayout Layout = SlideLayout.Content,
    AnswerTable? Table = null,
    ChartSpec? Chart = null,
    string? Subtitle = null,
    string? Notes = null,
    int? SectionNumber = null)
{
    public bool IsCover => Layout == SlideLayout.Cover;
}

/// <summary>
/// 2026-09-15 — cómo se reparte una respuesta en diapositivas. Va aparte del archivo porque es donde se
/// decide si la presentación se entiende, y así se prueba sin abrir PowerPoint.
///
/// · La primera es la portada, con el título y, si la respuesta empieza con una frase corta, esa frase.
/// · Si la respuesta tiene títulos, cada título abre una diapositiva. Con dos niveles de títulos
///   («#» y «##») y al menos dos del primero, los del primero abren una parte con su separador.
/// · Si no, pero es una lista numerada de pasos con sus detalles (lo más habitual), cada paso es una
///   diapositiva con sus detalles como puntos.
/// · Con cuatro a ocho apartados, detrás de la portada va un índice.
/// · Una tabla de cifras es una gráfica; cualquier otra tabla va en su propia diapositiva.
/// · Un apartado que solo tiene una cita se enseña en grande; el último, si es la conclusión, sobre
///   fondo oscuro.
/// · Seis a doce puntos cortos se reparten en dos columnas; una diapositiva con más texto se parte en
///   otra con «(cont.)»: una diapositiva que hay que leer entera no sirve para presentar.
/// · Un párrafo que empieza por «Notas:» no se ve en la diapositiva: va a las notas del orador.
/// </summary>
public static partial class PresentationPlan
{
    public const int MaximumLines = 7;
    public const int MaximumCharacters = 520;
    private const int MaximumSubtitleCharacters = 180;
    private const int MaximumAgendaEntries = 8;
    public const int MaximumTableRows = 8;
    private const int MaximumProcessSteps = 6;

    private enum SectionKind { Intro, Divider, Slide }

    private sealed class Section(string title, SectionKind kind)
    {
        public string Title { get; } = title;
        public SectionKind Kind { get; } = kind;
        public List<AnswerBlock> Blocks { get; } = [];
        public List<string> Notes { get; } = [];
    }

    public static IReadOnlyList<SlideSpec> From(string title, string markdown)
    {
        var blocks = AnswerMarkdown.Parse(markdown).ToList();
        var documentTitle = string.IsNullOrWhiteSpace(title) ? "Presentación" : title.Trim();

        // Un título igual al de la presentación al empezar ya está en la portada.
        if (blocks.FirstOrDefault() is AnswerHeading opening &&
            string.Equals(AnswerMarkdown.ToPlainText(opening.Spans).Trim(), documentTitle, StringComparison.OrdinalIgnoreCase))
        {
            blocks.RemoveAt(0);
        }

        string? subtitle = null;
        if (blocks.FirstOrDefault() is AnswerParagraph lead && NotesText(lead.Spans) is null &&
            AnswerMarkdown.ToPlainText(lead.Spans).Trim() is { Length: > 0 and <= MaximumSubtitleCharacters } leadText)
        {
            // «Claro, aquí tienes…:» es la frase del chat, no un subtítulo: no va en la portada.
            subtitle = ChatLead().IsMatch(leadText) ? null : leadText;
            blocks.RemoveAt(0);
        }

        var levels = blocks.OfType<AnswerHeading>().Select(heading => heading.Level).Distinct().Order().ToList();
        var byHeadings = levels.Count > 0;
        var bySteps = !byHeadings && blocks.OfType<AnswerListItem>().Count(item => item.Depth == 0 && item.Number is not null) >= 2;
        int? dividerLevel = levels.Count >= 2 && blocks.OfType<AnswerHeading>().Count(heading => heading.Level == levels[0]) >= 2 ? levels[0] : null;
        var slideLevel = dividerLevel is null ? int.MaxValue : levels[1];

        var sections = Split(blocks, byHeadings || bySteps ? "Introducción" : documentTitle, bySteps, dividerLevel, slideLevel);

        var slides = new List<SlideSpec> { new(documentTitle, [], SlideLayout.Cover, Subtitle: subtitle) };

        if (byHeadings)
        {
            // Con partes, cada entrada del índice es una parte y debajo lleva los apartados que contiene.
            var agenda = new List<SlideLine>();
            var entries = 0;
            for (var i = 0; i < sections.Count; i++)
            {
                if (dividerLevel is null ? sections[i].Kind != SectionKind.Slide : sections[i].Kind != SectionKind.Divider)
                {
                    continue;
                }

                agenda.Add(new SlideLine([new AnswerSpan(WithoutNumber(sections[i].Title), AnswerSpanStyle.None)], 0, ++entries, IsBullet: true));
                if (dividerLevel is not null)
                {
                    var parts = sections.Skip(i + 1).TakeWhile(section => section.Kind != SectionKind.Divider)
                        .Where(section => section.Kind == SectionKind.Slide).Select(section => WithoutNumber(section.Title)).ToList();
                    if (parts.Count > 0)
                    {
                        agenda.Add(new SlideLine([new AnswerSpan(string.Join(" · ", parts), AnswerSpanStyle.None)], 1, null, IsBullet: false));
                    }
                }
            }

            if (entries is >= 4 and <= MaximumAgendaEntries || (dividerLevel is not null && entries is >= 2 and <= MaximumAgendaEntries))
            {
                slides.Add(new SlideSpec("Contenido", agenda, SlideLayout.Agenda));
            }
        }

        // En una lista de pasos, detrás de la portada va el recorrido completo, con los pasos en fila.
        var steps = sections.Where(section => section.Kind == SectionKind.Slide).ToList();
        if (bySteps && steps.Count is >= 3 and <= MaximumProcessSteps)
        {
            slides.Add(new SlideSpec("Los pasos", steps.Select((section, index) =>
                new SlideLine([new AnswerSpan(WithoutNumber(section.Title), AnswerSpanStyle.None)], 0, index + 1, IsBullet: true)).ToList(),
                SlideLayout.Process));
        }

        var sectionNumber = 0;
        for (var i = 0; i < sections.Count; i++)
        {
            var section = sections[i];
            var first = slides.Count;

            if (section.Kind == SectionKind.Divider)
            {
                slides.Add(new SlideSpec(WithoutNumber(section.Title), [], SlideLayout.Section, SectionNumber: ++sectionNumber));
            }
            else
            {
                var isStep = bySteps && section.Kind == SectionKind.Slide;
                Lay(slides, section, isStep ? documentTitle : section.Title, isLast: i == sections.Count - 1, stepDetails: isStep);
            }

            if (section.Notes.Count > 0)
            {
                // Las notas van a la primera diapositiva del apartado; sin diapositivas, a la anterior.
                var target = Math.Min(first, slides.Count - 1);
                var existing = slides[target].Notes;
                slides[target] = slides[target] with
                {
                    Notes = string.Join("\n", new[] { existing }.Concat(section.Notes).Where(note => !string.IsNullOrWhiteSpace(note)))
                };
            }
        }

        return slides;
    }

    private static List<Section> Split(List<AnswerBlock> blocks, string introTitle, bool bySteps, int? dividerLevel, int slideLevel)
    {
        var sections = new List<Section>();
        var current = new Section(introTitle, SectionKind.Intro);

        void Open(Section next)
        {
            if (current.Blocks.Count > 0 || current.Notes.Count > 0 || current.Kind != SectionKind.Intro)
            {
                sections.Add(current);
            }

            current = next;
        }

        foreach (var block in blocks)
        {
            switch (block)
            {
                case AnswerHeading heading when dividerLevel is not null && heading.Level == dividerLevel:
                    var dividerTitle = AnswerMarkdown.ToPlainText(heading.Spans).Trim();
                    Open(new Section(dividerTitle, SectionKind.Divider));
                    // Lo que haya entre el separador y el primer subtítulo va en una diapositiva con el mismo título.
                    Open(new Section(dividerTitle, SectionKind.Intro));
                    break;
                case AnswerHeading heading when heading.Level <= slideLevel:
                    Open(new Section(AnswerMarkdown.ToPlainText(heading.Spans).Trim(), SectionKind.Slide));
                    break;
                case AnswerListItem { Depth: 0, Number: not null } step when bySteps:
                    Open(new Section($"{step.Number}. {AnswerMarkdown.ToPlainText(step.Spans).Trim()}", SectionKind.Slide));
                    break;
                case AnswerParagraph paragraph when NotesText(paragraph.Spans) is { } note:
                    current.Notes.Add(note);
                    break;
                case AnswerQuote quote when NotesText(quote.Spans) is { } quotedNote:
                    current.Notes.Add(quotedNote);
                    break;
                default:
                    current.Blocks.Add(block);
                    break;
            }
        }

        Open(new Section(string.Empty, SectionKind.Intro));
        return sections;
    }

    /// <param name="stepDetails">En un paso de una lista numerada, los detalles del paso suben un nivel: son los puntos de la diapositiva.</param>
    private static void Lay(List<SlideSpec> slides, Section section, string tableTitle, bool isLast, bool stepDetails)
    {
        var title = section.Title;
        IEnumerable<SlideLine> ToLines(AnswerBlock block) => PresentationPlan.ToLines(block, stepDetails);

        // Un apartado que es solo una cita corta: en grande.
        if (section.Blocks is [AnswerQuote onlyQuote] && AnswerMarkdown.ToPlainText(onlyQuote.Spans).Length <= 280)
        {
            slides.Add(new SlideSpec(title, [new SlideLine(onlyQuote.Spans, 0, null, IsBullet: false)], SlideLayout.Quote));
            return;
        }

        var lines = new List<SlideLine>();
        var tables = section.Blocks.OfType<AnswerTable>().ToList();

        // Una gráfica con unas pocas líneas que la explican: todo en la misma diapositiva.
        if (tables.Count == 1 && ChartPlan.From(tables[0], title) is { } soleChart)
        {
            var explanation = section.Blocks.Where(block => block is not AnswerTable).SelectMany(ToLines).ToList();
            if (explanation.Count <= 4 && explanation.Sum(line => line.Length) <= 320)
            {
                slides.Add(new SlideSpec(title, explanation, SlideLayout.Chart, Table: tables[0], Chart: soleChart));
                return;
            }
        }

        foreach (var block in section.Blocks)
        {
            if (block is AnswerTable table)
            {
                AddText(slides, title, lines, closing: false);
                lines = [];
                if (ChartPlan.From(table, tableTitle) is { } chart)
                {
                    slides.Add(new SlideSpec(tableTitle, [], SlideLayout.Chart, Table: table, Chart: chart));
                    continue;
                }

                // Una tabla larga sigue en otra diapositiva, repitiendo la cabecera, sin perder filas.
                for (var start = 0; start == 0 || start < table.Rows.Count; start += MaximumTableRows)
                {
                    var rows = table.Rows.Skip(start).Take(MaximumTableRows).ToList();
                    slides.Add(new SlideSpec(start == 0 ? tableTitle : tableTitle + " (cont.)", [], SlideLayout.Table, Table: table with { Rows = rows }));
                }

                continue;
            }

            lines.AddRange(ToLines(block));
        }

        AddText(slides, title, lines, closing: isLast && tables.Count == 0 && ClosingTitle().IsMatch(title));
    }

    private static IEnumerable<SlideLine> ToLines(AnswerBlock block, bool stepDetails) => block switch
    {
        // Un subtítulo por debajo del nivel de las diapositivas es una línea en negrita.
        AnswerHeading heading => [new SlideLine(heading.Spans.Select(span => span with { Style = span.Style | AnswerSpanStyle.Bold }).ToList(), 0, null, IsBullet: false)],
        AnswerListItem item when stepDetails => [new SlideLine(item.Spans, Math.Min(Math.Max(0, item.Depth - 1), 2), item.Depth == 0 ? null : item.Number, IsBullet: true)],
        AnswerListItem item => [new SlideLine(item.Spans, Math.Min(item.Depth, 2), item.Number, IsBullet: true)],
        AnswerParagraph paragraph => [new SlideLine(paragraph.Spans, 0, null, IsBullet: false)],
        AnswerQuote quote => [new SlideLine(quote.Spans, 0, null, IsBullet: false)],
        AnswerCode code => [new SlideLine([new AnswerSpan(code.Text, AnswerSpanStyle.Code)], 0, null, IsBullet: false)],
        _ => []
    };

    private static void AddText(List<SlideSpec> slides, string title, List<SlideLine> lines, bool closing)
    {
        if (lines.Count == 0)
        {
            return;
        }

        var characters = lines.Sum(line => line.Length);
        if (closing && lines.Count <= MaximumLines && characters <= MaximumCharacters)
        {
            slides.Add(new SlideSpec(title, lines, SlideLayout.Closing));
            return;
        }

        // Muchos puntos cortos caben mejor en dos columnas que en dos diapositivas.
        if (lines.Count is >= 6 and <= 12 && characters <= 900 &&
            lines.All(line => line is { IsBullet: true, Depth: 0 } && line.Length <= 80))
        {
            slides.Add(new SlideSpec(title, lines, SlideLayout.TwoColumns));
            return;
        }

        var page = new List<SlideLine>();
        var count = 0;
        var part = 0;
        foreach (var line in lines)
        {
            if (page.Count > 0 && (page.Count >= MaximumLines || count + line.Length > MaximumCharacters))
            {
                slides.Add(new SlideSpec(part++ == 0 ? title : title + " (cont.)", page));
                page = [];
                count = 0;
            }

            page.Add(line);
            count += line.Length;
        }

        slides.Add(new SlideSpec(part == 0 ? title : title + " (cont.)", page));
    }

    /// <summary>
    /// Un título sin su número delante («Parte 1: Contexto», «2. Metas» → «Contexto», «Metas»): el
    /// índice y los separadores ya ponen el suyo.
    /// </summary>
    private static string WithoutNumber(string title)
    {
        var clean = NumberPrefix().Replace(title, string.Empty).Trim();
        return clean.Length == 0 ? title : clean;
    }

    /// <summary>El texto de un párrafo de notas del orador («Notas: …»), o nulo si no lo es.</summary>
    private static string? NotesText(IReadOnlyList<AnswerSpan> spans)
    {
        var match = NotesPrefix().Match(AnswerMarkdown.ToPlainText(spans).Trim());
        return match.Success && match.Groups["text"].Value.Trim() is { Length: > 0 } text ? text : null;
    }

    [GeneratedRegex(@"^(notas?( (del|para el) (orador|presentador|ponente))?|gui[oó]n( del orador)?)\s*:\s*(?<text>[\s\S]+)$", RegexOptions.IgnoreCase)]
    private static partial Regex NotesPrefix();

    [GeneratedRegex(@"^((parte|secci[oó]n|bloque|cap[ií]tulo|unidad|tema|m[oó]dulo)\s+([0-9]+|[ivxlc]+|uno|dos|tres|cuatro|cinco)\s*[:.\-–—]\s*|[0-9]+[.)]\s+)", RegexOptions.IgnoreCase)]
    private static partial Regex NumberPrefix();

    [GeneratedRegex(@"(:\s*$)|^(¡?claro|por supuesto|aqu[ií] (tienes|te dejo)|te dejo|con gusto|perfecto|¡?listo)", RegexOptions.IgnoreCase)]
    private static partial Regex ChatLead();

    [GeneratedRegex(@"^(conclusi[oó]n(es)?|en resumen|resumen|cierre|reflexi[oó]n final|para terminar|conclusiones finales)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ClosingTitle();
}
