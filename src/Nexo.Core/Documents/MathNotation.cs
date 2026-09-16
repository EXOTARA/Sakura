using System.Text;

namespace Nexo.Core.Documents;

/// <summary>Una pieza de una fórmula ya entendida, lista para dibujarse.</summary>
public abstract record MathNode;

/// <summary>Texto suelto. <paramref name="Upright"/> distingue «x» (variable, en cursiva) de «2» o «+».</summary>
public sealed record MathRun(string Text, bool Upright) : MathNode;

/// <summary>Varias piezas seguidas.</summary>
public sealed record MathSequence(IReadOnlyList<MathNode> Parts) : MathNode;

/// <summary>Base con subíndice, superíndice o los dos.</summary>
public sealed record MathScript(MathNode Base, MathNode? Sub, MathNode? Sup) : MathNode;

/// <summary>Una fracción de verdad, con la raya horizontal.</summary>
public sealed record MathFraction(MathNode Numerator, MathNode Denominator) : MathNode;

/// <summary>Una raíz; <paramref name="Degree"/> nulo es la cuadrada.</summary>
public sealed record MathRadical(MathNode Radicand, MathNode? Degree) : MathNode;

/// <summary>Algo entre paréntesis, corchetes, llaves o barras de valor absoluto.</summary>
public sealed record MathDelimited(string Open, string Close, MathNode Content) : MathNode;

/// <summary>Sumatoria, productoria o integral, con sus límites.</summary>
public sealed record MathNAry(string Operator, MathNode? Lower, MathNode? Upper, MathNode Body) : MathNode;

/// <summary>Una función con nombre: sen, cos, ln… o un límite, cuyo nombre lleva debajo su condición.</summary>
public sealed record MathFunction(string Name, MathNode? Under, MathNode Argument) : MathNode;

/// <summary>
/// 2026-09-16 — entiende una fórmula escrita en texto y la deja lista para el editor de ecuaciones
/// (Adler: «si hay operaciones que las pase al editor de ecuaciones de Word… que esté en LaTeX»).
///
/// Acepta las dos formas de escribir que la gente ya tiene en los dedos, sin obligar a aprender una:
/// la de la calculadora y Excel —<c>x^2</c>, <c>x_1</c>, <c>sqrt(x)</c>, <c>pi</c>, <c>&lt;=</c>— y la
/// de LaTeX —<c>\frac{a}{b}</c>, <c>\sqrt[3]{x}</c>, <c>\pi</c>, <c>\leq</c>—, que es la que sale
/// cuando se copia de un libro o de otra IA.
///
/// Lo que no entiende no se inventa ni se rompe: vuelve como texto tal cual, y quien lo pidió ve que
/// esa fórmula concreta no se convirtió (<see cref="TryParse"/> devuelve falso).
/// </summary>
public static class MathNotation
{
    /// <summary>Lee la fórmula. Devuelve falso si encuentra algo que no sabría dibujar.</summary>
    public static bool TryParse(string text, out MathNode node)
    {
        try
        {
            var reader = new Reader(text);
            node = reader.ReadSequence(null);
            return !reader.Failed && reader.AtEnd;
        }
        catch (Exception exception) when (exception is InvalidOperationException or IndexOutOfRangeException or ArgumentOutOfRangeException)
        {
            node = new MathRun(text, Upright: true);
            return false;
        }
    }

    /// <summary>La fórmula tal como se lee en el chat, sin los signos de dólar ni las barras de LaTeX.</summary>
    public static string ToPlainText(string text)
    {
        var clean = new StringBuilder(text.Length);
        var reader = new Reader(text);
        Flatten(reader.ReadSequence(null), clean);
        return clean.ToString().Trim();
    }

    private static void Flatten(MathNode node, StringBuilder builder)
    {
        switch (node)
        {
            case MathRun run:
                builder.Append(run.Text);
                break;
            case MathSequence sequence:
                foreach (var part in sequence.Parts)
                {
                    Flatten(part, builder);
                }

                break;
            case MathScript script:
                Flatten(script.Base, builder);
                if (script.Sub is { } sub)
                {
                    builder.Append('_');
                    Flatten(sub, builder);
                }

                if (script.Sup is { } sup)
                {
                    builder.Append('^');
                    Flatten(sup, builder);
                }

                break;
            case MathFraction fraction:
                Flatten(fraction.Numerator, builder);
                builder.Append('/');
                Flatten(fraction.Denominator, builder);
                break;
            case MathRadical radical:
                builder.Append('√');
                Flatten(radical.Radicand, builder);
                break;
            case MathDelimited delimited:
                builder.Append(delimited.Open);
                Flatten(delimited.Content, builder);
                builder.Append(delimited.Close);
                break;
            case MathNAry nary:
                builder.Append(nary.Operator);
                Flatten(nary.Body, builder);
                break;
            case MathFunction function:
                builder.Append(function.Name).Append(' ');
                Flatten(function.Argument, builder);
                break;
        }
    }

