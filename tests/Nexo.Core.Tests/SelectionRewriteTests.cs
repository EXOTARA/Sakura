using Nexo.Core.Assistant;
using Xunit;

namespace Nexo.Core.Tests;

public sealed class SelectionRewriteTests
{
    [Fact]
    public void NothingSelected_SaysWhatToDo()
    {
        var (canRun, detail) = SelectionRewrite.Check("  ", aiEnabled: true);

        Assert.False(canRun);
        Assert.Contains("Selecciona", detail);
    }

    [Fact]
    public void WithoutAi_ItSaysSo() =>
        Assert.False(SelectionRewrite.Check("hola", aiEnabled: false).CanRun);

    [Fact]
    public void TooLong_IsRefused() =>
        Assert.False(SelectionRewrite.Check(new string('a', SelectionRewrite.MaximumLength + 1), aiEnabled: true).CanRun);

    [Fact]
    public void ThePrompt_AsksOnlyForTheResult_AndCarriesTheText()
    {
        var prompt = SelectionRewrite.BuildPrompt(SelectionAction.ToEnglish, "Hola, ¿cómo estás?");

        Assert.Contains("inglés", prompt);
        Assert.Contains("solo el resultado", prompt);
        Assert.EndsWith("Hola, ¿cómo estás?", prompt);
    }

    [Theory]
    [InlineData("\"Hello\"", "Hello")]
    [InlineData("«Hola»", "Hola")]
    [InlineData("  Texto  ", "Texto")]
    public void Clean_RemovesWrappingQuotes(string raw, string expected) =>
        Assert.Equal(expected, SelectionRewrite.Clean(raw));

    [Fact]
    public void EveryAction_HasALabel() =>
        Assert.All(SelectionRewrite.All, action => Assert.False(string.IsNullOrWhiteSpace(SelectionRewrite.Label(action))));
}
