using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Nexo.App.Motion;
using Nexo.App.Views.Controls;
using Nexo.Core.Ai;
using Nexo.Windows.Ai;

namespace Nexo.App;

public partial class ModelManagerWindow : Window
{
    private readonly string _baseUrl;
    private readonly string _currentModel;
    private readonly OllamaModelService _modelService = new();
    private readonly ObservableCollection<ModelRow> _models = [];
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private CancellationTokenSource? _operationCancellation;
    private bool _isBusy;
    private double? _downloadFraction;

    public ModelManagerWindow(string baseUrl, string currentModel)
    {
        InitializeComponent();
        ContentRendered += (_, _) =>
        {
            ModelNameTextBox.Focus();
            EntranceMotion.Rise(HeaderPanel, TimeSpan.Zero, offset: 6);
            EntranceMotion.Rise(DownloadCard, SakuraMotion.StaggerAt(2), offset: 8);
        };
        PreviewKeyDown += Window_PreviewKeyDown;
        _baseUrl = string.IsNullOrWhiteSpace(baseUrl)
            ? "http://127.0.0.1:11434/v1"
            : baseUrl;
        _currentModel = currentModel ?? string.Empty;
        ModelsListBox.ItemsSource = _models;
    }

    public string? SelectedModel { get; private set; }

    private ModelRow? SelectedRow => ModelsListBox.SelectedItem as ModelRow;

    private void Window_SourceInitialized(object? sender, EventArgs e) =>
        Shell.SakuraWindowChrome.MatchCaptionToTheme(this);

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

        HideDeleteConfirmation();
        SetBusy(true);
        SetStatus(StatusTone.Working, "Consultando los modelos instalados…");
        try
        {
            var models = await _modelService.ListAsync(_baseUrl, _lifetimeCancellation.Token);
            Fill(models, _currentModel);

            SetStatus(
                models.Count == 0 ? StatusTone.Attention : StatusTone.Ready,
                models.Count switch
                {
                    0 => "Ollama está conectado",
                    1 => "Ollama está conectado · 1 modelo",
                    _ => $"Ollama está conectado · {models.Count} modelos"
                });
            ShowEmptyState(models.Count == 0, connected: true);
            StaggerRows();
        }
        catch (HttpRequestException exception)
        {
            SetStatus(StatusTone.Problem, "No pude conectar con Ollama");
            ShowEmptyState(true, connected: false, detail: exception.Message);
        }
        catch (OperationCanceledException)
        {
            SetStatus(StatusTone.Attention, "La consulta fue cancelada");
        }
        catch (Exception exception)
        {
            SetStatus(StatusTone.Problem, "No pude leer los modelos");
            ShowEmptyState(_models.Count == 0, connected: false, detail: exception.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void Fill(IReadOnlyList<OllamaModelInfo> models, string? preferred)
    {
        _models.Clear();
        foreach (var model in models)
        {
            _models.Add(new ModelRow(model, model.Name.Equals(_currentModel, StringComparison.OrdinalIgnoreCase)));
        }

        var selected = _models.FirstOrDefault(row =>
            row.Name.Equals(preferred, StringComparison.OrdinalIgnoreCase));
        if (selected is not null)
        {
            ModelsListBox.SelectedItem = selected;
            ModelsListBox.ScrollIntoView(selected);
        }
    }

    private void ShowEmptyState(bool empty, bool connected, string? detail = null)
    {
        // El mensaje técnico de Windows es largo y no ayuda a quien lo lee; queda a mano en el
        // tooltip del estado para quien lo necesite.
        StatusPill.ToolTip = string.IsNullOrWhiteSpace(detail) ? null : detail;
        EmptyStatePanel.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        ModelsListBox.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        if (!empty)
        {
            return;
        }

        EmptyStateTitle.Text = connected ? "Todavía no hay modelos" : "No encuentro Ollama";
        EmptyStateText.Text = connected
            ? "Descarga el recomendado aquí abajo para que Sakura pueda responder sin conexión."
            : "Comprueba que Ollama esté abierto y pulsa Actualizar.";

        EntranceMotion.Pop(EmptyStatePanel, TimeSpan.Zero, from: 0.94);
    }

    /// <summary>Las filas llegan una detrás de otra, como las tarjetas del panel.</summary>
    private void StaggerRows()
    {
        ModelsListBox.UpdateLayout();
        for (var i = 0; i < Math.Min(8, _models.Count); i++)
        {
            if (ModelsListBox.ItemContainerGenerator.ContainerFromIndex(i) is FrameworkElement row)
            {
                EntranceMotion.Rise(row, SakuraMotion.StaggerAt(i), offset: 8);
            }
        }
    }

    private void SuggestionChip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string model })
        {
            ModelNameTextBox.Text = model;
            ModelNameTextBox.Focus();
            ModelNameTextBox.CaretIndex = model.Length;
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
            ShowDownloadResult("Usa un nombre como qwen3.5:4b o gemma3:4b.", success: false);
            return;
        }