    /// <summary>Nombres de función que se escriben derechos y no en cursiva.</summary>
    private static readonly HashSet<string> Functions = new(StringComparer.Ordinal)
    {
        "sin", "sen", "cos", "tan", "cot", "sec", "csc", "ln", "log", "exp", "arcsin", "arccos", "arctan",
        "senh", "cosh", "tanh", "max", "min", "mod", "det"
    };

    /// <summary>
    /// Letras griegas y símbolos, con y sin barra: se escribe <c>\pi</c> o <c>pi</c>, <c>&lt;=</c> o
    /// <c>\leq</c>, y sale lo mismo.
    /// </summary>
    private static readonly Dictionary<string, string> Symbols = new(StringComparer.Ordinal)
    {
        ["alpha"] = "α", ["beta"] = "β", ["gamma"] = "γ", ["delta"] = "δ", ["epsilon"] = "ε",
        ["zeta"] = "ζ", ["eta"] = "η", ["theta"] = "θ", ["lambda"] = "λ", ["mu"] = "μ",
        ["nu"] = "ν", ["xi"] = "ξ", ["pi"] = "π", ["rho"] = "ρ", ["sigma"] = "σ", ["tau"] = "τ",
        ["phi"] = "φ", ["chi"] = "χ", ["psi"] = "ψ", ["omega"] = "ω",
        ["Gamma"] = "Γ", ["Delta"] = "Δ", ["Theta"] = "Θ", ["Lambda"] = "Λ", ["Pi"] = "Π",
        ["Sigma"] = "Σ", ["Phi"] = "Φ", ["Psi"] = "Ψ", ["Omega"] = "Ω",
        ["infty"] = "∞", ["inf"] = "∞", ["partial"] = "∂", ["nabla"] = "∇",
        ["leq"] = "≤", ["le"] = "≤", ["geq"] = "≥", ["ge"] = "≥", ["neq"] = "≠", ["ne"] = "≠",
        ["approx"] = "≈", ["equiv"] = "≡", ["pm"] = "±", ["mp"] = "∓", ["times"] = "×",
        ["div"] = "÷", ["cdot"] = "·", ["to"] = "→", ["rightarrow"] = "→", ["Rightarrow"] = "⇒",
        ["in"] = "∈", ["notin"] = "∉", ["subset"] = "⊂", ["cup"] = "∪", ["cap"] = "∩",
        ["forall"] = "∀", ["exists"] = "∃", ["therefore"] = "∴", ["degree"] = "°"
    };

    /// <summary>Parejas de dos caracteres que la gente escribe con el teclado.</summary>
    private static readonly Dictionary<string, string> Shorthand = new(StringComparer.Ordinal)
    {
        ["<="] = "≤", [">="] = "≥", ["!="] = "≠", ["+-"] = "±", ["->"] = "→", ["=>"] = "⇒", ["~="] = "≈"
    };

    private static readonly Dictionary<string, string> NAryOperators = new(StringComparer.Ordinal)
    {
        ["sum"] = "∑", ["prod"] = "∏", ["int"] = "∫", ["iint"] = "∬", ["oint"] = "∮"
    };

    /// <summary>Lee la fórmula de izquierda a derecha, sin volver atrás.</summary>
    private sealed class Reader(string text)
    {
        private int _position;

        public bool Failed { get; private set; }

        public bool AtEnd => _position >= text.Length;

        private char Current => text[_position];

        private bool Has(int offset = 0) => _position + offset < text.Length;

        /// <param name="closing">Con qué carácter termina este trozo, o nulo hasta el final.</param>
        public MathNode ReadSequence(char? closing)
        {
            var parts = new List<MathNode>();
            while (!AtEnd && (closing is not { } end || Current != end))
            {
                var piece = ReadPiece();
                if (piece is null)
                {
                    break;
                }

                parts.Add(piece);
            }

            return parts.Count == 1 ? parts[0] : new MathSequence(parts);
        }

