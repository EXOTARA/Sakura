using Nexo.Core.Resources;

namespace Nexo.Core.Tests;

public sealed class GlobalHotkeyReleasePolicyTests
{
    [Theory]
    [InlineData("FortniteClient-Win64-Shipping")]
    [InlineData("vlc")]
    [InlineData("")]
    [InlineData(null)]
    public void FullScreenApplication_ReleasesHotkeys(string? processName) =>
        Assert.True(GlobalHotkeyReleasePolicy.ShouldRelease(
            isForegroundFullScreen: true,
            processName,
            isOwnWindow: false,
            resourceGovernorEnabled: true));

    [Theory]
    [InlineData("explorer")]
    [InlineData("SearchHost")]
    [InlineData("StartMenuExperienceHost")]
    public void DesktopAndShellProcesses_KeepHotkeys(string processName) =>
        Assert.False(GlobalHotkeyReleasePolicy.ShouldRelease(
            isForegroundFullScreen: true,
            processName,
            isOwnWindow: false,
            resourceGovernorEnabled: true));

    [Fact]
    public void OwnWindow_KeepsHotkeys() =>
        Assert.False(GlobalHotkeyReleasePolicy.ShouldRelease(
            isForegroundFullScreen: true,
            "game",
            isOwnWindow: true,
            resourceGovernorEnabled: true));

    [Fact]
    public void WindowNotCoveringTheScreen_KeepsHotkeys() =>
        Assert.False(GlobalHotkeyReleasePolicy.ShouldRelease(
            isForegroundFullScreen: false,
            "game",
            isOwnWindow: false,
            resourceGovernorEnabled: true));

    [Fact]
    public void GovernorDisabled_KeepsHotkeys() =>
        Assert.False(GlobalHotkeyReleasePolicy.ShouldRelease(
            isForegroundFullScreen: true,
            "game",
            isOwnWindow: false,
            resourceGovernorEnabled: false));
}
