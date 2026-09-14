namespace Nexo.Core.Shell;

/// <summary>Cuándo empieza cada pieza de la entrada del asistente, medido desde que el shell se abre.</summary>
public sealed record EntrancePlan(
    TimeSpan Header,
    TimeSpan CardBlock,
    TimeSpan ComposerBlock,
    TimeSpan Mark,
    IReadOnlyList<TimeSpan> Words,
    TimeSpan Description,
    IReadOnlyList<TimeSpan> Chips,
    IReadOnlyList<TimeSpan> Messages,
    TimeSpan ComposerContent,
    TimeSpan Settled);

/// <summary>
/// La primera apertura del asistente se organiza sola (Adler, 2026-09-14, sobre el vídeo 2 de
/// referencia): primero aparecen los bloques vacíos creciendo desde abajo, luego su contenido va
/// entrando, el saludo palabra a palabra y las sugerencias una detrás de otra.
///
/// Solo en la primera apertura de cada sesión. Una coreografía de un segundo es un recibimiento la
/// primera vez; repetida cada vez que se abre el panel, sería hacer esperar a alguien que ya sabe lo
/// que hay y viene a otra cosa. Las aperturas siguientes conservan la entrada corta de siempre.
///
/// Los tiempos se reparten aquí y no en la vista para poder comprobar lo que importa sin mirar la
/// pantalla: que cada contenido empiece después de su bloque, que el orden no se cruce y que el total
/// no se alargue aunque el saludo tenga muchas palabras o haya muchos mensajes.
/// </summary>
public static class EntranceChoreography
{
    /// <summary>Separación entre bloques, de arriba abajo.</summary>
    public static readonly TimeSpan BlockStagger = TimeSpan.FromMilliseconds(70);

    /// <summary>Lo que tarda un bloque en crecer lo bastante para que su contenido pueda entrar.</summary>
    public static readonly TimeSpan ContentDelay = TimeSpan.FromMilliseconds(190);

    public static readonly TimeSpan WordStagger = TimeSpan.FromMilliseconds(65);
    public static readonly TimeSpan ChipStagger = TimeSpan.FromMilliseconds(55);
    public static readonly TimeSpan MessageStagger = TimeSpan.FromMilliseconds(50);

    /// <summary>Techo de cada escalonado: con más piezas se aprietan en vez de alargarse.</summary>
    public static readonly TimeSpan MaximumSpread = TimeSpan.FromMilliseconds(360);

    /// <summary>Cuánto dura la animación de la última pieza, para saber cuándo termina todo.</summary>
    public static readonly TimeSpan PieceDuration = TimeSpan.FromMilliseconds(420);

    public static bool PlaysFull(bool firstOpenInSession, bool animationsEnabled) =>
        firstOpenInSession && animationsEnabled;

    public static EntrancePlan Plan(int wordCount, int chipCount, int messageCount)
    {
        var header = TimeSpan.Zero;
        var card = header + BlockStagger;
        var composer = card + BlockStagger;

        var mark = card + ContentDelay;
        var words = Spread(mark + TimeSpan.FromMilliseconds(80), Math.Max(0, wordCount), WordStagger);
        var afterWords = (words.Count > 0 ? words[^1] : mark) + TimeSpan.FromMilliseconds(90);
        var description = afterWords;
        var chips = Spread(description + TimeSpan.FromMilliseconds(90), Math.Max(0, chipCount), ChipStagger);

        // Con conversación en vez de bienvenida, los mensajes entran donde irían el saludo y las sugerencias.
        var messages = Spread(card + ContentDelay, Math.Max(0, messageCount), MessageStagger);

        var composerContent = composer + ContentDelay;

        var last = new[]
        {
            composerContent,
            description,
            chips.Count > 0 ? chips[^1] : TimeSpan.Zero,
            messages.Count > 0 ? messages[^1] : TimeSpan.Zero
        }.Max();

        return new EntrancePlan(
            header, card, composer, mark, words, description, chips, messages, composerContent, last + PieceDuration);
    }

    /// <summary>
    /// Reparte <paramref name="count"/> inicios a partir de <paramref name="start"/>. Si con el paso
    /// normal se pasaría del techo, el paso se encoge para que el último siga llegando a tiempo.
    /// </summary>
    public static IReadOnlyList<TimeSpan> Spread(TimeSpan start, int count, TimeSpan step)
    {
        if (count <= 0)
        {
            return [];
        }

        // Se redondea hacia abajo al tick: dividir un TimeSpan redondea al más cercano, y con eso el
        // último podía pasarse del techo por unos microsegundos.
        var effective = count > 1 && step * (count - 1) > MaximumSpread
            ? TimeSpan.FromTicks(MaximumSpread.Ticks / (count - 1))
            : step;

        var result = new TimeSpan[count];
        for (var i = 0; i < count; i++)
        {
            result[i] = start + (effective * i);
        }

        return result;
    }
}
