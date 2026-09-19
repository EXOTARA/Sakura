using Nexo.Core.Workspace;
using Nexo.Windows.Workspace;

namespace Nexo.Windows.Tests;

/// <summary>
/// Diseño D14 — la escritura real en disco. Ésta es la última puerta antes de un archivo de la
/// persona, así que lo que más importa probar es que no se sale de la carpeta autorizada.
/// </summary>
public sealed class FileSystemWorkspaceWriterTests : IDisposable
{
    private readonly string _root;
    private readonly FileSystemWorkspaceWriter _writer = new();

    public FileSystemWorkspaceWriterTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "kohana-writer-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Limpieza best-effort.
        }
    }

    [Fact]
    public void WritingInsideTheRoot_CreatesTheFileAndItsFolders()
    {
        var result = _writer.WriteFile(_root, Path.Combine("src", "Program.cs"), "hola");

        Assert.True(result.Success);
        Assert.Equal("hola", File.ReadAllText(Path.Combine(_root, "src", "Program.cs")));
    }

    [Fact]
    public void WritingOutsideTheRoot_IsRefused()
    {
        // La comprobación se repite aquí aunque el coordinador ya la haga: escribir fuera del
        // proyecto no tiene arreglo.
        var result = _writer.WriteFile(_root, Path.Combine("..", "..", "fuera.cs"), "no debería estar aquí");

        Assert.False(result.Success);
        Assert.False(File.Exists(Path.Combine(_root, "..", "..", "fuera.cs")));
    }

    [Fact]
    public void ReadForCheckpoint_TellsWhetherTheFileExisted()
    {
        var (existedBefore, readableBefore, _) = _writer.ReadForCheckpoint(_root, "nuevo.cs");
        Assert.False(existedBefore);
        Assert.True(readableBefore);

        _writer.WriteFile(_root, "nuevo.cs", "contenido");

        var (existed, readable, content) = _writer.ReadForCheckpoint(_root, "nuevo.cs");
        Assert.True(existed);
        Assert.True(readable);
        Assert.Equal("contenido", content);
    }

    [Fact]
    public void ReadForCheckpoint_ALockedFile_IsUnreadable_NotMissing()
    {
        File.WriteAllText(Path.Combine(_root, "abierto.cs"), "contenido");

        // Bloqueado en exclusiva por otro programa: la lectura falla, pero el archivo EXISTE. Antes se
        // hacía pasar por inexistente.
        using var locked = new FileStream(
            Path.Combine(_root, "abierto.cs"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var (_, readable, _) = _writer.ReadForCheckpoint(_root, "abierto.cs");

        Assert.False(readable);
    }

    [Fact]
    public void OverwritingKeepsTheFileIntact_NotTruncated()
    {
        // Escritura atómica: si Sakura muriera a media escritura, el original seguiría entero.
        _writer.WriteFile(_root, "a.cs", "primera versión");
        _writer.WriteFile(_root, "a.cs", "segunda versión");

        Assert.Equal("segunda versión", File.ReadAllText(Path.Combine(_root, "a.cs")));

        // Y no queda basura del archivo temporal.
        Assert.Empty(Directory.GetFiles(_root, "*.kohana-tmp"));
    }

    [Fact]
    public void DeletingInsideTheRoot_Works()
    {
        _writer.WriteFile(_root, "borrame.cs", "x");

        Assert.True(_writer.DeleteFile(_root, "borrame.cs").Success);
        Assert.False(File.Exists(Path.Combine(_root, "borrame.cs")));
    }

    [Fact]
    public void DeletingOutsideTheRoot_IsRefused() =>
        Assert.False(_writer.DeleteFile(_root, Path.Combine("..", "algo.cs")).Success);

    [Fact]
    public void DeletingSomethingThatIsNotThere_IsNotAFailure() =>
        // Deshacer la creación de un archivo que ya no está deja el proyecto como debía quedar.
        Assert.True(_writer.DeleteFile(_root, "nunca-existio.cs").Success);
}
