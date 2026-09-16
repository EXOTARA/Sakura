using Nexo.Core.Resources;

namespace Nexo.Core.Tests;

/// <summary>
/// 2026-09-16 — regresión del fallo que Adler no conseguía reproducir: el panel lateral se abría,
/// pero el cajón de arriba y el mando de volumen no, sin nada a pantalla completa.
/// </summary>
public sealed class ResourceGovernorFreshnessTests
{
    private static ResourceGovernorDecision Game() =>
        new(ResourceMode.Game, "Un juego está usando la pantalla completa.",
            AllowLocalCommands: true, AllowLocalAi: false, AllowRemoteAi: false, AllowVision: false,
            PauseWakeWord: true, SuppressTransientOverlays: true);

    [Fact]
    public void WithAFreshReading_TheFullScreenDecisionIsObeyed() =>
        Assert.True(ResourceGovernorFreshness.SuppressesOverlays(Game(), TimeSpan.FromSeconds(2)));

    [Fact]
    public void WithAStaleReading_NothingStaysMuted()
    {
        // El ciclo de métricas se paró: nadie ha comprobado si el juego sigue delante, así que la
        // decisión deja de mandar y el cajón y el volumen vuelven a salir.
        Assert.False(ResourceGovernorFreshness.SuppressesOverlays(Game(), TimeSpan.FromMinutes(30)));
        Assert.False(ResourceGovernorFreshness.SuppressesOverlays(Game(), ResourceGovernorFreshness.MaximumAge));
    }

    [Fact]
    public void InNormalMode_NothingIsMuted_HoweverOldTheReadingIs() =>
        Assert.False(ResourceGovernorFreshness.SuppressesOverlays(ResourceGovernorDecision.Normal, TimeSpan.Zero));

    [Fact]
    public void TheLimit_LeavesRoomForSeveralReadings() =>
        // Las métricas se leen cada dos segundos: quince da para siete intentos antes de desconfiar.
        Assert.True(ResourceGovernorFreshness.MaximumAge >= TimeSpan.FromSeconds(10));
}
