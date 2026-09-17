using Nexo.Core.Display;
using Xunit;

namespace Nexo.Core.Tests;

public sealed class BrightnessTargetTests
{
    [Theory]
    [InlineData(0, 2, 1)]
    [InlineData(1, 2, BrightnessTarget.All)]
    [InlineData(BrightnessTarget.All, 2, 0)]
    [InlineData(0, 1, 0)]
    public void Cycling_GoesThroughEachScreenThenAll(int current, int count, int expected) =>
        Assert.Equal(expected, BrightnessTarget.Next(current, count));

    [Fact]
    public void WithOneScreen_ThereIsNothingToChoose()
    {
        Assert.Null(BrightnessTarget.Label(0, 1));
        Assert.Equal(0, BrightnessTarget.Clamp(BrightnessTarget.All, 1));
    }

    [Fact]
    public void AScreenThatWasUnplugged_FallsBackToTheLastOne()
    {
        Assert.Equal(0, BrightnessTarget.Clamp(2, 1));
        Assert.Equal("Pantalla 2", BrightnessTarget.Label(BrightnessTarget.Clamp(3, 2), 2));
        Assert.Equal("Todas", BrightnessTarget.Label(BrightnessTarget.All, 3));
    }
}
