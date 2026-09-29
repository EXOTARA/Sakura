using System.Diagnostics;
using System.Text;
using Nexo.Core.Updates;
using Nexo.Windows.Updates;

namespace Nexo.Windows.Tests.Updates;

/// <summary>
/// Tester adversario, bloque instalación y actualización (0.30.36) — ejecuta el guion de verdad con
/// powershell.exe sobre una instalación de mentira. Las pruebas de texto de
/// <see cref="UpdateHelperScriptTests"/> no pueden ver cómo se comporta Move-Item ni cómo lee
/// PowerShell 5.1 un archivo sin BOM.
/// </summary>
public sealed class UpdateHelperScriptRealPowerShellTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sakura-ps-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    private (UpdateSwapPaths Paths, string Work) Prepare(string parentName)
    {
        var install = Path.Combine(_root, parentName, "Sakura");
        Directory.CreateDirectory(install);
        for (var i = 1; i <= 6; i++) File.WriteAllText(Path.Combine(install, $"f{i}.txt"), "OLD");
        File.WriteAllText(Path.Combine(install, "unins000.exe"), "x");
        var paths = UpdateSwapPathPolicy.Resolve(install);
        Directory.CreateDirectory(paths.Staged);
        for (var i = 1; i <= 6; i++) File.WriteAllText(Path.Combine(paths.Staged, $"f{i}.txt"), "NEW");
        var work = Path.Combine(_root, "work");
        Directory.CreateDirectory(work);
        return (paths, work);
    }

    private static int RunScript(string script, string work, bool bom)
    {
        var path = Path.Combine(work, "aplicar.ps1");
        // WindowsUpdateService usa File.WriteAllTextAsync(ruta, texto): UTF-8 SIN BOM.
        if (bom) File.WriteAllText(path, script, new UTF8Encoding(true)); else File.WriteAllText(path, script);
        var deadPid = Process.Start(new ProcessStartInfo("cmd", "/c exit") { CreateNoWindow = true, UseShellExecute = false })!;
        deadPid.WaitForExit();
        var psi = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = work };
        foreach (var a in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", path }) psi.ArgumentList.Add(a);
        using var ps = Process.Start(psi)!;
        ps.WaitForExit(60_000);
        return ps.ExitCode;
    }

    private static string BuildFor(UpdateSwapPaths paths, string work) =>
        UpdateHelperScript.Build(paths, PidOfExitedProcess(), Path.Combine(paths.Install, "Sakura.exe"),
            Path.Combine(paths.Install, "Sakura.exe"), Path.Combine(work, "pkg.zip"));

    private static int PidOfExitedProcess()
    {
        var p = Process.Start(new ProcessStartInfo("cmd", "/c exit") { CreateNoWindow = true, UseShellExecute = false })!;
        p.WaitForExit();
        return p.Id;
    }

    [Fact]
    public void WhenAFileInTheCurrentInstallIsLocked_TheInstallIsLeftWhole()
    {
        // Un antivirus o un DLL cargado por otro proceso: el archivo se puede leer pero no mover.
        // Move-Item sobre una carpeta NO es atómico en PowerShell 5.1: mueve archivo a archivo, falla
        // en el bloqueado y deja la instalación partida entre «Sakura» y «Sakura.old». Como $moved
        // sigue en false, la vuelta atrás no devuelve nada.
        var (paths, work) = Prepare("locked");
        using var held = new FileStream(Path.Combine(paths.Install, "f3.txt"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        var exit = RunScript(BuildFor(paths, work), work, bom: false);

        Assert.Equal(1, exit);
        for (var i = 1; i <= 6; i++)
        {
            Assert.True(File.Exists(Path.Combine(paths.Install, $"f{i}.txt")), $"f{i}.txt no está en la instalación tras la vuelta atrás");
        }
    }

    [Fact]
    public void AnInstallPathWithAccents_SwapsCorrectly()
    {
        // Quien se llama José o María tiene «José» en %LOCALAPPDATA%. WindowsUpdateService escribe el
        // guion en UTF-8 sin BOM, y powershell.exe 5.1 lo lee como ANSI: la ruta llega mal escrita,
        // Test-Path no encuentra la carpeta preparada y el ayudante sale con «No hay carpeta preparada».
        var (paths, work) = Prepare("José Muñoz");

        var exit = RunScript(BuildFor(paths, work), work, bom: false);

        Assert.Equal(0, exit);
        Assert.Equal("NEW", File.ReadAllText(Path.Combine(paths.Install, "f1.txt")));
    }

    [Fact]
    public void ATypographicApostropheInThePath_DoesNotBreakTheScript()
    {
        // PowerShell trata U+2018/U+2019/U+201A/U+201B como comillas simples; Quote solo duplica la
        // ASCII. Se escribe con BOM para aislar este fallo del de la codificación.
        var (paths, work) = Prepare("D’Angelo");

        var exit = RunScript(BuildFor(paths, work), work, bom: true);

        Assert.Equal(0, exit);
        Assert.Equal("NEW", File.ReadAllText(Path.Combine(paths.Install, "f1.txt")));
    }
}
