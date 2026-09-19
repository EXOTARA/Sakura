using System.Text;
using Nexo.Windows.Storage;

namespace Nexo.Windows.Tests.Documents;

/// <summary>
/// Un archivo que ya existe no se toca nunca, y una escritura que falla no deja basura con nombre de
/// bueno. Cada prueba recibe su propia carpeta: no muta ningún estado global.
/// </summary>
public sealed class FreshFileWriterTests : IDisposable
{
    private readonly string _folder;

    public FreshFileWriterTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "sakura-fresh-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

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
    public void AFreeName_IsUsedAsIs()
    {
        var result = FreshFileWriter.Write(_folder, "Informe.docx", new byte[] { 1, 2, 3 });

        Assert.True(result.Saved);
        Assert.Equal(Path.Combine(_folder, "Informe.docx"), result.FullPath);
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(result.FullPath));
    }

    [Fact]
    public void ATakenName_LeavesThePreviousContentByteForByteAndNumbersTheNewOne()
    {
        // Congela el defecto D: «no se sobrescribe nunca» tiene que ser una garantía, no una
        // comprobación.
        var original = new byte[] { 9, 8, 7, 6, 0, 255 };
        File.WriteAllBytes(Path.Combine(_folder, "Informe.docx"), original);

        var result = FreshFileWriter.Write(_folder, "Informe.docx", new byte[] { 1 });

        Assert.True(result.Saved);
        Assert.Equal(Path.Combine(_folder, "Informe (2).docx"), result.FullPath);
        Assert.Equal(original, File.ReadAllBytes(Path.Combine(_folder, "Informe.docx")));
        Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(result.FullPath));
    }

    [Fact]
    public void TwoWritesOfTheSameName_NeverOverwriteEachOther()
    {
        // No reproduce la carrera exacta (no es determinista): comprueba el resultado que la
        // garantía CreateNew debe dar, dos escrituras seguidas con el mismo nombre.
        var first = FreshFileWriter.Write(_folder, "Informe.docx", new byte[] { 1 });
        var second = FreshFileWriter.Write(_folder, "Informe.docx", new byte[] { 2 });

        Assert.NotEqual(first.FullPath, second.FullPath);
        Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(first.FullPath));
        Assert.Equal(new byte[] { 2 }, File.ReadAllBytes(second.FullPath));
    }

    [Fact]
    public void AFileThatAppearsBetweenTheCheckAndTheOpen_IsNeverOverwritten()
    {
        // La costura hace que la vía rápida mienta («libre») aunque el archivo ya exista: es lo que
        // pasa si alguien lo crea justo entre la comprobación y la apertura. Solo CreateNew lo
        // impide; con FileMode.Create esta prueba falla (comprobado con una mutación temporal).
        var original = new byte[] { 9, 8, 7 };
        File.WriteAllBytes(Path.Combine(_folder, "Informe.docx"), original);

        var result = FreshFileWriter.Write(
            _folder, "Informe.docx", stream => stream.WriteByte(1), _ => false);

        Assert.True(result.Saved, result.Message);
        Assert.Equal(Path.Combine(_folder, "Informe (2).docx"), result.FullPath);
        Assert.Equal(original, File.ReadAllBytes(Path.Combine(_folder, "Informe.docx")));
    }

    [Fact]
    public void TwoHundredTakenNames_FailWithAReadableMessage_AndTouchNone()
    {
        var marker = new byte[] { 42 };
        File.WriteAllBytes(Path.Combine(_folder, "Informe.docx"), marker);
        for (var index = 2; index <= FreshFileWriter.MaximumAttempts; index++)
        {
            File.WriteAllBytes(Path.Combine(_folder, $"Informe ({index}).docx"), marker);
        }

        var result = FreshFileWriter.Write(_folder, "Informe.docx", new byte[] { 1 });

        Assert.False(result.Saved);
        Assert.Contains("demasiados archivos", result.Message);
        Assert.Equal(FreshFileWriter.MaximumAttempts, Directory.GetFiles(_folder).Length);
        Assert.All(Directory.GetFiles(_folder), path => Assert.Equal(marker, File.ReadAllBytes(path)));
    }

    [Fact]
    public void AWriteThatFailsHalfway_LeavesNoFile()
    {
        // Defecto C: antes quedaba un archivo truncado con nombre de documento bueno.
        var result = FreshFileWriter.Write(_folder, "Informe.docx", stream =>
        {
            stream.Write(new byte[5000], 0, 5000);
            stream.Flush();
            throw new IOException("disco lleno simulado en " + _folder);
        });

        Assert.False(result.Saved);
        Assert.Empty(Directory.GetFiles(_folder));
    }

    [Fact]
    public void AFailureMessage_NeverCarriesThePathOrTheRawExceptionText()
    {
        var result = FreshFileWriter.Write(_folder, "Informe.docx", _ =>
            throw new IOException("mensaje crudo con " + _folder));

        Assert.False(result.Saved);
        Assert.DoesNotContain(_folder, result.Message);
        Assert.DoesNotContain("mensaje crudo", result.Message);
        Assert.Contains("Informe.docx", result.Message);
    }

    [Fact]
    public void AMissingFolder_FailsInsteadOfThrowing()
    {
        var result = FreshFileWriter.Write(
            Path.Combine(_folder, "no-existe"), "Informe.docx", new byte[] { 1 });

        Assert.False(result.Saved);
        Assert.Empty(Directory.GetDirectories(_folder));
    }

    [Fact]
    public void TheCreatedFile_IsNotHidden()
    {
        // Medido: un temporal oculto renombrado deja el documento guardado pero invisible.
        var result = FreshFileWriter.Write(_folder, "Informe.docx", new byte[] { 1 });

        Assert.Equal(0, (int)(File.GetAttributes(result.FullPath) & (FileAttributes.Hidden | FileAttributes.Temporary)));
    }

    [Fact]
    public void TextIsWrittenWithoutABom()
    {
        var result = FreshFileWriter.Write(
            _folder, "c.md", "¿Qué tal?", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        var bytes = File.ReadAllBytes(result.FullPath);
        Assert.Equal(Encoding.UTF8.GetBytes("¿Qué tal?"), bytes);
    }
}
