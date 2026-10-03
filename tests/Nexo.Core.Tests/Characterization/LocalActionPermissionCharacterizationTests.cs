using Nexo.Core.Automation;
using Nexo.Core.Commands;

namespace Nexo.Core.Tests.Characterization;

/// <summary>
/// Fase 1.1 — congela la distinción normativa **abrir una terminal ≠ ejecutar en ella**
/// (`PRODUCT_VISION` §F, `SECURITY_MODEL` §Matriz, `TEST_MATRIX` escenarios 21 y 22) tal como
/// está implementada **hoy**.
///
/// Estado real medido el 2026-07-23, con dos rutas distintas y una divergencia registrada:
///
/// <list type="bullet">
///   <item>
///     <b>Ruta de orden directa</b> (voz o paleta → <see cref="LocalCommandType.OpenPowerShell"/>):
///     abre la terminal <b>sin confirmación</b>. Cumple la decisión F.
///   </item>
///   <item>
///     <b>Ruta de rutina</b> (<see cref="AutomationActionType.OpenTerminal"/>): hoy es
///     <see cref="AutomationRiskLevel.Sensitive"/> y por tanto <b>sí</b> pide confirmación.
///     La decisión F dice que debería ser <c>Reversible</c>. <b>Divergencia conocida</b>,
///     se corrige en la Fase 5, no aquí: la fase 1.1 congela, no arregla.
///   </item>
///   <item>
///     <b>Ejecutar un comando arbitrario no existe todavía</b> como acción representable.
///     No hay <c>ExecuteShellCommand</c> en <see cref="AutomationActionType"/> ni en
///     <see cref="LocalCommandType"/>. El requisito "siempre preguntar" se cumple hoy de forma
///     vacía porque la capacidad no existe; se introducirá en la Fase 5.
///   </item>
/// </list>
/// </summary>
public sealed class LocalActionPermissionCharacterizationTests
{
    [Fact]
    public void OpeningATerminalByVoice_IsAPlainLocalCommand_WithNoConfirmationStep()
    {
        var parser = new NaturalCommandParser();

        var result = parser.Parse("abre PowerShell");

        Assert.Equal(CommandRoute.Local, result.Route);
        Assert.Equal(LocalCommandType.OpenPowerShell, result.Intent?.Type);

        // La ruta local no expone ninguna noción de confirmación: MainWindow.OpenShell
        // lanza el proceso directamente. La ausencia de `CommandRoute.Sensitive` en el
        // resultado es la evidencia de que no hay paso de aprobación.
        Assert.NotEqual(CommandRoute.Sensitive, result.Route);
    }

    [Theory]
    [InlineData("abre PowerShell", LocalCommandType.OpenPowerShell)]
    [InlineData("ábreme el PowerShell", LocalCommandType.OpenPowerShell)]
    public void OpeningATerminal_CarriesNoCommandPayload(string prompt, LocalCommandType expected)
    {
        var parser = new NaturalCommandParser();

        var intent = parser.Parse(prompt).Intent;

        Assert.Equal(expected, intent?.Type);

        // Ninguna orden de "abrir terminal" transporta texto ejecutable. Es la barrera
        // estructural que impide que abrir se convierta en ejecutar.
        Assert.Null(intent?.Target);
    }

