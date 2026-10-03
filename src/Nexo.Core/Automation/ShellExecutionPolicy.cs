namespace Nexo.Core.Automation;

/// <summary>
/// Decide si una acción de "abrir una aplicación" es en realidad **ejecución de un comando
/// arbitrario**, y por tanto necesita confirmación explícita del usuario.
///
/// <para>
/// <b>Por qué existe (defecto D2 de la fase 1.1).</b> <c>OpenApplication</c> reenvía
/// <see cref="AutomationAction.Arguments"/> al proceso y estaba clasificada como
/// <see cref="AutomationRiskLevel.Reversible"/>, es decir, **sin confirmación**. Un paso de
/// rutina con <c>Target="powershell.exe"</c> y <c>Arguments="-Command ..."</c> era ejecución
/// arbitraria sin aprobación, lo que incumple `SECURITY_MODEL` (escenario 22 de
/// `TEST_MATRIX`) y la decisión F de `PRODUCT_VISION`.
/// </para>
///
/// <para><b>Reglas normativas implementadas:</b></para>
/// <list type="bullet">
///   <item>Abrir un intérprete <b>sin argumentos</b> no requiere confirmación: abrir la
///     terminal no es ejecutar en ella.</item>
///   <item>Un intérprete <b>con cualquier argumento</b> requiere confirmación. No se intenta
///     distinguir banderas "inocuas" de banderas de ejecución: una lista blanca de banderas es
///     exactamente el tipo de defensa frágil que se evita. Mínimo privilegio por defecto.</item>
///   <item>La detección **no se basa solo en el nombre visible**: se normaliza ruta completa,
///     comillas, variables de entorno, separadores, mayúsculas, extensión omitida y los
///     puntos y espacios finales que Windows ignora al resolver un ejecutable.</item>
///   <item>Los **argumentos también se inspeccionan**: invocar un intérprete desde los
///     argumentos de otro programa (p. ej. <c>explorer.exe</c> con
///     <c>"powershell -Command ..."</c>) también requiere confirmación.</item>
/// </list>
///
/// <para>
/// <b>Alcance.</b> Esta política evalúa la **acción**, nunca quién la pidió. No existe ninguna
/// marca de "ya aprobado" que pueda saltársela, así que una rutina aprobada al crearse no
/// hereda permiso para ejecutar comandos nuevos, y el contenido que llegue del modelo, OCR,
/// archivos, web o skills no puede adquirir autoridad de usuario por el mero hecho de viajar
/// dentro de una acción. La identidad del actor como concepto de primera clase llega en la
/// Fase 5 (`SECURITY_MODEL` §Defensa por capas, punto 2).
/// </para>
/// </summary>
public static class ShellExecutionPolicy
{
    /// <summary>
    /// Intérpretes de comandos, hosts de scripts y binarios del sistema que se usan
    /// habitualmente para ejecutar código arbitrario. La lista es deliberadamente amplia:
    /// un falso positivo solo añade una confirmación; un falso negativo es ejecución sin
    /// aprobación.
    /// </summary>
    private static readonly HashSet<string> Interpreters = new(StringComparer.OrdinalIgnoreCase)
    {
        // Shells de Windows
        "powershell",
        "powershell_ise",
        "pwsh",
        "cmd",
        "command",
        // Lanzadores de terminal: con argumentos ejecutan lo que se les pase (conhost calc.exe,
        // wt new-tab calc.exe); sin argumentos solo abren la terminal.
        "conhost",
        "wt",
        // Hosts de scripts
        "wscript",
        "cscript",
        "mshta",
        // Binarios del sistema que ejecutan código ajeno
        "rundll32",
        "regsvr32",
        "installutil",
        "msbuild",
        "wmic",
        // Shells y runtimes de origen Unix disponibles en Windows
        "wsl",
        "bash",
        "sh",
        "zsh",
        "python",
        "pythonw",
        "node",
        "ruby",
        "perl",
        "php",
        "dotnet"
    };

    /// <summary>
    /// ¿El ejecutable indicado es un intérprete capaz de ejecutar código arbitrario?
    /// </summary>
    public static bool IsInterpreter(string? target) =>
        Interpreters.Contains(NormalizeExecutableName(target)) ||
        Interpreters.Contains(NormalizeExecutableName(Expand(target)));

