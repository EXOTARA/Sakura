using Nexo.Core.Settings;
using Nexo.Core.Shell;
using Xunit;

namespace Nexo.Core.Tests;

/// <summary>
/// Un borde de pantalla es un sitio disputado: ahí están la barra de desplazamiento de cualquier
/// ventana maximizada, el gesto de acoplar de Windows y, en las esquinas, el menú Inicio y el reloj.
/// Estas pruebas fijan dónde SÍ y dónde NO debe reaccionar Sakura, porque el fallo caro no es que no
/// aparezca: es que aparezca cuando alguien iba a otra cosa.
/// </summary>
public sealed class EdgeRevealPolicyTests
{
    private const double Left = 0;
    private const double Right = 1920;
    private const double Top = 0;
    private const double Bottom = 1000;

    private static EdgeRevealProbe At(double x, double y, SidebarPosition side) =>
        new(x, y, Left, Right, Top, Bottom, side);

    [Fact]
    public void TouchingTheDockedEdgeCounts()
    {
        Assert.True(EdgeRevealPolicy.IsInHotZone(At(Right, 500, SidebarPosition.Right)));
        Assert.True(EdgeRevealPolicy.IsInHotZone(At(Left, 500, SidebarPosition.Left)));
    }

    [Fact]
    public void TheOppositeEdgeDoesNothing()
    {
        // Sakura solo asoma por el lado en el que está. Reaccionar en el otro borde sería aparecer
        // desde donde nadie la llamó.
        Assert.False(EdgeRevealPolicy.IsInHotZone(At(Left, 500, SidebarPosition.Right)));
        Assert.False(EdgeRevealPolicy.IsInHotZone(At(Right, 500, SidebarPosition.Left)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void TheWholeHotZoneWidthIsSensitive(double distanceFromEdge)
    {
        Assert.True(EdgeRevealPolicy.IsInHotZone(
            At(Right - distanceFromEdge, 500, SidebarPosition.Right)));
        Assert.True(EdgeRevealPolicy.IsInHotZone(
            At(Left + distanceFromEdge, 500, SidebarPosition.Left)));
    }

    [Fact]
    public void JustPastTheHotZoneIsAlreadyTooFar()
    {
        // El primer píxel fuera de la franja es donde empieza la barra de desplazamiento de una
        // ventana maximizada. Si la franja se desbordara aunque fuera un píxel, desplazarse por
        // cualquier página abriría Sakura.
        Assert.False(EdgeRevealPolicy.IsInHotZone(
            At(Right - EdgeRevealPolicy.HotZoneWidth, 500, SidebarPosition.Right)));
        Assert.False(EdgeRevealPolicy.IsInHotZone(
            At(Left + EdgeRevealPolicy.HotZoneWidth, 500, SidebarPosition.Left)));
    }

    [Fact]
    public void CursorBeyondTheScreenIsNotInTheZone()
    {
        // Con varios monitores, el ratón puede estar más allá de este borde porque hay otra pantalla
        // al lado. Ahí no está pidiendo Sakura: está yendo a la otra pantalla.
        Assert.False(EdgeRevealPolicy.IsInHotZone(At(Right + 1, 500, SidebarPosition.Right)));
        Assert.False(EdgeRevealPolicy.IsInHotZone(At(Left - 1, 500, SidebarPosition.Left)));
    }

    [Fact]
    public void OnTheOuterDesktopEdge_TheStripIsNarrow()
    {
        // 2026-09-18 — solo cuenta el borde extremo: seis píxeles, justo dentro y justo fuera.
        var w = EdgeRevealPolicy.ExteriorHotZoneWidth;
        Assert.True(EdgeRevealPolicy.IsInHotZone(At(Left + w - 1, 500, SidebarPosition.Left)));
        Assert.False(EdgeRevealPolicy.IsInHotZone(At(Left + w, 500, SidebarPosition.Left)));
        // Right es exclusivo: los píxeles reales son Right-w .. Right-1, w en total, como a la izquierda.
        Assert.True(EdgeRevealPolicy.IsInHotZone(At(Right - w, 500, SidebarPosition.Right)));
        Assert.False(EdgeRevealPolicy.IsInHotZone(At(Right - w - 1, 500, SidebarPosition.Right)));
    }

    [Fact]
    public void OnASharedEdge_TheStripKeepsTheWideD38Width()
    {
        // D38: entre dos monitores el cursor cruza sin frenar; estrechar aquí lo haría inservible.
        var w = EdgeRevealPolicy.HotZoneWidth;
        var left = At(Left + w - 1, 500, SidebarPosition.Left) with { OnDesktopEdge = false };
        var beyondLeft = At(Left + w, 500, SidebarPosition.Left) with { OnDesktopEdge = false };
        Assert.True(EdgeRevealPolicy.IsInHotZone(left));
        Assert.False(EdgeRevealPolicy.IsInHotZone(beyondLeft));

        var right = At(Right - w, 500, SidebarPosition.Right) with { OnDesktopEdge = false };
        Assert.True(EdgeRevealPolicy.IsInHotZone(right));
        Assert.False(EdgeRevealPolicy.IsInHotZone(right with { CursorX = Right - w - 1 }));
    }

    [Fact]
    public void TheOuterStripIsNarrowerThanTheSharedOne() =>
        Assert.True(EdgeRevealPolicy.ExteriorHotZoneWidth < EdgeRevealPolicy.HotZoneWidth);

    [Fact]
    public void TheCornersAndMiddleAreUnchangedOnTheOuterEdge()
    {
        Assert.False(EdgeRevealPolicy.IsInHotZone(At(Left, 99, SidebarPosition.Left)));
        Assert.False(EdgeRevealPolicy.IsInHotZone(At(Left, 901, SidebarPosition.Left)));
        Assert.True(EdgeRevealPolicy.IsInHotZone(At(Left, 500, SidebarPosition.Left)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(99)]
    public void TheTopCornerBelongsToWindows(double y)
    {
        Assert.False(EdgeRevealPolicy.IsInHotZone(At(Right, y, SidebarPosition.Right)));
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(950)]
    [InlineData(901)]
    public void TheBottomCornerBelongsToWindows(double y)
    {
        // Abajo a la derecha está "mostrar escritorio" y el reloj; abajo a la izquierda, Inicio.
        Assert.False(EdgeRevealPolicy.IsInHotZone(At(Right, y, SidebarPosition.Right)));
        Assert.False(EdgeRevealPolicy.IsInHotZone(At(Left, y, SidebarPosition.Left)));
    }

    [Fact]
    public void TheMiddleOfTheEdgeIsTheUsableStretch()
    {
        // Entre las dos esquinas excluidas debe quedar sitio de sobra, o la función sería imposible
        // de acertar. Con un décimo excluido arriba y otro abajo, quedan ocho décimos.
        for (var y = 101d; y <= 899d; y += 50)
        {
            Assert.True(
                EdgeRevealPolicy.IsInHotZone(At(Right, y, SidebarPosition.Right)),
                $"El borde debería responder a la altura {y}.");
        }
    }

    [Fact]
    public void ADegenerateWorkAreaNeverTriggers()
    {
        // Puede pasar de verdad: al bloquear la sesión o al desconectar un monitor, Windows llega a
        // informar rectángulos vacíos. Sin esta salida, la división por el alto daría un margen
        // absurdo y la franja se comportaría de forma impredecible.
        Assert.False(EdgeRevealPolicy.IsInHotZone(
            new EdgeRevealProbe(0, 0, 0, 0, 0, 0, SidebarPosition.Right)));
        Assert.False(EdgeRevealPolicy.IsInHotZone(
            new EdgeRevealProbe(500, 500, 0, 1920, 500, 500, SidebarPosition.Right)));
    }

    [Fact]
    public void AfterOpening_TheEdgeOnlyRearmsOnceTheMouseTrulyLeaves()
    {
        // 2026-09-16 — con las pestañas del navegador pegadas al borde, el panel de volumen volvía a
        // salir en cuanto se cerraba. Temblar dentro de la franja no cuenta como irse.
        Assert.False(EdgeRevealPolicy.HasLeftEdge(At(Left + 5, 500, SidebarPosition.Left)));
        Assert.False(EdgeRevealPolicy.HasLeftEdge(At(Left + EdgeRevealPolicy.HotZoneWidth + 10, 500, SidebarPosition.Left)));
        Assert.True(EdgeRevealPolicy.HasLeftEdge(At(Left + EdgeRevealPolicy.RearmDistance + 1, 500, SidebarPosition.Left)));

        Assert.False(EdgeRevealPolicy.HasLeftEdge(At(Right - 30, 500, SidebarPosition.Right)));
        Assert.True(EdgeRevealPolicy.HasLeftEdge(At(Right - EdgeRevealPolicy.RearmDistance - 1, 500, SidebarPosition.Right)));
    }

    [Fact]
    public void TheRearmDistance_IsWiderThanTheHotZone() =>
        Assert.True(EdgeRevealPolicy.RearmDistance > EdgeRevealPolicy.HotZoneWidth);

    [Fact]
    public void TheCooldownOutlastsTheDwell()
    {
        // Si la pausa tras ocultar fuera más corta que la permanencia necesaria, cerrar Sakura con
        // el ratón todavía en el borde la volvería a abrir sola.
        Assert.True(EdgeRevealPolicy.CooldownAfterHide > EdgeRevealPolicy.Dwell);
    }

    // Monitor 0..1920 x 0..1080. La barra de tareas reserva 48 px en un borde: rcWork se mete.
    [Theory]
    [InlineData("arriba", false, true, true)]
    [InlineData("izquierda", true, false, true)]
    [InlineData("derecha", true, true, false)]
    [InlineData("abajo", true, true, true)]
    [InlineData("ninguna", true, true, true)]
    public void TaskbarOnAnEdge_OnlyDisablesTheNarrowStripOnThatEdge(
        string taskbar, bool topNarrow, bool leftNarrow, bool rightNarrow)
    {
        double workTop = taskbar == "arriba" ? 48 : 0;
        double workLeft = taskbar == "izquierda" ? 48 : 0;
        double workRight = taskbar == "derecha" ? 1872 : 1920;

        Assert.Equal(topNarrow, EdgeRevealPolicy.UsesNarrowStrip(true, 0, workTop));
        Assert.Equal(leftNarrow, EdgeRevealPolicy.UsesNarrowStrip(true, 0, workLeft));
        Assert.Equal(rightNarrow, EdgeRevealPolicy.UsesNarrowStrip(true, 1920, workRight));
    }

    [Fact]
    public void ANarrowStripNeverAppliesOnASharedDesktopEdge() =>
        Assert.False(EdgeRevealPolicy.UsesNarrowStrip(false, 0, 0));
}
