using Nexo.Core.Ambient;
using Nexo.Core.Automation;

namespace Nexo.Core.Tests.Characterization;

/// <summary>
/// Revisión adversarial (2026-10-03) de la auditoría de seguridad de 0.30.38: rodeos que la primera
/// versión de <see cref="ShellExecutionPolicy.RequiresConfirmationToOpen"/> dejaba pasar sin preguntar.
/// Cada caso devolvía <see cref="AutomationRiskLevel.Reversible"/> antes del arreglo.
/// </summary>
public sealed class ShellPolicyAdversarialReviewTests
{
    private static AutomationRiskLevel Risk(string target, string arguments = "", string workingDirectory = "") =>
        AutomationPermissionPolicy.GetRisk(new AutomationAction
        {
            Type = AutomationActionType.OpenApplication,
            Target = target,
            Arguments = arguments,
            WorkingDirectory = workingDirectory
        });

    // La ruta de red iba pegada a un interruptor: el trozo «/select,\\host…» no empieza por «\\».
    [Theory]
    [InlineData("explorer.exe", @"/select,\\host\share\x.exe")]
    [InlineData("explorer.exe", @"/root,\\host\share")]
    [InlineData("programa.exe", @"--carpeta=\\host\share")]
    [InlineData("programa.exe", "--abrir=file://host/share/x")]
    [InlineData("programa.exe", "--abrir=ms-msdt:/id PCWDiagnostic")]
    [InlineData("programa.exe", "/x;powershell")]
    public void NetworkPathOrSchemeGluedToASwitch_RequiresConfirmation(string target, string arguments) =>
        Assert.Equal(AutomationRiskLevel.Sensitive, Risk(target, arguments));

    // Windows no agrupa con comilla simple; el tokenizador sí, y se tragaba la ruta de red.
    [Theory]
    [InlineData("explorer.exe", @"it's \\host\share\x")]
    [InlineData("programa.exe", "a' powershell -c calc 'b")]
    public void SingleQuoteDoesNotHideAnArgumentFromTheCheck(string target, string arguments) =>
        Assert.Equal(AutomationRiskLevel.Sensitive, Risk(target, arguments));

    // El esquema ms-msdt y ms-appinstaller ya preguntaban; el binario y los archivos equivalentes no.
    [Theory]
    [InlineData("msdt.exe", "/id PCWDiagnostic /skip force /param \"x\"")]
    [InlineData("msdt")]
    [InlineData("hh.exe", @"C:\Users\yo\Descargas\ayuda.chm")]
    [InlineData("mmc.exe", @"C:\Users\yo\Descargas\consola.msc")]
    [InlineData(@"C:\Users\yo\Descargas\ayuda.chm")]
    [InlineData(@"C:\Users\yo\Descargas\consola.msc")]
    [InlineData(@"C:\Users\yo\Descargas\app.appinstaller")]
    [InlineData(@"C:\Users\yo\Descargas\app.msix")]
    [InlineData(@"C:\Users\yo\Descargas\app.appx")]
    [InlineData(@"C:\Users\yo\Descargas\x.library-ms")]
    [InlineData(@"C:\Users\yo\Descargas\x.searchConnector-ms")]
    [InlineData("search:query=x&crumb=location:\\\\host\\share")]
    [InlineData("ms-its:C:\\x.chm::/a.htm")]
    [InlineData("its:C:\\x.chm::/a.htm")]
    [InlineData("mk:@MSITStore:C:\\x.chm::/a.htm")]
    public void ExecutorsAndFilesEquivalentToRiskySchemes_RequireConfirmation(string target, string arguments = "") =>
        Assert.Equal(AutomationRiskLevel.Sensitive, Risk(target, arguments));

    // «%ComSpec%» no tiene ruta que recortar: sin expandir no parecía un intérprete.
    [Fact]
    public void ShellBehindAnEnvironmentVariable_WithArguments_RequiresConfirmation()
    {
        const string variable = "SAKURA_TEST_SHELL_REVIEW";
        Environment.SetEnvironmentVariable(variable, @"C:\Windows\System32\cmd.exe");
        try
        {
            Assert.Equal(AutomationRiskLevel.Sensitive, Risk($"%{variable}%", "/c calc"));
            // Sin argumentos sigue siendo abrir la terminal (decisión F).
            Assert.Equal(AutomationRiskLevel.Reversible, Risk($"%{variable}%"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    // La carpeta de trabajo de «Abrir VS Code» (code .) no se revisaba: una ruta de red abría sin preguntar.
    [Theory]
    [InlineData(@"\\host\share\proyecto")]
    [InlineData("//host/share/proyecto")]
    [InlineData("\"\\\\host\\share\"")]
    [InlineData("file://host/share")]
    public void NetworkWorkingDirectory_RequiresConfirmation(string workingDirectory) =>
        Assert.Equal(AutomationRiskLevel.Sensitive, Risk("code", ".", workingDirectory));

    // Lo que ya no preguntaba sigue sin preguntar.
    [Theory]
    [InlineData("code", ".", @"C:\Dev\Nexo")]
    [InlineData("code", ".", "{project}")]
    [InlineData("code", ".", "")]
    [InlineData("chrome.exe", "https://example.com/?a=b,c;d", "")]
    [InlineData("chrome.exe", "https://example.com/app.js", "")]
    [InlineData("notepad.exe", "notas.txt", "")]
    [InlineData("explorer.exe", @"/select,C:\Users\yo\notas.txt", "")]
    public void OrdinaryOpenings_StillDoNotAsk(string target, string arguments, string workingDirectory) =>
        Assert.Equal(AutomationRiskLevel.Reversible, Risk(target, arguments, workingDirectory));

    // Consola clásica: Windows atribuye la ventana al primer programa que corre dentro (python, ssh…).
    [Theory]
    [InlineData("python", "ConsoleWindowClass")]
    [InlineData("ssh", "ConsoleWindowClass")]
    [InlineData("ubuntu", "ConsoleWindowClass")]
    [InlineData("node", "CASCADIA_HOSTING_WINDOW_CLASS")]
    public void ConsoleWindowClass_IsATerminal_WhateverTheProcess(string process, string windowClass)
    {
        Assert.True(TerminalWindowPolicy.WouldRunCommands(process, windowClass, "uno\ndos"));
        Assert.False(TerminalWindowPolicy.WouldRunCommands(process, windowClass, "una línea"));
    }

    [Fact]
    public void OrdinaryWindowClass_DoesNotTurnAnEditorIntoATerminal() =>
        Assert.False(TerminalWindowPolicy.WouldRunCommands("notepad", "Notepad", "uno\ndos"));
}
