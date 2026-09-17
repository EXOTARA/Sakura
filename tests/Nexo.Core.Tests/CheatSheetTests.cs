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
                     "Alt + V", "Ctrl + Shift + T", "Ctrl + Shift + D", "Alt + Shift + S", "Alt + Shift + G",
                     "Alt + Shift + N", "Alt + Shift + F", "Alt + Shift + E",
                     "Ctrl + K", "Esc"
                 })
        {
            Assert.Contains(expected, keys);
        }

        Assert.DoesNotContain("Alt + Shift + R", keys);
    }

    [Fact]
    public void TheSelectionShortcut_IsTheOneThatWasFree_OrHiddenIfNone()
    {
        string[] Keys(string? letter) =>
            CheatSheet.Build(dictationEnabled: true, letter).SelectMany(group => group.Items).Select(item => item.Keys).ToArray();

        Assert.Contains("Alt + Shift + W", Keys("W"));
        Assert.DoesNotContain(Keys(null), key => key is "Alt + Shift + E" or "Alt + Shift + W" or "Alt + Shift + Q");
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
