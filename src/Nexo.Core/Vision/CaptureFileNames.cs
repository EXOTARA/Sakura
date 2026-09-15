using System.Globalization;

namespace Nexo.Core.Vision;

/// <summary>
/// 2026-09-15 — dónde y con qué nombre se guardan las capturas y las grabaciones de pantalla. En una
/// carpeta «Sakura» dentro de Imágenes o de Vídeos, con la fecha y la hora en el nombre para que se
/// ordenen solas y se reconozcan sin abrirlas. Nunca se sobrescribe un archivo: si ya existe uno con
/// ese nombre —dos capturas en el mismo segundo—, se le añade un número.
/// </summary>
public static class CaptureFileNames
{
    public const string FolderName = "Sakura";

    public static string ScreenshotPath(string picturesFolder, DateTimeOffset now, Func<string, bool> exists) =>
        Unique(Path.Combine(picturesFolder, FolderName), "Captura " + Stamp(now), ".png", exists);

    public static string RecordingPath(string videosFolder, DateTimeOffset now, Func<string, bool> exists) =>
        Unique(Path.Combine(videosFolder, FolderName), "Grabación " + Stamp(now), ".mp4", exists);

    private static string Stamp(DateTimeOffset now) =>
        now.ToString("yyyy-MM-dd HH-mm-ss", CultureInfo.InvariantCulture);

    private static string Unique(string folder, string name, string extension, Func<string, bool> exists)
    {
        ArgumentNullException.ThrowIfNull(exists);

        var path = Path.Combine(folder, name + extension);
        for (var number = 2; exists(path); number++)
        {
            path = Path.Combine(folder, $"{name} ({number}){extension}");
        }

        return path;
    }
}