        /// <summary>Una pieza con sus índices: primero qué es, y después si lleva «_» o «^».</summary>
        private MathNode? ReadPiece()
        {
            var atom = ReadAtom();
            if (atom is null)
            {
                return null;
            }

            MathNode? sub = null;
            MathNode? sup = null;
            while (!AtEnd && (Current == '^' || Current == '_'))
            {
                var isSuperscript = Current == '^';
                _position++;
                var script = ReadAtom() ?? new MathRun(string.Empty, Upright: true);
                if (isSuperscript)
                {
                    sup = script;
                }
                else
                {
                    sub = script;
                }
            }

            return sub is null && sup is null ? atom : new MathScript(atom, sub, sup);
        }

        private MathNode? ReadAtom()
        {
            if (AtEnd)
            {
                return null;
            }

            var character = Current;

            if (character == '\\')
            {
                return ReadCommand();
            }

            if (character is '{' or '(' or '[' or '|')
            {
                return ReadGroup(character);
            }

            if (character is '}' or ')' or ']')
            {
                // Un cierre sin apertura: la fórmula no está equilibrada.
                Failed = true;
                _position++;
                return new MathRun(character.ToString(), Upright: true);
            }

            if (char.IsDigit(character))
            {
                return ReadWhile(c => char.IsDigit(c) || c == '.' || c == ',', upright: true);
            }

            if (char.IsLetter(character))
            {
                return ReadWord();
            }

            if (char.IsWhiteSpace(character))
            {
                // Word separa solo los signos dentro de una ecuación, así que un espacio escrito al
                // lado de un «=» o un «+» saldría doble. Solo se conserva entre dos cosas pegadas
                // que sí necesitan aire: «2 x» o «x dx».
                var previous = _position > 0 ? text[_position - 1] : ' ';
                while (Has() && char.IsWhiteSpace(Current))
                {
                    _position++;
                }

                var next = Has() ? Current : ' ';
                return char.IsLetterOrDigit(previous) && char.IsLetterOrDigit(next)
                    ? new MathRun(" ", Upright: true)
                    : new MathRun(string.Empty, Upright: true);
            }

            return ReadOperator();
        }

        private MathNode ReadWord()
        {
            var start = _position;
            while (Has() && char.IsLetter(Current))
            {
                _position++;
            }

            var word = text[start.._position];

            // «sqrt(x)» y «lim_{x->0} f(x)» se escriben sin barra más a menudo que con ella.
            if (word is "sqrt")
            {
                return ReadRadical();
            }

            if (word is "lim")
            {
                return ReadLimit();
            }

            if (NAryOperators.TryGetValue(word, out var nary))
            {
                return ReadNAry(nary);
            }

            if (Functions.Contains(word))
            {
                return ReadFunction(word);
            }

            if (Symbols.TryGetValue(word, out var symbol))
            {
                return new MathRun(symbol, Upright: true);
            }

            // Varias letras seguidas son varias variables («xy»), salvo que sea una palabra conocida.
            return word.Length == 1
                ? new MathRun(word, Upright: false)
                : new MathSequence([.. word.Select(letter => new MathRun(letter.ToString(), Upright: false))]);
        }

        private MathNode ReadCommand()
        {
            _position++;
            var start = _position;
            while (Has() && char.IsLetter(Current))
            {
                _position++;
            }

            if (start == _position)
            {
                // Una barra seguida de un símbolo: «\{» o «\,».
                var escaped = Has() ? text[_position].ToString() : "\\";
                _position += escaped.Length;
                return new MathRun(escaped == "," ? " " : escaped, Upright: true);
            }

            var name = text[start.._position];
            return name switch
            {
                "frac" or "dfrac" or "tfrac" => ReadFraction(),
                "sqrt" => ReadRadical(),
                "lim" => ReadLimit(),
                "left" or "right" or "displaystyle" or "text" or "mathrm" => ReadTransparent(name),
                _ when NAryOperators.TryGetValue(name, out var nary) => ReadNAry(nary),
                _ when Functions.Contains(name) => ReadFunction(name),
                _ when Symbols.TryGetValue(name, out var symbol) => new MathRun(symbol, Upright: true),
                _ => Unknown(name)
            };
        }

