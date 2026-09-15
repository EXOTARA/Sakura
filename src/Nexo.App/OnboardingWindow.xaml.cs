using System.ComponentModel;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Nexo.App.Motion;
using Nexo.Core.Ai;
using Nexo.Core.Distribution;
using Nexo.Core.Settings;
using Nexo.Core.Voice;
using Nexo.Windows.Ai;
using Nexo.Windows.Distribution;
using Nexo.Windows.Shell;
using Nexo.Windows.Settings;
using Nexo.Windows.Voice;
using Nexo.Windows.WindowsIntegration;

namespace Nexo.App;

public partial class OnboardingWindow : Window
{
    private const string RecommendedModel = "qwen3.5:4b";

    private readonly ShellPreferences _preferences;
    private readonly JsonSettingsStore _settingsStore;
    private readonly WindowsStartupService _startupService = new();
    private readonly OllamaModelService _modelService = new();
    private readonly OllamaRuntimeService _runtimeService = new();
    private readonly WhisperVoiceInputService _voiceInputService = new();
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly string[] _stepTitles =
    [
        "Bienvenido a Sakura",
        "Configura la voz",
        "Conecta la IA local",
        "Privacidad y Windows"
    ];

    private readonly List<Border> _dots = [];

    private CancellationTokenSource? _aiOperationCancellation;
    private OllamaRuntimeSnapshot? _runtimeSnapshot;
    private bool _ollamaPageOpened;
    private string _activeAiBaseUrl = string.Empty;
    private int _step;
    private bool _allowClose;
    private bool _aiBusy;

    public OnboardingWindow(
        ShellPreferences preferences,
        JsonSettingsStore settingsStore)
    {
        InitializeComponent();
        _preferences = preferences;
        _settingsStore = settingsStore;
        _preferences.StartWithWindows = _startupService.IsEnabled();

        WakeWordCheckBox.IsChecked = preferences.WakeWordEnabled;
        VisionCheckBox.IsChecked = preferences.VisionEnabled;
        StartWithWindowsCheckBox.IsChecked = preferences.StartWithWindows;
        MinimizeToTrayCheckBox.IsChecked = preferences.MinimizeToTray;
        WindowsNotificationsCheckBox.IsChecked = preferences.ShowWindowsNotifications;
        NotificationSoundsCheckBox.IsChecked = preferences.PlayNotificationSounds;
        AiModelComboBox.Text = preferences.AiModel;
        BuildStepDots();
        ShowStep(0);
    }

    /// <summary>La barra de título del color de la ventana, no del acento de Windows (ver WindowsDwmChrome).</summary>
    private void Window_SourceInitialized(object? sender, EventArgs e) =>
        Shell.SakuraWindowChrome.MatchCaptionToTheme(this);

    private void BuildStepDots()
    {
        for (var i = 0; i < _stepTitles.Length; i++)
        {
            var dot = new Border
            {
                Width = 8,
                Height = 8,
                Margin = new Thickness(i == 0 ? 0 : 6, 0, 0, 0),
                CornerRadius = new CornerRadius(4),
                Background = (Brush)FindResource("BrushBorder")
            };
            _dots.Add(dot);
            StepDots.Children.Add(dot);
        }
    }

    /// <summary>
    /// La píldora del paso actual se alarga con un pequeño rebote y toma el acento; las ya recorridas
    /// quedan en acento tenue y las que faltan, en gris.
    /// </summary>
    private void UpdateStepDots()
    {
        var accent = (Brush)FindResource("BrushAccent");
        var soft = (Brush)FindResource("BrushAccentSoft");
        var pending = (Brush)FindResource("BrushBorder");

        for (var i = 0; i < _dots.Count; i++)
        {
            var dot = _dots[i];
            dot.Background = i == _step ? accent : i < _step ? soft : pending;
            dot.Animate(WidthProperty, i == _step ? 26 : 8, SakuraMotion.Emphasized, SakuraMotion.SubtleSpringCurve);
        }

        System.Windows.Automation.AutomationProperties.SetName(StepDots, $"Paso {_step + 1} de {_dots.Count}");
    }

