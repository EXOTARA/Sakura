using Nexo.Core.Diagnostics;

namespace Nexo.Core.Productization;

/// <summary>Diseño D20 — una cosa que Sakura guarda en tu equipo, descrita para una persona.</summary>
public sealed record SakuraDataItem(
    string FileName,
    string Title,
    string WhatItHolds,
    bool IsPersonal,
    bool IsEncrypted)
{
    public string FullPath => Path.Combine(NexoDataPaths.RootDirectory, FileName);
}

/// <summary>
/// Diseño D20 (Fase 9 — Productization) — el inventario de todo lo que Sakura guarda.
///
/// Existe porque tres cosas distintas lo necesitaban y ninguna podía inventárselo por su cuenta: la
/// copia de seguridad previa a actualizar (¿qué hay que copiar?), la desinstalación (¿qué es la app
/// y qué son tus datos?) y el informe de privacidad (¿qué guarda y dónde?). Tenerlo escrito una vez
/// evita la versión mala de esto, que es que cada una tenga su propia lista y una se quede corta
/// justo cuando importa.
///
/// **La lista se mantiene a mano a propósito.** Enumerar la carpeta sería automático y estaría
/// siempre "al día", pero no sabría decir qué es cada archivo ni si es personal — y sin eso el
/// inventario no sirve para lo único que existe: que la persona entienda qué tiene guardado. Que
/// añadir un archivo obligue a describirlo aquí es la característica, no el coste.
/// </summary>
public static class SakuraDataInventory
{
    public static IReadOnlyList<SakuraDataItem> All { get; } =
    [
        new("settings.json", "Tus ajustes",
            "Cómo tienes configurada Sakura: apariencia, voz, IA, permisos y niveles.",
            IsPersonal: true, IsEncrypted: false),

        new("tasks.json", "Tus tareas",
            "Las tareas y pendientes que has creado.",
            IsPersonal: true, IsEncrypted: false),

        new("focus.json", "Tus sesiones de enfoque",
            "Historial de sesiones de enfoque y descansos.",
            IsPersonal: true, IsEncrypted: false),

        // 2026-09-18 — habits.json se escribía desde el 2026-09-16 y nadie lo añadió aquí, así que la
        // copia previa a actualizar no lo copiaba. workspace.json NO está: NexoDataPaths.Workspace
        // existe pero ningún código lo escribe (la carpeta autorizada vive en settings.json), y
        // describir un archivo que nunca existe sería inventar datos.
        new("habits.json", "Tus hábitos",
            "Los hábitos que sigues y los días que los marcaste.",
            IsPersonal: true, IsEncrypted: false),

        new("routines.json", "Tus rutinas",
            "Las rutinas que has definido y sus pasos.",
            IsPersonal: true, IsEncrypted: false),

        new("conversation-history.json", "Tus conversaciones",
            "Lo que has hablado con Sakura, solo si activaste guardar el historial.",
            IsPersonal: true, IsEncrypted: false),

        new("ambient-requests.json", "Solicitudes ambientales",
            "Historial de las respuestas cortas de la píldora.",
            IsPersonal: true, IsEncrypted: false),

        new("memory.dat", "Tu memoria personal",
            "Lo que le pediste recordar. Cifrado con la protección de datos de Windows.",
            IsPersonal: true, IsEncrypted: true),

        new("audit.json", "Registro de actividad",
            "Qué hizo Sakura, cuándo y con qué permiso. No contiene datos personales.",
            IsPersonal: false, IsEncrypted: false),

        new("optimization-snapshot.json", "Estado previo a optimizar",
            "Cómo estaba el equipo antes de la última optimización, para poder deshacerla.",
            IsPersonal: false, IsEncrypted: false),

        new("workspace-checkpoints.json", "Copias previas de tu proyecto",
            "El contenido anterior de los archivos que Sakura modificó, para poder deshacerlo.",
            IsPersonal: true, IsEncrypted: false),

        new("skill-pack-snapshot.json", "Ajustes previos al pack activo",
            "Cómo tenías los ajustes antes de activar un pack, para poder desactivarlo.",
            IsPersonal: false, IsEncrypted: false),

        new("computer-use-snapshots.json", "Pasos que se pueden deshacer",
            "Lo que había en el portapapeles antes de que Sakura lo cambiara.",
            IsPersonal: true, IsEncrypted: false)
    ];

    /// <summary>Lo que hay que copiar antes de actualizar: todo, porque todo es recuperable o tuyo.</summary>
    public static IReadOnlyList<SakuraDataItem> ToBackUp => All;

    /// <summary>
    /// Lo tuyo. Es lo que la desinstalación **no** borra sin preguntar: el resto son datos de
    /// funcionamiento que Sakura puede rehacer, pero esto no lo puede rehacer nadie.
    /// </summary>
    public static IReadOnlyList<SakuraDataItem> Personal =>
        [.. All.Where(item => item.IsPersonal)];

    public static SakuraDataItem? Find(string? fileName) =>
        string.IsNullOrWhiteSpace(fileName)
            ? null
            : All.FirstOrDefault(item =>
                string.Equals(item.FileName, fileName.Trim(), StringComparison.OrdinalIgnoreCase));
}