        /// <summary>Órdenes que no dibujan nada por sí mismas.</summary>
        private MathNode ReadTransparent(string name)
        {
            if (name is "left" or "right")
            {
                // El delimitador que viene detrás se lee como tal.
                return ReadAtom() ?? new MathRun(string.Empty, Upright: true);
            }

            SkipSpaces();
            if (!AtEnd && Current == '{')
            {
                _position++;
                var content = ReadSequence('}');
                Expect('}');
                return name is "text" or "mathrm" ? Upright(content) : content;
            }

            return ReadAtom() ?? new MathRun(string.Empty, Upright: true);
        }

        private static MathNode Upright(MathNode node) => node switch
        {
            MathRun run => run with { Upright = true },
            MathSequence sequence => new MathSequence([.. sequence.Parts.Select(Upright)]),
            _ => node
        };

        private MathNode Unknown(string name)
        {
            Failed = true;
            return new MathRun("\\" + name, Upright: true);
        }

        private MathNode ReadFraction()
        {
            var numerator = ReadArgument();
            var denominator = ReadArgument();
            return new MathFraction(numerator, denominator);
        }

        private MathNode ReadRadical()
        {
            SkipSpaces();
            MathNode? degree = null;
            if (!AtEnd && Current == '[')
            {
                _position++;
                degree = ReadSequence(']');
                Expect(']');
            }

            return new MathRadical(ReadArgument(), degree);
        }

        private MathNode ReadLimit()
        {
            SkipSpaces();
            MathNode? under = null;
            if (!AtEnd && Current == '_')
            {
                _position++;
                under = ReadArgument();
            }

            return new MathFunction("lím", under, ReadArgument());
        }

        private MathNode ReadNAry(string symbol)
        {
            MathNode? lower = null;
            MathNode? upper = null;
            while (!AtEnd && (Current == '_' || Current == '^'))
            {
                var isUpper = Current == '^';
                _position++;
                var limit = ReadArgument();
                if (isUpper)
                {
                    upper = limit;
                }
                else
                {
                    lower = limit;
                }
            }

            return new MathNAry(symbol, lower, upper, ReadArgument());
        }

        private MathNode ReadFunction(string name)
        {
            // «sen x» y «sen(x)» son lo mismo; el paréntesis se conserva si lo escribieron.
            return new MathFunction(name == "sin" ? "sen" : name, null, ReadArgument());
        }

        /// <summary>El argumento de una orden: lo que va entre llaves, o la siguiente pieza suelta.</summary>
        private MathNode ReadArgument()
        {
            SkipSpaces();
            if (AtEnd)
            {
                Failed = true;
                return new MathRun(string.Empty, Upright: true);
            }

            if (Current == '{')
            {
                _position++;
                var content = ReadSequence('}');
                Expect('}');
                return content;
            }

            return ReadPiece() ?? new MathRun(string.Empty, Upright: true);
        }

        private MathNode ReadGroup(char open)
        {
            _position++;
            if (open == '{')
            {
                var inner = ReadSequence('}');
                Expect('}');
                return inner;
            }

            var (close, closing) = open switch
            {
                '(' => (')', ')'),
                '[' => (']', ']'),
                _ => ('|', '|')
            };

            var content = ReadSequence(closing);
            Expect(close);
            return new MathDelimited(open.ToString(), close.ToString(), content);
        }

        private MathNode ReadOperator()
        {
            if (Has(1) && Shorthand.TryGetValue(text.Substring(_position, 2), out var pair))
            {
                _position += 2;
                return new MathRun(pair, Upright: true);
            }

            var character = Current;
            _position++;
            var symbol = character switch
            {
                '*' => "·",
                '-' => "−",
                _ => character.ToString()
            };

            return new MathRun(symbol, Upright: true);
        }

        private MathNode ReadWhile(Func<char, bool> accept, bool upright)
        {
            var start = _position;
            while (Has() && accept(Current))
            {
                _position++;
            }

            return new MathRun(text[start.._position], upright);
        }

        private void SkipSpaces()
        {
            while (Has() && char.IsWhiteSpace(Current))
            {
                _position++;
            }
        }

        private void Expect(char character)
        {
            if (!AtEnd && Current == character)
            {
                _position++;
                return;
            }

            // Falta un cierre: se deja pasar, pero la fórmula queda marcada como no convertida.
            Failed = true;
        }
    }
}
