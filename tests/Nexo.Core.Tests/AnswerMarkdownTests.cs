using Nexo.Core.Assistant;

namespace Nexo.Core.Tests;

public sealed class AnswerMarkdownTests
{
    [Fact]
    public void TheMouseTableFromAdlersScreenshot_BecomesATable()
    {
        // Recortada de la captura de Adler (2026-09-14): salía como texto crudo con barras y guiones.
        const string answer = """
            Claro, aquí tienes tres opciones de **mouse** con buena relación calidad-precio:

            | Modelo | Tipo | Rango de precio (≈) | Ventajas principales |
            |--------|------|---------------------|----------------------|
            | **Logitech M720 Triathlon** | inalámbrico (2.4 GHz + Bluetooth) | $25-30 USD | Conexión a 3 dispositivos |
            | **Redragon M602-RGB** | cableado | $15-20 USD | DPI ajustable |

            Estas opciones cubren tanto usuarios que prefieren la libertad del inalámbrico como los que buscan cable.
            """;

        var blocks = AnswerMarkdown.Parse(answer);

        Assert.Equal(3, blocks.Count);
        var intro = Assert.IsType<AnswerParagraph>(blocks[0]);
        Assert.Contains(intro.Spans, span => span.Text == "mouse" && span.Style == AnswerSpanStyle.Bold);
        Assert.DoesNotContain(intro.Spans, span => span.Text.Contains("**"));

        var table = Assert.IsType<AnswerTable>(blocks[1]);
        Assert.Equal(4, table.Header.Count);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal("Logitech M720 Triathlon", AnswerMarkdown.ToPlainText(table.Rows[0][0]));
        Assert.Equal(AnswerSpanStyle.Bold, table.Rows[0][0][0].Style);
        Assert.Equal("$15-20 USD", AnswerMarkdown.ToPlainText(table.Rows[1][2]));

        Assert.IsType<AnswerParagraph>(blocks[2]);
    }

    [Fact]
    public void Lists_BulletsAndNumbers_KeepTheirNumbersAndDepth()
    {
        var blocks = AnswerMarkdown.Parse("""
            Pasos:
            1. Abre Configuración
            2. Ve a **Sistema**
               - Pantalla
            • Otra opción
            """);

        Assert.IsType<AnswerParagraph>(blocks[0]);
        var first = Assert.IsType<AnswerListItem>(blocks[1]);
        Assert.Equal(1, first.Number);
        var second = Assert.IsType<AnswerListItem>(blocks[2]);
        Assert.Equal(2, second.Number);
        Assert.Contains(second.Spans, span => span.Text == "Sistema" && span.Style == AnswerSpanStyle.Bold);
        var nested = Assert.IsType<AnswerListItem>(blocks[3]);
        Assert.Null(nested.Number);
        Assert.Equal(1, nested.Depth);
        Assert.IsType<AnswerListItem>(blocks[4]);
    }

    [Fact]
    public void HeadingsCodeAndLinks()
    {
        var blocks = AnswerMarkdown.Parse("""
            ## Cómo instalarlo
            Descárgalo desde [la web de Ollama](https://ollama.com) y ejecuta `ollama pull gemma3`.

            ```
            winget install Ollama.Ollama
            ```
            """);

        var heading = Assert.IsType<AnswerHeading>(blocks[0]);
        Assert.Equal("Cómo instalarlo", AnswerMarkdown.ToPlainText(heading.Spans));

        var paragraph = Assert.IsType<AnswerParagraph>(blocks[1]);
        Assert.Equal("Descárgalo desde la web de Ollama y ejecuta ollama pull gemma3.", AnswerMarkdown.ToPlainText(paragraph.Spans));
        Assert.Contains(paragraph.Spans, span => span.Text == "ollama pull gemma3" && span.Style == AnswerSpanStyle.Code);

        var code = Assert.IsType<AnswerCode>(blocks[2]);
        Assert.Equal("winget install Ollama.Ollama", code.Text);
    }

    [Theory]
    [InlineData("3 * 4 = 12")]
    [InlineData("un asterisco suelto * aquí")]
    [InlineData("**sin cerrar")]
    [InlineData("precio: $25-30 | aprox")]
    public void WhatIsNotMarkdown_StaysExactlyAsWritten(string text)
    {
        var blocks = AnswerMarkdown.Parse(text);

        var paragraph = Assert.IsType<AnswerParagraph>(Assert.Single(blocks));
        Assert.Equal(text, AnswerMarkdown.ToPlainText(paragraph.Spans));
    }

    [Fact]
    public void ItalicAndBoldTogether()
    {
        var spans = AnswerMarkdown.ParseInline("Esto es *importante* y **muy importante**.");

        Assert.Contains(spans, span => span.Text == "importante" && span.Style == AnswerSpanStyle.Italic);
        Assert.Contains(spans, span => span.Text == "muy importante" && span.Style == AnswerSpanStyle.Bold);
    }

    [Fact]
    public void ParagraphLineBreaks_AreKept_AndEmptyInputGivesNothing()
    {
        var paragraph = Assert.IsType<AnswerParagraph>(Assert.Single(AnswerMarkdown.Parse("Primera línea\nSegunda línea")));

        Assert.Equal("Primera línea\nSegunda línea", AnswerMarkdown.ToPlainText(paragraph.Spans));
        Assert.Empty(AnswerMarkdown.Parse("   \n  "));
    }
}
