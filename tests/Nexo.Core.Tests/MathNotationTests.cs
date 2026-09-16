using Nexo.Core.Assistant;
using Nexo.Core.Documents;

namespace Nexo.Core.Tests;

public sealed class MathNotationTests
{
    [Theory]
    // Notación de calculadora, la que ya se usa en Excel y GeoGebra.
    [InlineData("x^2", "<m:sSup>")]
    [InlineData("x_1", "<m:sSub>")]
    [InlineData("x_1^2", "<m:sSubSup>")]
    [InlineData("sqrt(x)", "<m:rad>")]
    [InlineData("sum_{i=1}^{n} i", "<m:nary>")]
    // La misma fórmula en LaTeX, que es lo que sale al copiar de un libro o de otra IA.
    [InlineData(@"\frac{a}{b}", "<m:f>")]
    [InlineData(@"\sqrt[3]{x}", "<m:deg>")]
    [InlineData(@"\int_0^1 x dx", "<m:nary>")]
    [InlineData(@"\lim_{x \to 0} f(x)", "<m:limLow>")]
    public void BothWaysOfWriting_ProduceARealEquation(string formula, string expected)
    {
        var omml = OfficeMath.Inline(formula);

        Assert.StartsWith("<m:oMath>", omml);
        Assert.Contains(expected, omml);
        Assert.Contains("Cambria Math", omml);
        Assert.True(OfficeMath.CanConvert(formula), formula);
    }

    [Fact]
    public void GreekLettersAndSigns_WithOrWithoutBackslash()
    {
        Assert.Contains("π", OfficeMath.Inline("pi"));
        Assert.Contains("π", OfficeMath.Inline(@"\pi"));
        Assert.Contains("≤", OfficeMath.Inline("x <= 3"));
        Assert.Contains("≤", OfficeMath.Inline(@"x \leq 3"));
        Assert.Contains("±", OfficeMath.Inline("a +- b"));
    }

    [Fact]
    public void Variables_AreItalic_AndNumbersAndSignsAreNot()
    {
        var omml = OfficeMath.Inline("2x");

        // «p» es el estilo derecho de OMML: se marca el 2, no la x.
        var two = omml.IndexOf("2", StringComparison.Ordinal);
        var upright = omml.IndexOf("m:val=\"p\"", StringComparison.Ordinal);
        Assert.True(upright >= 0 && upright < two, omml);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(omml, "m:val=\"p\""));
    }

    [Fact]
    public void AFormulaOfItsOwn_IsCentred()
    {
        var omml = OfficeMath.Display("x^2 + 1");

        Assert.Contains("<m:oMathPara>", omml);
        Assert.Contains("<m:jc m:val=\"center\"/>", omml);
    }

    [Theory]
    // Lo que no se entiende se dice, en vez de dibujar cualquier cosa.
    [InlineData(@"\begin{matrix} 1 & 2 \end{matrix}")]
    [InlineData(@"\frac{a}{")]
    [InlineData("x^2)")]
    public void WhatCannotBeConverted_IsReported(string formula) =>
        Assert.False(OfficeMath.CanConvert(formula));

    [Fact]
    public void InTheChat_AFormulaIsReadWithoutItsNotation()
    {
        Assert.Equal("x^2+1", MathNotation.ToPlainText("x^2 + 1").Replace(" ", string.Empty));
        Assert.Equal("π·r^2", MathNotation.ToPlainText(@"\pi \cdot r^2").Replace(" ", string.Empty));
        Assert.Equal("√x", MathNotation.ToPlainText("sqrt(x)").Replace("(", string.Empty).Replace(")", string.Empty));
    }

    [Fact]
    public void TheAnswerMarkdown_RecognisesFormulasInTheTextAndOnTheirOwnLine()
    {
        var blocks = AnswerMarkdown.Parse("El error es $e = |x - x_a|$.\n\n$$x_{n+1} = x_n - \\frac{f(x_n)}{f'(x_n)}$$");

        var paragraph = Assert.IsType<AnswerParagraph>(blocks[0]);
        var formula = Assert.Single(paragraph.Spans, span => span.Style.HasFlag(AnswerSpanStyle.Math));
        Assert.Equal("e = |x - x_a|", formula.Text);
        // El signo de dólar no se lee en el chat.
        Assert.DoesNotContain("$", AnswerMarkdown.ToPlainText(paragraph.Spans));

        var display = Assert.IsType<AnswerMath>(blocks[1]);
        Assert.StartsWith("x_{n+1}", display.Formula);
    }

    [Fact]
    public void TheWord_CarriesTheEquationsAndSaysWhichOnesItCouldNotRead()
    {
        var markdown = "## Método\n\nSe aplica $x^2$ hasta converger.\n\n$$\\frac{a+b}{2}$$\n\nY esto no: $\\begin{cases} 1 \\end{cases}$";
        var document = WordDocumentBuilder.BuildFromMarkdown("Métodos numéricos", markdown);

        using var archive = new System.IO.Compression.ZipArchive(new MemoryStream(document));
        using var reader = new StreamReader(archive.GetEntry("word/document.xml")!.Open());
        var body = reader.ReadToEnd();

        Assert.Contains("xmlns:m=\"http://schemas.openxmlformats.org/officeDocument/2006/math\"", body);
        Assert.Contains("<m:sSup>", body);
        Assert.Contains("<m:oMathPara>", body);
        Assert.Equal([@"\begin{cases} 1 \end{cases}"], WordDocumentBuilder.UnreadableFormulas(markdown));
    }
}
