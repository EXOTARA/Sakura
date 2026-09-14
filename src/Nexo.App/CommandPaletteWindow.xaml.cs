using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Nexo.Core.Commands;
using Nexo.Core.Diagnostics;

using Nexo.App.Shell;

namespace Nexo.App;

public sealed class CommandPalettePromptEventArgs(string prompt) : EventArgs
{
    public string Prompt { get; } = prompt;
}

public sealed record CommandPaletteSuggestion(
    string Title,
    string Command,
    string Subtitle,
    string Glyph,
    string ShortcutHint,
    IReadOnlyList<string> Keywords,
    bool IsPrompt = false)
{
    public override string ToString() => Title;

    public Visibility ShortcutVisibility =>
        string.IsNullOrWhiteSpace(ShortcutHint)
            ? Visibility.Collapsed
            : Visibility.Visible;

    public static CommandPaletteSuggestion Recent(string command) => new(
        command,
        command,
        "Usado recientemente",
        "↺",
        string.Empty,
        [command]);
}

public partial class CommandPaletteWindow : Window
{
    private static readonly CommandPaletteSuggestion[] BuiltInSuggestions =
    [
        new(
            "Explorador de archivos",
            "abre el explorador de archivos",
            "Abrir tus carpetas en Windows",
            "▣",
            "E",
            ["e", "explorador", "archivos", "carpetas", "windows explorer"]),
        new(
            "Descargas",
            "abre descargas",
            "Abrir la carpeta Descargas",
            "↓",
            "D",
            ["d", "descargas", "downloads", "carpeta"]),
        new(
            "Visual Studio Code",
            "abre Visual Studio Code",
            "Abrir el editor de código",
            "⌘",
            "V",
            ["v", "visual studio code", "vscode", "code", "editor"]),
        new(
            "Calculadora",
            "abre la calculadora",
            "Abrir la calculadora de Windows",
            "±",
            "C",
            ["c", "calculadora", "calc", "cuentas"]),
        new(
            "Administrador de tareas",
            "abre el administrador de tareas",
            "Revisar procesos y rendimiento",
            "◎",
            "T",
            ["t", "administrador de tareas", "task manager", "procesos"]),
        new(
            "PowerShell",
            "abre PowerShell",
            "Abrir una terminal en tu carpeta personal",
            ">_",
            "P",
            ["p", "powershell", "terminal", "consola"]),
        new(
            "Mirar lo que está abierto",
            "mira esta ventana",
            "Usar contexto visual temporal de la ventana activa",
            "◫",
            "M",
            ["m", "mirar", "captura", "vision", "ventana"]),
        new(
            "Enfoque de 25 minutos",
            "inicia enfoque durante 25 minutos",
            "Empezar una sesión de concentración",
            "◉",
            "F",
            ["f", "focus", "enfoque", "pomodoro", "25 minutos"]),
        new(
            "Spotify al 20 %",
            "baja Spotify al 20 %",
            "Ajustar el audio localmente",
            "♪",
            string.Empty,
            ["spotify", "audio", "volumen", "20"]),
        new(
            "Pendientes de hoy",
            "¿Qué tengo pendiente hoy?",
            "Consultar tareas y recordatorios",
            "✓",
            string.Empty,
            ["pendientes", "hoy", "tareas", "recordatorios"]),
        new(
            "Rutina de estudio",
            "ejecuta mi rutina de estudio",
            "Ejecutar una rutina guardada",
            "↻",
            string.Empty,
            ["rutina", "estudio", "automatizacion"])
    ];

    private readonly CommandPaletteStateStore _stateStore = new();
    private readonly CommandPaletteState _state;
    private bool _isExpanded;
    private bool _customizationVisible;
    private bool _isHiding;
    private bool _shellAnimationsEnabled = true;
    private bool _isApplyingCompletion;
    private bool _selectionExplicit;
    private CommandPaletteSuggestion? _activeCompletionSuggestion;

    public CommandPaletteWindow()
    {
        InitializeComponent();
        _state = _stateStore.Load();
        NormalizeRecentHistory();
        ReduceMotionCheckBox.IsChecked = _state.ReduceMotion;
        UpdateMotionSelection();
        RefreshSuggestions(string.Empty);
    }

