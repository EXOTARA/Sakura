namespace Nexo.Core.Voice;

/// <summary>
/// Diseño D88 — palabras que se le ofrecen a Vosk junto a la frase de activación, para que tenga
/// dónde escribir lo que de verdad oye.
///
/// La gramática que recibía Vosk solo contenía variantes de la frase y <c>[unk]</c>. Con una gramática
/// cerrada, el reconocedor no transcribe: elige la entrada más parecida de la lista. Ante «voy a sacar
/// la basura», «oye, saca la ropa» u «oye, se acabó el café», lo más parecido era «oye sakura», y
/// Sakura se despertaba. Medido con grabaciones reales de Adler (2026-09-13): **ocho despertares
/// falsos en 44 segundos** de frases normales, también en sensibilidad Precisa. Ninguna prueba lo veía
/// porque el comparador se probaba con texto ya transcrito, nunca con audio.
///
/// Con estas palabras en la gramática, «saca» deja de tener que salir escrito como «sakura».
/// Resultado sobre las mismas grabaciones y sobre una tanda nueva que no se usó para elegir la lista
/// (<c>scripts/voice/WakeBench</c>, y la tabla en L4 de KNOWN_LIMITATIONS.md):
///
/// | «Oye Sakura»                          | Antes | Con confusores |
/// |---------------------------------------|-------|----------------|
/// | Cerca, ~12 dichas                     | 10    | 10             |
/// | A dos metros con música, ~16 dichas   | 14    | 12             |
/// | Frases trampa, 44 s                   | 8     | 1              |
/// | Tanda nueva de validación, 70 s       | 2     | 0              |
///
/// El precio medido son dos aciertos de dieciséis a distancia.
///
/// **Todas sin tilde, a propósito.** La gramática viaja como JSON y el serializador escapa lo que no
/// es ASCII (<c>café</c> sale como <c>café</c>); Vosk no lo decodifica y descarta la palabra
/// sin avisar. Las que llevaban tilde nunca llegaron al reconocedor mientras se medía, así que la
/// lista que se publica es exactamente la que se midió.
/// </summary>
public static class WakeWordGrammarConfusers
{
    private static readonly string[] Sakura =
    [
        // Lo que se confundía con el nombre en las grabaciones.
        "saca", "sacar", "sacas", "saco", "sacarla", "sacarlo", "sabes", "sabe", "se", "acaba",
        "cura", "ropa", "basura", "cara", "casa", "sacude", "zapato", "seca", "segura", "seguro",
        "ahora", "ayuda", "dura", "pura", "cultura", "altura",
        // Los prefijos y las palabras más frecuentes, para que una frase corriente tenga por dónde ir.
        "oye", "hoy", "voy", "a", "la", "el", "que", "de", "no", "es", "y", "en", "un", "una", "por",
        "con", "para", "lo", "me", "ya", "mira", "eso", "esto"
    ];

    /// <summary>
    /// Las palabras para la frase dada. Las frases heredadas (Kohana, Nexo) no reciben ninguna: no se
    /// han medido con ellas, y su gramática se queda como estaba.
    /// </summary>
    public static IReadOnlyList<string> For(WakeWordPhrase phrase) =>
        phrase.IsSakura() ? Sakura : [];

    /// <summary>
    /// La gramática completa que se le da a Vosk: las formas de la frase, las palabras de esta clase y
    /// <c>[unk]</c> al final, sin repetidas.
    /// </summary>
    public static IReadOnlyList<string> ComposeGrammar(
        WakeWordPhrase phrase,
        IEnumerable<string> phraseForms)
    {
        ArgumentNullException.ThrowIfNull(phraseForms);

        return phraseForms
            .Concat(For(phrase))
            .Distinct(StringComparer.Ordinal)
            .Append("[unk]")
            .ToArray();
    }
}
