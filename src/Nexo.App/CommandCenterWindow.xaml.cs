using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Nexo.Core.Commands.CommandCenter;
using Nexo.Core.Shell;

using Nexo.App.Motion;
using Nexo.App.Shell;

namespace Nexo.App;

/// <summary>
/// Sakura Command Center (Diseño D2) — paleta de acciones locales reales, abierta con Ctrl + K.
///
/// Es distinta de <see cref="CommandPaletteWindow"/> (Ctrl + Espacio), que envía prompts de
/// lenguaje natural al asistente. Aquí no hay IA: cada entrada ejecuta una acción concreta del
/// shell o de un servicio local ya existente.
///
/// La ventana no sabe qué hace cada comando: recibe un <see cref="SakuraCommandRegistry"/> ya
/// poblado y solo se ocupa de buscar, mostrar, trasladar el teclado e informar del resultado.
/// </summary>
public partial class CommandCenterWindow : Window
{
    private SakuraCommandRegistry _registry;
    private IReadOnlyList<CommandCenterRow> _rows = [];

    /// <summary>
    /// Control que tenía el foco antes de abrir, para devolvérselo al cerrar. El encargo exige
    /// que el foco vuelva a su sitio, no que se pierda en la ventana principal.
    /// </summary>
    private IInputElement? _focusToRestore;

    public CommandCenterWindow(SakuraCommandRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
        InitializeComponent();
        KeyHintItems.ItemsSource = KeyHints;
    }

    // Las teclas que atiende Window_PreviewKeyDown.
    private static readonly CheatItem[] KeyHints =
    [
        new("↑ + ↓", "elegir"),
        new("Enter", "ejecutar"),
        new("Esc", "cerrar"),
    ];

    /// <summary>
    /// Diseño D3 — reemplaza el registro antes de mostrar la ventana. Algunos comandos son
    /// dinámicos por naturaleza (uno por rutina habilitada): reconstruir el registro cada vez que
    /// se abre, en vez de congelarlo la primera vez, evita que la paleta muestre rutinas que ya se
    /// renombraron, se deshabilitaron o se eliminaron.
    /// </summary>
    public void UpdateCommands(SakuraCommandRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
    }

    /// <summary>Se dispara cuando un comando termina en fallo, para que el shell pueda registrarlo.</summary>
    public event EventHandler<CommandCenterFailureEventArgs>? CommandFailed;

    /// <summary>
    /// Muestra la paleta sobre <paramref name="owner"/>, recordando el foco actual y dejándolo en
    /// el cuadro de búsqueda.
    /// </summary>
    public void ShowFor(Window owner, IInputElement? focusToRestore)
    {
        Owner = owner;
        _focusToRestore = focusToRestore;

        SearchBox.Text = string.Empty;
        HideStatus();
        Refresh();

        var opening = !IsVisible;
        if (opening)
        {
            Show();
        }

        Activate();

        if (opening)
        {
            PlayEntrance();
        }

        // El foco debe entrar en la búsqueda al abrir. Se hace en prioridad Input para que el
        // enfoque ocurra después de que la ventana esté realmente presentada.
        Dispatcher.BeginInvoke(
            () =>
            {
                SearchBox.Focus();
                Keyboard.Focus(SearchBox);
            },
            DispatcherPriority.Input);
    }

