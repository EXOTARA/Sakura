using Nexo.Core.Branding;

namespace Nexo.Core.Diagnostics;

/// <summary>
/// Rutas privadas de Sakura. El nombre de la clase se conserva temporalmente
/// para no forzar un renombrado masivo de namespaces durante la migración.
///
/// Diseño D3.2 — punto único de resolución de la raíz de datos. Antes de este diseño, todas las
/// propiedades ya derivaban de <see cref="RootDirectory"/> (ningún store leía su propia variable
/// de entorno por separado); este diseño solo le enseña a <see cref="RootDirectory"/> a resolver
/// un override — nada más en esta clase cambia de forma. Orden de precedencia:
/// 1. <see cref="UseExplicitDataRoot"/> (fijado por el proceso, p. ej. un futuro flag de línea de
///    comandos o un arnés de pruebas/validación).
/// 2. Variable de entorno <see cref="DataRootEnvironmentVariable"/> (pensada para scripts de
///    validación y desarrollo).
/// 3. Ruta de producción real (`%LocalAppData%\Sakura`) — sin cambios de comportamiento cuando no
///    hay ningún override activo.
///
/// Cuando hay un override activo, <see cref="LegacyRootDirectory"/> colapsa a la misma raíz que
/// <see cref="RootDirectory"/>: esto hace que <c>LegacyDataMigrator</c> vea origen == destino y no
/// copie nada — un perfil de validación nunca debe leer ni migrar los datos reales del usuario
/// (`%LocalAppData%\Nexo`), ni mezclarse con el perfil normal.
/// </summary>
public static class NexoDataPaths
{
    /// <summary>
    /// Diseño D3.2 — variable de entorno para apuntar la raíz de datos a un perfil aislado
    /// (validación interactiva, desarrollo, automatización, diagnóstico). No documentada como
    /// opción de usuario final: no hay selector en la UI normal.
    /// </summary>
    public const string DataRootEnvironmentVariable = "SAKURA_DATA_ROOT";

    /// <summary>
    /// Diseño D70 — el nombre anterior de la variable sigue funcionando. Quien tenga un perfil de
    /// validación montado con ella no debería descubrir el cambio de nombre por un fallo.
    /// </summary>
    public const string LegacyDataRootEnvironmentVariable = "KOHANA_DATA_ROOT";

    private static string? _explicitOverrideRoot;

    private static string LocalApplicationData =>
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    /// <summary>
    /// Diseño D3.2 — fija (o limpia, con <c>null</c>/cadena vacía) una raíz de datos explícita,
    /// con prioridad sobre la variable de entorno. Nunca se activa sola: alguien tiene que
    /// llamarla a propósito, así que el comportamiento de producción no cambia por defecto.
    /// Una cadena vacía o solo espacios NUNCA se acepta como una raíz real — se trata igual que
    /// "sin override", no como "usar la carpeta actual" ni ninguna otra ruta implícita.
    /// </summary>
    public static void UseExplicitDataRoot(string? path)
    {
        _explicitOverrideRoot = NormalizeOverride(path);
    }

    /// <summary>Diseño D3.2 — true si hay un perfil de validación/desarrollo activo.</summary>
    public static bool IsUsingOverrideRoot => ResolveOverrideRoot() is not null;

    /// <summary>
    /// Diseño D3.2 — la raíz de datos activa junto con de dónde vino, pensado para dejar
    /// constancia en un log de diagnóstico sin arriesgar exponer la ruta a quien no la pidió.
    /// </summary>
    public static (string Root, string Source) DescribeActiveRoot()
    {
        if (_explicitOverrideRoot is not null)
        {
            return (_explicitOverrideRoot, "explícito (proceso)");
        }

        var envOverride = NormalizeOverride(
            Environment.GetEnvironmentVariable(DataRootEnvironmentVariable) ??
            Environment.GetEnvironmentVariable(LegacyDataRootEnvironmentVariable));
        return envOverride is not null
            ? (envOverride, $"variable de entorno {DataRootEnvironmentVariable}")
            : (RootDirectory, "producción");
    }

