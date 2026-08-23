using Nexo.Core.Media;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D83 — el gato golpea con la música, no con un temporizador.
///
/// Las tres cosas que se fijan aquí son las tres formas de que esto quede mal: que no se mueva
/// cuando debería, que tiemble, y que se mueva con la música parada.
/// </summary>
public sealed class BongoBeatPolicyTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 8, 23, 3, 0, 0, TimeSpan.Zero);

    private static double[] Level(double low, double high = 0.2)
    {
        var levels = new double[32];
        for (var i = 0; i < levels.Length; i++)
        {
            levels[i] = i < BongoBeatPolicy.LowBandCount ? low : high;
        }
        return levels;
    }

    /// <summary>Deja la media asentada en un nivel de fondo, como una canción ya sonando.</summary>
    private static BongoBeatPolicy Settled(double background, out DateTimeOffset now)
    {
        var policy = new BongoBeatPolicy();
        now = Start;

        for (var i = 0; i < 60; i++)
        {
            now = now.AddMilliseconds(33);
            policy.Advance(Level(background), isPlaying: true, now);
        }

        return policy;
    }

    [Fact]
    public void WithTheMusicStopped_TheCatRests()
    {
        var policy = new BongoBeatPolicy();

        // Aunque llegue un espectro con energía: lo que manda es que no hay reproducción.
        var pose = policy.Advance(Level(0.9), isPlaying: false, Start);

        Assert.Equal(BongoPose.Raised, pose);
    }

    [Fact]
    public void ASuddenLowEnd_IsAStrike()
    {
        var policy = Settled(0.20, out var now);

        var pose = policy.Advance(Level(0.85), isPlaying: true, now.AddMilliseconds(33));

        Assert.Equal(BongoPose.Struck, pose);
    }

    [Fact]
    public void SteadyLowEnd_IsNotABeat()
    {
        // Un grave sostenido no es ritmo: es un zumbido, y el gato no debe temblar con él.
        var policy = Settled(0.55, out var now);

        for (var i = 0; i < 20; i++)
        {
            now = now.AddMilliseconds(33);
            Assert.Equal(BongoPose.Raised, policy.Advance(Level(0.55), isPlaying: true, now));
        }
    }

    [Fact]
    public void AfterAStrike_ItHoldsBriefly_SoTheGestureCanBeSeen()
    {
        var policy = Settled(0.20, out var now);

        var strike = now.AddMilliseconds(33);
        Assert.Equal(BongoPose.Struck, policy.Advance(Level(0.85), isPlaying: true, strike));

        // Aunque el grave se caiga a cero en el fotograma siguiente, el golpe se sostiene: uno que
        // dura dieciséis milisegundos no se ve, solo parpadea.
        Assert.Equal(
            BongoPose.Struck,
            policy.Advance(Level(0.0), isPlaying: true, strike.AddMilliseconds(30)));

        Assert.Equal(
            BongoPose.Raised,
            policy.Advance(Level(0.0), isPlaying: true, strike.AddMilliseconds(200)));
    }

    [Fact]
    public void Silence_NeverStrikes()
    {
        var policy = new BongoBeatPolicy();
        var now = Start;

        for (var i = 0; i < 40; i++)
        {
            now = now.AddMilliseconds(33);
            Assert.Equal(BongoPose.Raised, policy.Advance(Level(0.0), isPlaying: true, now));
        }
    }

    [Fact]
    public void OnlyTheLowBandsAreListenedTo()
    {
        // Platos y voz al máximo, graves quietos: el gato no se inmuta.
        var policy = Settled(0.20, out var now);

        var pose = policy.Advance(Level(0.20, high: 1.0), isPlaying: true, now.AddMilliseconds(33));

        Assert.Equal(BongoPose.Raised, pose);
    }
}
