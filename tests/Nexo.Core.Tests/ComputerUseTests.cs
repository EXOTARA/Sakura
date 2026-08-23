using Nexo.Core.ComputerUse;
using Nexo.Core.Permissions;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D17 (Fase 7) — el roadmap dice que *"automatizar mouse/teclado es frágil e inseguro si se
/// usa como primera opción en vez de último recurso — de ahí el orden estricto"*. Lo que se prueba
/// aquí es que ese orden se respeta siempre, y que en esta fase no se ejecuta nada.
/// </summary>
public sealed class ComputerUseTests
{
    private static PermissionSettings Permissions(PermissionLevel level)
    {
        var settings = new PermissionSettings();
        settings.Normalize();
        settings.For(SakuraCapability.ComputerUse).Level = level;
        return settings;
    }

    // ---------- El orden manda ----------

    [Fact]
    public void TheSafestAvailableMethodWins()
    {
        var choice = ComputerUseMethodPolicy.Choose(
        [
            ComputerUseMethod.Portapapeles,
            ComputerUseMethod.ApiOficial,
            ComputerUseMethod.UiAutomation
        ]);

        Assert.Equal(ComputerUseMethod.ApiOficial, choice.Method);
    }

    [Fact]
    public void ALessSafeMethodIsNotChosen_JustBecauseItWouldAlsoWork()
    {
        // Si existe la API oficial no se usa UI Automation: es la diferencia entre una
        // automatización que sobrevive a una actualización y una que no.
        var choice = ComputerUseMethodPolicy.Choose([ComputerUseMethod.UiAutomation, ComputerUseMethod.ApiOficial]);

        Assert.Equal(ComputerUseMethod.ApiOficial, choice.Method);
        Assert.True(ComputerUseMethodPolicy.IsSafestAvailable(
            choice.Method!.Value, [ComputerUseMethod.UiAutomation, ComputerUseMethod.ApiOficial]));
    }

    [Fact]
    public void TheOrderIsTheOneTheRoadmapFixes()
    {
        // Menor es más seguro, y ratón/teclado es el último.
        var ordered = Enum.GetValues<ComputerUseMethod>().OrderBy(method => (int)method).ToArray();

        Assert.Equal(ComputerUseMethod.ApiOficial, ordered.First());
        Assert.Equal(ComputerUseMethod.RatonTeclado, ordered.Last());
    }

    // ---------- Ratón y teclado ----------

    [Fact]
    public void SimulatedInput_IsNotUsedUnlessItWasEnabledOnPurpose()
    {
        var choice = ComputerUseMethodPolicy.Choose([ComputerUseMethod.RatonTeclado]);

        Assert.Null(choice.Method);
        Assert.Contains(choice.Rejected, rejection => rejection.Method == ComputerUseMethod.RatonTeclado);
    }

    [Fact]
    public void SimulatedInput_IsUsedOnlyWhenNothingElseIsThere()
    {
        var alone = ComputerUseMethodPolicy.Choose([ComputerUseMethod.RatonTeclado], simulatedInputAllowed: true);
        Assert.Equal(ComputerUseMethod.RatonTeclado, alone.Method);

        var withCompany = ComputerUseMethodPolicy.Choose(
            [ComputerUseMethod.RatonTeclado, ComputerUseMethod.Portapapeles], simulatedInputAllowed: true);
        Assert.Equal(ComputerUseMethod.Portapapeles, withCompany.Method);
    }

    [Fact]
    public void AMethodSkippedForBeingLessSafe_IsNotReportedAsRejected()
    {
        // Ratón y teclado no se "descarta" aquí: es que nunca llegó a ser candidato, porque el
        // portapapeles es más seguro y va antes. Listarlo como descartado daría a entender que se
        // consideró y se dijo que no, que no es lo que pasó.
        var choice = ComputerUseMethodPolicy.Choose(
            [ComputerUseMethod.RatonTeclado, ComputerUseMethod.Portapapeles]);

        Assert.Equal(ComputerUseMethod.Portapapeles, choice.Method);
        Assert.Empty(choice.Rejected);
    }