    public static string RootDirectory =>
        ResolveOverrideRoot() ??
        Path.Combine(LocalApplicationData, ProductIdentity.DataDirectoryName);

    public static string LegacyRootDirectory =>
        IsUsingOverrideRoot
            ? RootDirectory
            : Path.Combine(LocalApplicationData, ProductIdentity.LegacyDataDirectoryName);

    /// <summary>
    /// Diseño D70 — todas las carpetas de nombres anteriores, de la más reciente a la más vieja.
    ///
    /// Son dos porque el producto se ha llamado de tres formas y los datos no viajaron todos a la
    /// vez: los modelos de voz siguen en la carpeta de la primera etapa. Buscar en cadena es lo que
    /// permite renombrar sin dejar a nadie sin sus archivos, y es también lo que permite que la
    /// consolidación los recoja de donde estén.
    ///
    /// Con un override de validación activo colapsa a la raíz activa, igual que
    /// <see cref="LegacyRootDirectory"/>: un perfil aislado no debe leer los datos reales.
    /// </summary>
    public static IReadOnlyList<string> LegacyRootDirectories =>
        IsUsingOverrideRoot
            ? [RootDirectory]
            : ProductIdentity.PreviousDataDirectoryNames
                .Select(name => Path.Combine(LocalApplicationData, name))
                .ToArray();

    /// <summary>
    /// Devuelve la primera ruta que exista: primero la actual, luego cada nombre anterior en orden.
    /// Si no existe ninguna, devuelve la actual, que es donde se creará.
    /// </summary>
    private static string PreferExistingInChain(Func<string, string> build)
    {
        var current = build(RootDirectory);
        if (Directory.Exists(current) || File.Exists(current))
        {
            return current;
        }

        foreach (var legacyRoot in LegacyRootDirectories)
        {
            var candidate = build(legacyRoot);
            if (Directory.Exists(candidate) || File.Exists(candidate))
            {
                return candidate;
            }
        }

        return current;
    }

    private static string? ResolveOverrideRoot()
    {
        if (_explicitOverrideRoot is not null)
        {
            return _explicitOverrideRoot;
        }

        return NormalizeOverride(
            Environment.GetEnvironmentVariable(DataRootEnvironmentVariable) ??
            Environment.GetEnvironmentVariable(LegacyDataRootEnvironmentVariable));
    }

