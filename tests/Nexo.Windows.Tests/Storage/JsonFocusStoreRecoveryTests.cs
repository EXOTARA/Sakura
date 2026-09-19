using System.IO;
using Nexo.Core.Storage;
using Nexo.Core.Focus;
using Nexo.Windows.Focus;
using Xunit;

namespace Nexo.Windows.Tests.Storage;

/// <summary>
/// Congela el defecto que perdía el enfoque: una lectura fallida se parecía a «no tienes nada» y el
/// primer guardado escribía la lista vacía encima. Usa una ruta temporal propia, así que no toca el
/// override global de NexoDataPaths ni necesita la colección compartida.
/// </summary>
public sealed class JsonFocusStoreRecoveryTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "sakura-enfoque-" + Guid.NewGuid().ToString("N"));

    public JsonFocusStoreRecoveryTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string FilePath => Path.Combine(_root, "focus.json");

    [Fact]
    public void AMissingFile_IsMissing_AndNothingIsSetAside()
    {
        var store = new JsonFocusStore(FilePath);

        var tasks = store.Load();

        Assert.Empty(tasks.History);
        Assert.Equal(DataLoadStatus.Missing, store.LastLoad.Status);
        Assert.Empty(Directory.GetFiles(_root));
    }

    [Theory]
    [InlineData("[{\"Title\": \"trunc")]
    [InlineData("")]
    [InlineData("\0\0\0\0\0\0\0\0")]
    public void ADamagedFile_IsSetAsideAndReportedAsCorrupt(string content)
    {
        File.WriteAllText(FilePath, content);
        var store = new JsonFocusStore(FilePath);

        var tasks = store.Load();

        Assert.Empty(tasks.History);
        Assert.Equal(DataLoadStatus.Corrupt, store.LastLoad.Status);
        Assert.False(store.LastLoad.IsSafeToOverwrite);
        Assert.NotNull(store.LastLoad.PreservedPath);
        Assert.True(File.Exists(store.LastLoad.PreservedPath));
        Assert.Equal(content, File.ReadAllText(store.LastLoad.PreservedPath!));
    }

    [Fact]
    public void ALockedFile_IsUnreadable_AndTheOriginalIsNeverRenamed()
    {
        // Esta es la prueba que congela el defecto: con el archivo bloqueado, Sakura no debe
        // apartarlo ni tocarlo. Se conserva tal cual para cuando el bloqueo se suelte.
        File.WriteAllText(FilePath, "[]");
        var store = new JsonFocusStore(FilePath);

        using (new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var tasks = store.Load();

            Assert.Empty(tasks.History);
        }

        Assert.Equal(DataLoadStatus.Unreadable, store.LastLoad.Status);
        Assert.False(store.LastLoad.IsSafeToOverwrite);
        Assert.True(File.Exists(FilePath));
        Assert.Equal("[]", File.ReadAllText(FilePath));
        Assert.Single(Directory.GetFiles(_root));
    }

    [Fact]
    public void AGoodFile_LoadsAndIsOk()
    {
        var store = new JsonFocusStore(FilePath);
        store.Save(new FocusState { History = [new FocusHistoryEntry { Label = "Estudio", Duration = TimeSpan.FromMinutes(25) }] });

        var reloaded = new JsonFocusStore(FilePath);
        var tasks = reloaded.Load();

        Assert.Single(tasks.History);
        Assert.Equal(DataLoadStatus.Ok, reloaded.LastLoad.Status);
    }

    [Fact]
    public void ASaveThatCannotWriteTheTemporary_ThrowsOurException_AndLeavesTheRealFileIntact()
    {
        var store = new JsonFocusStore(FilePath);
        store.Save(new FocusState { History = [new FocusHistoryEntry { Label = "Lo que ya había", Duration = TimeSpan.FromMinutes(5) }] });
        var before = File.ReadAllText(FilePath);

        // Una carpeta con el nombre del temporal hace que escribirlo falle siempre, sin depender
        // del disco.
        Directory.CreateDirectory(FilePath + ".tmp");

        var exception = Assert.Throws<SakuraDataWriteException>(
            () => store.Save(new FocusState()));

        Assert.Equal(FilePath, exception.Path);
        Assert.Equal(before, File.ReadAllText(FilePath));
    }
}
