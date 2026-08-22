using Nexo.Core.Voice;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D74 — "nada" cierra la escucha, y una orden que contenga la palabra "nada" no.
///
/// Esa segunda mitad es la que importa: una cancelación que salta por una palabra suelta en mitad
/// de una frase es peor que no tener cancelación, porque se lleva por delante órdenes de verdad.
/// </summary>
public sealed class VoiceDismissalPolicyTests
{
    [Theory]
    [InlineData("nada")]
    [InlineData("Nada.")]
    [InlineData("¡nada!")]
    [InlineData("nada, gracias")]
    [InlineData("olvídalo")]
    [InlineData("déjalo así")]
    [InlineData("ya no")]
    [InlineData("cancela")]
    [InlineData("no importa")]
    [InlineData("sakura, nada")]
    [InlineData("nada sakura")]
    public void TheseCloseTheListening(string text) =>
        Assert.True(VoiceDismissalPolicy.IsDismissal(text), $"“{text}” debería cerrar la escucha.");

    [Theory]
    [InlineData("abre la carpeta nada")]
    [InlineData("no encuentro nada en el escritorio")]
    [InlineData("recuérdame que no hay nada pendiente")]
    [InlineData("busca nada en internet")]
    [InlineData("abre powershell")]
    [InlineData("")]
    [InlineData(null)]
    public void TheseAreOrdersAndMustSurvive(string? text) =>
        Assert.False(VoiceDismissalPolicy.IsDismissal(text), $"“{text}” no puede cerrar la escucha.");
}
