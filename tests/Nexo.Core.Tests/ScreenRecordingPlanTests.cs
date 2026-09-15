using Nexo.Core.Vision;

namespace Nexo.Core.Tests;

public sealed class ScreenRecordingPlanTests
{
    [Fact]
    public void OddScreenSizes_AreTrimmedToEvenForTheEncoder()
    {
        var plan = ScreenRecordingPlan.For(1366, 767);
        Assert.Equal(1366, plan.Width);
        Assert.Equal(766, plan.Height);
    }

    [Fact]
    public void FullHdAt60_UsesAboutTwelveMegabits()
    {
        var plan = ScreenRecordingPlan.For(1920, 1080);
        Assert.InRange(plan.VideoBitsPerSecond, 11_000_000u, 13_000_000u);
    }

    [Fact]
    public void BitrateStaysWithinSaneLimits()
    {
        Assert.Equal(40_000_000u, ScreenRecordingPlan.For(7680, 4320).VideoBitsPerSecond);
        Assert.Equal(4_000_000u, ScreenRecordingPlan.For(640, 480, 15).VideoBitsPerSecond);
    }

    [Fact]
    public void FrameRateIsClamped()
    {
        Assert.Equal(60, ScreenRecordingPlan.For(1920, 1080, 240).FramesPerSecond);
        Assert.Equal(15, ScreenRecordingPlan.For(1920, 1080, 1).FramesPerSecond);
    }

    [Fact]
    public void AMinuteOfFullHd_IsRoughlyNinetyMegabytes() =>
        Assert.InRange(ScreenRecordingPlan.For(1920, 1080).ApproximateMegabytesPerMinute(withAudio: true), 80, 100);
}
