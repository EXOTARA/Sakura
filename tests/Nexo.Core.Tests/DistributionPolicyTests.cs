using Nexo.Core.Distribution;

namespace Nexo.Core.Tests;

public sealed class DistributionPolicyTests
{
    [Fact]
    public void DirectCopy_KeepsEverythingItAlreadyDoes()
    {
        const DistributionChannel channel = DistributionChannel.Direct;

        Assert.True(DistributionPolicy.UsesOwnUpdater(channel));
        Assert.True(DistributionPolicy.ReconcilesInstalledAppsEntry(channel));
        Assert.True(DistributionPolicy.InstallsOllamaItself(channel));
        Assert.Equal(StartupRegistration.RunKey, DistributionPolicy.Startup(channel));
    }

    [Fact]
    public void StoreCopy_LeavesUpdatesToTheStore_AndDoesNotInstallOtherSoftware()
    {
        const DistributionChannel channel = DistributionChannel.MicrosoftStore;

        Assert.False(DistributionPolicy.UsesOwnUpdater(channel));
        Assert.False(DistributionPolicy.ReconcilesInstalledAppsEntry(channel));
        Assert.False(DistributionPolicy.InstallsOllamaItself(channel));
        Assert.Equal(StartupRegistration.PackageStartupTask, DistributionPolicy.Startup(channel));
    }
}
