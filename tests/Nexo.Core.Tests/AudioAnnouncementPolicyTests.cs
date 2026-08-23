using Nexo.Core.Audio;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D77 — cuándo avisar de un cambio de audio.
///
/// El defecto que motivó esto se ve usando la aplicación y no leyéndola: mover el deslizador de
/// volumen sacaba una cápsula por cada paso y dejaba una entrada por cada paso en «acciones
/// recientes». Ajustar el volumen no es un acto único —se toca, se escucha, se corrige— así que un
/// aviso por cambio acaba siendo cinco avisos por decisión.
/// </summary>
public sealed class AudioAnnouncementPolicyTests
{
    [Fact]
    public void WhatYouJustDidWithYourOwnHand_IsNotAnnouncedBackToYou()
    {
        // El deslizador se movió, el número cambió y el sonido cambió. La cápsula sobra.
        Assert.False(
            AudioAnnouncementPolicy.ShouldAnnounce(
                AudioActionStatus.Success, adjustedByHand: true));
    }

    [Fact]
    public void AChangeFromSomewhereElse_IsAlwaysAnnounced()
    {
        // Voz, rutina o paleta: aquí la cápsula es la única realimentación que existe, y quitarla
        // dejaría a alguien sin saber si su orden llegó a hacer algo.
        Assert.True(
            AudioAnnouncementPolicy.ShouldAnnounce(
                AudioActionStatus.Success, adjustedByHand: false));
    }

    [Theory]
    [InlineData(AudioActionStatus.NotFound)]
    [InlineData(AudioActionStatus.Unavailable)]
    [InlineData(AudioActionStatus.Failed)]
    public void WhatFailed_IsAnnouncedEvenIfYouDidItYourself(AudioActionStatus status)
    {
        // Un deslizador que se mueve mientras el volumen real no cambia es precisamente el caso en
        // el que hay que decirlo: sin aviso, la única señal disponible es el silencio.
        Assert.True(AudioAnnouncementPolicy.ShouldAnnounce(status, adjustedByHand: true));
        Assert.True(AudioAnnouncementPolicy.ShouldAnnounce(status, adjustedByHand: false));
    }
}