    /// <summary>
    /// 2026-09-15 — entra como la paleta: baja un poco inflándose desde arriba, el icono da un giro
    /// corto y las primeras filas llegan una detrás de otra. Solo al abrir; al escribir la lista se
    /// rehace a cada tecla y animarla sería un parpadeo.
    /// </summary>
    private void PlayEntrance()
    {
        Surface.BeginAnimation(OpacityProperty, null);
        SurfaceScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        SurfaceScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        SurfaceTranslate.BeginAnimation(TranslateTransform.YProperty, null);

        if (!SakuraMotion.AnimationsEnabled)
        {
            Surface.Opacity = 1;
            return;
        }

        var reveal = TimeSpan.FromMilliseconds(170);
        var settle = TimeSpan.FromMilliseconds(280);
        Surface.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, reveal) { EasingFunction = SakuraMotion.DecelerateCurve });
        SurfaceTranslate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-8, 0, reveal) { EasingFunction = SakuraMotion.DecelerateCurve });
        SurfaceScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.96, 1, settle) { EasingFunction = SakuraMotion.SubtleSpringCurve });
        SurfaceScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.96, 1, settle) { EasingFunction = SakuraMotion.SubtleSpringCurve });
        SearchIconRotation.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(-90, 0, settle) { EasingFunction = SakuraMotion.DecelerateCurve });

        ResultsList.UpdateLayout();
        for (var i = 0; i < Math.Min(6, ResultsList.Items.Count); i++)
        {
            if (ResultsList.ItemContainerGenerator.ContainerFromIndex(i) is FrameworkElement item)
            {
                EntranceMotion.Rise(item, TimeSpan.FromMilliseconds(40 + 28 * i), offset: 6);
            }
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        SearchHintText.Visibility = string.IsNullOrEmpty(SearchBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;

        HideStatus();
        Refresh();
    }

    private void Refresh()
    {
        var matches = CommandSearchEngine.Search(_registry.Commands, SearchBox.Text);

        _rows =
        [
            .. matches.Select(command =>
            {
                var availability = command.GetAvailability();
                return new CommandCenterRow(
                    command,
                    availability.IsAvailable,
                    // Un comando no disponible explica por qué en lugar de mostrar su descripción
                    // habitual: si no se puede ejecutar, lo útil es saber la razón.
                    availability.IsAvailable
                        ? command.Description
                        : availability.UnavailableReason ?? command.Description);
            })
        ];

        ResultsList.ItemsSource = _rows;
        var hasRows = _rows.Count > 0;
        ResultsList.Visibility = hasRows ? Visibility.Visible : Visibility.Collapsed;
        EmptyStateText.Visibility = hasRows ? Visibility.Collapsed : Visibility.Visible;

        SelectFirstAvailable();
    }

    private void SelectFirstAvailable()
    {
        // La selección inicial cae en el primer comando ejecutable: seleccionar uno deshabilitado
        // haría que Enter no hiciera nada sin explicación.
        for (var index = 0; index < _rows.Count; index++)
        {
            if (_rows[index].IsAvailable)
            {
                ResultsList.SelectedIndex = index;
                ResultsList.ScrollIntoView(_rows[index]);
                return;
            }
        }

        ResultsList.SelectedIndex = -1;
    }

    private void MoveSelection(int direction)
    {
        if (_rows.Count == 0)
        {
            return;
        }

        var start = ResultsList.SelectedIndex;
        var index = start;

        // Salta los comandos no disponibles y da la vuelta al llegar al extremo.
        for (var step = 0; step < _rows.Count; step++)
        {
            index += direction;
            if (index < 0)
            {
                index = _rows.Count - 1;
            }
            else if (index >= _rows.Count)
            {
                index = 0;
            }

            if (_rows[index].IsAvailable)
            {
                ResultsList.SelectedIndex = index;
                ResultsList.ScrollIntoView(_rows[index]);
                return;
            }
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                e.Handled = true;
                CloseAndRestoreFocus();
                break;

            case Key.Down when !ResultsList.IsKeyboardFocusWithin:
                e.Handled = true;
                MoveSelection(1);
                break;

            case Key.Up when !ResultsList.IsKeyboardFocusWithin:
                e.Handled = true;
                MoveSelection(-1);
                break;

            case Key.Enter:
                e.Handled = true;
                ExecuteSelected();
                break;
        }
    }

    private void ResultsList_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (ResultsList.SelectedItem is CommandCenterRow { IsAvailable: true })
        {
            ExecuteSelected();
        }
    }

    private async void ExecuteSelected()
    {
        if (ResultsList.SelectedItem is not CommandCenterRow row)
        {
            return;
        }

        if (!row.IsAvailable)
        {
            ShowStatus(row.Detail);
            return;
        }

        // Se cierra antes de ejecutar: casi todos los comandos navegan o cambian el shell, y
        // dejar la paleta encima taparía el resultado. El foco vuelve a su sitio primero para
        // que la acción se aplique sobre la ventana correcta.
        CloseAndRestoreFocus();

        var result = await row.Command.ExecuteAsync().ConfigureAwait(true);
        if (result.Succeeded)
        {
            return;
        }

        // Un fallo nunca cierra Sakura ni se informa como éxito: se avisa de forma no modal y se
        // entrega al shell para que lo registre con su stack trace.
        CommandFailed?.Invoke(
            this,
            new CommandCenterFailureEventArgs(row.Command, result));
    }

    private void CloseAndRestoreFocus()
    {
        Hide();

        var target = _focusToRestore;
        _focusToRestore = null;

        if (target is not null)
        {
            Dispatcher.BeginInvoke(() => Keyboard.Focus(target), DispatcherPriority.Input);
        }
        else
        {
            Owner?.Activate();
        }
    }

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        if (IsVisible)
        {
            CloseAndRestoreFocus();
        }
    }

    private void ShowStatus(string message)
    {
        StatusText.Text = message;
        StatusText.Visibility = Visibility.Visible;
    }

    private void HideStatus()
    {
        StatusText.Text = string.Empty;
        StatusText.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Fila mostrada en la lista. Es un tipo aparte del descriptor porque la disponibilidad y el
    /// texto visible dependen del momento en que se abre la paleta, no del registro.
    /// </summary>
    public sealed record CommandCenterRow(
        SakuraCommandDescriptor Command,
        bool IsAvailable,
        string Detail)
    {
        public string Title => Command.Title;

        public override string ToString() => Title;

        public string? Shortcut => Command.Shortcut;

        public IReadOnlyList<string> ShortcutParts =>
            Command.Shortcut?.Split(" + ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];

        /// <summary>
        /// El icono propio del comando si lo trae (ir a una sección lleva el de esa sección en la
        /// barra lateral) y, si no, el de su categoría.
        /// </summary>
        public Geometry? IconData =>
            (Command.IconKey is { } key ? Application.Current?.TryFindResource(key) as Geometry : null) ?? CategoryIcon;

        private Geometry? CategoryIcon => Application.Current?.TryFindResource(Command.Category switch
        {
            SakuraCommandCategory.Navigation => "IconSakuraHome",
            SakuraCommandCategory.Focus => "IconSakuraFocus",
            SakuraCommandCategory.Tasks => "IconSakuraTasks",
            SakuraCommandCategory.Audio => "IconSakuraAudio",
            SakuraCommandCategory.Capture => "IconSakuraCapture",
            SakuraCommandCategory.System => "IconSakuraSystem",
            SakuraCommandCategory.Ambient => "IconSakuraLook",
            _ => "IconSakuraSettings"
        }) as Geometry;

        public Visibility ShortcutVisibility =>
            string.IsNullOrWhiteSpace(Command.Shortcut) ? Visibility.Collapsed : Visibility.Visible;

        public string CategoryLabel => Command.Category switch
        {
            SakuraCommandCategory.Navigation => "Ir a",
            SakuraCommandCategory.Focus => "Enfoque",
            SakuraCommandCategory.Tasks => "Tareas",
            SakuraCommandCategory.Audio => "Audio",
            SakuraCommandCategory.Capture => "Captura",
            SakuraCommandCategory.System => "Sistema",
            SakuraCommandCategory.Ambient => "Ambiental",
            _ => "Shell"
        };
    }

    /// <summary>
    /// 2026-09-15 — cristal transparente con esquinas propias, como la paleta. Sin cristal disponible
    /// (contraste alto, Windows antiguo) se vuelve al marco de DWM de siempre.
    /// </summary>
    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        if (SakuraWindowChrome.TryApplyRoundedGlass(this, Surface, 26))
        {
            return;
        }

        SakuraWindowChrome.Apply(this, Surface);
    }
}

public sealed class CommandCenterFailureEventArgs(
    SakuraCommandDescriptor command,
    CommandExecutionResult result) : EventArgs
{
    public SakuraCommandDescriptor Command { get; } = command;

    public CommandExecutionResult Result { get; } = result;
}
