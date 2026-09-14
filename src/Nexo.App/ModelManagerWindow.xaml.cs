using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Windows;
using Nexo.Core.Ai;
using Nexo.Windows.Ai;

namespace Nexo.App;

public partial class ModelManagerWindow : Window
{
    private readonly string _baseUrl;
    private readonly string _currentModel;
    private readonly OllamaModelService _modelService = new();
    private readonly ObservableCollection<OllamaModelInfo> _models = [];
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private CancellationTokenSource? _operationCancellation;
    private bool _isBusy;

    public ModelManagerWindow(string baseUrl, string currentModel)
    {
        InitializeComponent();
        ContentRendered += (_, _) => ModelNameTextBox.Focus();
        _baseUrl = string.IsNullOrWhiteSpace(baseUrl)
            ? "http://127.0.0.1:11434/v1"
            : baseUrl;
        _currentModel = currentModel ?? string.Empty;
        ModelsListBox.ItemsSource = _models;
    }

    public string? SelectedModel { get; private set; }

    private async void Window_Loaded(object sender, RoutedEventArgs e) =>
        await RefreshModelsAsync();

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) =>
        await RefreshModelsAsync();

    private async Task RefreshModelsAsync()
    {
        if (_isBusy)
        {
            return;
        }

        SetBusy(true, "Consultando modelos instalados…");
        try
        {
            var models = await _modelService.ListAsync(_baseUrl, _lifetimeCancellation.Token);
            _models.Clear();
            foreach (var model in models)
            {
                _models.Add(model);
            }

            StatusText.Text = models.Count == 0
                ? "Ollama está conectado, pero todavía no hay modelos instalados."
                : $"{models.Count} modelo(s) instalado(s).";

            var current = _models.FirstOrDefault(model =>
                model.Name.Equals(_currentModel, StringComparison.OrdinalIgnoreCase));
            if (current is not null)
            {
                ModelsListBox.SelectedItem = current;
            }
        }
        catch (HttpRequestException exception)
        {
            StatusText.Text = $"No pude conectar con Ollama: {exception.Message}";
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "La operación fue cancelada.";
        }
        catch (Exception exception)
        {
            StatusText.Text = $"No pude leer los modelos: {exception.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        var model = OllamaModelName.Normalize(ModelNameTextBox.Text);
        if (model is null)
        {
            DownloadStatusText.Text = "Usa un nombre como qwen3.5:4b o gemma3:4b.";
            return;
        }

        _operationCancellation?.Dispose();
        _operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            _lifetimeCancellation.Token);
        DownloadProgressBar.Visibility = Visibility.Visible;
        DownloadProgressBar.IsIndeterminate = true;
        DownloadProgressBar.Value = 0;
        SetBusy(true, $"Descargando {model}…");

        var progress = new Progress<OllamaPullProgress>(update =>
        {
            DownloadStatusText.Text = update.Status;
            if (update.Percentage is double percentage)
            {
                DownloadProgressBar.IsIndeterminate = false;
                DownloadProgressBar.Value = percentage;
                DownloadStatusText.Text = $"{update.Status} · {percentage:0}%";
            }
        });

        try
        {
            var result = await _modelService.PullAsync(
                _baseUrl,
                model,
                progress,
                _operationCancellation.Token);
            DownloadStatusText.Text = result.Detail;
            StatusText.Text = result.Detail;
            if (result.Success)
            {
                await RefreshModelsAfterBusyAsync(model);
            }
        }
        finally
        {
            DownloadProgressBar.IsIndeterminate = false;
            SetBusy(false);
        }
    }

    private async Task RefreshModelsAfterBusyAsync(string preferredModel)
    {
        try
        {
            var models = await _modelService.ListAsync(_baseUrl, _lifetimeCancellation.Token);
            _models.Clear();
            foreach (var item in models)
            {
                _models.Add(item);
            }

            ModelsListBox.SelectedItem = _models.FirstOrDefault(item =>
                item.Name.Equals(preferredModel, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            // El mensaje de descarga ya informa el resultado; actualizar puede reintentarse.
        }
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy || ModelsListBox.SelectedItem is not OllamaModelInfo selected)
        {
            return;
        }

        var confirmation = MessageBox.Show(
            this,
            $"¿Eliminar {selected.Name} del equipo?",
            "Eliminar modelo",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        SetBusy(true, $"Eliminando {selected.Name}…");
        try
        {
            var result = await _modelService.DeleteAsync(
                _baseUrl,
                selected.Name,
                _lifetimeCancellation.Token);
            StatusText.Text = result.Detail;
            if (result.Success)
            {
                _models.Remove(selected);
            }
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ModelsListBox_SelectionChanged(
        object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        var hasSelection = ModelsListBox.SelectedItem is OllamaModelInfo;
        UseButton.IsEnabled = hasSelection && !_isBusy;
        DeleteButton.IsEnabled = hasSelection && !_isBusy;
    }

    private void UseButton_Click(object sender, RoutedEventArgs e)
    {
        if (ModelsListBox.SelectedItem is not OllamaModelInfo selected)
        {
            return;
        }

        SelectedModel = selected.Name;
        DialogResult = true;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void SetBusy(bool busy, string? status = null)
    {
        _isBusy = busy;
        InstallButton.IsEnabled = !busy;
        ModelsListBox.IsEnabled = !busy;
        UseButton.IsEnabled = !busy && ModelsListBox.SelectedItem is OllamaModelInfo;
        DeleteButton.IsEnabled = !busy && ModelsListBox.SelectedItem is OllamaModelInfo;
        if (!string.IsNullOrWhiteSpace(status))
        {
            StatusText.Text = status;
        }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        _lifetimeCancellation.Cancel();
        _operationCancellation?.Cancel();
        _operationCancellation?.Dispose();
        _operationCancellation = null;
        _modelService.Dispose();
        _lifetimeCancellation.Dispose();
    }
}
