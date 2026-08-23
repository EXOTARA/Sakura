namespace Nexo.Core.Media;

/// <summary>Lo que el gato hace en este fotograma.</summary>
public enum BongoPose
{
    /// <summary>Patas arriba, entre golpe y golpe.</summary>
    Raised,

    /// <summary>Patas abajo, sobre el golpe.</summary>
    Struck
}

/// <summary>
/// Diseño D83 — cuándo el gato da un golpe.
///
/// Un gato que alterna con un temporizador fijo no acompaña a la música, la ignora educadamente; y
/// uno que copia el nivel instantáneo tiembla, porque el nivel sube y baja decenas de veces por
/// segundo. Lo que se busca está en medio: **golpea cuando los graves suben por encima de lo que
/// venían haciendo**, y aguanta el golpe un rato antes de poder dar otro.
///
/// De ahí las tres piezas:
///
/// **Se miran solo las bandas graves.** El bombo y la caja son lo que marca el ritmo; las bandas
/// altas son platos y voz, que suben y bajan continuamente y harían pulsar al gato con todo.
///
/// **Se compara contra una media que se arrastra**, no contra un número fijo. Un umbral fijo hace
/// que una canción suave no mueva al gato nunca y una fuerte lo tenga siempre abajo. Con la media,
/// lo que dispara es el CONTRASTE, que es lo que un oído llama ritmo.
///
/// **Hay un tiempo muerto tras cada golpe.** Sin él, un grave sostenido dispara en cada fotograma
/// y el gato se convierte en un zumbido.
/// </summary>
public sealed class BongoBeatPolicy
{
    /// <summary>Cuántas bandas del grave se escuchan, de las 32 que llegan.</summary>
    public const int LowBandCount = 6;

    /// <summary>Cuánto tiene que superar el grave a su media para contar como golpe.</summary>
    public const double BeatFactor = 1.32;

    /// <summary>Ni el silencio ni el ruido de fondo mueven al gato.</summary>
    public const double MinimumEnergy = 0.08;

    /// <summary>Cuánto dura un golpe antes de poder dar otro.</summary>
    public static readonly TimeSpan StrikeHold = TimeSpan.FromMilliseconds(110);

    /// <summary>Cuánto pesa lo nuevo en la media que se arrastra.</summary>
    private const double Smoothing = 0.12;

    private double _average;
    private DateTimeOffset _struckAt = DateTimeOffset.MinValue;

    /// <summary>La pose que toca ahora mismo.</summary>
    public BongoPose Pose { get; private set; } = BongoPose.Raised;

    /// <summary>
    /// Entra un fotograma de espectro y sale la pose. <paramref name="isPlaying"/> manda sobre todo
    /// lo demás: con la música parada el gato descansa, por mucho que el micrófono o el sistema
    /// sigan moviendo números.
    /// </summary>
    public BongoPose Advance(
        IReadOnlyList<double> spectrumLevels,
        bool isPlaying,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(spectrumLevels);

        if (!isPlaying)
        {
            _average = 0;
            _struckAt = DateTimeOffset.MinValue;
            return Pose = BongoPose.Raised;
        }

        var energy = LowEnergy(spectrumLevels);

        // El golpe se mantiene un momento aunque la energía ya haya bajado: un golpe que dura un
        // fotograma no se ve, y lo que quedaría es un parpadeo.
        if (now - _struckAt < StrikeHold)
        {
            _average += (energy - _average) * Smoothing;
            return Pose = BongoPose.Struck;
        }

        var isBeat = energy >= MinimumEnergy && energy > _average * BeatFactor;
        _average += (energy - _average) * Smoothing;

        if (isBeat)
        {
            _struckAt = now;
            return Pose = BongoPose.Struck;
        }

        return Pose = BongoPose.Raised;
    }

    private static double LowEnergy(IReadOnlyList<double> levels)
    {
        if (levels.Count == 0)
        {
            return 0;
        }

        var count = Math.Min(LowBandCount, levels.Count);
        var total = 0d;

        for (var i = 0; i < count; i++)
        {
            total += levels[i];
        }

        return total / count;
    }
}
