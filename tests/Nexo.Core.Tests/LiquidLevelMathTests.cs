using Nexo.Core.Shell;

namespace Nexo.Core.Tests;

public sealed class LiquidLevelMathTests
{
    [Fact]
    public void Bump_IsFullAtTheCentreAndFlatAtTheEdges()
    {
        Assert.Equal(1, LiquidLevelMath.Bump(0), 6);
        Assert.Equal(0, LiquidLevelMath.Bump(1), 6);
        Assert.Equal(0, LiquidLevelMath.Bump(-1.5), 6);
        Assert.Equal(0.5, LiquidLevelMath.Bump(0.5), 6);
        Assert.Equal(0, LiquidLevelMath.Bump(double.NaN));
    }

    [Fact]
    public void LineOffset_PeaksAtTheKnobWhenStill()
    {
        var atKnob = LiquidLevelMath.LineOffset(100, 100, 16, 30, 0);
        var near = LiquidLevelMath.LineOffset(115, 100, 16, 30, 0);
        var far = LiquidLevelMath.LineOffset(140, 100, 16, 30, 0);

        Assert.Equal(16, atKnob, 6);
        Assert.InRange(near, 0.1, 15.9);
        Assert.Equal(0, far, 6);
    }

    [Fact]
    public void LineOffset_WhileMoving_StretchesOnlyTheTrailAndKeepsThePeakOnTheKnob()
    {
        // Subiendo (stretch 1) la cola de abajo llega más lejos; la de arriba no cambia.
        Assert.Equal(0, LiquidLevelMath.LineOffset(135, 100, 16, 30, 0), 6);
        Assert.True(LiquidLevelMath.LineOffset(135, 100, 16, 30, 1) > 0);
        Assert.Equal(0, LiquidLevelMath.LineOffset(65, 100, 16, 30, 1), 6);

        // El punto más abombado sigue a la altura del pomo: lo de «desfasado» que vio Adler.
        var peak = LiquidLevelMath.LineOffset(100, 100, 16, 30, 1);
        Assert.True(peak > LiquidLevelMath.LineOffset(98, 100, 16, 30, 1));
        Assert.True(peak > LiquidLevelMath.LineOffset(102, 100, 16, 30, 1));
    }

    [Theory]
    [InlineData("#E8739E")] // rosa de Sakura
    [InlineData("#3A86FF")] // un azul
    [InlineData("#2EA043")] // un verde
    public void ColorAt_FollowsTheAccent_PaleLowVividHigh(string hex)
    {
        var accent = RgbColor.FromHex(hex);
        var low = LiquidLevelMath.ColorAt(0, accent);
        var high = LiquidLevelMath.ColorAt(100, accent);

        Assert.Equal(accent, LiquidLevelMath.ColorAt(50, accent));
        Assert.True(ColorMath.RelativeLuminance(low) > ColorMath.RelativeLuminance(accent));
        Assert.True(ColorMath.Chroma(high) >= ColorMath.Chroma(accent));

        // Mismo tono en los tres puntos, con margen por el redondeo a bytes.
        Assert.InRange(Math.Abs(ColorMath.Hue(low) - ColorMath.Hue(accent)), 0, 6);
        Assert.InRange(Math.Abs(ColorMath.Hue(high) - ColorMath.Hue(accent)), 0, 6);
        Assert.Equal(low, LiquidLevelMath.ColorAt(double.NaN, accent));
    }

    [Fact]
    public void ColorAt_AGreyAccentStaysGrey()
    {
        var grey = RgbColor.FromHex("#808080");

        Assert.Equal(0, ColorMath.Chroma(LiquidLevelMath.ColorAt(0, grey)), 3);
        Assert.Equal(0, ColorMath.Chroma(LiquidLevelMath.ColorAt(100, grey)), 3);
    }

    [Fact]
    public void Spring_SettlesOnTheTargetWithASmallOvershoot()
    {
        double position = 0, velocity = 0, peak = 0;

        for (var frame = 0; frame < 240; frame++)
        {
            (position, velocity) = LiquidLevelMath.SpringStep(position, velocity, 60, 1d / 60, 220, 0.62);
            peak = Math.Max(peak, position);
        }

        Assert.True(LiquidLevelMath.IsSettled(position, velocity, 60));
        Assert.InRange(peak, 60.5, 72); // se pasa un poco, no se dispara
    }

    [Fact]
    public void Spring_ALateFrameDoesNotBlowUp()
    {
        var (position, velocity) = LiquidLevelMath.SpringStep(0, 0, 100, 2.0, 220, 0.62);

        Assert.InRange(position, 0, 100);
        Assert.True(double.IsFinite(velocity));
    }
}
