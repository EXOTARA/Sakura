using System.Text.Json;
using Nexo.Core.Diagnostics;
using Nexo.Core.Storage;
using Nexo.Windows.Storage;
using Nexo.Core.Tasks;

namespace Nexo.Windows.Tasks;

public sealed class JsonTaskStore : ITaskStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly object _sync = new();
    private readonly string _filePath;

    public JsonTaskStore(string? filePath = null)
    {
        _filePath = filePath ?? NexoDataPaths.Tasks;
    }

    /// <inheritdoc />
    public DataLoadOutcome LastLoad { get; private set; } = DataLoadOutcome.Ok;

    public IReadOnlyList<NexoTask> Load()
    {
        lock (_sync)
        {
            if (!File.Exists(_filePath))
            {
                LastLoad = DataLoadOutcome.Missing;
                return [];
            }

            // Tres intentos con 150 ms: un antivirus o un cliente de sincronización suelta el
            // archivo en menos que eso, y no merece que la persona vea un aviso por un bloqueo de
            // un instante.
            const int attempts = 3;
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    var json = File.ReadAllText(_filePath);
                    var loaded = JsonSerializer.Deserialize<List<NexoTask>>(json, JsonOptions);

                    // JSON válido de forma inesperada ([null]) pasaba la lectura y tumbaba el
                    // arranque en cada intento: se trata como dañado, por el mismo camino.
                    if (loaded is not null && loaded.Any(task => task is null))
                    {
                        throw new JsonException("La lista contiene un elemento vacío.");
                    }

                    LastLoad = DataLoadOutcome.Ok;
                    return loaded ?? [];
                }
                catch (JsonException exception)
                {
                    // Dañado: se aparta para que un archivo roto no se quede estorbando, y se
                    // dice dónde quedó. Devolver vacío es lo correcto aquí; lo que impide pisarlo
                    // es que LastLoad no sea seguro de sobrescribir.
                    var preserved = CorruptFileBackup.TryPreserve(_filePath);
                    LastLoad = DataLoadOutcome.Corrupt(exception.Message, preserved);
                    return [];
                }
                catch (Exception exception) when (
                    exception is IOException or UnauthorizedAccessException)
                {
                    if (attempt < attempts)
                    {
                        Thread.Sleep(150);
                        continue;
                    }

                    // No legible: NO se aparta ni se renombra nada. Medido el 2026-09-18: con el
                    // archivo bloqueado en exclusiva, el renombrado falla igual, y si el bloqueo
                    // fuera de otro tipo apartaría un archivo sano. El archivo se queda donde está.
                    LastLoad = DataLoadOutcome.Unreadable(SakuraDataWriteException.ReasonFor(exception));
                    return [];
                }
            }
        }
    }

    public void Save(IReadOnlyCollection<NexoTask> tasks)
    {
        lock (_sync)
        {
            var temporaryPath = _filePath + ".tmp";
            var json = JsonSerializer.Serialize(tasks, JsonOptions);
            try
            {
                // El archivo real solo se toca cuando el temporal ya está completo: si esto falla,
                // lo que había sigue intacto (comprobado el 2026-09-18).
                var directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(temporaryPath, json);
                File.Move(temporaryPath, _filePath, overwrite: true);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                // Se relanza como excepción propia en lugar de tragarla: quien decide qué hacer
                // (avisar, seguir en memoria) es el manager.
                throw new SakuraDataWriteException(
                    _filePath, SakuraDataWriteException.ReasonFor(exception), exception);
            }
        }
    }
}
