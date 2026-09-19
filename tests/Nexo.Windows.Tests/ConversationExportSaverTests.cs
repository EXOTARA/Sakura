using System.ComponentModel;
using Nexo.Core.Assistant;
using Nexo.Windows.Storage;

namespace Nexo.Windows.Tests;

/// <summary>Guardar la conversación y abrir el explorador se cuentan por separado.</summary>
public sealed class ConversationExportSaverTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 9, 18, 15, 30, 0, TimeSpan.Zero);

    private static readonly ConversationMessage[] Messages =
    [
        new(ConversationRole.User, "Hola", Now.AddMinutes(-1)),
        new(ConversationRole.Assistant, "Buenas", Now)
    ];

    private readonly string _folder =
        Path.Combine(Path.GetTempPath(), "sakura-export-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Limpieza best-effort.
        }
    }

    [Fact]
    public void WhenOnlyTheExplorerFails_TheFileIsStillReportedAsSaved()
    {
        var outcome = ConversationExportSaver.Save(
            _folder, Messages, Now, _ => throw new Win32Exception("explorer roto en " + _folder));

        Assert.True(outcome.Saved);
        Assert.False(outcome.FolderOpened);
        Assert.Single(Directory.GetFiles(_folder));
        Assert.Contains("guardada", outcome.Message);
        Assert.DoesNotContain("No pude guardarla", outcome.Message);
        Assert.DoesNotContain(_folder, outcome.Message);
    }

    [Fact]
    public void TwoExportsInTheSameMinute_AreNumbered_AndTheExplorerPointsAtTheSecond()
    {
        string? selected = null;
        ConversationExportSaver.Save(_folder, Messages, Now, _ => { });
        var second = ConversationExportSaver.Save(_folder, Messages, Now, path => selected = path);

        Assert.Equal(2, Directory.GetFiles(_folder).Length);
        Assert.EndsWith("(2).md", second.FileName);
        Assert.EndsWith(second.FileName, selected);
    }

    [Fact]
    public void WhenSavingFails_TheMessageHasNoPath()
    {
        // La «carpeta» es un archivo: no se puede crear ahí.
        Directory.CreateDirectory(_folder);
        var blocker = Path.Combine(_folder, "bloqueo");
        File.WriteAllText(blocker, "x");

        var outcome = ConversationExportSaver.Save(blocker, Messages, Now, _ => { });

        Assert.False(outcome.Saved);
        Assert.DoesNotContain(_folder, outcome.Message);
    }
}
