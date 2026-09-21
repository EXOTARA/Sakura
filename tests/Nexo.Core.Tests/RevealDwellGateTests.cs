using Nexo.Core.Shell;
using Xunit;

namespace Nexo.Core.Tests;

/// <summary>
/// 2026-09-18 — arrastrar una ventana hacia el borde abría el panel de arriba. Con un botón
/// pulsado, ningún borde abre nada; y soltar dentro de la franja y quedarse quieto tampoco.
/// </summary>
public sealed class RevealDwellGateTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Dwell = TopRevealPolicy.Dwell;

    private bool _down;

    private RevealDwellGate NewGate() => new(() => _down);

    [Fact]
    public void ConstructorRequiresTheButtonDelegate() =>
        Assert.Throws<ArgumentNullException>(() => new RevealDwellGate(null!));

    [Fact]
    public void TheGateConsultsTheDelegateItself()
    {
        var reads = 0;
        var gate = new RevealDwellGate(() => { reads++; return false; });
        gate.Observe(T0, true, true, Dwell);
        Assert.Equal(1, reads);
    }

    [Fact]
    public void StayingInsideForTheDwell_Fires()
    {
        var gate = NewGate();
        Assert.False(gate.Observe(T0, true, true, Dwell));
        Assert.True(gate.Observe(T0 + Dwell, true, true, Dwell));
    }

    [Fact]
    public void WithAButtonDown_NeverFires_HoweverLong()
    {
        var gate = NewGate();
        _down = true;
        for (var ms = 0; ms <= 5000; ms += 40)
        {
            Assert.False(gate.Observe(T0.AddMilliseconds(ms), true, true, Dwell));
        }
    }

    [Fact]
    public void PressInsideDragReleaseInsideAndStayStill_DoesNotFire()
    {
        // El gesto de maximizar: se suelta la ventana en y=0 y el cursor se queda ahí.
        var gate = NewGate();
        _down = true;
        gate.Observe(T0, true, true, Dwell);
        gate.Observe(T0.AddSeconds(2), true, true, Dwell);
        _down = false;

        for (var ms = 2040; ms <= 6000; ms += 40)
        {
            Assert.False(gate.Observe(T0.AddMilliseconds(ms), true, true, Dwell));
        }
    }

    [Fact]
    public void ADragThatEntersTheStripFromFarAway_AlsoDisarms()
    {
        var gate = NewGate();
        _down = true;
        gate.Observe(T0, false, false, Dwell);
        gate.Observe(T0.AddMilliseconds(40), true, true, Dwell);
        _down = false;
        Assert.False(gate.Observe(T0.AddMilliseconds(80), true, true, Dwell));
        Assert.False(gate.Observe(T0.AddSeconds(3), true, true, Dwell));
    }

    [Fact]
    public void LeavingTheNearZoneAndComingBack_RearmsTheEdge()
    {
        var gate = NewGate();
        _down = true;
        gate.Observe(T0, true, true, Dwell);
        _down = false;
        gate.Observe(T0.AddSeconds(1), true, true, Dwell);

        // Se aparta de verdad (fuera de la zona cercana) y vuelve sin botón.
        gate.Observe(T0.AddSeconds(2), false, false, Dwell);
        Assert.False(gate.Observe(T0.AddSeconds(3), true, true, Dwell));
        Assert.True(gate.Observe(T0.AddSeconds(3) + Dwell, true, true, Dwell));
    }

    [Fact]
    public void WigglingBetweenStripAndNearZone_DoesNotRearm()
    {
        var gate = NewGate();
        _down = true;
        gate.Observe(T0, true, true, Dwell);
        _down = false;

        // Fuera de la franja pero aún cerca: no cuenta como irse.
        gate.Observe(T0.AddSeconds(1), false, true, Dwell);
        Assert.False(gate.Observe(T0.AddSeconds(2), true, true, Dwell));
        Assert.False(gate.Observe(T0.AddSeconds(3), true, true, Dwell));
    }

    [Fact]
    public void ReleasingOutsideTheNearZone_ThenEnteringWithoutButton_Fires()
    {
        var gate = NewGate();
        _down = true;
        gate.Observe(T0, false, false, Dwell);
        _down = false;
        gate.Observe(T0.AddMilliseconds(40), true, true, Dwell);
        Assert.True(gate.Observe(T0.AddMilliseconds(40) + Dwell, true, true, Dwell));
    }

    [Fact]
    public void LeavingTheZone_RestartsTheDwell()
    {
        var gate = NewGate();
        gate.Observe(T0, true, true, Dwell);
        gate.Observe(T0.AddMilliseconds(100), false, false, Dwell);
        Assert.False(gate.Observe(T0.AddMilliseconds(200), true, true, Dwell));
    }

    [Fact]
    public void AfterFiring_ItDoesNotFireOnTheNextPoll()
    {
        var gate = NewGate();
        gate.Observe(T0, true, true, Dwell);
        Assert.True(gate.Observe(T0 + Dwell, true, true, Dwell));
        Assert.False(gate.Observe(T0 + Dwell + TimeSpan.FromMilliseconds(40), true, true, Dwell));
    }

    [Fact]
    public void Reset_DropsTheAccumulatedDwellAndTheDisarm()
    {
        var gate = NewGate();
        gate.Observe(T0, true, true, Dwell);
        gate.Reset();
        Assert.False(gate.Observe(T0 + Dwell, true, true, Dwell));

        _down = true;
        gate.Observe(T0.AddSeconds(1), true, true, Dwell);
        _down = false;
        gate.Reset();
        gate.Observe(T0.AddSeconds(2), true, true, Dwell);
        Assert.True(gate.Observe(T0.AddSeconds(2) + Dwell, true, true, Dwell));
    }
}
