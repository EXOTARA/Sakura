namespace Nexo.Core.Habits;

public sealed class Habit
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public List<DateOnly> DoneDays { get; set; } = [];

    public Habit Copy() => new() { Id = Id, Name = Name, CreatedAt = CreatedAt, DoneDays = [.. DoneDays] };
}

public interface IHabitStore
{
    IReadOnlyList<Habit> Load();

    void Save(IReadOnlyCollection<Habit> habits);
}

public sealed record HabitDay(Guid Id, string Name, bool DoneToday, int Streak);

/// <summary>
/// 2026-09-16 — hábitos, un módulo que se activa aparte y llega apagado (Adler no los usaría).
///
/// Sin castigo: la racha no se rompe por no haber marcado todavía hoy (cuenta hasta ayer), no hay
/// rojo ni recordatorios insistentes, y desmarcar un día es tan fácil como marcarlo. La
/// investigación de productividad señala la culpa por la racha perdida como la razón más común para
/// abandonar estas listas.
/// </summary>
public sealed class HabitManager
{
    public const int MaximumHabits = 8;

    private readonly object _sync = new();
    private readonly IHabitStore _store;
    private readonly List<Habit> _habits = [];

    public HabitManager(IHabitStore store)
    {
        _store = store;
    }

    public void Load()
    {
        lock (_sync)
        {
            _habits.Clear();
            _habits.AddRange(_store.Load().Where(habit => !string.IsNullOrWhiteSpace(habit.Name)));
        }
    }

    public IReadOnlyList<HabitDay> Today(DateOnly today)
    {
        lock (_sync)
        {
            return _habits
                .OrderBy(habit => habit.CreatedAt)
                .Select(habit => new HabitDay(habit.Id, habit.Name, habit.DoneDays.Contains(today), Streak(habit.DoneDays, today)))
                .ToList();
        }
    }

    public bool Add(string name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        lock (_sync)
        {
            if (trimmed.Length == 0 ||
                _habits.Count >= MaximumHabits ||
                _habits.Any(habit => habit.Name.Equals(trimmed, StringComparison.CurrentCultureIgnoreCase)))
            {
                return false;
            }

            _habits.Add(new Habit { Name = trimmed });
            Save();
            return true;
        }
    }

    public void Remove(Guid id)
    {
        lock (_sync)
        {
            if (_habits.RemoveAll(habit => habit.Id == id) > 0)
            {
                Save();
            }
        }
    }

    public void Toggle(Guid id, DateOnly day)
    {
        lock (_sync)
        {
            var habit = _habits.FirstOrDefault(candidate => candidate.Id == id);
            if (habit is null)
            {
                return;
            }

            if (!habit.DoneDays.Remove(day))
            {
                habit.DoneDays.Add(day);
            }

            // Solo hace falta lo reciente para la racha; así el archivo no crece sin fin.
            habit.DoneDays = habit.DoneDays.Where(done => done > day.AddDays(-400)).Distinct().Order().ToList();
            Save();
        }
    }

    /// <summary>Días seguidos hasta hoy, o hasta ayer si hoy aún no se marcó.</summary>
    public static int Streak(IReadOnlyCollection<DateOnly> done, DateOnly today)
    {
        var set = done.ToHashSet();
        var cursor = set.Contains(today) ? today : today.AddDays(-1);
        var streak = 0;
        while (set.Contains(cursor))
        {
            streak++;
            cursor = cursor.AddDays(-1);
        }

        return streak;
    }

    private void Save() => _store.Save(_habits.Select(habit => habit.Copy()).ToArray());
}
