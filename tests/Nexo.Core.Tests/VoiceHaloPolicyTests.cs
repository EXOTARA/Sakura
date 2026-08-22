using Nexo.Core.Voice;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D72 — cómo respira el halo.
///
/// Lo que se fija aquí no es que suba y baje, que es trivial, sino las dos asimetrías que hacen que
/// parezca vivo en vez de nervioso: sube deprisa y baja despacio, y no emite nada mientras nadie
/// habla. Sin la primera, el halo va por detrás de la voz; sin la segunda, late solo con el ruido
/// de la habitación.
/// </summary>
public sealed class VoiceHaloPolicyTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(16);

    [Fact]
    public void ItRisesFasterThanItFalls()
    {
        var subiendo = new VoiceHaloPolicy();
        var bajando = new VoiceHaloPolicy();

        // Cien milisegundos de voz en uno; el otro parte de arriba y se queda en silencio.
        for (var i = 0; i < 6; i++)
        {
            subiendo.Advance(1, Frame);
        }

        for (var i = 0; i < 40; i++)
        {
            bajando.Advance(1, Frame);
        }

        var alcanzado = subiendo.Level;
        var partida = bajando.Level;

        for (var i = 0; i < 6; i++)
        {
            bajando.Advance(0, Frame);
        }

        var caido = partida - bajando.Level;

        // En el mismo tiempo, lo que sube es más de lo que baja. Ese es el ataque rápido y la caída
        // lenta escritos como una comparación, no como dos constantes que hay que creerse.
        Assert.True(
            alcanzado > caido,
            $"En 100 ms subió {alcanzado:F3} y bajó {caido:F3}: la caída no puede ser más rápida que el ataque.");
    }

    [Fact]
    public void SilenceEmitsNoRings()
    {
        var halo = new VoiceHaloPolicy();

        // Dos segundos de ruido de fondo, por debajo del umbral.
        for (var i = 0; i < 125; i++)
        {
            halo.Advance(VoiceHaloPolicy.SilenceThreshold - 0.01, Frame);
        }

        Assert.Empty(halo.Rings);
    }

    [Fact]
    public void SpeechEmitsRingsAtTheDeclaredInterval()
    {
        var halo = new VoiceHaloPolicy();

        // Medio segundo hablando: al ritmo declarado caben dos anillos y empieza el tercero.
        for (var i = 0; i < 31; i++)
        {
            halo.Advance(1, Frame);
        }

        Assert.InRange(halo.Rings.Count, 2, 3);
    }

    [Fact]
    public void RingsNeverExceedTheFrameBudget()
    {
        var halo = new VoiceHaloPolicy();

        // Diez segundos hablando sin parar: el tope es presupuesto de fotograma, no estética.
        for (var i = 0; i < 625; i++)
        {
            halo.Advance(1, Frame);
        }

        Assert.True(halo.Rings.Count <= VoiceHaloPolicy.MaxRings);
    }

    [Fact]
    public void ARingDiesWhenItsLifetimeIsOver()
    {
        var halo = new VoiceHaloPolicy();

        for (var i = 0; i < 12; i++)
        {
            halo.Advance(1, Frame);
        }

        Assert.NotEmpty(halo.Rings);

        // Silencio durante más de lo que vive un anillo MÁS lo que tarda el nivel en caer bajo el
        // umbral. Mientras el nivel baja se siguen emitiendo anillos, y eso es deliberado: el halo
        // acompaña al final de la frase en vez de cortarse en seco con la última sílaba.
        var frames = (int)(
            (VoiceHaloPolicy.RingLifetime.TotalMilliseconds +
             (VoiceHaloPolicy.Decay.TotalMilliseconds * 4)) / Frame.TotalMilliseconds) + 2;
        for (var i = 0; i < frames; i++)
        {
            halo.Advance(0, Frame);
        }

        Assert.Empty(halo.Rings);
    }

    [Fact]
    public void SilenceMeasuresZeroAndFullScaleMeasuresOne()
    {
        var silencio = new byte[1024];
        Assert.Equal(0, VoiceLevelMeter.Measure(silencio, silencio.Length));

        // Onda cuadrada a fondo de escala: la energía es máxima, así que el nivel es 1.
        var maximo = new byte[1024];
        for (var i = 0; i < maximo.Length; i += 2)
        {
            maximo[i] = 0xFF;
            maximo[i + 1] = 0x7F;
        }

        Assert.Equal(1, VoiceLevelMeter.Measure(maximo, maximo.Length), 3);
    }

    [Fact]
    public void AQuietRoomIsBelowTheThresholdThatEmitsRings()
    {
        // Ruido de una habitación en silencio medido de verdad: unos -50 dBFS, tres milésimas de
        // fondo de escala. Por encima de eso ya no es una habitación en silencio, es un ventilador.
        var ruido = new byte[2048];
        var aleatorio = new Random(7);
        for (var i = 0; i < ruido.Length; i += 2)
        {
            var muestra = (short)aleatorio.Next(-100, 100);
            ruido[i] = (byte)(muestra & 0xFF);
            ruido[i + 1] = (byte)((muestra >> 8) & 0xFF);
        }

        Assert.True(
            VoiceLevelMeter.Measure(ruido, ruido.Length) < VoiceHaloPolicy.SilenceThreshold,
            "El ruido de una habitación en silencio no puede pasar el umbral de emisión.");
    }
}
