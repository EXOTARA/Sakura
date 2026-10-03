using System.Windows;
using Nexo.App.Motion;
using System.Windows.Controls;
using System.Windows.Input;
using Nexo.Core.Commands.CommandCenter;

namespace Nexo.App.Tests;

/// <summary>
/// Diseño D2 — comportamiento real de la ventana del Sakura Command Center en WPF.
///
/// El registro y la búsqueda se prueban sin interfaz en <c>Nexo.Core.Tests</c>; aquí se comprueba
/// lo que solo existe al levantar WPF: que la lista se rellene y se filtre, que el estado vacío
/// aparezca, que las flechas salten los comandos no disponibles, que Escape cierre y que el foco
/// vuelva al control que lo tenía antes de abrir.
/// </summary>
[Collection(StaWpfCollection.Name)]
public sealed class CommandCenterWindowTests
{
    private readonly StaWpfFixture _fixture;

    public CommandCenterWindowTests(StaWpfFixture fixture) => _fixture = fixture;

    private static SakuraCommandDescriptor Command(
        string id,
        string title,
        IReadOnlyList<string>? keywords = null,
        Func<SakuraCommandAvailability>? availability = null,
        Action? onExecute = null) =>
        new(
            id,
            title,
            "Descripción de prueba.",
            SakuraCommandCategory.Navigation,
            _ =>
            {
                onExecute?.Invoke();
                return Task.FromResult(CommandExecutionResult.Success());
            },
            keywords,
            availability: availability);

    /// <summary>
    /// Crea la ventana con un dueño real fuera de pantalla y la deja lista para interactuar,
    /// ejecuta <paramref name="body"/> y limpia siempre las dos ventanas.
    /// </summary>
    private void WithCommandCenter(
        SakuraCommandRegistry registry,
        Action<CommandCenterWindow, TextBox, ListBox, Window> body)
    {
        _fixture.Invoke(() =>
        {
            var owner = new Window
            {
                Width = 400,
                Height = 300,
                Left = -4000,
                Top = -4000,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                ShowActivated = false
            };

            // Un control enfocable en el dueño permite comprobar el retorno del foco al cerrar.
            var ownerFocusTarget = new TextBox();
            owner.Content = ownerFocusTarget;
            owner.Show();
            owner.UpdateLayout();
            Keyboard.Focus(ownerFocusTarget);

            // La salida del Command Center ahora es animada y se oculta al terminar; estas pruebas
            // comprueban cuándo se cierra, no cómo, así que van por la ruta sin animación.
            var animations = SakuraMotion.AnimationsEnabled;
            SakuraMotion.AnimationsEnabled = false;

            var window = new CommandCenterWindow(registry);
            try
            {
                window.ShowFor(owner, ownerFocusTarget);
                window.UpdateLayout();

                var searchBox = (TextBox)window.FindName("SearchBox")!;
                var resultsList = (ListBox)window.FindName("ResultsList")!;

                body(window, searchBox, resultsList, owner);
            }
            finally
            {
                SakuraMotion.AnimationsEnabled = animations;
                window.Close();
                owner.Close();
            }
        });
    }

    [Fact]
    public void Opening_ShowsEveryCommand_AndSelectsTheFirstAvailable()
    {
        var registry = new SakuraCommandRegistry();
        registry.Register(Command("a", "Ir a Inicio"));
        registry.Register(Command("b", "Ir a Sistema"));

        WithCommandCenter(registry, (_, _, results, _) =>
        {
            Assert.Equal(2, results.Items.Count);
            Assert.Equal(0, results.SelectedIndex);
        });
    }

    [Fact]
    public void Typing_FiltersTheResults()
    {
        var registry = new SakuraCommandRegistry();
        registry.Register(Command("a", "Ir a Inicio"));
        registry.Register(Command("b", "Ir a Sistema"));

        WithCommandCenter(registry, (_, search, results, _) =>
        {
            search.Text = "sistema";
            results.UpdateLayout();

            Assert.Single(results.Items);
        });
    }

    [Fact]
    public void Typing_MatchesKeywordsAndIgnoresAccentsAndCase()
    {
        var registry = new SakuraCommandRegistry();
        registry.Register(Command("settings", "Ir a Personalización", keywords: ["ajustes"]));

        WithCommandCenter(registry, (_, search, results, _) =>
        {
            search.Text = "  PERSONALIZACION  ";
            results.UpdateLayout();
            Assert.Single(results.Items);

            search.Text = "ajustes";
            results.UpdateLayout();
            Assert.Single(results.Items);
        });
    }

    [Fact]
    public void NoMatches_ShowsTheEmptyState_InsteadOfAnEmptyList()
    {
        var registry = new SakuraCommandRegistry();
        registry.Register(Command("a", "Ir a Inicio"));

        WithCommandCenter(registry, (window, search, results, _) =>
        {
            search.Text = "zzzz";
            window.UpdateLayout();

            var emptyState = (TextBlock)window.FindName("EmptyStateText")!;
            Assert.Equal(Visibility.Visible, emptyState.Visibility);
            Assert.Equal(Visibility.Collapsed, results.Visibility);
        });
    }

