using Nexo.Core.Resources;

namespace Nexo.Core.Tests;

public sealed class GlobalHotkeyReconcilerTests
{
    private const int Flow = 99;
    private static readonly int[] Always = [1, 2, 3];

    [Fact]
    public void FirstRegistration_RegistersEverythingAndFlowWhenEnabled()
    {
        var plan = GlobalHotkeyReconciler.Plan(false, [], Always, Flow, flowEnabled: true);

        Assert.Empty(plan.Unregister);
        Assert.Equal([1, 2, 3, Flow], plan.Register.OrderBy(id => id));
    }

    [Fact]
    public void FlowDisabled_IsNeverRegistered()
    {
        var plan = GlobalHotkeyReconciler.Plan(false, [], Always, Flow, flowEnabled: false);

        Assert.DoesNotContain(Flow, plan.Register);
    }

    [Fact]
    public void FlowTurnedOffWhileRegistered_IsReleased()
    {
        var plan = GlobalHotkeyReconciler.Plan(false, [1, 2, 3, Flow], Always, Flow, flowEnabled: false);

        Assert.Equal([Flow], plan.Unregister);
        Assert.Empty(plan.Register);
    }

    [Fact]
    public void FullScreen_ReleasesEverythingRegistered()
    {
        var plan = GlobalHotkeyReconciler.Plan(true, [1, 3, Flow], Always, Flow, flowEnabled: true);

        Assert.Equal([1, 3, Flow], plan.Unregister.OrderBy(id => id));
        Assert.Empty(plan.Register);
    }

    [Fact]
    public void AlreadyInDesiredState_DoesNothing()
    {
        Assert.Same(
            GlobalHotkeyPlan.Empty,
            GlobalHotkeyReconciler.Plan(true, [], Always, Flow, flowEnabled: true));
        Assert.Same(
            GlobalHotkeyPlan.Empty,
            GlobalHotkeyReconciler.Plan(false, [1, 2, 3, Flow], Always, Flow, flowEnabled: true));
    }

    [Fact]
    public void HotkeyThatFailedToRegister_IsRetriedAlone()
    {
        // El 2 lo tenía otra aplicación al volver del juego: en la siguiente vuelta solo se reintenta ese.
        var plan = GlobalHotkeyReconciler.Plan(false, [1, 3, Flow], Always, Flow, flowEnabled: true);

        Assert.Empty(plan.Unregister);
        Assert.Equal([2], plan.Register);
    }

    [Fact]
    public void GovernorTurnedOffDuringGame_RecoversEverything()
    {
        // Con el gobernador apagado la política nunca pide soltar, así que se recupera lo soltado.
        var release = GlobalHotkeyReleasePolicy.ShouldRelease(true, "game", false, resourceGovernorEnabled: false);
        var plan = GlobalHotkeyReconciler.Plan(release, [], Always, Flow, flowEnabled: true);

        Assert.Equal([1, 2, 3, Flow], plan.Register.OrderBy(id => id));
    }
}
