using Nexo.Windows.Workspace;

namespace Nexo.Windows.Tests;

/// <summary>
/// El lector salta enlaces reales (uniones y simbólicos) pero no debe saltar cualquier reparse point:
/// los archivos «Files On-Demand» de OneDrive lo llevan y su LinkTarget es null.
/// </summary>
public sealed class FileSystemWorkspaceReaderLinkTests : IDisposable
{
    private readonly string _base = Path.Combine(Path.GetTempPath(), "kohana-reader-tests", Guid.NewGuid().ToString("N"));
    private readonly string _root;
    private readonly string _outside;
    private string? _junction;

    public FileSystemWorkspaceReaderLinkTests()
    {
        _root = Path.Combine(_base, "proj");
        _outside = Path.Combine(_base, "fuera");
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(_outside);
    }

    public void Dispose()
    {
        try
        {
            if (_junction is not null && Directory.Exists(_junction))
            {
                Directory.Delete(_junction); // solo quita la unión
            }

            Directory.Delete(_base, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Limpieza best-effort.
        }
    }

    [Fact]
    public void ListFiles_ReturnsOrdinaryFiles_AndSkipsAJunctionToOutside()
    {
        File.WriteAllText(Path.Combine(_root, "propio.txt"), "mío");
        File.WriteAllText(Path.Combine(_outside, "ajeno.txt"), "no debería listarse");
        _junction = Path.Combine(_root, "docs");

        var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
            "cmd.exe", $"/c mklink /J \"{_junction}\" \"{_outside}\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false
        })!;
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);

        var files = new FileSystemWorkspaceReader().ListFiles(_root, 100);

        Assert.Equal(["propio.txt"], files.Select(file => file.RelativePath).ToArray());
    }
}