    /// <summary>
    /// Diseño D3.2 — una ruta inválida (vacía, solo espacios, o que .NET no pueda resolver a una
    /// ruta absoluta) nunca se acepta en silencio como si fuera válida: se normaliza a "sin
    /// override", que cae de vuelta a la siguiente prioridad (variable de entorno o producción),
    /// en vez de lanzar y tumbar el arranque de la aplicación por un valor mal escrito.
    /// </summary>
    private static string? NormalizeOverride(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(path.Trim());
        }
        catch (Exception exception) when (
            exception is ArgumentException or PathTooLongException or NotSupportedException)
        {
            return null;
        }
    }

    public static string RuntimeDirectory => Path.Combine(
        RootDirectory,
        "Runtime");

    public static string LegacyRuntimeDirectory => Path.Combine(
        LegacyRootDirectory,
        "Runtime");

    public static string OllamaRuntimeDirectory =>
        PreferExistingInChain(root => Path.Combine(root, "Runtime", "Ollama"));

    public static string ModelsDirectory => Path.Combine(
        RootDirectory,
        "Models");

    public static string LegacyModelsDirectory => Path.Combine(
        LegacyRootDirectory,
        "Models");

    public static string OllamaModelsDirectory =>
        PreferExistingInChain(root => Path.Combine(root, "Models", "Ollama"));

    public static string VoskModelsDirectory =>
        PreferExistingInChain(root => Path.Combine(root, "Models", "Vosk"));

    public static string WhisperModel =>
        PreferExistingInChain(root => Path.Combine(root, "Models", "ggml-base.bin"));

    public static string OllamaExecutable =>
        Path.Combine(OllamaRuntimeDirectory, "ollama.exe");

    public static string TempDirectory =>
        Path.Combine(RootDirectory, "Temp");

    public static string OllamaInstallerTempDirectory =>
        Path.Combine(TempDirectory, "OllamaInstaller");

    public static string LogsDirectory =>
        Path.Combine(RootDirectory, "Logs");

    public static string OllamaRuntimeLog =>
        Path.Combine(LogsDirectory, "ollama-runtime.log");

    public static string ResourceGovernorLog =>
        Path.Combine(LogsDirectory, "resource-governor.log");

    /// <summary>
    /// Fallos de comandos del Sakura Command Center (Diseño D2). Guarda el detalle técnico que la
    /// notificación no modal no muestra: tipo, mensaje, excepción interna y stack trace.
    /// </summary>
    public static string CommandCenterLog =>
        Path.Combine(LogsDirectory, "command-center.log");

    public static string VoiceCaptureLog =>
        Path.Combine(LogsDirectory, "voice-capture.log");

    public static string WakeWordRecognitionLog =>
        Path.Combine(LogsDirectory, "wake-word-recognition.log");

    public static string Settings => Path.Combine(RootDirectory, "settings.json");
    public static string Tasks => Path.Combine(RootDirectory, "tasks.json");
    public static string Focus => Path.Combine(RootDirectory, "focus.json");
    public static string Routines => Path.Combine(RootDirectory, "routines.json");
    public static string Habits => Path.Combine(RootDirectory, "habits.json");
    public static string AmbientRequests => Path.Combine(RootDirectory, "ambient-requests.json");
    public static string Memory => Path.Combine(RootDirectory, "memory.dat");

    /// <summary>
    /// Claves de API de los proveedores de nube, cifradas con DPAPI. Archivo aparte de
    /// <see cref="Settings"/> a propósito: los ajustes acaban en capturas de pantalla y diagnósticos.
    /// </summary>
    public static string AiCredentials => Path.Combine(RootDirectory, "ai-credentials.dat");
    public static string OptimizationSnapshot =>
        Path.Combine(RootDirectory, "optimization-snapshot.json");

    /// <summary>
    /// Diseño D11 — registro propio de la optimización. Sustituido en D13 por el Audit Log único;
    /// la ruta sobrevive solo para poder importar lo que ya estuviera escrito.
    /// </summary>
    public static string OptimizationAudit =>
        Path.Combine(RootDirectory, "optimization-audit.json");

    /// <summary>Diseño D13 — Audit Log orientado al usuario: qué hizo Sakura, cuándo y con qué permiso.</summary>
    public static string Audit => Path.Combine(RootDirectory, "audit.json");

    /// <summary>Diseño D12 — carpeta de trabajo autorizada y su historial de autorizaciones.</summary>
    public static string Workspace => Path.Combine(RootDirectory, "workspace.json");

    /// <summary>Diseño D14 — copias previas de los archivos que Sakura modificó, para poder deshacer.</summary>
    public static string WorkspaceCheckpoints =>
        Path.Combine(RootDirectory, "workspace-checkpoints.json");

    /// <summary>Diseño D15 — ajustes anteriores al pack activo, para poder desactivarlo.</summary>
    public static string SkillPackSnapshot =>
        Path.Combine(RootDirectory, "skill-pack-snapshot.json");

    /// <summary>Diseño D18 — pasos de Computer Use que Sakura puede deshacer.</summary>
    public static string ComputerUseSnapshots =>
        Path.Combine(RootDirectory, "computer-use-snapshots.json");
    public static string Conversation => Path.Combine(
        RootDirectory,
        "conversation-history.json");

    private static string PreferExistingDirectory(
        string currentPath,
        string legacyPath) =>
        Directory.Exists(currentPath) || !Directory.Exists(legacyPath)
            ? currentPath
            : legacyPath;

    private static string PreferExistingFile(
        string currentPath,
        string legacyPath) =>
        File.Exists(currentPath) || !File.Exists(legacyPath)
            ? currentPath
            : legacyPath;
}
