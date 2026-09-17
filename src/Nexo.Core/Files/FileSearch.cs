namespace Nexo.Core.Files;

public sealed record FileSearchResult(string Name, string FullPath, string Folder, DateTimeOffset Modified);

/// <summary>Busca archivos del usuario por nombre o contenido, con el índice de Windows.</summary>
public interface IFileSearchService
{
    Task<IReadOnlyList<FileSearchResult>> SearchAsync(string query, int maximum, CancellationToken cancellationToken);
}

/// <summary>
/// 2026-09-16 — buscar archivos desde Sakura (Alt+Shift+F). Todo ocurre en el equipo: se consulta el
/// índice que Windows ya mantiene, sin recorrer el disco ni mandar nada fuera.
/// </summary>
public static class FileSearchPolicy
{
    public const int MinimumQueryLength = 2;
    public const int MaximumResults = 12;

    /// <summary>Las carpetas donde suele estar lo de uno; el resto del disco es ruido (programas, sistema).</summary>
    public static IReadOnlyList<string> Roots(Func<Environment.SpecialFolder, string> resolve, string userProfile) =>
        new[]
        {
            resolve(Environment.SpecialFolder.MyDocuments),
            resolve(Environment.SpecialFolder.Desktop),
            Path.Combine(userProfile, "Downloads"),
            resolve(Environment.SpecialFolder.MyPictures),
            resolve(Environment.SpecialFolder.MyVideos),
            resolve(Environment.SpecialFolder.MyMusic)
        }
        .Where(path => !string.IsNullOrWhiteSpace(path))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    public static string Normalize(string? query) =>
        string.Join(' ', (query ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static bool CanSearch(string? query) => Normalize(query).Length >= MinimumQueryLength;

    /// <summary>
    /// Primero lo que tiene la búsqueda en el nombre, después lo más reciente. Sin duplicados.
    /// </summary>
    public static IReadOnlyList<FileSearchResult> Rank(IEnumerable<FileSearchResult> results, string query, int maximum)
    {
        var normalized = Normalize(query);
        return results
            .DistinctBy(result => result.FullPath, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(result => result.Name.Contains(normalized, StringComparison.CurrentCultureIgnoreCase))
            .ThenByDescending(result => result.Modified)
            .Take(maximum)
            .ToList();
    }

    /// <summary>«hace 3 días», «hoy», «el 2 de mayo de 2025».</summary>
    public static string When(DateTimeOffset modified, DateTimeOffset now)
    {
        var days = (now.Date - modified.Date).Days;
        return days switch
        {
            <= 0 => "hoy",
            1 => "ayer",
            < 7 => $"hace {days} días",
            _ => modified.ToString(
                modified.Year == now.Year ? "d 'de' MMMM" : "d 'de' MMMM 'de' yyyy",
                System.Globalization.CultureInfo.GetCultureInfo("es-MX"))
        };
    }
}
