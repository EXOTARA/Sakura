using System.Reflection;
using System.Runtime.InteropServices;
using Nexo.Core.Ai;
using Nexo.Core.Diagnostics;
using Nexo.Core.Settings;
using Nexo.Core.Voice;

namespace Nexo.Windows.Diagnostics;

public sealed class NexoDiagnosticService : IDisposable
{
    private readonly IOllamaModelService _modelService;
    private readonly bool _ownsModelService;

    public NexoDiagnosticService(IOllamaModelService? modelService = null)
    {
        _modelService = modelService ?? new Nexo.Windows.Ai.OllamaModelService();
        _ownsModelService = modelService is null;
    }

    public async Task<NexoDiagnosticSnapshot> CaptureAsync(
        ShellPreferences preferences,
        IReadOnlyList<VoiceInputDevice> voiceDevices,
        bool whisperReady,
        bool wakeWordReady,
        bool wakeWordListening,
        bool trayActive,
        bool startupEnabled,
        CancellationToken cancellationToken = default)
    {
        var items = new List<DiagnosticItem>();
        var selectedMicrophone = voiceDevices.FirstOrDefault(device =>
            device.DeviceNumber == preferences.VoiceInputDeviceNumber);
        items.Add(new DiagnosticItem(
            "Micrófono",
            selectedMicrophone is null ? DiagnosticStatus.Warning : DiagnosticStatus.Ready,
            selectedMicrophone?.Name ?? "El dispositivo configurado no está disponible."));
        items.Add(new DiagnosticItem(
            "Whisper",
            whisperReady ? DiagnosticStatus.Ready : DiagnosticStatus.Warning,
            whisperReady
                ? "Modelo local preparado."
                : "Se preparará o descargará cuando uses Mic."));
        items.Add(new DiagnosticItem(
            "Frase de activación",
            !preferences.WakeWordEnabled
                ? DiagnosticStatus.Information
                : wakeWordListening
                    ? DiagnosticStatus.Ready
                    : DiagnosticStatus.Warning,
            !preferences.WakeWordEnabled
                ? "Desactivada."
                : wakeWordListening
                    ? "Escuchando localmente."
                    : wakeWordReady
                        ? "Preparada, pero no está escuchando."
                        : "El modelo de activación todavía no está listo."));

        await AddAiStatusAsync(items, preferences, cancellationToken);

        items.Add(new DiagnosticItem(
            "Lens",
            preferences.VisionEnabled ? DiagnosticStatus.Ready : DiagnosticStatus.Information,
            preferences.VisionEnabled
                ? "Capturas bajo demanda habilitadas."
                : "Desactivado por preferencia."));
        items.Add(new DiagnosticItem(
            "Bandeja del sistema",
            trayActive ? DiagnosticStatus.Ready : DiagnosticStatus.Warning,
            trayActive ? "Activa." : "No se pudo confirmar el icono de bandeja."));
        items.Add(new DiagnosticItem(
            "Inicio con Windows",
            startupEnabled ? DiagnosticStatus.Ready : DiagnosticStatus.Information,
            startupEnabled ? "Registrado para el usuario actual." : "Desactivado."));
        items.Add(BuildDataStatus());

        var assembly = Assembly.GetEntryAssembly() ?? typeof(NexoDiagnosticService).Assembly;
        var version = assembly.GetName().Version?.ToString() ?? "desconocida";
        return new NexoDiagnosticSnapshot(
            DateTimeOffset.Now,
            version,
            RuntimeInformation.OSDescription,
            RuntimeInformation.FrameworkDescription,
            NexoDataPaths.RootDirectory,
            items);
    }

