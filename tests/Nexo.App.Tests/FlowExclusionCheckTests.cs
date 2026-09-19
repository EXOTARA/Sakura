using Nexo.App.Permissions;
using Nexo.Core.Ambient;
using Nexo.Core.Permissions;

namespace Nexo.App.Tests;

/// <summary>Lo que Flow le pregunta al broker antes de grabar y antes de escribir.</summary>
public sealed class FlowExclusionCheckTests
{
    private static PermissionSettings Flow(PermissionLevel level, params string[] excluded)
    {
        var settings = new PermissionSettings();
        var entry = settings.For(SakuraCapability.Flow);
        entry.Level = level;
        entry.ExcludedApps = [.. excluded];
        return settings;
    }

    [Fact]
    public void DescribeTarget_JoinsTitleAndProcess_AndHandlesMissing()
    {
        Assert.Equal(
            "Contraseñas — keepass",
            FlowExclusionCheck.DescribeTarget(new AmbientContextSnapshot("Contraseñas", "keepass", false)));
        Assert.Equal(
            "keepass",
            FlowExclusionCheck.DescribeTarget(new AmbientContextSnapshot(null, "keepass", false)));
        Assert.Null(FlowExclusionCheck.DescribeTarget(null));
        Assert.Null(FlowExclusionCheck.DescribeTarget(new AmbientContextSnapshot(" ", null, true)));
    }

    [Fact]
    public void ExcludedProcess_IsDenied_EvenWhenPermitido()
    {
        var target = FlowExclusionCheck.DescribeTarget(new AmbientContextSnapshot("Bóveda", "keepass", false));

        Assert.True(FlowExclusionCheck.Evaluate(Flow(PermissionLevel.Permitido, "keepass"), "x", target).IsDenied);
    }

    [Fact]
    public void Bloqueado_IsDenied()
    {
        Assert.True(FlowExclusionCheck.Evaluate(Flow(PermissionLevel.Bloqueado), "x", "Word").IsDenied);
    }

    [Fact]
    public void Preguntar_IsNotDenied_BecauseAskingIsNotResolvedInFlow()
    {
        // Un diálogo robaría el foco a la aplicación destino; por eso «Preguntar» no detiene el
        // dictado. Si esto cambia, hay que resolver antes el foco.
        Assert.False(FlowExclusionCheck.Evaluate(Flow(PermissionLevel.Preguntar), "x", "Word").IsDenied);
    }
}
