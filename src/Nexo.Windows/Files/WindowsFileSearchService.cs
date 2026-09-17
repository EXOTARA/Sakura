using Nexo.Core.Files;
using Windows.Storage;
using Windows.Storage.Search;

namespace Nexo.Windows.Files;

/// <summary>
/// 2026-09-16 — consulta el índice de búsqueda de Windows con las API de Windows.Storage
/// (<see cref="IndexerOption.UseIndexerWhenAvailable"/>): es lo mismo que usa el Explorador, así que
/// encuentra por nombre y por contenido sin recorrer el disco. Si una carpeta no está indexada, esa
/// carpeta simplemente aporta menos; no se bloquea la búsqueda.
/// </summary>
public sealed class WindowsFileSearchService : IFileSearchService
{
    private readonly IReadOnlyList<string> _roots;

    public WindowsFileSearchService(IReadOnlyList<string>? roots = null)
    {
        _roots = roots ?? FileSearchPolicy.Roots(
            Environment.GetFolderPath,
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
    }

    public async Task<IReadOnlyList<FileSearchResult>> SearchAsync(string query, int maximum, CancellationToken cancellationToken)
    {
        var normalized = FileSearchPolicy.Normalize(query);
        if (!FileSearchPolicy.CanSearch(normalized))
        {
            return [];
        }

        var searches = _roots
            .Where(Directory.Exists)
            .Select(root => SearchRootAsync(root, normalized, maximum, cancellationToken));
        var found = await Task.WhenAll(searches).ConfigureAwait(false);
        return FileSearchPolicy.Rank(found.SelectMany(results => results), normalized, maximum);
    }

    private static async Task<IReadOnlyList<FileSearchResult>> SearchRootAsync(
        string root, string query, int maximum, CancellationToken cancellationToken)
    {
        try
        {
            var folder = await StorageFolder.GetFolderFromPathAsync(root).AsTask(cancellationToken).ConfigureAwait(false);
            var options = new QueryOptions(CommonFileQuery.OrderBySearchRank, null)
            {
                FolderDepth = FolderDepth.Deep,
                IndexerOption = IndexerOption.UseIndexerWhenAvailable,
                UserSearchFilter = query
            };
            options.SetPropertyPrefetch(global::Windows.Storage.FileProperties.PropertyPrefetchOptions.BasicProperties, null);

            var files = await folder.CreateFileQueryWithOptions(options)
                .GetFilesAsync(0, (uint)maximum)
                .AsTask(cancellationToken)
                .ConfigureAwait(false);

            var results = new List<FileSearchResult>(files.Count);
            foreach (var file in files)
            {
                var properties = await file.GetBasicPropertiesAsync().AsTask(cancellationToken).ConfigureAwait(false);
                results.Add(new FileSearchResult(
                    file.Name,
                    file.Path,
                    Path.GetDirectoryName(file.Path) ?? root,
                    properties.DateModified));
            }

            return results;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or ArgumentException or System.Runtime.InteropServices.COMException)
        {
            return [];
        }
    }
}
