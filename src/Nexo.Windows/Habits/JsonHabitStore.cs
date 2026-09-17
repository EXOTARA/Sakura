using System.Text.Json;
using Nexo.Core.Diagnostics;
using Nexo.Core.Habits;

namespace Nexo.Windows.Habits;

/// <summary>2026-09-16 — los hábitos en <c>habits.json</c>, con el mismo cuidado que las tareas.</summary>
public sealed class JsonHabitStore : IHabitStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly object _sync = new();
    private readonly string _filePath;

    public JsonHabitStore(string? filePath = null)
    {
        _filePath = filePath ?? NexoDataPaths.Habits;
    }

    public IReadOnlyList<Habit> Load()
    {
        lock (_sync)
        {
            if (!File.Exists(_filePath))
            {
                return [];
            }

            try
            {
                return JsonSerializer.Deserialize<List<Habit>>(File.ReadAllText(_filePath), JsonOptions) ?? [];
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                try
                {
                    File.Move(_filePath, _filePath + $".corrupt-{DateTime.Now:yyyyMMdd-HHmmss}", overwrite: true);
                }
                catch (Exception moveException) when (moveException is IOException or UnauthorizedAccessException)
                {
                }

                return [];
            }
        }
    }

    public void Save(IReadOnlyCollection<Habit> habits)
    {
        lock (_sync)
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temporaryPath = _filePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(habits, JsonOptions));
            File.Move(temporaryPath, _filePath, overwrite: true);
        }
    }
}
