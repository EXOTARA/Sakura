using Nexo.Core.Permissions;

namespace Nexo.Core.Tests;

/// <summary>
/// Lens y Flow ahora consultan al broker con la aplicación destino rellena. Estas pruebas fijan la
/// matriz completa contra <see cref="PermissionBroker.Decide"/> con las mismas peticiones que la
/// ventana construye: antes nadie las consultaba y lo elegido en Personalizar no se cumplía.
/// </summary>
public sealed class LensFlowPermissionTests
{
    private const string Target = "Gestor de contraseñas — keepass";

    private static PermissionSettings With(SakuraCapability capability, PermissionLevel level, params string[] excluded)
    {
        var settings = new PermissionSettings();
        var entry = settings.For(capability);
        entry.Level = level;
        entry.ExcludedApps = [.. excluded];
        return settings;
    }

    [Theory]
    [InlineData(SakuraCapability.Lens)]
    [InlineData(SakuraCapability.Flow)]
    public void Bloqueado_Denies(SakuraCapability capability)
    {
        var decision = PermissionBroker.Decide(
            new PermissionRequest(capability, "prueba", Target),
            With(capability, PermissionLevel.Bloqueado));

        Assert.True(decision.IsDenied);
    }

    [Theory]
    [InlineData(SakuraCapability.Lens)]
    [InlineData(SakuraCapability.Flow)]
    public void Preguntar_RequiresConfirmation(SakuraCapability capability)
    {
        var decision = PermissionBroker.Decide(
            new PermissionRequest(capability, "prueba", Target),
            With(capability, PermissionLevel.Preguntar));

        Assert.Equal(PermissionOutcome.RequiereConfirmacion, decision.Outcome);
    }

    [Theory]
    [InlineData(SakuraCapability.Lens)]
    [InlineData(SakuraCapability.Flow)]
    public void Permitido_Proceeds(SakuraCapability capability)
    {
        var decision = PermissionBroker.Decide(
            new PermissionRequest(capability, "prueba", Target),
            With(capability, PermissionLevel.Permitido));

        Assert.True(decision.MayProceedWithoutAsking);
    }

    [Theory]
    [InlineData(SakuraCapability.Lens)]
    [InlineData(SakuraCapability.Flow)]
    public void ExcludedApp_WinsOverPermitido(SakuraCapability capability)
    {
        var decision = PermissionBroker.Decide(
            new PermissionRequest(capability, "prueba", Target),
            With(capability, PermissionLevel.Permitido, "KeePass"));

        Assert.True(decision.IsDenied);
    }

    [Theory]
    [InlineData(SakuraCapability.Lens)]
    [InlineData(SakuraCapability.Flow)]
    public void ExcludedApp_DoesNotAffectOtherApps(SakuraCapability capability)
    {
        var decision = PermissionBroker.Decide(
            new PermissionRequest(capability, "prueba", "Bloc de notas — notepad"),
            With(capability, PermissionLevel.Permitido, "KeePass"));

        Assert.True(decision.MayProceedWithoutAsking);
    }

    [Fact]
    public void Exclusion_IsPerCapability()
    {
        // Excluir una aplicación de Lens no la excluye de Flow.
        var settings = With(SakuraCapability.Lens, PermissionLevel.Permitido, "KeePass");
        settings.For(SakuraCapability.Flow).Level = PermissionLevel.Permitido;

        var flow = PermissionBroker.Decide(
            new PermissionRequest(SakuraCapability.Flow, "prueba", Target), settings);

        Assert.True(flow.MayProceedWithoutAsking);
    }

    [Fact]
    public void WithoutTarget_ExclusionsCannotMatch()
    {
        // Documenta por qué la ventana SIEMPRE debe rellenar TargetApp: sin él, la exclusión no
        // se dispara ni aunque la aplicación esté en la lista.
        var decision = PermissionBroker.Decide(
            new PermissionRequest(SakuraCapability.Lens, "prueba"),
            With(SakuraCapability.Lens, PermissionLevel.Permitido, "KeePass"));

        Assert.True(decision.MayProceedWithoutAsking);
    }
}
