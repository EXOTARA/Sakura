using Nexo.Core.Storage;

namespace Nexo.Core.Focus;

public sealed class FocusManager
{
    private static readonly TimeSpan MinimumDuration = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaximumDuration = TimeSpan.FromHours(24);

    private readonly object _sync = new();
    private readonly IFocusStore _store;
    private FocusState _state = new();
    private bool _persistenceSuspended;
    private bool _writeFailing;

    public FocusManager(IFocusStore store)
    {
        _store = store;
    }

    /// <summary>Cómo salió la última lectura del archivo. Ver <see cref="Tasks.TaskManager.LoadOutcome"/>.</summary>
    public DataLoadOutcome LoadOutcome { get; private set; } = DataLoadOutcome.Ok;

    /// <summary>
    /// Verdadero cuando el archivo existía pero no se pudo leer: Enfoque sigue funcionando en memoria
    /// y no se guarda nada, para no escribir un historial vacío encima del que la persona tenía.
    /// </summary>
    public bool IsPersistenceSuspended
    {
        get { lock (_sync) { return _persistenceSuspended; } }
    }

    /// <summary>Solo al pasar de «guardaba bien» a «falla»; ver <see cref="Tasks.TaskManager.WriteFailed"/>.</summary>
    public event EventHandler<DataWriteFailure>? WriteFailed;

    public void Load()
    {
        lock (_sync)
        {
            _state = Normalize(_store.Load());
            LoadOutcome = _store.LastLoad;
            _persistenceSuspended = !LoadOutcome.IsSafeToOverwrite;
            _writeFailing = false;
        }
    }

    /// <summary>
    /// Se llama una vez justo después de <see cref="Load"/>. Si el temporizador guardado ya había
    /// vencido mientras Sakura estaba cerrada (cierre forzado, apagado del equipo), la sesión pasa al
    /// historial con la duración programada, pero se devuelve marcada como recuperada para que nadie
    /// la celebre como si acabara de terminar: Sakura no sabe cuánto rato estuvo la persona.
    /// </summary>
    public FocusRecovery? RecoverAfterRestart(DateTimeOffset now)
    {
        lock (_sync)
        {
            var timerId = _state.ActiveTimer?.Id;
            var completion = CollectCompletionLocked(now);

            // CollectCompletionLocked recorta el historial a 90 días: una sesión más vieja se
            // descarta en la misma llamada. Solo se devuelve una recuperación si de verdad quedó en
            // el historial, para que el aviso no prometa «cuento N minutos» sobre algo que no está.
            if (completion is null || timerId is null || !_state.History.Any(entry => entry.Id == timerId))
            {
                return null;
            }

            return new FocusRecovery(completion);
        }
    }

    public FocusDashboardSnapshot GetSnapshot(DateTimeOffset now)
    {
        lock (_sync)
        {
            var active = _state.ActiveTimer?.Copy();
            var remaining = active?.GetRemaining(now) ?? TimeSpan.Zero;
            var today = now.LocalDateTime.Date;
            var completedToday = _state.History
                .Where(entry => entry.CompletedAt.LocalDateTime.Date == today)
                .ToArray();
            var focused = completedToday
                .Where(entry => entry.Kind != FocusSessionKind.Break)
                .Sum(entry => entry.Duration.TotalMinutes);

            return new FocusDashboardSnapshot(
                active,
                remaining,
                completedToday.Length,
                (int)Math.Round(focused, MidpointRounding.AwayFromZero));
        }
    }

    public FocusOperationResult Start(
        TimeSpan duration,
        string? label,
        FocusSessionKind kind,
        DateTimeOffset now,
        Guid? taskId = null)
    {
        if (duration < MinimumDuration || duration > MaximumDuration)
        {
            return FocusOperationResult.Failed(
                "El temporizador debe durar entre 5 segundos y 24 horas.");
        }

        lock (_sync)
        {
            if (_state.ActiveTimer is not null)
            {
                return FocusOperationResult.Failed(
                    "Ya hay un temporizador activo. Termínalo o cancélalo antes de iniciar otro.");
            }

            var timer = new FocusTimer
            {
                Id = Guid.NewGuid(),
                Label = string.IsNullOrWhiteSpace(label)
                    ? GetDefaultLabel(kind)
                    : label.Trim(),
                Kind = kind,
                CreatedAt = now,
                StartedAt = now,
                EndsAt = now.Add(duration),
                Duration = duration,
                PausedRemaining = duration,
                Status = FocusTimerStatus.Running,
                TaskId = taskId
            };

            _state.ActiveTimer = timer;
            SaveLocked();
            return FocusOperationResult.Completed(
                $"Inicié {timer.Label.ToLowerInvariant()} por {FormatDuration(duration)}.",
                timer.Copy());
        }
    }

