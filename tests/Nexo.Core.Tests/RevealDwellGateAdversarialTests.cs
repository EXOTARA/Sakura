using Nexo.Core.Shell;
using Xunit;

namespace Nexo.Core.Tests;

public sealed class RevealDwellGateAdversarialTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Dwell = TopRevealPolicy.Dwell;

    [Fact]
    public void PressingAfterDwellingInside_ThenReleasing_DoesNotFireWithTheOldDwell()
    {
        // Entra sin botón (100 ms), pulsa y arrastra 2 s dentro de la franja, suelta: la cuenta
        // debe empezar de cero; no puede disparar en el sondeo de la suelta.
        var down = false;
        var gate = new RevealDwellGate(() => down);
        Assert.False(gate.Observe(T0, true, true, Dwell));
        Assert.False(gate.Observe(T0.AddMilliseconds(100), true, true, Dwell));
        down = true;
        Assert.False(gate.Observe(T0.AddMilliseconds(140), true, true, Dwell));
        Assert.False(gate.Observe(T0.AddSeconds(2), true, true, Dwell));
        down = false;
        Assert.False(gate.Observe(T0.AddSeconds(2).AddMilliseconds(40), true, true, Dwell));
    }
}
