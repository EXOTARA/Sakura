using System.Globalization;

namespace Nexo.Core.Tasks;

public enum TodaySectionKind
{
    Important,
    Day,
    Later,
    Done
}

public sealed record TodayItem(NexoTask Task, string When, bool LeftPending);

public sealed record TodaySection(TodaySectionKind Kind, string Title, IReadOnlyList<TodayItem> Items);

/// <summary>
/// 2026-09-16 — Hoy como centro del día.
///
/// Hoy empieza vacío: solo entra lo que es de ese día. Lo importante va arriba, con un máximo de tres
/// (la investigación: con más de tres «importantes», nada lo es). Lo atrasado sigue en Hoy, pero dice
/// «Quedó pendiente», no «vencida», y no hay contador rojo. Lo sin fecha y lo de otros días queda en
/// «Luego», plegado. Las tareas soltadas no aparecen en ninguna parte.
///
/// Sirve para cualquier día: el calendario del panel de arriba abre Hoy en el día que se toque.
/// </summary>
public static class TodayPlan
{
    public const int MaximumImportant = 3;

    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-MX");

    public static IReadOnlyList<TodaySection> Build(IEnumerable<NexoTask> tasks, DateOnly day, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(tasks);

        var today = DateOnly.FromDateTime(now.Date);
        var isToday = day == today;
        var all = tasks.Where(task => task.ArchivedAt is null).ToList();

        var pending = all.Where(task => !task.IsCompleted).ToList();
        var ofDay = pending
            .Where(task => task.DueAt is { } due && (isToday ? Day(due) <= day : Day(due) == day))
            .ToList();

        // Lo importante sin fecha también es de hoy; en otro día solo cuenta lo que vence ese día.
        var important = pending
            .Where(task => task.Priority == TaskPriority.High && (ofDay.Contains(task) || (isToday && task.DueAt is null)))
            .OrderBy(task => task.DueAt ?? DateTimeOffset.MaxValue)
            .ThenBy(task => task.CreatedAt)
            .Take(MaximumImportant)
            .ToList();

        var dayItems = ofDay
            .Except(important)
            .OrderByDescending(task => task.Priority)
            .ThenBy(task => task.DueAt)
            .ThenBy(task => task.CreatedAt)
            .ToList();

        var later = pending
            .Except(important)
            .Except(dayItems)
            .OrderBy(task => task.DueAt ?? DateTimeOffset.MaxValue)
            .ThenBy(task => task.CreatedAt)
            .ToList();

        var done = all
            .Where(task => task.CompletedAt is { } completed && Day(completed) == day)
            .OrderByDescending(task => task.CompletedAt)
            .ToList();

        var sections = new List<TodaySection>();
        Add(sections, TodaySectionKind.Important, "Importantes", important, now);
        Add(sections, TodaySectionKind.Day, isToday ? "Hoy" : Capitalize(day.ToString("dddd d 'de' MMMM", Spanish)), dayItems, now);
        if (isToday)
        {
            Add(sections, TodaySectionKind.Later, "Luego", later, now);
        }

        Add(sections, TodaySectionKind.Done, isToday ? "Hechas hoy" : "Hechas ese día", done, now);
        return sections;
    }

    /// <summary>Días con algo pendiente, para marcarlos en el calendario.</summary>
    public static IReadOnlySet<DateOnly> BusyDays(IEnumerable<NexoTask> tasks) =>
        tasks
            .Where(task => task.ArchivedAt is null && !task.IsCompleted && task.DueAt.HasValue)
            .Select(task => Day(task.DueAt!.Value))
            .ToHashSet();

    /// <summary>«Hoy · 17:00», «Quedó pendiente», «Mañana», «vie 18 sep», «Sin fecha», «Hecha 11:20».</summary>
    public static string Describe(NexoTask task, DateTimeOffset now)
    {
        if (task.CompletedAt is { } completed)
        {
            return "Hecha a las " + Clock(completed);
        }

        if (task.DueAt is not { } due)
        {
            return "Sin fecha";
        }

        var today = DateOnly.FromDateTime(now.Date);
        var day = Day(due);
        var time = due.TimeOfDay == TimeSpan.Zero ? string.Empty : " · " + Clock(due);

        if (day < today)
        {
            return "Quedó pendiente";
        }

        if (day == today)
        {
            return "Hoy" + time;
        }

        if (day == today.AddDays(1))
        {
            return "Mañana" + time;
        }

        return Capitalize(due.ToString("ddd d MMM", Spanish).Replace(".", string.Empty)) + time;
    }

    /// <summary>
    /// La hora como se dice: «5:00 pm», «9:30 am». Adler pidió el formato de 12 horas con am/pm en
    /// lugar del de 24.
    /// </summary>
    public static string Clock(DateTimeOffset value)
    {
        var hour = value.Hour % 12 == 0 ? 12 : value.Hour % 12;
        return $"{hour}:{value.Minute:00} {(value.Hour < 12 ? "am" : "pm")}";
    }

    private static void Add(List<TodaySection> sections, TodaySectionKind kind, string title, List<NexoTask> tasks, DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(now.Date);
        sections.Add(new TodaySection(
            kind,
            title,
            tasks.Select(task => new TodayItem(
                task,
                Describe(task, now),
                !task.IsCompleted && task.DueAt is { } due && Day(due) < today)).ToList()));
    }

    private static DateOnly Day(DateTimeOffset value) => DateOnly.FromDateTime(value.Date);

    private static string Capitalize(string text) =>
        text.Length == 0 ? text : char.ToUpper(text[0], Spanish) + text[1..];
}
