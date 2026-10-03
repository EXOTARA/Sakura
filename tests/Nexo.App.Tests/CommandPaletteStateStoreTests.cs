using System.IO;
using Xunit;

namespace Nexo.App.Tests;

/// <summary>
/// La paleta tuvo su propia casilla «Reducir movimiento»; al quitarla, quien la tenía marcada no debe
/// volver a ver animaciones en la paleta.
/// </summary>
public sealed class CommandPaletteStateStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"palette-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        File.Delete(_path);
        File.Delete(_path + ".tmp");
    }

    private CommandPaletteState LoadFrom(string json)
    {
        File.WriteAllText(_path, json);
        return new CommandPaletteStateStore(_path).Load();
    }

    [Fact]
    public void AnOldReduceMotionTrueBecomesTheNoMotionPreset()
    {
        var state = LoadFrom("""{"MotionPreset":1,"ReduceMotion":true,"RecentCommands":[]}""");

        Assert.Equal(ShellMotionPreset.None, state.MotionPreset);
        Assert.Null(state.ReduceMotion);
    }

    [Theory]
    [InlineData("""{"MotionPreset":2,"ReduceMotion":false,"RecentCommands":[]}""")]
    [InlineData("""{"MotionPreset":2,"RecentCommands":[]}""")]
    public void WithoutTheOldFlagThePresetIsKept(string json)
    {
        Assert.Equal(ShellMotionPreset.Calm, LoadFrom(json).MotionPreset);
    }

    [Fact]
    public void TheOldFieldIsNotWrittenAgain()
    {
        var store = new CommandPaletteStateStore(_path);
        File.WriteAllText(_path, """{"MotionPreset":0,"ReduceMotion":true,"RecentCommands":[]}""");

        store.Save(store.Load());

        var saved = File.ReadAllText(_path);
        Assert.DoesNotContain("ReduceMotion", saved);
        Assert.Contains("\"MotionPreset\": 3", saved);
    }
}
