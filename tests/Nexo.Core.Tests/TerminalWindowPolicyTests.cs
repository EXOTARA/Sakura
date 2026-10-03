using Nexo.Core.Ambient;

namespace Nexo.Core.Tests;

public sealed class TerminalWindowPolicyTests
{
    [Theory]
    [InlineData("WindowsTerminal")]
    [InlineData("cmd")]
    [InlineData("PowerShell")]
    [InlineData("pwsh.exe")]
    [InlineData("conhost")]
    [InlineData("OpenConsole")]
    [InlineData("wsl")]
    [InlineData("bash")]
    [InlineData("mintty")]
    [InlineData("alacritty")]
    [InlineData("wezterm-gui")]
    [InlineData("putty")]
    [InlineData("kitty")]
    [InlineData("ConEmu64")]
    [InlineData("ConEmuC64")]
    [InlineData("Hyper")]
    [InlineData("Tabby")]
    [InlineData("ttermpro")]
    [InlineData("MobaXterm")]
    [InlineData("Terminus")]
    [InlineData("cmder")]
    public void KnownTerminals_AreDetected(string processName) =>
        Assert.True(TerminalWindowPolicy.IsTerminal(processName));

    [Theory]
    [InlineData("notepad")]
    [InlineData("winword")]
    [InlineData("")]
    [InlineData(null)]
    public void OtherProcesses_AreNotTerminals(string? processName) =>
        Assert.False(TerminalWindowPolicy.IsTerminal(processName));

    [Fact]
    public void WouldRunCommands_NeedsTerminalAndLineBreak()
    {
        Assert.True(TerminalWindowPolicy.WouldRunCommands("cmd", "a\nb"));
        Assert.True(TerminalWindowPolicy.WouldRunCommands("cmd", "a\rb"));
        Assert.False(TerminalWindowPolicy.WouldRunCommands("cmd", "a b"));
        Assert.False(TerminalWindowPolicy.WouldRunCommands("notepad", "a\nb"));
        Assert.False(TerminalWindowPolicy.WouldRunCommands("cmd", null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void UnknownProcess_WithLineBreaks_FailsClosed(string? processName)
    {
        Assert.True(TerminalWindowPolicy.WouldRunCommands(processName, "a\nb"));
        Assert.False(TerminalWindowPolicy.WouldRunCommands(processName, "una línea"));
    }
}