    [Fact]
    public void AMethodActuallyRejected_IsShown_NotHidden()
    {
        // Aquí sí se consideró y se dijo que no: era el único disponible.
        var choice = ComputerUseMethodPolicy.Choose([ComputerUseMethod.RatonTeclado]);

        Assert.Null(choice.Method);
        var rejection = Assert.Single(choice.Rejected);
        Assert.Equal(ComputerUseMethod.RatonTeclado, rejection.Method);
        Assert.Contains("último recurso", rejection.Reason);
    }

    [Fact]
    public void WithNoMethodAvailable_NothingIsAttempted()
    {
        var choice = ComputerUseMethodPolicy.Choose([]);

        Assert.Null(choice.Method);
        Assert.Contains("no lo intento", choice.Reason);
    }

    [Fact]
    public void UnknownMethodValues_AreIgnored() =>
        Assert.Null(ComputerUseMethodPolicy.Choose([(ComputerUseMethod)99]).Method);

    // ---------- El plan ----------

    [Fact]
    public void WithComputerUseBlocked_ThePlanSaysSo()
    {
        var plan = ComputerUsePlanner.Build(
            new ComputerUseIntent("Vaciar la papelera"),
            [ComputerUseMethod.ShellSeguro],
            Permissions(PermissionLevel.Bloqueado),
            AutonomyLevel.Proponer);

        Assert.False(plan.CanBeExecuted);
        Assert.Contains("bloqueada", plan.Blocker!);
    }

    [Fact]
    public void ThePlanIsStillBuiltWhenSomethingBlocksIt()
    {
        // Un plan que no llega a formarse deja a la persona sin saber qué haría falta para que sí.
        var plan = ComputerUsePlanner.Build(
            new ComputerUseIntent("Ver la red"),
            [ComputerUseMethod.ShellSeguro],
            Permissions(PermissionLevel.Bloqueado),
            AutonomyLevel.Proponer);

        Assert.NotNull(plan.Blocker);
        Assert.True(plan.Choice.HasMethod);
    }

    [Fact]
    public void NothingIsExecutedAtLevelsOneToThree()
    {
        // La Fase 7 es explícita en que "no se salta niveles del modelo de autonomía".
        foreach (var level in new[] { AutonomyLevel.Ver, AutonomyLevel.Guiar, AutonomyLevel.Proponer })
        {
            var plan = ComputerUsePlanner.Build(
                new ComputerUseIntent("Ver la red"),
                [ComputerUseMethod.ShellSeguro],
                Permissions(PermissionLevel.Permitido),
                level);

            Assert.False(plan.CanBeExecuted);
            Assert.Contains("no lo hago", plan.Blocker!);
        }
    }

    [Fact]
    public void MandatoryCategories_StillApplyToComputerUse()
    {
        var plan = ComputerUsePlanner.Build(
            new ComputerUseIntent("Borrar la carpeta", Categories: [MandatoryConfirmation.BorradoIrreversible]),
            [ComputerUseMethod.ShellSeguro],
            Permissions(PermissionLevel.Permitido),
            AutonomyLevel.Proponer);

        // No queda denegado, pero tampoco pasa sin preguntar: eso lo decide el broker, y el plan lo
        // arrastra al no poder ejecutarse todavía.
        Assert.False(plan.CanBeExecuted);
    }

    [Fact]
    public void OnlyWhatCanBeUndoneWithCertainty_IsMarkedReversible()
    {
        var clipboard = ComputerUsePlanner.Build(
            new ComputerUseIntent("Copiar algo"),
            [ComputerUseMethod.Portapapeles],
            Permissions(PermissionLevel.Permitido),
            AutonomyLevel.Proponer);

        var uiAutomation = ComputerUsePlanner.Build(
            new ComputerUseIntent("Pulsar un botón"),
            [ComputerUseMethod.UiAutomation],
            Permissions(PermissionLevel.Permitido),
            AutonomyLevel.Proponer);

        Assert.True(clipboard.IsReversible);
        Assert.False(uiAutomation.IsReversible);
    }

    // ---------- Autonomía ----------

    [Theory]
    [InlineData(AutonomyLevel.Ver)]
    [InlineData(AutonomyLevel.Guiar)]
    [InlineData(AutonomyLevel.Proponer)]
    public void LevelsOneToThree_AreAvailable(AutonomyLevel level) =>
        Assert.True(ComputerUseAutonomyPolicy.IsAvailable(level));

