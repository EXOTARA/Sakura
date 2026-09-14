using System.Globalization;
using System.Text;

namespace Nexo.Core.Voice;

public static class WakeWordTextMatcher
{
    private static readonly HashSet<string> ExactBrandNames =
        new(StringComparer.Ordinal)
        {
            "kohana"
        };

    // Formas probables al pronunciar Kohana en español: co-ja-na / ko-ja-na.
    //
    // Diseño D66 — las seis últimas no son suposiciones: son lo que Vosk escribió al oír a Adler
    // decir "Oye Kohana" ocho veces en una grabación de su propio micrófono. Antes de añadirlas,
    // ninguno de esos ocho intentos despertaba a Kohana en sensibilidad Equilibrada.
    private static readonly HashSet<string> PhoneticBrandNames =
        new(StringComparer.Ordinal)
        {
            "jana",
            "jena",
            "cojan",
            // Diseño D68 — de la segunda grabación, hecha a dos metros y con un video sonando. A esa
            // distancia Vosk deja de oír algo parecido a "kohana" y produce estas: son palabras
            // reales, pero ninguna es algo que alguien le diga a su computadora por casualidad.
            "cogeme",
            "cogerme",
            "cojeme",
            "jarana",
            "kojan",
            "cojanna",
            "cohan",
            "cojana",
            "kojana",
            "cohana",
            "koana",
            "coana",
            "cogana",
            "coyana",
            "cujana",
            "kujana",
            "juana",
            "joana",
            "johana",
            "johanna",
            "conana",
            "ohana",
            "kohanna"
        };

    /// <summary>
    /// Diseño D69 — Sakura sí existe en el léxico español del reconocedor, y esa es toda la razón
    /// del cambio de nombre. Medido con la voz de Windows: "oye sakura" sale escrito tal cual las
    /// tres veces. La lista de variantes es corta a propósito, porque no hace falta que sea larga.
    /// </summary>
    private static readonly HashSet<string> SakuraExactNames =
        new(StringComparer.Ordinal)
        {
            "sakura"
        };

    private static readonly HashSet<string> SakuraPhoneticNames =
        new(StringComparer.Ordinal)
        {
            "sacura",
            "zacura",
            "sakuro",
            "sakuras",
            "saqura",
            "sagura"
        };

    private static readonly HashSet<string> OyePrefixes =
        new(StringComparer.Ordinal)
        {
            // "hoy" sale de la misma grabación: Vosk escribió "hoy" por "oye" dos veces de ocho.
            // Como prefijo no decide nada por su cuenta —lo que discrimina es la palabra siguiente—
            // así que admitirlo no abre la puerta a despertares por hablar del día de hoy.
            "oye", "oi", "oy", "hoy", "oye,"
        };

    private static readonly HashSet<string> HeyPrefixes =
        new(StringComparer.Ordinal)
        {
            "hey", "ey", "ei", "e", "eh", "he", "jei", "ay", "ai", "ahi", "hay"
        };

    private static readonly HashSet<string> NexoShortPhrases =
        new(StringComparer.Ordinal)
        {
            "nexo"
        };

    private static readonly HashSet<string> OyeNexoPhrases =
        new(StringComparer.Ordinal)
        {
            "oye nexo", "oi nexo"
        };

    private static readonly HashSet<string> HeyNexoPhrases =
        new(StringComparer.Ordinal)
        {
            "hey nexo", "ey nexo", "ei nexo", "ai nexo", "ahi nexo", "hey neso", "ey neso"
        };

    public static bool IsMatch(
        string? text,
        WakeWordPhrase phrase,
        WakeWordSensitivity sensitivity = WakeWordSensitivity.Balanced,
        IEnumerable<string>? customAliases = null) =>
        Evaluate(text, phrase, sensitivity, customAliases).IsMatch;

