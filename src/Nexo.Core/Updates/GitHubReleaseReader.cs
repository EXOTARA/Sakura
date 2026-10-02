using System.Text.Json;

namespace Nexo.Core.Updates;

/// <summary>Lo que hace falta bajar de una publicación: el paquete y su huella.</summary>
public readonly record struct ReleaseAssets(
    bool IsUsable,
    string Version,
    string PackageUrl,
    long PackageSize,
    string ChecksumUrl,
    string Notes,
    string Problem)
{
    public static ReleaseAssets Rejected(string problem) =>
        new(false, string.Empty, string.Empty, 0, string.Empty, string.Empty, problem);
}

/// <summary>
/// Diseño D64 — lee una publicación de GitHub y saca de ella lo que hace falta.
///
/// Se usa GitHub y no un servidor propio porque **ya es donde se publica**: `publish.ps1` genera el
/// zip y su `.sha256` al lado, y una publicación de GitHub los lleva como adjuntos. Montar un
/// servicio aparte para anunciar versiones sería una pieza más que mantener, que se cae sola, y que
/// haría falta pagar.
///
/// La huella no viene en la respuesta de GitHub, así que se busca el adjunto `.sha256` que
/// acompaña al paquete. Que la huella viaje en un archivo aparte no la debilita: los dos van por
/// https desde el mismo sitio, y lo que se está protegiendo es una descarga que se corta o un
/// servidor que devuelve basura, no a GitHub mintiendo sobre su propio contenido.
///
/// **Aquí solo se lee.** Lo que se saca pasa después por
/// <see cref="UpdateManifestReader"/>, que es quien decide si vale. Separarlo importa: esto entiende
/// la forma de una respuesta de GitHub, y aquello entiende qué es aceptable — y lo segundo no debe
/// cambiar porque GitHub cambie su JSON.
/// </summary>
public static class GitHubReleaseReader
{
    /// <summary>
    /// Las publicaciones del repositorio, de la más reciente hacia atrás.
    ///
    /// **No se usa <c>/releases/latest</c>**, y esto lo descubrió la primera consulta de verdad:
    /// para GitHub, «latest» significa la última que **no** es preliminar. Sakura publica todas sus
    /// versiones como preliminares, así que ese camino devuelve 404 — no «no hay nada nuevo», sino
    /// un error que se lee como que no hay conexión.
    ///
    /// Pedir la lista y elegir por número es además más correcto: cuál es la más nueva lo decide
    /// <see cref="SakuraVersion"/>, que es de quien depende esa respuesta, y no el orden en que
    /// GitHub tenga a bien devolverlas.
    /// </summary>
    public static Uri ReleasesUrl(string owner, string repository) =>
        new($"https://api.github.com/repos/{owner}/{repository}/releases?per_page=20");

