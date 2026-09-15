using Nexo.Core.Ai;

namespace Nexo.Core.Tests;

public sealed class ApiKeyHintTests
{
    [Fact]
    public void ALongKey_ShowsOnlyItsLastFourCharacters() =>
        Assert.Equal("…W9xQ", ApiKeyHint.Ending("gsk_0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHW9xQ"));

    [Fact]
    public void AShortKey_ShowsNothingOfIt() =>
        Assert.Equal("…", ApiKeyHint.Ending("abc123"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NoKey_NoHint(string? key) =>
        Assert.Null(ApiKeyHint.Ending(key));
}
