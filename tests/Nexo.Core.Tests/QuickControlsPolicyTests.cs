using Nexo.Core.Settings;
using Nexo.Core.Shell;
using Xunit;

namespace Nexo.Core.Tests;

public sealed class QuickControlsPolicyTests
{
    [Theory]
    [InlineData(SidebarPosition.Right, SidebarPosition.Left)]
    [InlineData(SidebarPosition.Left, SidebarPosition.Right)]
    public void TheControlsLiveOnTheEdgeOppositeToSakura(
        SidebarPosition kohana,
        SidebarPosition expected)
    {
        // Si compartieran borde, el mismo gesto tendría que decidir entre abrir Sakura y abrir los
        // mandos. En bordes opuestos no compiten y no hay nada que recordar.
        Assert.Equal(expected, QuickControlsPolicy.ControlsEdgeFor(kohana));
    }

    [Theory]
    [InlineData(true, false, false, 5, true)]   // arrastrando una barra
    [InlineData(false, true, false, 5, true)]   // con el teclado dentro y la ventana delante
    [InlineData(false, false, true, 0, true)]   // el ratón acaba de moverse encima
    [InlineData(false, false, true, 1, true)]
    [InlineData(false, false, true, 2, false)]  // el ratón se quedó parado en el borde
    [InlineData(false, false, false, 0, false)] // nadie lo usa
    public void ThePanelStaysOnlyWhileSomeoneIsUsingIt(
        bool dragging, bool keyboard, bool cursorOver, int stillTicks, bool expected) =>
        Assert.Equal(expected, QuickControlsPolicy.ShouldStay(dragging, keyboard, cursorOver, stillTicks));

    [Fact]
    public void MovingSakuraMovesTheControlsWithIt()
    {
        var left = QuickControlsPolicy.ControlsEdgeFor(SidebarPosition.Left);
        var right = QuickControlsPolicy.ControlsEdgeFor(SidebarPosition.Right);

        Assert.NotEqual(left, right);
    }

    [Theory]
    [InlineData(-40, 0)]
    [InlineData(0, 0)]
    [InlineData(43.4, 43)]
    [InlineData(43.5, 44)]
    [InlineData(100, 100)]
    [InlineData(180, 100)]
    public void APercentIsClampedAndRoundedToWhatTheHardwareAccepts(double input, int expected)
    {
        // Ni el mezclador ni el monitor rechazan un valor fuera de rango: lo aceptan y dejan el
        // sistema en un estado que nadie pidió, así que el recorte tiene que pasar aquí.
        Assert.Equal(expected, QuickControlsPolicy.NormalizePercent(input));
    }

    [Fact]
    public void ANotANumberPercentBecomesZeroInsteadOfPropagating()
    {
        // Una división entre cero calculando el porcentaje de un rango vacío llega hasta aquí; sin
        // esto, el NaN pasaría al hardware convertido en un entero cualquiera.
        Assert.Equal(0, QuickControlsPolicy.NormalizePercent(double.NaN));
    }

    [Fact]
    public void OnlyTheControlsThatCanActuallyDoSomethingAreShown()
    {
        Assert.Equal(
            [QuickControlKind.Volume],
            QuickControlsPolicy.VisibleControls(volumeAvailable: true, brightnessAvailable: false));

        Assert.Equal(
            [QuickControlKind.Volume, QuickControlKind.Brightness],
            QuickControlsPolicy.VisibleControls(volumeAvailable: true, brightnessAvailable: true));
    }

    [Fact]
    public void WithNothingAvailableThereIsNothingToShow()
    {
        // Quien llama usa esto para decidir si abrir el panel: una lista vacía significa no abrirlo,
        // en vez de enseñar una tarjeta con dos mandos muertos.
        Assert.Empty(QuickControlsPolicy.VisibleControls(
            volumeAvailable: false, brightnessAvailable: false));
    }

    [Fact]
    public void VolumeAlwaysComesBeforeBrightness()
    {
        // El orden es el del boceto y no es indiferente: el volumen se toca mucho más, así que va
        // arriba, donde cae el ratón al llegar al borde.
        var controls = QuickControlsPolicy.VisibleControls(true, true);

        Assert.Equal(QuickControlKind.Volume, controls[0]);
    }
}