    public string ClearTemporaryData()
    {
        var removed = 0;
        try
        {
            Directory.CreateDirectory(NexoDataPaths.RootDirectory);
            foreach (var file in Directory.EnumerateFiles(
                         NexoDataPaths.RootDirectory,
                         "*.tmp",
                         SearchOption.AllDirectories))
            {
                try
                {
                    File.Delete(file);
                    removed++;
                }
                catch
                {
                    // Un archivo en uso no debe impedir limpiar los demás.
                }
            }

            var tempDirectory = Path.Combine(NexoDataPaths.RootDirectory, "Temp");
            if (Directory.Exists(tempDirectory))
            {
                foreach (var file in Directory.EnumerateFiles(tempDirectory))
                {
                    try
                    {
                        File.Delete(file);
                        removed++;
                    }
                    catch
                    {
                    }
                }
            }

            return removed == 1
                ? "Se eliminó 1 archivo temporal."
                : $"Se eliminaron {removed} archivos temporales.";
        }
        catch (Exception exception)
        {
            return $"No pude limpiar los temporales: {exception.Message}";
        }
    }

    public void Dispose()
    {
        if (_ownsModelService && _modelService is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private async Task AddAiStatusAsync(
        ICollection<DiagnosticItem> items,
        ShellPreferences preferences,
        CancellationToken cancellationToken)
    {
        if (preferences.AiProvider == AiProviderKind.Disabled)
        {
            items.Add(new DiagnosticItem(
                "Inteligencia artificial",
                DiagnosticStatus.Information,
                "Desactivada; los comandos locales continúan disponibles."));
            return;
        }

        if (!AiProviderDefaults.UsesOllamaProtocol(preferences.AiProvider))
        {
            items.Add(new DiagnosticItem(
                "Inteligencia artificial",
                DiagnosticStatus.Information,
                $"Proveedor configurado: {AiProviderDefaults.Get(preferences.AiProvider).DisplayName}."));
            return;
        }

        try
        {
            var models = await _modelService.ListAsync(
                preferences.AiBaseUrl,
                cancellationToken);
            var selectedExists = models.Any(model =>
                model.Name.Equals(preferences.AiModel, StringComparison.OrdinalIgnoreCase));
            items.Add(new DiagnosticItem(
                "Ollama",
                models.Count == 0
                    ? DiagnosticStatus.Warning
                    : selectedExists
                        ? DiagnosticStatus.Ready
                        : DiagnosticStatus.Warning,
                models.Count == 0
                    ? "Conectado, pero no hay modelos instalados."
                    : selectedExists
                        ? $"Conectado · {models.Count} modelo(s) · activo: {preferences.AiModel}."
                        : $"Conectado · {models.Count} modelo(s), pero {preferences.AiModel} no está instalado."));
        }
        catch (Exception exception) when (
            exception is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or UriFormatException)
        {
            items.Add(new DiagnosticItem(
                "Ollama",
                DiagnosticStatus.Unavailable,
                $"No disponible: {exception.Message}"));
        }
    }

    private static DiagnosticItem BuildDataStatus()
    {
        try
        {
            Directory.CreateDirectory(NexoDataPaths.RootDirectory);
            var knownFiles = new[]
            {
                NexoDataPaths.Settings,
                NexoDataPaths.Tasks,
                NexoDataPaths.Focus,
                NexoDataPaths.Routines,
                NexoDataPaths.Conversation
            };
            var existing = knownFiles.Count(File.Exists);
            var backups = Directory.EnumerateFiles(
                    NexoDataPaths.RootDirectory,
                    "*.corrupt-*",
                    SearchOption.TopDirectoryOnly)
                .Count();
            var detail = $"{existing} archivo(s) de datos encontrado(s).";
            if (backups > 0)
            {
                // Un .corrupt-* es la copia de un archivo que no se pudo leer, guardada al lado. Sakura no
                // la restaura sola: llamarla «respaldo de recuperación disponible» prometía algo que
                // ningún código cumple. No lleva la ruta: este texto va al paquete de soporte y la ruta
                // trae el nombre de usuario de Windows.
                detail += $" {backups} copia(s) de archivos dañados guardada(s) junto a tus datos; " +
                    "Sakura no las restaura sola.";
            }

            return new DiagnosticItem(
                "Datos locales",
                DiagnosticStatus.Ready,
                detail);
        }
        catch (Exception exception)
        {
            return new DiagnosticItem(
                "Datos locales",
                DiagnosticStatus.Warning,
                $"No pude revisar la carpeta: {exception.Message}");
        }
    }
}
