namespace Nexo.Core.Voice;

/// <summary>Un anillo vivo del halo: dónde está en su vida, de 0 (recién nacido) a 1 (desvanecido).</summary>
public readonly record struct VoiceHaloRing(double Progress, double Strength);

/// <summary>
/// Diseño D72 — el halo que aparece cuando Sakura oye su nombre.
///
/// Todo lo que decide *cómo respira* está aquí y no en la ventana: el nivel del micrófono llega
/// crudo y a saltos, y lo que se ve en pantalla tiene que ser una curva. Separarlo permite además
/// comprobarlo sin abrir una ventana, que es la regla de este proyecto para decidir qué vive en
/// Core.
///
/// El filtro es de ataque rápido y caída lenta, como los medidores de audio de siempre: cuando
/// alguien empieza a hablar el halo tiene que responder en el mismo instante —si tarda, se siente
/// desconectado de la voz—, pero al callar debe apagarse despacio, porque el habla real está llena
/// de silencios de décimas de segundo entre palabras y un halo que se derrumba en cada uno parpadea
/// en vez de respirar.
/// </summary>
public sealed class VoiceHaloPolicy
{
    /// <summary>Lo que tarda en subir. Corto: la voz manda.</summary>
    public static readonly TimeSpan Attack = TimeSpan.FromMilliseconds(80);

    /// <summary>Lo que tarda en bajar. Largo: cubre los huecos entre palabras.</summary>
    public static readonly TimeSpan Decay = TimeSpan.FromMilliseconds(350);

    /// <summary>Cada cuánto nace un anillo mientras hay voz.</summary>
    public static readonly TimeSpan RingInterval = TimeSpan.FromMilliseconds(180);

    /// <summary>Lo que vive un anillo desde que nace hasta que se desvanece del todo.</summary>
    public static readonly TimeSpan RingLifetime = TimeSpan.FromMilliseconds(1400);

    /// <summary>
    /// Por debajo de esto no se emiten anillos. No es silencio absoluto: es el ruido de fondo de
    /// una habitación normal, y sin este umbral el halo late solo, sin que nadie hable.
    /// </summary>
    public const double SilenceThreshold = 0.06;

    /// <summary>
    /// Tope de anillos vivos a la vez. Es presupuesto de fotograma, no estética: cada anillo es un
    /// trazo más por cada uno de los sesenta fotogramas del segundo.
    /// </summary>
    public const int MaxRings = 5;

    private readonly List<double> _ringAges = [];
    private double _level;
    private double _sinceLastRing;

    /// <summary>Nivel suavizado, de 0 a 1. Es lo que la vista usa para escalar la marca.</summary>
    public double Level => _level;

    /// <summary>Anillos vivos, del más viejo al más nuevo.</summary>
    public IReadOnlyList<VoiceHaloRing> Rings =>
        _ringAges
            .Select(age => new VoiceHaloRing(
                Math.Clamp(age / RingLifetime.TotalSeconds, 0, 1),
                _level))
            .ToArray();

    /// <summary>
    /// Avanza el halo un fotograma. <paramref name="rawLevel"/> es el nivel crudo del micrófono
    /// (0 a 1) y <paramref name="elapsed"/> lo que pasó desde el fotograma anterior.
    /// </summary>
    public void Advance(double rawLevel, TimeSpan elapsed)
    {
        var seconds = Math.Max(0, elapsed.TotalSeconds);
        var target = Math.Clamp(rawLevel, 0, 1);

        // Constante de tiempo distinta según se suba o se baje. La forma exponencial es la que hace
        // que el movimiento no tenga esquinas: nunca llega de golpe, se acerca.
        var tau = target > _level ? Attack.TotalSeconds : Decay.TotalSeconds;
        var factor = tau <= 0 ? 1 : 1 - Math.Exp(-seconds / tau);
        _level += (target - _level) * factor;

        for (var i = _ringAges.Count - 1; i >= 0; i--)
        {
            _ringAges[i] += seconds;
            if (_ringAges[i] >= RingLifetime.TotalSeconds)
            {
                _ringAges.RemoveAt(i);
            }
        }

        _sinceLastRing += seconds;
        if (_level < SilenceThreshold || _sinceLastRing < RingInterval.TotalSeconds)
        {
            return;
        }

        _sinceLastRing = 0;
        _ringAges.Add(0);

        // Si se llegó al tope, muere el más viejo: es el que menos se ve.
        if (_ringAges.Count > MaxRings)
        {
            _ringAges.RemoveAt(0);
        }
    }

    /// <summary>Deja el halo como recién abierto. Se llama al ocultarlo, no al mostrarlo.</summary>
    public void Reset()
    {
        _ringAges.Clear();
        _level = 0;
        _sinceLastRing = 0;
    }
}
