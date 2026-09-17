using Nexo.Windows.Files;
using Xunit;

namespace Nexo.Windows.Tests;

public sealed class WindowsFileSearchServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sakura-files-" + Guid.NewGuid().ToString("N"));

    public WindowsFileSearchServiceTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Materias"));
        File.WriteAllText(Path.Combine(_root, "Materias", "Reporte biseccion.txt"), "metodo numerico");
        File.WriteAllText(Path.Combine(_root, "lista compras.txt"), "tortillas");
    }

    [Fact]
    public async Task FindsFilesByName_InSubfolders()
    {
        var service = new WindowsFileSearchService([_root]);

        var results = await service.SearchAsync("biseccion", 10, CancellationToken.None);

        var result = Assert.Single(results);
        Assert.Equal("Reporte biseccion.txt", result.Name);
        Assert.EndsWith("Materias", result.Folder);
    }

    [Fact]
    public async Task AMissingFolder_GivesNothing_InsteadOfFailing() =>
        Assert.Empty(await new WindowsFileSearchService([Path.Combine(_root, "no-existe")])
            .SearchAsync("biseccion", 10, CancellationToken.None));

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
