using Nexo.Core.Automation;
using Nexo.Core.Commands;
using Nexo.Core.Focus;
using Nexo.Core.Tasks;
using Nexo.Core.Voice;

namespace Nexo.Core.Tests;

public sealed class VoiceCommandExamplesTests
{
    public static TheoryData<string> AllPhrases()
    {
        var data = new TheoryData<string>();
        foreach (var phrase in VoiceCommandExamples.Groups.SelectMany(group => group.Phrases))
        {
            data.Add(phrase);
        }

        return data;
    }

    /// <summary>
    /// Cada ejemplo que enseña la pestaña Voz se resuelve sin IA, por la misma cadena de despacho que
    /// la ventana principal y en una instalación limpia (sin rutinas). Si una frase deja de funcionar
    /// en local, esta prueba lo dice antes de que alguien la lea en el panel y no le conteste.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllPhrases))]
    public void EveryExample_IsHandledLocally(string phrase)
    {
        var decision = PromptDispatchPolicy.Resolve(
            new SpanishRoutineCommandParser().Parse(phrase),
            new SpanishFocusCommandParser().Parse(phrase),
            new SpanishTaskCommandParser().Parse(phrase, new DateTimeOffset(2026, 9, 14, 12, 0, 0, TimeSpan.FromHours(-6))),
            new NaturalCommandParser().Parse(phrase),
            _ => false);

        Assert.False(decision.GoesToAi, $"«{phrase}» acabaría en la IA: {decision.Reason}");
    }

    [Fact]
    public void AFewGroups_EachWithAFewPhrases()
    {
        Assert.InRange(VoiceCommandExamples.Groups.Count, 3, 6);
        Assert.All(VoiceCommandExamples.Groups, group => Assert.InRange(group.Phrases.Count, 1, 3));
    }
}
