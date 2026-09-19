using System.IO;

namespace Nexo.Core.Documents;

/// <summary>Los sitios con nombre donde Sakura puede dejar un documento.</summary>
public enum DocumentFolder
{
    Desktop,
    Documents,
    Downloads
}

/// <summary>Dónde va a quedar un documento, o por qué no se puede.</summary>
public readonly record struct DocumentTarget(
    bool IsAllowed,
    DocumentFolder Folder,
    string FileName,
    string Message)
{
    public static DocumentTarget Allow(DocumentFolder folder, string fileName) =>
        new(true, folder, fileName, string.Empty);

    public static DocumentTarget Refuse(string message) =>
        new(false, default, string.Empty, message);
}

/// <summary>
/// Diseño D59 — «déjalo en mi escritorio», no <c>C:\Users\Usuario\Desktop</c>.
///
/// Adler lo dijo de forma exacta: estar administrando permisos, carpetas y rutas del estilo
/// <c>Usuario/user/profile1/appdata/downloads</c> es muy complicado para quien no sabe. Y tiene
/// razón en algo más profundo que la comodidad: **una ruta es un detalle de implementación**. Quien
/// pide un documento sabe dónde quiere que aparezca —el escritorio, sus documentos, las descargas—
/// y no tiene por qué saber cómo llama Windows a eso ni dónde vive su perfil.
///
/// Por eso los destinos son tres y son cerrados. No es una limitación provisional: es la frontera.
/// Una carpeta arbitraria por nombre significaría que una frase mal entendida puede escribir en
/// cualquier sitio, y el permiso de proyecto —que sí acota— existe precisamente para lo contrario.
/// Estos tres son sitios donde aparecen archivos constantemente y donde uno nuevo no sorprende a
/// nadie.
///
/// Resolver el nombre está aquí, y no en la capa de Windows, porque lo difícil no es preguntarle al
/// sistema dónde está el escritorio: es decidir qué frases significan «el escritorio» y qué nombres
/// de archivo son aceptables. Eso hay que poder probarlo sin escribir en el disco de nadie.
/// </summary>
public static class DocumentDestination
{
    /// <summary>
    /// Caracteres que Windows no admite en un nombre de archivo. Se piden al propio sistema en vez
    /// de escribirlos a mano: la lista incluye también los de control, que nadie recuerda y que un
    /// dictado puede colar.
    ///
    /// Se comprueba aquí y no se confía en que el sistema falle al escribir: un error de E/S al
    /// final de un trabajo largo es la peor forma de enterarse de que el título llevaba dos puntos.
    /// </summary>
    private static readonly char[] Forbidden = Path.GetInvalidFileNameChars();

    /// <summary>
    /// Nombres reservados por MS-DOS que Windows sigue rechazando hoy. Un documento titulado «CON»
    /// es raro pero no imposible, y el fallo que produce no se parece en nada a su causa.
    /// </summary>
    internal static readonly string[] Reserved =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    ];

    /// <summary>Lo más largo que se admite, dejando sitio para la carpeta y la extensión.</summary>
    internal const int MaximumNameLength = 96;

    /// <summary>
    /// Traduce cómo alguien nombra un sitio. Devuelve <c>null</c> cuando no reconoce ninguno, que no
    /// es lo mismo que un sitio prohibido: quien llama decide si preguntar o usar el escritorio.
    /// </summary>
    public static DocumentFolder? TryReadFolder(string? spoken)
    {
        if (string.IsNullOrWhiteSpace(spoken))
        {
            return null;
        }

        var text = spoken.Trim().ToLowerInvariant();

        // Se busca contenido y no igualdad porque esto llega de una frase hablada: «déjalo en mi
        // escritorio» y «escritorio» piden lo mismo.
        if (Contains(text, "escritorio") || Contains(text, "desktop"))
        {
            return DocumentFolder.Desktop;
        }

        if (Contains(text, "descarga") || Contains(text, "download"))
        {
            return DocumentFolder.Downloads;
        }

        if (Contains(text, "documento") || Contains(text, "document"))
        {
            return DocumentFolder.Documents;
        }

        return null;
    }

    private static bool Contains(string haystack, string needle) =>
        haystack.Contains(needle, StringComparison.Ordinal);

    /// <summary>
    /// Comprueba el destino completo y devuelve el nombre ya con su extensión.
    ///
    /// La extensión la pone Sakura y no se toma del nombre pedido: quien dicta un título no está
    /// eligiendo un formato, y un documento llamado «notas.txt» guardado como Word sería un archivo
    /// que miente sobre lo que lleva dentro.
    /// </summary>
    public static DocumentTarget Resolve(DocumentFolder folder, string? requestedName, string extension)
    {
        if (string.IsNullOrWhiteSpace(requestedName))
        {
            return DocumentTarget.Refuse("Hace falta un nombre para el documento.");
        }

        var name = requestedName.Trim();

        // Un nombre que llega con su propia extensión se queda sin ella: se añade la correcta
        // después, y si no, «informe.docx» acabaría como «informe.docx.docx».
        if (name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^extension.Length].TrimEnd();
        }

        if (name.Length == 0)
        {
            return DocumentTarget.Refuse("Hace falta un nombre para el documento.");
        }

        if (name.IndexOfAny(Forbidden) >= 0)
        {
            return DocumentTarget.Refuse(
                """Ese nombre lleva caracteres que Windows no admite: \ / : * ? " < > |""");
        }

        // Un nombre que empieza por punto se esconde en Windows, y un documento que se pide y no
        // aparece se lee como que no se cre\u00f3.
        if (name.StartsWith('.'))
        {
            return DocumentTarget.Refuse("Un nombre que empieza por punto queda oculto en Windows.");
        }

        // Windows se come los puntos y espacios finales al crear el archivo, así que el nombre
        // guardado no sería el pedido.
        if (name.EndsWith('.') || name.EndsWith(' '))
        {
            name = name.TrimEnd('.', ' ');
            if (name.Length == 0)
            {
                return DocumentTarget.Refuse("Hace falta un nombre para el documento.");
            }
        }

        if (Reserved.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            return DocumentTarget.Refuse($"«{name}» es un nombre reservado por Windows.");
        }

        if (name.Length > MaximumNameLength)
        {
            return DocumentTarget.Refuse(
                $"Ese nombre es demasiado largo: {MaximumNameLength} caracteres como mucho.");
        }

        return DocumentTarget.Allow(folder, name + extension);
    }

    /// <summary>
    /// La otra puerta: para un título que escribió un modelo, no una persona.
    ///
    /// <see cref="Resolve"/> rechaza, y eso es correcto para un nombre que alguien dictó y puede
    /// repetir. Un título como «Introducción: el problema» lo escribe el modelo y nadie lo va a
    /// corregir: rechazarlo tiraba el documento entero, ya hecho, después de preguntar por internet
    /// y de descargar imágenes. Aquí se limpia y nunca se rechaza; el documento por dentro conserva
    /// su título exacto, solo el nombre del archivo se adapta a lo que Windows admite.
    /// </summary>
    public static DocumentTarget ResolveFromTitle(
        DocumentFolder folder, string? title, string extension, string fallback = "Respuesta de Sakura") =>
        DocumentTarget.Allow(folder, DocumentFileName.Sanitize(title, extension, fallback));

    /// <summary>Cómo se dice el destino en un aviso, para no enseñar rutas.</summary>
    public static string Describe(DocumentFolder folder) => folder switch
    {
        DocumentFolder.Desktop => "el escritorio",
        DocumentFolder.Downloads => "las descargas",
        _ => "los documentos"
    };
}