    /// <summary>
    /// Lee una respuesta de GitHub: una lista de publicaciones o una sola. De una lista se queda con
    /// la de número más alto que se pueda usar; que merezca ofrecerse lo decide después
    /// <see cref="UpdateCheckPolicy"/>.
    /// </summary>
    public static ReleaseAssets Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return ReleaseAssets.Rejected("La consulta de versiones no devolvió nada.");
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                return ReadBest(root);
            }

            if (root.ValueKind != JsonValueKind.Object)
            {
                return ReleaseAssets.Rejected("La respuesta de versiones no tiene la forma esperada.");
            }

            return ReadOne(root);
        }
        catch (JsonException)
        {
            // Una página de error, una redirección o un límite de peticiones llegan como algo que
            // no es JSON. Es un martes normal, no una excepción que deba subir.
            return ReleaseAssets.Rejected("La respuesta de versiones no se pudo entender.");
        }
    }

    /// <summary>Lee una sola publicación. Devuelve rechazo si le falta algo imprescindible.</summary>
    private static ReleaseAssets ReadOne(JsonElement root)
    {
        // Un borrador no está publicado: ofrecerlo sería instalar algo que ni siquiera se ha
        // terminado de subir.
        if (root.TryGetProperty("draft", out var draft) &&
            draft.ValueKind == JsonValueKind.True)
        {
            return ReleaseAssets.Rejected("La última publicación es un borrador.");
        }

        var version = ReadString(root, "tag_name");
        if (version.Length == 0)
        {
            return ReleaseAssets.Rejected("La publicación no dice qué versión es.");
        }

        if (!root.TryGetProperty("assets", out var assets) ||
            assets.ValueKind != JsonValueKind.Array)
        {
            return ReleaseAssets.Rejected("La publicación no trae ningún archivo.");
        }

        // Lo que importa no es el orden en que GitHub devuelva los adjuntos, es el emparejamiento:
        // cada publicación lleva cuatro (instalador, zip portable y la huella de cada uno), y antes
        // se tomaba «el último .zip» y «el último .sha256» por separado. Acertaba solo porque
        // «Setup» ordena antes que «win»; con otro orden, o con un segundo .zip, se habría bajado el
        // paquete equivocado o comprobado con la huella de otro archivo, y nadie habría podido
        // actualizarse (con un mensaje que hasta sugería que alguien manipuló la descarga).
        //
        // Primera pasada: el paquete es el .zip más grande. El de Sakura son cien megas y
        // cualquier otro zip que llegue será menor; con un empate se queda el primero.
        var packageUrl = string.Empty;
        var packageName = string.Empty;
        long packageSize = 0;

        foreach (var asset in assets.EnumerateArray())
        {
            var name = ReadString(asset, "name");
            var url = ReadString(asset, "browser_download_url");

            if (name.Length == 0 || url.Length == 0 ||
                !name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var bytes = asset.TryGetProperty("size", out var size) && size.TryGetInt64(out var value)
                ? value
                : 0;

            if (packageUrl.Length == 0 || bytes > packageSize)
            {
                packageUrl = url;
                packageName = name;
                packageSize = bytes;
            }
        }

        if (packageUrl.Length == 0)
        {
            return ReleaseAssets.Rejected("La publicación no trae el paquete de Sakura.");
        }

        // Segunda pasada: la huella es la que se llama exactamente como el paquete más «.sha256».
        // Nada de «termina en .sha256»: eso es lo que dejaba pasar la huella de otro archivo.
        var checksumUrl = string.Empty;
        var checksumName = packageName + ".sha256";

        foreach (var asset in assets.EnumerateArray())
        {
            if (string.Equals(ReadString(asset, "name"), checksumName, StringComparison.OrdinalIgnoreCase))
            {
                checksumUrl = ReadString(asset, "browser_download_url");
                if (checksumUrl.Length > 0)
                {
                    break;
                }
            }
        }

        if (checksumUrl.Length == 0)
        {
            // Sin huella no se instala. Una publicación a la que se le olvidó subir el .sha256
            // no es una publicación de la que fiarse.
            return ReleaseAssets.Rejected("La publicación no trae la huella para comprobarla.");
        }

        return new ReleaseAssets(
            IsUsable: true,
            Version: version,
            PackageUrl: packageUrl,
            PackageSize: packageSize,
            ChecksumUrl: checksumUrl,
            Notes: ReadString(root, "body"),
            Problem: string.Empty);
    }

    /// <summary>
    /// De una lista, la publicación usable con el número más alto.
    ///
    /// Se comparan versiones y no fechas: una corrección publicada hoy sobre una rama vieja es más
    /// reciente en el tiempo y más antigua en número, y lo que se quiere instalar es lo segundo.
    /// </summary>
    private static ReleaseAssets ReadBest(JsonElement releases)
    {
        var best = ReleaseAssets.Rejected("No hay ninguna publicación que se pueda usar.");
        SakuraVersion? bestVersion = null;

        foreach (var release in releases.EnumerateArray())
        {
            if (release.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var candidate = ReadOne(release);
            if (!candidate.IsUsable || !SakuraVersion.TryParse(candidate.Version, out var version))
            {
                continue;
            }

            if (bestVersion is null || version > bestVersion.Value)
            {
                best = candidate;
                bestVersion = version;
            }
        }

        return best;
    }

    /// <summary>
    /// Saca la huella del archivo <c>.sha256</c>.
    ///
    /// El formato habitual es «huella  nombre-del-archivo», que es lo que escriben tanto
    /// <c>sha256sum</c> como el script de publicación. Se toma la primera palabra y se ignora el
    /// resto: el nombre que venga detrás no aporta nada que no se sepa ya.
    /// </summary>
    public static string ReadChecksum(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var first = content
            .Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?
            .Trim();

        if (string.IsNullOrEmpty(first))
        {
            return string.Empty;
        }

        var space = first.IndexOfAny([' ', '\t']);
        return space > 0 ? first[..space] : first;
    }

    private static string ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim() ?? string.Empty
            : string.Empty;
}
