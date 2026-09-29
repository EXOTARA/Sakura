using Nexo.App.Automation;

namespace Nexo.App.Tests;

public sealed class OpenTerminalStartInfoTests
{
    [Theory]
    [InlineData(@"C:\Proyectos\Sakura")]
    [InlineData("C:\\Proyectos\\a\u2019;Set-Content x y;\u2019")]
    [InlineData("C:\\Proyectos\\a'; calc; '")]
    public void FolderNeverEntersThePowerShellCommandLine(string directory)
    {
        var info = NexoAutomationActionExecutor.BuildTerminalStartInfo(directory);

        // Comillas tipográficas (’ ‘ ‚ ‛) cierran una cadena de PowerShell igual que la simple: la
        // carpeta solo puede viajar como directorio de trabajo, nunca como texto que se interprete.
        Assert.Equal("-NoExit", info.Arguments);
        Assert.Equal(directory, info.WorkingDirectory);
    }

    [Theory]
    [InlineData(@"C:\Users\adler\Proyectos\Sakura", "Sakura")]
    [InlineData(@"C:\Users\adler\Proyectos\Sakura\", "Sakura")]
    [InlineData("\"C:\\Users\\adler\\Mis cosas\"", "Mis cosas")]
    [InlineData(@"\\host\share\carpeta", "carpeta")]
    [InlineData("code", "code")]
    [InlineData(@"C:\", "la carpeta")]
    [InlineData("", "la carpeta")]
    public void MessagesShowOnlyTheLastFolderName_NeverTheFullPath(string path, string expected)
    {
        var shown = NexoAutomationActionExecutor.DisplayName(path, "la carpeta");

        Assert.Equal(expected, shown);
        Assert.DoesNotContain("adler", shown);
    }
}
