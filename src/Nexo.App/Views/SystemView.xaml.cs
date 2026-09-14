using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Nexo.Core.AdaptiveEngine;
using Nexo.Core.Audit;
using Nexo.Core.Hardware;
using Nexo.Core.Metrics;
using Nexo.Core.Optimization;
using Nexo.Core.Resources;

namespace Nexo.App.Views;

/// <summary>
/// Diseño D44 — Sistema se queda con el diagnóstico: capacidad del equipo, plan adaptativo,
/// optimización, registro de lo que Sakura ha hecho y estado del runtime.
///
/// Las medidas en vivo —procesador, gráfica, disco, red, memoria— se fueron al cajón del borde de
/// arriba (<see cref="Nexo.App.DashboardWindow"/>). No es un reparto arbitrario: esto de aquí se lee
/// sentado y de vez en cuando; aquello se mira de reojo mientras haces otra cosa, y para eso no
/// puede exigir abrir la aplicación y navegar hasta una pestaña.
/// </summary>
public partial class SystemView : UserControl
{
    private readonly ObservableCollection<AdaptiveEnginePlanRow> _adaptiveEnginePlanRows = [];
    private readonly ObservableCollection<AuditRow> _auditRows = [];

    public SystemView()
    {
        InitializeComponent();
        AdaptiveEnginePlanItemsControl.ItemsSource = _adaptiveEnginePlanRows;
        AuditItemsControl.ItemsSource = _auditRows;
    }

    public event EventHandler? RestartVoiceRequested;

    public event EventHandler? DiagnosticsRequested;

    public event EventHandler? HardwareCapabilityRefreshRequested;

    // Diseño D11 (Fase 4 — Adaptive Computer Optimization)
    public event Action<OptimizationScenario>? OptimizationScenarioRequested;

    public event EventHandler? OptimizationUndoRequested;

    public event EventHandler? OptimizationAuditRequested;

