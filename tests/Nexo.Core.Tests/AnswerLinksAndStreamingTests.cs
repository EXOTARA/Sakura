using Nexo.Core.Assistant;

namespace Nexo.Core.Tests;

public sealed class AnswerLinksAndStreamingTests
{
    private static IReadOnlyList<AnswerSpan> Links(string text) =>
        AnswerMarkdown.ParseInline(text).Where(span => span.Url is not null).ToList();

    [Fact]
    public void MarkdownLink_KeepsItsTextAndCarriesTheUrl()
    {
        var link = Assert.Single(Links("Mira [la web de Ollama](https://ollama.com) ahora."));
        Assert.Equal("la web de Ollama", link.Text);
        Assert.Equal("https://ollama.com/", link.Url);
    }

    [Fact]
    public void AngleBracketLink_LikeTheOneInAdlersCapture_IsClickableAndShortened()
    {
        var spans = AnswerMarkdown.ParseInline("Enlace: <https://www.amazon.com/dp/B07S9J7R2B>");
        var link = Assert.Single(spans, span => span.Url is not null);
        Assert.Equal("https://www.amazon.com/dp/B07S9J7R2B", link.Url);
        Assert.Equal("amazon.com/dp/B07S9J7R2B", link.Text);
        Assert.DoesNotContain(spans, span => span.Text.Contains('<') || span.Text.Contains('>'));
    }

    [Theory]
    [InlineData("Está en https://example.com/guia.", "https://example.com/guia")]
    [InlineData("(ver https://example.com/a)", "https://example.com/a")]
    [InlineData("https://es.wikipedia.org/wiki/Rat%C3%B3n_(inform%C3%A1tica), dice", "https://es.wikipedia.org/wiki/Rat%C3%B3n_(inform%C3%A1tica)")]
    public void BareUrl_LeavesTrailingPunctuationOutside(string text, string expected)
    {
        var link = Assert.Single(Links(text));
        Assert.Equal(expected, link.Url);
    }

    [Theory]
    [InlineData("[abrir](file:///C:/Windows/system32/calc.exe)")]
    [InlineData("[clic](javascript:alert(1))")]
    [InlineData("<ftp://example.com/archivo>")]
    public void OnlyWebAddressesBecomeLinks(string text) =>
        Assert.Empty(Links(text));

    [Fact]
    public void AMarkdownLinkWhoseTextIsTheAddress_IsShownShort()
    {
        var link = Assert.Single(Links("[https://www.razer.com/es-es/mice/basilisk-v3](https://www.razer.com/es-es/mice/basilisk-v3)"));
        Assert.Equal("razer.com/es-es/mice/basilisk-v3", link.Text);
    }

    [Fact]
    public void TwoLinksSideBySide_StayApart()
    {
        var links = Links("<https://a.com> <https://b.com>");
        Assert.Equal(2, links.Count);
    }

    [Theory]
    [InlineData("**Precio", "**Precio**")]
    [InlineData("Hola **", "Hola ")]
    [InlineData("usa `npm ins", "usa `npm ins`")]
    [InlineData("**ok** y listo", "**ok** y listo")]
    [InlineData("```\nvar x = 1;", "```\nvar x = 1;\n```")]
    [InlineData("- mira [microsoft.com/es-es/p/fab](www.", "- mira ")]
    [InlineData("- mira [microsoft.com", "- mira ")]
    [InlineData("Enlace: <https://www.amaz", "Enlace: ")]
    [InlineData("Enlace: <ht", "Enlace: ")]
    [InlineData("3 < 4 y listo", "3 < 4 y listo")]
    [InlineData("[listo](https://a.com) y más", "[listo](https://a.com) y más")]
    public void WhileStreaming_DanglingMarksAreClosedForDrawing(string text, string expected) =>
        Assert.Equal(expected, AnswerMarkdown.CloseDanglingMarks(text));

    [Fact]
    public void Pacer_NeverShowsHalfAWordWhileTheModelIsStillWriting()
    {
        const string text = "Logitech MX Master es muy cómodo";
        var shown = 0;
        for (var step = 0; step < 40; step++)
        {
            shown = StreamingTextPacer.NextLength(text[..20], shown, finished: false);
            Assert.True(shown == 0 || shown == 20 || char.IsWhiteSpace(text[shown - 1]) || char.IsWhiteSpace(text[shown]),
                $"Cortó dentro de una palabra en {shown}");
        }
    }

    [Fact]
    public void Pacer_ReachesTheEndOnceTheModelFinishes()
    {
        var text = string.Join(' ', Enumerable.Repeat("palabra", 200));
        var shown = 0;
        var steps = 0;
        while (shown < text.Length && steps < 100)
        {
            shown = StreamingTextPacer.NextLength(text, shown, finished: true);
            steps++;
        }

        Assert.Equal(text.Length, shown);
        Assert.True(steps < 30, $"Tardó {steps} pasos en ponerse al día");
    }

    [Fact]
    public void Pacer_WithNothingPending_StaysPut() =>
        Assert.Equal(5, StreamingTextPacer.NextLength("hola ", 5, finished: false));

    [Fact]
    public void FollowUps_HaveShortLabelsAndARealRequest()
    {
        Assert.All(AnswerFollowUps.All, followUp =>
        {
            Assert.InRange(followUp.Label.Length, 3, 16);
            Assert.True(followUp.Prompt.Length > 20);
        });
        Assert.True(AnswerFollowUps.Compact.Count < AnswerFollowUps.All.Count);
    }
}
