using System.IO;
using System.Windows;
using Nexo.App.WindowsIntegration;
using Nexo.Core.Diagnostics;
using Nexo.Core.WindowsIntegration;
using Nexo.Windows.Composition;
using Nexo.Windows.Settings;
using Nexo.Windows.Storage;
using Nexo.Windows.WindowsIntegration;

namespace Nexo.App;

public partial class App : System.Windows.Application
{
    private SingleInstanceCoordinator? _singleInstance;
    private ManagedOllamaSupervisor? _managedOllamaSupervisor;
    private SakuraCompositionRoot? _compositionRoot;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Diseño D3.2 — cuando KOHANA_DATA_ROOT (u otro override explícito) está activo, se dejan
        // dos rastros honestos: un archivo dedicado bajo la propia raíz aislada (nunca en el
        // perfil real) y la salida de depuración, sin exponer la ruta a quien no la pidió (no hay
        // selector de esto en la UI normal). No se declara "aislado" solo porque la variable
        // exista: DescribeActiveRoot ya normaliza valores vacíos o inválidos a "sin override".
        if (NexoDataPaths.IsUsingOverrideRoot)
        {
            var (root, source) = NexoDataPaths.DescribeActiveRoot();
            System.Diagnostics.Debug.WriteLine(
                $"[Sakura] Perfil de validación activo — raíz: {root} (origen: {source})");
            try
            {
                Directory.CreateDirectory(NexoDataPaths.LogsDirectory);
                File.AppendAllText(
                    Path.Combine(NexoDataPaths.LogsDirectory, "data-root.log"),
                    $"{DateTimeOffset.Now:O} Perfil de validación activo — raíz: {root} " +
                    $"(origen: {source}){Environment.NewLine}");
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                // Dejar constancia es una cortesía de diagnóstico, no un requisito para arrancar.
            }
        }

        // La migración es conservadora: copia datos de Nexo a Sakura sin borrar ni sobrescribir
        // la carpeta anterior. Un fallo no impide abrir la app. Cuando hay un perfil de
        // validación activo, NexoDataPaths.LegacyRootDirectory ya colapsa a la misma raíz que
        // RootDirectory, así que el propio guard de origen==destino de MigrateIfNeeded evita leer
        // o copiar los datos reales del usuario hacia el perfil aislado.
        try
        {
            LegacyDataMigrator.MigrateIfNeeded();

            // Diseño D70 — y los modelos de voz y el runtime, que la migración normal deja fuera
            // por su tamaño, se traen moviéndolos. Sin esto la aplicación sigue leyéndolos de una
            // carpeta con un nombre que ya no es el suyo, y borrarla la deja muda.
            LegacyDataMigrator.ConsolidateHeavyFolders();
        }
        catch (Exception)
        {
            // La configuración puede recrearse con valores seguros.
        }
        _singleInstance = new SingleInstanceCoordinator();
        if (!_singleInstance.IsPrimaryInstance)
        {
            _singleInstance.SignalPrimaryInstance();
            Shutdown();
            return;
        }

        var settingsStore = new JsonSettingsStore();
        var preferences = settingsStore.Load();
        var requestedHiddenStart = StartupCommandBuilder.ShouldStartHidden(e.Args) ||
            Nexo.Windows.Distribution.WindowsDistributionChannel.WasLaunchedByStartupTask();

        if (!preferences.HasCompletedOnboarding)
        {
            var onboarding = new OnboardingWindow(preferences, settingsStore);
            onboarding.ShowDialog();
            requestedHiddenStart = false;
        }

        _managedOllamaSupervisor = new ManagedOllamaSupervisor();
        _compositionRoot = new SakuraCompositionRoot();

        var mainWindow = new MainWindow(
            requestedHiddenStart,
            _managedOllamaSupervisor,
            _compositionRoot.AiChatService,
            _compositionRoot.AudioMixerService,
            _compositionRoot.ScreenCaptureService,
            _compositionRoot.VoiceCoordinator,
            _compositionRoot.HardwareCapabilityService,
            _compositionRoot.AdaptiveEngineRegistry);
        MainWindow = mainWindow;

        _singleInstance.ActivationRequested += (_, _) =>
            Dispatcher.BeginInvoke(new Action(mainWindow.ShowFromBackground));
        _singleInstance.StartListening();

        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // El runtime de IA administrado ya se detuvo de forma asíncrona en la ruta de
        // salida (MainWindow.RequestExitAsync), antes de Application.Shutdown, mientras el
        // Dispatcher aún bombeaba. Aquí el Dispose solo libera recursos sin bloquear.
        _managedOllamaSupervisor?.Dispose();
        _managedOllamaSupervisor = null;

        // SakuraCompositionRoot es dueño del subsistema de voz (el coordinador más Whisper,
        // TTS y Vosk) y los libera aquí, después de que Window_Closed ya haya cancelado el
        // token de vida y desuscrito los eventos de wake word. El ServiceProvider no libera
        // instancias que no creó él mismo (verificado), así que esta es la única ruta de
        // Dispose de esos servicios. Los demás servicios de interfaz (IA, mezclador, captura)
        // mantienen su liberación anterior.
        _compositionRoot?.Dispose();
        _compositionRoot = null;

        _singleInstance?.Dispose();
        _singleInstance = null;
        base.OnExit(e);
    }
}
