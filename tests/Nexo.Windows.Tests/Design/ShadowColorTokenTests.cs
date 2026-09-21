namespace Nexo.Windows.Tests.Design;

/// <summary>
/// Hallazgo de auditoría (tester, 2026-09-20) — Colors.xaml documenta que el negro de sombra se
/// unificó bajo <c>ColorShadow</c> porque antes se escribía "Black" en unos sitios y "#000000" en
/// otros (17 sitios, dice el comentario). Controls.xaml y las nueve ventanas que dependen de él ya
/// están migrados, pero MainWindow.xaml y Views/DashboardView.xaml siguen con
/// <c>Color="Black"</c> literal en su DropShadowEffect: si alguna vez ColorShadow cambia (p.ej.
/// para un tema claro), estos dos sitios no lo seguirán, exactamente la deriva que el token
/// pretendía evitar.
/// </summary>
public sealed class ShadowColorTokenTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void NoAppXaml_HasHardcodedShadowBlack_OutsideTheColorShadowToken()
    {
        var appDir = Path.Combine(RepositoryRoot, "src", "Nexo.App");
        var offenders = new List<string>();

        foreach (var path in Directory.EnumerateFiles(appDir, "*.xaml", SearchOption.AllDirectories))
        {
            // Colors.xaml es la única fuente legítima del literal: ahí se define el token.
            if (Path.GetFileName(path) == "Colors.xaml")
            {
                continue;
            }

            var text = File.ReadAllText(path);
            if (text.Contains("Color=\"Black\"", StringComparison.Ordinal) ||
                text.Contains("Color=\"#000000\"", StringComparison.Ordinal) ||
                text.Contains("Color=\"#FF000000\"", StringComparison.Ordinal))
            {
                offenders.Add(Path.GetRelativePath(RepositoryRoot, path));
            }
        }

        Assert.True(offenders.Count == 0,
            $"Sombra negra sin tokenizar (debería usar {{DynamicResource ColorShadow}}) en: {string.Join(", ", offenders)}.");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Nexo.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("No se encontró Nexo.slnx desde el directorio de pruebas.");
    }
}
