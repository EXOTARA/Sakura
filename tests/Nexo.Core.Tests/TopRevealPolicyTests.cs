using Nexo.Core.Shell;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D44 — la zona del centro del borde de arriba que baja el cajón.
///
/// Lo que se comprueba aquí no es que detecte el borde, que es lo fácil, sino que **no** se dispare
/// donde estorbaría: las dos esquinas de arriba pertenecen a la ventana activa —ahí están su barra
/// de título y su botón de cerrar— y a Windows.
/// </summary>
public sealed class TopRevealPolicyTests
{
    private static TopRevealProbe At(double x, double y) => new(x, y, 0, 1920, 0);

    [Fact]
    public void DeadCentreOfTheTopEdge_IsInside() =>
        Assert.True(TopRevealPolicy.IsInHotZone(At(960, 0)));

    [Fact]
    public void JustBelowTheStrip_IsOutside()
    {
        // La franja es [borde, borde + alto): mide exactamente su alto, y el primero de fuera
        // nunca cuenta como dentro.
        var last = TopRevealPolicy.HotZoneHeight - 1;
        Assert.True(TopRevealPolicy.IsInHotZone(At(960, last)));
        Assert.False(TopRevealPolicy.IsInHotZone(At(960, last + 1)));
    }

    [Fact]
    public void OnASharedTopEdge_TheStripKeepsItsOldHeight()
    {
        // Monitores apilados: el cursor cruza sin frenar (D38 en vertical), 4 px serían inalcanzables.
        var shared = At(960, 0) with { OnDesktopEdge = false };
        Assert.True(TopRevealPolicy.IsInHotZone(shared with { CursorY = 19 }));
        Assert.False(TopRevealPolicy.IsInHotZone(shared with { CursorY = 20 }));
        Assert.True(TopRevealPolicy.IsInHotZone(shared with { CursorY = 10 }));
    }

    [Fact]
    public void TheNearZone_IsWiderThanAnyStrip_AndFollowsTheCentreThird()
    {
        Assert.True(TopRevealPolicy.IsNearHotZone(At(960, TopRevealPolicy.RearmDistance - 1)));
        Assert.False(TopRevealPolicy.IsNearHotZone(At(960, TopRevealPolicy.RearmDistance)));
        Assert.False(TopRevealPolicy.IsNearHotZone(At(100, 2)));
        Assert.True(TopRevealPolicy.RearmDistance > TopRevealPolicy.SharedHotZoneHeight);
    }

    [Fact]
    public void TheStripIsNarrow_ADraggedWindowTitleBarDoesNotReachIt()
    {
        // 2026-09-18 — con 20 px, arrastrar una ventana por su barra de título rozaba la franja.
        Assert.True(TopRevealPolicy.HotZoneHeight <= 4);
        Assert.False(TopRevealPolicy.IsInHotZone(At(960, 5)));
        Assert.False(TopRevealPolicy.IsInHotZone(At(960, 19)));
    }

    [Fact]
    public void TheTopCorners_AreNotOurs()
    {
        // Arriba a la derecha está el botón de cerrar de cualquier ventana maximizada. Bajar un
        // panel encima de él al ir a cerrar algo sería insufrible.
        Assert.False(TopRevealPolicy.IsInHotZone(At(4, 2)));
        Assert.False(TopRevealPolicy.IsInHotZone(At(1916, 2)));
    }

    [Fact]
    public void TheZoneIsTheCentreThird()
    {
        // 1920 de ancho: el tercio central va de 640 a 1280.
        Assert.False(TopRevealPolicy.IsInHotZone(At(639, 2)));
        Assert.True(TopRevealPolicy.IsInHotZone(At(641, 2)));
        Assert.True(TopRevealPolicy.IsInHotZone(At(1279, 2)));
        Assert.False(TopRevealPolicy.IsInHotZone(At(1281, 2)));
    }

    [Fact]
    public void OnASecondMonitor_TheZoneFollowsItsOwnEdge()
    {
        // El área de trabajo del monitor derecho empieza en 1920, no en cero. Calcular el centro
        // sobre el escritorio entero pondría la zona en el borde compartido entre las dos pantallas,
        // que es justo por donde uno cruza sin querer entrar en nada.
        var second = new TopRevealProbe(2880, 2, 1920, 3840, 0);
        Assert.True(TopRevealPolicy.IsInHotZone(second));

        var seam = new TopRevealProbe(1930, 2, 1920, 3840, 0);
        Assert.False(TopRevealPolicy.IsInHotZone(seam));
    }

    [Fact]
    public void AboveTheWorkArea_IsOutside()
    {
        // Con la barra de tareas arriba, el área de trabajo no empieza en cero: lo que queda por
        // encima es de la barra.
        var probe = new TopRevealProbe(960, 10, 0, 1920, 48);
        Assert.False(TopRevealPolicy.IsInHotZone(probe));
        Assert.True(TopRevealPolicy.IsInHotZone(probe with { CursorY = 50 }));
    }

    [Theory]
    [InlineData(1920, 1017.6)]
    [InlineData(3840, TopRevealPolicy.MaximumWidth)]
    [InlineData(1024, 620)]
    public void ResolveWidth_IsHalfTheScreenWithinLimits(double workArea, double expected) =>
        Assert.Equal(expected, TopRevealPolicy.ResolveWidth(workArea), 1);

    [Fact]
    public void ResolveWidth_WithoutAScreen_FallsBackToTheMinimum() =>
        Assert.Equal(TopRevealPolicy.MinimumWidth, TopRevealPolicy.ResolveWidth(0));
}
