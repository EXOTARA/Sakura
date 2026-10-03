using Nexo.Core.AdaptiveEngine;
using Nexo.Core.Shell;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D62 — qué se le pide a DWM y, sobre todo, cuándo no se le pide nada.
///
/// Lo interesante aquí no es el caso bueno (Windows 11 moderno, acrílico) sino los cuatro en los que
/// hay que renunciar al desenfoque: una versión de Windows que no lo tiene, contraste alto,
/// transparencias apagadas por el usuario y modo eco. En los cuatro la ventana tiene que enterarse
/// de que le toca pintar su propio fondo, porque si se queda transparente esperando un fondo que no
/// va a llegar, se ve negra.
/// </summary>
public sealed class WindowBackdropPolicyTests
{
    private static WindowBackdropProbe Probe(
        int build = 26200,
        bool transparency = true,
        bool highContrast = false,
        HardwarePerformanceMode mode = HardwarePerformanceMode.Balanced) =>
        new(build, transparency, highContrast, mode);

    [Fact]
    public void ModernWindows_GetsAcrylicAndRoundCorners()
    {
        var decision = WindowBackdropPolicy.Decide(Probe(), WindowBackdrop.Acrylic);

        Assert.Equal(WindowBackdrop.Acrylic, decision.Backdrop);
        Assert.Equal(WindowCorner.Round, decision.Corner);
        Assert.False(decision.PaintOwnBackground);
    }

    [Fact]
    public void SquareCornersAreHonouredWhereRoundOnesWouldFit()
    {
        // Diseño D62 — Sakura pide rectas: en el arco de las redondeadas el borde de DWM se lee
        // como una mancha oscura, y un canto recto no tiene arco.
        var decision = WindowBackdropPolicy.Decide(Probe(), WindowBackdrop.Acrylic, WindowCorner.Square);

        Assert.Equal(WindowCorner.Square, decision.Corner);
        Assert.Equal(WindowBackdrop.Acrylic, decision.Backdrop);
    }

    [Fact]
    public void OnWindows10_NoCornerIsAskedForEvenWhenOneIsRequested()
    {
        // Ahí no hay atributo que pedir, ni para redondear ni para no hacerlo.
        var decision = WindowBackdropPolicy.Decide(
            Probe(build: 19045),
            WindowBackdrop.Acrylic,
            WindowCorner.Square);

        Assert.Equal(WindowCorner.System, decision.Corner);
    }

    [Fact]
    public void Windows10_GetsNeitherBackdropNorCorners()
    {
        var decision = WindowBackdropPolicy.Decide(Probe(build: 19045), WindowBackdrop.Acrylic);

        Assert.Equal(WindowBackdrop.None, decision.Backdrop);
        Assert.Equal(WindowCorner.System, decision.Corner);
        Assert.True(decision.PaintOwnBackground);
    }

    [Fact]
    public void FirstWindows11_RoundsTheCornersButHasNoBackdrop()
    {
        // 22000 redondea; los fondos del sistema llegaron en 22621. Es la única franja donde una de
        // las dos cosas se puede pedir y la otra no.
        var decision = WindowBackdropPolicy.Decide(
            Probe(build: WindowBackdropPolicy.RoundedCornersBuild),
            WindowBackdrop.Acrylic);

        Assert.Equal(WindowBackdrop.None, decision.Backdrop);
        Assert.Equal(WindowCorner.Round, decision.Corner);
        Assert.True(decision.PaintOwnBackground);
    }

    [Fact]
    public void TransparencyTurnedOffInWindows_IsNotWorkedAround()
    {
        var decision = WindowBackdropPolicy.Decide(Probe(transparency: false), WindowBackdrop.Acrylic);

        Assert.Equal(WindowBackdrop.None, decision.Backdrop);
        Assert.True(decision.PaintOwnBackground);

        // Las esquinas no son un efecto de transparencia: esas se quedan.
        Assert.Equal(WindowCorner.Round, decision.Corner);
    }

    [Fact]
    public void HighContrast_LosesTheBlur()
    {
        var decision = WindowBackdropPolicy.Decide(Probe(highContrast: true), WindowBackdrop.Acrylic);

        Assert.Equal(WindowBackdrop.None, decision.Backdrop);
        Assert.True(decision.PaintOwnBackground);
    }

    [Fact]
    public void EcoMode_DowngradesAcrylicToMicaInsteadOfDroppingIt()
    {
        var decision = WindowBackdropPolicy.Decide(
            Probe(mode: HardwarePerformanceMode.Eco),
            WindowBackdrop.Acrylic);

        Assert.Equal(WindowBackdrop.Mica, decision.Backdrop);
        Assert.False(decision.PaintOwnBackground);
    }

    [Fact]
    public void EcoMode_LeavesMicaAlone()
    {
        var decision = WindowBackdropPolicy.Decide(
            Probe(mode: HardwarePerformanceMode.Eco),
            WindowBackdrop.Mica);

        Assert.Equal(WindowBackdrop.Mica, decision.Backdrop);
    }

    [Fact]
    public void AWindowThatAsksForNothing_StillGetsItsCornersRounded()
    {
        var decision = WindowBackdropPolicy.Decide(Probe(), WindowBackdrop.None);

        Assert.Equal(WindowBackdrop.None, decision.Backdrop);
        Assert.Equal(WindowCorner.Round, decision.Corner);
        Assert.True(decision.PaintOwnBackground);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, false, true)]
    [InlineData(true, true, true)]
    [InlineData(false, true, true)]
    public void GlassWindowsGoOpaqueWithoutTransparencyOrWithHighContrast(
        bool transparency, bool highContrast, bool expected) =>
        Assert.Equal(
            expected,
            WindowBackdropPolicy.RequiresOpaqueSurface(Probe(transparency: transparency, highContrast: highContrast)));
}
