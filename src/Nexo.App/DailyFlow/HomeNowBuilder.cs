using System.Globalization;
using Nexo.Core.Focus;
using Nexo.Core.Tasks;

namespace Nexo.App.DailyFlow;

/// <summary>Qué ocupa la burbuja «Ahora».</summary>
public enum HomeNowKind
{
    /// <summary>La próxima tarea importante: se puede empezar o dejar para mañana.</summary>
    Task,

    /// <summary>Una sesión de enfoque en marcha o en pausa.</summary>
    Focus,

    /// <summary>No hay nada importante para hoy.</summary>
    Empty
}

/// <summary>Lo que dice la burbuja «Ahora» y a qué tarea apunta.</summary>
public sealed record HomeNow(
    HomeNowKind Kind,
    string Title,
    string Detail,
    Guid? TaskId = null,
    bool IsPaused = false,
    bool IsImportant = false);

/// <summary>Una línea del hilo de ayer: la hora y lo que pasó.</summary>
public sealed record HomeYesterdayItem(string Time, string Text);

/// <summary>El resumen local de ayer. Nulo si ayer no quedó nada registrado.</summary>
public sealed record HomeYesterday(int TasksDone, int FocusMinutes, IReadOnlyList<HomeYesterdayItem> Items)
{
    /// <summary>La frase corta de la burbuja: «4 tareas · 1 h 10 min de enfoque».</summary>
    public string Headline
    {
        get
        {
            var parts = new List<string>();
            if (TasksDone > 0)
            {
                parts.Add(TasksDone == 1 ? "1 tarea" : $"{TasksDone} tareas");
            }

            if (FocusMinutes > 0)
            {
                parts.Add(HomeNowBuilder.Minutes(FocusMinutes) + " de enfoque");
            }

            return string.Join(" · ", parts);
        }
    }
}

/// <summary>Todo lo que enseña el Inicio nuevo.</summary>
public sealed record HomeTodayModel(
    string Greeting,
    string DateLine,
    HomeNow Now,
    int DoneToday,
    int PlannedToday,
    int FocusMinutesToday,
    HomeYesterday? Yesterday,
    IReadOnlyList<HomeReviewItem>? Review = null);

/// <summary>Una tarea que quedó pendiente de días anteriores, para el repaso de la mañana.</summary>
public sealed record HomeReviewItem(Guid TaskId, string Title, string Detail);