    // Revisión adversarial 2026-10-03: «%ComSpec%» a secas no tiene ruta que recortar, así que
    // NormalizeExecutableName devolvía «%comspec%» y «%ComSpec% /c calc» no parecía un intérprete,
    // aunque el resto de la política ya trata el destino como si se expandiera.
    private static string Expand(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : Environment.ExpandEnvironmentVariables(value.Trim().Trim('"', '\'').Trim());

    /// <summary>
    /// ¿Esta combinación de ejecutable y argumentos constituye ejecución de un comando
    /// arbitrario y, por tanto, exige confirmación explícita?
    /// </summary>
    public static bool RequiresConfirmation(string? target, string? arguments)
    {
        // Un intérprete con cualquier argumento deja de ser "abrir" y pasa a ser "ejecutar".
        if (IsInterpreter(target) && !string.IsNullOrWhiteSpace(arguments))
        {
            return true;
        }

        // Un programa cualquiera que invoque un intérprete desde sus argumentos.
        return MentionsInterpreter(arguments);
    }

    /// <summary>
    /// Binarios del sistema que ejecutan o instalan otras cosas aunque su nombre no parezca un
    /// intérprete (<c>forfiles /c calc.exe</c>, <c>msiexec /i https://… /qn</c>, <c>schtasks /create</c>,
    /// <c>reg add …\Run</c>). Abrirlos, con o sin argumentos, no es «abrir una aplicación». Los shells
    /// (powershell, pwsh, cmd) NO están aquí a propósito: abrir la terminal sin argumentos no es
    /// ejecutar en ella (decisión F de PRODUCT_VISION), y con argumentos ya los cubre la regla del
    /// intérprete.
    /// </summary>
    private static readonly HashSet<string> SystemExecutors = new(StringComparer.OrdinalIgnoreCase)
    {
        "forfiles", "msiexec", "schtasks", "reg", "rundll32", "regsvr32", "mshta", "wscript",
        "cscript", "certutil", "bitsadmin", "cmstp", "installutil",
        // Revisión adversarial 2026-10-03: el esquema ms-msdt ya preguntaba, pero msdt.exe con los
        // mismos parámetros (Follina) no; hh.exe ejecuta lo que traiga un .chm y mmc.exe un .msc.
        "msdt", "hh", "mmc"
    };

    /// <summary>
    /// Extensiones que ejecutan código, instalan o lanzan otra cosa al abrirse con el shell.
    /// <see cref="NormalizeExecutableName"/> le quita «.bat», «.cmd» y «.ps1» al nombre, así que
    /// <c>x.bat</c> dejaba de parecer un intérprete: se comprueban aparte, sobre el nombre completo.
    /// </summary>
    private static readonly string[] RiskyExtensions =
    [
        ".bat", ".cmd", ".ps1", ".vbs", ".vbe", ".js", ".jse", ".wsf", ".hta", ".msi", ".scr",
        ".lnk", ".url", ".reg", ".cpl", ".pif", ".application", ".appref-ms", ".jar", ".py", ".pyw",
        ".wsh", ".sct", ".msp", ".settingcontent-ms", ".diagcab",
        // Revisión adversarial 2026-10-03: ms-appinstaller y search-ms ya preguntaban como esquema,
        // pero sus archivos equivalentes no; .chm y .msc ejecutan script al abrirse.
        ".chm", ".msc", ".appinstaller", ".msix", ".msixbundle", ".appx", ".appxbundle",
        ".library-ms", ".searchconnector-ms"
    ];

    // ponytail: lista de esquemas conocidos por lanzar o descargar cosas; los esquemas de aplicaciones
    // (spotify:, ms-settings:, steam://) siguen sin pedir confirmación. Si aparece otro esquema
    // peligroso, se añade aquí.
    private static readonly HashSet<string> RiskySchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "http", "https", "ftp", "ftps", "file", "ms-msdt", "search-ms", "ms-appinstaller", "javascript",
        "ms-officecmd", "ms-word", "ms-excel", "ms-powerpoint", "vbscript", "shell",
        // Revisión adversarial 2026-10-03: «search:» es el alias de search-ms; its/ms-its/mk abren
        // el contenido de un .chm.
        "search", "its", "ms-its", "mk"
    };

