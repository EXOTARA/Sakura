using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Nexo.Core.Commands;

namespace Nexo.Core.Voice;

/// <summary>
/// Corrige errores frecuentes de transcripción en órdenes cortas sin alterar
/// consultas abiertas. Está pensado como una capa pequeña entre Whisper y el
/// motor de comandos de Nexo.
/// </summary>
public static partial class SpanishVoiceTranscriptNormalizer
{
    private static readonly string[] PowerShellAliases =
    [
        "powershell",
        "power shell",
        "powershel",
        "powersheld",
        "powershield",
        "powerchel",
        "power chel"
    ];

    public static string Normalize(string? transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript))
        {
            return string.Empty;
        }

        var normalized = NormalizeCharacters(transcript);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        // Cuando la activación y la orden se dicen de corrido, el búfer previo
        // también contiene “Sakura” u “Oye Sakura”. Durante la transición se
        // aceptan las frases heredadas de Nexo. Se elimina solo al inicio.
        normalized = WakeWordPrefixRegex().Replace(normalized, string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        // Los comandos de terminal suelen ser muy cortos. Whisper reconoce bien
        // el nombre, pero puede deformar el verbo "abre" (avady, avedit, avite…).
        if (WordCount(normalized) <= 4 &&
            PowerShellAliases.Any(alias => normalized.Contains(alias, StringComparison.Ordinal)))
        {
            return "abre powershell";
        }

        normalized = ReplaceWholeWord(normalized, "potify", "spotify");
        normalized = ReplaceWholeWord(normalized, "espotify", "spotify");
        normalized = ReplaceWholeWord(normalized, "spotifai", "spotify");
        normalized = ReplaceWholeWord(normalized, "spotifay", "spotify");
        normalized = ReplaceWholeWord(normalized, "spoty", "spotify");
        normalized = ReplaceWholeWord(normalized, "spoti", "spotify");
        normalized = ReplaceWholeWord(normalized, "discor", "discord");

        // Variantes que Whisper suele generar al oír "sube" seguido de Spotify.
        normalized = SuBeSpotifyRegex().Replace(normalized, "sube spotify");

        // El mismo diccionario se usa para texto y voz. Así "bajas", "bájale",
        // "subes" o una palabra a una letra de distancia no requieren reglas
        // duplicadas en cada parte de la aplicación.
        normalized = SpanishCommandLexicon.NormalizeForParsing(normalized);

        if (DateQuestionRegex().IsMatch(normalized))
        {
            return "que dia es hoy";
        }

        if (TimeQuestionRegex().IsMatch(normalized))
        {
            return "que hora es";
        }

        if (SystemStatusRegex().IsMatch(normalized))
        {
            return "como esta mi pc";
        }

        if (PeekRegex().IsMatch(normalized))
        {
            return "muestra peek";
        }

        return normalized;
    }

    private static string NormalizeCharacters(string value)
    {
        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(character) ? character : ' ');
        }

        return WhitespaceRegex().Replace(builder.ToString(), " ").Trim();
    }

    private static string ReplaceWholeWord(string value, string oldWord, string newWord) =>
        Regex.Replace(
            value,
            $@"\b{Regex.Escape(oldWord)}\b",
            newWord,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static int WordCount(string value) =>
        value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

    // 2026-09-14 — «oy» y «hoy» por «oye», y la muletilla «esa» que Whisper mete a veces entre las dos
    // palabras («oy esa sakura»): sin ellas, la activación se quedaba dentro de la pregunta que se
    // mandaba a la IA. Visto en una conversación real de Adler.
    [GeneratedRegex(@"^(?:(?:oye|oy|hoy|oi|hey|ey|ei|ahi|ai|ay)(?:\s+(?:esa|ese))?\s+)?(?:sakura|sacura|zacura|kohana|koana|cohana|kojana|ohana|nexo|neso|nejo|exo)(?:\s+|$)", RegexOptions.IgnoreCase)]
    private static partial Regex WakeWordPrefixRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"^(?:que\s+dia\s+es\s+hoy|que\s+fecha\s+es\s+hoy|que\s+bien\s+soy|que\s+dia\s+soy)$", RegexOptions.IgnoreCase)]
    private static partial Regex DateQuestionRegex();

    [GeneratedRegex(@"^(?:que\s+hora\s+es|dime\s+la\s+hora|hora\s+actual)$", RegexOptions.IgnoreCase)]
    private static partial Regex TimeQuestionRegex();

    [GeneratedRegex(@"^(?:como|cómo)\s+(?:esta|anda)\s+mi\s+(?:pc|pece|pese|pic|pecé)$", RegexOptions.IgnoreCase)]
    private static partial Regex SystemStatusRegex();

    [GeneratedRegex(@"^(?:muestra|abre|ver|modo|ensena)\s+(?:el\s+)?(?:peek|pic|pick|peck|pique)$", RegexOptions.IgnoreCase)]
    private static partial Regex PeekRegex();

    [GeneratedRegex(@"^(?:su\s+vez|subes|sube\s+el|sube)\s+(?:potify|espotify|spotifai|spotifay|spotify)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SuBeSpotifyRegex();
}
