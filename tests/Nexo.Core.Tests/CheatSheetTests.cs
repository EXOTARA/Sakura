using Nexo.Core.Shell;

namespace Nexo.Core.Tests;

public sealed class CheatSheetTests
{
    [Fact]
    public void SakurasOwnShortcuts_AreTheOnesItRegisters()
    {
        var keys = CheatSheet.Build(dictationEnabled: true)
            .SelectMany(group => group.Items)
            .Select(item => item.Keys)
            .ToList();

        // Los mismos que registra MainWindow.Window_SourceInitialized, más Ctrl + K y Esc, que son de la ventana.
        foreach (var expected in new[]
                 {
                     "Alt + A", "Alt + Shift + A", "Ctrl + Espacio", "Ctrl + Shift + Espacio",
                     "Alt + V", "Ctrl + Shift + T", "Ctrl + Shift + D", "Ctrl + K", "Esc"
                 })
        {
            Assert.Contains(expected, keys);
        }
    }

    [Fact]
    public void DictationOff_HidesItsShortcut_BecauseItIsNotRegistered()
    {
        var keys = CheatSheet.Build(dictationEnabled: false).SelectMany(group => group.Items).Select(item => item.Keys);

        Assert.DoesNotContain("Ctrl + Shift + D", keys);
    }

    [Fact]
    public void KeysSplitIntoTheirParts_ForTheKeycaps()
    {
        var item = new CheatItem("Ctrl + Shift + Esc", "Administrador de tareas");

        Assert.Equal(["Ctrl", "Shift", "Esc"], item.KeyParts);
        Assert.Equal(["Win", "."], new CheatItem("Win + .", "Emojis").KeyParts);
    }

    [Fact]
    public void NoShortcutIsListedTwice()
    {
        var keys = CheatSheet.Build(dictationEnabled: true).SelectMany(group => group.Items).Select(item => item.Keys).ToList();

        Assert.Equal(keys.Count, keys.Distinct().Count());
    }
}