    /// <summary>
    /// Auditoría 2026-09-28 — decide si <c>OpenApplication</c> necesita confirmación. Antes solo
    /// contaban los intérpretes con argumentos, y quedaban fuera <c>forfiles … /c calc.exe</c>,
    /// <c>msiexec /i https://…</c>, <c>schtasks</c>, <c>reg add</c>, los scripts sueltos
    /// (<c>.vbs</c>, <c>.js</c>, <c>.hta</c>), <c>x.bat</c> y las rutas UNC, que además entregan el
    /// hash NTLM de la persona al servidor remoto. Un programa ordinario (Spotify, el Bloc de notas,
    /// <c>code .</c>, un esquema como <c>spotify:</c>) sigue sin preguntar, con o sin argumentos: las
    /// rutinas solo las crea la persona y preguntar cada vez no protegería de nada. Los argumentos
    /// pasan por las mismas comprobaciones de extensión, UNC y esquema que el destino
    /// (<c>explorer.exe x.bat</c> abre el script con el shell), salvo http/https/www, que en un
    /// argumento es un navegador con una dirección.
    /// </summary>
    public static bool RequiresConfirmationToOpen(string? target, string? arguments)
    {
        if (RequiresConfirmation(target, arguments))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(arguments) &&
            ArgumentFragments(arguments).Any(fragment => IsRiskyReference(fragment, isArgument: true)))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(target))
        {
            return false;
        }

        if (IsRiskyReference(target, isArgument: false))
        {
            return true;
        }

        var expanded = Expand(target);
        return HasShortName(expanded) || SystemExecutors.Contains(NormalizeExecutableName(expanded));
    }

    /// <summary>
    /// Revisión adversarial 2026-10-03 — la carpeta de trabajo de <c>OpenApplication</c> no pasaba por
    /// ninguna comprobación: una rutina «Abrir VS Code» (<c>code .</c>) con la carpeta
    /// <c>\\servidor\recurso</c> abría la ruta de red sin preguntar (y Sakura ya la toca al comprobar
    /// que existe, lo que entrega el hash NTLM). Pregunta igual que una ruta de red en el destino.
    /// </summary>
    public static bool RequiresConfirmationForWorkingDirectory(string? workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory) ||
            workingDirectory.Trim().Equals("{project}", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var name = Expand(workingDirectory);
        return name.StartsWith(@"\\", StringComparison.Ordinal) ||
               name.StartsWith("//", StringComparison.Ordinal) ||
               HasRiskyScheme(name, allowWeb: false);
    }

    /// <summary>
    /// Revisión adversarial 2026-10-03 — los trozos de los argumentos que se revisan. Dos rodeos:
    /// <list type="bullet">
    ///   <item>Windows (CommandLineToArgvW) solo entiende comillas dobles; el tokenizador también
    ///     agrupaba con comillas simples, así que <c>it's \\host\share\x</c> se juntaba en un solo
    ///     trozo que no empezaba por <c>\\</c>. Se revisan los trozos de ambas formas.</item>
    ///   <item><c>explorer.exe /select,\\host\share\x.exe</c> o <c>--carpeta=\\host\share</c>:
    ///     la ruta va pegada a un interruptor, así que se revisa también lo que va tras «,», «;» o
    ///     «=». Una dirección web entera (http, https, www) se deja tal cual, como antes.</item>
    /// </list>
    /// </summary>
    private static IEnumerable<string> ArgumentFragments(string arguments)
    {
        foreach (var token in Tokenize(arguments).Concat(Tokenize(arguments, singleQuotes: false)))
        {
            yield return token;

            var trimmed = token.Trim().Trim('"', '\'').Trim();
            if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var piece in token.Split(new[] { ',', ';', '=' }, StringSplitOptions.RemoveEmptyEntries))
            {
                yield return piece;
            }
        }
    }

    private static bool IsRiskyReference(string value, bool isArgument)
    {
        // ShellExecute expande variables de entorno: %COMSPEC% o una variable que apunte a un UNC
        // tienen que verse tal como se ejecutarán.
        var name = Environment.ExpandEnvironmentVariables(value.Trim()).Trim('"', '\'').Trim()
            .TrimEnd(' ', '.');

        // Un navegador con una dirección («chrome https://…/app.js») es normal; la dirección no se
        // trata como archivo.
        if (isArgument &&
            (name.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
             name.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
             name.StartsWith("www.", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (name.StartsWith(@"\\", StringComparison.Ordinal) ||
            name.StartsWith("//", StringComparison.Ordinal) ||
            (!isArgument && name.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) ||
            HasRiskyScheme(name, allowWeb: isArgument))
        {
            return true;
        }

        return RiskyExtensions.Any(extension => name.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
    }

    // ponytail: nombre corto 8.3 («POWERS~1.EXE») = un «~» seguido de dígito en el último tramo. Es una
    // heurística: un archivo normal con «~1» en el nombre pedirá confirmación de más, y no se resuelve
    // el nombre largo real (haría falta tocar el disco). Solo se mira el destino, no los argumentos.
    private static bool HasShortName(string path)
    {
        var separator = path.LastIndexOfAny(['\\', '/']);
        var last = separator >= 0 ? path[(separator + 1)..] : path;
        for (var index = 0; index < last.Length - 1; index++)
        {
            if (last[index] == '~' && char.IsAsciiDigit(last[index + 1]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasRiskyScheme(string target, bool allowWeb)
    {
        var colon = target.IndexOf(':');

        // Una sola letra antes de «:» es una unidad («C:\…»), no un esquema.
        if (colon <= 1)
        {
            return false;
        }

        var scheme = target[..colon].Trim();
        return RiskySchemes.Contains(scheme) &&
               !(allowWeb && (scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
                              scheme.Equals("https", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// ¿Los argumentos nombran un intérprete? Cubre el rodeo de lanzar un shell a través de
    /// otro programa.
    /// </summary>
    public static bool MentionsInterpreter(string? arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return false;
        }

        foreach (var token in ArgumentFragments(arguments))
        {
            if (Interpreters.Contains(NormalizeExecutableName(token)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Reduce cualquier forma de referirse a un ejecutable a su nombre base en minúsculas.
    /// Maneja comillas, rutas completas, variables de entorno, ambos separadores, extensión
    /// presente o ausente, y los espacios y puntos finales que Windows descarta al resolver
    /// un nombre de archivo.
    /// </summary>
    public static string NormalizeExecutableName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var text = value.Trim().Trim('"', '\'');

        // Windows ignora espacios y puntos finales: "powershell.exe. " resuelve igual.
        text = text.TrimEnd(' ', '.');

        // Último segmento de la ruta, sea cual sea el separador. Cubre rutas absolutas y
        // rutas con variables de entorno sin necesidad de expandirlas.
        var separator = text.LastIndexOfAny(['\\', '/']);
        if (separator >= 0)
        {
            text = text[(separator + 1)..];
        }

        text = text.TrimEnd(' ', '.');

        // Extensión ejecutable opcional.
        foreach (var extension in new[] { ".exe", ".com", ".bat", ".cmd", ".ps1" })
        {
            if (text.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                text = text[..^extension.Length];
                break;
            }
        }

        return text.Trim();
    }

    /// <summary>
    /// Separa una línea de argumentos respetando comillas, para no partir rutas con espacios.
    /// </summary>
    private static IEnumerable<string> Tokenize(string arguments, bool singleQuotes = true)
    {
        var current = new System.Text.StringBuilder();
        var quote = '\0';

        foreach (var character in arguments)
        {
            if (quote != '\0')
            {
                if (character == quote)
                {
                    quote = '\0';
                }
                else
                {
                    current.Append(character);
                }

                continue;
            }

            if (character == '"' || (singleQuotes && character == '\''))
            {
                quote = character;
                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                if (current.Length > 0)
                {
                    yield return current.ToString();
                    current.Clear();
                }

                continue;
            }

            current.Append(character);
        }

        if (current.Length > 0)
        {
            yield return current.ToString();
        }
    }
}
