using Nexo.Core.Voice;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D81 — la pestaña Voz existe para contestar «¿por qué no me está oyendo?».
///
/// Lo que se fija aquí es que las tres causas de que no conteste —escucha apagada, sin micrófono,
/// modelos sin preparar— se marquen, y que nada más lo haga. Una lista donde todo pide atención no
/// señala nada.
/// </summary>
public sealed class VoiceGlancePolicyTests
{
    private static IReadOnlyList<VoiceGlanceRow> Healthy() =>
        VoiceGlancePolicy.Describe(
            wakeWordEnabled: true,
            wakeWordPhrase: "Oye Sakura",
            modelsReady: true,
            inputDeviceName: "Micrófono (Realtek)");


    [Fact]
    public void WhenEverythingWorks_NothingAsksForAttention()
    {
        Assert.All(Healthy(), row => Assert.False(row.NeedsAttention, row.Label));
    }

    [Fact]
    public void TheListeningRow_SaysWhichPhraseItIsWaitingFor()
    {
        var listening = Healthy()[0];

        Assert.Equal("Escucha", listening.Label);
        Assert.Contains("Oye Sakura", listening.Value);
    }

    [Theory]
    [InlineData(false, true, "Micrófono (Realtek)", "Escucha")]
    [InlineData(true, true, null, "Micrófono")]
    public void EachReasonSakuraCannotHearYou_IsMarked(
        bool wakeWordEnabled,
        bool modelsReady,
        string? device,
        string expectedLabel)
    {
        var rows = VoiceGlancePolicy.Describe(
            wakeWordEnabled, "Oye Sakura", modelsReady, device);

        var flagged = rows.Where(row => row.NeedsAttention).ToList();

        Assert.Single(flagged);
        Assert.Equal(expectedLabel, flagged[0].Label);
    }

    [Fact]
    public void ModelsNotDownloadedYet_AreNotAProblem_TheyComeOnFirstUse()
    {
        var rows = VoiceGlancePolicy.Describe(
            wakeWordEnabled: true, "Oye Sakura", modelsReady: false, "Micrófono (Realtek)");

        var models = rows.Single(row => row.Label == "Modelos de voz");
        Assert.False(models.NeedsAttention);
        Assert.Equal("Se descargan al usar la voz", models.Value);
    }
}