    public static WakeWordMatchResult Evaluate(
        string? text,
        WakeWordPhrase phrase,
        WakeWordSensitivity sensitivity = WakeWordSensitivity.Balanced,
        IEnumerable<string>? customAliases = null)
    {
        var recognizedText = text?.Trim() ?? string.Empty;
        var normalized = Canonicalize(Normalize(recognizedText));
        if (normalized.Length == 0)
        {
            return WakeWordMatchResult.Rejected(
                recognizedText,
                normalized,
                "Vosk todavía no produjo una frase reconocible.");
        }

        if (phrase.IsLegacy())
        {
            return EvaluateLegacy(recognizedText, normalized, phrase);
        }

        var aliases = WakeWordAliasPolicy.NormalizeMany(customAliases)
            .Select(Canonicalize)
            .ToHashSet(StringComparer.Ordinal);
        if (aliases.Contains(normalized))
        {
            return WakeWordMatchResult.Accepted(
                recognizedText,
                normalized,
                WakeWordMatchKind.CustomAlias,
                "Coincidió con un alias personal guardado.");
        }

        if (sensitivity == WakeWordSensitivity.Strict)
        {
            return EvaluateStrict(recognizedText, normalized, phrase);
        }

        if (TryMatchKnownPhrase(normalized, phrase, out var knownKind, out var knownDetail))
        {
            return WakeWordMatchResult.Accepted(
                recognizedText,
                normalized,
                knownKind,
                knownDetail);
        }

        if (sensitivity == WakeWordSensitivity.High &&
            TryMatchApproximatePhrase(normalized, phrase, out var approximateDetail))
        {
            return WakeWordMatchResult.Accepted(
                recognizedText,
                normalized,
                WakeWordMatchKind.Approximate,
                approximateDetail);
        }

        return WakeWordMatchResult.Rejected(
            recognizedText,
            normalized,
            BuildRejectionDetail(normalized, phrase));
    }

    public static IReadOnlyList<string> GetGrammarPhrases(
        WakeWordPhrase phrase,
        WakeWordSensitivity sensitivity,
        IEnumerable<string>? customAliases = null)
    {
        var phrases = new HashSet<string>(StringComparer.Ordinal);

        if (phrase.IsLegacy())
        {
            foreach (var legacy in (phrase switch
                     {
                         WakeWordPhrase.OyeNexo => OyeNexoPhrases,
                         WakeWordPhrase.HeyNexo => HeyNexoPhrases,
                         _ => NexoShortPhrases
                     }))
            {
                phrases.Add(legacy);
            }

            return phrases.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        var exact = phrase.IsSakura() ? SakuraExactNames : ExactBrandNames;
        var phonetic = phrase.IsSakura() ? SakuraPhoneticNames : PhoneticBrandNames;

        var targets = sensitivity == WakeWordSensitivity.Strict
            ? exact
            : exact.Concat(phonetic).ToHashSet(StringComparer.Ordinal);

        switch (phrase)
        {
            case WakeWordPhrase.OyeKohana or WakeWordPhrase.OyeSakura:
                AddPrefixed(phrases, OyePrefixes, targets);
                if (sensitivity == WakeWordSensitivity.High)
                {
                    AddTargets(phrases, targets);
                }
                break;
            case WakeWordPhrase.HeyKohana or WakeWordPhrase.HeySakura:
                AddPrefixed(phrases, HeyPrefixes, targets);
                if (sensitivity == WakeWordSensitivity.High)
                {
                    AddTargets(phrases, targets);
                }
                break;
            default:
                AddTargets(phrases, targets);
                if (sensitivity != WakeWordSensitivity.Strict)
                {
                    AddPrefixed(phrases, OyePrefixes, targets);
                    AddPrefixed(phrases, HeyPrefixes, targets);
                }
                break;
        }

        foreach (var alias in WakeWordAliasPolicy.NormalizeMany(customAliases))
        {
            phrases.Add(alias);
        }

        return phrases.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var decomposed = text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(character) ? character : ' ');
        }

