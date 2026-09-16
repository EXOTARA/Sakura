using Nexo.Core.Assistant;
using Xunit;

namespace Nexo.Core.Tests;

public sealed class ConversationExportTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 15, 30, 0, TimeSpan.FromHours(-6));

    private static readonly ConversationMessage[] Messages =
    [
        new(ConversationRole.User, "¿Cómo resuelvo: la bisección? paso a paso", Now.AddMinutes(-2)),
        new(ConversationRole.Assistant, "Primero eliges un intervalo.", Now.AddMinutes(-1))
    ];

    [Fact]
    public void Markdown_KeepsWhoSaidWhat_InOrder()
    {
        var markdown = ConversationExport.ToMarkdown(Messages, Now);

        Assert.StartsWith("# Conversación con Sakura", markdown);
        Assert.True(markdown.IndexOf("## Tú · 15:28", StringComparison.Ordinal) < markdown.IndexOf("## Sakura · 15:29", StringComparison.Ordinal));
        Assert.Contains("Primero eliges un intervalo.", markdown);
    }

    [Fact]
    public void FileName_UsesTheFirstQuestion_WithoutForbiddenCharacters()
    {
        var name = ConversationExport.FileName(Messages, Now);

        Assert.Equal("2026-09-16 15.30 - ¿Cómo resuelvo la bisección paso a.md", name);
        Assert.DoesNotContain(':', name);
    }

    [Fact]
    public void FileName_WithoutQuestions_HasAFallback() =>
        Assert.EndsWith("- Conversación.md", ConversationExport.FileName([], Now));
}