    private void OptimizationScenarioButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag } && Enum.TryParse<OptimizationScenario>(tag, out var scenario))
        {
            OptimizationScenarioRequested?.Invoke(scenario);
        }
    }

    private void OptimizationUndoButton_Click(object sender, RoutedEventArgs e) =>
        OptimizationUndoRequested?.Invoke(this, EventArgs.Empty);

    private void OptimizationAuditButton_Click(object sender, RoutedEventArgs e) =>
        OptimizationAuditRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Diseño D11 — el botón de deshacer solo se activa cuando hay un snapshot guardado. Ofrecer
    /// "deshacer" sin nada que deshacer haría dudar de si la optimización anterior se aplicó.
    /// </summary>
    public void SetOptimizationStatus(string? detail, bool canUndo)
    {
        if (!string.IsNullOrWhiteSpace(detail))
        {
            OptimizationStatusText.Text = detail;
        }

        OptimizationUndoButton.IsEnabled = canUndo;
    }

    // ---------- Diseño D13: Audit Log orientado al usuario ----------

    public event EventHandler? AuditRefreshRequested;

    /// <summary>
    /// Diseño D18 — deshacer DESDE el registro. La entrada ya sabía cómo revertirse desde D13 y le
    /// faltaba el botón: un "cómo deshacerlo" que obliga a ir a buscar el comando correcto es media
    /// promesa.
    /// </summary>
    public event Action<AuditEntry>? AuditRevertRequested;

    private void AuditRefreshButton_Click(object sender, RoutedEventArgs e) =>
        AuditRefreshRequested?.Invoke(this, EventArgs.Empty);

    private void AuditRevertButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: AuditEntry entry })
        {
            AuditRevertRequested?.Invoke(entry);
        }
    }

    /// <summary>
    /// Diseño D13 — cada entrada muestra los cuatro datos que el modelo de confianza exige, en
    /// campos separados. "Sin vuelta atrás" se enseña igual que una reversión disponible: callarlo
    /// haría parecer que todo se puede deshacer.
    /// </summary>
    public void UpdateAudit(IReadOnlyList<AuditEntry> entries)
    {
        _auditRows.Clear();

        foreach (var entry in (entries ?? []).Take(25))
        {
            _auditRows.Add(new AuditRow(
                Entry: entry,
                Headline: $"{entry.Capability} · {entry.Action}",
                When: entry.At.ToString("dd/MM HH:mm"),
                Detail: entry.Detail,
                PermissionLine: string.IsNullOrWhiteSpace(entry.Permission)
                    ? "Permiso: —"
                    : $"Permiso: {entry.Permission}" +
                      (entry.AutonomyLevel is { } level ? $" · nivel de autonomía {level}" : string.Empty),
                ReversalLine: string.IsNullOrWhiteSpace(entry.RevertHint)
                    ? "Sin vuelta atrás automática."
                    : $"Cómo deshacerlo: {entry.RevertHint}",

                // El botón solo aparece cuando la entrada trae con qué deshacer. Enseñarlo
                // deshabilitado en todas las demás llenaría el registro de botones muertos.
                RevertVisibility: entry.CanRevert ? Visibility.Visible : Visibility.Collapsed));
        }

        AuditEmptyText.Visibility = _auditRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private sealed record AuditRow(
        AuditEntry Entry,
        string Headline,
        string When,
        string Detail,
        string PermissionLine,
        string ReversalLine,
        Visibility RevertVisibility)
    {
        // El Narrador anuncia cada elemento con su ToString; el de un record vuelca todos los campos.
        public override string ToString() => Headline;
    }

    public void UpdateSnapshot(SystemSnapshot snapshot)
    {
        TopProcessNameText.Text = string.IsNullOrWhiteSpace(snapshot.TopProcessName)
            ? "No disponible"
            : snapshot.TopProcessName;

        TopProcessMemoryText.Text = snapshot.TopProcessWorkingSetBytes.HasValue
            ? FormatBytes(snapshot.TopProcessWorkingSetBytes.Value)
            : string.Empty;

        var health = SystemHealthEvaluator.Evaluate(snapshot);
        HealthText.Text = SystemHealthEvaluator.GetLabel(health);
        HealthText.Foreground = health == SystemHealthState.Ready
            ? (Brush)FindResource("BrushSuccess")
            : (Brush)FindResource("BrushWarning");

        UpdatedAtText.Text = snapshot.CapturedAt == DateTimeOffset.MinValue
            ? string.Empty
            : snapshot.CapturedAt.ToString("HH:mm:ss");
    }

    public void UpdateRuntimeStatus(
        bool voiceReady,
        bool wakeWordEnabled,
        bool wakeWordListening,
        bool visionEnabled,
        string aiStatus,
        bool aiHealthy,
        ResourceMode resourceMode,
        string resourceReason)
    {
        RuntimeVoiceText.Text = wakeWordEnabled
            ? wakeWordListening ? "Atenta" : "Pausada"
            : voiceReady ? "Micrófono listo" : "No preparada";
        RuntimeVoiceText.Foreground = wakeWordListening || (!wakeWordEnabled && voiceReady)
            ? (Brush)FindResource("BrushSuccess")
            : (Brush)FindResource("BrushWarning");

        RuntimeAiText.Text = string.IsNullOrWhiteSpace(aiStatus) ? "Desactivada" : aiStatus;
        RuntimeAiText.Foreground = aiHealthy
            ? (Brush)FindResource("BrushSuccess")
            : (Brush)FindResource("BrushTextSecondary");

        RuntimeVisionText.Text = visionEnabled ? "Activa bajo demanda" : "Desactivada";
        RuntimeVisionText.Foreground = visionEnabled
            ? (Brush)FindResource("BrushSuccess")
            : (Brush)FindResource("BrushTextSecondary");

        RuntimeModeText.Text = resourceMode switch
        {
            ResourceMode.Game => "Modo Juego",
            ResourceMode.Busy => "Protegiendo recursos",
            _ => "Normal"
        };
        RuntimeModeText.Foreground = resourceMode == ResourceMode.Normal
            ? (Brush)FindResource("BrushSuccess")
            : (Brush)FindResource("BrushWarning");
        RuntimeDetailText.Text = string.IsNullOrWhiteSpace(resourceReason)
            ? "Sakura está lista."
            : resourceReason;
    }

    public void ShowHardwareCapabilityUpdating()
    {
        HardwareCapabilityRefreshButton.IsEnabled = false;
        HardwareCapabilitySummaryText.Text = "Actualizando la detección de hardware…";
    }

    public void UpdateHardwareCapability(HardwareCapabilityProfile profile)
    {
        HardwareCapabilityRefreshButton.IsEnabled = true;

        HardwareCapabilityTierText.Text = DescribeTier(profile.Tier);
        HardwareCapabilitySummaryText.Text = profile.Summary;

        var processor = profile.Snapshot.Processor;
        HardwareCapabilityCpuText.Text = string.IsNullOrWhiteSpace(processor.Name)
            ? "No disponible"
            : processor.Name;

        HardwareCapabilityCoresText.Text = processor.PhysicalCores.HasValue || processor.LogicalProcessors.HasValue
            ? $"{FormatCount(processor.PhysicalCores)} físicos · {FormatCount(processor.LogicalProcessors)} lógicos"
            : "No disponible";

        HardwareCapabilityRamText.Text = profile.Snapshot.Memory.TotalPhysicalBytes.HasValue
            ? FormatBytes((long)profile.Snapshot.Memory.TotalPhysicalBytes.Value)
            : "No disponible";

        HardwareCapabilityArchitectureText.Text = FormatArchitecture(processor);

        var graphics = profile.Snapshot.PreferredGraphicsAdapter;
        HardwareCapabilityGpuText.Text = string.IsNullOrWhiteSpace(graphics.Name)
            ? "No disponible"
            : graphics.Name;

        HardwareCapabilityGraphicsMemoryText.Text = graphics.DedicatedMemoryBytes.HasValue
            ? FormatBytes(graphics.DedicatedMemoryBytes.Value)
            : "No disponible";

        HardwareCapabilityBatteryText.Text = profile.Snapshot.HasBattery switch
        {
            true => "Presente",
            false => "No detectada",
            null => "No disponible"
        };

        var missing = profile.MissingData.Select(reason => reason.Message).ToList();
        HardwareCapabilityMissingDataText.Text = missing.Count > 0
            ? "No se pudo leer: " + string.Join(" · ", missing)
            : profile.HasCompleteData ? string.Empty : "Faltan algunos datos del equipo.";
        HardwareCapabilityMissingDataText.Visibility = HardwareCapabilityMissingDataText.Text.Length > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public void UpdateAdaptiveEnginePlan(AdaptiveEnginePlan plan, IReadOnlyList<EngineDescriptor> descriptors)
    {
        AdaptiveEnginePlanModeText.Text = DescribeMode(plan.Mode);
        AdaptiveEnginePlanSummaryText.Text = plan.Summary;

        // Solo se dice algo cuando hay algo que decir: «la información es suficiente» en cada visita
        // era una frase que no cambiaba nada de lo que se ve debajo.
        AdaptiveEnginePlanConfidenceText.Text = string.Join(" ", plan.GeneralWarnings);
        AdaptiveEnginePlanConfidenceText.Visibility = plan.GeneralWarnings.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        _adaptiveEnginePlanRows.Clear();
        foreach (var recommendation in plan.Recommendations)
        {
            _adaptiveEnginePlanRows.Add(BuildAdaptiveEnginePlanRow(recommendation, descriptors));
        }
    }

    private static AdaptiveEnginePlanRow BuildAdaptiveEnginePlanRow(
        EngineRecommendation recommendation,
        IReadOnlyList<EngineDescriptor> descriptors)
    {
        var recommendedLine = recommendation.RecommendedEngineId is { } recommendedId
            ? ResolveDisplayName(recommendedId, descriptors)
            : "Ninguna opción compatible";

        var warningText = string.Join(" · ", recommendation.Warnings.Select(w => w.TrimEnd('.')));

        return new AdaptiveEnginePlanRow(
            DescribeCategory(recommendation.Category),
            recommendedLine,
            DescribeState(recommendation, descriptors),
            warningText,
            WarningVisibility: warningText.Length > 0 ? Visibility.Visible : Visibility.Collapsed,
            RecommendationOnlyVisibility: recommendation.IsRecommendationOnly ? Visibility.Visible : Visibility.Collapsed);
    }

    private static string ResolveDisplayName(EngineIdentifier id, IReadOnlyList<EngineDescriptor> descriptors) =>
        descriptors.FirstOrDefault(d => d.Id == id)?.DisplayName ?? id.Value;

    /// <summary>
    /// Cómo está ahora, en una línea. Lo desconocido no se enseña: «Activo: desconocido» en todas las
    /// tarjetas no decía nada, solo ocupaba sitio.
    /// </summary>
    private static string DescribeState(EngineRecommendation recommendation, IReadOnlyList<EngineDescriptor> descriptors)
    {
        var recommended = recommendation.RecommendedEngineId;

        if (recommended is not null && recommendation.ActiveEngineId == recommended)
        {
            return "Ya está en uso.";
        }

        var parts = new List<string>();

        if (recommendation.ActiveEngineId is { } active)
        {
            parts.Add($"En uso: {ResolveDisplayName(active, descriptors)}");
        }

        if (recommendation.ConfiguredEngineId is not { } configured)
        {
            parts.Add("Sin configurar");
        }
        else if (configured == recommended)
        {
            parts.Add("Ya configurado");
        }
        else if (configured != recommendation.ActiveEngineId)
        {
            parts.Add($"Configurado: {ResolveDisplayName(configured, descriptors)}");
        }

        return string.Join(" · ", parts) + ".";
    }

    private static string DescribeMode(HardwarePerformanceMode mode) => mode switch
    {
        HardwarePerformanceMode.Automatic => "Automático",
        HardwarePerformanceMode.Eco => "Ahorro",
        HardwarePerformanceMode.Balanced => "Equilibrado",
        HardwarePerformanceMode.Maximum => "Máximo",
        _ => "Automático"
    };

    private static string DescribeCategory(EngineCategory category) => category switch
    {
        EngineCategory.SpeechToText => "Reconocimiento de voz",
        EngineCategory.WakeWord => "Palabra de activación",
        EngineCategory.TextToSpeech => "Síntesis de voz",
        EngineCategory.LocalLanguageModel => "Modelo de lenguaje",
        _ => category.ToString()
    };

    private sealed record AdaptiveEnginePlanRow(
        string CategoryLabel,
        string RecommendedLine,
        string StateLine,
        string WarningText,
        Visibility WarningVisibility,
        Visibility RecommendationOnlyVisibility)
    {
        public override string ToString() => CategoryLabel;
    }

    private void RestartVoiceButton_Click(object sender, RoutedEventArgs e) =>
        RestartVoiceRequested?.Invoke(this, EventArgs.Empty);

    private void DiagnosticsButton_Click(object sender, RoutedEventArgs e) =>
        DiagnosticsRequested?.Invoke(this, EventArgs.Empty);

    private void HardwareCapabilityRefreshButton_Click(object sender, RoutedEventArgs e) =>
        HardwareCapabilityRefreshRequested?.Invoke(this, EventArgs.Empty);

    private static string DescribeTier(HardwareCapabilityTier tier) => tier switch
    {
        HardwareCapabilityTier.Basic => "Básico",
        HardwareCapabilityTier.Standard => "Estándar",
        HardwareCapabilityTier.Accelerated => "Acelerado",
        HardwareCapabilityTier.HighPerformance => "Alto rendimiento",
        _ => "Desconocido"
    };

    private static string FormatArchitecture(ProcessorCapability processor)
    {
        if (string.IsNullOrWhiteSpace(processor.OperatingSystemArchitecture))
        {
            return "No disponible";
        }

        if (!string.IsNullOrWhiteSpace(processor.ProcessArchitecture) &&
            processor.ProcessArchitecture != processor.OperatingSystemArchitecture)
        {
            return $"{processor.OperatingSystemArchitecture} (proceso: {processor.ProcessArchitecture})";
        }

        return processor.OperatingSystemArchitecture;
    }

    private static string FormatCount(int? value) =>
        value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "?";

    private static string FormatPercentage(double? value) =>
        value.HasValue ? $"{value.Value:0}%" : "—";

    private static string FormatBytes(long bytes)
    {
        const double megabyte = 1024d * 1024d;
        const double gigabyte = 1024d * 1024d * 1024d;

        return bytes >= gigabyte
            ? $"{bytes / gigabyte:0.0} GB"
            : $"{bytes / megabyte:0} MB";
    }
}