    public event EventHandler<CommandPalettePromptEventArgs>? PromptSubmitted;

    public event EventHandler? WorkspaceRequested;

    public void ShowPalette(bool shellAnimationsEnabled)
    {
        _shellAnimationsEnabled = shellAnimationsEnabled;
        _isHiding = false;
        _customizationVisible = false;
        CustomizationSurface.Visibility = Visibility.Collapsed;
        _selectionExplicit = false;
        _activeCompletionSuggestion = null;
        PromptTextBox.Text = string.Empty;
        PlaceholderText.Visibility = Visibility.Visible;
        InputModeText.Text = "Escribe para buscar o preguntar";
        InputModeDot.Fill = (Brush)FindResource("BrushTextTertiary");
        SetExpanded(false, animate: false);
        PositionPalette();

        var stopwatch = Stopwatch.StartNew();
        if (!IsVisible)
        {
            Show();
        }

        Activate();
        Topmost = true;
        PromptTextBox.Focus();
        Keyboard.Focus(PromptTextBox);

        ApplyShowMotion();
        Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(() =>
            {
                stopwatch.Stop();
                TimingText.Text = $"Listo en {stopwatch.ElapsedMilliseconds} ms";
                WritePerformanceSample(stopwatch.ElapsedMilliseconds);
            }));
    }

    public void HidePalette(bool immediate = false)
    {
        if (!IsVisible || _isHiding)
        {
            return;
        }

        if (immediate || !ShouldAnimate())
        {
            HideImmediately();
            return;
        }

        _isHiding = true;
        var settings = ResolveMotionSettings(isHiding: true);
        var easing = new CubicEase { EasingMode = EasingMode.EaseIn };
        var duration = TimeSpan.FromMilliseconds(settings.DurationMilliseconds);

        var opacityAnimation = new DoubleAnimation(0, duration)
        {
            EasingFunction = easing
        };
        opacityAnimation.Completed += (_, _) => HideImmediately();

        RootBorder.BeginAnimation(OpacityProperty, opacityAnimation);
        PaletteTranslate.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation(settings.Offset, duration)
            {
                EasingFunction = easing
            });
    }

    private void HideImmediately()
    {
        RootBorder.BeginAnimation(OpacityProperty, null);
        PaletteTranslate.BeginAnimation(TranslateTransform.YProperty, null);
        RootBorder.Opacity = 0;
        PaletteTranslate.Y = -8;
        _isHiding = false;
        Hide();
    }

    private void ApplyShowMotion()
    {
        RootBorder.BeginAnimation(OpacityProperty, null);
        PaletteTranslate.BeginAnimation(TranslateTransform.YProperty, null);

        if (!ShouldAnimate())
        {
            RootBorder.Opacity = 1;
            PaletteTranslate.Y = 0;
            return;
        }

        var settings = ResolveMotionSettings(isHiding: false);
        var duration = TimeSpan.FromMilliseconds(settings.DurationMilliseconds);
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

        RootBorder.Opacity = 0;
        PaletteTranslate.Y = -settings.Offset;
        RootBorder.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(1, duration)
            {
                EasingFunction = easing
            });
        PaletteTranslate.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation(0, duration)
            {
                EasingFunction = easing
            });
    }

    private void PositionPalette()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + Math.Max(18, (workArea.Width - Width) / 2);
        Top = workArea.Top + Math.Max(30, Math.Min(136, workArea.Height * 0.14));
    }

    private void PromptTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        PlaceholderText.Visibility = string.IsNullOrEmpty(PromptTextBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (_isApplyingCompletion)
        {
            return;
        }

        _selectionExplicit = false;
        _activeCompletionSuggestion = null;

        var query = PromptTextBox.Text.Trim();
        RefreshSuggestions(query);

        if (string.IsNullOrWhiteSpace(query))
        {
            InputModeText.Text = "Escribe para buscar o preguntar";
            InputModeDot.Fill = (Brush)FindResource("BrushTextTertiary");
            return;
        }

        SetExpanded(true, animate: true);
        UpdateInputMode();
    }

    private void RefreshSuggestions(string query)
    {
        var exactBuiltInMatch = BuiltInSuggestions.Any(item =>
            CommandPaletteInputPolicy.IsExactSuggestionMatch(
                query,
                item.Title,
                item.Command,
                item.Keywords));

        if (!exactBuiltInMatch &&
            CommandPaletteInputPolicy.IsLikelyNaturalPrompt(query))
        {
            var promptSuggestion = new CommandPaletteSuggestion(
                "Preguntar a Sakura",
                query.Trim(),
                "Enviar esta consulta exactamente como la escribiste",
                "✦",
                "Enter",
                [query],
                IsPrompt: true);
            SuggestionsList.ItemsSource = new[] { promptSuggestion };
            SuggestionsList.SelectedIndex = 0;
            return;
        }

        var recent = _state.RecentCommands
            .Select(CommandPaletteSuggestion.Recent)
            .ToArray();
        var builtInCommands = BuiltInSuggestions
            .Select(item => item.Command)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var recentCommands = recent
            .Select(item => item.Command)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        IEnumerable<CommandPaletteSuggestion> combined =
            string.IsNullOrWhiteSpace(query)
                ? recent.Concat(BuiltInSuggestions.Where(item =>
                    !recentCommands.Contains(item.Command)))
                : BuiltInSuggestions.Concat(recent.Where(item =>
                    !builtInCommands.Contains(item.Command)));

        if (!string.IsNullOrWhiteSpace(query))
        {
            combined = combined
                .Select(item => new
                {
                    Suggestion = item,
                    Score = ScoreSuggestion(item, query)
                })
                .Where(item => item.Score > 0)
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.Suggestion.Title.Length)
                .Select(item => item.Suggestion);
        }

        var suggestions = combined.Take(7).ToArray();
        SuggestionsList.ItemsSource = suggestions;
        SuggestionsList.SelectedIndex = suggestions.Length > 0 ? 0 : -1;
    }

    private static int ScoreSuggestion(
        CommandPaletteSuggestion suggestion,
        string query)
    {
        var normalizedQuery = query.Trim();
        if (normalizedQuery.Length == 0)
        {
            return 1;
        }

        if (suggestion.ShortcutHint.Equals(
                normalizedQuery,
                StringComparison.OrdinalIgnoreCase))
        {
            return 300;
        }

        if (suggestion.Title.StartsWith(
                normalizedQuery,
                StringComparison.CurrentCultureIgnoreCase))
        {
            return 220;
        }

        if (suggestion.Command.StartsWith(
                normalizedQuery,
                StringComparison.CurrentCultureIgnoreCase))
        {
            return 200;
        }

        if (suggestion.Keywords.Any(keyword => keyword.StartsWith(
                normalizedQuery,
                StringComparison.CurrentCultureIgnoreCase)))
        {
            return 170;
        }

        if (suggestion.Title.Contains(
                normalizedQuery,
                StringComparison.CurrentCultureIgnoreCase) ||
            suggestion.Command.Contains(
                normalizedQuery,
                StringComparison.CurrentCultureIgnoreCase))
        {
            return 120;
        }

        var queryWords = normalizedQuery.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var searchable = suggestion.Title + " " + suggestion.Command + " " +
            string.Join(" ", suggestion.Keywords);
        var matches = queryWords.Count(word => searchable.Contains(
            word,
            StringComparison.CurrentCultureIgnoreCase));
        return matches == 0 ? 0 : 40 + matches * 20;
    }

    private void TryApplyShortcutCompletion(string query)
    {
        if (CommandPaletteInputPolicy.IsLikelyNaturalPrompt(query) ||
            SuggestionsList.SelectedItem is not CommandPaletteSuggestion selected)
        {
            return;
        }

        if (query.Length == 1 &&
            selected.ShortcutHint.Equals(query, StringComparison.OrdinalIgnoreCase))
        {
            ApplyCompletion(selected, selected.Title, query.Length);
            return;
        }

        if (query.Length < 2 || query.Length > 18)
        {
            return;
        }

        if (selected.Title.StartsWith(
                query,
                StringComparison.CurrentCultureIgnoreCase))
        {
            ApplyCompletion(selected, selected.Title, query.Length);
            return;
        }

        if (selected.Command.StartsWith(
                query,
                StringComparison.CurrentCultureIgnoreCase))
        {
            ApplyCompletion(selected, selected.Command, query.Length);
            return;
        }

        var actionQuery = query.StartsWith(
            "abre ",
            StringComparison.CurrentCultureIgnoreCase)
            ? query[5..].TrimStart()
            : query;

        if (actionQuery.Length >= 2 &&
            (selected.Title.StartsWith(
                 actionQuery,
                 StringComparison.CurrentCultureIgnoreCase) ||
             selected.Keywords.Any(keyword => keyword.StartsWith(
                 actionQuery,
                 StringComparison.CurrentCultureIgnoreCase))))
        {
            ApplyCompletion(selected, selected.Command, preservedPrefixLength: 0);
        }
    }

    private void ApplyCompletion(
        CommandPaletteSuggestion suggestion,
        string? completionText = null,
        int preservedPrefixLength = 0)
    {
        _isApplyingCompletion = true;
        _activeCompletionSuggestion = suggestion;
        try
        {
            var text = string.IsNullOrWhiteSpace(completionText)
                ? suggestion.Title
                : completionText;
            PromptTextBox.Text = text;
            var prefixLength = Math.Clamp(
                preservedPrefixLength,
                0,
                text.Length);
            PromptTextBox.Select(
                prefixLength,
                text.Length - prefixLength);
        }
        finally
        {
            _isApplyingCompletion = false;
        }
    }

    private void NormalizeRecentHistory()
    {
        var normalized = _state.RecentCommands
            .Select(NormalizeRecentCommand)
            .Where(command => !string.IsNullOrWhiteSpace(command))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToList();

        if (_state.RecentCommands.SequenceEqual(
                normalized,
                StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        _state.RecentCommands = normalized;
        _stateStore.Save(_state);
    }

    private static string NormalizeRecentCommand(string command)
    {
        var trimmed = command.Trim();
        if (CommandPaletteInputPolicy.IsLikelyNaturalPrompt(trimmed))
        {
            return trimmed;
        }

        var best = BuiltInSuggestions
            .Select(suggestion => new
            {
                Suggestion = suggestion,
                Score = ScoreSuggestion(suggestion, trimmed)
            })
            .OrderByDescending(candidate => candidate.Score)
            .FirstOrDefault();

        return best is not null && best.Score >= 170
            ? best.Suggestion.Command
            : trimmed;
    }

    private void CustomizeButton_Click(object sender, RoutedEventArgs e)
    {
        _customizationVisible = !_customizationVisible;
        CustomizationSurface.Visibility = _customizationVisible
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (_customizationVisible)
        {
            AnimateSurfaceIn(CustomizationSurface);
        }

        PromptTextBox.Focus();
    }

    private void SetExpanded(bool expanded, bool animate)
    {
        if (_isExpanded == expanded && ExpandedSurface.Visibility ==
            (expanded ? Visibility.Visible : Visibility.Collapsed))
        {
            return;
        }

        _isExpanded = expanded;
        ExpandedSurface.Visibility = expanded
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (expanded && animate)
        {
            AnimateSurfaceIn(ExpandedSurface);
        }
    }

    private void AnimateSurfaceIn(UIElement surface)
    {
        if (!ShouldAnimate())
        {
            surface.Opacity = 1;
            return;
        }

        surface.Opacity = 0;
        surface.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(1, TimeSpan.FromMilliseconds(120))
            {
                EasingFunction = new CubicEase
                {
                    EasingMode = EasingMode.EaseOut
                }
            });
    }

    private void SuggestionsList_PreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        _selectionExplicit = true;
        _activeCompletionSuggestion = null;
    }

    private void SuggestionsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        SubmitSelectedSuggestion();
    }

    private void SuggestionsList_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        UpdateInputMode();
    }

    private void UpdateInputMode()
    {
        var query = PromptTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            InputModeText.Text = "Escribe para buscar o preguntar";
            InputModeDot.Fill = (Brush)FindResource("BrushTextTertiary");
            return;
        }

        if (SuggestionsList.SelectedItem is CommandPaletteSuggestion selected)
        {
            if (selected.IsPrompt)
            {
                InputModeText.Text = "Consulta · Enter para enviar";
                InputModeDot.Fill = (Brush)FindResource("BrushSuccess");
                return;
            }

            var score = ScoreSuggestion(selected, query);
            var execute = CommandPaletteInputPolicy.ShouldExecuteSuggestion(
                query,
                selected.Title,
                selected.Command,
                selected.Keywords,
                score,
                ReferenceEquals(_activeCompletionSuggestion, selected),
                _selectionExplicit);

            if (execute)
            {
                InputModeText.Text = "Acción local · Enter para ejecutar";
                InputModeDot.Fill = (Brush)FindResource("BrushAccent");
                return;
            }
        }

        InputModeText.Text = "Consulta · Enter para enviar";
        InputModeDot.Fill = (Brush)FindResource("BrushSuccess");
    }

    private bool ShouldSubmitSelectedSuggestion()
    {
        if (SuggestionsList.SelectedItem is not CommandPaletteSuggestion selected)
        {
            return false;
        }

        if (selected.IsPrompt)
        {
            return true;
        }

        var query = PromptTextBox.Text.Trim();
        var score = ScoreSuggestion(selected, query);
        return CommandPaletteInputPolicy.ShouldExecuteSuggestion(
            query,
            selected.Title,
            selected.Command,
            selected.Keywords,
            score,
            ReferenceEquals(_activeCompletionSuggestion, selected),
            _selectionExplicit);
    }

    private void SubmitSelectedSuggestion()
    {
        if (SuggestionsList.SelectedItem is CommandPaletteSuggestion selected)
        {
            SubmitPrompt(selected.Command);
        }
    }

    private void SubmitPrompt(string? preferredPrompt = null)
    {
        var prompt = string.IsNullOrWhiteSpace(preferredPrompt)
            ? PromptTextBox.Text.Trim()
            : preferredPrompt.Trim();

        if (string.IsNullOrWhiteSpace(prompt))
        {
            return;
        }

        _stateStore.Remember(_state, prompt);
        PromptSubmitted?.Invoke(this, new CommandPalettePromptEventArgs(prompt));
        HidePalette();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (!string.IsNullOrEmpty(PromptTextBox.Text))
            {
                PromptTextBox.Clear();
                SetExpanded(false, animate: false);
                InputModeText.Text = "Escribe para buscar o preguntar";
                InputModeDot.Fill = (Brush)FindResource("BrushTextTertiary");
            }
            else
            {
                HidePalette();
            }

            e.Handled = true;
            return;
        }

        if (!PromptTextBox.IsKeyboardFocusWithin && !SuggestionsList.IsKeyboardFocusWithin) return;

        if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            // Shift + Enter pertenece al TextBox y agrega una línea.
            return;
        }

        if (e.Key == Key.Tab && Keyboard.Modifiers == ModifierKeys.Control && PromptTextBox.IsKeyboardFocusWithin)
        {
            if (SuggestionsList.SelectedItem is CommandPaletteSuggestion selected)
            {
                ApplyCompletion(selected, selected.Command);
                PromptTextBox.CaretIndex = PromptTextBox.Text.Length;
                UpdateInputMode();
            }
            else
            {
                SetExpanded(true, animate: true);
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            SubmitPrompt();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
        {
            WorkspaceRequested?.Invoke(this, EventArgs.Empty);
            HidePalette();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            if (SuggestionsList.IsKeyboardFocusWithin || ShouldSubmitSelectedSuggestion())
            {
                SubmitSelectedSuggestion();
            }
            else
            {
                SubmitPrompt();
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.Down && PromptTextBox.IsKeyboardFocusWithin && _isExpanded && SuggestionsList.Items.Count > 0)
        {
            _selectionExplicit = true;
            _activeCompletionSuggestion = null;
            SuggestionsList.SelectedIndex = Math.Min(
                SuggestionsList.Items.Count - 1,
                SuggestionsList.SelectedIndex + 1);
            SuggestionsList.ScrollIntoView(SuggestionsList.SelectedItem);
            UpdateInputMode();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Up && PromptTextBox.IsKeyboardFocusWithin && _isExpanded && SuggestionsList.Items.Count > 0)
        {
            _selectionExplicit = true;
            _activeCompletionSuggestion = null;
            SuggestionsList.SelectedIndex = Math.Max(
                0,
                SuggestionsList.SelectedIndex - 1);
            SuggestionsList.ScrollIntoView(SuggestionsList.SelectedItem);
            UpdateInputMode();
            e.Handled = true;
        }
    }

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        HidePalette();
    }

    private void MotionPresetButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string value } ||
            !Enum.TryParse<ShellMotionPreset>(value, ignoreCase: true, out var preset))
        {
            return;
        }

        _state.MotionPreset = preset;
        _state.ReduceMotion = preset == ShellMotionPreset.None ||
            ReduceMotionCheckBox.IsChecked == true;
        ReduceMotionCheckBox.IsChecked = _state.ReduceMotion;
        _stateStore.Save(_state);
        UpdateMotionSelection();
        ApplyShowMotion();
        PromptTextBox.Focus();
    }

    private void ReduceMotionCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        _state.ReduceMotion = ReduceMotionCheckBox.IsChecked == true;
        _stateStore.Save(_state);
        UpdateMotionSelection();
        PromptTextBox.Focus();
    }

    private void UpdateMotionSelection()
    {
        var defaultBrush = TryFindResource("BrushSurfaceRaised") as Brush;
        var selectedBrush = TryFindResource("BrushAccentSoft") as Brush;
        var buttons = new[]
        {
            FluidMotionButton,
            SnappyMotionButton,
            CalmMotionButton,
            NoMotionButton
        };

        foreach (var button in buttons)
        {
            button.Background = defaultBrush;
            button.Foreground = TryFindResource("BrushTextSecondary") as Brush;
        }

        var selected = _state.MotionPreset switch
        {
            ShellMotionPreset.Snappy => SnappyMotionButton,
            ShellMotionPreset.Calm => CalmMotionButton,
            ShellMotionPreset.None => NoMotionButton,
            _ => FluidMotionButton
        };
        selected.Background = selectedBrush;
        selected.Foreground = TryFindResource("BrushAccent") as Brush;
    }

    private bool ShouldAnimate() =>
        _shellAnimationsEnabled &&
        !_state.ReduceMotion &&
        _state.MotionPreset != ShellMotionPreset.None;

    private MotionSettings ResolveMotionSettings(bool isHiding)
    {
        return _state.MotionPreset switch
        {
            ShellMotionPreset.Snappy => new MotionSettings(
                isHiding ? 78 : 108,
                6),
            ShellMotionPreset.Calm => new MotionSettings(
                isHiding ? 180 : 240,
                8),
            ShellMotionPreset.None => new MotionSettings(0, 0),
            _ => new MotionSettings(
                isHiding ? 118 : 170,
                8)
        };
    }

    private static void WritePerformanceSample(long elapsedMilliseconds)
    {
        try
        {
            var logDirectory = Path.Combine(NexoDataPaths.RootDirectory, "Logs");
            Directory.CreateDirectory(logDirectory);
            File.AppendAllText(
                Path.Combine(logDirectory, "performance.log"),
                $"{DateTimeOffset.Now:O}\tcommand_palette_visible\t{elapsedMilliseconds}ms{Environment.NewLine}");
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            // La medición nunca debe impedir que la paleta se abra.
        }
    }

    private readonly record struct MotionSettings(
        double DurationMilliseconds,
        double Offset);
    /// <summary>
    /// Diseño D62 — el marco lo compone Windows. La paleta pinta su superficie con su propio
    /// degradado, así que aquí solo se pide el fondo del sistema.
    /// </summary>
    private void Window_SourceInitialized(object? sender, EventArgs e) =>
        SakuraWindowChrome.ApplyBackdropOnly(this);

}
