namespace Nexo.Core.Storage;

/// <summary>
/// Un almacén no pudo escribir (disco lleno, archivo ocupado, sin permiso). Se lanza en lugar de
/// tragarse el fallo o de dejar subir una IOException cruda hasta un manejador de eventos de WPF, que
/// cerraría la app. Quien decide qué hacer con ella es el manager, no el almacén.
/// </summary>
public sealed class SakuraDataWriteException : IOException
{
    public SakuraDataWriteException(string path, string reason, Exception? inner = null)
        : base($"No se pudo guardar el archivo de datos: {reason}", inner)
    {
        Path = path;
        Reason = reason;
    }

    /// <summary>Ruta sin redactar: se queda en memoria y nunca se escribe en el paquete de soporte.</summary>
    public string Path { get; }

    public string Reason { get; }

    /// <summary>
    /// Motivo genérico en español deducido del TIPO de fallo, nunca de su mensaje: el mensaje de una
    /// IOException trae la ruta completa (con el nombre de usuario de Windows) y estos motivos acaban
    /// en mensajes del asistente, que viajan al proveedor de IA y a la conversación exportada.
    /// </summary>
    public static string ReasonFor(Exception exception)
    {
        if (exception is UnauthorizedAccessException)
        {
            return "no hay permiso para escribir ahí.";
        }

        // Los códigos de Win32 viajan en los 16 bits bajos del HResult.
        return (exception.HResult & 0xFFFF) switch
        {
            112 or 39 => "el disco está lleno.",
            32 or 33 => "el archivo está en uso por otro programa.",
            _ => "no se pudo escribir."
        };
    }
}

/// <summary>Aviso de que guardar empezó a fallar. Se emite solo al pasar de «iba bien» a «falla».</summary>
public sealed record DataWriteFailure(string Path, string Reason);
