using Nexo.Core.Voice;

namespace Nexo.Core.Tests;

public sealed class SpanishVoiceTranscriptNormalizerTests
{
    [Theory]
    [InlineData("Avady PowerShell.", "abre powershell")]
    [InlineData("¡Avedit PowerShell!", "abre powershell")]
    [InlineData("Avady Powershield.", "abre powershell")]
    [InlineData("como está mi pece", "como esta mi pc")]
    [InlineData("como anda mi pic", "como esta mi pc")]
    [InlineData("qué bien soy", "que dia es hoy")]
    [InlineData("qué día soy", "que dia es hoy")]
    [InlineData("qué hora es", "que hora es")]
    [InlineData("muestra pic", "muestra peek")]
    [InlineData("enséñame el pick", "muestra peek")]
    [InlineData("Su vez potify al 50.", "sube spotify al 50")]
    [InlineData("bajas Spotify al 50", "baja spotify al 50")]
    [InlineData("bájale a Spotify", "baja a spotify")]
    [InlineData("Sakura, abre PowerShell", "abre powershell")]
    [InlineData("Oye Sakura muestra Peek", "muestra peek")]
    [InlineData("Nexo, abre PowerShell", "abre powershell")]
    [InlineData("Oye Nexo muestra Peek", "muestra peek")]
    [InlineData("Nexo qué día es hoy", "que dia es hoy")]
    [InlineData("Ahí Nexo, qué es esto", "que es esto")]
    [InlineData("Ey Neso abre calculadora", "abre calculadora")]
    [InlineData("Hey Nexo", "")]
    [InlineData("oy sakura que es el actual presidente", "que es el actual presidente")]
    [InlineData("oy esa sakura cual es el estado mas grande de mexico", "cual es el estado mas grande de mexico")]
    [InlineData("Hoy Sakura, abre PowerShell", "abre powershell")]
    public void Normalize_CorrectsFrequentShortCommandErrors(
        string transcript,
        string expected)
    {
        var result = SpanishVoiceTranscriptNormalizer.Normalize(transcript);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Explícame para qué sirve PowerShell en Windows",
        "explicame para que sirve powershell en windows")]
    [InlineData("por qué bajas Spotify cuando abro un juego",
        "por que bajas spotify cuando abro un juego")]
    public void Normalize_DoesNotRewriteOpenQuestions(string transcript, string expected)
    {
        var result = SpanishVoiceTranscriptNormalizer.Normalize(transcript);

        Assert.Equal(expected, result);
    }
}
