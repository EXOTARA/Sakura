using Nexo.Core.Shell;

namespace Nexo.Core.Tests;

public sealed class DrawerClearancePolicyTests
{
    // Un shell de 560 x 1032 pegado al borde derecho de una pantalla de 1920, como el de Adler.
    private static readonly ScreenBounds Shell = new(1336, 24, 560, 1032);

    [Fact]
    public void DrawerOverTheShell_PushesItJustBelowWithAGap()
    {
        // Panel superior centrado de 1180 de ancho: de 370 a 1550, y 586 de alto.
        var drawer = new ScreenBounds(370, 0, 1180, 586);

        var inset = DrawerClearancePolicy.TopInset(Shell, drawer);

        Assert.Equal(586 + DrawerClearancePolicy.Gap - 24, inset);
    }

    [Fact]
    public void DrawerThatDoesNotReachTheShellSideways_ChangesNothing()
    {
        var drawer = new ScreenBounds(370, 0, 900, 586); // termina en 1270, antes de 1336

        Assert.Equal(0, DrawerClearancePolicy.TopInset(Shell, drawer));
    }

    [Fact]
    public void DrawerAboveTheShell_ChangesNothing()
    {
        var drawer = new ScreenBounds(370, -600, 1180, 580); // termina en -20, por encima de 24

        Assert.Equal(0, DrawerClearancePolicy.TopInset(Shell, drawer));
    }

    [Fact]
    public void AVeryTallDrawer_NeverSquashesTheShellBelowItsMinimum()
    {
        var drawer = new ScreenBounds(370, 0, 1180, 1000);

        var inset = DrawerClearancePolicy.TopInset(Shell, drawer);

        Assert.Equal(Shell.Height - DrawerClearancePolicy.MinimumShellHeight, inset);
    }

    [Fact]
    public void ShellOnAnotherMonitor_ChangesNothing()
    {
        var otherMonitorShell = new ScreenBounds(3256, 24, 560, 1032);
        var drawer = new ScreenBounds(370, 0, 1180, 586);

        Assert.Equal(0, DrawerClearancePolicy.TopInset(otherMonitorShell, drawer));
    }
}
