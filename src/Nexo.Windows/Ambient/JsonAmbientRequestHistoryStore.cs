using System.Text.Json;
using Nexo.Core.Ambient;
using Nexo.Core.Diagnostics;
using Nexo.Windows.Storage;

namespace Nexo.Windows.Ambient;

public sealed class JsonAmbientRequestHistoryStore : IAmbientRequestHistoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly object _sync = new();
    private readonly string _filePath;

    public JsonAmbientRequestHistoryStore(string? filePath = null)
    {
        _filePath = filePath ?? NexoDataPaths.AmbientRequests;
    }

    public AmbientRequestState Load()
    {
        lock (_sync)
        {
            if (!File.Exists(_filePath))
            {
                return new AmbientRequestState();
            }

            try
            {
                var json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<AmbientRequestState>(json, JsonOptions)
                    ?? new AmbientRequestState();
            }
            catch (JsonException)
            {
                CorruptFileBackup.TryPreserve(_filePath);
                return new AmbientRequestState();
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                // No se pudo abrir: no se aparta un archivo que puede estar sano.
                return new AmbientRequestState();
            }
        }
    }

    public void Save(AmbientRequestState state)
    {
        lock (_sync)
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temporaryPath = _filePath + ".tmp";
            var json = JsonSerializer.Serialize(state, JsonOptions);
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, _filePath, overwrite: true);
        }
    }
}
