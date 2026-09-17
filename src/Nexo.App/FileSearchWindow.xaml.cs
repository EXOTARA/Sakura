using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Nexo.App.Motion;
using Nexo.Core.Files;

namespace Nexo.App;

/// <summary>2026-09-16 — búsqueda de archivos (Alt+Shift+F).</summary>
public partial class FileSearchWindow : Window
{
    private readonly IFileSearchService _search;
    private readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private CancellationTokenSource? _pending;
    private bool _closing;

    public FileSearchWindow(IFileSearchService search)
    {
        _search = search;
        InitializeComponent();
        _debounce.Tick += async (_, _) =>
        {
            _debounce.Stop();
            await RunAsync();
        };
        Deactivated += (_, _) => Dismiss();
    }

    public void ShowAtTop()
    {
        _closing = false;
        QueryTextBox.Clear();
        ResultsList.ItemsSource = null;
        ResultsList.Visibility = Visibility.Collapsed;
        var area = SystemParameters.WorkArea;
        Left = area.Left + (area.Width - Width) / 2;
        Top = area.Top + area.Height * 0.16;
        Card.Opacity = 1;

        Show();
        Activate();
        QueryTextBox.Focus();
        Keyboard.Focus(QueryTextBox);
        if (SakuraMotion.AnimationsEnabled)
        {
            EntranceMotion.Rise(Card, TimeSpan.Zero, offset: 8);
        }
    }

    private void QueryTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        Placeholder.Visibility = QueryTextBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        _debounce.Stop();
        _debounce.Start();
    }

    private async Task RunAsync()
    {
        _pending?.Cancel();
        var query = QueryTextBox.Text;
        if (!FileSearchPolicy.CanSearch(query))
        {
            ResultsList.Visibility = Visibility.Collapsed;
            StatusText.Text = "Escribe al menos dos letras.";
            return;
        }

        var cancellation = _pending = new CancellationTokenSource();
        StatusText.Text = "Buscando…";
        try
        {
            var results = await _search.SearchAsync(query, FileSearchPolicy.MaximumResults, cancellation.Token);
            if (cancellation.IsCancellationRequested)
            {
                return;
            }

            var now = DateTimeOffset.Now;
            ResultsList.ItemsSource = results
                .Select(result => new ResultItem(result.Name, $"{FileSearchPolicy.When(result.Modified, now)} · {result.Folder}", result.FullPath))
                .ToList();
            ResultsList.Visibility = results.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            ResultsList.SelectedIndex = results.Count == 0 ? -1 : 0;
            StatusText.Text = results.Count == 0
                ? "No encontré nada. Prueba con otra palabra."
                : "Enter abre · Ctrl+Enter abre la carpeta · Esc cierra";
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void QueryTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                e.Handled = true;
                Dismiss();
                break;
            case Key.Down when ResultsList.Items.Count > 0:
                e.Handled = true;
                ResultsList.SelectedIndex = Math.Min(ResultsList.SelectedIndex + 1, ResultsList.Items.Count - 1);
                ResultsList.ScrollIntoView(ResultsList.SelectedItem);
                break;
            case Key.Up when ResultsList.Items.Count > 0:
                e.Handled = true;
                ResultsList.SelectedIndex = Math.Max(ResultsList.SelectedIndex - 1, 0);
                ResultsList.ScrollIntoView(ResultsList.SelectedItem);
                break;
            case Key.Enter when ResultsList.SelectedItem is ResultItem item:
                e.Handled = true;
                Open(item, folder: Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
                break;
        }
    }

    private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ResultsList.SelectedItem is ResultItem item)
        {
            Open(item, folder: false);
        }
    }

    private void Open(ResultItem item, bool folder)
    {
        try
        {
            var start = folder
                ? new ProcessStartInfo("explorer.exe", $"/select,\"{item.FullPath}\"")
                : new ProcessStartInfo(item.FullPath);
            start.UseShellExecute = true;
            Process.Start(start);
            Dismiss();
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            StatusText.Text = "No se pudo abrir: " + exception.Message;
        }
    }

    private void Dismiss()
    {
        if (_closing || !IsVisible)
        {
            return;
        }

        _closing = true;
        _pending?.Cancel();
        Hide();
    }

    private sealed record ResultItem(string Name, string Detail, string FullPath);
}
