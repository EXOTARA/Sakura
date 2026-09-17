using Nexo.Core.Ai;
using Xunit;

namespace Nexo.Core.Tests;

public sealed class LocalAiDiskPolicyTests
{
    private const long Gb = 1024L * 1024 * 1024;

    [Fact]
    public void AFullInstall_NeedsRoomForTheEngineAndTheModel()
    {
        Assert.Null(LocalAiDiskPolicy.Check(10 * Gb, engineInstalled: false));
        Assert.Contains("9 GB", LocalAiDiskPolicy.Check(6 * Gb, engineInstalled: false));
    }

    [Fact]
    public void OnlyTheModel_NeedsLess() =>
        Assert.Null(LocalAiDiskPolicy.Check(5 * Gb, engineInstalled: true));
}