/// <summary>
/// 2026-09-16 — el Inicio nuevo (dirección «Burbujas» con la calma de «Calma», elegida por Adler).
///
/// Inicio deja de ser un tablero de cuatro tarjetas que solo informan y responde una pregunta: ¿qué
/// hago ahora? La investigación de productividad (docs/research) lo resume en que lo que no se
/// convierte en una acción no se usa, y que lo que se acumula en rojo hace abandonar.
///
/// Por eso aquí no hay contador de vencidas: una tarea que se pasó de fecha sigue siendo candidata a
/// «Ahora», pero se dice «quedó pendiente», no «vencida». Y el resumen de ayer es solo lo que ya está
/// en el equipo —tareas hechas y enfoque—; los chats no se vuelven a mandar a nadie (decidido con
/// Adler).
///
/// Es lógica pura, sin WPF, para poder probarla con los managers de verdad.
/// </summary>
public static class HomeNowBuilder
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-MX");

    public static HomeTodayModel Build(TaskManager taskManager, FocusManager focusManager, string? name, DateTimeOffset now, bool includeReview = false)
    {
        ArgumentNullException.ThrowIfNull(taskManager);
        ArgumentNullException.ThrowIfNull(focusManager);

        var today = now.Date;
        var tasks = taskManager.GetAll();
        var pending = tasks.Where(task => !task.IsCompleted).ToList();
        var doneToday = tasks.Count(task => task.CompletedAt is { } done && done.Date == today);
        var candidates = Candidates(pending, today);

        var focus = FocusDisplayStateBuilder.Build(
            focusManager,
            now,
            taskId => tasks.FirstOrDefault(task => task.Id == taskId)?.Title);

        HomeNow current;
        if (focus.HasSession)
        {
            var label = string.IsNullOrWhiteSpace(focus.AssociatedLabel) ? "Sesión de enfoque" : focus.AssociatedLabel!;
            current = new HomeNow(
                HomeNowKind.Focus,
                label,
                focus.IsPaused ? $"En pausa · quedan {focus.ClockText}" : $"Enfocándote · quedan {focus.ClockText}",
                IsPaused: focus.IsPaused);
        }
        else if (candidates.FirstOrDefault() is { } next)
        {
            current = new HomeNow(HomeNowKind.Task, next.Title, Describe(next, now), next.Id, IsImportant: next.Priority == TaskPriority.High);
        }
        else if (tasks.Count == 0)
        {
            // Instalación nueva: nada que resumir ni recordar todavía. Se invita con un ejemplo.
            current = new HomeNow(
                HomeNowKind.Empty,
                "Tu día empieza aquí",
                "Toca y apunta algo, como «entregar el ensayo el viernes»");
        }
        else
        {
            current = new HomeNow(
                HomeNowKind.Empty,
                "Nada importante para hoy",
                pending.Count > 0 ? "Toca para planear el día" : "Toca para añadir una tarea");
        }

        var greeting = now.Hour switch
        {
            < 6 => "Buenas noches",
            < 12 => "Buenos días",
            < 19 => "Buenas tardes",
            _ => "Buenas noches"
        };

        if (!string.IsNullOrWhiteSpace(name))
        {
            greeting += ", " + name.Trim();
        }

        return new HomeTodayModel(
            greeting,
            now.ToString("dddd, d 'de' MMMM", Spanish),
            current,
            doneToday,
            doneToday + candidates.Count,
            focus.FocusMinutesToday,
            Yesterday(tasks, focusManager.GetHistory(), today.AddDays(-1)),
            includeReview ? Review(pending, today) : null);
    }

    /// <summary>
    /// 2026-09-16 — el repaso de la mañana: lo que quedó de días anteriores, para decidir con un
    /// toque si va hoy, mañana o se suelta. Sin esto lo atrasado se acumula en silencio, que es lo
    /// que la investigación señala como la causa de abandonar una lista.
    /// </summary>
    private static IReadOnlyList<HomeReviewItem> Review(IEnumerable<NexoTask> pending, DateTime today) =>
        pending
            .Where(task => task.DueAt is { } due && due.Date < today)
            .OrderBy(task => task.DueAt)
            .Take(5)
            .Select(task => new HomeReviewItem(
                task.Id,
                task.Title,
                (today - task.DueAt!.Value.Date).Days == 1 ? "Era para ayer" : $"Era para el {task.DueAt.Value.ToString("dddd d", Spanish)}"))
            .ToList();

    /// <summary>
    /// Lo que cuenta como «de hoy»: lo importante, lo que vence hoy y lo que quedó pendiente de días
    /// anteriores. Primero lo importante; dentro de eso, lo que vence antes.
    /// </summary>
    private static List<NexoTask> Candidates(IEnumerable<NexoTask> pending, DateTime today) =>
        pending
            .Where(task => task.Priority == TaskPriority.High || (task.DueAt is { } due && due.Date <= today))
            .OrderByDescending(task => task.Priority)
            .ThenBy(task => task.DueAt ?? DateTimeOffset.MaxValue)
            .ThenBy(task => task.CreatedAt)
            .ToList();

    private static string Describe(NexoTask task, DateTimeOffset now)
    {
        var important = task.Priority == TaskPriority.High ? "Importante" : null;
        string? when = null;

        if (task.DueAt is { } due)
        {
            var day = due.Date;
            var hasTime = due.TimeOfDay != TimeSpan.Zero;
            var time = hasTime ? " a las " + TodayPlan.Clock(due) : string.Empty;

            when = day < now.Date
                ? "quedó pendiente"
                : day == now.Date
                    ? "hoy" + time
                    : day == now.Date.AddDays(1)
                        ? "mañana" + time
                        : day < now.Date.AddDays(7)
                            ? "vence el " + due.ToString("dddd", Spanish) + time
                            : "vence el " + due.ToString("d 'de' MMMM", Spanish);
        }

        var parts = new[] { important, when }.Where(part => !string.IsNullOrEmpty(part)).ToList();
        if (parts.Count == 0)
        {
            return "Pendiente";
        }

        var text = string.Join(" · ", parts);
        return char.ToUpper(text[0], Spanish) + text[1..];
    }

    private static HomeYesterday? Yesterday(IEnumerable<NexoTask> tasks, IEnumerable<FocusHistoryEntry> history, DateTime yesterday)
    {
        var items = new List<(DateTimeOffset At, string Text)>();
        var tasksDone = 0;
        foreach (var task in tasks)
        {
            if (task.CompletedAt is { } done && done.Date == yesterday)
            {
                tasksDone++;
                items.Add((done, task.Title));
            }
        }

        var focusMinutes = 0d;
        foreach (var entry in history)
        {
            if (entry.CompletedAt.Date != yesterday)
            {
                continue;
            }

            focusMinutes += entry.Duration.TotalMinutes;
            items.Add((entry.StartedAt, "Enfoque de " + Minutes((int)Math.Round(entry.Duration.TotalMinutes))));
        }

        if (items.Count == 0)
        {
            return null;
        }

        return new HomeYesterday(
            tasksDone,
            (int)Math.Round(focusMinutes),
            items.OrderBy(item => item.At)
                .Select(item => new HomeYesterdayItem(TodayPlan.Clock(item.At), item.Text))
                .ToList());
    }

    /// <summary>«45 min», «1 h», «1 h 10 min».</summary>
    public static string Minutes(int minutes)
    {
        if (minutes < 60)
        {
            return $"{minutes} min";
        }

        var hours = minutes / 60;
        var rest = minutes % 60;
        return rest == 0 ? $"{hours} h" : $"{hours} h {rest} min";
    }
}