    public bool WasCompleted { get; private set; }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        LoadMicrophones();
        await RefreshAiStateAsync();
    }

    private void LoadMicrophones()
    {
        var devices = _voiceInputService.GetInputDevices();
        MicrophoneComboBox.ItemsSource = devices;
        MicrophoneComboBox.SelectedValue = devices.Any(device =>
            device.DeviceNumber == _preferences.VoiceInputDeviceNumber)
            ? _preferences.VoiceInputDeviceNumber
            : devices.FirstOrDefault()?.DeviceNumber ?? -1;
        MicrophoneStatusText.Text = devices.Count switch
        {
            0 => "Windows no informó micrófonos disponibles. Puedes continuar y configurarlo después.",
            1 => "Se encontró un micrófono.",
            _ => $"Se encontraron {devices.Count} micrófonos."
        };
    }

    private static void OpenOllamaDownloadPage()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://ollama.com/download/windows",
                UseShellExecute = true
            });
        }
        catch (Exception)
        {
            // Sin navegador predeterminado no hay nada que abrir; el texto ya dice de dónde bajarlo.
        }
    }

    private async Task RefreshAiStateAsync()
    {
        if (_aiBusy)
        {
            return;
        }

        SetAiBusy(true, canCancel: false);
        ShowAiProgress("Comprobando la IA local…", indeterminate: true);

        try
        {
            var snapshot = await _runtimeService.InspectAsync(
                _lifetimeCancellation.Token);
            await ApplyRuntimeSnapshotAsync(
                snapshot,
                _lifetimeCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            AiRuntimeTitleText.Text = "Comprobación cancelada";
            AiStatusText.Text = "Puedes volver a intentarlo con Actualizar.";
        }
        catch (Exception exception)
        {
            AiRuntimeTitleText.Text = "No pude comprobar la IA local";
            AiStatusText.Text = exception.Message;
            InstallAiButton.Content = "Volver a intentar";
            InstallAiButton.Visibility = Visibility.Visible;
        }
        finally
        {
            HideAiProgress();
            SetAiBusy(false);
        }
    }

    private async Task ApplyRuntimeSnapshotAsync(
        OllamaRuntimeSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        _runtimeSnapshot = snapshot;
        AiStatusText.Text = snapshot.Message;

        if (!snapshot.IsRunning)
        {
            _activeAiBaseUrl = string.Empty;
            AiModelComboBox.ItemsSource = null;
            AiModelComboBox.IsEnabled = false;
            RefreshModelsButton.IsEnabled = false;
            ManageModelsButton.IsEnabled = false;
            InstallAiButton.Visibility = Visibility.Visible;

            if (snapshot.State == OllamaRuntimeState.ManagedInstalled)
            {
                AiRuntimeTitleText.Text = "IA local instalada";
                InstallAiButton.Content = "Iniciar IA local";
            }
            else if (!DistributionPolicy.InstallsOllamaItself(WindowsDistributionChannel.Current))
            {
                // Diseño D89 — política 10.2.3 de Microsoft Store: no se ofrece instalar software de
                // otros. Se manda a la web de Ollama y se vuelve a comprobar cuando la persona vuelve.
                AiRuntimeTitleText.Text = "Falta Ollama";
                AiStatusText.Text =
                    "La IA local de Sakura funciona con Ollama, un programa gratuito de otros autores. " +
                    "Descárgalo desde su web, instálalo y vuelve aquí. También puedes seguir sin IA local.";
                InstallAiButton.Content = _ollamaPageOpened ? "Ya lo instalé: comprobar" : "Descargar Ollama";
            }
            else
            {
                AiRuntimeTitleText.Text = "IA local no instalada";
                InstallAiButton.Content = "Instalar IA local";
            }

            return;
        }

        _activeAiBaseUrl = snapshot.BaseUrl;
        AiRuntimeTitleText.Text = snapshot.State == OllamaRuntimeState.ManagedRunning
            ? "IA local de Sakura lista"
            : "Ollama conectado";

        await LoadModelsAsync(snapshot.BaseUrl, cancellationToken);
    }

    private async Task LoadModelsAsync(
        string baseUrl,
        CancellationToken cancellationToken)
    {
        var selectedModel = (AiModelComboBox.Text ?? string.Empty).Trim();
        var models = await _modelService.ListAsync(baseUrl, cancellationToken);
        var modelNames = models.Select(model => model.Name).ToArray();
        AiModelComboBox.ItemsSource = modelNames;

        var selectedIndex = Array.FindIndex(modelNames, model =>
            model.Equals(selectedModel, StringComparison.OrdinalIgnoreCase));
        var recommendedIndex = Array.FindIndex(modelNames, model =>
            model.Equals(RecommendedModel, StringComparison.OrdinalIgnoreCase));

        if (selectedIndex >= 0)
        {
            AiModelComboBox.SelectedIndex = selectedIndex;
        }
        else if (recommendedIndex >= 0)
        {
            AiModelComboBox.SelectedIndex = recommendedIndex;
        }
        else if (models.Count > 0)
        {
            AiModelComboBox.SelectedIndex = 0;
        }

        if (models.Count == 0)
        {
            AiStatusText.Text =
                "El motor local está listo. Falta descargar el modelo recomendado.";
            InstallAiButton.Content = "Descargar modelo recomendado";
            InstallAiButton.Visibility = Visibility.Visible;
        }
        else
        {
            AiStatusText.Text =
                $"IA local conectada · {models.Count} modelo(s) disponible(s).";
            InstallAiButton.Visibility = Visibility.Collapsed;
        }
    }

    private async Task<bool> DownloadRecommendedModelAsync(
        string baseUrl,
        CancellationToken cancellationToken)
    {
        var installedModels = await _modelService.ListAsync(
            baseUrl,
            cancellationToken);

        var installed = installedModels.Any(model =>
            model.Name.Equals(
                RecommendedModel,
                StringComparison.OrdinalIgnoreCase));

        if (installed)
        {
            AiModelComboBox.Text = RecommendedModel;
            return true;
        }

        ShowAiProgress(
            $"Descargando {RecommendedModel}…",
            indeterminate: true);

        var progress = new Progress<OllamaPullProgress>(update =>
        {
            AiProgressText.Text = update.Percentage is double percentage
                ? $"{update.Status} · {percentage:0}%"
                : update.Status;
            AiProgressText.Visibility = Visibility.Visible;

            if (update.Percentage is double value)
            {
                AiProgressBar.IsIndeterminate = false;
                AiProgressBar.Value = value;
            }
        });

        var result = await _modelService.PullAsync(
            baseUrl,
            RecommendedModel,
            progress,
            cancellationToken);

        AiStatusText.Text = result.Detail;
        if (!result.Success)
        {
            InstallAiButton.Content = "Volver a descargar";
            InstallAiButton.Visibility = Visibility.Visible;
            return false;
        }

        AiModelComboBox.Text = RecommendedModel;
        return true;
    }

    private void ShowStep(int step)
    {
        var previous = _step;
        _step = Math.Clamp(step, 0, 3);
        WelcomePanel.Visibility = _step == 0 ? Visibility.Visible : Visibility.Collapsed;
        VoicePanel.Visibility = _step == 1 ? Visibility.Visible : Visibility.Collapsed;
        AiPanel.Visibility = _step == 2 ? Visibility.Visible : Visibility.Collapsed;
        PrivacyPanel.Visibility = _step == 3 ? Visibility.Visible : Visibility.Collapsed;
        StepTitleText.Text = _stepTitles[_step];
        UpdateStepDots();
        BackButton.IsEnabled = !_aiBusy && _step > 0;
        NextButton.Content = _step == 3 ? "Terminar" : "Siguiente";

        // 2026-09-14 — el paso nuevo entra por el lado hacia el que se avanza y su contenido llega
        // escalonado, como el resto de Sakura. Sin animación en el primer paso al abrir: la ventana ya
        // aparece por su cuenta.
        if (_step != previous && IsLoaded)
        {
            PlayStepEntrance(_step > previous ? 1 : -1);
        }
    }

    private void PlayStepEntrance(int direction)
    {
        if (!SakuraMotion.AnimationsEnabled)
        {
            return;
        }

        StepHost.EnterFrom(StepHostTranslate, fromOffset: 36 * direction);

        var panel = _step switch
        {
            0 => WelcomePanel,
            1 => VoicePanel,
            2 => AiPanel,
            _ => PrivacyPanel
        };

        var index = 0;
        foreach (var child in panel.Children.OfType<FrameworkElement>())
        {
            EntranceMotion.Rise(child, TimeSpan.FromMilliseconds(60) + SakuraMotion.StaggerAt(index++), offset: 8);
        }
    }

    private void BackButton_Click(object sender, RoutedEventArgs e) =>
        ShowStep(_step - 1);

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        if (_step < 3)
        {
            ShowStep(_step + 1);
            return;
        }

        CompleteOnboarding();
    }

    private void SkipButton_Click(object sender, RoutedEventArgs e)
    {
        _preferences.HasCompletedOnboarding = true;
        _settingsStore.Save(_preferences);
        WasCompleted = true;
        _allowClose = true;
        Close();
    }

    private async void RefreshModelsButton_Click(object sender, RoutedEventArgs e) =>
        await RefreshAiStateAsync();

    private async void InstallAiButton_Click(object sender, RoutedEventArgs e)
    {
        if (_aiBusy)
        {
            return;
        }

        if (!DistributionPolicy.InstallsOllamaItself(WindowsDistributionChannel.Current) &&
            _runtimeSnapshot?.State is null or OllamaRuntimeState.Unavailable)
        {
            if (!_ollamaPageOpened)
            {
                _ollamaPageOpened = true;
                OpenOllamaDownloadPage();
                InstallAiButton.Content = "Ya lo instalé: comprobar";
                return;
            }

            await RefreshAiStateAsync();
            return;
        }

        _aiOperationCancellation?.Dispose();
        _aiOperationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            _lifetimeCancellation.Token);
        var cancellationToken = _aiOperationCancellation.Token;

        SetAiBusy(true, canCancel: true);
        ShowAiProgress("Preparando la IA local…", indeterminate: true);

        try
        {
            var snapshot = await _runtimeService.InspectAsync(cancellationToken);

            if (snapshot.State == OllamaRuntimeState.Unavailable)
            {
                var runtimeProgress = new Progress<OllamaRuntimeInstallProgress>(update =>
                {
                    AiProgressText.Text = update.Percentage is double percentage
                        ? $"{update.Message} · {percentage:0}%"
                        : update.Message;
                    AiProgressText.Visibility = Visibility.Visible;

                    if (update.Percentage is double value)
                    {
                        AiProgressBar.IsIndeterminate = false;
                        AiProgressBar.Value = value;
                    }
                    else
                    {
                        AiProgressBar.IsIndeterminate = true;
                    }
                });

                snapshot = await _runtimeService.InstallManagedAsync(
                    runtimeProgress,
                    cancellationToken);
            }
            else if (snapshot.State == OllamaRuntimeState.ManagedInstalled)
            {
                AiProgressText.Text = "Iniciando la IA local…";
                AiProgressText.Visibility = Visibility.Visible;
                snapshot = await _runtimeService.StartManagedAsync(
                    cancellationToken);
            }

            _runtimeSnapshot = snapshot;
            if (!snapshot.IsRunning)
            {
                AiRuntimeTitleText.Text = "No se pudo preparar la IA local";
                AiStatusText.Text = snapshot.Message;
                InstallAiButton.Content = "Volver a intentar";
                InstallAiButton.Visibility = Visibility.Visible;
                return;
            }

            _activeAiBaseUrl = snapshot.BaseUrl;
            AiRuntimeTitleText.Text = snapshot.State == OllamaRuntimeState.ManagedRunning
                ? "IA local de Sakura lista"
                : "Ollama conectado";

            var modelReady = await DownloadRecommendedModelAsync(
                snapshot.BaseUrl,
                cancellationToken);
            if (!modelReady)
            {
                return;
            }

            await LoadModelsAsync(snapshot.BaseUrl, cancellationToken);
            AiRuntimeTitleText.Text = "Todo listo";
            AiStatusText.Text =
                $"{RecommendedModel} está instalado. Ya puedes usar la IA de Sakura.";
        }
        catch (OperationCanceledException)
        {
            AiRuntimeTitleText.Text = "Instalación cancelada";
            AiStatusText.Text = "No se hicieron cambios incompletos. Puedes intentarlo de nuevo.";
            InstallAiButton.Content = "Volver a intentar";
            InstallAiButton.Visibility = Visibility.Visible;
        }
        catch (HttpRequestException exception)
        {
            AiRuntimeTitleText.Text = "No se pudo completar la descarga";
            AiStatusText.Text = exception.Message;
            InstallAiButton.Content = "Volver a intentar";
            InstallAiButton.Visibility = Visibility.Visible;
        }
        catch (Exception exception)
        {
            AiRuntimeTitleText.Text = "No se pudo preparar la IA local";
            AiStatusText.Text = exception.Message;
            InstallAiButton.Content = "Volver a intentar";
            InstallAiButton.Visibility = Visibility.Visible;
        }
        finally
        {
            HideAiProgress();
            SetAiBusy(false);
            _aiOperationCancellation?.Dispose();
            _aiOperationCancellation = null;
        }
    }

    private void CancelAiInstallButton_Click(object sender, RoutedEventArgs e)
    {
        CancelAiInstallButton.IsEnabled = false;
        AiProgressText.Text = "Cancelando…";
        AiProgressText.Visibility = Visibility.Visible;
        _aiOperationCancellation?.Cancel();
    }

    private async void ManageModelsButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_activeAiBaseUrl))
        {
            AiStatusText.Text = "Primero inicia o instala la IA local.";
            return;
        }

        var window = new ModelManagerWindow(
            _activeAiBaseUrl,
            AiModelComboBox.Text)
        {
            Owner = this
        };
        if (window.ShowDialog() == true &&
            !string.IsNullOrWhiteSpace(window.SelectedModel))
        {
            AiModelComboBox.Text = window.SelectedModel;
        }

        await LoadModelsAsync(
            _activeAiBaseUrl,
            _lifetimeCancellation.Token);
    }

    private void ShowAiProgress(string message, bool indeterminate)
    {
        AiProgressBar.Value = 0;
        AiProgressBar.IsIndeterminate = indeterminate;
        AiProgressBar.Visibility = Visibility.Visible;
        AiProgressText.Text = message;
        AiProgressText.Visibility = Visibility.Visible;
    }

    private void HideAiProgress()
    {
        AiProgressBar.IsIndeterminate = false;
        AiProgressBar.Value = 0;
        AiProgressBar.Visibility = Visibility.Collapsed;
        AiProgressText.Visibility = Visibility.Collapsed;
        CancelAiInstallButton.IsEnabled = true;
    }

    private void SetAiBusy(bool busy, bool canCancel = false)
    {
        _aiBusy = busy;
        SkipButton.IsEnabled = !busy;
        BackButton.IsEnabled = !busy && _step > 0;
        NextButton.IsEnabled = !busy;
        InstallAiButton.IsEnabled = !busy;
        CancelAiInstallButton.Visibility = busy && canCancel
            ? Visibility.Visible
            : Visibility.Collapsed;

        var runtimeRunning = _runtimeSnapshot?.IsRunning == true;
        AiModelComboBox.IsEnabled = !busy && runtimeRunning;
        RefreshModelsButton.IsEnabled = !busy && runtimeRunning;
        ManageModelsButton.IsEnabled = !busy && runtimeRunning;
    }

    private void CompleteOnboarding()
    {
        if (MicrophoneComboBox.SelectedItem is VoiceInputDevice microphone)
        {
            _preferences.VoiceInputDeviceNumber = microphone.DeviceNumber;
        }

        _preferences.WakeWordEnabled = WakeWordCheckBox.IsChecked == true;
        _preferences.WakeWordPhrase = WakeWordPhrase.OyeSakura;

        var rawModel = (AiModelComboBox.Text ?? string.Empty).Trim();
        var model = string.IsNullOrWhiteSpace(rawModel)
            ? null
            : OllamaModelName.Normalize(rawModel);
        if (!string.IsNullOrWhiteSpace(rawModel) && model is null)
        {
            AiStatusText.Text = "El nombre del modelo contiene caracteres no válidos.";
            ShowStep(2);
            return;
        }

        if (model is null)
        {
            _preferences.AiProvider = AiProviderKind.Disabled;
            _preferences.AiBaseUrl = string.Empty;
            _preferences.AiModel = string.Empty;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(_activeAiBaseUrl))
            {
                AiStatusText.Text =
                    "La IA local no está conectada. Instálala, iníciala o deja el modelo vacío para continuar sin IA.";
                ShowStep(2);
                return;
            }

            _preferences.AiProvider = AiProviderKind.Ollama;
            _preferences.AiBaseUrl = _activeAiBaseUrl;
            _preferences.AiModel = model;
            _preferences.AiApiKeyEnvironmentVariable = string.Empty;
        }

        _preferences.VisionEnabled = VisionCheckBox.IsChecked == true;
        _preferences.StartWithWindows = StartWithWindowsCheckBox.IsChecked == true;
        _preferences.MinimizeToTray = MinimizeToTrayCheckBox.IsChecked == true;
        _preferences.ShowWindowsNotifications = WindowsNotificationsCheckBox.IsChecked == true;
        _preferences.PlayNotificationSounds = NotificationSoundsCheckBox.IsChecked == true;
        _preferences.HasCompletedOnboarding = true;

        var startupResult = _startupService.SetEnabled(_preferences.StartWithWindows);
        if (!startupResult.Success && _preferences.StartWithWindows)
        {
            _preferences.StartWithWindows = false;
            FinishStatusText.Text = startupResult.Message;
            MessageBox.Show(
                this,
                startupResult.Message,
                "Inicio con Windows",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        _settingsStore.Save(_preferences);
        WasCompleted = true;
        _allowClose = true;
        Close();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!_allowClose)
        {
            var result = MessageBox.Show(
                this,
                "¿Omitir la configuración inicial? Puedes repetirla después desde Personalización.",
                "Configurar Sakura",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                e.Cancel = true;
                return;
            }

            _preferences.HasCompletedOnboarding = true;
            _settingsStore.Save(_preferences);
        }

        _lifetimeCancellation.Cancel();
        _aiOperationCancellation?.Cancel();
        _aiOperationCancellation?.Dispose();
        _aiOperationCancellation = null;
        _voiceInputService.Dispose();
        _modelService.Dispose();
        _runtimeService.Dispose();
        _lifetimeCancellation.Dispose();
    }
}
