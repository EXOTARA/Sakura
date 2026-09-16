using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Nexo.App.Motion;
using Nexo.Core.Tasks;

namespace Nexo.App.Views;

/// <summary>
/// 2026-09-16 — Hoy como centro del día. Qué entra en cada sección lo decide
/// <see cref="TodayPlan"/>; lo que se escribe en la caja lo entiende <see cref="QuickCapture"/>.
/// </summary>
public partial class TasksView : UserControl
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-MX");

    private readonly TaskManager _taskManager;
    private readonly HashSet<TodaySectionKind> _open = [TodaySectionKind.Important, TodaySectionKind.Day];
    private DateOnly? _day;
    private Guid? _editingId;
    private Guid? _releasedId;

    public TasksView(TaskManager taskManager)
    {
        _taskManager = taskManager;
        InitializeComponent();
        Refresh();
    }

    public event EventHandler? TasksChanged;

    /// <summary>Se pidió enfocarse en una tarea; lo arranca quien tiene el FocusManager.</summary>
    public event EventHandler<TaskFocusRequestedEventArgs>? FocusRequested;

    private DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    private DateOnly Day => _day ?? Today;

    /// <summary>Abre Hoy en otro día: lo usa el calendario del panel de arriba.</summary>
    public void ShowDay(DateOnly day)
    {
        _day = day == Today ? null : day;
        EditorBorder.Visibility = Visibility.Collapsed;
        Refresh();
        if (SakuraMotion.AnimationsEnabled)
        {
            EntranceMotion.Rise(SectionsItemsControl, TimeSpan.Zero);
        }
    }

    public void Refresh()
    {
        var now = DateTimeOffset.Now;
        var isToday = Day == Today;

        // Hoy ya lo dice el encabezado del panel: aquí basta la fecha. Otro día sí lleva título.
        DayTitleText.Text = Capitalize(Day.ToString("dddd d 'de' MMMM", Spanish));
        DaySubtitleText.Text = isToday ? string.Empty : "Lo que apuntes aquí queda para este día.";
        DaySubtitleText.Visibility = isToday ? Visibility.Collapsed : Visibility.Visible;
        BackToTodayButton.Visibility = isToday ? Visibility.Collapsed : Visibility.Visible;

        var sections = TodayPlan.Build(_taskManager.GetAll(), Day, now)
            .Where(section => section.Items.Count > 0)
            .Select(section => CreateSection(section, now))
            .ToArray();

        SectionsItemsControl.ItemsSource = sections;

        var empty = sections.Length == 0;
        EmptyStatePanel.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        if (empty)
        {
            var hasAnyTask = _taskManager.GetAll().Count > 0;
            EmptyStateTitle.Text = isToday ? "Hoy está libre" : "Nada para este día";
            EmptyStateText.Text = !isToday
                ? "Apunta arriba lo que quieras tener presente ese día."
                : hasAnyTask
                    ? "No hay nada para hoy. Apunta algo arriba o disfruta el hueco."
                    : "Apunta arriba lo que tengas que hacer, como lo dirías: «llamar al dentista mañana a las 10» o «importante: estudiar para el parcial el jueves».";
        }
    }

    public void OpenNewEditor()
    {
        _editingId = null;
        EditorTitleText.Text = "Nueva tarea";
        TitleTextBox.Text = CaptureTextBox.Text.Trim();
        NotesTextBox.Text = string.Empty;
        SetWhen(new DateTimeOffset(Day.ToDateTime(TimeOnly.MinValue), DateTimeOffset.Now.Offset));
        PriorityComboBox.SelectedIndex = 1;
        ReminderCheckBox.IsChecked = false;
        DeleteTaskButton.Visibility = Visibility.Collapsed;
        HideEditorError();
        EditorBorder.Visibility = Visibility.Visible;
        TitleTextBox.Focus();
    }

    public void FocusPrimaryControl()
    {
        if (EditorBorder.Visibility == Visibility.Visible)
        {
            TitleTextBox.Focus();
        }
        else
        {
            CaptureTextBox.Focus();
        }
    }

    // ---------- Captura ----------

    private void CaptureTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var text = CaptureTextBox.Text;
        CapturePlaceholder.Visibility = text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

        if (string.IsNullOrWhiteSpace(text))
        {
            CapturePreviewText.Visibility = Visibility.Collapsed;
            return;
        }

        // Lo que se entendió, antes de guardar: así una fecha mal leída se ve y se corrige.
        var now = DateTimeOffset.Now;
        var parsed = QuickCapture.Parse(text, now, Day);
        var probe = new NexoTask { DueAt = parsed.DueAt };
        var parts = new List<string> { parsed.DueAt is null ? "Sin fecha" : TodayPlan.Describe(probe, now) };
        if (parsed.Priority == TaskPriority.High)
        {
            parts.Add("Importante");
        }

        CapturePreviewText.Text = "↵  " + string.Join(" · ", parts);
        CapturePreviewText.Visibility = Visibility.Visible;
    }

    private void CaptureTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CaptureTextBox.Clear();
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter || string.IsNullOrWhiteSpace(CaptureTextBox.Text))
        {
            return;
        }

        e.Handled = true;
        var parsed = QuickCapture.Parse(CaptureTextBox.Text, DateTimeOffset.Now, Day);
        _taskManager.Create(parsed.Title, null, parsed.DueAt, parsed.Priority);
        CaptureTextBox.Clear();
        Changed();
    }

    // ---------- Secciones y filas ----------

    private void SectionHeader_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: TodaySectionKind kind })
        {
            return;
        }

        if (!_open.Remove(kind))
        {
            _open.Add(kind);
        }

        Refresh();
    }

    private void CheckButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTaskId(sender, out var id))
        {
            return;
        }

        var done = sender is Button { Tag: "Done" };
        if (done)
        {
            _taskManager.Reopen(id);
        }
        else
        {
            _taskManager.Complete(id);
        }

        Changed();
    }

    /// <summary>
    /// 2026-09-16 — «▶» ya no arranca 25 minutos sin preguntar (Adler: un enfoque de 25 minutos
    /// para «comprar tortillas» no tiene sentido). Se elige cuánto rato, y el botón solo aparece en
    /// lo importante, que es el trabajo que pide sentarse; un recordatorio no.
    /// </summary>
    private void FocusTaskButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || !TryGetTaskId(sender, out var id) || Find(id) is not { } task)
        {
            return;
        }

        var menu = new ContextMenu { PlacementTarget = button, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        foreach (var minutes in new[] { 5, 15, 25, 45 })
        {
            var item = new MenuItem { Header = minutes == 5 ? "Solo 5 min para empezar" : $"{minutes} min" };
            item.Click += (_, _) => FocusRequested?.Invoke(this, new TaskFocusRequestedEventArgs(task.Id, task.Title, minutes));
            menu.Items.Add(item);
        }

        button.ContextMenu = menu;
        menu.IsOpen = true;
    }

    private void PostponeTaskButton_Click(object sender, RoutedEventArgs e)
    {
        if (TryGetTaskId(sender, out var id))
        {
            _taskManager.Postpone(id, DateTimeOffset.Now);
            Changed();
        }
    }

    private void ReleaseTaskButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTaskId(sender, out var id) || Find(id) is not { } task)
        {
            return;
        }

        _taskManager.Release(id, DateTimeOffset.Now);
        _releasedId = id;
        UndoText.Text = $"Soltaste «{task.Title}»";
        UndoBar.Visibility = Visibility.Visible;

        // El aviso se va solo: una barra que se queda para siempre acaba siendo ruido.
        _undoTimer ??= new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
        _undoTimer.Stop();
        _undoTimer.Tick -= HideUndo;
        _undoTimer.Tick += HideUndo;
        _undoTimer.Start();
        Changed();
    }

    private System.Windows.Threading.DispatcherTimer? _undoTimer;

    private void HideUndo(object? sender, EventArgs e)
    {
        _undoTimer?.Stop();
        _releasedId = null;
        UndoBar.Visibility = Visibility.Collapsed;
    }

    private void UndoButton_Click(object sender, RoutedEventArgs e)
    {
        if (_releasedId is { } id)
        {
            _taskManager.Restore(id);
        }

        _releasedId = null;
        UndoBar.Visibility = Visibility.Collapsed;
        Changed();
    }

    private void BackToTodayButton_Click(object sender, RoutedEventArgs e) => ShowDay(Today);

    // ---------- Editor ----------

    private void NewTaskButton_Click(object sender, RoutedEventArgs e) =>
        OpenNewEditor();

    private void CancelEditorButton_Click(object sender, RoutedEventArgs e) =>
        EditorBorder.Visibility = Visibility.Collapsed;

    private void EditTaskButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTaskId(sender, out var id))
        {
            return;
        }

        if (Find(id) is not { } task)
        {
            Refresh();
            return;
        }

        _editingId = id;
        EditorTitleText.Text = "Editar tarea";
        TitleTextBox.Text = task.Title;
        NotesTextBox.Text = task.Notes;
        SetWhen(task.DueAt);
        PriorityComboBox.SelectedIndex = task.Priority switch
        {
            TaskPriority.Low => 0,
            TaskPriority.High => 2,
            _ => 1
        };
        ReminderCheckBox.IsChecked = task.ReminderEnabled;
        DeleteTaskButton.Visibility = Visibility.Visible;
        HideEditorError();
        EditorBorder.Visibility = Visibility.Visible;
        TitleTextBox.Focus();
    }

    private void SaveTaskButton_Click(object sender, RoutedEventArgs e)
    {
        var title = TitleTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            ShowEditorError("Escribe un título para la tarea.");
            return;
        }

        if (!TryGetDueAt(out var dueAt, out var dateError))
        {
            ShowEditorError(dateError);
            return;
        }

        var priority = GetSelectedPriority();
        var reminderEnabled = ReminderCheckBox.IsChecked == true;
        if (reminderEnabled && (dueAt is null || dueAt.Value.TimeOfDay == TimeSpan.Zero))
        {
            ShowEditorError("Elige fecha y hora para poder activar el recordatorio.");
            return;
        }

        if (reminderEnabled && dueAt <= DateTimeOffset.Now)
        {
            ShowEditorError("El recordatorio debe quedar en una fecha futura.");
            return;
        }

        if (_editingId.HasValue)
        {
            if (Find(_editingId.Value) is not { } existing)
            {
                ShowEditorError("La tarea ya no existe.");
                return;
            }

            existing.Title = title;
            existing.Notes = NotesTextBox.Text;
            existing.DueAt = dueAt;
            existing.Priority = priority;
            existing.ReminderEnabled = reminderEnabled;
            var result = _taskManager.Update(existing);
            if (!result.Success)
            {
                ShowEditorError(result.Message);
                return;
            }
        }
        else
        {
            _taskManager.Create(title, NotesTextBox.Text, dueAt, priority, reminderEnabled);
            CaptureTextBox.Clear();
        }

        EditorBorder.Visibility = Visibility.Collapsed;
        Changed();
    }

    private void DeleteTaskButton_Click(object sender, RoutedEventArgs e)
    {
        if (_editingId is not { } id || Find(id) is not { } task)
        {
            return;
        }

        // Diseño D3: eliminar pregunta, con «No» por defecto. Para quitar algo de la vista sin
        // perderlo está «Soltar».
        var confirmation = MessageBox.Show(
            $"¿Eliminar «{task.Title}»? Esta acción no se puede deshacer.",
            "Eliminar tarea",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);
        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        _taskManager.Delete(id);
        EditorBorder.Visibility = Visibility.Collapsed;
        Changed();
    }

    // ---------- Cuándo ----------

    private DateTimeOffset? _whenOriginal;
    private string _whenOriginalText = string.Empty;

    /// <summary>Pone en la caja una fecha ya guardada, escrita de forma que se vuelva a entender.</summary>
    private void SetWhen(DateTimeOffset? due)
    {
        _whenOriginal = due;
        _whenOriginalText = due is not { } value
            ? string.Empty
            : DateOnly.FromDateTime(value.Date) == Today
                ? "hoy" + (value.TimeOfDay == TimeSpan.Zero ? string.Empty : " " + TodayPlan.Clock(value))
                : value.ToString("dd/MM", CultureInfo.InvariantCulture) + (value.TimeOfDay == TimeSpan.Zero ? string.Empty : " " + TodayPlan.Clock(value));
        WhenTextBox.Text = _whenOriginalText;
    }

    private void WhenTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        WhenHint.Visibility = WhenTextBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        WhenPreviewText.Text = TryGetDueAt(out var due, out var error)
            ? due is null ? "Sin fecha" : "→ " + TodayPlan.Describe(new NexoTask { DueAt = due }, DateTimeOffset.Now)
            : error;
    }

    private void WhenChip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string text })
        {
            // Si ya había hora, se conserva al cambiar de día.
            var time = TryGetDueAt(out var due, out _) && due is { } current && current.TimeOfDay != TimeSpan.Zero
                ? " " + TodayPlan.Clock(current)
                : string.Empty;
            WhenTextBox.Text = text.Length == 0 ? string.Empty : text + time;
        }
    }

    private void EditorHint_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (NotesHint is null || TitleHint is null)
        {
            return;
        }

        NotesHint.Visibility = NotesTextBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        TitleHint.Visibility = TitleTextBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private bool TryGetDueAt(out DateTimeOffset? dueAt, out string error)
    {
        dueAt = null;
        error = string.Empty;
        var text = WhenTextBox.Text.Trim();

        if (text.Length == 0)
        {
            return true;
        }

        // Sin tocar, se guarda tal cual: releer «15/09» podría llevarla al año que viene.
        if (text == _whenOriginalText)
        {
            dueAt = _whenOriginal;
            return true;
        }

        // Aquí un número suelto es una hora («3» → 3:00 pm); en la caja de captura no, porque ahí
        // puede ser parte del título.
        if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var hour) && hour is >= 1 and <= 12)
        {
            text = "a las " + text;
        }

        // Si sobra texto sin entender, se dice en vez de guardar otra fecha.
        var parsed = QuickCapture.Parse(text, DateTimeOffset.Now);
        if (parsed.DueAt is null || parsed.Leftover.Length > 0)
        {
            error = "No entendí la fecha. Prueba «hoy 5 pm», «viernes» o «20/09».";
            return false;
        }

        dueAt = parsed.DueAt;
        return true;
    }

    // ---------- Ayudantes ----------

    private void Changed()
    {
        Refresh();
        TasksChanged?.Invoke(this, EventArgs.Empty);
    }

    private NexoTask? Find(Guid id) =>
        _taskManager.GetAll().FirstOrDefault(candidate => candidate.Id == id);

    private SectionItem CreateSection(TodaySection section, DateTimeOffset now)
    {
        var collapsible = section.Kind is TodaySectionKind.Later or TodaySectionKind.Done;
        var open = !collapsible || _open.Contains(section.Kind);
        var titleBrush = section.Kind == TodaySectionKind.Important
            ? (Brush)FindResource("BrushAccent")
            : (Brush)FindResource("BrushTextSecondary");

        return new SectionItem(
            section.Kind,
            section.Title.ToUpper(Spanish),
            section.Items.Count.ToString(CultureInfo.InvariantCulture),
            collapsible ? (open ? "▴" : "▾") : string.Empty,
            collapsible,
            collapsible ? $"{section.Title}, {section.Items.Count}. {(open ? "Plegar" : "Desplegar")}" : section.Title,
            titleBrush,
            open ? Visibility.Visible : Visibility.Collapsed,
            section.Items.Select(CreateRow).ToArray());
    }

    private RowItem CreateRow(TodayItem item)
    {
        var done = item.Task.IsCompleted;
        return new RowItem(
            item.Task.Id,
            item.Task.Title,
            item.When,
            done ? "Done" : "Pending",
            done ? "Reabrir tarea" : "Marcar como completada",
            done ? (Brush)FindResource("BrushTextTertiary") : (Brush)FindResource("BrushTextPrimary"),
            (Brush)FindResource(item.LeftPending ? "BrushWarning" : "BrushTextTertiary"),
            done ? Visibility.Collapsed : Visibility.Visible,
            !done && item.Task.Priority == TaskPriority.High ? Visibility.Visible : Visibility.Collapsed,
            done ? TextDecorations.Strikethrough : null);
    }

    private TaskPriority GetSelectedPriority() =>
        PriorityComboBox.SelectedItem is ComboBoxItem { Tag: string value } &&
        Enum.TryParse<TaskPriority>(value, ignoreCase: true, out var priority)
            ? priority
            : TaskPriority.Normal;

    private void ShowEditorError(string message)
    {
        EditorErrorText.Text = message;
        EditorErrorText.Visibility = Visibility.Visible;
    }

    private void HideEditorError()
    {
        EditorErrorText.Text = string.Empty;
        EditorErrorText.Visibility = Visibility.Collapsed;
    }

    private static bool TryGetTaskId(object sender, out Guid id)
    {
        id = Guid.Empty;
        return sender is Button { CommandParameter: Guid taskId } && (id = taskId) != Guid.Empty;
    }

    private static string Capitalize(string text) =>
        text.Length == 0 ? text : char.ToUpper(text[0], Spanish) + text[1..];

    private sealed record SectionItem(
        TodaySectionKind Key,
        string Title,
        string CountText,
        string Chevron,
        bool IsCollapsible,
        string AccessibleName,
        Brush TitleBrush,
        Visibility ItemsVisibility,
        IReadOnlyList<RowItem> Items);

    private sealed record RowItem(
        Guid Id,
        string Title,
        string When,
        string CheckTag,
        string CheckName,
        Brush TitleBrush,
        Brush WhenBrush,
        Visibility PendingVisibility,
        Visibility FocusVisibility,
        TextDecorationCollection? TextDecorations);
}

public sealed class TaskFocusRequestedEventArgs(Guid taskId, string taskTitle, int minutes = 25) : EventArgs
{
    public Guid TaskId { get; } = taskId;

    public string TaskTitle { get; } = taskTitle;

    public int Minutes { get; } = minutes;
}
