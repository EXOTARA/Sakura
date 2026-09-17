using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Nexo.Core.Automation;

namespace Nexo.App.Views;

public partial class RoutinesView : UserControl
{
    private readonly RoutineManager _routineManager;
    private Guid? _editingId;

    public RoutinesView(RoutineManager routineManager)
    {
        _routineManager = routineManager;
        InitializeComponent();
        Refresh();
    }

    public event EventHandler<RoutineRequestedEventArgs>? ExecuteRequested;

    public void Refresh()
    {
        var items = _routineManager.GetAll()
            .Select(routine => new RoutineListItem(
                routine.Id,
                routine.Name,
                $"«{routine.TriggerPhrase}»",
                routine.IsEnabled ? string.Empty : " · en pausa",
                BuildStepSummary(routine),
                FormatLastExecution(routine),
                routine.LastExecutionSucceeded == false
                    ? (Brush)FindResource("BrushWarning")
                    : (Brush)FindResource("BrushTextTertiary"),
                routine.IsEnabled ? "Pausar" : "Activar",
                $"Ejecutar {routine.Name}",
                routine.IsEnabled ? $"Pausar {routine.Name}" : $"Activar {routine.Name}",
                $"Editar {routine.Name}",
                $"Eliminar {routine.Name}",
                routine.IsEnabled ? 1.0 : 0.55))
            .ToArray();
        RoutinesItemsControl.ItemsSource = items;
        EmptyStateText.Visibility = items.Length == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private static string FormatLastExecution(RoutineDefinition routine)
    {
        if (!routine.LastExecutedAt.HasValue)
        {
            return "Nunca ejecutada";
        }

        var outcome = routine.LastExecutionSucceeded == false ? " · con avisos" : string.Empty;
        var at = routine.LastExecutedAt.Value;
        return $"Última vez: {at.ToString("ddd d MMM", CultureInfo.GetCultureInfo("es-MX")).Replace(".", string.Empty)} · {Nexo.Core.Tasks.TodayPlan.Clock(at)}{outcome}";
    }

    public void FocusPrimaryControl()
    {
        if (EditorScrollViewer.Visibility == Visibility.Visible)
        {
            NameTextBox.Focus();
        }
    }

    /// <summary>Abre el editor ya rellenado; nada se guarda hasta pulsar Guardar.</summary>
    private void TemplateButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string template })
        {
            return;
        }

        NewRoutineButton_Click(sender, e);
        switch (template)
        {
            case "study":
                NameTextBox.Text = "Estudiar";
                TriggerTextBox.Text = "modo estudio";
                MuteDiscordCheckBox.IsChecked = true;
                SpotifyVolumeCheckBox.IsChecked = true;
                SpotifyVolumeTextBox.Text = "20";
                StartFocusCheckBox.IsChecked = true;
                FocusMinutesTextBox.Text = "25";
                break;
            case "code":
                NameTextBox.Text = "Programar";
                TriggerTextBox.Text = "a programar";
                OpenCodeCheckBox.IsChecked = true;
                OpenTerminalCheckBox.IsChecked = true;
                StartFocusCheckBox.IsChecked = true;
                FocusMinutesTextBox.Text = "50";
                break;
            case "rest":
                NameTextBox.Text = "Descanso";
                TriggerTextBox.Text = "hora de descansar";
                UnmuteDiscordCheckBox.IsChecked = true;
                StartBreakCheckBox.IsChecked = true;
                BreakMinutesTextBox.Text = "10";
                break;
        }
    }

    private void NewRoutineButton_Click(object sender, RoutedEventArgs e)
    {
        _editingId = null;
        EditorTitleText.Text = "Nuevo atajo";
        NameTextBox.Text = string.Empty;
        TriggerTextBox.Text = string.Empty;
        EnabledCheckBox.IsChecked = true;
        ConfirmationCheckBox.IsChecked = true;
        ProjectFolderTextBox.Text = "{project}";
        OpenCodeCheckBox.IsChecked = false;
        OpenTerminalCheckBox.IsChecked = false;
        SpotifyVolumeCheckBox.IsChecked = false;
        SpotifyVolumeTextBox.Text = "20";
        MuteDiscordCheckBox.IsChecked = false;
        UnmuteDiscordCheckBox.IsChecked = false;
        StartFocusCheckBox.IsChecked = true;
        FocusMinutesTextBox.Text = "40";
        StartBreakCheckBox.IsChecked = false;
        BreakMinutesTextBox.Text = "10";
        CreateTaskCheckBox.IsChecked = false;
        TaskTitleTextBox.Text = string.Empty;
        ShowEditor();
    }

    private void EditRoutineButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetId(sender, out var id))
        {
            return;
        }

        var routine = _routineManager.GetAll().FirstOrDefault(candidate => candidate.Id == id);
        if (routine is null)
        {
            Refresh();
            return;
        }

        _editingId = routine.Id;
        EditorTitleText.Text = "Editar atajo";
        NameTextBox.Text = routine.Name;
        TriggerTextBox.Text = routine.TriggerPhrase;
        EnabledCheckBox.IsChecked = routine.IsEnabled;
        ConfirmationCheckBox.IsChecked = routine.RequiresConfirmation;

        var code = routine.Steps.FirstOrDefault(step => step.Type == AutomationActionType.OpenApplication &&
            step.Target.Equals("code", StringComparison.OrdinalIgnoreCase));
        var terminal = routine.Steps.FirstOrDefault(step => step.Type == AutomationActionType.OpenTerminal);
        var spotify = routine.Steps.FirstOrDefault(step => step.Type == AutomationActionType.SetApplicationVolume &&
            step.Target.Equals("Spotify", StringComparison.OrdinalIgnoreCase));
        var discord = routine.Steps.FirstOrDefault(step => step.Type == AutomationActionType.MuteApplication &&
            step.Target.Equals("Discord", StringComparison.OrdinalIgnoreCase));
        var unmuteDiscord = routine.Steps.FirstOrDefault(step => step.Type == AutomationActionType.UnmuteApplication &&
            step.Target.Equals("Discord", StringComparison.OrdinalIgnoreCase));
        var focus = routine.Steps.FirstOrDefault(step => step.Type == AutomationActionType.StartFocus);
        var breakTimer = routine.Steps.FirstOrDefault(step => step.Type == AutomationActionType.StartBreak);
        var task = routine.Steps.FirstOrDefault(step => step.Type == AutomationActionType.CreateTask);

        OpenCodeCheckBox.IsChecked = code is not null;
        OpenTerminalCheckBox.IsChecked = terminal is not null;
        ProjectFolderTextBox.Text = terminal?.WorkingDirectory ?? code?.WorkingDirectory ?? "{project}";
        SpotifyVolumeCheckBox.IsChecked = spotify is not null;
        SpotifyVolumeTextBox.Text = (spotify?.NumericValue ?? 20).ToString("0", CultureInfo.InvariantCulture);
        MuteDiscordCheckBox.IsChecked = discord is not null;
        UnmuteDiscordCheckBox.IsChecked = unmuteDiscord is not null;
        StartFocusCheckBox.IsChecked = focus is not null;
        FocusMinutesTextBox.Text = (focus?.NumericValue ?? 40).ToString("0", CultureInfo.InvariantCulture);
        StartBreakCheckBox.IsChecked = breakTimer is not null;
        BreakMinutesTextBox.Text = (breakTimer?.NumericValue ?? 10).ToString("0", CultureInfo.InvariantCulture);
        CreateTaskCheckBox.IsChecked = task is not null;
        TaskTitleTextBox.Text = task?.Text ?? string.Empty;
        ShowEditor();
    }

    private void DeleteRoutineButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetId(sender, out var id))
        {
            return;
        }

        var routine = _routineManager.GetAll().FirstOrDefault(candidate => candidate.Id == id);
        if (routine is null)
        {
            return;
        }

        var confirmation = MessageBox.Show(
            $"¿Eliminar el atajo {routine.Name}?",
            "Eliminar atajo",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        _routineManager.Delete(id);
        Refresh();
    }

    private void RunRoutineButton_Click(object sender, RoutedEventArgs e)
    {
        if (TryGetId(sender, out var id))
        {
            ExecuteRequested?.Invoke(this, new RoutineRequestedEventArgs(id));
        }
    }

    private void ToggleRoutineButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetId(sender, out var id))
        {
            return;
        }

        var routine = _routineManager.GetAll().FirstOrDefault(candidate => candidate.Id == id);
        if (routine is null)
        {
            return;
        }

        _routineManager.SetEnabled(id, !routine.IsEnabled);
        Refresh();
    }

    private void CancelEditorButton_Click(object sender, RoutedEventArgs e) => HideEditor();

    private void SaveRoutineButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryBuildRoutine(out var routine, out var error))
        {
            EditorErrorText.Text = error;
            EditorErrorText.Visibility = Visibility.Visible;
            return;
        }

        var result = _editingId.HasValue
            ? _routineManager.Update(routine)
            : _routineManager.Create(routine);
        if (!result.Success)
        {
            EditorErrorText.Text = result.Message;
            EditorErrorText.Visibility = Visibility.Visible;
            return;
        }

        HideEditor();
        Refresh();
    }

    private bool TryBuildRoutine(out RoutineDefinition routine, out string error)
    {
        routine = new RoutineDefinition
        {
            Id = _editingId ?? Guid.NewGuid(),
            Name = NameTextBox.Text.Trim(),
            TriggerPhrase = TriggerTextBox.Text.Trim(),
            IsEnabled = EnabledCheckBox.IsChecked == true,
            RequiresConfirmation = ConfirmationCheckBox.IsChecked == true
        };
        error = string.Empty;
        var folder = string.IsNullOrWhiteSpace(ProjectFolderTextBox.Text)
            ? "{project}"
            : ProjectFolderTextBox.Text.Trim();

        if (OpenCodeCheckBox.IsChecked == true)
        {
            routine.Steps.Add(new AutomationAction
            {
                Type = AutomationActionType.OpenApplication,
                Target = "code",
                Arguments = ".",
                WorkingDirectory = folder
            });
        }

        if (OpenTerminalCheckBox.IsChecked == true)
        {
            routine.Steps.Add(new AutomationAction
            {
                Type = AutomationActionType.OpenTerminal,
                WorkingDirectory = folder
            });
        }

        if (SpotifyVolumeCheckBox.IsChecked == true)
        {
            if (!TryReadNumber(SpotifyVolumeTextBox.Text, 0, 100, out var volume))
            {
                error = "El volumen de Spotify debe estar entre 0 y 100.";
                return false;
            }

            routine.Steps.Add(new AutomationAction
            {
                Type = AutomationActionType.SetApplicationVolume,
                Target = "Spotify",
                NumericValue = volume
            });
        }

        if (MuteDiscordCheckBox.IsChecked == true)
        {
            routine.Steps.Add(new AutomationAction
            {
                Type = AutomationActionType.MuteApplication,
                Target = "Discord"
            });
        }

        if (UnmuteDiscordCheckBox.IsChecked == true)
        {
            routine.Steps.Add(new AutomationAction
            {
                Type = AutomationActionType.UnmuteApplication,
                Target = "Discord"
            });
        }

        if (StartFocusCheckBox.IsChecked == true)
        {
            if (!TryReadNumber(FocusMinutesTextBox.Text, 1, 1440, out var minutes))
            {
                error = "La sesión de enfoque debe durar entre 1 y 1440 minutos.";
                return false;
            }

            routine.Steps.Add(new AutomationAction
            {
                Type = AutomationActionType.StartFocus,
                NumericValue = minutes,
                Text = $"Sesión de {routine.Name.ToLowerInvariant()}"
            });
        }

        if (StartBreakCheckBox.IsChecked == true)
        {
            if (!TryReadNumber(BreakMinutesTextBox.Text, 1, 1440, out var breakMinutes))
            {
                error = "El descanso debe durar entre 1 y 1440 minutos.";
                return false;
            }

            routine.Steps.Add(new AutomationAction
            {
                Type = AutomationActionType.StartBreak,
                NumericValue = breakMinutes,
                Text = "Descanso"
            });
        }

        if (CreateTaskCheckBox.IsChecked == true)
        {
            var taskTitle = TaskTitleTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(taskTitle))
            {
                error = "Escribe el título de la tarea que creará el atajo.";
                return false;
            }

            routine.Steps.Add(new AutomationAction
            {
                Type = AutomationActionType.CreateTask,
                Text = taskTitle
            });
        }

        if (routine.Steps.Count == 0)
        {
            error = "Selecciona al menos una acción.";
            return false;
        }

        return true;
    }

    private void ShowEditor()
    {
        EditorErrorText.Visibility = Visibility.Collapsed;
        ListScrollViewer.Visibility = Visibility.Collapsed;
        EditorScrollViewer.Visibility = Visibility.Visible;
        NameTextBox.Focus();
    }

    private void HideEditor()
    {
        EditorScrollViewer.Visibility = Visibility.Collapsed;
        ListScrollViewer.Visibility = Visibility.Visible;
        EditorErrorText.Visibility = Visibility.Collapsed;
    }

    private static bool TryReadNumber(string value, int min, int max, out double result) =>
        double.TryParse(value.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out result) &&
        result >= min && result <= max;

    private static bool TryGetId(object sender, out Guid id)
    {
        id = Guid.Empty;
        return sender is Button { Tag: not null } button &&
               Guid.TryParse(button.Tag.ToString(), out id);
    }

    private static string BuildStepSummary(RoutineDefinition routine)
    {
        var names = routine.Steps.Where(step => step.IsEnabled).Select(step => step.Type switch
        {
            AutomationActionType.OpenApplication => $"Abrir {step.Target}",
            AutomationActionType.OpenFolder => "Abrir carpeta",
            AutomationActionType.OpenTerminal => "Abrir PowerShell",
            AutomationActionType.SetApplicationVolume => $"{step.Target} al {step.NumericValue:0}%",
            AutomationActionType.MuteApplication => $"Silenciar {step.Target}",
            AutomationActionType.UnmuteApplication => $"Activar {step.Target}",
            AutomationActionType.StartFocus => $"Enfoque {step.NumericValue:0} min",
            AutomationActionType.StartBreak => $"Descanso {step.NumericValue:0} min",
            AutomationActionType.CreateTask => $"Crear tarea: {step.Text}",
            _ => "Acción bloqueada"
        });
        return string.Join(" · ", names);
    }

    private sealed record RoutineListItem(
        Guid Id,
        string Name,
        string TriggerPhrase,
        string Status,
        string StepSummary,
        string LastExecutionText,
        Brush LastExecutionBrush,
        string ToggleLabel,
        string RunAutomationName,
        string ToggleAutomationName,
        string EditAutomationName,
        string DeleteAutomationName,
        double RowOpacity);
}

public sealed class RoutineRequestedEventArgs : EventArgs
{
    public RoutineRequestedEventArgs(Guid routineId)
    {
        RoutineId = routineId;
    }

    public Guid RoutineId { get; }
}