    public FocusOperationResult Pause(DateTimeOffset now)
    {
        lock (_sync)
        {
            var timer = _state.ActiveTimer;
            if (timer is null)
            {
                return FocusOperationResult.Failed("No hay un temporizador activo.");
            }

            if (timer.Status == FocusTimerStatus.Paused)
            {
                return FocusOperationResult.Failed("El temporizador ya está en pausa.");
            }

            timer.PausedRemaining = timer.GetRemaining(now);
            timer.EndsAt = null;
            timer.Status = FocusTimerStatus.Paused;
            SaveLocked();
            return FocusOperationResult.Completed(
                $"Pausé {timer.Label.ToLowerInvariant()}.",
                timer.Copy());
        }
    }

    public FocusOperationResult Resume(DateTimeOffset now)
    {
        lock (_sync)
        {
            var timer = _state.ActiveTimer;
            if (timer is null)
            {
                return FocusOperationResult.Failed("No hay un temporizador activo.");
            }

            if (timer.Status == FocusTimerStatus.Running)
            {
                return FocusOperationResult.Failed("El temporizador ya está en curso.");
            }

            if (timer.PausedRemaining <= TimeSpan.Zero)
            {
                return FocusOperationResult.Failed("Ese temporizador ya no tiene tiempo restante.");
            }

            timer.EndsAt = now.Add(timer.PausedRemaining);
            timer.Status = FocusTimerStatus.Running;
            SaveLocked();
            return FocusOperationResult.Completed(
                $"Continué {timer.Label.ToLowerInvariant()}.",
                timer.Copy());
        }
    }

    public FocusOperationResult Cancel()
    {
        lock (_sync)
        {
            var timer = _state.ActiveTimer;
            if (timer is null)
            {
                return FocusOperationResult.Failed("No hay un temporizador activo.");
            }

            _state.ActiveTimer = null;
            SaveLocked();
            return FocusOperationResult.Completed(
                $"Cancelé {timer.Label.ToLowerInvariant()}.");
        }
    }

    /// <summary>
    /// Diseño D3: termina la sesión activa ANTES de que se agote su duración, contando el tiempo
    /// realmente transcurrido como completado. Distinto de <see cref="Cancel"/> (que descarta la
    /// sesión sin dejar rastro) y de la finalización pasiva de <see cref="CollectCompletion"/>
    /// (que solo detecta cuándo el tiempo ya se agotó): esta es la acción que el usuario dispara a
    /// propósito para decir "terminé antes, pero cuenta esto".
    /// </summary>
    public FocusOperationResult Finish(DateTimeOffset now)
    {
        lock (_sync)
        {
            var timer = _state.ActiveTimer;
            if (timer is null)
            {
                return FocusOperationResult.Failed("No hay un temporizador activo.");
            }

            var elapsed = timer.Duration - timer.GetRemaining(now);
            if (elapsed <= TimeSpan.Zero)
            {
                // Nada transcurrido todavía: no hay nada útil que registrar en el historial, así
                // que equivale a cancelar.
                _state.ActiveTimer = null;
                SaveLocked();
                return FocusOperationResult.Completed(
                    $"Finalicé {timer.Label.ToLowerInvariant()}.");
            }

            var historyEntry = new FocusHistoryEntry
            {
                Id = timer.Id,
                Label = timer.Label,
                Kind = timer.Kind,
                StartedAt = timer.StartedAt,
                CompletedAt = now,
                Duration = elapsed,
                TaskId = timer.TaskId
            };

            _state.History.Add(historyEntry);
            _state.ActiveTimer = null;
            TrimHistoryLocked(now);
            SaveLocked();

            // Diseño D3.1: se expone la misma FocusCompletion que ya usa CollectCompletion, para
            // que quien llame a Finish() pueda mostrar un aviso de finalización (duración real,
            // tarea asociada) sin tener que volver a leer el timer antes de llamar — Finish() ya
            // lo limpió.
            var completion = new FocusCompletion(
                historyEntry.Label,
                historyEntry.Kind,
                historyEntry.Duration,
                historyEntry.CompletedAt,
                historyEntry.TaskId);

            return FocusOperationResult.Completed(
                $"Finalizaste {timer.Label.ToLowerInvariant()} antes de tiempo. " +
                $"Se registraron {FormatDuration(elapsed)}.",
                completion: completion);
        }
    }

    public FocusCompletion? CollectCompletion(DateTimeOffset now)
    {
        lock (_sync)
        {
            return CollectCompletionLocked(now);
        }
    }

