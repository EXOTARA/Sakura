using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Nexo.Core.Ai;
using Nexo.Core.AdaptiveEngine;
using Nexo.Core.Flow;
using Nexo.Core.Memory;
using Nexo.Core.Permissions;
using Nexo.Core.Settings;
using Nexo.Core.Skills;
using Nexo.Core.Voice;
using Nexo.Core.Workspace;

namespace Nexo.App.Views;

public partial class SettingsView : UserControl
{
    private bool _isApplyingPreferences;

    /// <summary>Se pidió mirar si hay una versión nueva.</summary>
    public event Action? UpdateCheckRequested;

    /// <summary>Se aceptó instalar la versión encontrada.</summary>
    public event Action? UpdateInstallRequested;

    /// <summary>Se dejó pasar la versión encontrada.</summary>
    public event Action? UpdateSkipRequested;

    public event Action<SidebarPosition>? PositionChanged;
    public event Action<double>? WidthChanged;
    public event Action<double>? OpacityChanged;
    public event Action<string>? AccentChanged;
    public event Action<AccentSource>? AccentSourceChanged;
    public event Action<bool>? AnimationsChanged;
    public event Action<bool>? EdgeRevealChanged;
    public event Action<bool>? AutomaticUpdateCheckChanged;
    public event Action<string, bool>? ModuleVisibilityChanged;
    public event Action<string, bool>? PeekOptionChanged;
    public event Action<bool>? ConversationHistoryChanged;
    public event Action<bool>? VoiceResponsesChanged;
    public event Action<int>? VoiceInputDeviceChanged;
    public event Action<bool>? WakeWordEnabledChanged;
    public event Action<WakeWordPhrase>? WakeWordPhraseChanged;
    public event Action<WakeWordSensitivity>? WakeWordSensitivityChanged;
    public event EventHandler? WakeWordTestRequested;
    public event EventHandler? WakeWordAliasFromLastRequested;
    public event EventHandler? WakeWordAliasesClearRequested;

    // Diseño D7 (Fase 3 — Sakura Flow)
    public event Action<bool>? FlowEnabledChanged;
    public event Action<FlowMode>? FlowModeChanged;
    public event Action<IReadOnlyList<string>>? FlowDictionaryChanged;
    public event Action<IReadOnlyList<string>>? FlowSnippetsChanged;

    // Diseño D10 (Fase 6 — Context and Memory)
    public event Action<bool>? MemoryEnabledChanged;
    public event Action<MemoryCategory, bool>? MemoryCategoryChanged;
    public event Action<int>? MemoryRetentionChanged;
    public event Action<IReadOnlyList<string>>? MemoryExclusionsChanged;
    public event EventHandler? MemoryShowRequested;
    public event EventHandler? MemoryForgetAllRequested;

    // Diseño D16: permisos por capacidad
    public event Action<SakuraCapability, PermissionLevel>? CapabilityPermissionChanged;

    // Diseño D19: exclusiones por aplicación
    public event Action<IReadOnlyList<string>>? PermissionExclusionsChanged;

    // Diseño D23 (Fase 8 — Skills Platform)
    public event Action<SkillPackId>? SkillPackActivationRequested;
    public event EventHandler? SkillPackDeactivationRequested;

    // Diseño D13 (Fase 5 — Project Companion)
    public event EventHandler? WorkspaceAuthorizeRequested;
    public event EventHandler? WorkspaceRevokeRequested;
    public event Action<AutonomyLevel>? WorkspaceAutonomyLevelChanged;

    public event Action<AiProviderKind>? AiProviderChanged;

    // Diseño D25 — la clave viaja al almacén cifrado, nunca a ShellPreferences.
    public event Action<AiProviderKind, string>? ApiKeyChanged;
    public event Action<AiProviderKind>? ApiKeyRequested;
    public event Action<string>? ApiKeyPageRequested;
    public event Action<string>? AiBaseUrlChanged;
    public event Action<string>? AiModelChanged;
    public event Action<string>? AiApiKeyEnvironmentVariableChanged;
    public event Action<bool>? ShareSystemMetricsWithAiChanged;
    public event Action<bool>? VisionEnabledChanged;
    public event Action<bool>? ResourceGovernorEnabledChanged;
    public event Action<bool>? PauseWakeWordInGameModeChanged;
    public event Action<bool>? ProtectVisionWhenBusyChanged;
    public event Action<HardwarePerformanceMode>? HardwarePerformanceModeChanged;
    public event Action<bool>? StartWithWindowsChanged;
    public event Action<bool>? MinimizeToTrayChanged;
    public event Action<bool>? WindowsNotificationsChanged;
    public event Action<bool>? NotificationSoundsChanged;
    public event EventHandler? AiTestConnectionRequested;
    public event EventHandler? ManageModelsRequested;
    public event EventHandler? DiagnosticsRequested;
    public event EventHandler? OnboardingRequested;

    /// <summary>
    /// Diseño D2 — restaurar solo la apariencia (posición, ancho, transparencia, acento,
    /// animaciones y barra lateral). Deliberadamente NO toca tareas, rutinas, historial ni la
    /// configuración de voz, IA, motores o integración con Windows.
    /// </summary>
    public event EventHandler? ResetAppearanceRequested;

    public SettingsView()
    {
        InitializeComponent();
    }

