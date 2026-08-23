namespace Nexo.Core.ComputerUse;

/// <summary>Diseño D17 — un comando de la lista de permitidos, con lo que hace en lenguaje llano.</summary>
public sealed record SafeShellCommand(string Id, string Title, string Executable, string Arguments)
{
    /// <summary>
    /// Lo que se espera a un comando corriente antes de darlo por perdido.
    ///
    /// Está pensado para los que responden al instante, que son casi todos.
    /// </summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Cuánto puede tardar ESTE comando.
    ///
    /// Existe porque un único número para todos estaba mal por los dos lados: ajustado a
    /// `ipconfig`, que contesta al momento, y por tanto demasiado corto para los que enumeran
    /// medio sistema. `driverquery` recorre todos los controladores instalados y en una máquina
    /// virtual pasa de veinte segundos con normalidad — el usuario recibía «tardó demasiado» por
    /// un comando que iba a funcionar perfectamente si se le hubiera dejado terminar.
    ///
    /// Forma parte de la igualdad del registro, y eso es deliberado: la lista de permitidos se
    /// comprueba por valor, así que un comando fabricado fuera tampoco puede colarse cambiando
    /// solo el tiempo.
    /// </summary>
    public TimeSpan Timeout { get; init; } = DefaultTimeout;
}

/// <summary>
/// Diseño D17 (Fase 7) — "shell seguro" del roadmap, y la palabra que manda es **seguro**. No es un
/// intérprete al que se le pueda pasar lo que sea: es una lista corta, fija y escrita a mano de
/// comandos que **solo leen**. Ninguno cambia nada del equipo, así que ninguno necesita reversión.
///
/// Por qué una lista de permitidos y no un filtro de comandos peligrosos: <c>ShellExecutionPolicy</c>
/// ya dejó registrado el argumento en la fase 1.1 — una lista de banderas "inocuas" es exactamente
/// el tipo de defensa frágil que se evita. Aquí no hay nada que filtrar porque no hay nada que
/// componer: el ejecutable y los argumentos son constantes del código.
///
/// **Ningún comando de esta lista acepta entrada del usuario ni del modelo.** En cuanto un argumento
/// venga de fuera deja de ser esta lista y pasa a ser ejecución arbitraria, que necesita otro diseño.
/// </summary>
public static class SafeShellCatalog
{
    public static IReadOnlyList<SafeShellCommand> All { get; } =
    [
        new("net.ip", "Ver la configuración de red", "ipconfig", "/all"),
        new("net.ping", "Comprobar si hay conexión", "ping", "-n 4 1.1.1.1"),
        new("net.dns", "Ver la caché de DNS", "ipconfig", "/displaydns"),
        // Los dos que enumeran el sistema entero tardan de verdad, y no por estar rotos.
        new("sys.info", "Ver los datos del sistema", "systeminfo", string.Empty)
        {
            Timeout = TimeSpan.FromSeconds(60)
        },
        new("sys.drivers", "Ver los controladores instalados", "driverquery", string.Empty)
        {
            Timeout = TimeSpan.FromSeconds(60)
        }
    ];

    // Dos candidatos que se descartaron al escribir la lista, anotados para que no vuelvan solos:
    // `powercfg /batteryreport` ESCRIBE un archivo, así que rompe la invariante de "solo leen"; y
    // `wmic` está en la lista de intérpretes de `ShellExecutionPolicy` —binarios del sistema que
    // ejecutan código ajeno—, así que meterlo aquí contradiría la política que Sakura ya aplica en
    // otro sitio. Una lista de permitidos solo vale si se defiende cuando estorba.

    public static SafeShellCommand? Find(string? id) =>
        string.IsNullOrWhiteSpace(id)
            ? null
            : All.FirstOrDefault(command => string.Equals(command.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Ninguno de estos comandos modifica el equipo, así que no hace falta snapshot para
    /// ejecutarlos. Es una propiedad de la LISTA, no de cada llamada: si algún día entra un comando
    /// que escriba, esto deja de ser cierto y hay que revisarlo aquí, no en quien lo ejecuta.
    /// </summary>
    public static bool IsReadOnly(SafeShellCommand command) => All.Contains(command);
}
