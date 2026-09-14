namespace Nexo.Core.Distribution;

/// <summary>Cómo se registra Sakura para arrancar con Windows.</summary>
public enum StartupRegistration
{
    /// <summary>Un valor en <c>HKCU\...\CurrentVersion\Run</c>.</summary>
    RunKey,

    /// <summary>La extensión <c>windows.startupTask</c> del paquete.</summary>
    PackageStartupTask
}

/// <summary>
/// Diseño D89 — lo que cada canal puede hacer, decidido en un solo sitio.
///
/// Cada respuesta tiene su motivo, y ninguno es de gusto:
/// </summary>
public static class DistributionPolicy
{
    /// <summary>
    /// El actualizador propio sustituye la carpeta del programa. En la Store esa carpeta es de solo
    /// lectura y quien actualiza es la Store; un segundo mecanismo compitiendo con ella solo puede
    /// romper la instalación.
    /// </summary>
    public static bool UsesOwnUpdater(DistributionChannel channel) =>
        channel == DistributionChannel.Direct;

    /// <summary>
    /// D87 corrige la entrada de «Aplicaciones instaladas» que escribe Inno. Un paquete no tiene esa
    /// entrada —la gestiona Windows—, y dentro del paquete la escritura en el registro del usuario
    /// quedaría en una copia privada que no ve nadie.
    /// </summary>
    public static bool ReconcilesInstalledAppsEntry(DistributionChannel channel) =>
        channel == DistributionChannel.Direct;

    /// <summary>
    /// Política 10.2.3 de Microsoft Store: una app no puede ofrecer instalar software que no ha
    /// desarrollado quien la publica. Ollama es de otros, así que en la Store se enlaza a su web en vez
    /// de descargarlo y ponerlo en marcha. La política 10.2.4 pide además avisarlo al principio de la
    /// descripción.
    /// </summary>
    public static bool InstallsOllamaItself(DistributionChannel channel) =>
        channel == DistributionChannel.Direct;

    /// <summary>
    /// Dentro de un paquete, lo que se escribe en <c>HKCU</c> va a una copia privada del paquete, así
    /// que la clave <c>Run</c> nunca llegaría a Windows. El mecanismo soportado es la tarea de inicio
    /// declarada en el manifiesto.
    /// </summary>
    public static StartupRegistration Startup(DistributionChannel channel) =>
        channel == DistributionChannel.MicrosoftStore
            ? StartupRegistration.PackageStartupTask
            : StartupRegistration.RunKey;
}