    /// <summary>
    /// Diseño D18 — el nivel 4 se abrió porque existen las tres cosas que el modelo de confianza
    /// pide antes de ejecutar: el broker (D16), el Audit Log (D13) y una reversión real para el
    /// único método que la admite.
    /// </summary>
    [Fact]
    public void LevelFour_IsAvailableSinceTheBrokerAndTheAuditLogExist()
    {
        Assert.True(ComputerUseAutonomyPolicy.IsAvailable(AutonomyLevel.EjecutarUnPaso));
        Assert.True(ComputerUseAutonomyPolicy.CanExecute(AutonomyLevel.EjecutarUnPaso));
    }

    [Theory]
    [InlineData(AutonomyLevel.ColaborarConConfirmaciones)]
    [InlineData(AutonomyLevel.AutomatizarSecuencia)]
    public void ChainedLevels_AreNotAvailableYet(AutonomyLevel level)
    {
        // Encadenar y automatizar son problemas distintos de "hacer una acción confirmada".
        Assert.False(ComputerUseAutonomyPolicy.IsAvailable(level));
        Assert.False(ComputerUseAutonomyPolicy.CanExecute(level));
    }

    [Fact]
    public void TheDefaultLevel_DoesNotRiseWithD18()
    {
        // Ejecutar es posible; estar encendido es otra decisión. Y además el permiso llega
        // Bloqueado, así que hacen falta dos.
        Assert.Equal(AutonomyLevel.Proponer, ComputerUseAutonomyPolicy.Default);
        Assert.False(ComputerUseAutonomyPolicy.CanExecute(ComputerUseAutonomyPolicy.Default));
    }

    [Fact]
    public void AnUnknownLevel_FailsClosed() =>
        Assert.False(ComputerUseAutonomyPolicy.IsAvailable((AutonomyLevel)99));

    // ---------- La lista de comandos ----------

    [Fact]
    public void EverySafeCommandIsReadOnly_AndUsesNoInterpreter()
    {
        // Si algún día entra un comando que escribe, esta prueba tiene que fallar aquí y no en el
        // equipo de alguien.
        var interpreters = new[] { "powershell", "pwsh", "cmd", "wmic", "wscript", "cscript", "bash", "python" };

        Assert.All(SafeShellCatalog.All, command =>
        {
            Assert.True(SafeShellCatalog.IsReadOnly(command));
            Assert.DoesNotContain(interpreters, interpreter =>
                command.Executable.Contains(interpreter, StringComparison.OrdinalIgnoreCase));

            // Ninguno escribe a un archivo.
            Assert.DoesNotContain('>', command.Arguments);
            Assert.DoesNotContain("/output", command.Arguments, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void SafeCommandsAreFoundById_AndUnknownOnesAreNot()
    {
        Assert.NotNull(SafeShellCatalog.Find("net.ip"));
        Assert.Null(SafeShellCatalog.Find("rm -rf"));
        Assert.Null(SafeShellCatalog.Find(null));
    }

    [Fact]
    public void CommandsThatEnumerateTheWholeSystem_GetLongerThanTheDefault()
    {
        // Esto se descubrió con la CI en rojo: `driverquery` recorre todos los controladores y en
        // una máquina virtual pasa de veinte segundos sin estar rota. Con un único tiempo para
        // toda la lista, el usuario recibía «tardó demasiado» por un comando que iba a terminar
        // bien. El número no es un detalle de implementación: es la diferencia entre un
        // diagnóstico y un mensaje de error.
        foreach (var id in new[] { "sys.drivers", "sys.info" })
        {
            var command = SafeShellCatalog.Find(id);
            Assert.NotNull(command);
            Assert.True(
                command!.Timeout > SafeShellCommand.DefaultTimeout,
                $"«{command.Title}» enumera medio sistema y necesita más que el tiempo corriente.");
        }
    }

    [Fact]
    public void TheQuickCommands_KeepTheDefault()
    {
        // Y lo contrario: alargar el tiempo de todos «por si acaso» convertiría un comando colgado
        // en un minuto de espera mirando una ventana quieta.
        foreach (var id in new[] { "net.ip", "net.dns" })
        {
            var command = SafeShellCatalog.Find(id);
            Assert.NotNull(command);
            Assert.Equal(SafeShellCommand.DefaultTimeout, command!.Timeout);
        }
    }
}