    public void ApplyPreferences(ShellPreferences preferences)
    {
        _isApplyingPreferences = true;

        WidthSlider.Value = preferences.Width;
        OpacitySlider.Value = preferences.Opacity;
        WidthValueText.Text = $"{preferences.Width:0} px";
        OpacityValueText.Text = $"{preferences.Opacity:P0}";
        AnimationsCheckBox.IsChecked = preferences.AnimationsEnabled;
        EdgeRevealCheckBox.IsChecked = preferences.EdgeRevealEnabled;
        AutomaticUpdateCheckBox.IsChecked = preferences.AutomaticUpdateCheckEnabled;
        ApplyAccentSourceToControls(preferences.AccentSource);
        HighlightSelectedTheme(preferences.AccentColor);
        HomeModuleCheckBox.IsChecked = preferences.ShowHomeModule;
        TasksModuleCheckBox.IsChecked = preferences.ShowTasksModule;
        FocusModuleCheckBox.IsChecked = preferences.ShowFocusModule;
        RoutinesModuleCheckBox.IsChecked = preferences.ShowRoutinesModule;
        AudioModuleCheckBox.IsChecked = preferences.ShowAudioModule;
        CaptureModuleCheckBox.IsChecked = preferences.ShowCaptureModule;
        SystemModuleCheckBox.IsChecked = preferences.ShowSystemModule;
        PeekEnabledCheckBox.IsChecked = preferences.PeekEnabled;
        PeekCpuCheckBox.IsChecked = preferences.ShowCpuInPeek;
        PeekMemoryCheckBox.IsChecked = preferences.ShowMemoryInPeek;
        PeekGpuCheckBox.IsChecked = preferences.ShowGpuInPeek;
        PeekDiskCheckBox.IsChecked = preferences.ShowDiskInPeek;
        PeekTopProcessCheckBox.IsChecked = preferences.ShowTopProcessInPeek;
        SaveConversationHistoryCheckBox.IsChecked = preferences.SaveConversationHistory;
        SpeakVoiceResponsesCheckBox.IsChecked = preferences.SpeakVoiceResponses;
        VoiceInputDeviceComboBox.SelectedValue = preferences.VoiceInputDeviceNumber;
        WakeWordEnabledCheckBox.IsChecked = preferences.WakeWordEnabled;
        // Diseño D69 — los tres controles ofrecen Sakura, pero siguen sabiendo leer las elecciones
        // de los dos nombres anteriores: lo que importa es la forma —corta, "oye" o "hey"—, no la
        // marca con la que se guardó.
        WakeWordShortRadioButton.IsChecked = preferences.WakeWordPhrase is
            WakeWordPhrase.Sakura or WakeWordPhrase.Sakura or WakeWordPhrase.Nexo;
        WakeWordOyeRadioButton.IsChecked = preferences.WakeWordPhrase is
            WakeWordPhrase.OyeSakura or WakeWordPhrase.OyeSakura or WakeWordPhrase.OyeNexo;
        WakeWordHeyRadioButton.IsChecked = preferences.WakeWordPhrase is
            WakeWordPhrase.HeySakura or WakeWordPhrase.HeySakura or WakeWordPhrase.HeyNexo;
        SelectWakeWordSensitivity(preferences.WakeWordSensitivity);
        SetWakeWordAliases(preferences.WakeWordAliases);

        FlowEnabledCheckBox.IsChecked = preferences.FlowEnabled;
        FlowModeTextoRadioButton.IsChecked = preferences.FlowMode == FlowMode.Texto;
        FlowModeCorreoRadioButton.IsChecked = preferences.FlowMode == FlowMode.Correo;
        FlowModeCodigoRadioButton.IsChecked = preferences.FlowMode == FlowMode.Codigo;
        FlowDictionaryBox.Text = string.Join(Environment.NewLine, preferences.FlowDictionary);
        FlowSnippetsBox.Text = string.Join(Environment.NewLine, preferences.FlowSnippets);
        ApplyMemorySettings(preferences.Memory);
        ApplyPermissionSettings(preferences.Permissions);
        ApplyComputerUseAutonomyLevel(preferences.ComputerUseAutonomyLevel);
        ApplyWorkspaceSettings(preferences.Workspace);
        ApplyAiProviderSelection(preferences.AiProvider);
        AiBaseUrlTextBox.Text = preferences.AiBaseUrl;
        AiModelTextBox.Text = preferences.AiModel;
        AiApiKeyVariableTextBox.Text = preferences.AiApiKeyEnvironmentVariable;
        ShareSystemMetricsWithAiCheckBox.IsChecked = preferences.ShareSystemMetricsWithAi;
        VisionEnabledCheckBox.IsChecked = preferences.VisionEnabled;
        ResourceGovernorEnabledCheckBox.IsChecked = preferences.ResourceGovernorEnabled;
        PauseWakeWordInGameModeCheckBox.IsChecked = preferences.PauseWakeWordInGameMode;
        ProtectVisionWhenBusyCheckBox.IsChecked = preferences.ProtectVisionWhenBusy;
        UpdateResourceGovernorOptionsAvailability();
        ApplyHardwarePerformanceModeSelection(preferences.HardwarePerformanceMode);
        StartWithWindowsCheckBox.IsChecked = preferences.StartWithWindows;
        MinimizeToTrayCheckBox.IsChecked = preferences.MinimizeToTray;
        WindowsNotificationsCheckBox.IsChecked = preferences.ShowWindowsNotifications;
        NotificationSoundsCheckBox.IsChecked = preferences.PlayNotificationSounds;
        SetAiConnectionStatus(
            preferences.AiProvider == AiProviderKind.Disabled
                ? "La IA está desactivada."
                : $"{AiProviderDefaults.Get(preferences.AiProvider).DisplayName} configurado. Prueba la conexión antes de usarlo.",
            isSuccess: null);
        UpdatePositionButtons(preferences.Position);
        UpdatePeekOptionsAvailability();
        UpdateWakeWordOptionsAvailability();
        UpdateAiOptionsAvailability();

        _isApplyingPreferences = false;

        // Después de bajar la bandera, no antes: quien atiende este evento vuelve a llamar a
        // SetStoredApiKeyPresence, que la maneja por su cuenta, y anidarlas la dejaría en falso a
        // mitad de esta rutina — el resto de asignaciones empezarían a disparar eventos de cambio
        // como si la persona los hubiera tocado.
        ApiKeyRequested?.Invoke(preferences.AiProvider);
    }

    private void LeftButton_Click(object sender, RoutedEventArgs e)
    {
        UpdatePositionButtons(SidebarPosition.Left);
        PositionChanged?.Invoke(SidebarPosition.Left);
    }

    private void RightButton_Click(object sender, RoutedEventArgs e)
    {
        UpdatePositionButtons(SidebarPosition.Right);
        PositionChanged?.Invoke(SidebarPosition.Right);
    }

    private void WidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (WidthValueText is null)
        {
            return;
        }

        WidthValueText.Text = $"{e.NewValue:0} px";
        if (!_isApplyingPreferences)
        {
            WidthChanged?.Invoke(e.NewValue);
        }
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (OpacityValueText is null)
        {
            return;
        }

