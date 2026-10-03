using Nexo.Core.Storage;
using Nexo.Core.Workspace;

namespace Nexo.Windows.Workspace;

/// <summary>
/// Diseño D14 (Fase 5, nivel 4) — escritura real en disco. **No decide nada**: los permisos, la
/// contención de rutas y el checkpoint son de <see cref="WorkspaceEditCoordinator"/>. Aquí solo se
/// escribe, se lee para el checkpoint y se borra al deshacer.
///
/// Aun así se comprueba la contención otra vez antes de tocar el disco. No es desconfianza del
/// coordinador: es que ésta es la última puerta antes de un archivo real, y una comprobación
/// duplicada cuesta microsegundos mientras que escribir fuera del proyecto no tiene arreglo.
/// </summary>
public sealed class FileSystemWorkspaceWriter : IWorkspaceWriter
{
    public (bool Existed, bool Readable, string Content) ReadForCheckpoint(string authorizedRoot, string relativePath)
    {
        if (!TryResolve(authorizedRoot, relativePath, out var fullPath))
        {
            // Una ruta que no se puede resolver no es «un archivo que no existe»: no se sabe.
            return (false, false, string.Empty);
        }

        try
        {
            return File.Exists(fullPath)
                ? (true, true, File.ReadAllText(fullPath))
                : (false, true, string.Empty);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            // Antes esto devolvía «no existía», y el coordinador reemplazaba un archivo que solo
            // había fallado al leerse. Ahora se dice la verdad: no se pudo leer.
            return (false, false, string.Empty);
        }
    }

    public WorkspaceStepResult WriteFile(string authorizedRoot, string relativePath, string content)
    {
        if (!TryResolve(authorizedRoot, relativePath, out var fullPath))
        {
            return WorkspaceStepResult.Failed(
                "Esa ruta cae fuera de la carpeta autorizada, así que no escribí nada.");
        }

        try
        {
            var directory = Path.GetDirectoryName(fullPath);

            // Solo se crean carpetas DENTRO de la raíz autorizada; TryResolve ya lo garantizó.
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Escritura atómica, igual que el resto de stores del proyecto: si Sakura muere a media
            // escritura, el archivo original sigue entero en vez de quedar truncado.
            var temporaryPath = fullPath + ".kohana-tmp";

            // Un «.kohana-tmp» que ya sea un enlace haría que WriteAllText escribiera en su destino.
            if (CrossesReparsePoint(temporaryPath, authorizedRoot))
            {
                return WorkspaceStepResult.Failed(
                    "Esa ruta cae fuera de la carpeta autorizada, así que no escribí nada.");
            }

            File.WriteAllText(temporaryPath, content);
            File.Move(temporaryPath, fullPath, overwrite: true);

            return WorkspaceStepResult.Ok($"Escribí {relativePath}.");
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // Motivo genérico y no exception.Message: trae la ruta completa con el usuario de Windows y
            // este texto acaba en el Audit Log y en mensajes del asistente.
            return WorkspaceStepResult.Failed(
                $"No pude escribir {relativePath}: {SakuraDataWriteException.ReasonFor(exception)}");
        }
    }

    public WorkspaceStepResult DeleteFile(string authorizedRoot, string relativePath)
    {
        if (!TryResolve(authorizedRoot, relativePath, out var fullPath))
        {
            return WorkspaceStepResult.Failed(
                "Esa ruta cae fuera de la carpeta autorizada, así que no borré nada.");
        }

        try
        {
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }

            return WorkspaceStepResult.Ok($"Borré {relativePath}.");
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return WorkspaceStepResult.Failed(
                $"No pude borrar {relativePath}: {SakuraDataWriteException.ReasonFor(exception)}");
        }
    }

    private static bool TryResolve(string authorizedRoot, string relativePath, out string fullPath)
    {
        fullPath = string.Empty;

        if (string.IsNullOrWhiteSpace(authorizedRoot) || string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        try
        {
            var candidate = Path.GetFullPath(Path.Combine(authorizedRoot, relativePath));
            if (!WorkspacePathPolicy.IsInside(candidate, authorizedRoot))
            {
                return false;
            }

            if (CrossesReparsePoint(candidate, authorizedRoot))
            {
                return false;
            }

            fullPath = candidate;
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException
                or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Auditoría 2026-09-28: <c>IsInside</c> compara texto, y una unión o enlace simbólico dentro del
    /// proyecto (<c>proyecto\docs</c> que apunta a otra parte) pasa esa comparación y aun así escribe
    /// fuera. El lector ya salta los reparse points; aquí se rechaza cada segmento EXISTENTE entre la
    /// raíz y el destino (la raíz misma no cuenta: la persona la eligió) y el propio archivo si ya
    /// existe y es un enlace. Si un atributo no se puede leer, se rechaza: fallar cerrado.
    ///
    /// Solo cuentan los enlaces REALES (<see cref="FileSystemInfo.LinkTarget"/> no nulo: simbólicos y
    /// uniones). Los archivos «Files On-Demand» de OneDrive también llevan ReparsePoint, pero son
    /// marcadores de nube y su LinkTarget es null: rechazarlos impediría editar un proyecto que viva
    /// en Escritorio o Documentos sincronizados.
    /// </summary>
    private static bool CrossesReparsePoint(string candidate, string authorizedRoot)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(authorizedRoot));
        var current = candidate;

        while (!string.Equals(
                   Path.TrimEndingDirectorySeparator(current), root, StringComparison.OrdinalIgnoreCase))
        {
            if (IsRealLink(current))
            {
                return true;
            }

            var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(current));
            if (string.IsNullOrEmpty(parent))
            {
                return true;
            }

            current = parent;
        }

        return false;
    }

    private static bool IsRealLink(string path)
    {
        FileAttributes attributes;
        try
        {
            attributes = File.GetAttributes(path);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            // Lo que no existe todavía no puede ser un enlace.
            return false;
        }

        if ((attributes & FileAttributes.ReparsePoint) == 0)
        {
            return false;
        }

        // El tipo importa: LinkTarget de un directorio solo se lee con DirectoryInfo.
        FileSystemInfo info = (attributes & FileAttributes.Directory) != 0
            ? new DirectoryInfo(path)
            : new FileInfo(path);
        return info.LinkTarget is not null;
    }
}
