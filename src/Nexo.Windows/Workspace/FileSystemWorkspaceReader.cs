using System.IO.Enumeration;
using Nexo.Core.Workspace;

namespace Nexo.Windows.Workspace;

/// <summary>
/// Diseño D12 (Fase 5) — lectura real del proyecto autorizado. Cada archivo pasa por
/// <see cref="WorkspacePathPolicy"/> ANTES de abrirse; esta clase no decide nada por su cuenta, solo
/// ejecuta lo que la política permite. Es a propósito: si la decisión viviera aquí, cada nueva forma
/// de listar o buscar podría inventarse sus propias reglas.
///
/// El recorrido no sigue enlaces simbólicos ni uniones de directorio. En Windows una unión dentro
/// del proyecto puede apuntar a cualquier parte del disco, y seguirla convertiría "autoricé esta
/// carpeta" en "autoricé lo que esa carpeta señale".
/// </summary>
public sealed class FileSystemWorkspaceReader : IWorkspaceReader
{
    private static readonly EnumerationOptions Enumeration = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        // ReparsePoint ya no se salta por atributo: los archivos «Files On-Demand» de OneDrive lo llevan
        // y un proyecto en Escritorio/Documentos sincronizados quedaría sin leer. Los enlaces reales se
        // saltan en EnumerateSafely con LinkTarget.
        AttributesToSkip = FileAttributes.System | FileAttributes.Hidden
    };

    private static bool IsRealLink(ref FileSystemEntry entry) =>
        (entry.Attributes & FileAttributes.ReparsePoint) != 0 && entry.ToFileSystemInfo().LinkTarget is not null;

    /// <summary>
    /// Diseño D41 — tope de entradas EXAMINADAS, no solo de devueltas.
    ///
    /// El tope de archivos que ya había limitaba el resultado, no el trabajo: si el filtro rechaza
    /// todo lo que encuentra, el recorrido nunca llega al tope y sigue caminando. Con una carpeta
    /// enorme eso significa recorrerla entera. Se vio con una carpeta que devolvió cero archivos
    /// legibles después de mirar cientos de miles, y dejó la aplicación sin responder.
    ///
    /// Cincuenta mil entradas es mucho más de lo que tiene cualquier proyecto real y se recorre en
    /// un instante; lo que corta es el caso patológico, no el uso normal.
    /// </summary>
    private const int MaximumEntriesExamined = 50_000;

    public IReadOnlyList<WorkspaceFile> ListFiles(string authorizedRoot, int maximumFiles)
    {
        if (!Directory.Exists(authorizedRoot) || maximumFiles <= 0)
        {
            return [];
        }

        var files = new List<WorkspaceFile>();
        var examined = 0;

        foreach (var path in EnumerateSafely(authorizedRoot))
        {
            if (++examined > MaximumEntriesExamined)
            {
                break;
            }

            if (!WorkspacePathPolicy.CanRead(path, authorizedRoot).IsAllowed)
            {
                continue;
            }

            long length;
            try
            {
                length = new FileInfo(path).Length;
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            if (!WorkspacePathPolicy.CheckSize(length).IsAllowed)
            {
                continue;
            }

            files.Add(new WorkspaceFile(path, Path.GetRelativePath(authorizedRoot, path), length));

            if (files.Count >= maximumFiles)
            {
                break;
            }
        }

        return files;
    }

    public WorkspaceReadResult ReadFile(string authorizedRoot, string fullPath)
    {
        var relative = SafeRelativePath(authorizedRoot, fullPath);

        var verdict = WorkspacePathPolicy.CanRead(fullPath, authorizedRoot);
        if (!verdict.IsAllowed)
        {
            return WorkspaceReadResult.Refused(relative, verdict.Message);
        }

        try
        {
            var info = new FileInfo(fullPath);
            if (!info.Exists)
            {
                return WorkspaceReadResult.Refused(relative, "Ese archivo no existe.");
            }

            var size = WorkspacePathPolicy.CheckSize(info.Length);
            if (!size.IsAllowed)
            {
                return WorkspaceReadResult.Refused(relative, size.Message);
            }

            var raw = File.ReadAllText(fullPath);
            var redacted = WorkspaceSecretScanner.Redact(raw);

            return WorkspaceReadResult.Read(relative, redacted, secretsRedacted: redacted != raw);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return WorkspaceReadResult.Refused(relative, "No pude abrir ese archivo.");
        }
    }

    public IReadOnlyList<WorkspaceSearchHit> Search(string authorizedRoot, string query, int maximumHits)
    {
        if (string.IsNullOrWhiteSpace(query) || maximumHits <= 0)
        {
            return [];
        }

        var hits = new List<WorkspaceSearchHit>();
        var needle = query.Trim();

        foreach (var file in ListFiles(authorizedRoot, maximumFiles: 2000))
        {
            string[] lines;
            try
            {
                lines = File.ReadAllLines(file.FullPath);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            for (var index = 0; index < lines.Length; index++)
            {
                if (!lines[index].Contains(needle, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // La línea que se devuelve se redacta igual que el contenido completo: una
                // coincidencia puede caer justo encima de un token.
                hits.Add(new WorkspaceSearchHit(
                    file.RelativePath,
                    index + 1,
                    WorkspaceSecretScanner.Redact(lines[index].Trim())));

                if (hits.Count >= maximumHits)
                {
                    return hits;
                }
            }
        }

        return hits;
    }

    private static IEnumerable<string> EnumerateSafely(string root)
    {
        try
        {
            return new FileSystemEnumerable<string>(root, (ref FileSystemEntry entry) => entry.ToFullPath(), Enumeration)
            {
                ShouldIncludePredicate = (ref FileSystemEntry entry) => !entry.IsDirectory && !IsRealLink(ref entry),
                ShouldRecursePredicate = (ref FileSystemEntry entry) => !IsRealLink(ref entry)
            };
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static string SafeRelativePath(string authorizedRoot, string fullPath)
    {
        try
        {
            return Path.GetRelativePath(authorizedRoot, fullPath);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return fullPath;
        }
    }
}