    private FocusCompletion? CollectCompletionLocked(DateTimeOffset now)
    {
        var timer = _state.ActiveTimer;
        if (timer is null ||
            timer.Status != FocusTimerStatus.Running ||
            !timer.EndsAt.HasValue ||
            timer.EndsAt.Value > now)
        {
            return null;
        }

        var completedAt = timer.EndsAt.Value;
        var historyEntry = new FocusHistoryEntry
        {
            Id = timer.Id,
            Label = timer.Label,
            Kind = timer.Kind,
            StartedAt = timer.StartedAt,
            CompletedAt = completedAt,
            Duration = timer.Duration,
            TaskId = timer.TaskId
        };

        _state.History.Add(historyEntry);
        _state.ActiveTimer = null;
        TrimHistoryLocked(now);
        SaveLocked();

        return new FocusCompletion(
            historyEntry.Label,
            historyEntry.Kind,
            historyEntry.Duration,
            historyEntry.CompletedAt,
            historyEntry.TaskId);
    }

    /// <summary>
    /// Diseño D3.1 — copia de solo lectura del historial real, para que Nexo.App construya un
    /// resumen de actividad reciente sin poder mutar el estado interno de <see cref="FocusManager"/>.
    /// </summary>
    public IReadOnlyList<FocusHistoryEntry> GetHistory()
    {
        lock (_sync)
        {
            return _state.History.Select(entry => entry.Copy()).ToList();
        }
    }

    public string BuildStatus(DateTimeOffset now)
    {
        lock (_sync)
        {
            var timer = _state.ActiveTimer;
            if (timer is null)
            {
                return "No hay un temporizador activo.";
            }

            var remaining = timer.GetRemaining(now);
            var status = timer.Status == FocusTimerStatus.Paused
                ? "está en pausa"
                : "sigue en curso";

            return $"{timer.Label} {status}. Quedan {FormatRemaining(remaining)}.";
        }
    }

    private void SaveLocked()
    {
        if (_persistenceSuspended)
        {
            // No se escribe encima de un archivo que no se pudo leer; ver Load.
            return;
        }

        try
        {
            _store.Save(_state.Copy());
            _writeFailing = false;
        }
        catch (SakuraDataWriteException exception)
        {
            // El estado queda en memoria; se avisa una sola vez de que no está en disco.
            if (!_writeFailing)
            {
                _writeFailing = true;
                WriteFailed?.Invoke(this, new DataWriteFailure(exception.Path, exception.Reason));
            }
        }
    }

    private void TrimHistoryLocked(DateTimeOffset now)
    {
        var cutoff = now.AddDays(-90);
        _state.History = _state.History
            .Where(entry => entry.CompletedAt >= cutoff)
            .OrderBy(entry => entry.CompletedAt)
            .TakeLast(500)
            .ToList();
    }

    private static FocusState Normalize(FocusState? state)
    {
        state ??= new FocusState();
        state.History ??= [];
        state.History = state.History
            .Where(entry => entry is not null && entry.Duration > TimeSpan.Zero)
            .Select(entry =>
            {
                var copy = entry.Copy();
                copy.Label ??= string.Empty;
                return copy;
            })
            .ToList();

        if (state.ActiveTimer is { } timer)
        {
            if (timer.Duration <= TimeSpan.Zero ||
                timer.Duration > MaximumDuration ||
                string.IsNullOrWhiteSpace(timer.Label))
            {
                state.ActiveTimer = null;
            }
            else
            {
                timer.Label = timer.Label.Trim();
                timer.PausedRemaining = timer.PausedRemaining > TimeSpan.Zero
                    ? timer.PausedRemaining
                    : timer.Duration;
            }
        }

        return state;
    }

    private static string GetDefaultLabel(FocusSessionKind kind) => kind switch
    {
        FocusSessionKind.Focus => "Sesión de enfoque",
        FocusSessionKind.Study => "Sesión de estudio",
        FocusSessionKind.Break => "Descanso",
        _ => "Temporizador"
    };

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1 && duration.Minutes == 0)
        {
            var hours = (int)duration.TotalHours;
            return $"{hours} hora{(hours == 1 ? string.Empty : "s")}";
        }

        var minutes = Math.Max(1, (int)Math.Round(duration.TotalMinutes));
        return $"{minutes} minuto{(minutes == 1 ? string.Empty : "s")}";
    }

    private static string FormatRemaining(TimeSpan remaining)
    {
        if (remaining.TotalHours >= 1)
        {
            return $"{(int)remaining.TotalHours} h {remaining.Minutes:D2} min";
        }

        if (remaining.TotalMinutes >= 1)
        {
            return $"{(int)remaining.TotalMinutes} min {remaining.Seconds:D2} s";
        }

        return $"{Math.Max(0, remaining.Seconds)} segundos";
    }
}