    [Fact]
    public void NoLocalCommandTypeCanExecuteAnArbitraryShellCommand()
    {
        // Congela la superficie: si alguien añade una capacidad de ejecución arbitraria,
        // esta prueba falla y obliga a pasar por el modelo de permisos de la Fase 5.
        var executionLikeCommands = Enum.GetNames<LocalCommandType>()
            .Where(name =>
                name.Contains("Execute", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Run", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Script", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.Empty(executionLikeCommands);
    }

    [Fact]
    public void NoAutomationActionTypeCanExecuteAnArbitraryShellCommand()
    {
        var executionLikeActions = Enum.GetNames<AutomationActionType>()
            .Where(name =>
                name.Contains("Execute", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Shell", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Script", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Command", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.Empty(executionLikeActions);
    }

    [Fact]
    public void OpenTerminalInARoutine_IsStillSensitiveToday()
    {
        // DIVERGENCIA CONOCIDA frente a la decisión F (debería ser Reversible).
        // Se congela el estado actual; el cambio pertenece a la Fase 5.
        var action = new AutomationAction
        {
            Type = AutomationActionType.OpenTerminal,
            WorkingDirectory = @"C:\Dev"
        };

        Assert.Equal(AutomationRiskLevel.Sensitive, AutomationPermissionPolicy.GetRisk(action));
    }

    [Fact]
    public void ARoutineContainingATerminalStep_RequiresConfirmation()
    {
        var routine = new RoutineDefinition
        {
            Name = "Programación",
            TriggerPhrase = "modo programación",
            RequiresConfirmation = false,
            Steps =
            [
                new AutomationAction
                {
                    Type = AutomationActionType.OpenTerminal,
                    WorkingDirectory = @"C:\Dev"
                }
            ]
        };

        Assert.True(AutomationPermissionPolicy.RequiresConfirmation(routine));
    }

    [Fact]
    public void ADisabledSensitiveStep_DoesNotForceConfirmation()
    {
        var routine = new RoutineDefinition
        {
            Name = "Programación",
            TriggerPhrase = "modo programación",
            RequiresConfirmation = false,
            Steps =
            [
                new AutomationAction
                {
                    Type = AutomationActionType.OpenTerminal,
                    WorkingDirectory = @"C:\Dev",
                    IsEnabled = false
                }
            ]
        };

        Assert.False(AutomationPermissionPolicy.RequiresConfirmation(routine));
    }

    [Fact]
    public void UnknownActionTypes_DefaultToBlocked_NotAllowed()
    {
        // `SECURITY_MODEL` §Nivel Bloqueado: el default de un tipo desconocido es bloquear.
        var action = new AutomationAction { Type = AutomationActionType.None };

        Assert.Equal(AutomationRiskLevel.Blocked, AutomationPermissionPolicy.GetRisk(action));
        Assert.False(AutomationPermissionPolicy.IsAllowed(action, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void EveryActionTypeHasAnExplicitRisk_AndNoneIsAccidentallyPermissive()
    {
        // Recorre el enum completo: ningún tipo debe quedar sin clasificar.
        foreach (var type in Enum.GetValues<AutomationActionType>())
        {
            var risk = AutomationPermissionPolicy.GetRisk(new AutomationAction { Type = type });

            Assert.True(
                Enum.IsDefined(risk),
                $"El tipo {type} produjo un nivel de riesgo no definido.");
        }
    }

    [Fact]
    public void OpenTerminalCarriesNoCallerSuppliedArguments()
    {
        // `NexoAutomationActionExecutor.OpenTerminal` ignora `action.Arguments` y construye
        // siempre `-NoExit` con la carpeta en WorkingDirectory. Esa es la barrera estructural que hoy
        // separa "abrir la terminal" de "ejecutar en la terminal" en la ruta de rutinas.
        // Se congela como invariante: si alguien empieza a reenviar `Arguments` a la terminal,
        // se convierte en ejecución arbitraria y debe pasar por confirmación.
        var action = new AutomationAction
        {
            Type = AutomationActionType.OpenTerminal,
            WorkingDirectory = @"C:\Dev",
            Arguments = "-Command \"Remove-Item C:\\ -Recurse\""
        };

        // La validación acepta la acción porque solo mira el directorio de trabajo:
        // los argumentos son inertes para este tipo.
        Assert.True(AutomationPermissionPolicy.IsAllowed(action, out _));
        Assert.Equal(AutomationRiskLevel.Sensitive, AutomationPermissionPolicy.GetRisk(action));
    }

    [Fact]
    public void OpenApplication_ForwardingShellArguments_NowRequiresConfirmation()
    {
        // El defecto D2 de la fase 1.1 está corregido en 1.1.1.
        //
        // `OpenApplication` sigue reenviando `action.Arguments` al proceso, pero los
        // argumentos forman ahora parte de la evaluación tipada de riesgo: si el objetivo es
        // un intérprete y hay argumentos, la acción es `Sensitive` y exige confirmación.
        // Ver `ShellExecutionPolicyTests` para la batería completa.
        var action = new AutomationAction
        {
            Type = AutomationActionType.OpenApplication,
            Target = "powershell.exe",
            Arguments = "-Command \"Get-Process\""
        };

        Assert.True(AutomationPermissionPolicy.IsAllowed(action, out _));
        Assert.Equal(AutomationRiskLevel.Sensitive, AutomationPermissionPolicy.GetRisk(action));

        var routine = new RoutineDefinition
        {
            Name = "Sin confirmación",
            TriggerPhrase = "arranque",
            RequiresConfirmation = false,
            Steps = [action]
        };

        Assert.True(AutomationPermissionPolicy.RequiresConfirmation(routine));
    }

    [Fact]
    public void DisabledActions_AreAllowedWithoutValidation()
    {
        // Conducta actual: una acción deshabilitada pasa la validación sin revisarse.
        var action = new AutomationAction
        {
            Type = AutomationActionType.OpenApplication,
            Target = string.Empty,
            IsEnabled = false
        };

        Assert.True(AutomationPermissionPolicy.IsAllowed(action, out _));
    }
}