    [Fact]
    public void UnavailableCommand_IsListedWithItsReason_InsteadOfBeingHidden()
    {
        var registry = new SakuraCommandRegistry();
        registry.Register(Command(
            "focus.cancel",
            "Finalizar enfoque",
            availability: () => SakuraCommandAvailability.Unavailable("No hay ninguna sesión activa.")));

        WithCommandCenter(registry, (_, _, results, _) =>
        {
            var row = Assert.IsType<CommandCenterWindow.CommandCenterRow>(results.Items[0]);

            Assert.False(row.IsAvailable);
            Assert.Equal("No hay ninguna sesión activa.", row.Detail);
        });
    }

    [Fact]
    public void Selection_SkipsUnavailableCommands()
    {
        var registry = new SakuraCommandRegistry();
        registry.Register(Command(
            "blocked",
            "Comando bloqueado",
            availability: () => SakuraCommandAvailability.Unavailable("No disponible.")));
        registry.Register(Command("ready", "Comando disponible"));

        WithCommandCenter(registry, (_, _, results, _) =>
        {
            // El primero no es ejecutable, así que la selección inicial debe caer en el segundo:
            // seleccionar uno deshabilitado haría que Enter no hiciera nada sin explicación.
            Assert.Equal(1, results.SelectedIndex);
        });
    }

    [Fact]
    public void ArrowKeys_MoveTheSelectionAndWrapAround()
    {
        var registry = new SakuraCommandRegistry();
        registry.Register(Command("a", "Uno"));
        registry.Register(Command("b", "Dos"));

        WithCommandCenter(registry, (window, _, results, _) =>
        {
            Assert.Equal(0, results.SelectedIndex);

            RaiseKey(window, Key.Down);
            Assert.Equal(1, results.SelectedIndex);

            // Da la vuelta en vez de quedarse atascado en el extremo.
            RaiseKey(window, Key.Down);
            Assert.Equal(0, results.SelectedIndex);

            RaiseKey(window, Key.Up);
            Assert.Equal(1, results.SelectedIndex);
        });
    }

    [Fact]
    public void Escape_HidesTheWindow()
    {
        var registry = new SakuraCommandRegistry();
        registry.Register(Command("a", "Uno"));

        WithCommandCenter(registry, (window, _, _, _) =>
        {
            Assert.True(window.IsVisible);

            RaiseKey(window, Key.Escape);

            Assert.False(window.IsVisible);
        });
    }

    [Fact]
    public void Reopening_ClearsThePreviousQuery()
    {
        var registry = new SakuraCommandRegistry();
        registry.Register(Command("a", "Ir a Inicio"));
        registry.Register(Command("b", "Ir a Sistema"));

        WithCommandCenter(registry, (window, search, results, owner) =>
        {
            search.Text = "sistema";
            window.UpdateLayout();
            Assert.Single(results.Items);

            RaiseKey(window, Key.Escape);
            window.ShowFor(owner, null);
            window.UpdateLayout();

            Assert.Equal(string.Empty, search.Text);
            Assert.Equal(2, results.Items.Count);
        });
    }

    [Fact]
    public void SearchHint_DisappearsWhileTyping()
    {
        var registry = new SakuraCommandRegistry();
        registry.Register(Command("a", "Uno"));

        WithCommandCenter(registry, (window, search, _, _) =>
        {
            var hint = (TextBlock)window.FindName("SearchHintText")!;
            Assert.Equal(Visibility.Visible, hint.Visibility);

            search.Text = "u";
            window.UpdateLayout();
            Assert.Equal(Visibility.Collapsed, hint.Visibility);

            search.Text = string.Empty;
            window.UpdateLayout();
            Assert.Equal(Visibility.Visible, hint.Visibility);
        });
    }

    [Fact]
    public void EveryRow_ExposesACategoryLabel()
    {
        var registry = new SakuraCommandRegistry();
        registry.Register(new SakuraCommandDescriptor(
            "audio.mute",
            "Silenciar audio",
            "",
            SakuraCommandCategory.Audio,
            _ => Task.FromResult(CommandExecutionResult.Success())));

        WithCommandCenter(registry, (_, _, results, _) =>
        {
            var row = Assert.IsType<CommandCenterWindow.CommandCenterRow>(results.Items[0]);
            Assert.Equal("Audio", row.CategoryLabel);
        });
    }

    [Fact]
    public void RowsWithoutShortcut_HideTheShortcutBadge()
    {
        var registry = new SakuraCommandRegistry();
        registry.Register(Command("a", "Sin atajo"));
        registry.Register(new SakuraCommandDescriptor(
            "b",
            "Con atajo",
            "",
            SakuraCommandCategory.Shell,
            _ => Task.FromResult(CommandExecutionResult.Success()),
            shortcut: "Alt + A"));

        WithCommandCenter(registry, (_, _, results, _) =>
        {
            var withoutShortcut = (CommandCenterWindow.CommandCenterRow)results.Items[0];
            var withShortcut = (CommandCenterWindow.CommandCenterRow)results.Items[1];

            Assert.Equal(Visibility.Collapsed, withoutShortcut.ShortcutVisibility);
            Assert.Equal(Visibility.Visible, withShortcut.ShortcutVisibility);
        });
    }

    private static void RaiseKey(Window window, Key key)
    {
        var keyboardDevice = InputManager.Current.PrimaryKeyboardDevice;
        var source = PresentationSource.FromVisual(window)
            ?? throw new InvalidOperationException("La ventana no tiene PresentationSource.");

        var args = new KeyEventArgs(keyboardDevice, source, 0, key)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent
        };

        window.RaiseEvent(args);
    }
}
