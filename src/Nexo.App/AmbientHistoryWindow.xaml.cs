using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Nexo.App.Ambient;

using Nexo.App.Shell;

namespace Nexo.App;

public partial class AmbientHistoryWindow : Window
{
    public AmbientHistoryWindow()
    {
        InitializeComponent();
        ContentRendered += (_, _) => CloseButton.Focus();
    }

    /// <summary>Se dispara cuando el usuario pulsa "Deshacer" sobre una entrada del historial.</summary>
    public event EventHandler<Guid>? UndoRequested;

    public void ShowFor(Window owner, IReadOnlyList<AmbientRequestHistoryItem> items)
    {
        Owner = owner;
        Apply(items);

        if (!IsVisible)
        {
            Show();
        }

        Activate();
        CloseButton.Focus();
    }

    public void Apply(IReadOnlyList<AmbientRequestHistoryItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        HistoryItemsControl.ItemsSource = items.Select(item => new HistoryRow(item)).ToArray();
        EmptyStateText.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Hide();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Hide();
            e.Handled = true;
        }
    }

    private void UndoButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: Guid requestId })
        {
            UndoRequested?.Invoke(this, requestId);
        }
    }

    private sealed class HistoryRow(AmbientRequestHistoryItem item)
    {
        public Guid Id => item.Id;

        public string Prompt => item.Prompt;

        // UIA usa este texto para nombrar la fila, no el tipo interno.
        public override string ToString() => Prompt;

        public string StatusLabel => item.StatusLabel;

        public string TimestampText => item.TimestampText;

        public string? ResultText => item.ResultText;

        public Visibility ResultVisibility =>
            string.IsNullOrEmpty(item.ResultText) ? Visibility.Collapsed : Visibility.Visible;

        public Visibility UndoButtonVisibility =>
            item.CanUndo && !item.Undone ? Visibility.Visible : Visibility.Collapsed;
    }
    /// <summary>Diseño D62 — el marco lo compone Windows. Ver <see cref="SakuraWindowChrome"/>.</summary>
    private void Window_SourceInitialized(object? sender, EventArgs e) =>
        SakuraWindowChrome.Apply(this, Surface);

}
