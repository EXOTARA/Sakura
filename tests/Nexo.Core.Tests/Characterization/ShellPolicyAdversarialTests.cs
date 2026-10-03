using Nexo.Core.Ambient;
using Nexo.Core.Automation;

namespace Nexo.Core.Tests.Characterization;

/// <summary>Pruebas adversarias (tester 2026-09-28) contra ShellExecutionPolicy.RequiresConfirmationToOpen.</summary>
public sealed class ShellPolicyAdversarialTests
{
    private static AutomationRiskLevel Risk(string target, string arguments = "") =>
        AutomationPermissionPolicy.GetRisk(new AutomationAction
        {
            Type = AutomationActionType.OpenApplication,
            Target = target,
            Arguments = arguments
        });

    // El chequeo de extensión/UNC/URL solo mira Target: meter el script en Arguments de un
    // ejecutable ordinario que lo abre con el shell lo evita.
    [Theory]
    [InlineData("explorer.exe", @"C:\Users\yo\Descargas\factura.vbs")]
    [InlineData("explorer.exe", @"C:\Users\yo\Descargas\x.bat")]
    [InlineData("explorer.exe", @"\\host\share\x.exe")]
    [InlineData("conhost.exe", "calc.exe")]
    [InlineData("wt.exe", "new-tab calc.exe")]
    public void ScriptUncOrUrlInArguments_OfAnOrdinaryLauncher_ShouldRequireConfirmation(string target, string arguments) =>
        Assert.Equal(AutomationRiskLevel.Sensitive, Risk(target, arguments));

    // Extensiones que ShellExecute abre ejecutando código y que no están en RiskyExtensions.
    [Theory]
    [InlineData("x.cpl")]
    [InlineData("x.pif")]
    [InlineData("x.application")]
    [InlineData("x.appref-ms")]
    [InlineData("x.jar")]
    [InlineData("x.py")]
    [InlineData("x.pyw")]
    [InlineData("x.wsh")]
    [InlineData("x.sct")]
    [InlineData("x.msp")]
    [InlineData("x.settingcontent-ms")]
    [InlineData("x.diagcab")]
    public void OtherCodeRunningExtensions_ShouldRequireConfirmation(string target) =>
        Assert.Equal(AutomationRiskLevel.Sensitive, Risk(target));

    // Esquemas que descargan/ejecutan y no están en RiskySchemes.
    [Theory]
    [InlineData("ms-officecmd:x")]
    [InlineData(@"ms-word:ofe|u|http://ejemplo.invalid/x.docm")]
    [InlineData("vbscript:x")]
    [InlineData("shell:startup")]
    public void OtherRiskySchemes_ShouldRequireConfirmation(string target) =>
        Assert.Equal(AutomationRiskLevel.Sensitive, Risk(target));

    // Nombre corto 8.3: ShellExecute lo resuelve al ejecutable real si el volumen tiene 8.3 activo.
    [Fact]
    public void ShortName_OfPowerShell_ShouldRequireConfirmation() =>
        Assert.Equal(AutomationRiskLevel.Sensitive, Risk("POWERS~1.EXE"));

    // Tests de terminales
    [Theory]
    [InlineData("ConEmu64")]
    [InlineData("Hyper")]
    [InlineData("Tabby")]
    [InlineData("ttermpro")]
    [InlineData("MobaXterm")]
    [InlineData("Terminus")]
    [InlineData("cmder")]
    public void TerminalLikeWindows_ShouldBeRefusedMultilineText(string process) =>
        Assert.True(TerminalWindowPolicy.WouldRunCommands(process, "ls\nrm -rf x"));
}
