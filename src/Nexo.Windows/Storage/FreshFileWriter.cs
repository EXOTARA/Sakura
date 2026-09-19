using System.IO;
using System.Text;
using Nexo.Core.Storage;

namespace Nexo.Windows.Storage;

/// <summary>Dónde acabó el archivo (puede no ser el nombre pedido), o por qué no se guardó.</summary>
public readonly record struct FreshFileResult(bool Saved, string FullPath, string Message)
{
    public static FreshFileResult Ok(string fullPath) => new(true, fullPath, string.Empty);

    public static FreshFileResult Failed(string message) => new(false, string.Empty, message);
}

/// <summary>
/// Escribe un archivo NUEVO en una carpeta de la persona (escritorio, Documentos, Imágenes) sin poder
/// pisar nada. Buscar un nombre libre y luego escribir eran dos pasos con un hueco en medio, y
/// <c>File.WriteAllBytes</c> sobrescribe si el archivo aparece justo ahí; con
/// <see cref="FileMode.CreateNew"/> numerar y crear son la misma operación y es el propio sistema
/// quien impide pisar: «No se sobrescribe nunca» pasa de comprobación a garantía.
///
/// A propósito NO se escribe a un temporal para renombrar. Medido: <c>File.Move</c> conserva los
/// atributos, y un temporal oculto renombrado dejaba el documento guardado pero invisible, que es
/// indistinguible de «no se guardó». Por eso tampoco se marca nada como Hidden o Temporary. A cambio,
/// si la escritura falla se borra lo que quedó a medias: ese archivo lo creó Sakura hace un instante
/// y no es de nadie más. Si el proceso muere justo escribiendo, puede quedar uno incompleto (L17).
///
/// Los mensajes de fallo llevan solo un motivo genérico deducido del tipo de excepción: el mensaje de
/// una IOException trae la ruta completa con el usuario de Windows y estos textos pueden acabar en
/// una cápsula o en una conversación que viaja al proveedor de IA.
/// </summary>
public static class FreshFileWriter
{
    /// <summary>Lo que se intenta antes de rendirse con los nombres numerados.</summary>
    public const int MaximumAttempts = 200;

    public static FreshFileResult Write(string folder, string fileName, ReadOnlySpan<byte> content)
    {
        // Un span no puede capturarse en una lambda: se copia. Los documentos son de pocos MB.
        var copy = content.ToArray();
        return Write(folder, fileName, stream => stream.Write(copy, 0, copy.Length));
    }

    public static FreshFileResult Write(string folder, string fileName, string content, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(encoding);

        // GetBytes y no un StreamWriter: así la codificación no añade su BOM por su cuenta
        // (la exportación de la conversación pide UTF-8 sin BOM).
        var bytes = encoding.GetBytes(content);
        return Write(folder, fileName, bytes);
    }

    /// <summary>
    /// La versión que escribe con una acción: existe para poder probar de forma determinista qué
    /// pasa cuando la escritura falla a mitad.
    /// </summary>
    public static FreshFileResult Write(string folder, string fileName, Action<Stream> write) =>
        Write(folder, fileName, write, IsTaken);

    /// <summary>
    /// Un nombre está ocupado si hay un archivo O una carpeta con él: <c>File.Exists</c> da false para
    /// una carpeta, y CreateNew sobre ella lanza UnauthorizedAccessException, que sin esto pasaba por
    /// «no hay permiso» y abortaba un guardado que con «(2)» habría salido bien.
    /// </summary>
    private static bool IsTaken(string path) => File.Exists(path) || Directory.Exists(path);

    /// <summary>
    /// Costura de pruebas: <paramref name="exists"/> es la vía rápida inyectable. Una prueba la hace
    /// mentir («libre») para simular que el archivo aparece entre la comprobación y la apertura; sin
    /// ella la vía rápida enmascaraba que la protección real es CreateNew.
    /// </summary>
    internal static FreshFileResult Write(
        string folder, string fileName, Action<Stream> write, Func<string, bool> exists)
    {
        ArgumentNullException.ThrowIfNull(write);
        ArgumentNullException.ThrowIfNull(exists);

        try
        {
            var stem = Path.GetFileNameWithoutExtension(fileName);
            var extension = Path.GetExtension(fileName);

            for (var index = 1; index <= MaximumAttempts; index++)
            {
                var candidate = Path.Combine(
                    folder, index == 1 ? fileName : $"{stem} ({index}){extension}");

                // Vía rápida: lo habitual es que esté libre, pero cuando está ocupado no hace falta
                // pagar una excepción para enterarse.
                if (exists(candidate))
                {
                    continue;
                }

                FileStream stream;
                try
                {
                    stream = new FileStream(candidate, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                }
                catch (Exception exception) when (
                    exception is IOException or UnauthorizedAccessException && IsTaken(candidate))
                {
                    // Alguien lo creó entre la comprobación y la apertura (o es una carpeta): es la
                    // carrera que antes sustituía el archivo y que ahora no hace daño. Se sigue con el
                    // número siguiente. Un UnauthorizedAccessException REAL (sin permiso, y el nombre
                    // sigue libre) no entra aquí: sale como fallo genérico, no como numeración sin fin.
                    continue;
                }

                try
                {
                    using (stream)
                    {
                        write(stream);
                    }

                    return FreshFileResult.Ok(candidate);
                }
                catch (Exception exception)
                {
                    // CUALQUIER excepción de quien escribe borra lo parcial: si no, quedaba un archivo
                    // vacío con nombre de bueno. El stream ya está cerrado al salir del using.
                    DeleteQuietly(candidate);

                    if (exception is IOException or UnauthorizedAccessException or NotSupportedException)
                    {
                        return FreshFileResult.Failed(
                            $"No pude guardar «{Path.GetFileName(candidate)}»: " +
                            SakuraDataWriteException.ReasonFor(exception));
                    }

                    // Un fallo de programación de quien llama no se disfraza de «no pude guardar».
                    throw;
                }
            }

            return FreshFileResult.Failed(
                "Ya hay demasiados archivos con ese nombre. Prueba con otro título.");
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or
                NotSupportedException or ArgumentException)
        {
            return FreshFileResult.Failed(
                $"No pude guardar «{SafeName(fileName)}»: {SakuraDataWriteException.ReasonFor(exception)}");
        }
    }

    private static string SafeName(string fileName)
    {
        try
        {
            return Path.GetFileName(fileName);
        }
        catch (ArgumentException)
        {
            return "el archivo";
        }
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Es limpieza de lo que Sakura acaba de crear; si tampoco se puede, ya se avisó del fallo.
        }
    }
}