        return string.Join(
            ' ',
            builder.ToString()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static WakeWordMatchResult EvaluateLegacy(
        string recognizedText,
        string normalized,
        WakeWordPhrase phrase)
    {
        var isMatch = phrase switch
        {
            WakeWordPhrase.OyeNexo => OyeNexoPhrases.Contains(normalized),
            WakeWordPhrase.HeyNexo => HeyNexoPhrases.Contains(normalized),
            _ => NexoShortPhrases.Contains(normalized)
        };

        return isMatch
            ? WakeWordMatchResult.Accepted(
                recognizedText,
                normalized,
                WakeWordMatchKind.Legacy,
                "Coincidió con la frase heredada seleccionada.")
            : WakeWordMatchResult.Rejected(
                recognizedText,
                normalized,
                "La frase heredada seleccionada no coincide.");
    }

    private static WakeWordMatchResult EvaluateStrict(
        string recognizedText,
        string normalized,
        WakeWordPhrase phrase)
    {
        var expected = phrase switch
        {
            WakeWordPhrase.OyeSakura => normalized == "oye sakura",
            WakeWordPhrase.HeySakura => normalized is "hey sakura" or "ey sakura",
            WakeWordPhrase.Sakura => normalized == "sakura",
            WakeWordPhrase.OyeKohana => normalized == "oye kohana",
            WakeWordPhrase.HeyKohana => normalized is "hey kohana" or "ey kohana",
            _ => normalized == "kohana"
        };

        return expected
            ? WakeWordMatchResult.Accepted(
                recognizedText,
                normalized,
                WakeWordMatchKind.Exact,
                "Coincidencia exacta con la frase configurada.")
            : WakeWordMatchResult.Rejected(
                recognizedText,
                normalized,
                phrase.IsSakura()
                    ? "La sensibilidad Precisa requiere la forma exacta escrita como Sakura."
                    : "La sensibilidad Precisa requiere la forma exacta escrita como Kohana.");
    }

    private static bool TryMatchKnownPhrase(
        string normalized,
        WakeWordPhrase phrase,
        out WakeWordMatchKind kind,
        out string detail)
    {
        var words = normalized.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        kind = WakeWordMatchKind.None;
        detail = string.Empty;

        string? target = null;
        switch (phrase)
        {
            case WakeWordPhrase.OyeKohana or WakeWordPhrase.OyeSakura
                when words.Length == 2 && OyePrefixes.Contains(words[0]):
                target = words[1];
                break;
            case WakeWordPhrase.HeyKohana or WakeWordPhrase.HeySakura
                when words.Length == 2 && HeyPrefixes.Contains(words[0]):
                target = words[1];
                break;
            case WakeWordPhrase.Kohana or WakeWordPhrase.Sakura when words.Length == 1:
                target = words[0];
                break;
            case WakeWordPhrase.Kohana or WakeWordPhrase.Sakura
                when words.Length == 2 &&
                     (OyePrefixes.Contains(words[0]) || HeyPrefixes.Contains(words[0])):
                target = words[1];
                break;
        }

        if (target is null)
        {
            return false;
        }

        // Diseño D69 — cada familia tiene su nombre. Lo demás —prefijos, sensibilidades, la forma
        // de decidir— es igual para las dos, que es la razón de que el cambio de nombre quepa aquí
        // y no exija otro reconocedor.
        var exact = phrase.IsSakura() ? SakuraExactNames : ExactBrandNames;
        var phonetic = phrase.IsSakura() ? SakuraPhoneticNames : PhoneticBrandNames;
        var brand = phrase.IsSakura() ? "Sakura" : "Kohana";

        if (exact.Contains(target))
        {
            kind = WakeWordMatchKind.Exact;
            detail = $"Coincidió con {brand}.";
            return true;
        }

        if (phonetic.Contains(target))
        {
            kind = WakeWordMatchKind.Phonetic;
            detail = $"Coincidió con la pronunciación española “{target}”.";
            return true;
        }

        return false;
    }

    private static bool TryMatchApproximatePhrase(
        string normalized,
        WakeWordPhrase phrase,
        out string detail)
    {
        var words = normalized.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        detail = string.Empty;

        string? target = null;
        if (phrase == WakeWordPhrase.Kohana)
        {
            if (words.Length == 1)
            {
                target = words[0];
            }
            else if (words.Length == 2 &&
                     (OyePrefixes.Contains(words[0]) || HeyPrefixes.Contains(words[0])))
            {
                target = words[1];
            }
        }
        else if (words.Length == 2)
        {
            var validPrefix = phrase == WakeWordPhrase.OyeKohana
                ? OyePrefixes.Contains(words[0])
                : HeyPrefixes.Contains(words[0]);
            if (validPrefix)
            {
                target = words[1];
            }
        }
        else if (words.Length == 1)
        {
            // En sensibilidad Alta se tolera que Vosk omita “oye/ey”.
            target = words[0];
        }

        if (target is null || target.Length is < 4 or > 9)
        {
            return false;
        }

        var candidates = phrase.IsSakura()
            ? new[] { "sakura", "sacura", "zacura" }
            : ["kohana", "cojana", "kojana"];

        var distance = candidates.Min(candidate => LevenshteinDistance(target, candidate));
        if (distance > 2)
        {
            return false;
        }

        detail = $"Coincidencia aproximada de alta sensibilidad con “{target}”.";
        return true;
    }

    private static string BuildRejectionDetail(string normalized, WakeWordPhrase phrase)
    {
        if (normalized.Contains("nexo", StringComparison.Ordinal) ||
            normalized.Contains("neso", StringComparison.Ordinal))
        {
            return $"Se escuchó Nexo, pero el modo {(phrase.IsSakura() ? "Sakura" : "Kohana")} ya no acepta el nombre heredado.";
        }

        return phrase.IsSakura()
            ? $"“{normalized}” no coincide con “{phrase.ToSpokenText()}”."
            : $"“{normalized}” no coincide con “{phrase.ToSpokenText()}” ni con cojana/kojana.";
    }

    private static string Canonicalize(string normalized)
    {
        if (normalized.Length == 0)
        {
            return normalized;
        }

        // Diseño D66 — estas seis van primero a propósito. Vosk parte "Kohana" en dos fichas con
        // una vocal suelta delante ("eco jana", "ico jana", "eco gana"), y las reglas de abajo
        // reconocen "co jana" dentro de "eco jana": si corrieran antes, dejarían "ecojana", que no
        // es nada. Medido sobre la grabación del micrófono de Adler.
        return normalized
            .Replace("eco jana", "cojana", StringComparison.Ordinal)
            .Replace("ico jana", "cojana", StringComparison.Ordinal)
            .Replace("eco jena", "cojana", StringComparison.Ordinal)
            .Replace("eco gana", "cojana", StringComparison.Ordinal)
            .Replace("ico gana", "cojana", StringComparison.Ordinal)
            .Replace("coja man", "cojana", StringComparison.Ordinal)
            .Replace("coge ama", "cogeme", StringComparison.Ordinal)
            .Replace("con jarana", "jarana", StringComparison.Ordinal)
            .Replace("ko hana", "kohana", StringComparison.Ordinal)
            .Replace("ko ana", "koana", StringComparison.Ordinal)
            .Replace("co hana", "cohana", StringComparison.Ordinal)
            .Replace("co ana", "coana", StringComparison.Ordinal)
            .Replace("co jana", "cojana", StringComparison.Ordinal)
            .Replace("ko jana", "kojana", StringComparison.Ordinal)
            .Replace("coja ana", "cojana", StringComparison.Ordinal)
            .Replace("coja na", "cojana", StringComparison.Ordinal)
            .Replace("koja na", "kojana", StringComparison.Ordinal)
            .Replace("con ana", "conana", StringComparison.Ordinal)
            .Replace("co gana", "cogana", StringComparison.Ordinal)
            .Replace("co yana", "coyana", StringComparison.Ordinal)
            .Replace("o hana", "ohana", StringComparison.Ordinal);
    }

    private static void AddTargets(HashSet<string> phrases, IEnumerable<string> targets)
    {
        foreach (var target in targets)
        {
            phrases.Add(target);
        }
    }

    private static void AddPrefixed(
        HashSet<string> phrases,
        IEnumerable<string> prefixes,
        IEnumerable<string> targets)
    {
        foreach (var prefix in prefixes)
        {
            foreach (var target in targets)
            {
                phrases.Add($"{prefix} {target}");
            }
        }
    }

    private static int LevenshteinDistance(string left, string right)
    {
        if (left.Length == 0)
        {
            return right.Length;
        }

        if (right.Length == 0)
        {
            return left.Length;
        }

        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];
        for (var column = 0; column <= right.Length; column++)
        {
            previous[column] = column;
        }

        for (var row = 1; row <= left.Length; row++)
        {
            current[0] = row;
            for (var column = 1; column <= right.Length; column++)
            {
                var substitutionCost = left[row - 1] == right[column - 1] ? 0 : 1;
                current[column] = Math.Min(
                    Math.Min(current[column - 1] + 1, previous[column] + 1),
                    previous[column - 1] + substitutionCost);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }
}