        HideDeleteConfirmation();
        _operationCancellation?.Dispose();
        _operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            _lifetimeCancellation.Token);

        DownloadResultText.Visibility = Visibility.Collapsed;
        ShowDownloadProgress();
        SetBusy(true);
        InstallButton.Visibility = Visibility.Collapsed;
        CancelDownloadButton.Visibility = Visibility.Visible;
        CancelDownloadButton.IsEnabled = true;
        SetStatus(StatusTone.Working, $"Descargando {model}…");

        var progress = new Progress<OllamaPullProgress>(update =>
        {
            DownloadStatusText.Text = OllamaPullStatusText.Describe(update);
            if (update.Percentage is double percentage)
            {
                DownloadPercentText.Text = $"{percentage:0}%";
                SetDownloadFraction(percentage / 100d);
            }
        });

        var succeeded = false;
        try
        {
            var result = await _modelService.PullAsync(
                _baseUrl,
                model,
                progress,
                _operationCancellation.Token);
            succeeded = result.Success;
            if (succeeded)
            {
                SetDownloadFraction(1);
                DownloadPercentText.Text = "100%";
            }

            SetStatus(succeeded ? StatusTone.Ready : StatusTone.Problem, result.Detail);
            ShowDownloadResult(result.Detail, succeeded);
        }
        finally
        {
            CancelDownloadButton.Visibility = Visibility.Collapsed;
            InstallButton.Visibility = Visibility.Visible;
            HideDownloadProgress();
            SetBusy(false);
        }

        if (succeeded)
        {
            await RefreshModelsAfterBusyAsync(model);
        }
    }

    private void CancelDownloadButton_Click(object sender, RoutedEventArgs e)
    {
        CancelDownloadButton.IsEnabled = false;
        DownloadStatusText.Text = "Cancelando…";
        _operationCancellation?.Cancel();
    }

    private async Task RefreshModelsAfterBusyAsync(string preferredModel)
    {
        try
        {
            var models = await _modelService.ListAsync(_baseUrl, _lifetimeCancellation.Token);
            Fill(models, preferredModel);
            ShowEmptyState(models.Count == 0, connected: true);
            SetStatus(StatusTone.Ready, models.Count == 1
                ? "Ollama está conectado · 1 modelo"
                : $"Ollama está conectado · {models.Count} modelos");

            // El recién descargado se señala con un pequeño salto para que se vea dónde quedó.
            ModelsListBox.UpdateLayout();
            if (SelectedRow is { } row &&
                ModelsListBox.ItemContainerGenerator.ContainerFromItem(row) is FrameworkElement container)
            {
                EntranceMotion.Pop(container, TimeSpan.Zero, from: 0.96);
            }
        }
        catch
        {
            // El resultado de la descarga ya está a la vista; Actualizar puede volver a intentarlo.
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy || SelectedRow is not { } selected)
        {
            return;
        }

        ConfirmDeleteText.Text = selected.Info.SizeBytes > 0
            ? $"¿Eliminar {selected.Name}? Libera {selected.SizeDisplay} y no se puede deshacer."
            : $"¿Eliminar {selected.Name}? No se puede deshacer.";
        FooterActions.Visibility = Visibility.Hidden;
        ConfirmDeletePanel.Visibility = Visibility.Visible;
        EntranceMotion.Pop(ConfirmDeletePanel, TimeSpan.Zero, from: 0.96);
        KeepModelButton.Focus();
    }

    private void KeepModelButton_Click(object sender, RoutedEventArgs e)
    {
        HideDeleteConfirmation();
        DeleteButton.Focus();
    }

    private void HideDeleteConfirmation()
    {
        ConfirmDeletePanel.Visibility = Visibility.Collapsed;
        FooterActions.Visibility = Visibility.Visible;
    }

    private async void ConfirmDeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy || SelectedRow is not { } selected)
        {
            HideDeleteConfirmation();
            return;
        }

        HideDeleteConfirmation();
        SetBusy(true);
        SetStatus(StatusTone.Working, $"Eliminando {selected.Name}…");
        try
        {
            var result = await _modelService.DeleteAsync(
                _baseUrl,
                selected.Name,
                _lifetimeCancellation.Token);
            SetStatus(result.Success ? StatusTone.Ready : StatusTone.Problem, result.Detail);
            if (result.Success)
            {
                _models.Remove(selected);
                ShowEmptyState(_models.Count == 0, connected: true);
            }
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ModelsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var hasSelection = SelectedRow is not null;
        UseButton.IsEnabled = hasSelection && !_isBusy;
        DeleteButton.IsEnabled = hasSelection && !_isBusy;
        if (!hasSelection)
        {
            HideDeleteConfirmation();
        }
    }

    private void ModelsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (!_isBusy && e.OriginalSource is DependencyObject source &&
            ItemsControl.ContainerFromElement(ModelsListBox, source) is ListBoxItem)
        {
            UseButton_Click(sender, e);
        }
    }

    private void UseButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRow is not { } selected)
        {
            return;
        }

        SelectedModel = selected.Name;
        DialogResult = true;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Esc primero retira la pregunta de eliminar; solo con ella cerrada, cierra la ventana.
        if (e.Key == Key.Escape && ConfirmDeletePanel.Visibility == Visibility.Visible)
        {
            e.Handled = true;
            HideDeleteConfirmation();
            DeleteButton.Focus();
        }
    }

    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        InstallButton.IsEnabled = !busy;
        RefreshButton.IsEnabled = !busy;
        SuggestionChips.IsEnabled = !busy;
        ModelsListBox.IsEnabled = !busy;
        UseButton.IsEnabled = !busy && SelectedRow is not null;
        DeleteButton.IsEnabled = !busy && SelectedRow is not null;

        RefreshIconRotation.BeginAnimation(RotateTransform.AngleProperty, null);
        if (busy && SakuraMotion.AnimationsEnabled)
        {
            RefreshIconRotation.BeginAnimation(
                RotateTransform.AngleProperty,
                new DoubleAnimation(0, 360, TimeSpan.FromMilliseconds(900)) { RepeatBehavior = RepeatBehavior.Forever });
        }
    }

    private enum StatusTone
    {
        Working,
        Ready,
        Attention,
        Problem
    }

    private void SetStatus(StatusTone tone, string text)
    {
        StatusText.Text = text;
        StatusDot.Fill = (Brush)FindResource(tone switch
        {
            StatusTone.Ready => "BrushSuccess",
            StatusTone.Attention => "BrushAccent",
            StatusTone.Problem => "BrushDanger",
            _ => "BrushTextTertiary"
        });
    }

    private void ShowDownloadResult(string text, bool success)
    {
        DownloadResultText.Text = text;
        DownloadResultText.Foreground = (Brush)FindResource(success ? "BrushSuccess" : "BrushTextSecondary");
        DownloadResultText.Visibility = Visibility.Visible;
    }

    private void ShowDownloadProgress()
    {
        var low = LiquidPainter.LevelColor(this, 25);
        var high = LiquidPainter.LevelColor(this, 100);
        DownloadFillStart.Color = low;
        DownloadFillEnd.Color = high;
        DownloadStatusText.Text = "Preparando la descarga…";
        DownloadPercentText.Text = string.Empty;
        DownloadProgressPanel.Visibility = Visibility.Visible;
        _downloadFraction = null;
        UpdateLayout();
        ApplyDownloadFill(animate: false);
        EntranceMotion.Rise(DownloadProgressPanel, TimeSpan.Zero, offset: 6);
    }

    private void HideDownloadProgress()
    {
        DownloadFillTranslate.BeginAnimation(TranslateTransform.XProperty, null);
        DownloadProgressPanel.Visibility = Visibility.Collapsed;
        _downloadFraction = null;
    }

    private void SetDownloadFraction(double fraction)
    {
        var wasIndeterminate = _downloadFraction is null;
        _downloadFraction = Math.Clamp(fraction, 0, 1);
        ApplyDownloadFill(animate: !wasIndeterminate);
    }

    private void DownloadTrack_SizeChanged(object sender, SizeChangedEventArgs e) =>
        ApplyDownloadFill(animate: false);

    /// <summary>
    /// La barra: con porcentaje, el relleno ocupa todo el ancho y se desplaza hasta dejar ver lo que
    /// falta, siempre desde donde esté ahora, así cada aviso de Ollama la empuja sin saltos. Sin
    /// porcentaje todavía, un tramo corto recorre la pista para decir «está en marcha».
    /// </summary>
    private void ApplyDownloadFill(bool animate)
    {
        var width = DownloadTrack.ActualWidth;
        if (width <= 0)
        {
            return;
        }

        if (_downloadFraction is not { } fraction)
        {
            DownloadFill.Width = width * 0.3;
            DownloadFillTranslate.BeginAnimation(TranslateTransform.XProperty, null);
            if (!SakuraMotion.AnimationsEnabled)
            {
                DownloadFillTranslate.X = width * 0.35;
                return;
            }

            DownloadFillTranslate.BeginAnimation(
                TranslateTransform.XProperty,
                new DoubleAnimation(-width * 0.3, width, TimeSpan.FromMilliseconds(1300))
                {
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = SakuraMotion.StandardCurve
                });
            return;
        }

        DownloadFill.Width = width;
        var target = -(1 - fraction) * width;
        if (!animate || !SakuraMotion.AnimationsEnabled)
        {
            DownloadFillTranslate.BeginAnimation(TranslateTransform.XProperty, null);
            DownloadFillTranslate.X = target;
            return;
        }

        DownloadFillTranslate.BeginAnimation(
            TranslateTransform.XProperty,
            new DoubleAnimation(target, TimeSpan.FromMilliseconds(320)) { EasingFunction = SakuraMotion.DecelerateCurve });
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        DownloadFillTranslate.BeginAnimation(TranslateTransform.XProperty, null);
        RefreshIconRotation.BeginAnimation(RotateTransform.AngleProperty, null);
        _lifetimeCancellation.Cancel();
        _operationCancellation?.Cancel();
        _operationCancellation?.Dispose();
        _operationCancellation = null;
        _modelService.Dispose();
        _lifetimeCancellation.Dispose();
    }

    /// <summary>
    /// Una fila de la lista. «En uso» es el modelo que Sakura tiene elegido ahora mismo; lo demás sale
    /// de Ollama.
    /// </summary>
    public sealed record ModelRow(OllamaModelInfo Info, bool InUse)
    {
        public string Name => Info.Name;

        public string SizeDisplay => Info.SizeDisplay;

        public Visibility InUseVisibility => InUse ? Visibility.Visible : Visibility.Collapsed;

        public string Detail => Info.ModifiedAt is { } modified
            ? $"Actualizado el {modified.LocalDateTime:d MMM yyyy}"
            : "Sin fecha de actualización";

        public override string ToString() => InUse ? $"{Name}, en uso" : Name;
    }
}
