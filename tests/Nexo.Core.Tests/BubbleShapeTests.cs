using Nexo.Core.Shell;

namespace Nexo.Core.Tests;

public sealed class BubbleShapeTests
{
    [Fact]
    public void StartsAsACircle_EndsAsThePill()
    {
        var shape = new BubbleShape(420, 88, 22);

        var start = shape.At(0);
        Assert.Equal(start.Width, start.Height);
        Assert.Equal(start.Width / 2, start.Radius);

        var end = shape.At(1);
        Assert.Equal(new BubbleFrame(420, 88, 22), end);
    }

    [Fact]
    public void TheSeedIsCapped_SoATallPillStillStartsAsASmallBubble()
    {
        Assert.Equal(BubbleShape.MaximumSeed, new BubbleShape(480, 300, 36).Seed);
        Assert.Equal(40, new BubbleShape(300, 40, 20).Seed);
    }

    [Fact]
    public void GrowsSidewaysBeforeGainingHeight()
    {
        var shape = new BubbleShape(420, 160, 36);
        var half = shape.At(0.5);

        var widthShare = (half.Width - shape.Seed) / (420 - shape.Seed);
        var heightShare = (half.Height - shape.Seed) / (160 - shape.Seed);
        Assert.True(widthShare > heightShare);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.13)]
    [InlineData(0.5)]
    [InlineData(0.87)]
    [InlineData(1)]
    [InlineData(1.4)]
    [InlineData(-3)]
    [InlineData(double.NaN)]
    public void TheRadiusNeverExceedsHalfTheHeight_AndSizesStayInBounds(double progress)
    {
        var shape = new BubbleShape(370, 88, 22);
        var frame = shape.At(progress);

        Assert.True(frame.Radius <= (frame.Height / 2) + 1e-9);
        Assert.InRange(frame.Width, shape.Seed, 370);
        Assert.InRange(frame.Height, shape.Seed, 88);
    }
}
