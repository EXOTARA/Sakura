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
    public void FileName_TwoExportsInTheSameMinute_GiveTheSameName()
    {
        // Medido: el nombre lleva la hora al minuto, así que dos exportaciones seguidas comparten
        // nombre. Por eso al escribir hace falta numerar (FreshFileWriter): esta prueba deja
        // documentado el porqué y avisa si algún día el nombre cambia.
        var first = ConversationExport.FileName(Messages, Now.AddSeconds(10));
        var second = ConversationExport.FileName(Messages, Now.AddSeconds(55));

        Assert.Equal(first, second);
    }

    [Fact]
    public void FileName_WithoutQuestions_HasAFallback() =>
        Assert.EndsWith("- Conversación.md", ConversationExport.FileName([], Now));
}
