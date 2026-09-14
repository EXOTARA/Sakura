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
    public void LineOffset_StretchesAndTrailsWhileMoving()
    {
        // Moviéndose (stretch 1) la comba llega más lejos que quieta y su centro queda desplazado.
        var stillReach = LiquidLevelMath.LineOffset(135, 100, 16, 30, 0);
        var movingReach = LiquidLevelMath.LineOffset(135, 100, 16, 30, 1);

        Assert.Equal(0, stillReach, 6);
        Assert.True(movingReach > 0);
        Assert.True(LiquidLevelMath.LineOffset(100, 100, 16, 30, 1) < LiquidLevelMath.LineOffset(108, 100, 16, 30, 1));
    }

    [Fact]
    public void ColorAt_GoesFromLimeThroughYellowToOrange()
    {
        Assert.Equal(LiquidLevelMath.Low, LiquidLevelMath.ColorAt(0));
        Assert.Equal(LiquidLevelMath.Middle, LiquidLevelMath.ColorAt(50));
        Assert.Equal(LiquidLevelMath.High, LiquidLevelMath.ColorAt(100));
        Assert.Equal(LiquidLevelMath.High, LiquidLevelMath.ColorAt(250));
        Assert.Equal(LiquidLevelMath.Low, LiquidLevelMath.ColorAt(double.NaN));
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