        OpacityValueText.Text = $"{e.NewValue:P0}";
        if (!_isApplyingPreferences)
        {
            OpacityChanged?.Invoke(e.NewValue);
        }
    }

    private void AccentButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string accent })
        {
            // AccentChanged primero: su manejador en MainWindow aplica el acento nuevo al recurso
            // BrushAccent de forma síncrona, y el resaltado de abajo lee ese mismo recurso —si se
            // invirtiera el orden, la tarjeta se resaltaría con el color de acento saliente.
            AccentChanged?.Invoke(accent);
            HighlightSelectedTheme(accent);
        }
    }

    /// <summary>
    /// Resalta la tarjeta cuyo acento coincide con el guardado, y solo esa: BorderBrush y
    /// BorderThickness se fijan directamente en cada botón, ganándole al valor por defecto del
    /// estilo. Ningún color a mano fuera de la galería coincide con ninguna tarjeta, así que las
    /// cuatro se quedan sin resaltar —eso es correcto, no un caso sin cubrir.
    /// </summary>
    private void HighlightSelectedTheme(string accentHex)
    {
        var cards = new (Button Button, string AccentHex)[]
        {
            (ThemeSakuraButton, "#8B6CFF"),
            (ThemeOceanoButton, "#4D8DFF"),
            (ThemeBosqueButton, "#35C58A"),
            (ThemeCerezoButton, "#F06CA8")
        };

        foreach (var (button, cardAccentHex) in cards)
        {
            var isSelected = string.Equals(cardAccentHex, accentHex, StringComparison.OrdinalIgnoreCase);
            button.BorderThickness = new Thickness(isSelected ? 2 : 1);
            button.BorderBrush = isSelected
                ? (Brush)FindResource("BrushAccent")
                : (Brush)FindResource("BrushBorder");
        }
    }

    /// <summary>
    /// Las dos casillas describen un solo ajuste con tres estados, así que se excluyen entre sí:
    /// marcar una desmarca la otra. Con dos banderas independientes existiría el estado "las dos
    /// activas", que no significa nada y habría que resolver a la fuerza en algún sitio.
    /// </summary>
    private void AccentSourceCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isApplyingPreferences)
        {
            return;
        }

        var source = sender switch
        {
            var s when ReferenceEquals(s, AccentFromWallpaperCheckBox) &&
                       AccentFromWallpaperCheckBox.IsChecked == true => AccentSource.Wallpaper,
            var s when ReferenceEquals(s, AccentFollowsWindowsCheckBox) &&
                       AccentFollowsWindowsCheckBox.IsChecked == true => AccentSource.Windows,
            _ => AccentSource.Manual
        };

        ApplyAccentSourceToControls(source);
        AccentSourceChanged?.Invoke(source);
    }

    private void ApplyAccentSourceToControls(AccentSource source)
    {
        // Se guarda y se restaura en vez de bajar la bandera al final: esto se llama también desde
        // dentro de ApplyPreferences, que ya la tiene levantada, y dejarla en falso a media rutina
        // haría que el resto de asignaciones dispararan eventos de cambio como si alguien las
        // hubiera tocado a mano.
        var wasApplying = _isApplyingPreferences;
        _isApplyingPreferences = true;
        AccentFromWallpaperCheckBox.IsChecked = source == AccentSource.Wallpaper;
        AccentFollowsWindowsCheckBox.IsChecked = source == AccentSource.Windows;
        _isApplyingPreferences = wasApplying;

        // La galería solo tiene sentido cuando el color lo elige la persona: con el acento atado al
        // fondo o a Windows, pulsar una tarjeta no haría nada visible y se leería como un fallo.
        ThemeGalleryPanel.IsEnabled = source == AccentSource.Manual;
    }

    private void EdgeRevealCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isApplyingPreferences)
        {
            EdgeRevealChanged?.Invoke(EdgeRevealCheckBox.IsChecked == true);
        }
    }

    private void AutomaticUpdateCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isApplyingPreferences)
        {
            AutomaticUpdateCheckChanged?.Invoke(AutomaticUpdateCheckBox.IsChecked == true);
        }
    }

    private void AnimationsCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isApplyingPreferences)
        {
            AnimationsChanged?.Invoke(AnimationsCheckBox.IsChecked == true);
        }
    }

    private void ModuleCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isApplyingPreferences || sender is not CheckBox { Tag: string module } checkBox)
        {
            return;
        }

        ModuleVisibilityChanged?.Invoke(module, checkBox.IsChecked == true);
    }

    private void PeekCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (PeekEnabledCheckBox is null)
        {
            return;
        }

        UpdatePeekOptionsAvailability();

        if (_isApplyingPreferences || sender is not CheckBox { Tag: string option } checkBox)
        {
            return;
        }

        PeekOptionChanged?.Invoke(option, checkBox.IsChecked == true);
    }

    private void SaveConversationHistoryCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isApplyingPreferences)
        {
            ConversationHistoryChanged?.Invoke(SaveConversationHistoryCheckBox.IsChecked == true);
        }
    }

    private void VisionEnabledCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isApplyingPreferences)
        {
            VisionEnabledChanged?.Invoke(VisionEnabledCheckBox.IsChecked == true);
        }
    }

    private void ResourceGovernorCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (ResourceGovernorEnabledCheckBox is null)
        {
            return;
        }

        UpdateResourceGovernorOptionsAvailability();
        if (_isApplyingPreferences)
        {
            return;
        }

        if (sender == ResourceGovernorEnabledCheckBox)
        {
            ResourceGovernorEnabledChanged?.Invoke(
                ResourceGovernorEnabledCheckBox.IsChecked == true);
        }
        else if (sender == PauseWakeWordInGameModeCheckBox)
        {
            PauseWakeWordInGameModeChanged?.Invoke(
                PauseWakeWordInGameModeCheckBox.IsChecked == true);
        }
        else if (sender == ProtectVisionWhenBusyCheckBox)
        {
            ProtectVisionWhenBusyChanged?.Invoke(
                ProtectVisionWhenBusyCheckBox.IsChecked == true);
        }
    }

    private void UpdateResourceGovernorOptionsAvailability()
    {
        if (PauseWakeWordInGameModeCheckBox is null ||
            ProtectVisionWhenBusyCheckBox is null)
        {
            return;
        }

        var enabled = ResourceGovernorEnabledCheckBox.IsChecked == true;
        PauseWakeWordInGameModeCheckBox.IsEnabled = enabled;
        ProtectVisionWhenBusyCheckBox.IsEnabled = enabled;
    }

    private void SpeakVoiceResponsesCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isApplyingPreferences)
        {
            VoiceResponsesChanged?.Invoke(SpeakVoiceResponsesCheckBox.IsChecked == true);
        }
    }

    private void StartWithWindowsCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isApplyingPreferences)
        {
            StartWithWindowsChanged?.Invoke(StartWithWindowsCheckBox.IsChecked == true);
        }
    }

    private void MinimizeToTrayCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isApplyingPreferences)
        {
            MinimizeToTrayChanged?.Invoke(MinimizeToTrayCheckBox.IsChecked == true);
        }
    }

    private void WindowsNotificationsCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isApplyingPreferences)
        {
            WindowsNotificationsChanged?.Invoke(WindowsNotificationsCheckBox.IsChecked == true);
        }
    }

    private void NotificationSoundsCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isApplyingPreferences)
        {
            NotificationSoundsChanged?.Invoke(NotificationSoundsCheckBox.IsChecked == true);
        }
    }

    public void SetStartWithWindows(bool enabled)
    {
        if (StartWithWindowsCheckBox is null)
        {
            return;
        }

        _isApplyingPreferences = true;
        StartWithWindowsCheckBox.IsChecked = enabled;
        _isApplyingPreferences = false;
    }

    public void SetWindowsIntegrationStatus(string detail, bool? isSuccess)
    {
        if (WindowsIntegrationStatusText is null)
        {
            return;
        }

        WindowsIntegrationStatusText.Text = detail;
        WindowsIntegrationStatusText.Foreground = isSuccess switch
        {
            true => (System.Windows.Media.Brush)FindResource("BrushSuccess"),
            false => (System.Windows.Media.Brush)FindResource("BrushWarning"),
            _ => (System.Windows.Media.Brush)FindResource("BrushTextSecondary")
        };
    }

    public void SetVoiceInputDevices(
        IReadOnlyList<VoiceInputDevice> devices,
        int selectedDeviceNumber)
    {
        if (VoiceInputDeviceComboBox is null)
        {
            return;
        }

        _isApplyingPreferences = true;
        VoiceInputDeviceComboBox.ItemsSource = devices;
        VoiceInputDeviceComboBox.SelectedValue = selectedDeviceNumber;

        if (VoiceInputDeviceComboBox.SelectedItem is not VoiceInputDevice &&
            devices.Count > 0)
        {
            VoiceInputDeviceComboBox.SelectedIndex = 0;
        }

        VoiceInputDeviceComboBox.IsEnabled = devices.Count > 0;
        VoiceInputDeviceStatusText.Text = devices.Count switch
        {
            0 => "Windows no encontró micrófonos disponibles.",
            1 => "Se encontró un micrófono. Sakura lo usará para Mic y la frase de activación.",
            _ => "El micrófono elegido se usa tanto para Mic como para “Oye Sakura”."
        };
        _isApplyingPreferences = false;
    }

    private void VoiceInputDeviceComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_isApplyingPreferences ||
            VoiceInputDeviceComboBox.SelectedItem is not VoiceInputDevice device)
        {
            return;
        }

        VoiceInputDeviceChanged?.Invoke(device.DeviceNumber);
    }

    public void ApplyAiProviderDefaults(AiProviderKind provider)
    {
        var preset = AiProviderDefaults.Get(provider);
        _isApplyingPreferences = true;
        AiBaseUrlTextBox.Text = preset.BaseUrl;
        AiModelTextBox.Text = preset.DefaultModel;
        AiApiKeyVariableTextBox.Text = preset.ApiKeyEnvironmentVariable;
        _isApplyingPreferences = false;
        DescribeAiProvider(provider);
        UpdateAiOptionsAvailability();
        SetAiConnectionStatus(
            provider == AiProviderKind.Disabled
                ? "La IA está desactivada."
                : $"{preset.DisplayName} seleccionado. Revisa el modelo y prueba la conexión.",
            isSuccess: null);
    }

    public void SetAiConnectionStatus(string detail, bool? isSuccess)
    {
        if (AiConnectionStatusText is null)
        {
            return;
        }

        AiConnectionStatusText.Text = detail;
        AiConnectionStatusText.Foreground = isSuccess switch
        {
            true => (System.Windows.Media.Brush)FindResource("BrushSuccess"),
            false => (System.Windows.Media.Brush)FindResource("BrushWarning"),
            _ => (System.Windows.Media.Brush)FindResource("BrushTextSecondary")
        };
    }

    public void SetAiModel(string model)
    {
        if (AiModelTextBox is null)
        {
            return;
        }

        _isApplyingPreferences = true;
        AiModelTextBox.Text = model;
        _isApplyingPreferences = false;
    }

    public void SetAiTestInProgress(bool inProgress)
    {
        if (AiTestConnectionButton is null)
        {
            return;
        }

        AiTestConnectionButton.IsEnabled = !inProgress &&
            SelectedAiProvider != AiProviderKind.Disabled;
        AiTestConnectionButton.Content = inProgress
            ? "Probando…"
            : "Probar conexión";
        ManageModelsButton.IsEnabled = !inProgress &&
            AiProviderDefaults.UsesOllamaProtocol(SelectedAiProvider);
    }

    private void AiProviderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isApplyingPreferences ||
            AiProviderComboBox.SelectedItem is not AiProviderChoice choice)
        {
            return;
        }

        AiProviderChanged?.Invoke(choice.Kind);
        ApplyAiProviderDefaults(choice.Kind);
        ApiKeyRequested?.Invoke(choice.Kind);
    }

    private void AiApiKeyPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_isApplyingPreferences)
        {
            return;
        }

        ApiKeyChanged?.Invoke(SelectedAiProvider, AiApiKeyPasswordBox.Password);
    }

    private void AiGetApiKeyButton_Click(object sender, RoutedEventArgs e)
    {
        var url = AiProviderDefaults.Get(SelectedAiProvider).ApiKeyUrl;
        if (!string.IsNullOrWhiteSpace(url))
        {
            ApiKeyPageRequested?.Invoke(url);
        }
    }

    private void AiForgetApiKeyButton_Click(object sender, RoutedEventArgs e)
    {
        AiApiKeyPasswordBox.Clear();
        ApiKeyChanged?.Invoke(SelectedAiProvider, string.Empty);
    }

    /// <summary>
    /// Muestra si hay clave guardada sin mostrar la clave. El cuadro de contraseña se deja vacío a
    /// propósito incluso cuando hay una guardada: rellenarlo con la clave real la pondría en pantalla
    /// —y en cualquier captura— cada vez que alguien abre Ajustes.
    /// </summary>
    /// <summary>
    /// Se usa al cambiar de proveedor o al abrir el apartado: la caja pasa a referirse a la clave
    /// de otro proveedor, así que se vacía a propósito —nunca se vuelve a mostrar una clave ya
    /// guardada, ni siquiera la suya propia— y el texto de ayuda dice si ya hay algo guardado.
    /// </summary>
    public void SetStoredApiKeyPresence(bool hasKey)
    {
        if (AiApiKeyStatusText is null)
        {
            return;
        }

        _isApplyingPreferences = true;
        AiApiKeyPasswordBox.Clear();
        _isApplyingPreferences = false;

        UpdateApiKeyStatusText(hasKey);
    }

    /// <summary>
    /// Diseño D26 — igual que <see cref="SetStoredApiKeyPresence"/> pero sin vaciar la caja. Antes,
    /// PasswordChanged llamaba a SetStoredApiKeyPresence nada más guardar cada pulsación, y esa
    /// llamada limpiaba la caja de inmediato: pegar la clave se veía como si no hiciera nada, y
    /// escribir algo después —pensando que había fallado— sobrescribía la clave buena con una a
    /// medias. Mientras la persona está escribiendo, solo se refleja si ya quedó algo guardado; la
    /// caja se vacía únicamente cuando cambia de proveedor, en SetStoredApiKeyPresence.
    /// </summary>
    public void UpdateApiKeyStatusText(bool hasKey)
    {
        AiApiKeyStatusText.Text = hasKey
            ? "Hay una clave guardada en este equipo, cifrada con tu cuenta de Windows. Escribe una nueva solo si quieres reemplazarla."
            : "Pega aquí tu clave. Se guarda cifrada en este equipo y nunca se escribe en el archivo de ajustes.";
        AiForgetApiKeyButton.IsEnabled = hasKey;
    }

    private void AiTextBox_LostKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        if (_isApplyingPreferences || sender is not TextBox { Tag: string field } textBox)
        {
            return;
        }

        var value = textBox.Text.Trim();
        switch (field)
        {
            case "BaseUrl":
                AiBaseUrlChanged?.Invoke(value);
                break;
            case "Model":
                AiModelChanged?.Invoke(value);
                break;
            case "ApiKeyVariable":
                AiApiKeyEnvironmentVariableChanged?.Invoke(value);
                break;
        }
    }

    private void ShareSystemMetricsWithAiCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isApplyingPreferences)
        {
            ShareSystemMetricsWithAiChanged?.Invoke(
                ShareSystemMetricsWithAiCheckBox.IsChecked == true);
        }
    }

    private void AiTestConnectionButton_Click(object sender, RoutedEventArgs e)
    {
        AiBaseUrlChanged?.Invoke(AiBaseUrlTextBox.Text.Trim());
        AiModelChanged?.Invoke(AiModelTextBox.Text.Trim());
        AiApiKeyEnvironmentVariableChanged?.Invoke(AiApiKeyVariableTextBox.Text.Trim());
        AiTestConnectionRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ManageModelsButton_Click(object sender, RoutedEventArgs e)
    {
        AiBaseUrlChanged?.Invoke(AiBaseUrlTextBox.Text.Trim());
        AiModelChanged?.Invoke(AiModelTextBox.Text.Trim());
        ManageModelsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void DiagnosticsButton_Click(object sender, RoutedEventArgs e) =>
        DiagnosticsRequested?.Invoke(this, EventArgs.Empty);

    private void OnboardingButton_Click(object sender, RoutedEventArgs e) =>
        OnboardingRequested?.Invoke(this, EventArgs.Empty);

    private void ResetAppearanceButton_Click(object sender, RoutedEventArgs e) =>
        ResetAppearanceRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Diseño D25 — el selector de proveedor se construye desde
    /// <see cref="AiProviderDefaults.SelectableOrder"/> en vez de una lista escrita a mano en el
    /// XAML. Eran cinco botones de radio; ahora hay diez proveedores y la mitad son de nube, así
    /// que duplicar la lista en la interfaz garantizaba que un día se quedara corta.
    /// </summary>
    private sealed record AiProviderChoice(AiProviderKind Kind, string DisplayName);

    private void PopulateAiProviders()
    {
        AiProviderComboBox.ItemsSource = AiProviderDefaults.SelectableOrder
            .Select(kind => new AiProviderChoice(kind, AiProviderDefaults.Get(kind).DisplayName))
            .ToArray();
    }

    private void ApplyAiProviderSelection(AiProviderKind provider)
    {
        if (AiProviderComboBox.ItemsSource is null)
        {
            PopulateAiProviders();
        }

        AiProviderComboBox.SelectedItem = AiProviderComboBox.Items
            .OfType<AiProviderChoice>()
            .FirstOrDefault(choice => choice.Kind == provider);

        DescribeAiProvider(provider);
    }

    private AiProviderKind SelectedAiProvider =>
        AiProviderComboBox?.SelectedItem is AiProviderChoice choice
            ? choice.Kind
            : AiProviderKind.Disabled;

    /// <summary>
    /// Explica el proveedor antes de que alguien lo elija: dónde corre y qué cuesta. Son las dos
    /// preguntas que decidían si el equipo de una persona iba a arrastrarse, y hasta ahora la
    /// interfaz no contestaba ninguna de las dos — solo mostraba el nombre.
    /// </summary>
    private void DescribeAiProvider(AiProviderKind provider)
    {
        if (AiProviderSummaryText is null)
        {
            return;
        }

        var preset = AiProviderDefaults.Get(provider);

        var location = preset.Location switch
        {
            AiProviderLocation.Local => "En tu equipo",
            AiProviderLocation.Cloud => "En la nube",
            _ => string.Empty
        };

        var cost = preset.Cost switch
        {
            AiProviderCost.FreeOnDevice => "gratis, sin cuenta",
            AiProviderCost.FreeTier => "tiene capa gratuita",
            AiProviderCost.Paid => "de pago por uso",
            _ => string.Empty
        };

        var badge = (location, cost) switch
        {
            ("", "") => string.Empty,
            (_, "") => location,
            ("", _) => cost,
            _ => $"{location} · {cost}"
        };

        AiProviderBadgeText.Text = badge;

        // "Sin inteligencia artificial" no corre en ningún sitio ni cuesta nada, así que no tiene
        // etiqueta. Un TextBlock vacío sigue ocupando el alto de una línea, y ese hueco en blanco
        // sobre el texto se lee como un fallo de maquetación.
        AiProviderBadgeText.Visibility = string.IsNullOrEmpty(badge)
            ? Visibility.Collapsed
            : Visibility.Visible;

        AiProviderSummaryText.Text = preset.Summary;

        AiApiKeyPanel.Visibility = preset.RequiresApiKey
            ? Visibility.Visible
            : Visibility.Collapsed;
        AiGetApiKeyButton.IsEnabled = !string.IsNullOrWhiteSpace(preset.ApiKeyUrl);

        AiBaseUrlHintText.Text = AiProviderDefaults.IsManagedBySakura(provider)
            ? "La IA local de Sakura siempre vive en esta dirección y la administra la propia aplicación; cambiarla aquí no tiene efecto."
            : $"Dirección por omisión de {preset.DisplayName}: {preset.BaseUrl}";

        AiBaseUrlTextBox.IsEnabled = provider != AiProviderKind.Disabled &&
            !AiProviderDefaults.IsManagedBySakura(provider);
    }

    private void HardwarePerformanceModeRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        if (_isApplyingPreferences || sender is not RadioButton { Tag: string modeTag })
        {
            return;
        }

        HardwarePerformanceModeChanged?.Invoke(ParseHardwarePerformanceMode(modeTag));
    }

    private static HardwarePerformanceMode ParseHardwarePerformanceMode(string modeTag)
    {
        return Enum.TryParse<HardwarePerformanceMode>(modeTag, ignoreCase: true, out var mode)
            ? mode
            : HardwarePerformanceMode.Automatic;
    }

    private void ApplyHardwarePerformanceModeSelection(HardwarePerformanceMode mode)
    {
        PerformanceModeAutomaticRadioButton.IsChecked = mode == HardwarePerformanceMode.Automatic;
        PerformanceModeEcoRadioButton.IsChecked = mode == HardwarePerformanceMode.Eco;
        PerformanceModeBalancedRadioButton.IsChecked = mode == HardwarePerformanceMode.Balanced;
        PerformanceModeMaximumRadioButton.IsChecked = mode == HardwarePerformanceMode.Maximum;
    }

    private void UpdateAiOptionsAvailability()
    {
        if (AiBaseUrlTextBox is null)
        {
            return;
        }

        var provider = SelectedAiProvider;
        var enabled = provider != AiProviderKind.Disabled;

        // La dirección la fija DescribeAiProvider: con la IA local de Sakura no se puede escribir,
        // porque el motor solo existe donde la propia aplicación lo pone.
        AiModelTextBox.IsEnabled = enabled;
        AiApiKeyVariableTextBox.IsEnabled = enabled;
        ShareSystemMetricsWithAiCheckBox.IsEnabled = enabled;
        AiTestConnectionButton.IsEnabled = enabled;
        ManageModelsButton.IsEnabled = enabled &&
            AiProviderDefaults.UsesOllamaProtocol(provider);
    }

    private void WakeWordEnabledCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        UpdateWakeWordOptionsAvailability();

        if (!_isApplyingPreferences)
        {
            WakeWordEnabledChanged?.Invoke(WakeWordEnabledCheckBox.IsChecked == true);
        }
    }

    private void WakeWordPhraseRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        if (_isApplyingPreferences || sender is not RadioButton { Tag: string phrase })
        {
            return;
        }

        var value = phrase.Equals("OyeSakura", StringComparison.OrdinalIgnoreCase)
            ? WakeWordPhrase.OyeSakura
            : phrase.Equals("HeySakura", StringComparison.OrdinalIgnoreCase)
                ? WakeWordPhrase.HeySakura
                : WakeWordPhrase.Sakura;
        WakeWordPhraseChanged?.Invoke(value);
    }

    private void WakeWordSensitivityComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isApplyingPreferences ||
            WakeWordSensitivityComboBox.SelectedItem is not ComboBoxItem { Tag: string value } ||
            !Enum.TryParse<WakeWordSensitivity>(value, ignoreCase: true, out var sensitivity))
        {
            return;
        }

        WakeWordSensitivityChanged?.Invoke(sensitivity);
    }

    private void WakeWordTestButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isApplyingPreferences)
        {
            WakeWordTestRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    public void SetWakeWordTestStatus(string detail, bool? isSuccess)
    {
        WakeWordTestStatusText.Text = detail;
        WakeWordTestStatusText.Foreground = isSuccess switch
        {
            true => (System.Windows.Media.Brush)FindResource("BrushSuccess"),
            false => (System.Windows.Media.Brush)FindResource("BrushWarning"),
            _ => (System.Windows.Media.Brush)FindResource("BrushTextSecondary")
        };
    }

    public void SetWakeWordObservation(WakeWordRecognitionObservedEventArgs observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var state = observation.IsFinal ? "final" : "parcial";
        SetWakeWordTestStatus(
            $"Vosk ({state}) escuchó “{observation.RecognizedText}”. {observation.Match.Detail}",
            observation.Match.IsMatch ? true : null);
        WakeWordUseObservedAliasButton.IsEnabled =
            !observation.Match.IsMatch &&
            WakeWordAliasPolicy.TryNormalize(observation.RecognizedText, out _, out _);
    }

    public void SetWakeWordAliases(IReadOnlyCollection<string> aliases)
    {
        aliases ??= Array.Empty<string>();
        WakeWordAliasesText.Text = aliases.Count == 0
            ? "Aliases personales: ninguno"
            : "Aliases personales: " + string.Join(", ", aliases.Select(alias => $"“{alias}”"));
        WakeWordClearAliasesButton.IsEnabled = aliases.Count > 0;
    }

    public void ClearWakeWordObservation()
    {
        WakeWordUseObservedAliasButton.IsEnabled = false;
    }

    private void WakeWordUseObservedAliasButton_Click(object sender, RoutedEventArgs e) =>
        WakeWordAliasFromLastRequested?.Invoke(this, EventArgs.Empty);

    private void WakeWordClearAliasesButton_Click(object sender, RoutedEventArgs e) =>
        WakeWordAliasesClearRequested?.Invoke(this, EventArgs.Empty);

    private void SelectWakeWordSensitivity(WakeWordSensitivity sensitivity)
    {
        foreach (var item in WakeWordSensitivityComboBox.Items.OfType<ComboBoxItem>())
        {
            if (item.Tag is string value &&
                Enum.TryParse<WakeWordSensitivity>(value, ignoreCase: true, out var parsed) &&
                parsed == sensitivity)
            {
                WakeWordSensitivityComboBox.SelectedItem = item;
                return;
            }
        }

        WakeWordSensitivityComboBox.SelectedIndex = 1;
    }

    private void UpdateWakeWordOptionsAvailability()
    {
        if (WakeWordShortRadioButton is null)
        {
            return;
        }

        var enabled = WakeWordEnabledCheckBox.IsChecked == true;
        WakeWordShortRadioButton.IsEnabled = enabled;
        WakeWordOyeRadioButton.IsEnabled = enabled;
        WakeWordHeyRadioButton.IsEnabled = enabled;
        WakeWordSensitivityComboBox.IsEnabled = enabled;
        WakeWordTestButton.IsEnabled = enabled;
        WakeWordUseObservedAliasButton.IsEnabled = false;
        WakeWordClearAliasesButton.IsEnabled = enabled &&
            !WakeWordAliasesText.Text.EndsWith("ninguno", StringComparison.OrdinalIgnoreCase);
    }

    private void UpdatePeekOptionsAvailability()
    {
        if (PeekCpuCheckBox is null)
        {
            return;
        }

        var enabled = PeekEnabledCheckBox.IsChecked == true;
        PeekCpuCheckBox.IsEnabled = enabled;
        PeekMemoryCheckBox.IsEnabled = enabled;
        PeekGpuCheckBox.IsEnabled = enabled;
        PeekDiskCheckBox.IsEnabled = enabled;
        PeekTopProcessCheckBox.IsEnabled = enabled;
    }

    private void UpdatePositionButtons(SidebarPosition position)
    {
        LeftButton.Background = position == SidebarPosition.Left
            ? (System.Windows.Media.Brush)FindResource("BrushAccentSoft")
            : (System.Windows.Media.Brush)FindResource("BrushSurfaceRaised");

        RightButton.Background = position == SidebarPosition.Right
            ? (System.Windows.Media.Brush)FindResource("BrushAccentSoft")
            : (System.Windows.Media.Brush)FindResource("BrushSurfaceRaised");
    }

    // ---------- Diseño D7 (Fase 3 — Sakura Flow) ----------

    private void FlowEnabledCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isApplyingPreferences)
        {
            return;
        }

        FlowEnabledChanged?.Invoke(FlowEnabledCheckBox.IsChecked == true);
    }

    private void FlowModeRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        if (_isApplyingPreferences || sender is not RadioButton { Tag: string tag })
        {
            return;
        }

        if (Enum.TryParse<FlowMode>(tag, out var mode))
        {
            FlowModeChanged?.Invoke(mode);
        }
    }

    private void FlowDictionaryBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isApplyingPreferences)
        {
            return;
        }

        var lines = SplitListLines(FlowDictionaryBox.Text);
        ReportIgnoredLines(lines, "diccionario");
        FlowDictionaryChanged?.Invoke(lines);
    }

    private void FlowSnippetsBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isApplyingPreferences)
        {
            return;
        }

        var lines = SplitListLines(FlowSnippetsBox.Text);
        ReportIgnoredLines(lines, "atajos");
        FlowSnippetsChanged?.Invoke(lines);
    }

    private static IReadOnlyList<string> SplitListLines(string? text) =>
        (text ?? string.Empty)
            .ReplaceLineEndings()
            .Split(
                Environment.NewLine,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();

    /// <summary>
    /// Diseño D7 — el parser ignora en silencio las líneas mal escritas para que una sola no tumbe
    /// el resto, pero en la interfaz sí conviene decirlo: si alguien escribe "cojana Sakura" (sin
    /// el igual) y no pasa nada, parecería que el diccionario no funciona.
    /// </summary>
    private void ReportIgnoredLines(IReadOnlyList<string> lines, string listName)
    {
        var accepted = FlowSettingsParser.ParseDictionary(lines).Count;
        var ignored = lines.Count - accepted;

        if (ignored <= 0)
        {
            FlowListsStatusText.Visibility = Visibility.Collapsed;
            return;
        }

        FlowListsStatusText.Text = ignored == 1
            ? $"Se ignoró 1 línea del {listName} porque no tiene el formato dicho=escrito."
            : $"Se ignoraron {ignored} líneas del {listName} porque no tienen el formato dicho=escrito.";
        FlowListsStatusText.Visibility = Visibility.Visible;
    }

    // ---------- Diseño D10 (Fase 6 — Context and Memory) ----------

    /// <summary>
    /// Diseño D10 — la memoria dejó de configurarse a mano en <c>settings.json</c>. Los cuatro
    /// controles que la fase exige (activar, categorías, retención y exclusiones) viven aquí, junto
    /// a los dos que tienen que funcionar siempre: ver lo guardado y borrarlo.
    /// </summary>
    public void ApplyMemorySettings(MemorySettings? memory)
    {
        var settings = memory ?? new MemorySettings();

        MemoryEnabledCheckBox.IsChecked = settings.Enabled;
        MemoryPreferencesCheckBox.IsChecked = settings.RememberPreferences;
        MemoryConversationCheckBox.IsChecked = settings.RememberConversation;
        MemoryHabitsCheckBox.IsChecked = settings.RememberHabits;
        MemoryRetentionBox.Text = settings.RetentionDays.ToString();
        MemoryExclusionsBox.Text = string.Join(Environment.NewLine, settings.Exclusions);

        UpdateMemoryCategoriesAvailability(settings.Enabled);
    }

    /// <summary>
    /// Los botones de ver y olvidar NO se desactivan con la memoria: revocar y auditar deben poder
    /// hacerse siempre. Si apagar la memoria bloqueara el borrado, lo ya guardado quedaría atrapado
    /// justo cuando la persona quiere deshacerse de ello.
    /// </summary>
    private void UpdateMemoryCategoriesAvailability(bool enabled)
    {
        MemoryCategoriesPanel.IsEnabled = enabled;
        MemoryCategoriesPanel.Opacity = enabled ? 1.0 : 0.55;
    }

    private void MemoryEnabledCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isApplyingPreferences)
        {
            return;
        }

        var enabled = MemoryEnabledCheckBox.IsChecked == true;
        UpdateMemoryCategoriesAvailability(enabled);

        if (!enabled)
        {
            // Refleja en la interfaz lo que MemorySettings.Normalize hace en los datos: apagar el
            // interruptor general apaga las categorías. Si la vista siguiera enseñándolas marcadas,
            // volver a activar la memoria parecería restaurar permisos que ya no existen.
            _isApplyingPreferences = true;
            MemoryPreferencesCheckBox.IsChecked = false;
            MemoryConversationCheckBox.IsChecked = false;
            MemoryHabitsCheckBox.IsChecked = false;
            _isApplyingPreferences = false;
        }

        MemoryEnabledChanged?.Invoke(enabled);
    }

    private void MemoryCategoryCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isApplyingPreferences || sender is not CheckBox { Tag: string tag } box)
        {
            return;
        }

        if (Enum.TryParse<MemoryCategory>(tag, out var category))
        {
            MemoryCategoryChanged?.Invoke(category, box.IsChecked == true);
        }
    }

    private void MemoryRetentionBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isApplyingPreferences)
        {
            return;
        }

        if (!int.TryParse(MemoryRetentionBox.Text?.Trim(), out var days))
        {
            // Un valor ilegible no puede aceptarse en silencio: la retención es el límite de cuánto
            // se conserva, y dejar el cuadro con texto inválido haría creer que se guardó.
            SetMemoryStatus(
                $"La retención debe ser un número de días entre {MemorySettings.MinimumRetentionDays} " +
                $"y {MemorySettings.MaximumRetentionDays}.");
            return;
        }

        var clamped = Math.Clamp(days, MemorySettings.MinimumRetentionDays, MemorySettings.MaximumRetentionDays);
        MemoryRetentionBox.Text = clamped.ToString();

        SetMemoryStatus(clamped != days
            ? $"La retención se ajustó a {clamped} días, el límite permitido."
            : null);

        MemoryRetentionChanged?.Invoke(clamped);
    }

    private void MemoryExclusionsBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isApplyingPreferences)
        {
            return;
        }

        MemoryExclusionsChanged?.Invoke(SplitListLines(MemoryExclusionsBox.Text));
    }

    private void MemoryShowButton_Click(object sender, RoutedEventArgs e) =>
        MemoryShowRequested?.Invoke(this, EventArgs.Empty);

    private void MemoryForgetAllButton_Click(object sender, RoutedEventArgs e) =>
        MemoryForgetAllRequested?.Invoke(this, EventArgs.Empty);

    public void SetMemoryStatus(string? message)
    {
        MemoryStatusText.Text = message ?? string.Empty;
        MemoryStatusText.Visibility = string.IsNullOrWhiteSpace(message)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    // ---------- Diseño D13 (Fase 5 — Project Companion) ----------

    /// <summary>
    /// Diseño D13 — el proyecto dejó de manejarse solo desde la paleta de comandos. Autorizar y
    /// revocar están aquí, uno al lado del otro, junto a hasta dónde puede llegar Sakura.
    /// </summary>
    public void ApplyWorkspaceSettings(WorkspaceSettings? workspace)
    {
        var settings = workspace ?? new WorkspaceSettings();
        var authorized = settings.HasAuthorizedFolder;

        WorkspacePathText.Text = authorized
            ? $"Carpeta autorizada: {settings.AuthorizedPath}"
            : "No hay ninguna carpeta autorizada.";

        WorkspaceRevokeButton.IsEnabled = authorized;

        // El nivel solo tiene sentido si hay algo a lo que aplicarlo.
        WorkspaceAutonomyPanel.IsEnabled = authorized;
        WorkspaceAutonomyPanel.Opacity = authorized ? 1.0 : 0.55;

        var wasApplying = _isApplyingPreferences;
        _isApplyingPreferences = true;
        WorkspaceLevelVerRadioButton.IsChecked = settings.AutonomyLevel == AutonomyLevel.Ver;
        WorkspaceLevelGuiarRadioButton.IsChecked = settings.AutonomyLevel == AutonomyLevel.Guiar;
        WorkspaceLevelProponerRadioButton.IsChecked = settings.AutonomyLevel == AutonomyLevel.Proponer;
        WorkspaceLevelEjecutarRadioButton.IsChecked = settings.AutonomyLevel == AutonomyLevel.EjecutarUnPaso;
        WorkspaceLevelColaborarRadioButton.IsChecked =
            settings.AutonomyLevel == AutonomyLevel.ColaborarConConfirmaciones;
        _isApplyingPreferences = wasApplying;
    }

    private void WorkspaceLevelRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        if (_isApplyingPreferences || sender is not RadioButton { Tag: string tag })
        {
            return;
        }

        // Solo se emiten niveles que la política ofrece. La interfaz no puede conceder lo que el
        // modelo de confianza no permite todavía.
        if (Enum.TryParse<AutonomyLevel>(tag, out var level) &&
            WorkspaceAutonomyPolicy.IsAvailable(level))
        {
            WorkspaceAutonomyLevelChanged?.Invoke(level);
        }
    }

    // ---------- Diseño D16: permisos por capacidad ----------

    private readonly System.Collections.ObjectModel.ObservableCollection<PermissionRow> _permissionRows = [];

    /// <summary>
    /// Diseño D16 — una fila por capacidad, cada una con su propio nivel. Se listan todas aunque
    /// alguna esté bloqueada: una capacidad que no aparece es una capacidad cuyo permiso nadie sabe
    /// que existe.
    /// </summary>
    public void ApplyPermissionSettings(PermissionSettings? permissions)
    {
        var settings = permissions ?? new PermissionSettings();
        settings.Normalize();

        _permissionRows.Clear();
        PermissionsItemsControl.ItemsSource = _permissionRows;
        PermissionExclusionsBox.Text = string.Join(
            Environment.NewLine, PermissionExclusionParser.Format(settings));

        foreach (var capability in Enum.GetValues<SakuraCapability>())
        {
            var permission = settings.For(capability);
            var excluded = permission.ExcludedApps.Count;

            _permissionRows.Add(new PermissionRow(
                capability,
                CapabilityTitle(capability),
                excluded == 0
                    ? CapabilityText.Describe(capability)
                    : $"{CapabilityText.Describe(capability)} · {excluded} exclusiones",
                permission.Level,
                OnPermissionRowChanged));
        }
    }

    // Diseño D18: hasta dónde puede llegar Computer Use, con su propio nivel.
    public event Action<AutonomyLevel>? ComputerUseAutonomyLevelChanged;

    public void ApplyComputerUseAutonomyLevel(AutonomyLevel level)
    {
        var wasApplying = _isApplyingPreferences;
        _isApplyingPreferences = true;

        ComputerUseVerRadioButton.IsChecked = level == AutonomyLevel.Ver;
        ComputerUseGuiarRadioButton.IsChecked = level == AutonomyLevel.Guiar;
        ComputerUseProponerRadioButton.IsChecked = level == AutonomyLevel.Proponer;
        ComputerUseEjecutarRadioButton.IsChecked = level == AutonomyLevel.EjecutarUnPaso;

        _isApplyingPreferences = wasApplying;
    }

    private void ComputerUseLevelRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        if (_isApplyingPreferences || sender is not RadioButton { Tag: string tag })
        {
            return;
        }

        // Solo se emiten niveles que la política de Computer Use ofrece: la interfaz no puede
        // conceder lo que el modelo de confianza no permite todavía.
        if (Enum.TryParse<AutonomyLevel>(tag, out var level) &&
            Nexo.Core.ComputerUse.ComputerUseAutonomyPolicy.IsAvailable(level))
        {
            ComputerUseAutonomyLevelChanged?.Invoke(level);
        }
    }

    private void OnPermissionRowChanged(SakuraCapability capability, PermissionLevel level)
    {
        if (_isApplyingPreferences)
        {
            return;
        }

        CapabilityPermissionChanged?.Invoke(capability, level);
    }

    // ---------- Diseño D23 (Fase 8 — Skills Platform) ----------

    private readonly System.Collections.ObjectModel.ObservableCollection<SkillPackRow> _skillPackRows = [];

    /// <summary>
    /// Diseño D23 — los seis packs con su estado. Cada fila dice qué le falta al pack **antes** de
    /// activarlo: enterarse después de que necesitaba un permiso que no diste es enterarse tarde.
    /// </summary>
    public void ApplySkillPacks(SkillPackId? activePack, ShellPreferences preferences)
    {
        _skillPackRows.Clear();
        SkillPackItemsControl.ItemsSource = _skillPackRows;

        foreach (var pack in SkillPackCatalog.All)
        {
            var isActive = activePack == pack.Id;
            var missing = pack.Requirements.Count(requirement => !requirement.IsSatisfied(preferences));

            _skillPackRows.Add(new SkillPackRow(
                pack.Id,
                pack.Name,
                pack.Purpose,
                StateLine: isActive
                    ? "Activo ahora mismo."
                    : missing == 0
                        ? "Listo para activar."
                        : missing == 1
                            ? "Le falta 1 permiso que solo puedes dar tú."
                            : $"Le faltan {missing} permisos que solo puedes dar tú.",
                ButtonText: isActive ? "Desactivar" : "Activar"));
        }
    }

    private void SkillPackButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: SkillPackId id })
        {
            return;
        }

        var row = _skillPackRows.FirstOrDefault(entry => entry.Id == id);
        if (row?.ButtonText == "Desactivar")
        {
            SkillPackDeactivationRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        SkillPackActivationRequested?.Invoke(id);
    }

    private sealed record SkillPackRow(
        SkillPackId Id,
        string Name,
        string Purpose,
        string StateLine,
        string ButtonText);

    private void PermissionExclusionsBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isApplyingPreferences)
        {
            return;
        }

        var lines = SplitListLines(PermissionExclusionsBox.Text);
        var ignored = PermissionExclusionParser.Parse(lines).IgnoredLines;

        // Una exclusión que la persona cree puesta y no lo está es una protección que no existe, así
        // que las líneas ignoradas se dicen en voz alta.
        SetPermissionsStatus(ignored switch
        {
            0 => "Exclusiones guardadas.",
            1 => "Se ignoró 1 línea porque no tiene el formato capacidad: aplicación.",
            _ => $"Se ignoraron {ignored} líneas porque no tienen el formato capacidad: aplicación."
        });

        PermissionExclusionsChanged?.Invoke(lines);
    }

    public void SetPermissionsStatus(string? message)
    {
        PermissionsStatusText.Text = message ?? string.Empty;
        PermissionsStatusText.Visibility = string.IsNullOrWhiteSpace(message)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private static string CapabilityTitle(SakuraCapability capability) => capability switch
    {
        SakuraCapability.Lens => "Ver la pantalla (Lens)",
        SakuraCapability.Flow => "Dictado global (Flow)",
        SakuraCapability.Memoria => "Memoria personal",
        SakuraCapability.Proyecto => "Proyecto autorizado",
        SakuraCapability.Optimizacion => "Optimizar el equipo",
        SakuraCapability.ComputerUse => "Actuar sobre el equipo",
        _ => capability.ToString()
    };

    private sealed class PermissionRow(
        SakuraCapability capability,
        string title,
        string detail,
        PermissionLevel level,
        Action<SakuraCapability, PermissionLevel> onChanged)
    {
        private PermissionLevel _level = level;

        public string Title { get; } = title;

        public string Detail { get; } = detail;

        public IReadOnlyList<PermissionLevel> Levels { get; } = Enum.GetValues<PermissionLevel>();

        public PermissionLevel Level
        {
            get => _level;
            set
            {
                if (_level == value)
                {
                    return;
                }

                _level = value;
                onChanged(capability, value);
            }
        }
    }

    private void WorkspaceAuthorizeButton_Click(object sender, RoutedEventArgs e) =>
        WorkspaceAuthorizeRequested?.Invoke(this, EventArgs.Empty);

    private void WorkspaceRevokeButton_Click(object sender, RoutedEventArgs e) =>
        WorkspaceRevokeRequested?.Invoke(this, EventArgs.Empty);

    // ---------- Actualizaciones (Diseño D65) ----------

    /// <summary>La versión que está corriendo ahora, para que se vea sin buscarla.</summary>
    public void SetCurrentVersion(string version) =>
        UpdateCurrentVersionText.Text = $"Versión instalada: {version}";

    /// <summary>
    /// Enseña en qué punto va la comprobación. El botón se desactiva mientras trabaja: pulsarlo dos
    /// veces lanzaría dos consultas y dos descargas de cien megas.
    /// </summary>
    public void SetUpdateStatus(string status, bool busy)
    {
        UpdateStatusText.Text = status;
        UpdateCheckButton.IsEnabled = !busy;
    }

    /// <summary>
    /// Enseña la versión encontrada, o esconde la tarjeta si no hay ninguna.
    ///
    /// Las notas de la publicación se enseñan tal cual las escribió quien publicó: son la única
    /// respuesta a «qué cambia si instalo esto», y resumirlas por mi cuenta sería inventar.
    /// </summary>
    public void ShowUpdateOffer(string? version, string? notes) =>
        ShowUpdateOffer(version, notes, targetFolder: null);

    public void ShowUpdateOffer(string? version, string? notes, string? targetFolder)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            UpdateOfferPanel.Visibility = Visibility.Collapsed;
            return;
        }

        UpdateOfferTitleText.Text = $"Hay una versión nueva: {version}";
        UpdateOfferNotesText.Text = string.IsNullOrWhiteSpace(notes)
            ? "Sin notas de la publicación."
            : notes.Trim();

        UpdateTargetText.Text = string.IsNullOrWhiteSpace(targetFolder)
            ? string.Empty
            : $"Se reemplazará la carpeta: {targetFolder}";

        UpdateOfferNotesText.Visibility = Visibility.Visible;
        UpdateProgressBar.Visibility = Visibility.Collapsed;
        UpdateInstallButton.IsEnabled = true;
        UpdateSkipButton.IsEnabled = true;
        UpdateOfferPanel.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Avance de la descarga. Los dos botones se apagan: una vez que empieza a bajar, «ahora no» ya
    /// no describe nada que pueda pasar, y un botón que no cumple es peor que ninguno.
    /// </summary>
    public void SetUpdateProgress(double fraction)
    {
        UpdateProgressBar.Visibility = Visibility.Visible;
        UpdateProgressBar.Value = Math.Clamp(fraction, 0, 1);
        UpdateInstallButton.IsEnabled = false;
        UpdateSkipButton.IsEnabled = false;
    }

    private void UpdateCheckButton_Click(object sender, RoutedEventArgs e) =>
        UpdateCheckRequested?.Invoke();

    private void UpdateInstallButton_Click(object sender, RoutedEventArgs e) =>
        UpdateInstallRequested?.Invoke();

    private void UpdateSkipButton_Click(object sender, RoutedEventArgs e) =>
        UpdateSkipRequested?.Invoke();
}
