using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Nexo.Core.Assistant;

namespace Nexo.App.Views.Controls;

/// <summary>
/// Dibuja una respuesta de Sakura con su formato: párrafos, títulos, listas, código y tablas
/// (Adler, 2026-09-14). La lectura la hace <see cref="AnswerMarkdown"/>; esto solo elige cómo se ve.
///
/// **Las tablas se dibujan como tarjetas, una por fila.** El chat mide menos de 450 píxeles de ancho
/// y una tabla de cuatro columnas ahí es un muro de celdas de dos palabras cada una. Una tarjeta por
/// fila, con la primera celda como título y el resto como «columna: valor», se lee de arriba abajo
/// sin perder nada de lo que decía la tabla.
/// </summary>
public static class AnswerRenderer
{
    private const double BodySize = 13;
    private const double BodyLineHeight = 20;

    public static UIElement Render(string text)
    {
        var stack = new StackPanel();
        var blocks = AnswerMarkdown.Parse(text);

        if (blocks.Count == 0)
        {
            stack.Children.Add(Paragraph([new AnswerSpan(text, AnswerSpanStyle.None)]));
            return stack;
        }

        AnswerBlock? previous = null;
        foreach (var block in blocks)
        {
            var element = block switch
            {
                AnswerHeading heading => Heading(heading),
                AnswerListItem item => ListItem(item),
                AnswerTable table => Table(table),
                AnswerCode code => Code(code),
                AnswerParagraph paragraph => Paragraph(paragraph.Spans),
                _ => null
            };

            if (element is null)
            {
                continue;
            }

            // Los puntos de una misma lista van juntos; entre bloques distintos, un respiro.
            element.Margin = previous is null
                ? new Thickness(0)
                : previous is AnswerListItem && block is AnswerListItem
                    ? new Thickness(0, 3, 0, 0)
                    : new Thickness(0, 9, 0, 0);

            stack.Children.Add(element);
            previous = block;
        }

        return stack;
    }

    private static TextBlock Paragraph(IReadOnlyList<AnswerSpan> spans)
    {
        var textBlock = NewText();
        AddInlines(textBlock, spans);
        return textBlock;
    }

    private static TextBlock Heading(AnswerHeading heading)
    {
        var textBlock = NewText();
        textBlock.FontSize = 13.5;
        textBlock.FontWeight = FontWeights.SemiBold;
        AddInlines(textBlock, heading.Spans);
        return textBlock;
    }

    private static FrameworkElement ListItem(AnswerListItem item)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(item.Number is null ? 16 : 22) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());

        var marker = NewText();
        marker.Text = item.Number is { } number
            ? number.ToString(CultureInfo.CurrentCulture) + "."
            : "•";
        marker.Foreground = Resource<Brush>("BrushAccent");
        if (item.Number is not null)
        {
            marker.FontWeight = FontWeights.SemiBold;
        }

        var body = Paragraph(item.Spans);
        Grid.SetColumn(body, 1);
        grid.Children.Add(marker);
        grid.Children.Add(body);

        return new Border { Padding = new Thickness(14 * Math.Min(item.Depth, 3), 0, 0, 0), Child = grid };
    }

    private static FrameworkElement Table(AnswerTable table)
    {
        var cards = new StackPanel();

        foreach (var row in table.Rows)
        {
            var card = new StackPanel();
            for (var column = 0; column < row.Count; column++)
            {
                var cell = row[column];
                if (AnswerMarkdown.ToPlainText(cell).Trim().Length == 0)
                {
                    continue;
                }

                var textBlock = NewText();
                if (column == 0)
                {
                    textBlock.FontWeight = FontWeights.SemiBold;
                    AddInlines(textBlock, cell.Select(span => span with { Style = span.Style & ~AnswerSpanStyle.Bold }).ToList());
                }
                else
                {
                    textBlock.Margin = new Thickness(0, 3, 0, 0);
                    var header = column < table.Header.Count
                        ? AnswerMarkdown.ToPlainText(table.Header[column]).Trim()
                        : string.Empty;
                    if (header.Length > 0)
                    {
                        textBlock.Inlines.Add(new Run(header + ": ") { Foreground = Resource<Brush>("BrushTextSecondary") });
                    }

                    AddInlines(textBlock, cell);
                }

                card.Children.Add(textBlock);
            }

            cards.Children.Add(new Border
            {
                Margin = new Thickness(0, cards.Children.Count == 0 ? 0 : 6, 0, 0),
                Padding = new Thickness(11, 8, 11, 9),
                CornerRadius = Resource<CornerRadius>("RadiusMedium"),
                Background = Resource<Brush>("BrushSurface"),
                BorderBrush = Resource<Brush>("BrushBorder"),
                BorderThickness = new Thickness(1),
                Child = card
            });
        }

        return cards;
    }

    private static FrameworkElement Code(AnswerCode code) =>
        new Border
        {
            Padding = new Thickness(10, 8, 10, 8),
            CornerRadius = Resource<CornerRadius>("RadiusMedium"),
            Background = Resource<Brush>("BrushSurface"),
            BorderBrush = Resource<Brush>("BrushBorder"),
            BorderThickness = new Thickness(1),
            Child = new TextBlock
            {
                Text = code.Text,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                Foreground = Resource<Brush>("BrushTextPrimary")
            }
        };

    private static TextBlock NewText() =>
        new()
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = BodySize,
            LineHeight = BodyLineHeight,
            Foreground = Resource<Brush>("BrushTextPrimary")
        };

    private static void AddInlines(TextBlock textBlock, IReadOnlyList<AnswerSpan> spans)
    {
        foreach (var span in spans)
        {
            var parts = span.Text.Split('\n');
            for (var i = 0; i < parts.Length; i++)
            {
                if (i > 0)
                {
                    textBlock.Inlines.Add(new LineBreak());
                }

                if (parts[i].Length == 0)
                {
                    continue;
                }

                var run = new Run(parts[i]);
                if (span.Style.HasFlag(AnswerSpanStyle.Bold))
                {
                    run.FontWeight = FontWeights.SemiBold;
                }

                if (span.Style.HasFlag(AnswerSpanStyle.Italic))
                {
                    run.FontStyle = FontStyles.Italic;
                }

                if (span.Style.HasFlag(AnswerSpanStyle.Code))
                {
                    run.FontFamily = new FontFamily("Cascadia Mono, Consolas");
                    run.Background = Resource<Brush>("BrushSurface");
                }

                textBlock.Inlines.Add(run);
            }
        }
    }

    private static T Resource<T>(string key) => (T)Application.Current.FindResource(key);
}
