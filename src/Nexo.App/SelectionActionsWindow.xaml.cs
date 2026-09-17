using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Nexo.App.Motion;
using Nexo.Core.Assistant;

namespace Nexo.App;

/// <summary>2026-09-16 — acciones sobre el texto seleccionado (Alt+Shift+E por omisión).</summary>
public partial class SelectionActionsWindow : Window
{
    private string _result = string.Empty;
    private bool _working;

    public SelectionActionsWindow()
    {
        InitializeComponent();
        foreach (var action in SelectionRewrite.All)
        {
            var button = new Button
            {
                Content = SelectionRewrite.Label(action),
                Tag = action,
                Margin = new Thickness(0, 0, 6, 6),
                Style = (Style)FindResource("SoftChipButtonStyle")
            };
            button.Click += Action_Click;
            ActionsPanel.Children.Add(button);
        }

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Hide();
            }
        };
    }

    /// <summary>Se eligió una acción; quien tiene la IA la ejecuta y va llamando a <see cref="Append"/>.</summary>
    public event EventHandler<SelectionAction>? ActionChosen;

    /// <summary>Se pidió poner el resultado en lugar de la selección.</summary>
    public event EventHandler<string>? ReplaceRequested;

    public void ShowFor(string selection)
    {
        _result = string.Empty;
        _working = false;
        HeaderText.Text = "TEXTO SELECCIONADO";
        SourceText.Text = selection.ReplaceLineEndings(" ");
        ActionsPanel.Visibility = Visibility.Visible;
        ActionsPanel.IsEnabled = true;
        ResultBorder.Visibility = Visibility.Collapsed;
        ResultActions.Visibility = Visibility.Collapsed;
        ResultText.Text = string.Empty;

        // Junto al ratón, que es donde suele estar la selección, sin salirse de la pantalla.
        GetCursorPos(out var cursor);
        var source = PresentationSource.FromVisual(Application.Current.MainWindow);
        var scale = source?.CompositionTarget?.TransformFromDevice.M11 ?? 1.0;
        var area = SystemParameters.WorkArea;
        Left = Math.Clamp(cursor.X * scale - 40, area.Left, area.Right - Width);
        Top = Math.Clamp(cursor.Y * scale + 12, area.Top, area.Bottom - 320);

        Show();
        Activate();
        (ActionsPanel.Children[0] as Button)?.Focus();

        if (SakuraMotion.AnimationsEnabled)
        {
            EntranceMotion.Rise(Card, TimeSpan.Zero, offset: 8);
        }
    }

    public void Append(string chunk)
    {
        _result += chunk;
        ResultText.Text = _result;
    }

    public void Complete(string? error)
    {
        _working = false;
        ActionsPanel.IsEnabled = true;
        if (error is not null)
        {
            HeaderText.Text = "NO SE PUDO";
            ResultText.Text = error;
            return;
        }

        _result = SelectionRewrite.Clean(_result);
        ResultText.Text = _result;
        HeaderText.Text = "LISTO";
        ResultActions.Visibility = Visibility.Visible;
        ReplaceButton.Focus();
    }

    private void Action_Click(object sender, RoutedEventArgs e)
    {
        if (_working || sender is not Button { Tag: SelectionAction action })
        {
            return;
        }

        _working = true;
        _result = string.Empty;
        HeaderText.Text = SelectionRewrite.Label(action).ToUpperInvariant() + "…";
        ActionsPanel.IsEnabled = false;
        ResultText.Text = string.Empty;
        ResultBorder.Visibility = Visibility.Visible;
        ResultActions.Visibility = Visibility.Collapsed;
        ActionChosen?.Invoke(this, action);
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(_result);
            HeaderText.Text = "COPIADO";
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            HeaderText.Text = "EL PORTAPAPELES ESTÁ OCUPADO";
        }
    }

    private void Replace_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        ReplaceRequested?.Invoke(this, _result);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Hide();

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct CursorPoint
    {
        public int X;
        public int Y;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetCursorPos(out CursorPoint point);
}
