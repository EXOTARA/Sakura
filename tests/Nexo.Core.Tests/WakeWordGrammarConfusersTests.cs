using Nexo.Core.Voice;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D88 — las palabras con las que Vosk puede escribir lo que oye en vez de «sakura».
///
/// Lo que importa de verdad se midió con audio (scripts/voice/WakeBench). Estas pruebas guardan las
/// dos condiciones sin las que esa medida deja de valer: que ninguna de estas palabras pueda
/// despertar a Sakura por sí misma, y que todas lleguen al reconocedor.
/// </summary>
public sealed class WakeWordGrammarConfusersTests
{
    public static TheoryData<WakeWordPhrase, WakeWordSensitivity> SakuraModes()
    {
        var data = new TheoryData<WakeWordPhrase, WakeWordSensitivity>();
        foreach (var phrase in new[] { WakeWordPhrase.OyeSakura, WakeWordPhrase.HeySakura, WakeWordPhrase.Sakura })
        {
            // Alta queda fuera a propósito: acepta cualquier palabra a dos letras de «sakura», y
            // «basura» o «segura» lo están. Antes de esto Vosk ya las escribía como «sakura» en ese
            // modo, así que no se abre nada que no estuviera abierto.
            data.Add(phrase, WakeWordSensitivity.Balanced);
            data.Add(phrase, WakeWordSensitivity.Strict);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(SakuraModes))]
    public void NoConfuser_WakesSakura_AloneOrAfterAPrefix(
        WakeWordPhrase phrase,
        WakeWordSensitivity sensitivity)
    {
        var confusers = WakeWordGrammarConfusers.For(phrase);
        Assert.NotEmpty(confusers);

        foreach (var word in confusers)
        {
            foreach (var text in new[] { word, $"oye {word}", $"hey {word}", $"hoy {word}" })
            {
                var result = WakeWordTextMatcher.Evaluate(text, phrase, sensitivity);
                Assert.False(result.IsMatch, $"«{text}» no debería despertar a Sakura ({phrase}, {sensitivity}).");
            }
        }
    }

    [Theory]
    [InlineData(WakeWordPhrase.OyeSakura)]
    [InlineData(WakeWordPhrase.HeySakura)]
    [InlineData(WakeWordPhrase.Sakura)]
    public void Grammar_KeepsEveryFormOfThePhrase_AndEndsInUnknown(WakeWordPhrase phrase)
    {
        var forms = WakeWordTextMatcher.GetGrammarPhrases(phrase, WakeWordSensitivity.Balanced);

        var grammar = WakeWordGrammarConfusers.ComposeGrammar(phrase, forms);

        Assert.All(forms, form => Assert.Contains(form, grammar));
        Assert.All(WakeWordGrammarConfusers.For(phrase), word => Assert.Contains(word, grammar));
        Assert.Equal("[unk]", grammar[^1]);
        Assert.Equal(grammar.Count, grammar.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData(WakeWordPhrase.OyeKohana)]
    [InlineData(WakeWordPhrase.Nexo)]
    public void LegacyPhrases_KeepTheirGrammarUntouched(WakeWordPhrase phrase)
    {
        var forms = WakeWordTextMatcher.GetGrammarPhrases(phrase, WakeWordSensitivity.Balanced);

        var grammar = WakeWordGrammarConfusers.ComposeGrammar(phrase, forms);

        Assert.Empty(WakeWordGrammarConfusers.For(phrase));
        Assert.Equal(forms.Append("[unk]"), grammar);
    }

    [Fact]
    public void EveryConfuser_IsPlainAscii_SoItActuallyReachesVosk()
    {
        // La gramática viaja como JSON y el serializador escapa lo que no es ASCII: «café» llega a
        // Vosk como «café», que no está en su vocabulario, y la descarta sin avisar. Una palabra
        // con tilde aquí sería una palabra que parece estar y no está.
        foreach (var word in WakeWordGrammarConfusers.For(WakeWordPhrase.OyeSakura))
        {
            Assert.True(word.All(c => c is >= 'a' and <= 'z'), $"«{word}» no es ASCII en minúsculas.");
        }
    }
}
