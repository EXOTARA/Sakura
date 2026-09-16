using Nexo.Core.Ai;

namespace Nexo.Core.Tests;

public sealed class AiProviderLimitsTests
{
    /// <summary>El mensaje exacto que le salió a Adler en el chat, dos veces seguidas.</summary>
    private const string GroqError =
        "Request too large for model `qwen/qwen3.8-27b` in organization org_01m04x1etzesf8myk6hmee5nc4 " +
        "service tier on_demand on output tokens per minute (OTPM): Limit 1000, Requested 1935. " +
        "The request's expected output tokens exceed the enforced limit; reduce max_tokens and try again.";

    [Fact]
    public void TheProviderSaysHowMuchItAdmits_SoSeAskForALittleLess()
    {
        Assert.True(AiProviderLimits.IsOutputLimit(GroqError));
        Assert.Equal(872, AiProviderLimits.OutputLimit(GroqError));
    }

    [Theory]
    [InlineData("Limit 400, Requested 1200. reduce max_tokens", 272)]
    // Nunca se pide tan poco que no quepa una respuesta.
    [InlineData("tokens per minute (OTPM): Limit 100, Requested 900", 256)]
    public void TheRetry_NeverAsksForLessThanAnAnswerNeeds(string error, int expected) =>
        Assert.Equal(expected, AiProviderLimits.OutputLimit(error));

    [Theory]
    // Otros fallos del proveedor no se confunden con este.
    [InlineData("El proveedor rechazó la solicitud (401 Unauthorized): Invalid API Key")]
    [InlineData("messages[5].content must be a string")]
    [InlineData("Rate limit reached for requests per minute, please try again later")]
    [InlineData(null)]
    public void OtherFailures_AreNotMistakenForThisOne(string? error)
    {
        Assert.False(AiProviderLimits.IsOutputLimit(error));
        Assert.Null(AiProviderLimits.OutputLimit(error));
    }

    [Fact]
    public void AFollowUpButton_IsRecognised_SoItsQuestionDoesNotCarryTheScreenshot()
    {
        Assert.True(Nexo.Core.Assistant.AnswerFollowUps.IsFollowUp("Explícamelo más fácil, como a alguien que no sabe del tema."));
        Assert.True(Nexo.Core.Assistant.AnswerFollowUps.IsFollowUp("  Dímelo más corto, solo con lo esencial.  "));
        Assert.False(Nexo.Core.Assistant.AnswerFollowUps.IsFollowUp("¿Qué dice este error?"));
        Assert.False(Nexo.Core.Assistant.AnswerFollowUps.IsFollowUp(null));
    }

    [Fact]
    public void WhenEvenTheRetryDoesNotFit_ItIsExplainedInSpanish()
    {
        Assert.Contains("872 tokens por minuto", AiProviderLimits.Explain(872));
        Assert.Contains("más corto", AiProviderLimits.Explain(null));
    }
}
