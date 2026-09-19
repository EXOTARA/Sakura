using System.IO;
using Nexo.Core.Storage;
using Nexo.Core.Tasks;
using Nexo.Windows.Tasks;
using Xunit;

namespace Nexo.Windows.Tests.Storage;

/// <summary>
/// Congela el defecto que perdía las tareas: una lectura fallida se parecía a «no tienes nada» y el
/// primer guardado escribía la lista vacía encima. Usa una ruta temporal propia, así que no toca el
/// override global de NexoDataPaths ni necesita la colección compartida.
/// </summary>
public sealed class JsonTaskStoreRecoveryTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "sakura-tareas-" + Guid.NewGuid().ToString("N"));

    public JsonTaskStoreRecoveryTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string FilePath => Path.Combine(_root, "tasks.json");

    [Fact]
    public void AMissingFile_IsMissing_AndNothingIsSetAside()
    {
        var store = new JsonTaskStore(FilePath);

        var tasks = store.Load();

        Assert.Empty(tasks);
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
        var store = new JsonTaskStore(FilePath);

        var tasks = store.Load();

        Assert.Empty(tasks);
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
        var store = new JsonTaskStore(FilePath);

        using (new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var tasks = store.Load();

            Assert.Empty(tasks);
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
        var store = new JsonTaskStore(FilePath);
        store.Save([new NexoTask { Title = "Entregar informe" }]);

        var reloaded = new JsonTaskStore(FilePath);
        var tasks = reloaded.Load();

        Assert.Single(tasks);
        Assert.Equal(DataLoadStatus.Ok, reloaded.LastLoad.Status);
    }

    [Fact]
    public void ASaveThatCannotWriteTheTemporary_ThrowsOurException_AndLeavesTheRealFileIntact()
    {
        var store = new JsonTaskStore(FilePath);
        store.Save([new NexoTask { Title = "Lo que ya había" }]);
        var before = File.ReadAllText(FilePath);

        // Una carpeta con el nombre del temporal hace que escribirlo falle siempre, sin depender
        // del disco.
        Directory.CreateDirectory(FilePath + ".tmp");

        var exception = Assert.Throws<SakuraDataWriteException>(
            () => store.Save([new NexoTask { Title = "No debe llegar" }]));

        Assert.Equal(FilePath, exception.Path);
        Assert.Equal(before, File.ReadAllText(FilePath));
    }
}
