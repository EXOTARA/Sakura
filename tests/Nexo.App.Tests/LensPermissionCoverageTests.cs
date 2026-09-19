using System.IO;
using System.Text.RegularExpressions;

namespace Nexo.App.Tests;

/// <summary>
/// Adversarial: cada llamada a IScreenCaptureService.CaptureAsync en la app debe estar precedida,
/// en su mismo metodo, por una consulta al broker con SakuraCapability.Lens.
/// </summary>
public sealed class LensPermissionCoverageTests
{
    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Nexo.sln")) &&
               !Directory.Exists(Path.Combine(dir.FullName, "src", "Nexo.App")))
        {
            dir = dir.Parent;
        }

        return dir!.FullName;
    }

    [Fact]
    public void EveryScreenCaptureCall_IsGuardedByLensPermission()
    {
        var unguarded = new List<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(Root(), "src", "Nexo.App"), "MainWindow*.cs"))
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                if (!lines[i].Contains("_screenCaptureService.CaptureAsync"))
                {
                    continue;
                }

                // Sube hasta la firma del metodo (linea con indentacion de 4 y "private ").
                var start = i;
                while (start > 0 && !Regex.IsMatch(lines[start], @"^    (private|public|internal|protected) "))
                {
                    start--;
                }

                var body = string.Join('\n', lines[start..i]);
                // TryGetLensPermission es el único helper que arma la petición de Lens: cuenta como
                // consulta al broker igual que escribir SakuraCapability.Lens a mano.
                if (!body.Contains("SakuraCapability.Lens") && !body.Contains("TryGetLensPermission("))
                {
                    unguarded.Add($"{Path.GetFileName(file)}:{i + 1} ({lines[start].Trim()})");
                }
            }
        }

        Assert.True(unguarded.Count == 0, "Capturas sin permiso de Lens: " + string.Join(" | ", unguarded));
    }
}
