namespace Nexo.Core.Ai;

/// <summary>
/// 2026-09-16 — ¿cabe la IA local? Medido en una instalación desde cero: el motor descarga 1,45 GB,
/// se descomprime a 1,85 GB y, mientras tanto, el zip y la copia descomprimida conviven (pico de
/// ~5 GB); el modelo recomendado ocupa 3,2 GB. Se deja margen para no llenar el disco del todo.
/// </summary>
public static class LocalAiDiskPolicy
{
    private const long Gigabyte = 1024L * 1024 * 1024;

    /// <summary>Espacio libre necesario antes de empezar.</summary>
    public static long RequiredBytes(bool engineInstalled) =>
        engineInstalled ? 4 * Gigabyte : 9 * Gigabyte;

    /// <summary>Null si cabe; si no, el aviso para la persona.</summary>
    public static string? Check(long freeBytes, bool engineInstalled)
    {
        var required = RequiredBytes(engineInstalled);
        if (freeBytes >= required)
        {
            return null;
        }

        return $"No hay espacio suficiente: la IA local necesita unos {required / Gigabyte} GB libres y quedan " +
               $"{Math.Max(0, freeBytes) / (double)Gigabyte:0.#} GB. Libera espacio o usa un proveedor en la nube.";
    }
}
