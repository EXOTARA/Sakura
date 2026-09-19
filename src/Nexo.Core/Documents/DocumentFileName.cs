using System.Globalization;
using System.IO;
using System.Text;

namespace Nexo.Core.Documents;

/// <summary>
/// Un nombre de archivo que siempre sale, a partir de un título que escribió un modelo.
///
/// Los dos puntos y la interrogación de cierre son la forma normal de titular en español
/// («Ensayo: la Revolución Mexicana»), y Windows no los admite en un nombre. Rechazar el título tiraba
/// el documento entero, así que aquí se limpia: nunca falla y nunca devuelve vacío. El título dentro
/// del documento no pasa por aquí y conserva su puntuación.
///
/// Las reglas son las medidas y nada más: no se quita el «¿» de apertura, no se pone mayúscula
/// inicial, no se traduce ningún símbolo.
/// </summary>
public static class DocumentFileName
{
    private static readonly char[] Forbidden = Path.GetInvalidFileNameChars();

    public static string Sanitize(string? requested, string extension, string fallback)
    {
        var name = Clean(requested, extension);

        if (name.Length == 0)
        {
            name = Clean(fallback, extension);
        }

        if (name.Length == 0)
        {
            name = "Documento";
        }

        // Al final y no antes: quitar los caracteres prohibidos convierte «CON:» en «CON», el nombre
        // desnudo. Medido: escribir en <carpeta>\NUL no da error y no deja archivo, así que sin esta
        // comprobación Sakura diría «Documento guardado» sobre algo que no existe. Se mira el trozo
        // anterior al primer punto porque «NUL.txt» también se porta como dispositivo en algunas
        // rutas; con la extensión añadida detrás, solo el nombre desnudo era el caso medido, pero
        // cubrir ambos no cuesta nada.
        var dot = name.IndexOf('.');
        var head = dot >= 0 ? name[..dot] : name;
        if (DocumentDestination.Reserved.Contains(head.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            name += " (documento)";
        }

        return name + extension;
    }

    private static string Clean(string? requested, string extension)
    {
        var name = (requested ?? string.Empty).Trim();

        // Cada carácter prohibido por un espacio, no por un guion: «Introducción: el problema» tiene
        // que leerse «Introducción el problema». Esto incluye «/», «\» y «:», así que el nombre nunca
        // puede salir de su carpeta; «..» se queda sin barras y los puntos se recortan abajo.
        //
        // Los caracteres de formato Unicode (ceros de ancho, marcas RTL/LRM, U+202E…) se quitan del
        // todo: no se ven, así que un título hecho solo de ellos daba un nombre invisible sin activar
        // el texto de reserva, y U+202E puede hacer que «txt.exe» se lea al revés. Los de control
        // (tabuladores, saltos) separan palabras, como un espacio.
        var builder = new StringBuilder(name.Length);
        foreach (var character in name)
        {
            var category = char.GetUnicodeCategory(character);
            if (category == UnicodeCategory.Format)
            {
                continue;
            }

            builder.Append(
                category == UnicodeCategory.Control || Array.IndexOf(Forbidden, character) >= 0
                    ? ' '
                    : character);
        }

        // Espacios repetidos fuera, y puntos y espacios recortados de los dos extremos: Windows se
        // come los finales al crear el archivo (el nombre guardado no sería el pedido) y un nombre
        // que empieza por punto queda oculto.
        name = Tidy(builder.ToString());

        // La extensión se decide DESPUÉS de recortar: con «informe.docx.» quitarla antes dejaba el
        // punto final pegado y acababa en «informe.docx.docx». Misma regla que
        // DocumentDestination.Resolve.
        if (extension.Length > 0 && name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
        {
            name = Tidy(name[..^extension.Length]);
        }

        // Tope de 96, el mismo de DocumentDestination, cortando por la última palabra entera. Se corta
        // en límites de elemento de texto y no de unidad UTF-16: partir una pareja de sustitutos
        // (un emoji) dejaba un carácter huérfano que ni siquiera se puede codificar.
        if (name.Length > DocumentDestination.MaximumNameLength)
        {
            var limit = 0;
            foreach (var start in StringInfo.ParseCombiningCharacters(name))
            {
                if (start > DocumentDestination.MaximumNameLength)
                {
                    break;
                }

                limit = start;
            }

            var boundary = limit < name.Length && name[limit] == ' '
                ? limit
                : name[..limit].LastIndexOf(' ');

            // Una sola «palabra» de más de 96 caracteres no tiene dónde cortar limpiamente.
            name = Tidy(boundary > 0 ? name[..boundary] : name[..limit]);
        }

        return name;
    }

    private static string Tidy(string text) =>
        string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim('.', ' ');
}
