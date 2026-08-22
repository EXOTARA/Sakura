using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Nexo.App.Ambient;
using Nexo.Core.Documents;
using Nexo.Core.Updates;
using Nexo.App.Automation;
using Nexo.App.DailyFlow;
using Nexo.App.Motion;
using Nexo.App.Optimization;
using Nexo.Windows.Display;
using Nexo.Windows.Documents;
using Nexo.Windows.Updates;
using Nexo.Windows.Metrics;
using Nexo.Windows.Shell;
using Nexo.App.WindowsIntegration;
using Nexo.App.Shell;
using Nexo.App.Views;
using Nexo.Core.Ai;
using Nexo.Core.Ambient;
using Nexo.Core.Assistant;
using Nexo.Core.Audit;
using Nexo.Core.AdaptiveEngine;
using Nexo.Core.Automation;
using Nexo.Core.Audio;
using Nexo.Core.Commands;
using Nexo.Core.Commands.CommandCenter;
using Nexo.Core.ComputerUse;
using Nexo.Core.Diagnostics;
using Nexo.Core.Display;
using Nexo.Core.Flow;
using Nexo.Core.Focus;
using Nexo.Core.Hardware;
using Nexo.Core.Media;
using Nexo.Core.Memory;
using Nexo.Core.Metrics;
using Nexo.Core.Optimization;
using Nexo.Core.Permissions;
using Nexo.Core.Productization;
using Nexo.Core.SelfCheck;
using Nexo.Core.Sequences;
using Nexo.Core.Resources;
using Nexo.Core.Settings;
using Nexo.Core.Shell;
using Nexo.Core.Skills;
using Nexo.Core.Tasks;
using Nexo.Core.Voice;
using Nexo.Core.Vision;
using Nexo.Core.Workspace;
using Nexo.Windows.Ai;
using Nexo.Windows.Ambient;
using Nexo.Windows.Audit;
using Nexo.Windows.Diagnostics;
using Nexo.Windows.ComputerUse;
using Nexo.Windows.Automation;
using Nexo.Windows.Assistant;
using Nexo.Windows.Audio;
using Nexo.Windows.Flow;
using Nexo.Windows.Focus;
using Nexo.Windows.Media;
using Nexo.Windows.Memory;
using Nexo.Windows.Optimization;
using Nexo.Windows.Productization;
using Nexo.Windows.SelfCheck;
using Nexo.Windows.Resources;
using Nexo.Windows.Settings;
using Nexo.Windows.Skills;
using Nexo.Windows.Tasks;
using Nexo.Windows.Voice;
using Nexo.Windows.Vision;
using Nexo.Windows.WindowsIntegration;
using Nexo.Windows.Workspace;
using NexoFocusManager = Nexo.Core.Focus.FocusManager;

namespace Nexo.App;

public partial class MainWindow : Window
{
    private const int ShellHotkeyId = 0x4E58;
    private const int PeekHotkeyId = 0x4E59;
    private const int CommandPaletteHotkeyId = 0x4E5A;
    private const int LookHotkeyId = 0x4E5B;

    /// <summary>Diseño D6.3 (Fase 3 — Sakura Flow) — atajo global de dictado.</summary>
    private const int FlowHotkeyId = 0x4E5C;

    /// <summary>
    /// Diseño D58 — Escape cierra el shell aunque el teclado esté en otra aplicación.
    ///
    /// Hacía falta clicar Sakura antes de poder cerrarla con Escape, y el motivo no era el atajo
    /// sino Windows: el bloqueo de primer plano se niega a dar el foco a una ventana que nadie ha
    /// clicado, así que <c>Activate()</c> no basta y las teclas se quedaban en la aplicación de
    /// antes. Como el manejador de Escape cuelga de la ventana, nunca se enteraba.
    ///
    /// **Se registra solo mientras el shell está en pantalla y se suelta al ocultarse.** Un Escape
    /// global permanente se lo quitaría a todo el sistema, que es justo lo que no se hizo con
    /// Ctrl+K por la misma razón. Mientras Sakura está delante, Escape es suyo; en cuanto se va,
    /// vuelve a quien lo tuviera.
    /// </summary>
    private const int EscapeHotkeyId = 0x4E5D;

    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint VirtualKeyA = 0x41;
    private const uint VirtualKeySpace = 0x20;
    private const uint VirtualKeyD = 0x44;
    private const uint VirtualKeyEscape = 0x1B;
    private const uint ModNone = 0x0000;
    private const int WmHotkey = 0x0312;
    private const int WmPowerBroadcast = 0x0218;
    private const int PbtApmResumeSuspend = 0x0007;
    private const int PbtApmResumeAutomatic = 0x0012;

    private readonly DispatcherTimer _clockTimer;
    private readonly DispatcherTimer _metricsTimer;
    private readonly DispatcherTimer _taskReminderTimer;
    private readonly DispatcherTimer _focusTickTimer;
    private readonly DispatcherTimer _visualContextExpiryTimer = new();
    private readonly JsonSettingsStore _settingsStore = new();
    private readonly WindowsStartupService _startupService = new();
    private readonly JsonConversationStore _conversationStore = new();
    private readonly NaturalCommandParser _commandParser = new();
    private readonly SpanishTaskCommandParser _taskCommandParser = new();
    private readonly SpanishFocusCommandParser _focusCommandParser = new();
    private readonly SpanishRoutineCommandParser _routineCommandParser = new();
    private readonly IAiChatService _aiChatService;
    private readonly IAudioMixerService _audioMixerService;
    private readonly IScreenCaptureService _screenCaptureService;
    private readonly VoiceCoordinator _voiceCoordinator;
    private readonly IHardwareCapabilityService _hardwareCapabilityService;
    private readonly IAdaptiveEngineRegistry _adaptiveEngineRegistry;
    private OllamaRuntimeSnapshot? _latestOllamaRuntimeSnapshot;
    private readonly SemaphoreSlim _aiGate = new(1, 1);

    // Serializa las DECISIONES del Resource Governor (pausar/reanudar wake word según el
    // modo de recursos y la bandera _resourceGovernorWakeWordPaused), no el acceso físico
    // a Vosk: las operaciones reales del motor pasan después por el ámbito de wake word del
    // coordinador (vía PauseWakeWordAsync/ApplyWakeWordPreferenceAsync). No es un candado
    // del subsistema de voz —esos viven en VoiceCoordinator—; por eso vive en MainWindow.
    private readonly SemaphoreSlim _resourceGovernorDecisionGate = new(1, 1);
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly WindowsSystemMetricsService _metricsService = new();
    private readonly WindowsResourceGovernorService _resourceGovernorService = new();
    private readonly ShellPreferences _preferences;
    private readonly JsonTaskStore _taskStore = new();
    private readonly TaskManager _taskManager;
    private readonly JsonFocusStore _focusStore = new();
    private readonly NexoFocusManager _focusManager;
    private readonly JsonRoutineStore _routineStore = new();
    private readonly RoutineManager _routineManager;
    private readonly RoutineRunner _routineRunner;
    private readonly JsonAmbientRequestHistoryStore _ambientRequestStore = new();
    private readonly AmbientRequestManager _ambientRequestManager;
    private readonly WindowsAmbientContextProvider _ambientContextProvider = new();

    /// <summary>
    /// Diseño D4 (corrección post smoke test manual) — fuente propia y en tiempo real de "última
    /// ventana ajena en primer plano" para el Context Snapshot ambiental. Deliberadamente
    /// independiente de <see cref="_lastExternalWindowHandle"/> (mecanismo existente de Vision/
    /// Peek, que solo se actualiza en puntos concretos como <see cref="RememberForegroundWindow"/>
    /// y no ante un Alt+Tab normal) para no alterar ese comportamiento ya probado.
    /// </summary>
    private readonly ForegroundWindowTracker _ambientForegroundTracker = new();

    /// <summary>Diseño D5 (Fase 2 — Sakura Lens) — OCR y lectura de UI Automation, ambos sin estado.</summary>
    private readonly WindowsOcrService _lensOcrService = new();
    private readonly WindowsUiAutomationReader _lensUiAutomationReader = new();
    private readonly LensHighlightOverlay _lensHighlightOverlay = new();

    /// <summary>Diseño D8 (Fase 4) — optimización adaptativa del equipo.</summary>
    private readonly WindowsOptimizationApplier _optimizationApplier = new();
    private readonly JsonOptimizationSnapshotStore _optimizationSnapshotStore = new();
    /// <summary>
    /// Diseño D13 — el Audit Log único: qué hizo Sakura, cuándo, con qué permiso y cómo deshacerlo.
    /// Lo comparten todas las capacidades; un registro por capacidad obligaría a la persona a saber
    /// de antemano en cuál mirar.
    /// </summary>
    private readonly JsonSakuraAuditLog _auditLog = new();

    /// <summary>
    /// Diseño D11 (Fase 4) — orquesta aplicar, verificar, deshacer y registrar. Se crea en el
    /// constructor porque necesita las preferencias ya cargadas para el objetivo "consumo de
    /// Sakura".
    /// </summary>
    private readonly OptimizationCoordinator _optimizationCoordinator;

    /// <summary>Diseño D9 (Fase 6) — memoria opt-in, cifrada en reposo.</summary>
    private readonly MemoryManager _memoryManager = new(new DpapiMemoryStore());

    /// <summary>
    /// Diseño D10 — recuerdo propuesto que espera un sí. Nunca se guarda solo: mientras vive aquí
    /// no está en la memoria.
    /// </summary>
    private MemoryCandidate? _pendingMemoryCandidate;

    /// <summary>Diseño D12 (Fase 5) — lectura de solo-lectura del proyecto autorizado.</summary>
    private readonly FileSystemWorkspaceReader _workspaceReader = new();

    /// <summary>
    /// Diseño D12 — contexto del proyecto para la SIGUIENTE consulta, y solo para ésa. No se manda
    /// en todas: el proyecto solo sale del equipo cuando la persona pidió algo sobre el proyecto.
    /// </summary>
    private string? _pendingWorkspaceContext;

    /// <summary>
    /// Corrección de un defecto real (probado a mano tras D14): rutas de archivos existentes cuyo
    /// contenido SÍ viajó en <see cref="_pendingWorkspaceContext"/> de esta consulta. Es la
    /// salvaguarda contra la sustitución accidental de un archivo que el modelo nunca llegó a ver:
    /// <see cref="OfferWorkspaceEdit"/> se niega a aplicar un cambio a un archivo existente que no
    /// esté en este conjunto, en vez de confiar en que el modelo lo haya conservado bien.
    /// </summary>
    private HashSet<string> _pendingWorkspaceEditKnownContentPaths = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Diseño D13 — la paleta no acepta argumentos: la consulta de búsqueda llega por chat.</summary>
    private bool _awaitingWorkspaceSearchQuery;

    /// <summary>Diseño D17 (Fase 7) — qué métodos puede usar Sakura para actuar en este equipo.</summary>
    private readonly WindowsComputerUseMethodProbe _computerUseProbe = new();

    /// <summary>Diseño D17 — igual que la búsqueda: lo que se quiere hacer llega por chat.</summary>
    private bool _awaitingComputerUseIntent;

    /// <summary>Diseño D18 (Fase 7, nivel 4) — el único camino por el que Sakura actúa en el equipo.</summary>
    private readonly ComputerUseCoordinator _computerUseCoordinator;

    /// <summary>Diseño D20 (Fase 9) — copia verificada de los datos, para poder volver.</summary>
    private readonly FileSystemDataBackupService _backupService = new();

    /// <summary>Diseño D21 (Fase 9) — el mismo diagnóstico que ve la ventana, para poder exportarlo.</summary>
    private readonly NexoDiagnosticService _diagnosticService = new();

    /// <summary>Diseño D22 — comprobaciones que tocan disco, en una carpeta temporal aparte.</summary>
    private readonly WindowsSelfCheckProbes _selfCheckProbes = new();

    /// <summary>Diseño D24 (nivel 5) — encadena pasos parando en los puntos de riesgo.</summary>
    private readonly SequenceCoordinator _sequenceCoordinator;

    private readonly UpdateSafetyCoordinator _updateSafety;

    /// <summary>Diseño D14 — igual que la búsqueda: la instrucción del cambio llega por chat.</summary>
    private bool _awaitingWorkspaceEditInstruction;

    /// <summary>
    /// Diseño D14 — la SIGUIENTE respuesta de la IA puede traer un cambio propuesto. Solo esa: sin
    /// esta marca, cualquier respuesta que por casualidad contuviera el formato ofrecería escribir
    /// en el proyecto, y nadie lo habría pedido.
    /// </summary>
    private bool _pendingWorkspaceEditRequested;

    /// <summary>
    /// Diseño D14 (Fase 5, nivel 4) — checkpoints y escritura. Existen siempre, pero
    /// <see cref="WorkspaceEditCoordinator"/> se niega a escribir por debajo del nivel 4, así que
    /// tenerlos construidos no concede nada.
    /// </summary>
    private readonly JsonWorkspaceCheckpointStore _workspaceCheckpointStore = new();

    private readonly WorkspaceEditCoordinator _workspaceEditCoordinator;

    /// <summary>Diseño D15 (Fase 8) — packs que dejan configuradas capacidades ya existentes.</summary>
    private readonly SkillPackCoordinator _skillPackCoordinator;

    /// <summary>Diseño D6 (Fase 3 — Sakura Flow) — dictado global.</summary>
    private readonly WindowsFlowTextInserter _flowTextInserter;

    /// <summary>
    /// Handle de la ventana donde se escribirá lo dictado, recordado AL EMPEZAR. Si cambia el foco
    /// antes de terminar, <see cref="WindowsFlowTextInserter"/> se niega a escribir.
    /// </summary>
    private long _flowTargetWindowHandle;

    /// <summary>
    /// Señal que cierra el dictado en curso. El atajo llega como dos pulsaciones separadas, pero el
    /// ámbito de voz debe sostenerse durante toda la sección crítica; por eso una única operación
    /// asíncrona espera aquí en vez de guardar el ámbito en un campo entre dos manejadores.
    /// </summary>
    private TaskCompletionSource<bool>? _flowStopSignal;
    private readonly SakuraPillWindow _sakuraPillWindow = new();

    /// <summary>Diseño D72 — el halo que aparece cuando Sakura oye su nombre.</summary>
    private readonly VoiceHaloWindow _voiceHaloWindow = new();
    private readonly HomeView _homeView = new();
    private readonly AssistantView _assistantView = new();
    private readonly TasksView _tasksView;
    private readonly FocusView _focusView;
    private readonly RoutinesView _routinesView;
    private readonly AudioView _audioView;
    private readonly CaptureView _captureView = new();
    private readonly SystemView _systemView = new();
    private readonly SettingsView _settingsView = new();
    private readonly PeekWindow _peekWindow = new();
    private readonly CapsuleWindow _capsuleWindow = new();
    private readonly AnswerPillWindow _answerPillWindow = new();
    private readonly CommandPaletteWindow _commandPaletteWindow;

    /// <summary>
    /// Diseño D2 — Sakura Command Center. Se crea la primera vez que se abre (Ctrl + K o el botón
    /// del encabezado) para no alargar el arranque con una ventana que puede no usarse.
    /// </summary>
    private CommandCenterWindow? _commandCenterWindow;

    /// <summary>
    /// Diseño D4.4 — visor del historial de solicitudes ambientales. Igual que
    /// <see cref="_commandCenterWindow"/>, perezoso: se crea la primera vez que se pide desde el
    /// Command Center.
    /// </summary>
    private AmbientHistoryWindow? _ambientHistoryWindow;

    /// <summary>
    /// Diseño D3 — ver <see cref="DailyFlowEventHub"/>. Reenvía los eventos de dominio que ya
    /// existían (TasksChanged/FocusChanged/rutina ejecutada) a un solo punto de extensión, sin
    /// reemplazar los manejadores existentes.
    /// </summary>
    private readonly DailyFlowEventHub _dailyFlowHub = new();

    /// <summary>
    /// Diseño D3.1 — conecta el mini temporizador global del encabezado con FocusManager y la
    /// navegación, sin acumular esa lógica en MainWindow. Se construye en el constructor porque
    /// el mini temporizador es parte fija del shell (a diferencia del Command Center, que se crea
    /// de forma perezosa).
    /// </summary>
    private readonly FocusContinuityCoordinator _focusContinuity;

    /// <summary>
    /// Diseño D4 — conecta <see cref="AmbientRequestManager"/> con el Sakura Pill Host
    /// (<see cref="_sakuraPillWindow"/>), igual que <see cref="_focusContinuity"/> conecta
    /// <c>FocusManager</c> con el mini temporizador.
    /// </summary>
    private readonly SakuraPillCoordinator _ambientCoordinator;
    private readonly TrayIconController _trayIcon;
    private readonly Dictionary<string, FrameworkElement> _views;
    private readonly bool _startHidden;
    private readonly ManagedOllamaSupervisor? _managedOllamaSupervisor;

    /// <summary>
    /// Diseño D25 — claves de los proveedores de nube, cifradas con DPAPI y fuera de
    /// <c>settings.json</c>. Se lee al construir cada petición, no se guarda en un campo suelto:
    /// una clave copiada a una variable vive lo que viva el objeto, y esto vive lo que dura la app.
    /// </summary>
    private readonly IAiApiKeyStore _apiKeyStore = new DpapiAiApiKeyStore();
    private readonly WindowsDocumentDropService _documentDropService = new();
    private readonly WindowsUpdateService _updateService = new();
    private UpdateManifest? _offeredUpdate;

    /// <summary>Diseño D27 — el borde de la pantalla como forma de llamar a Sakura.</summary>
    private readonly WindowsEdgeRevealWatcher _edgeRevealWatcher = new();

    /// <summary>
    /// Diseño D35 — el mismo vigilante, configurado en el borde contrario. Se reutiliza tal cual en
    /// vez de escribir otro: la política de franja estrecha, esquinas excluidas y permanencia mínima
    /// es la que hace que un borde no estorbe, y vale igual para los mandos que para Sakura.
    /// </summary>
    private readonly WindowsEdgeRevealWatcher _quickControlsWatcher = new();

    /// <summary>
    /// Diseño D42 — velocidad de red y espacio de disco para las cápsulas de rendimiento. Guarda la
    /// lectura anterior para poder calcular una velocidad, así que tiene que ser el mismo objeto
    /// entre refrescos.
    /// </summary>
    private readonly WindowsThroughputSource _throughputSource = new();

    /// <summary>
    /// Diseño D43 — la sesión de medios de Windows para la pestaña Media. Se guarda entre refrescos
    /// porque cachea la portada de la pista actual y el gestor de WinRT, que cuesta pedir.
    /// </summary>
    private readonly IMediaSessionReader _mediaSessionReader = new WindowsMediaSessionReader();

    /// <summary>
    /// Diseño D44 — el cajón del panel y el vigilante del borde de arriba que lo baja. Se crean con
    /// la ventana y no al primer uso: la primera apertura tiene que caer ya medida, y construir la
    /// vista entera en ese instante metería un tirón justo en la animación que la presenta.
    /// </summary>
    private readonly DashboardWindow _dashboardWindow = new();

    private readonly WindowsTopRevealWatcher _topRevealWatcher = new();

    private readonly QuickControlsWindow _quickControlsWindow = new();
    private readonly DdcDisplayBrightnessService _brightnessService = new();

    private HwndSource? _windowSource;

    /// <summary>
    /// Diseño D62 — qué fondo compone el sistema detrás del shell. Es nulo hasta que la ventana
    /// tiene identificador, y hasta entonces se pinta el fondo propio: una ventana transparente
    /// esperando un acrílico que quizá no llegue se ve negra, y ese es el único fallo que no se
    /// puede consentir.
    /// </summary>
    private WindowBackdropDecision? _backdropDecision;
    private SystemSnapshot _latestSnapshot = SystemSnapshot.Empty;
    private ResourceGovernorDecision _resourceDecision = ResourceGovernorDecision.Normal;
    private bool _isHiding;

    // Diseño D58 — lo que se abre por roce tiene que saber retirarse solo.
    private readonly DispatcherTimer _ambientStallWatch = new()
    {
        Interval = TimeSpan.FromSeconds(5)
    };

    private readonly DispatcherTimer _unattendedWatch = new()
    {
        Interval = TimeSpan.FromMilliseconds(250)
    };

    private bool _openedByHover;
    private bool _escapeHotkeyHeld;
    private bool _touchedSinceReveal;
    private DateTimeOffset? _pointerOutsideSince;

    private bool _isClosed;
    private bool _allowExit;
    private bool _exitRequested;
    private bool _trayHintShown;
    private int _metricsRefreshInProgress;
    private string _currentDestination = "Home";
    private string _previousDestination = "Home";
    private bool _voicePromptActive;
    private bool _managedAiRuntimeFailureNotified;
    private bool _promptFromCommandPalette;

    /// <summary>Si la respuesta en curso va a la píldora de esquina en vez de a la ventana.</summary>
    private bool _answerInPill;

    /// <summary>Motivo por el que la respuesta en curso no llegó, para enseñarlo en la píldora.</summary>
    private string? _pillFailure;
    private bool _sideRailExpanded;
    private bool _visualContextPersistent;
    private bool _silentVisualContext;
    private bool _resourceGovernorWakeWordPaused;
    private bool _wakeWordTestActive;
    private CancellationTokenSource? _wakeWordTestCancellation;
    private WakeWordRecognitionObservedEventArgs? _lastWakeWordObservation;
    private string _runtimeAiStatus = "Desactivada";
    private bool _runtimeAiHealthy;
    private string? _visualContextMetadata;
    private string? _pendingVoicePrompt;
    private AiImageAttachment? _pendingVisionAttachment;
    private long _lastExternalWindowHandle;

    public MainWindow(
        bool startHidden = false,
        ManagedOllamaSupervisor? managedOllamaSupervisor = null,
        IAiChatService? aiChatService = null,
        IAudioMixerService? audioMixerService = null,
        IScreenCaptureService? screenCaptureService = null,
        VoiceCoordinator? voiceCoordinator = null,
        IHardwareCapabilityService? hardwareCapabilityService = null,
        IAdaptiveEngineRegistry? adaptiveEngineRegistry = null)
    {
        InitializeComponent();
        _commandPaletteWindow = new CommandPaletteWindow();

        // Todos los servicios son dependencias obligatorias provistas por la raíz de
        // composición (App.OnStartup): MainWindow nunca construye un motor. Se asignan aquí,
        // antes de cualquier campo dependiente y antes de cablear eventos. El coordinador de
        // voz es el único punto de acceso al subsistema de voz; MainWindow no recibe los tres
        // motores (Whisper, TTS, Vosk), que posee y libera SakuraCompositionRoot.
        _aiChatService = aiChatService ?? throw new ArgumentNullException(nameof(aiChatService));
        _audioMixerService = audioMixerService ?? throw new ArgumentNullException(nameof(audioMixerService));
        _screenCaptureService = screenCaptureService ?? throw new ArgumentNullException(nameof(screenCaptureService));
        _voiceCoordinator = voiceCoordinator ?? throw new ArgumentNullException(nameof(voiceCoordinator));
        _hardwareCapabilityService = hardwareCapabilityService ?? throw new ArgumentNullException(nameof(hardwareCapabilityService));
        _adaptiveEngineRegistry = adaptiveEngineRegistry ?? throw new ArgumentNullException(nameof(adaptiveEngineRegistry));

        _startHidden = startHidden;
        _managedOllamaSupervisor = managedOllamaSupervisor;
        _preferences = _settingsStore.Load();
        _preferences.StartWithWindows = _startupService.IsEnabled();
        _taskManager = new TaskManager(_taskStore);
        _taskManager.Load();
        _focusManager = new NexoFocusManager(_focusStore);
        _focusManager.Load();
        _routineManager = new RoutineManager(_routineStore);
        _routineManager.Load();
        _routineRunner = new RoutineRunner(new NexoAutomationActionExecutor(
            _audioMixerService,
            _focusManager,
            _taskManager));
        _ambientRequestManager = new AmbientRequestManager(_ambientRequestStore);
        _ambientRequestManager.Load();
        _flowTextInserter = new WindowsFlowTextInserter(_ambientContextProvider);
        _memoryManager.Load();

        // Diseño D11 — el coordinador necesita las preferencias ya cargadas: el segundo objetivo
        // que aplica de verdad es el modo de rendimiento de la propia Sakura.
        _optimizationCoordinator = new OptimizationCoordinator(
            _optimizationApplier,
            new PreferencesSakuraFootprintApplier(
                _preferences,
                () =>
                {
                    SavePreferences();
                    RefreshAdaptiveEnginePlan();
                }),
            _optimizationSnapshotStore,
            _auditLog);

        _workspaceEditCoordinator = new WorkspaceEditCoordinator(
            new FileSystemWorkspaceWriter(),
            _workspaceCheckpointStore,
            _auditLog);

        _skillPackCoordinator = new SkillPackCoordinator(
            new JsonSkillPackSnapshotStore(),
            _auditLog);

        _computerUseCoordinator = new ComputerUseCoordinator(
            new WindowsComputerUseExecutor(),
            _computerUseProbe,
            new JsonComputerUseSnapshotStore(),
            _auditLog,

            // Diseño D19 — el invocador comparte el lector de Lens, pero no la capacidad: leer un
            // control e invocarlo pasan por interfaces y permisos distintos.
            new WindowsUiAutomationInvoker(_lensUiAutomationReader));

        _updateSafety = new UpdateSafetyCoordinator(_backupService, _auditLog);
        _sequenceCoordinator = new SequenceCoordinator(_auditLog);

        _tasksView = new TasksView(_taskManager);
        _focusView = new FocusView(
            _focusManager,
            taskId => _taskManager.GetAll().FirstOrDefault(task => task.Id == taskId)?.Title);
        _routinesView = new RoutinesView(_routineManager);
        _audioView = new AudioView(_audioMixerService);

        _focusContinuity = new FocusContinuityCoordinator(
            _focusManager,
            FocusMiniTimerControl,
            taskId => _taskManager.GetAll().FirstOrDefault(task => task.Id == taskId)?.Title,
            () => NavigateTo(ShellNavigationPolicy.Focus, animate: _preferences.AnimationsEnabled));
        _focusContinuity.FinishRequested += (_, _) => FinishActiveFocusSession();

        _ambientCoordinator = new SakuraPillCoordinator(_ambientRequestManager, _sakuraPillWindow);
        _ambientCoordinator.QuickActionInvoked += AmbientCoordinator_QuickActionInvoked;

        _views = new Dictionary<string, FrameworkElement>(StringComparer.OrdinalIgnoreCase)
        {
            [ShellNavigationPolicy.Home] = _homeView,
            [ShellNavigationPolicy.Assistant] = _assistantView,
            [ShellNavigationPolicy.Tasks] = _tasksView,
            [ShellNavigationPolicy.Focus] = _focusView,
            [ShellNavigationPolicy.Routines] = _routinesView,
            [ShellNavigationPolicy.Audio] = _audioView,
            [ShellNavigationPolicy.Capture] = _captureView,
            [ShellNavigationPolicy.System] = _systemView,
            [ShellNavigationPolicy.Settings] = _settingsView
        };

        _trayIcon = new TrayIconController(
            () => Dispatcher.BeginInvoke(new Action(ShowFromBackground)),
            () => Dispatcher.BeginInvoke(new Action(async () => await ShowPeekAsync())),
            () => Dispatcher.BeginInvoke(new Action(RequestExit)));

        _assistantView.PromptSubmitted += AssistantView_PromptSubmitted;
        _assistantView.ConversationChanged += AssistantView_ConversationChanged;
        _assistantView.ConversationCleared += AssistantView_ConversationCleared;
        _assistantView.VoiceInputStarted += AssistantView_VoiceInputStarted;
        _assistantView.VoiceInputStopped += AssistantView_VoiceInputStopped;
        _assistantView.VisionCaptureRequested += AssistantView_VisionCaptureRequested;
        _assistantView.VisionAttachmentCleared += AssistantView_VisionAttachmentCleared;
        _assistantView.ImagePasted += AssistantView_ImagePasted;
        _assistantView.DocumentSaveRequested += AssistantView_DocumentSaveRequested;
        _tasksView.TasksChanged += TasksView_TasksChanged;
        _tasksView.FocusRequested += TasksView_FocusRequested;
        _focusView.FocusChanged += FocusView_FocusChanged;
        _focusView.CompleteAssociatedTaskRequested += FocusView_CompleteAssociatedTaskRequested;
        _routinesView.ExecuteRequested += RoutinesView_ExecuteRequested;
        // Los eventos de wake word se suscriben a través del coordinador (paso directo al
        // servicio subyacente): MainWindow ya no necesita una referencia al servicio.
        _voiceCoordinator.WakeWordDetected += WakeWordService_WakeWordDetected;

        // Diseño D72 — el nivel llega desde el hilo de captura de NAudio. Escribir un double desde
        // otro hilo no necesita marshalling; el fotograma siguiente lo recoge. Cualquier otra cosa
        // que se tocara aquí sí lo necesitaría.
        _voiceCoordinator.LevelObserved += (_, e) => _voiceHaloWindow.ReportLevel(e.Level);
        _voiceCoordinator.RecognitionObserved += WakeWordService_RecognitionObserved;
        _voiceCoordinator.WakeWordCustomAliases = _preferences.WakeWordAliases;
        _audioView.ActionCompleted += AudioView_ActionCompleted;
        _captureView.CaptureRequested += CaptureView_CaptureRequested;
        _commandPaletteWindow.PromptSubmitted += CommandPaletteWindow_PromptSubmitted;
        _commandPaletteWindow.WorkspaceRequested += CommandPaletteWindow_WorkspaceRequested;

        // La píldora contesta de reojo; si la respuesta merece leerse entera, esto es la puerta a
        // Sakura. Es una puerta y no un salto automático: abrirla sola volvería a arrastrar a la
        // persona fuera de donde estaba, que es justo lo que la píldora vino a evitar.
        _answerPillWindow.OpenInSakuraRequested += (_, _) =>
        {
            ShowAnimated();
            NavigateTo("Assistant", animate: true);
        };
        _homeView.CommandRequested += HomeView_CommandRequested;
        _homeView.TasksRequested += HomeView_TasksRequested;
        _homeView.FocusRequested += HomeView_FocusRequested;
        _homeView.RoutinesRequested += HomeView_RoutinesRequested;
        _homeView.ContextRequested += HomeView_ContextRequested;
        _homeView.NewTaskRequested += HomeView_NewTaskRequested;
        _homeView.StartFocusRequested += HomeView_StartFocusRequested;
        _homeView.PauseFocusRequested += (_, _) => { _focusManager.Pause(DateTimeOffset.Now); CheckFocusTimer(); };
        _homeView.ResumeFocusRequested += (_, _) => { _focusManager.Resume(DateTimeOffset.Now); CheckFocusTimer(); };
        _homeView.CommandCenterRequested += (_, _) => ShowCommandCenter();
        _systemView.RestartVoiceRequested += async (_, _) => await RestartWakeWordAsync();
        _systemView.DiagnosticsRequested += (_, _) => ShowDiagnostics();
        _systemView.HardwareCapabilityRefreshRequested += async (_, _) => await RefreshHardwareCapabilityAsync();

        // Diseño D11 (Fase 4) — los mismos siete escenarios que ya existían como comandos, ahora
        // también en Sistema. Comparten camino: la interfaz no aplica nada por su cuenta.
        _systemView.OptimizationScenarioRequested += async scenario =>
            await ProposeOptimizationAsync(scenario);
        _systemView.OptimizationUndoRequested += (_, _) => RestoreOptimization();
        _systemView.OptimizationAuditRequested += (_, _) => ShowOptimizationAudit();
        _systemView.AuditRefreshRequested += (_, _) => RefreshAuditPanel();
        _systemView.AuditRevertRequested += RevertAuditEntry;

        // Diseño D43 — los tres controles del reproductor. Tras dar la orden se vuelve a leer sin
        // esperar al siguiente refresco: entre pulsar pausa y que el icono cambie hay medio segundo
        // largo, y en ese hueco parece que el botón no hizo nada.
        _dashboardWindow.View.MediaPlayPauseRequested += async (_, _) =>
            await RunMediaCommandAsync(_mediaSessionReader.TogglePlayPauseAsync);
        _dashboardWindow.View.MediaNextRequested += async (_, _) =>
            await RunMediaCommandAsync(_mediaSessionReader.SkipNextAsync);
        _dashboardWindow.View.MediaPreviousRequested += async (_, _) =>
            await RunMediaCommandAsync(_mediaSessionReader.SkipPreviousAsync);

        // Diseño D44 — el centro del borde de arriba baja el cajón. El aviso llega desde un hilo de
        // fondo, así que hay que volver al de la interfaz antes de tocar la ventana.
        _topRevealWatcher.RevealRequested += HandleTopRevealRequested;
        _dashboardWindow.View.PanelImagePickRequested += (_, _) => PickPanelImage();
        _dashboardWindow.View.SetPanelImage(_preferences.PanelImagePath);

        _dashboardWindow.Dismissed += (_, _) => _topRevealWatcher.SuppressBriefly();

        _topRevealWatcher.Configure(_preferences.EdgeRevealEnabled);
        _assistantView.ConfigureHistory(
            _preferences.SaveConversationHistory,
            _preferences.RecentConversationMessageLimit);

        if (_preferences.SaveConversationHistory)
        {
            _assistantView.LoadConversation(_conversationStore.Load());
        }

        WireSettingsEvents();
        _settingsView.ApplyPreferences(_preferences);

        // Diseño D13 — la auditoría se enseña ya poblada. Un panel vacío al abrir haría pensar que
        // no hay registro, cuando lo que pasa es que nadie lo ha pedido todavía.
        RefreshAuditPanel();
        RefreshSkillPackPanel();
        _systemView.SetOptimizationStatus(detail: null, _optimizationCoordinator.HasSomethingToUndo);
        UpdateAiProviderStatus();
        ApplyPreferences();
        _voiceCoordinator.WakeWordSensitivity = _preferences.WakeWordSensitivity;
        _assistantView.SetVisionAvailability(_preferences.VisionEnabled);
        ConfigureVoiceInputDevices();
        NavigateTo(ShellNavigationPolicy.DefaultDestination, animate: false);
        SetSideRailExpanded(_preferences.SideRailExpanded, animate: false, persist: false);
        _edgeRevealWatcher.RevealRequested += HandleEdgeRevealRequested;
        _quickControlsWatcher.RevealRequested += HandleQuickControlsRequested;
        _quickControlsWindow.ControlChanged += QuickControlsWindow_ControlChanged;

        // Diseño D28 — con el acento siguiendo a Windows, cambiar de fondo de escritorio (con
        // "Color de acento automático" activo) debe verse en Sakura sin reabrirla. UserPreferenceChanged
        // es el evento que ya usa el propio Windows para avisar de esto — nadie más lo dispara.
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += OnSystemUserPreferenceChanged;

        UpdateResourceModeIndicator(ResourceGovernorDecision.Normal);
        RefreshRuntimeDashboard();
        _ = RefreshHardwareCapabilityAsync();

        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (_, _) => UpdateClock();

        _metricsTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _metricsTimer.Tick += async (_, _) => await RefreshMetricsAsync();

        _taskReminderTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(15)
        };
        _taskReminderTimer.Tick += (_, _) => CheckTaskReminders();

        _focusTickTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _focusTickTimer.Tick += (_, _) => CheckFocusTimer();

        _unattendedWatch.Tick += (_, _) => CheckUnattendedReveal();

        // Diseño D63 — el reloj cierra las solicitudes que se quedaron esperando una respuesta que
        // no va a llegar. Arreglar los filtros de excepción de Lens es lo que quita la causa
        // conocida; esto es lo que hace que la píldora no dependa de que ningún camino futuro se
        // acuerde de cerrarse. Corre siempre, también con el shell oculto, porque la píldora vive
        // fuera de la ventana principal.
        _ambientStallWatch.Tick += (_, _) =>
        {
            if (_ambientRequestManager.FailIfStalled(DateTimeOffset.Now).Success)
            {
                CheckAmbientRequest();
            }
        };
        _ambientStallWatch.Start();

        // Cualquier señal de que hay alguien delante cancela la retirada. Se escuchan en modo
        // Preview porque los controles hijos se quedan con los eventos normales: un clic dentro del
        // chat nunca llegaría a la ventana.
        PreviewMouseDown += (_, _) => _touchedSinceReveal = true;
        PreviewKeyDown += (_, _) => _touchedSinceReveal = true;
        PreviewTextInput += (_, _) => _touchedSinceReveal = true;

        _visualContextExpiryTimer.Interval = TimeSpan.FromMinutes(2);
        _visualContextExpiryTimer.Tick += (_, _) =>
        {
            _visualContextExpiryTimer.Stop();
            ClearPendingVisionAttachment();
        };
    }

    private void WireSettingsEvents()
    {
        _settingsView.PositionChanged += position =>
        {
            _preferences.Position = position;
            ApplyShellSide();
            _edgeRevealWatcher.Configure(_preferences.EdgeRevealEnabled, position);
            _quickControlsWatcher.Configure(
                _preferences.EdgeRevealEnabled,
                QuickControlsPolicy.ControlsEdgeFor(position));
            PositionWindow();
            _peekWindow.HideImmediately();
            SavePreferences();
        };

        _settingsView.WidthChanged += width =>
        {
            _preferences.Width = width;
            Width = width;
            PositionWindow();
            SavePreferences();
        };

        _settingsView.OpacityChanged += opacity =>
        {
            _preferences.Opacity = opacity;
            ApplyShellOpacity();
            SavePreferences();
        };

        _settingsView.AccentChanged += accent =>
        {
            _preferences.AccentColor = accent;
            ApplyAccent(accent);
            UpdateNavigationState(_currentDestination);
            SavePreferences();
        };

        _settingsView.AccentSourceChanged += source =>
        {
            _preferences.AccentSource = source;
            ApplyEffectiveAccent();
            UpdateNavigationState(_currentDestination);
            SavePreferences();
        };

        _settingsView.AnimationsChanged += enabled =>
        {
            _preferences.AnimationsEnabled = enabled;
            SakuraMotion.AnimationsEnabled = ShellAnimationsAllowed;
            SavePreferences();
        };

        _settingsView.EdgeRevealChanged += enabled =>
        {
            _preferences.EdgeRevealEnabled = enabled;
            _edgeRevealWatcher.Configure(enabled, _preferences.Position);
            _topRevealWatcher.Configure(enabled);
            _quickControlsWatcher.Configure(
                enabled,
                QuickControlsPolicy.ControlsEdgeFor(_preferences.Position));
            SavePreferences();
        };

        _settingsView.AutomaticUpdateCheckChanged += enabled =>
        {
            _preferences.AutomaticUpdateCheckEnabled = enabled;
            SavePreferences();
        };

        _settingsView.ModuleVisibilityChanged += (module, visible) =>
        {
            SetModuleVisibility(module, visible);
            SavePreferences();
        };

        _settingsView.PeekOptionChanged += (option, enabled) =>
        {
            ApplyPeekOption(option, enabled);
            SavePreferences();
        };

        _settingsView.ConversationHistoryChanged += enabled =>
        {
            _preferences.SaveConversationHistory = enabled;
            _assistantView.ConfigureHistory(
                enabled,
                _preferences.RecentConversationMessageLimit);

            if (enabled)
            {
                _conversationStore.Save(_assistantView.GetConversationSnapshot());
            }
            else
            {
                _conversationStore.Clear();
            }

            SavePreferences();
        };

        _settingsView.VoiceResponsesChanged += enabled =>
        {
            _preferences.SpeakVoiceResponses = enabled;
            if (!enabled)
            {
                _voiceCoordinator.StopSpeaking();
            }

            SavePreferences();
        };

        _settingsView.VoiceInputDeviceChanged += deviceNumber =>
        {
            _ = ChangeVoiceInputDeviceAsync(deviceNumber);
        };

        _settingsView.WakeWordEnabledChanged += enabled =>
        {
            _preferences.WakeWordEnabled = enabled;
            SavePreferences();
            _ = ApplyWakeWordPreferenceAsync(showCapsule: true);
            RefreshAdaptiveEnginePlan();
        };

        _settingsView.WakeWordPhraseChanged += phrase =>
        {
            _preferences.WakeWordPhrase = phrase;
            SavePreferences();
            if (_preferences.WakeWordEnabled)
            {
                _ = ApplyWakeWordPreferenceAsync(showCapsule: false);
            }
        };

        _settingsView.WakeWordSensitivityChanged += sensitivity =>
        {
            _preferences.WakeWordSensitivity = sensitivity;
            _voiceCoordinator.WakeWordSensitivity = sensitivity;
            SavePreferences();
            if (_preferences.WakeWordEnabled)
            {
                _ = ApplyWakeWordPreferenceAsync(showCapsule: false);
            }
        };

        _settingsView.WakeWordTestRequested += async (_, _) =>
            await StartWakeWordTestAsync();

        _settingsView.WakeWordAliasFromLastRequested += async (_, _) =>
            await AddLastWakeWordObservationAsAliasAsync();

        _settingsView.WakeWordAliasesClearRequested += async (_, _) =>
            await ClearWakeWordAliasesAsync();

        // Diseño D7 (Fase 3 — Sakura Flow)
        _settingsView.FlowEnabledChanged += enabled =>
        {
            _preferences.FlowEnabled = enabled;
            SavePreferences();
            ApplyFlowHotkeyRegistration();
        };

        _settingsView.FlowModeChanged += mode =>
        {
            _preferences.FlowMode = mode;
            SavePreferences();
        };

        _settingsView.FlowDictionaryChanged += lines =>
        {
            _preferences.FlowDictionary = [.. lines];
            SavePreferences();
        };

        _settingsView.FlowSnippetsChanged += lines =>
        {
            _preferences.FlowSnippets = [.. lines];
            SavePreferences();
        };

        // Diseño D10 (Fase 6 — Context and Memory)
        _settingsView.MemoryEnabledChanged += enabled =>
        {
            _preferences.Memory.Enabled = enabled;

            // Normalize() apaga también las categorías cuando el interruptor general se apaga. Se
            // llama aquí, y no solo al guardar, para que la política vea el estado correcto en la
            // siguiente frase aunque el guardado tarde.
            _preferences.Memory.Normalize();
            SavePreferences();
            _settingsView.ApplyMemorySettings(_preferences.Memory);
            _settingsView.SetMemoryStatus(enabled
                ? "Memoria activada. Elige qué categorías puede recordar."
                : "Memoria desactivada. Lo ya guardado sigue ahí hasta que lo borres.");
        };

        _settingsView.MemoryCategoryChanged += (category, enabled) =>
        {
            switch (category)
            {
                case MemoryCategory.Preferencias:
                    _preferences.Memory.RememberPreferences = enabled;
                    break;
                case MemoryCategory.Conversacion:
                    _preferences.Memory.RememberConversation = enabled;
                    break;
                case MemoryCategory.Habitos:
                    _preferences.Memory.RememberHabits = enabled;
                    break;
            }

            SavePreferences();
        };

        _settingsView.MemoryRetentionChanged += days =>
        {
            _preferences.Memory.RetentionDays = days;
            SavePreferences();

            // La retención se aplica al leer, así que basta con pedir la lista para que lo que ya
            // caducó desaparezca ahora mismo y no en la próxima escritura.
            var remaining = _memoryManager.GetAll(_preferences.Memory, DateTimeOffset.Now).Count;
            _settingsView.SetMemoryStatus(
                $"Retención de {days} días. Quedan {remaining} recuerdos guardados.");
        };

        _settingsView.MemoryExclusionsChanged += lines =>
        {
            _preferences.Memory.Exclusions = [.. lines];
            _preferences.Memory.Normalize();
            SavePreferences();
            _settingsView.SetMemoryStatus(
                _preferences.Memory.Exclusions.Count == 0
                    ? "Sin exclusiones."
                    : $"{_preferences.Memory.Exclusions.Count} exclusiones activas. " +
                      "No afectan a lo ya guardado: para eso, usa «olvidar todo».");
        };

        _settingsView.MemoryShowRequested += (_, _) =>
        {
            ShowMemoryContents();
            NavigateTo("Assistant", animate: true);
        };

        _settingsView.MemoryForgetAllRequested += (_, _) =>
        {
            var result = ForgetAllMemory();
            _settingsView.SetMemoryStatus(result.Message);
        };

        // Diseño D16 — cambiar un permiso es la decisión de la que cuelgan las demás, así que se
        // confirma al ampliar y se registra siempre.
        _settingsView.CapabilityPermissionChanged += (capability, level) =>
        {
            var permission = _preferences.Permissions.For(capability);
            var previous = permission.Level;

            if (previous == level)
            {
                return;
            }

            // Política de mínimo privilegio: ampliar exige una confirmación nueva; restringir, no.
            if (PermissionBroker.RequiresNewConfirmation(previous, level))
            {
                var confirmation = MessageBox.Show(
                    this,
                    $"Vas a ampliar el permiso de «{CapabilityText.Describe(capability)}» " +
                        $"de {previous} a {level}." + Environment.NewLine + Environment.NewLine +
                        "Aunque lo permitas, seguiré preguntándote antes de borrar algo sin " +
                        "recuperación, tocar credenciales, enviar algo fuera de tu equipo o pedir " +
                        "permisos de administrador." + Environment.NewLine + Environment.NewLine +
                        "¿Lo amplío?",
                    "Ampliar un permiso",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirmation != MessageBoxResult.Yes)
                {
                    _settingsView.ApplyPermissionSettings(_preferences.Permissions);
                    _settingsView.SetPermissionsStatus("No cambié ese permiso.");
                    return;
                }
            }

            permission.Level = level;
            SavePreferences();

            RecordAudit(
                AuditCapability.Permisos,
                $"Permiso cambiado ({capability})",
                $"{previous} → {level}",
                "Cambio explícito del usuario");

            _settingsView.SetPermissionsStatus(
                $"«{CapabilityTitleFor(capability)}» quedó en {level}.");
        };

        // Diseño D23 — los packs, ya activables desde Personalizar y no solo desde la paleta.
        _settingsView.SkillPackActivationRequested += id =>
        {
            ApplySkillPack(SkillPackCatalog.Get(id));
            RefreshSkillPackPanel();
        };

        _settingsView.SkillPackDeactivationRequested += (_, _) =>
        {
            RevertSkillPack();
            RefreshSkillPackPanel();
        };

        // Diseño D19 — las exclusiones por aplicación, ya editables sin abrir settings.json.
        _settingsView.PermissionExclusionsChanged += lines =>
        {
            PermissionExclusionParser.Apply(_preferences.Permissions, lines);
            _preferences.Permissions.Normalize();
            SavePreferences();

            var total = _preferences.Permissions.Capabilities.Sum(entry => entry.ExcludedApps.Count);
            RecordAudit(
                AuditCapability.Permisos,
                "Exclusiones por aplicación actualizadas",
                total == 0 ? "Sin exclusiones." : $"{total} exclusiones activas.",
                "Cambio explícito del usuario");

            _settingsView.ApplyPermissionSettings(_preferences.Permissions);
        };

        // Diseño D18 — subir hasta "ejecutar un paso" en el equipo se confirma aparte del permiso:
        // son dos decisiones distintas y la más arriesgada del roadmap merece las dos.
        _settingsView.ComputerUseAutonomyLevelChanged += level =>
        {
            var previous = _preferences.ComputerUseAutonomyLevel;
            if (previous == level)
            {
                return;
            }

            if (level > previous)
            {
                var confirmation = MessageBox.Show(
                    this,
                    $"Vas a subir lo que Sakura puede hacer en tu equipo de {previous} a {level}." +
                        Environment.NewLine + Environment.NewLine +
                        (level == AutonomyLevel.EjecutarUnPaso
                            ? "Podrá ejecutar UNA acción cada vez, y te la confirmaré antes. Sigo " +
                              "eligiendo siempre la forma más segura disponible, y sigo sin usar " +
                              "ratón y teclado simulados."
                            : "Seguirá sin ejecutar nada.") +
                        Environment.NewLine + Environment.NewLine + "¿Lo subo?",
                    "Subir el nivel en el equipo",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (confirmation != MessageBoxResult.Yes)
                {
                    _settingsView.ApplyComputerUseAutonomyLevel(previous);
                    return;
                }
            }

            _preferences.ComputerUseAutonomyLevel = level;
            SavePreferences();

            RecordAudit(
                AuditCapability.Permisos,
                "Nivel en el equipo cambiado",
                $"{previous} → {level}",
                "Elección explícita del usuario",
                (int)level);
        };

        // Diseño D13 (Fase 5 — Project Companion). El refresco del panel vive dentro de
        // AuthorizeWorkspaceFolder/RevokeWorkspace, no aquí: autorizar también se puede desde la
        // paleta de comandos, y cuando el refresco colgaba de estos dos manejadores esa ruta dejaba
        // la tarjeta diciendo "No hay ninguna carpeta autorizada" con los niveles deshabilitados,
        // aunque la carpeta sí estaba autorizada — y sin niveles no había forma de subir a
        // «Ejecutar un paso», que es justo lo que la propia tarjeta te manda a hacer.
        _settingsView.WorkspaceAuthorizeRequested += (_, _) => AuthorizeWorkspaceFolder();

        _settingsView.WorkspaceRevokeRequested += (_, _) => RevokeWorkspace();

        _settingsView.WorkspaceAutonomyLevelChanged += level =>
        {
            var previousLevel = _preferences.Workspace.AutonomyLevel;
            if (previousLevel == level)
            {
                return;
            }

            // Misma política de mínimo privilegio que el nivel del equipo y que los permisos por
            // capacidad (D16): ampliar exige confirmación nueva, restringir no. Faltaba justo aquí,
            // y es donde más pesa: subir a «Ejecutar un paso» o «Colaborar» es lo que deja a Sakura
            // escribir en archivos del usuario, mientras que ampliar una capacidad cualquiera ya se
            // confirmaba desde D16.
            if (level > previousLevel)
            {
                var confirmation = MessageBox.Show(
                    this,
                    $"Vas a subir lo que Sakura puede hacer en tu proyecto de {previousLevel} a {level}." +
                        Environment.NewLine + Environment.NewLine +
                        (level >= AutonomyLevel.EjecutarUnPaso
                            ? "Podrá MODIFICAR archivos de esa carpeta. Te confirmaré cada cambio " +
                              "antes de aplicarlo, y guardaré una copia previa para poder deshacerlo."
                            : "Seguirá sin modificar ningún archivo.") +
                        Environment.NewLine + Environment.NewLine + "¿Lo subo?",
                    "Subir el nivel en el proyecto",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (confirmation != MessageBoxResult.Yes)
                {
                    _settingsView.ApplyWorkspaceSettings(_preferences.Workspace);
                    return;
                }
            }

            _preferences.Workspace.AutonomyLevel = level;
            _preferences.Workspace.Normalize();
            SavePreferences();

            // Cambiar hasta dónde puede llegar Sakura es una decisión de permisos, así que se
            // registra igual que autorizar la carpeta.
            RecordAudit(
                AuditCapability.Permisos,
                "Nivel de autonomía del proyecto cambiado",
                WorkspaceAutonomyPolicy.Describe(_preferences.Workspace.AutonomyLevel),
                "Elección explícita del usuario",
                (int)_preferences.Workspace.AutonomyLevel);
        };

        _settingsView.AiProviderChanged += provider =>
        {
            // Diseño D58 — antes de irse, se apunta el modelo que estaba en uso para el proveedor
            // que se abandona; al llegar, se recupera el suyo. Sin esto, cambiar de proveedor y
            // volver devolvía el modelo de fábrica en vez del que la persona había elegido.
            RememberModelForCurrentProvider();

            var preset = AiProviderDefaults.Get(provider);
            _preferences.AiProvider = provider;
            _preferences.AiBaseUrl = preset.BaseUrl;
            _preferences.AiModel = RecallModelFor(provider, preset.DefaultModel);
            _preferences.AiApiKeyEnvironmentVariable = preset.ApiKeyEnvironmentVariable;
            UpdateAiProviderStatus();
            SavePreferences();
            ConfigureManagedOllamaSupervisor();
            RefreshAdaptiveEnginePlan();
        };

        _settingsView.AiBaseUrlChanged += baseUrl =>
        {
            _preferences.AiBaseUrl = AiProviderDefaults.NormalizeBaseUrl(baseUrl);
            SavePreferences();
            ConfigureManagedOllamaSupervisor();
        };

        // Diseño D25 — la clave nunca pasa por ShellPreferences ni por settings.json: va derecha al
        // almacén cifrado y se lee otra vez solo al construir cada petición.
        _settingsView.ApiKeyChanged += (provider, apiKey) =>
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _apiKeyStore.Remove(provider);
            }
            else
            {
                _apiKeyStore.Write(provider, apiKey);
            }

            // No SetStoredApiKeyPresence aquí: esa versión vacía la caja, y esto se dispara en
            // cada pulsación mientras la persona sigue escribiendo o acaba de pegar. Solo se
            // refresca el texto de ayuda; la caja se limpia únicamente al cambiar de proveedor.
            _settingsView.UpdateApiKeyStatusText(
                !string.IsNullOrWhiteSpace(_apiKeyStore.Read(provider)));
        };

        _settingsView.ApiKeyRequested += provider =>
            _settingsView.SetStoredApiKeyPresence(
                !string.IsNullOrWhiteSpace(_apiKeyStore.Read(provider)));

        _settingsView.ApiKeyPageRequested += OpenExternalPage;

        _settingsView.UpdateCheckRequested += () => _ = CheckForUpdateAsync();
        _settingsView.UpdateInstallRequested += () => _ = InstallOfferedUpdateAsync();
        _settingsView.UpdateSkipRequested += SkipOfferedUpdate;
        _settingsView.SetCurrentVersion(CurrentVersionText);

        _settingsView.AiModelChanged += model =>
        {
            _preferences.AiModel = model.Trim();
            RememberModelForCurrentProvider();
            UpdateAiProviderStatus();
            SavePreferences();
        };

        _settingsView.AiApiKeyEnvironmentVariableChanged += variableName =>
        {
            _preferences.AiApiKeyEnvironmentVariable = variableName.Trim();
            SavePreferences();
        };

        _settingsView.ShareSystemMetricsWithAiChanged += enabled =>
        {
            _preferences.ShareSystemMetricsWithAi = enabled;
            SavePreferences();
        };

        _settingsView.VisionEnabledChanged += enabled =>
        {
            _preferences.VisionEnabled = enabled;
            _assistantView.SetVisionAvailability(enabled);
            if (!enabled)
            {
                ClearPendingVisionAttachment();
            }
            SavePreferences();
            RefreshRuntimeDashboard();
        };

        _settingsView.ResourceGovernorEnabledChanged += enabled =>
        {
            _preferences.ResourceGovernorEnabled = enabled;
            SavePreferences();
            _ = RefreshMetricsAsync();
        };

        _settingsView.PauseWakeWordInGameModeChanged += enabled =>
        {
            _preferences.PauseWakeWordInGameMode = enabled;
            SavePreferences();
            _ = ApplyResourceGovernorDecisionAsync(_resourceDecision);
        };

        _settingsView.ProtectVisionWhenBusyChanged += enabled =>
        {
            _preferences.ProtectVisionWhenBusy = enabled;
            SavePreferences();
        };

        _settingsView.HardwarePerformanceModeChanged += mode =>
        {
            _preferences.HardwarePerformanceMode = mode;
            SavePreferences();
            RefreshAdaptiveEnginePlan();
        };

        _settingsView.StartWithWindowsChanged += enabled =>
        {
            var result = _startupService.SetEnabled(enabled);
            if (result.Success)
            {
                _preferences.StartWithWindows = enabled;
                SavePreferences();
            }
            else
            {
                _settingsView.SetStartWithWindows(_preferences.StartWithWindows);
            }

            _settingsView.SetWindowsIntegrationStatus(result.Message, result.Success);
        };

        _settingsView.MinimizeToTrayChanged += enabled =>
        {
            _preferences.MinimizeToTray = enabled;
            SavePreferences();
            _settingsView.SetWindowsIntegrationStatus(
                enabled
                    ? "Cerrar Sakura lo ocultará en la bandeja."
                    : "Cerrar Sakura terminará completamente la aplicación.",
                isSuccess: null);
        };

        _settingsView.WindowsNotificationsChanged += enabled =>
        {
            _preferences.ShowWindowsNotifications = enabled;
            SavePreferences();
        };

        _settingsView.NotificationSoundsChanged += enabled =>
        {
            _preferences.PlayNotificationSounds = enabled;
            SavePreferences();
        };

        _settingsView.AiTestConnectionRequested += async (_, _) =>
            await TestAiConnectionAsync();

        _settingsView.ManageModelsRequested += (_, _) =>
            ShowModelManager();

        _settingsView.DiagnosticsRequested += (_, _) =>
            ShowDiagnostics();

        _settingsView.OnboardingRequested += async (_, _) =>
            await ShowOnboardingAsync();

        // Diseño D2 — restaurar apariencia. Solo se reaplican las preferencias visuales; las
        // funcionales (tareas, rutinas, voz, IA, motores, integración con Windows) ni se leen ni
        // se escriben aquí, que es justo lo que hace segura esta acción.
        _settingsView.ResetAppearanceRequested += (_, _) =>
        {
            _preferences.ResetVisualPreferences();

            Width = _preferences.Width;
            PositionWindow();
            _peekWindow.HideImmediately();
            ApplyShellOpacity();
            ApplyEffectiveAccent();
            SetSideRailExpanded(_preferences.SideRailExpanded, animate: false, persist: false);
            UpdateNavigationState(_currentDestination);

            // Refresca los controles de Personalizar para que reflejen los valores restaurados.
            _settingsView.ApplyPreferences(_preferences);

            SavePreferences();
            _assistantView.AddSakuraMessage(
                "Apariencia restaurada. Tus tareas, rutinas y la configuración de voz, IA y motores no se tocaron.");
        };
    }

    /// <summary>
    /// Abre en el navegador la página donde se consigue la clave del proveedor. La dirección viene
    /// siempre de <see cref="AiProviderDefaults"/> —una constante del programa—, nunca de algo que
    /// alguien haya escrito o que haya llegado en una respuesta: <c>UseShellExecute</c> con una
    /// cadena arbitraria lanzaría cualquier cosa que Windows sepa abrir, no solo páginas.
    /// </summary>
    private void OpenExternalPage(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception exception) when (
            exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _capsuleWindow.ShowMessage(
                CapsuleKind.Warning,
                "No pude abrir el navegador",
                uri.AbsoluteUri,
                _preferences.Position);
        }
    }

    private void ShowModelManager()
    {
        // El administrador de modelos habla con el motor que esté configurado ahora. Si el proveedor
        // activo no es de los que usan Ollama, se cae al administrado por Sakura —el que la propia
        // aplicación puede instalar—, no al externo, que puede no existir en este equipo.
        var isOllamaProvider = AiProviderDefaults.UsesOllamaProtocol(_preferences.AiProvider);
        var providerKind = isOllamaProvider
            ? _preferences.AiProvider
            : AiProviderKind.SakuraLocal;
        var baseUrl = isOllamaProvider
            ? _preferences.AiBaseUrl
            : AiProviderDefaults.Get(AiProviderKind.SakuraLocal).BaseUrl;

        var window = new ModelManagerWindow(baseUrl, _preferences.AiModel)
        {
            Owner = this
        };

        if (window.ShowDialog() != true ||
            string.IsNullOrWhiteSpace(window.SelectedModel))
        {
            return;
        }

        _preferences.AiProvider = providerKind;
        _preferences.AiBaseUrl = baseUrl;
        _preferences.AiModel = window.SelectedModel;
        _preferences.AiApiKeyEnvironmentVariable = string.Empty;
        _settingsView.ApplyPreferences(_preferences);
        UpdateAiProviderStatus();
        SavePreferences();
        _capsuleWindow.ShowMessage(
            CapsuleKind.Success,
            "Modelo seleccionado",
            window.SelectedModel,
            _preferences.Position);
    }

    private void ShowDiagnostics()
    {
        var window = new DiagnosticsWindow(
            _preferences,
            _voiceCoordinator.GetInputDevices(),
            _voiceCoordinator.IsVoiceInputReady,
            _voiceCoordinator.IsWakeWordReady,
            _voiceCoordinator.IsWakeWordListening,
            trayActive: true,
            _startupService.IsEnabled(),
            _hardwareCapabilityService,
            _adaptiveEngineRegistry)
        {
            Owner = this
        };
        window.ShowDialog();
    }

    private async Task ShowOnboardingAsync()
    {
        await PauseWakeWordAsync();
        _voiceCoordinator.StopSpeaking();

        var window = new OnboardingWindow(_preferences, _settingsStore)
        {
            Owner = this
        };
        window.ShowDialog();

        _preferences.StartWithWindows = _startupService.IsEnabled();
        _settingsView.ApplyPreferences(_preferences);
        ApplyPreferences();
        UpdateAiProviderStatus();
        ConfigureManagedOllamaSupervisor();
        _assistantView.SetVisionAvailability(_preferences.VisionEnabled);
        ConfigureVoiceInputDevices();
        await ApplyWakeWordPreferenceAsync(showCapsule: false);
    }

    private void SideRailToggleButton_Click(object sender, RoutedEventArgs e)
    {
        SetSideRailExpanded(!_sideRailExpanded, animate: true, persist: true);
    }

    /// <summary>
    /// Diseño D27 — el vigilante avisa desde un hilo de fondo, así que hay que volver al hilo de la
    /// interfaz antes de tocar la ventana. Las condiciones se comprueban aquí y no en el vigilante
    /// porque son estado del shell, no geometría del ratón: el vigilante sabe dónde está el cursor
    /// y nada más.
    /// </summary>
    private void HandleEdgeRevealRequested()
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (IsVisible || _isHiding || !_preferences.EdgeRevealEnabled)
            {
                return;
            }

            // Con algo a pantalla completa —un juego, un vídeo, una presentación— el shell no se
            // asoma solo. Es la misma condición que ya silencia el resto de avisos pasajeros, y
            // aparecer encima de una partida por rozar el borde sería el peor momento posible.
            if (_resourceDecision.SuppressTransientOverlays)
            {
                return;
            }

            RememberForegroundWindow();

            // Diseño D58 — esta apertura no la pidió nadie: basta con rozar el borde. Se marca como
            // tal para que sepa retirarse si resulta que fue un accidente.
            _openedByHover = true;
            ShowAnimated();
        });
    }

    /// <summary>
    /// Diseño D58 — retira el shell si se abrió por roce y nadie ha aparecido.
    ///
    /// Se sondea la posición del cursor en vez de escuchar <c>MouseLeave</c> por la misma razón que
    /// en el cajón: la ventana está llena de controles hijos y salir de uno para entrar en otro
    /// genera un MouseLeave por el camino, así que el evento miente.
    /// </summary>
    /// <summary>
    /// Toma Escape mientras el shell está delante. Si el registro falla —otra aplicación lo tiene—
    /// no se avisa de nada: Escape desde dentro sigue funcionando y el botón de cerrar también, así
    /// que no hay nada roto que contar.
    /// </summary>
    private void AcquireEscapeHotkey()
    {
        if (_escapeHotkeyHeld || _isClosed)
        {
            return;
        }

        var windowHandle = new WindowInteropHelper(this).Handle;
        if (windowHandle == IntPtr.Zero)
        {
            return;
        }

        _escapeHotkeyHeld = RegisterHotKey(
            windowHandle, EscapeHotkeyId, ModNone, VirtualKeyEscape);
    }

    private void ReleaseEscapeHotkey()
    {
        if (!_escapeHotkeyHeld)
        {
            return;
        }

        _escapeHotkeyHeld = false;

        var windowHandle = new WindowInteropHelper(this).Handle;
        if (windowHandle != IntPtr.Zero)
        {
            UnregisterHotKey(windowHandle, EscapeHotkeyId);
        }
    }

    private void CheckUnattendedReveal()
    {
        if (!_openedByHover || _isHiding || _isClosed || !IsVisible)
        {
            _unattendedWatch.Stop();
            return;
        }

        _pointerOutsideSince = IsPointerOverShell()
            ? null
            : _pointerOutsideSince ?? DateTimeOffset.UtcNow;

        if (UnattendedRevealPolicy.ShouldRetract(
                _openedByHover,
                _pointerOutsideSince,
                DateTimeOffset.UtcNow,
                hasUserAttention: _touchedSinceReveal))
        {
            HideAnimated();
        }
    }

    private bool IsPointerOverShell()
    {
        if (!GetCursorPos(out var cursor) || ActualWidth <= 0 || ActualHeight <= 0)
        {
            // Sin saber dónde está el puntero se prefiere dejarlo abierto: retirar algo por si acaso
            // hace desaparecer lo que alguien puede estar mirando.
            return true;
        }

        try
        {
            var topLeft = PointToScreen(new Point(0, 0));
            var bottomRight = PointToScreen(new Point(ActualWidth, ActualHeight));

            // Un margen de holgura para que el propio borde sensible cuente como «dentro»: si no, el
            // píxel entre la franja y la ventana empezaría la cuenta atrás con el ratón pegado a ella.
            const double slack = 12;

            return cursor.X >= topLeft.X - slack &&
                   cursor.X <= bottomRight.X + slack &&
                   cursor.Y >= topLeft.Y - slack &&
                   cursor.Y <= bottomRight.Y + slack;
        }
        catch (InvalidOperationException)
        {
            // La ventana puede perder su origen entre medias; se trata como «no sabemos».
            return true;
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out ShellCursorPoint point);

    [StructLayout(LayoutKind.Sequential)]
    private struct ShellCursorPoint
    {
        public int X;
        public int Y;
    }

    /// <summary>
    /// Diseño D35 — el borde contrario a Sakura enseña volumen y brillo. Se lee el estado real justo
    /// antes de abrir y no se cachea: el volumen lo cambia cualquier cosa —la rueda del teclado, otro
    /// programa, el propio Windows— y un panel que apareciera con el valor de hace un rato mostraría
    /// una barra que no coincide con lo que se oye.
    /// </summary>
    private void HandleQuickControlsRequested()
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (_quickControlsWindow.IsVisible || !_preferences.EdgeRevealEnabled)
            {
                return;
            }

            // Misma condición que para el shell: con algo a pantalla completa, nada se asoma solo.
            if (_resourceDecision.SuppressTransientOverlays)
            {
                return;
            }

            var audio = _audioMixerService.ReadSnapshot();
            var brightness = _brightnessService.ReadSnapshot();

            _quickControlsWindow.ShowControls(
                _preferences.Position,
                audio.MasterVolumePercent,
                audio.IsAvailable,
                brightness.Percent,
                brightness.IsAvailable);
        });
    }

    private void QuickControlsWindow_ControlChanged(
        object? sender,
        QuickControlChangedEventArgs e)
    {
        switch (e.Kind)
        {
            case QuickControlKind.Volume:
                _audioMixerService.SetMasterVolume(e.Percent);
                break;

            case QuickControlKind.Brightness:
                _brightnessService.TrySetBrightness(e.Percent);
                break;
        }
    }

    /// <summary>
    /// Diseño D1 (Sakura Shell): las animaciones del shell respetan tanto la preferencia propia
    /// de Sakura como la preferencia de Windows (Configuración de accesibilidad → Efectos
    /// visuales). Si cualquiera de las dos está desactivada, los cambios de estado se aplican
    /// de inmediato, sin transición.
    /// </summary>
    private bool ShellAnimationsAllowed => _preferences.AnimationsEnabled && SystemParameters.ClientAreaAnimation;

    private void SetSideRailExpanded(bool expanded, bool animate, bool persist = true)
    {
        _sideRailExpanded = expanded;
        if (persist)
        {
            _preferences.SideRailExpanded = expanded;
            SavePreferences();
        }
        SideRailToggleButton.ToolTip = expanded
            ? "Contraer navegación"
            : "Expandir navegación";
        ApplySideRailButtonLayout(expanded);

        var targetWidth = expanded
            ? (double)FindResource("SidebarWidthExpanded")
            : (double)FindResource("SidebarWidthCollapsed");

        if (!animate || !ShellAnimationsAllowed)
        {
            SideRailBorder.BeginAnimation(FrameworkElement.WidthProperty, null);
            SideRailBorder.Width = targetWidth;
            ResetNavigationLabelEntrance();
            UpdateNavigationState(_currentDestination);
            return;
        }

        var currentWidth = SideRailBorder.ActualWidth > 0
            ? SideRailBorder.ActualWidth
            : SideRailBorder.Width;

        // Diseño D36 — la curva de resorte y no la de siempre: al abrir, el rail pasa un punto de
        // su ancho final y vuelve, que es lo que da la sensación de burbuja del boceto en vez de la
        // de un panel que se estira. Al cerrar se usa la curva que acelera, porque lo que se retira
        // no pide atención y rebotar al plegarse se lee como un error.
        var animation = new DoubleAnimation(
            currentWidth,
            targetWidth,
            expanded ? SakuraMotion.Emphasized : SakuraMotion.Exit)
        {
            EasingFunction = expanded
                ? SakuraMotion.SubtleSpringCurve
                : SakuraMotion.AccelerateCurve
        };
        animation.Completed += (_, _) =>
        {
            SideRailBorder.BeginAnimation(FrameworkElement.WidthProperty, null);
            SideRailBorder.Width = targetWidth;
        };
        SideRailBorder.BeginAnimation(FrameworkElement.WidthProperty, animation);

        AnimateNavigationLabels(expanded);
        UpdateNavigationState(_currentDestination);
    }

    /// <summary>
    /// Los nombres entran escalonados detrás del ancho, uno tras otro de arriba abajo. Que aparezcan
    /// todos a la vez en el instante en que hay sitio delata que solo se estaba cambiando un número;
    /// escalonados, el rail parece llenarse. Al plegar no hay escalonado: los nombres ya no caben y
    /// esperarlos solo retrasaría el gesto.
    /// </summary>
    private void AnimateNavigationLabels(bool expanded)
    {
        var labels = NavigationLabels();

        if (!expanded)
        {
            foreach (var label in labels)
            {
                label.BeginAnimation(OpacityProperty, null);
                label.Opacity = 1;
            }

            return;
        }

        for (var index = 0; index < labels.Count; index++)
        {
            var label = labels[index];
            label.BeginAnimation(OpacityProperty, null);
            label.Opacity = 0;
            label.Animate(
                OpacityProperty,
                1,
                SakuraMotion.Reveal,
                SakuraMotion.DecelerateCurve,
                SakuraMotion.StaggerAt(index));
        }
    }

    private void ResetNavigationLabelEntrance()
    {
        foreach (var label in NavigationLabels())
        {
            label.BeginAnimation(OpacityProperty, null);
            label.Opacity = 1;
        }
    }

    private IReadOnlyList<TextBlock> NavigationLabels() =>
    [
        HomeNavLabel, AssistantNavLabel, TasksNavLabel, FocusNavLabel, RoutinesNavLabel,
        AudioNavLabel, CaptureNavLabel, SystemNavLabel, SettingsNavLabel
    ];

    private void ApplySideRailButtonLayout(bool expanded)
    {
        var buttonWidth = expanded
            ? (double)FindResource("SidebarButtonWidthExpanded")
            : (double)FindResource("SidebarButtonWidthCollapsed");
        SideRailContentGrid.Width = buttonWidth;
        SideRailToggleButton.Width = buttonWidth;
        SettingsNavButton.Width = buttonWidth;
        SideRailBrandText.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;

        foreach (var label in new[]
                 {
                     HomeNavLabel,
                     AssistantNavLabel,
                     TasksNavLabel,
                     FocusNavLabel,
                     RoutinesNavLabel,
                     AudioNavLabel,
                     CaptureNavLabel,
                     SystemNavLabel,
                     SettingsNavLabel
                 })
        {
            label.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        }

        foreach (var button in new[]
                 {
                     HomeNavButton,
                     AssistantNavButton,
                     TasksNavButton,
                     FocusNavButton,
                     RoutinesNavButton,
                     AudioNavButton,
                     CaptureNavButton,
                     SystemNavButton
                 })
        {
            button.Width = buttonWidth;
        }
    }

    private void CommandPaletteButton_Click(object sender, RoutedEventArgs e)
    {
        ShowCommandPalette();
    }

    private void ShowCommandPalette()
    {
        if (_isClosed)
        {
            return;
        }

        RememberForegroundWindow();
        _commandPaletteWindow.ShowPalette(_preferences.AnimationsEnabled);
    }

    private void CommandCenterButton_Click(object sender, RoutedEventArgs e)
    {
        ShowCommandCenter();
    }

    /// <summary>
    /// Diseño D2 — abre el Sakura Command Center (Ctrl + K). Se crea de forma perezosa la primera
    /// vez: construir la ventana y el registro en el arranque solo retrasaría el inicio de Sakura
    /// para una función que puede no usarse en toda la sesión.
    /// </summary>
    private void ShowCommandCenter()
    {
        if (_isClosed)
        {
            return;
        }

        if (_commandCenterWindow is null)
        {
            _commandCenterWindow = new CommandCenterWindow(BuildCommandRegistry());
            _commandCenterWindow.CommandFailed += CommandCenterWindow_CommandFailed;
        }
        else
        {
            // Diseño D3: algunos comandos son dinámicos (uno por rutina habilitada) — se
            // reconstruye el registro en cada apertura para que nunca muestre una rutina ya
            // renombrada, deshabilitada o eliminada desde la última vez.
            _commandCenterWindow.UpdateCommands(BuildCommandRegistry());
        }

        _commandCenterWindow.ShowFor(this, Keyboard.FocusedElement);
    }

    private void CommandCenterWindow_CommandFailed(object? sender, CommandCenterFailureEventArgs e)
    {
        // Un comando fallido no cierra Sakura ni se informa como éxito: se avisa en la
        // conversación (no modal) y el detalle técnico va al diagnóstico.
        _assistantView.AddSakuraMessage(e.Result.Message ?? "No se pudo completar la acción.");

        if (e.Result.Error is { } error)
        {
            WriteCommandCenterLog(e.Command.Id, error);
        }
    }

    /// <summary>
    /// Registra el fallo de un comando conservando tipo, mensaje, excepción interna y stack trace,
    /// como exige el encargo. Sigue el mismo patrón que el resto de registros de Sakura: escribir
    /// un log nunca puede afectar al funcionamiento de la aplicación.
    /// </summary>
    private static void WriteCommandCenterLog(string commandId, Exception error)
    {
        try
        {
            Directory.CreateDirectory(Nexo.Core.Diagnostics.NexoDataPaths.LogsDirectory);
            File.AppendAllText(
                Nexo.Core.Diagnostics.NexoDataPaths.CommandCenterLog,
                $"{DateTimeOffset.Now:O} | comando '{commandId}' | " +
                $"{error.GetType().FullName}: {error.Message}{Environment.NewLine}" +
                $"{error}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (IOException)
        {
            // El registro no debe afectar el funcionamiento de Sakura.
        }
        catch (UnauthorizedAccessException)
        {
            // El registro no debe afectar el funcionamiento de Sakura.
        }
    }

    /// <summary>
    /// Construye el registro de comandos enlazando cada uno a un servicio real ya existente.
    /// No se inventa ninguna acción: solo se exponen cosas que el shell ya sabe hacer.
    /// </summary>
    private SakuraCommandRegistry BuildCommandRegistry()
    {
        var registry = new SakuraCommandRegistry();

        registry.RegisterRange(BuildNavigationCommands());

        // Diseño D72 — poder ver el halo sin hablar.
        //
        // La primera vez que se publicó, Adler no lo vio y no había forma de saber si el fallo
        // estaba en el halo o en el camino de la voz. Una orden que lo enciende cinco segundos
        // separa las dos preguntas, y de paso deja probarlo sin micrófono.
        registry.Register(new SakuraCommandDescriptor(
            "voice.halo.preview",
            "Probar el halo de voz",
            "Enciende el halo cinco segundos, sin hablar, para ver cómo se ve.",
            SakuraCommandCategory.Shell,
            async _ =>
            {
                ShowVoiceHalo();

                // Sin micrófono el nivel no se mueve, así que se simula una frase: sube, se sostiene
                // con altibajos y baja. Es lo que hace visible que reacciona en vez de parpadear.
                var aleatorio = new Random();
                for (var i = 0; i < 100; i++)
                {
                    var fase = i / 100.0;
                    var envolvente = fase < 0.15 ? fase / 0.15 : fase > 0.8 ? (1 - fase) / 0.2 : 1;
                    _voiceHaloWindow.ReportLevel(
                        Math.Clamp(envolvente * (0.45 + (aleatorio.NextDouble() * 0.5)), 0, 1));
                    await Task.Delay(50);
                }

                _voiceHaloWindow.HideHalo();
                return CommandExecutionResult.Success();
            }));

        registry.Register(new SakuraCommandDescriptor(
            "shell.sidebar.toggle",
            "Alternar barra lateral",
            "Expande o contrae la navegación lateral.",
            SakuraCommandCategory.Shell,
            _ =>
            {
                SetSideRailExpanded(!_sideRailExpanded, animate: _preferences.AnimationsEnabled);
                return Task.FromResult(CommandExecutionResult.Success());
            },
            keywords: ["barra", "lateral", "sidebar", "contraer", "expandir", "navegación"]));

        registry.Register(new SakuraCommandDescriptor(
            "focus.open",
            "Abrir Enfoque",
            "Va a la sección de Enfoque.",
            SakuraCommandCategory.Focus,
            _ =>
            {
                NavigateTo(ShellNavigationPolicy.Focus, animate: _preferences.AnimationsEnabled);
                return Task.FromResult(CommandExecutionResult.Success());
            },
            keywords: ["enfoque", "concentración", "pomodoro", "sesión", "abrir"]));

        registry.RegisterRange(BuildFocusStartCommands());

        registry.Register(new SakuraCommandDescriptor(
            "focus.cancel",
            "Cancelar sesión de enfoque",
            "Descarta la sesión en curso sin registrarla en el historial.",
            SakuraCommandCategory.Focus,
            _ =>
            {
                var result = _focusManager.Cancel();
                CheckFocusTimer();
                return Task.FromResult(result.Success
                    ? CommandExecutionResult.Success(result.Message)
                    : CommandExecutionResult.Failure(result.Message));
            },
            keywords: ["enfoque", "cancelar", "descartar", "detener"],
            availability: () => _focusManager.GetSnapshot(DateTimeOffset.Now).ActiveTimer is not null
                ? SakuraCommandAvailability.Available
                : SakuraCommandAvailability.Unavailable("No hay ninguna sesión de enfoque activa.")));

        registry.Register(new SakuraCommandDescriptor(
            "focus.pause",
            "Pausar sesión de enfoque",
            "Pausa la sesión en curso, conservando el tiempo restante.",
            SakuraCommandCategory.Focus,
            _ =>
            {
                var result = _focusManager.Pause(DateTimeOffset.Now);
                CheckFocusTimer();
                return Task.FromResult(result.Success
                    ? CommandExecutionResult.Success(result.Message)
                    : CommandExecutionResult.Failure(result.Message));
            },
            keywords: ["enfoque", "pausar", "detener"],
            availability: () => _focusManager.GetSnapshot(DateTimeOffset.Now).ActiveTimer is { Status: FocusTimerStatus.Running }
                ? SakuraCommandAvailability.Available
                : SakuraCommandAvailability.Unavailable("No hay ninguna sesión de enfoque en curso para pausar.")));

        registry.Register(new SakuraCommandDescriptor(
            "focus.resume",
            "Continuar sesión de enfoque",
            "Reanuda la sesión pausada.",
            SakuraCommandCategory.Focus,
            _ =>
            {
                var result = _focusManager.Resume(DateTimeOffset.Now);
                CheckFocusTimer();
                return Task.FromResult(result.Success
                    ? CommandExecutionResult.Success(result.Message)
                    : CommandExecutionResult.Failure(result.Message));
            },
            keywords: ["enfoque", "continuar", "reanudar", "resumir"],
            availability: () => _focusManager.GetSnapshot(DateTimeOffset.Now).ActiveTimer is { Status: FocusTimerStatus.Paused }
                ? SakuraCommandAvailability.Available
                : SakuraCommandAvailability.Unavailable("No hay ninguna sesión de enfoque en pausa.")));

        registry.Register(new SakuraCommandDescriptor(
            "focus.finish",
            "Finalizar sesión de enfoque",
            "Termina la sesión ahora y cuenta el tiempo ya transcurrido, a diferencia de Cancelar.",
            SakuraCommandCategory.Focus,
            _ => Task.FromResult(FinishActiveFocusSession()),
            keywords: ["enfoque", "finalizar", "terminar", "completar"],
            availability: () => _focusManager.GetSnapshot(DateTimeOffset.Now).ActiveTimer is not null
                ? SakuraCommandAvailability.Available
                : SakuraCommandAvailability.Unavailable("No hay ninguna sesión de enfoque activa.")));

        registry.Register(new SakuraCommandDescriptor(
            "focus.history",
            "Mostrar historial de enfoque",
            "Abre Enfoque, donde están las últimas sesiones y el resumen del día.",
            SakuraCommandCategory.Focus,
            _ =>
            {
                NavigateTo(ShellNavigationPolicy.Focus, animate: _preferences.AnimationsEnabled);
                return Task.FromResult(CommandExecutionResult.Success());
            },
            keywords: ["enfoque", "historial", "actividad", "resumen", "sesiones"]));

        registry.Register(new SakuraCommandDescriptor(
            "ambient.contextPeek",
            "¿Qué ventana tengo activa?",
            "Muestra en el Sakura Pill Host el título y el proceso de la última ventana externa " +
            "que tuviste activa, sin robarle el foco.",
            SakuraCommandCategory.Ambient,
            _ => ExecuteAmbientContextPeekAsync(),
            keywords: ["ventana", "activa", "contexto", "ambiental", "pill", "sakura"],
            availability: () => _ambientRequestManager.GetSnapshot(recentCount: 0).ActiveRequest is null
                ? SakuraCommandAvailability.Available
                : SakuraCommandAvailability.Unavailable("Ya hay una solicitud ambiental en curso.")));

        registry.Register(new SakuraCommandDescriptor(
            "ambient.history",
            "Ver historial de solicitudes ambientales",
            "Abre el historial de solicitudes del Sakura Pill Host, con la opción de deshacer las que aplique.",
            SakuraCommandCategory.Ambient,
            _ =>
            {
                ShowAmbientHistory();
                return Task.FromResult(CommandExecutionResult.Success());
            },
            keywords: ["ambiental", "historial", "solicitudes", "pill", "sakura"]));

        registry.RegisterRange(BuildLensCommands());
        registry.RegisterRange(BuildOptimizationCommands());
        registry.RegisterRange(BuildMemoryCommands());
        registry.RegisterRange(BuildWorkspaceCommands());
        registry.RegisterRange(BuildSkillPackCommands());
        registry.RegisterRange(BuildComputerUseCommands());
        registry.RegisterRange(BuildProductizationCommands());

        registry.Register(new SakuraCommandDescriptor(
            "tasks.create",
            "Crear una tarea",
            "Abre Hoy para añadir una tarea nueva.",
            SakuraCommandCategory.Tasks,
            _ =>
            {
                NavigateTo(ShellNavigationPolicy.Tasks, animate: _preferences.AnimationsEnabled);
                _tasksView.OpenNewEditor();
                return Task.FromResult(CommandExecutionResult.Success());
            },
            keywords: ["tarea", "pendiente", "nueva", "añadir", "hoy"]));

        registry.Register(new SakuraCommandDescriptor(
            "audio.mute",
            "Silenciar audio",
            "Silencia el volumen maestro del equipo.",
            SakuraCommandCategory.Audio,
            _ => Task.FromResult(ApplyMasterMute(muted: true)),
            keywords: ["audio", "silenciar", "mute", "volumen"]));

        registry.Register(new SakuraCommandDescriptor(
            "audio.unmute",
            "Restaurar audio",
            "Quita el silencio del volumen maestro.",
            SakuraCommandCategory.Audio,
            _ => Task.FromResult(ApplyMasterMute(muted: false)),
            keywords: ["audio", "restaurar", "activar", "volumen", "sonido"]));

        registry.Register(new SakuraCommandDescriptor(
            "voice.settings",
            "Abrir configuración de voz",
            "Va a Personalizar, donde vive la configuración de voz.",
            SakuraCommandCategory.System,
            _ =>
            {
                NavigateTo(ShellNavigationPolicy.Settings, animate: _preferences.AnimationsEnabled);
                return Task.FromResult(CommandExecutionResult.Success());
            },
            keywords: ["voz", "micrófono", "whisper", "wake word", "dictado"]));

        registry.Register(new SakuraCommandDescriptor(
            "engine.settings",
            "Ir a configuración del motor",
            "Muestra en Sistema los motores registrados, el recomendado y el configurado.",
            SakuraCommandCategory.System,
            _ =>
            {
                NavigateTo(ShellNavigationPolicy.System, animate: _preferences.AnimationsEnabled);
                return Task.FromResult(CommandExecutionResult.Success());
            },
            keywords: ["motor", "engine", "registry", "adaptativo", "recomendado", "rendimiento"]));

        registry.RegisterRange(BuildRoutineExecutionCommands());

        return registry;
    }

    /// <summary>
    /// Diseño D3.1 — un comando por cada preset de duración, solo disponible cuando no hay ya una
    /// sesión de enfoque en curso: nunca deben aparecer dos comandos de "iniciar" e "finalizar/
    /// pausar" incompatibles a la vez como disponibles. Cada uno reusa
    /// <see cref="FocusView.StartPreset"/> (no llama a FocusManager.Start directamente) para no
    /// perder una tarea pendiente de asociar si el usuario ya venía de "Enfocarme" en una tarea.
    /// </summary>
    /// <summary>
    /// Diseño D5.6 (Fase 2 — Sakura Lens) — un comando por modo, no un único comando genérico con
    /// un selector: así cada modo aparece por su propio nombre en la búsqueda, igual que los
    /// presets de Enfoque (<see cref="BuildFocusStartCommands"/>).
    /// </summary>
    private IEnumerable<SakuraCommandDescriptor> BuildLensCommands()
    {
        (LensMode Mode, string Id, string Title, string Description, string[] Keywords)[] presets =
        [
            (LensMode.Soporte, "lens.soporte", "Sakura Lens · Soporte",
                "Observa la ventana activa y explica qué problema hay y cómo resolverlo.",
                ["lens", "soporte", "ayuda", "problema", "observar", "mirando"]),
            (LensMode.Estudio, "lens.estudio", "Sakura Lens · Estudio",
                "Observa la ventana activa y explica qué es y cómo funciona, paso a paso.",
                ["lens", "estudio", "aprender", "explicar", "observar", "mirando"]),
            (LensMode.Desarrollo, "lens.desarrollo", "Sakura Lens · Desarrollo",
                "Observa la ventana activa y analiza el código o error visible.",
                ["lens", "desarrollo", "codigo", "error", "diagnostico", "observar", "mirando"])
        ];

        foreach (var (mode, id, title, description, keywords) in presets)
        {
            yield return new SakuraCommandDescriptor(
                id,
                title,
                description,
                SakuraCommandCategory.Capture,
                _ => ExecuteLensAsync(mode),
                keywords: keywords,
                availability: () => _ambientRequestManager.GetSnapshot(recentCount: 0).ActiveRequest is null
                    ? SakuraCommandAvailability.Available
                    : SakuraCommandAvailability.Unavailable("Ya hay una solicitud ambiental en curso."));
        }
    }

    /// <summary>
    /// Diseño D8 (Fase 4) — un comando por escenario. Todos PROPONEN: muestran el plan y lo que
    /// cambiaría, sin tocar nada. Aplicar es un segundo paso explícito, porque cambiar la
    /// configuración del sistema es, en el modelo de confianza, riesgo alto con snapshot previo
    /// obligatorio.
    /// </summary>
    private IEnumerable<SakuraCommandDescriptor> BuildOptimizationCommands()
    {
        (OptimizationScenario Scenario, string Id, string Title)[] presets =
        [
            (OptimizationScenario.Jugar, "optimize.jugar", "Optimizar para jugar"),
            (OptimizationScenario.Programar, "optimize.programar", "Optimizar para programar"),
            (OptimizationScenario.EdicionVideo, "optimize.video", "Optimizar para editar video"),
            (OptimizationScenario.Videollamada, "optimize.videollamada", "Optimizar para videollamada"),
            (OptimizationScenario.Bateria, "optimize.bateria", "Optimizar para batería"),
            (OptimizationScenario.General, "optimize.general", "Optimizar para uso general")
        ];

        foreach (var (scenario, id, title) in presets)
        {
            yield return new SakuraCommandDescriptor(
                id,
                title,
                "Revisa tu hardware real y propone los cambios que tengan sentido. No aplica nada sin que lo confirmes.",
                SakuraCommandCategory.System,
                _ => ProposeOptimizationAsync(scenario),
                keywords: ["optimizar", "rendimiento", "equipo", "pc", title.ToLowerInvariant()]);
        }

        yield return new SakuraCommandDescriptor(
            "optimize.restaurar",
            "Deshacer la última optimización",
            "Devuelve el equipo al estado guardado antes del último plan aplicado.",
            SakuraCommandCategory.System,
            _ => Task.FromResult(RestoreOptimization()),
            keywords: ["deshacer", "restaurar", "optimizacion", "revertir"],
            availability: () => _optimizationCoordinator.HasSomethingToUndo
                ? SakuraCommandAvailability.Available
                : SakuraCommandAvailability.Unavailable("No hay ninguna optimización aplicada que deshacer."));

        // Diseño D11 — la auditoría es consultable desde el mismo sitio que todo lo demás. Un
        // registro que solo se puede leer abriendo un archivo a mano no lo lee nadie.
        yield return new SakuraCommandDescriptor(
            "optimize.historial",
            "Ver el historial de optimizaciones",
            "Muestra qué se aplicó, qué se deshizo y qué falló, con su fecha.",
            SakuraCommandCategory.System,
            _ =>
            {
                ShowOptimizationAudit();
                return Task.FromResult(CommandExecutionResult.Success());
            },
            keywords: ["historial", "auditoria", "optimizacion", "registro"]);

        // Diseño D13 — el registro completo, no solo el de una capacidad. "¿Qué ha hecho Sakura en
        // mi equipo?" es una pregunta sola, y no debería obligar a saber en qué apartado mirar.
        yield return new SakuraCommandDescriptor(
            "audit.show",
            "Ver todo lo que Sakura ha hecho",
            "El registro completo: qué hizo, cuándo, con qué permiso y cómo deshacerlo.",
            SakuraCommandCategory.System,
            _ =>
            {
                ShowFullAudit();
                return Task.FromResult(CommandExecutionResult.Success());
            },
            keywords: ["auditoria", "registro", "actividad", "historial", "privacidad"]);
    }

    private void RefreshAuditPanel() => _systemView.UpdateAudit(_auditLog.Read());

    /// <summary>
    /// Diseño D18 — deshacer una acción concreta desde el registro. El despacho es por capacidad, y
    /// cada una usa su propio camino de reversión: el registro dice QUÉ se puede deshacer, pero
    /// quien sabe CÓMO sigue siendo la capacidad que lo hizo.
    /// </summary>
    private void RevertAuditEntry(AuditEntry entry)
    {
        if (!entry.CanRevert)
        {
            return;
        }

        var confirmation = MessageBox.Show(
            this,
            $"{entry.Detail}{Environment.NewLine}{Environment.NewLine}{entry.RevertHint}" +
                $"{Environment.NewLine}{Environment.NewLine}¿Lo deshago?",
            "Deshacer",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        var detail = entry.Capability switch
        {
            AuditCapability.Optimizacion => _optimizationCoordinator.Restore().Detail,

            AuditCapability.Proyecto when Guid.TryParse(entry.RevertToken, out var checkpointId) =>
                _workspaceEditCoordinator.Revert(checkpointId, _preferences.Workspace).Detail,

            AuditCapability.Permisos when Guid.TryParse(entry.RevertToken, out var snapshotId) =>
                _computerUseCoordinator.Revert(snapshotId, _preferences.ComputerUseAutonomyLevel).Detail,

            // Desactivar un pack toca preferencias con efectos vivos, así que va por su propio
            // camino y no por el genérico: reaplicarlas es parte de deshacerlo.
            AuditCapability.Permisos when Enum.TryParse<SkillPackId>(entry.RevertToken, out _) =>
                RevertSkillPack().Message ?? "Pack desactivado.",

            _ => "Esa acción no sé deshacerla desde aquí."
        };

        _assistantView.AddSakuraMessage(detail);
        ShowFlowNotice(CapsuleKind.Information, "Deshacer", detail);
        RefreshAuditPanel();
    }

    private static string CapabilityTitleFor(SakuraCapability capability) =>
        CapabilityText.Describe(capability);

    /// <summary>
    /// Diseño D16 — el único punto por el que una capacidad pide permiso. Devuelve true si puede
    /// seguir. Cuando el broker pide confirmación, se pregunta aquí y la respuesta queda registrada:
    /// un "sí" que no deja rastro es indistinguible de un permiso que nadie dio.
    /// </summary>
    private bool TryGetPermission(PermissionRequest request)
    {
        var decision = PermissionBroker.Decide(request, _preferences.Permissions);

        if (decision.IsDenied)
        {
            _assistantView.AddSakuraMessage(decision.Reason);
            RecordAudit(
                AuditCapability.Permisos,
                $"Acción denegada ({request.Capability})",
                $"{request.Description} — {decision.Reason}",
                "Permiso denegado");
            return false;
        }

        if (decision.MayProceedWithoutAsking)
        {
            return true;
        }

        var confirmation = MessageBox.Show(
            this,
            $"{request.Description}{Environment.NewLine}{Environment.NewLine}{decision.Reason}" +
                $"{Environment.NewLine}{Environment.NewLine}¿Lo hago?",
            $"Permiso: {CapabilityTitleFor(request.Capability)}",
            MessageBoxButton.YesNo,
            decision.TriggeredCategories.Count > 0
                ? MessageBoxImage.Warning
                : MessageBoxImage.Question);

        var granted = confirmation == MessageBoxResult.Yes;

        RecordAudit(
            AuditCapability.Permisos,
            granted
                ? $"Acción autorizada ({request.Capability})"
                : $"Acción rechazada ({request.Capability})",
            request.Description,
            granted ? "Confirmación explícita del usuario" : "El usuario dijo que no");

        return granted;
    }

    private void ShowFullAudit()
    {
        var entries = _auditLog.Read();

        var message = new StringBuilder();
        if (entries.Count == 0)
        {
            message.Append("Todavía no he hecho nada que valga la pena registrar.");
        }
        else
        {
            message.AppendLine("Todo lo que he hecho, de lo más reciente a lo más antiguo:");
            foreach (var entry in entries.Take(25))
            {
                message.Append("· ").AppendLine(entry.Describe());
            }
        }

        _assistantView.AddSakuraMessage(message.ToString().TrimEnd());
        NavigateTo("Assistant", animate: true);
    }

    /// <summary>
    /// Diseño D13 — conceder o revocar un permiso es de lo primero que debe quedar registrado: es
    /// la decisión de la que cuelgan todas las demás.
    /// </summary>
    private void RecordAudit(
        AuditCapability capability,
        string action,
        string detail,
        string permission,
        int? autonomyLevel = null,
        string revertHint = "")
    {
        _auditLog.Append(new AuditEntry
        {
            At = DateTimeOffset.Now,
            Capability = capability,
            Action = action,
            Detail = detail,
            Permission = permission,
            AutonomyLevel = autonomyLevel,
            RevertHint = revertHint
        });

        RefreshAuditPanel();
    }

    private void ShowOptimizationAudit()
    {
        var entries = _optimizationCoordinator.ReadAudit();

        var message = new StringBuilder();
        if (entries.Count == 0)
        {
            message.Append("Todavía no he aplicado ninguna optimización.");
        }
        else
        {
            message.AppendLine("Historial de optimizaciones:");
            foreach (var entry in entries.Take(15))
            {
                message.Append("· ").AppendLine(entry.Describe());
            }
        }

        _assistantView.AddSakuraMessage(message.ToString().TrimEnd());
        NavigateTo("Assistant", animate: true);
    }

    private async Task<CommandExecutionResult> ProposeOptimizationAsync(OptimizationScenario scenario)
    {
        var profile = await _hardwareCapabilityService.RefreshAsync(_lifetimeCancellation.Token);
        var plan = OptimizationPlanBuilder.Build(scenario, profile);

        ShowOptimizationPlan(plan);
        return CommandExecutionResult.Success();
    }

    /// <summary>
    /// Diseño D8 — enseña el plan y, solo si hay algo que Sakura pueda aplicar Y revertir, ofrece
    /// aplicarlo. La confirmación es un diálogo explícito: el modelo de confianza no permite pasar
    /// de proponer a actuar sin que la persona lo diga.
    /// </summary>
    private void ShowOptimizationPlan(OptimizationPlan plan)
    {
        var message = new StringBuilder();
        message.AppendLine(plan.Summary);

        foreach (var change in plan.Changes)
        {
            message.AppendLine();
            message.Append(change.Target == OptimizationTarget.Advice ? "· Consejo: " : "· Cambio: ");
            message.AppendLine(change.Title);
            message.Append("  ").AppendLine(change.Justification);
        }

        foreach (var skipped in plan.SkippedForMissingData)
        {
            message.AppendLine();
            message.Append("· No propuesto: ").AppendLine(skipped);
        }

        _assistantView.AddSakuraMessage(message.ToString().TrimEnd());

        if (!plan.RequiresSnapshot)
        {
            return;
        }

        var confirmation = MessageBox.Show(
            this,
            $"{plan.Summary}" + Environment.NewLine + Environment.NewLine +
                "¿Aplico los cambios? Guardaré cómo está ahora para poder deshacerlo.",
            "Optimizar equipo",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        ApplyOptimization(plan);
    }

    /// <summary>
    /// Diseño D11 — el orden (snapshot antes que nada, reversión de lo ya aplicado si un paso falla)
    /// vive ahora en <see cref="OptimizationCoordinator"/>, en Core, donde se puede probar con
    /// dobles. Aquí solo queda enseñar el resultado.
    /// </summary>
    private void ApplyOptimization(OptimizationPlan plan)
    {
        var result = _optimizationCoordinator.Apply(plan);

        ShowFlowNotice(
            result.IsApplied ? CapsuleKind.Success : CapsuleKind.Warning,
            result.IsApplied ? "Equipo optimizado" : "No se pudo optimizar",
            result.Detail);

        _systemView.SetOptimizationStatus(result.Detail, _optimizationCoordinator.HasSomethingToUndo);
        RefreshAuditPanel();
    }

    private CommandExecutionResult RestoreOptimization()
    {
        var result = _optimizationCoordinator.Restore();

        _systemView.SetOptimizationStatus(result.Detail, _optimizationCoordinator.HasSomethingToUndo);
        RefreshAuditPanel();

        if (!result.IsApplied)
        {
            return CommandExecutionResult.Failure(result.Detail);
        }

        ShowFlowNotice(CapsuleKind.Success, "Optimización deshecha", result.Detail);
        return CommandExecutionResult.Success(result.Detail);
    }

    /// <summary>
    /// Diseño D9 (Fase 6) — comandos de memoria. "Ver" y "olvidar todo" existen aunque la memoria
    /// esté apagada: revocar y auditar deben funcionar SIEMPRE. Si apagar la memoria bloqueara el
    /// borrado, lo ya guardado quedaría atrapado justo cuando la persona quiere deshacerse de ello.
    /// </summary>
    private IEnumerable<SakuraCommandDescriptor> BuildMemoryCommands()
    {
        yield return new SakuraCommandDescriptor(
            "memory.show",
            "Ver lo que Sakura recuerda",
            "Muestra, en texto claro, todo lo que hay guardado en la memoria.",
            SakuraCommandCategory.System,
            _ =>
            {
                ShowMemoryContents();
                return Task.FromResult(CommandExecutionResult.Success());
            },
            keywords: ["memoria", "recuerda", "guardado", "privacidad"]);

        yield return new SakuraCommandDescriptor(
            "memory.forgetAll",
            "Olvidar todo lo que Sakura recuerda",
            "Borra por completo la memoria guardada. No se puede deshacer.",
            SakuraCommandCategory.System,
            _ => Task.FromResult(ForgetAllMemory()),
            keywords: ["olvidar", "borrar", "memoria", "privacidad"]);
    }

    /// <summary>
    /// Diseño D10 (Fase 6) — la memoria ya se llena desde la conversación, que es lo que D9 dejó
    /// pendiente. Dos caminos con reglas distintas a propósito:
    ///
    /// - **Explícito** ("recuerda que ..."): se guarda. La persona acaba de dar la orden; volver a
    ///   preguntarle "¿seguro?" sería ruido. Si la política lo rechaza, se dice POR QUÉ — un
    ///   "recuerda que ..." que no guarda nada y no explica nada parecería que funcionó.
    /// - **Observado** (una preferencia dicha de paso): **nunca** se guarda solo. Se propone y hace
    ///   falta un sí. Guardar lo que alguien mencionó sin pedirlo es exactamente la vigilancia
    ///   silenciosa que el roadmap prohíbe para esta fase.
    ///
    /// Devuelve true solo cuando la frase ERA la orden de memoria y ya está atendida.
    /// </summary>
    private bool TryHandleMemoryPrompt(string prompt)
    {
        if (_pendingMemoryCandidate is { } pending)
        {
            // Una sola oportunidad: la propuesta caduca con la siguiente frase, sea cual sea. Una
            // pregunta que sigue viva varios turnos acabaría capturando un "sí" dicho por otra cosa.
            _pendingMemoryCandidate = null;

            if (IsVoiceConfirmation(SpanishVoiceTranscriptNormalizer.Normalize(prompt)))
            {
                SaveMemoryCandidate(pending);
                return true;
            }
        }

        var candidate = MemoryCandidateDetector.Detect(prompt);
        if (candidate is null)
        {
            return false;
        }

        if (candidate.Source == MemoryCandidateSource.Explicito)
        {
            SaveMemoryCandidate(candidate);
            return true;
        }

        // Se consulta la política ANTES de proponer: preguntar "¿lo recuerdo?" para después
        // rechazarlo por una exclusión propia sería hacer perder el tiempo a la persona.
        var verdict = MemoryPolicy.CanRemember(candidate.Category, candidate.Text, _preferences.Memory);
        if (!verdict.Success)
        {
            // En silencio: nadie pidió recordar nada, así que un aviso aquí sería una interrupción
            // no solicitada.
            return false;
        }

        _pendingMemoryCandidate = candidate;
        ShowFlowNotice(
            CapsuleKind.Information,
            "¿Lo recuerdo?",
            $"«{candidate.Text}». Responde «sí» para guardarlo.");

        // La frase sigue su curso normal: la propuesta es aparte, no reemplaza la conversación.
        return false;
    }

    private void SaveMemoryCandidate(MemoryCandidate candidate)
    {
        var result = _memoryManager.Remember(
            candidate.Category,
            candidate.Text,
            _preferences.Memory,
            DateTimeOffset.Now);

        _assistantView.AddSakuraMessage(result.Success
            ? $"{result.Message} «{candidate.Text}»"
            : result.Message);

        ShowFlowNotice(
            result.Success ? CapsuleKind.Success : CapsuleKind.Warning,
            result.Success ? "Lo recordaré" : "No lo recordé",
            result.Message);
    }

    private void ShowMemoryContents()
    {
        var settings = _preferences.Memory;
        var entries = _memoryManager.GetAll(settings, DateTimeOffset.Now);

        var message = new StringBuilder();
        message.AppendLine(settings.Enabled
            ? $"Memoria activada · retención de {settings.RetentionDays} días."
            : "La memoria está desactivada: no estoy guardando nada nuevo.");

        if (entries.Count == 0)
        {
            message.Append("No hay nada guardado.");
        }
        else
        {
            foreach (var entry in entries)
            {
                message.AppendLine();
                message.Append("· [")
                    .Append(MemoryPolicy.CategoryLabel(entry.Category))
                    .Append("] ")
                    .Append(entry.Text);
            }
        }

        if (settings.Exclusions.Count > 0)
        {
            message.AppendLine();
            message.Append("Exclusiones activas: ").Append(string.Join(", ", settings.Exclusions));
        }

        _assistantView.AddSakuraMessage(message.ToString().TrimEnd());
    }

    private CommandExecutionResult ForgetAllMemory()
    {
        // Diseño D16 — pasa por el broker en vez de por un diálogo propio. Borrar sin recuperación
        // es una de las siete categorías de confirmación obligatoria, así que preguntará aunque la
        // memoria esté permitida: es exactamente lo que el broker existe para garantizar.
        if (!TryGetPermission(new PermissionRequest(
                SakuraCapability.Memoria,
                "Borrar todo lo que Sakura recuerda. No se puede deshacer.",
                Categories: [MandatoryConfirmation.BorradoIrreversible])))
        {
            return CommandExecutionResult.Failure("No borré nada.");
        }

        var result = _memoryManager.ForgetEverything();

        RecordAudit(
            AuditCapability.Memoria,
            "Memoria borrada por completo",
            result.Message,
            "Confirmación explícita del usuario");

        ShowFlowNotice(CapsuleKind.Success, "Memoria borrada", result.Message);
        return CommandExecutionResult.Success(result.Message);
    }

    /// <summary>
    /// Diseño D12 (Fase 5 — Project Companion) — cuatro comandos y ni uno que escriba. La capacidad
    /// vive en los niveles 1–3 del modelo de confianza ("ninguna capacidad nueva puede empezar en el
    /// nivel 6"), así que Sakura lee, explica y guía; los cambios los aplica la persona.
    ///
    /// Autorizar y revocar están al mismo nivel, en el mismo sitio: un permiso que cuesta más
    /// quitar que dar no es un permiso, es una trampa.
    /// </summary>
    private IEnumerable<SakuraCommandDescriptor> BuildWorkspaceCommands()
    {
        yield return new SakuraCommandDescriptor(
            "workspace.authorize",
            "Autorizar una carpeta de proyecto",
            "Elige la carpeta que Sakura podrá leer. Solo lectura: no modifica archivos.",
            SakuraCommandCategory.System,
            _ =>
            {
                AuthorizeWorkspaceFolder();
                return Task.FromResult(CommandExecutionResult.Success());
            },
            keywords: ["proyecto", "carpeta", "workspace", "autorizar", "codigo"]);

        yield return new SakuraCommandDescriptor(
            "workspace.show",
            "Ver el proyecto autorizado",
            "Muestra qué carpeta puede leer Sakura y con qué nivel de autonomía.",
            SakuraCommandCategory.System,
            _ =>
            {
                ShowWorkspaceStatus();
                return Task.FromResult(CommandExecutionResult.Success());
            },
            keywords: ["proyecto", "workspace", "permisos", "acceso"]);

        yield return new SakuraCommandDescriptor(
            "workspace.explain",
            "Explicar el proyecto autorizado",
            "Sakura revisa la estructura del proyecto y te la explica. No cambia nada.",
            SakuraCommandCategory.System,
            async _ => await ExplainWorkspaceAsync(),
            keywords: ["proyecto", "explicar", "estructura", "codigo"],
            availability: () => _preferences.Workspace.HasAuthorizedFolder
                ? SakuraCommandAvailability.Available
                : SakuraCommandAvailability.Unavailable("Todavía no autorizaste ninguna carpeta de proyecto."));

        // Diseño D13 — la búsqueda existía desde D12 pero sin comando que la expusiera. Devuelve
        // ruta, línea y la línea encontrada, ya redactada: una coincidencia puede caer justo encima
        // de un token.
        yield return new SakuraCommandDescriptor(
            "workspace.search",
            "Buscar en el proyecto autorizado",
            "Busca un texto dentro de los archivos que Sakura puede leer.",
            SakuraCommandCategory.System,
            _ =>
            {
                SearchWorkspace();
                return Task.FromResult(CommandExecutionResult.Success());
            },
            keywords: ["buscar", "proyecto", "codigo", "workspace"],
            availability: () => _preferences.Workspace.HasAuthorizedFolder
                ? SakuraCommandAvailability.Available
                : SakuraCommandAvailability.Unavailable("Todavía no autorizaste ninguna carpeta de proyecto."));

        // Diseño D14 (Fase 5, nivel 4) — proponer y aplicar UN cambio. Solo aparece cuando el nivel
        // de autonomía lo permite: un comando visible que siempre responde "no puedo" enseña a
        // ignorar los mensajes de permisos.
        yield return new SakuraCommandDescriptor(
            "workspace.edit",
            "Pedir un cambio en el proyecto",
            "Sakura propone el cambio, te lo enseña y solo lo aplica si lo confirmas. Siempre se puede deshacer.",
            SakuraCommandCategory.System,
            _ =>
            {
                RequestWorkspaceEdit();
                return Task.FromResult(CommandExecutionResult.Success());
            },
            keywords: ["cambiar", "editar", "proyecto", "codigo", "modificar"],
            availability: () => !_preferences.Workspace.HasAuthorizedFolder
                ? SakuraCommandAvailability.Unavailable("Todavía no autorizaste ninguna carpeta de proyecto.")
                : WorkspaceAutonomyPolicy.CanWrite(_preferences.Workspace.AutonomyLevel)
                    ? SakuraCommandAvailability.Available
                    : SakuraCommandAvailability.Unavailable(
                        "Sube el nivel de autonomía del proyecto a «Ejecutar un paso» en Personalizar."));

        yield return new SakuraCommandDescriptor(
            "workspace.undo",
            "Deshacer el último cambio en el proyecto",
            "Devuelve el archivo a como estaba antes de que Sakura lo tocara.",
            SakuraCommandCategory.System,
            _ => Task.FromResult(UndoLastWorkspaceEdit()),
            keywords: ["deshacer", "revertir", "proyecto", "cambio"],
            availability: () => _workspaceEditCoordinator.Checkpoints.Count > 0
                ? SakuraCommandAvailability.Available
                : SakuraCommandAvailability.Unavailable("No he cambiado nada en tu proyecto."));

        yield return new SakuraCommandDescriptor(
            "workspace.revoke",
            "Revocar el acceso al proyecto",
            "Sakura deja de poder leer esa carpeta, en el acto.",
            SakuraCommandCategory.System,
            _ => Task.FromResult(RevokeWorkspace()),
            keywords: ["revocar", "quitar", "proyecto", "acceso", "privacidad"],
            availability: () => _preferences.Workspace.HasAuthorizedFolder
                ? SakuraCommandAvailability.Available
                : SakuraCommandAvailability.Unavailable("No hay ninguna carpeta autorizada."));
    }

    private void AuthorizeWorkspaceFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Elige la carpeta del proyecto que Sakura podrá leer",
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var chosen = dialog.FolderName;

        // Diseño D41 — hay carpetas que no se autorizan: la raíz de una unidad, el perfil entero,
        // las del sistema. Se comprueba ANTES de la confirmación, porque preguntar «¿lo autorizo?»
        // para algo que no se va a conceder es hacer perder el tiempo a quien contesta.
        var rootVerdict = WorkspaceRootPolicy.CanAuthorize(chosen);
        if (!rootVerdict.IsAllowed)
        {
            MessageBox.Show(
                this,
                rootVerdict.Message,
                "Esa carpeta no se puede autorizar",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            _assistantView.AddSakuraMessage(rootVerdict.Message);
            return;
        }

        // La confirmación dice qué se concede Y qué no. Un permiso que solo enumera lo que gana
        // quien lo pide no deja decidir a quien lo da.
        var confirmation = MessageBox.Show(
            this,
            $"Sakura podrá LEER los archivos de:{Environment.NewLine}{chosen}{Environment.NewLine}{Environment.NewLine}" +
                "No podrá modificarlos ni borrarlos. No leerá .env, claves ni carpetas de dependencias, " +
                "y ocultará los valores que parezcan secretos antes de enviar nada a la IA." +
                $"{Environment.NewLine}{Environment.NewLine}Puedes revocarlo cuando quieras. ¿Lo autorizo?",
            "Autorizar carpeta de proyecto",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        _preferences.Workspace.AuthorizedPath = chosen;
        _preferences.Workspace.AuthorizedAt = DateTimeOffset.Now;
        _preferences.Workspace.Normalize();
        SavePreferences();

        RecordAudit(
            AuditCapability.Permisos,
            "Carpeta de proyecto autorizada",
            chosen,
            "Autorización explícita del usuario",
            (int)_preferences.Workspace.AutonomyLevel,
            "Revocar el acceso al proyecto.");

        ShowFlowNotice(
            CapsuleKind.Success,
            "Proyecto autorizado",
            $"Puedo leer {Path.GetFileName(Path.TrimEndingDirectorySeparator(chosen))}. Solo lectura.");
        ShowWorkspaceStatus();
        _settingsView.ApplyWorkspaceSettings(_preferences.Workspace);
    }

    /// <summary>
    /// Diseño D13 — la paleta de comandos no acepta argumentos, así que la consulta se pide en la
    /// conversación: el comando deja a Sakura esperando UNA frase, igual que la propuesta de
    /// recuerdo de D10. Vale una sola, y se cancela como cualquier otra pregunta.
    /// </summary>
    private void SearchWorkspace()
    {
        _awaitingWorkspaceSearchQuery = true;
        _assistantView.AddSakuraMessage("¿Qué busco en el proyecto? Escríbelo y lo busco.");
        NavigateTo("Assistant", animate: true);
    }

    private bool TryHandleWorkspaceSearchPrompt(string prompt)
    {
        if (!_awaitingWorkspaceSearchQuery)
        {
            return false;
        }

        _awaitingWorkspaceSearchQuery = false;

        var normalized = SpanishVoiceTranscriptNormalizer.Normalize(prompt);
        if (IsVoiceCancellation(normalized))
        {
            _assistantView.AddSakuraMessage("De acuerdo, no busco nada.");
            return true;
        }

        var workspace = _preferences.Workspace;
        if (!workspace.HasAuthorizedFolder)
        {
            // El acceso pudo revocarse entre el comando y la respuesta. Revocar tiene que surtir
            // efecto en el acto, incluso a media conversación.
            _assistantView.AddSakuraMessage("Ya no tengo ninguna carpeta autorizada, así que no busqué nada.");
            return true;
        }

        var hits = _workspaceReader.Search(workspace.AuthorizedPath, prompt, maximumHits: 40);

        var message = new StringBuilder();
        if (hits.Count == 0)
        {
            message.Append($"No encontré «{prompt.Trim()}» en el proyecto.");
        }
        else
        {
            message.AppendLine($"{hits.Count} coincidencias de «{prompt.Trim()}»:");
            foreach (var hit in hits.Take(25))
            {
                message.Append("· ")
                    .Append(hit.RelativePath)
                    .Append(':')
                    .Append(hit.LineNumber)
                    .Append("  ")
                    .AppendLine(hit.Line);
            }
        }

        _assistantView.AddSakuraMessage(message.ToString().TrimEnd());
        return true;
    }

    /// <summary>
    /// Diseño D17 (Fase 7 — Safe Computer Use) — Sakura dice **qué haría y por qué método**, sin
    /// hacerlo. El roadmap prohíbe saltarse niveles del modelo de autonomía, y esta capacidad acaba
    /// de nacer: empieza donde tienen que empezar todas.
    /// </summary>
    private IEnumerable<SakuraCommandDescriptor> BuildComputerUseCommands()
    {
        yield return new SakuraCommandDescriptor(
            "computeruse.plan",
            "Proponer cómo hacer algo en el equipo",
            "Sakura elige la forma más segura de hacerlo y te la explica. No lo ejecuta.",
            SakuraCommandCategory.System,
            _ =>
            {
                _awaitingComputerUseIntent = true;
                _assistantView.AddSakuraMessage(
                    "¿Qué quieres hacer en el equipo? Te digo cómo lo haría y por qué de esa forma.");
                NavigateTo("Assistant", animate: true);
                return Task.FromResult(CommandExecutionResult.Success());
            },
            keywords: ["equipo", "hacer", "accion", "automatizar", "computer use"]);

        yield return new SakuraCommandDescriptor(
            "computeruse.methods",
            "Ver cómo puede actuar Sakura en el equipo",
            "Muestra los métodos disponibles, en orden de más a menos seguro.",
            SakuraCommandCategory.System,
            _ =>
            {
                ShowComputerUseMethods();
                return Task.FromResult(CommandExecutionResult.Success());
            },
            keywords: ["metodos", "seguridad", "equipo", "computer use"]);

        // Diseño D18 (Fase 7, nivel 4) — los comandos de la lista de permitidos, uno por uno. Solo
        // aparecen habilitados cuando el permiso y el nivel lo consienten: un comando visible que
        // siempre responde "no puedo" enseña a ignorar los mensajes de permisos.
        foreach (var command in SafeShellCatalog.All)
        {
            var current = command;

            yield return new SakuraCommandDescriptor(
                $"computeruse.run.{current.Id}",
                current.Title,
                $"Ejecuta «{current.Executable} {current.Arguments}». Solo lee: no cambia nada del equipo.",
                SakuraCommandCategory.System,
                _ =>
                {
                    RunSafeCommand(current);
                    return Task.FromResult(CommandExecutionResult.Success());
                },
                keywords: ["equipo", "diagnostico", current.Title.ToLowerInvariant()],
                availability: ComputerUseAvailability);
        }
    }

    /// <summary>
    /// Diseño D20 (Fase 9 — Productization) — actualizar y desinstalar sin sorpresas. Nada de esto
    /// actualiza ni desinstala: prepara la copia verificada de la que volver, y enseña qué se lleva
    /// y qué se queda.
    /// </summary>
    private IEnumerable<SakuraCommandDescriptor> BuildProductizationCommands()
    {
        yield return new SakuraCommandDescriptor(
            "data.inventory",
            "Ver qué guarda Sakura en tu equipo",
            "La lista completa: qué es cada archivo, si contiene datos tuyos y si está cifrado.",
            SakuraCommandCategory.System,
            _ =>
            {
                ShowDataInventory();
                return Task.FromResult(CommandExecutionResult.Success());
            },
            keywords: ["datos", "privacidad", "guardar", "archivos"]);

        yield return new SakuraCommandDescriptor(
            "update.prepare",
            "Preparar una copia antes de actualizar",
            "Copia tus datos y comprueba que la copia está bien, para poder volver si algo sale mal.",
            SakuraCommandCategory.System,
            _ =>
            {
                PrepareUpdateBackup();
                return Task.FromResult(CommandExecutionResult.Success());
            },
            keywords: ["actualizar", "copia", "seguridad", "respaldo"]);

        yield return new SakuraCommandDescriptor(
            "update.rollback",
            "Restaurar la última copia de tus datos",
            "Devuelve tus ajustes, tareas y demás a como estaban en la copia más reciente.",
            SakuraCommandCategory.System,
            _ =>
            {
                RestoreLatestBackup();
                return Task.FromResult(CommandExecutionResult.Success());
            },
            keywords: ["restaurar", "copia", "volver", "respaldo"],
            availability: () => _backupService.ListBackups().Count > 0
                ? SakuraCommandAvailability.Available
                : SakuraCommandAvailability.Unavailable("Todavía no hay ninguna copia guardada."));

        // Diseño D21 — el criterio de terminado de la Fase 9 pide "diagnóstico exportable para
        // soporte". Exportar es enviar, así que el archivo se redacta y dice qué dejó fuera.
        yield return new SakuraCommandDescriptor(
            "support.export",
            "Exportar un diagnóstico para soporte",
            "Guarda un archivo con versiones, estado y actividad reciente. Sin tus datos, y te dice qué dejó fuera.",
            SakuraCommandCategory.System,
            _ =>
            {
                ExportSupportBundle();
                return Task.FromResult(CommandExecutionResult.Success());
            },
            keywords: ["soporte", "diagnostico", "exportar", "ayuda", "problema"]);

        // Diseño D22 — comprobar aquí y ahora, sobre el binario instalado, que las garantías se
        // cumplen en ESTE equipo.
        yield return new SakuraCommandDescriptor(
            "selfcheck.run",
            "Comprobar que Sakura funciona bien",
            "Revisa permisos, migraciones, cifrado, copias y redacción en este equipo. No toca tus datos.",
            SakuraCommandCategory.System,
            _ =>
            {
                RunSelfCheck();
                return Task.FromResult(CommandExecutionResult.Success());
            },
            keywords: ["comprobar", "revisar", "diagnostico", "funciona", "prueba"]);

        yield return new SakuraCommandDescriptor(
            "privacy.report",
            "Ver el informe de privacidad",
            "Qué guarda Sakura, dónde, si está cifrado y cómo borrarlo.",
            SakuraCommandCategory.System,
            _ =>
            {
                ShowPrivacyReport();
                return Task.FromResult(CommandExecutionResult.Success());
            },
            keywords: ["privacidad", "datos", "cifrado", "borrar"]);

        yield return new SakuraCommandDescriptor(
            "uninstall.plan",
            "Ver qué pasaría al desinstalar Sakura",
            "Enseña qué se borraría y qué se conservaría, antes de desinstalar nada.",
            SakuraCommandCategory.System,
            _ =>
            {
                ShowUninstallPlan();
                return Task.FromResult(CommandExecutionResult.Success());
            },
            keywords: ["desinstalar", "borrar", "quitar", "datos"]);
    }

    private void ShowDataInventory()
    {
        var message = new StringBuilder();
        message.AppendLine($"Todo lo que guardo está en {NexoDataPaths.RootDirectory}:");

        foreach (var item in SakuraDataInventory.All)
        {
            var exists = File.Exists(item.FullPath);
            message.AppendLine();
            message.Append("· ").Append(item.Title)
                .Append(exists ? "" : " (todavía no existe)")
                .AppendLine();
            message.Append("  ").AppendLine(item.WhatItHolds);
            message.Append("  ")
                .Append(item.IsPersonal ? "Contiene datos tuyos." : "No contiene datos personales.")
                .AppendLine(item.IsEncrypted ? " Cifrado." : string.Empty);
        }

        _assistantView.AddSakuraMessage(message.ToString().TrimEnd());
        NavigateTo("Assistant", animate: true);
    }

    private void PrepareUpdateBackup()
    {
        var readiness = _updateSafety.PrepareUpdate();

        var message = new StringBuilder();
        message.AppendLine(readiness.Detail);

        if (readiness.Backup is { } backup)
        {
            foreach (var file in backup.Files.Where(file => file.Copied))
            {
                message.Append("· ").Append(file.FileName).Append(" — ").AppendLine(file.Detail);
            }
        }

        _assistantView.AddSakuraMessage(message.ToString().TrimEnd());
        NavigateTo("Assistant", animate: true);

        ShowFlowNotice(
            readiness.CanUpdate ? CapsuleKind.Success : CapsuleKind.Warning,
            readiness.CanUpdate ? "Copia verificada" : "Copia incompleta",
            readiness.Detail);

        RefreshAuditPanel();
    }

    private void RestoreLatestBackup()
    {
        var latest = _backupService.ListBackups().FirstOrDefault();
        if (latest is null)
        {
            _assistantView.AddSakuraMessage("Todavía no hay ninguna copia guardada.");
            return;
        }

        // Restaurar sobrescribe lo que tengas ahora: es de las cosas que el modelo de confianza
        // manda confirmar siempre, tenga el permiso que tenga.
        if (!TryGetPermission(new PermissionRequest(
                SakuraCapability.Memoria,
                $"Restaurar la copia «{latest}». Sobrescribe tus ajustes, tareas y memoria actuales.",
                Categories: [MandatoryConfirmation.BorradoIrreversible])))
        {
            return;
        }

        var result = _updateSafety.Rollback(latest);

        _assistantView.AddSakuraMessage(result.Detail);
        ShowFlowNotice(
            result.Success ? CapsuleKind.Success : CapsuleKind.Warning,
            result.Success ? "Datos restaurados" : "No se pudo restaurar",
            result.Detail);

        if (result.Success)
        {
            _assistantView.AddSakuraMessage(
                "Reinicia Sakura para que use los datos restaurados.");
        }

        RefreshAuditPanel();
    }

    /// <summary>
    /// Diseño D21 — genera el paquete de soporte y deja que la persona elija dónde guardarlo.
    /// Exportar es enviar: el contenido va redactado y el archivo dice qué dejó fuera, para que se
    /// pueda revisar antes de mandárselo a nadie.
    /// </summary>
    private async void ExportSupportBundle()
    {
        NexoDiagnosticSnapshot snapshot;
        try
        {
            snapshot = await _diagnosticService.CaptureAsync(
                _preferences,
                _voiceCoordinator.GetInputDevices(),
                _voiceCoordinator.IsVoiceInputReady,
                _voiceCoordinator.IsWakeWordReady,
                _voiceCoordinator.IsWakeWordListening,
                trayActive: true,
                _startupService.IsEnabled(),
                _lifetimeCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var bundle = SupportBundleBuilder.Build(snapshot, _auditLog.Read(), File.Exists);

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Guardar el diagnóstico para soporte",
            FileName = $"kohana-soporte-{DateTimeOffset.Now:yyyyMMdd-HHmm}.txt",
            Filter = "Archivo de texto (*.txt)|*.txt",
            DefaultExt = ".txt"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            File.WriteAllText(dialog.FileName, bundle);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            ShowFlowNotice(CapsuleKind.Warning, "No pude guardarlo", exception.Message);
            return;
        }

        RecordAudit(
            AuditCapability.Permisos,
            "Diagnóstico exportado",
            PathRedactor.Shorten(dialog.FileName),
            "Exportación pedida por el usuario");

        _assistantView.AddSakuraMessage(
            $"Guardé el diagnóstico en {dialog.FileName}." + Environment.NewLine +
            "Ábrelo y léelo entero antes de mandárselo a nadie: al final dice qué dejé fuera.");
        NavigateTo("Assistant", animate: true);

        ShowFlowNotice(CapsuleKind.Success, "Diagnóstico guardado", "Sin tus datos personales dentro.");
    }

    /// <summary>
    /// Diseño D22 — corre las comprobaciones y enseña el informe. Las de disco van en una carpeta
    /// temporal propia: una autocomprobación que ensucia lo que comprueba no sirve de nada.
    /// </summary>
    private void RunSelfCheck()
    {
        var results = new List<SelfCheckResult>(SelfCheckSuite.RunLogicChecks());
        results.AddRange(_selfCheckProbes.Run());

        var report = SelfCheckReport.Build(results);
        _assistantView.AddSakuraMessage(report);
        NavigateTo("Assistant", animate: true);

        var failed = results.Count(result => result.Status == SelfCheckStatus.Fallo);

        RecordAudit(
            AuditCapability.Permisos,
            "Autocomprobación ejecutada",
            failed == 0
                ? $"{results.Count} comprobaciones, ninguna falla."
                : $"{results.Count} comprobaciones, {failed} fallan.",
            "Comprobación interna, sin tocar datos personales");

        ShowFlowNotice(
            failed == 0 ? CapsuleKind.Success : CapsuleKind.Warning,
            failed == 0 ? "Todo funciona" : $"{failed} comprobaciones fallan",
            failed == 0
                ? "Falta la parte que solo puedes comprobar tú usándola."
                : "Míralas en el chat: están las primeras.");
    }

    private void ShowPrivacyReport()
    {
        var report = PrivacyReportBuilder.Build(
            NexoDataPaths.RootDirectory,
            File.Exists,
            path =>
            {
                try
                {
                    return new FileInfo(path).Length;
                }
                catch (Exception exception) when (
                    exception is IOException or UnauthorizedAccessException)
                {
                    return 0;
                }
            });

        _assistantView.AddSakuraMessage(report);
        NavigateTo("Assistant", animate: true);
    }

    private void ShowUninstallPlan()
    {
        var keep = UninstallPlanner.Build(UninstallDataChoice.Conservar);

        var message = new StringBuilder();
        message.AppendLine("Si desinstalas Sakura conservando tus datos:");
        message.AppendLine();
        message.AppendLine(UninstallPlanner.Describe(keep));
        message.AppendLine();
        message.AppendLine(
            "Si prefieres que no quede nada, borra la carpeta de datos a mano después de " +
            $"desinstalar: {NexoDataPaths.RootDirectory}");

        _assistantView.AddSakuraMessage(message.ToString().TrimEnd());
        NavigateTo("Assistant", animate: true);
    }

    private SakuraCommandAvailability ComputerUseAvailability()
    {
        if (_preferences.Permissions.For(SakuraCapability.ComputerUse).Level == PermissionLevel.Bloqueado)
        {
            return SakuraCommandAvailability.Unavailable(
                "Actuar sobre el equipo está bloqueado. Puedes cambiarlo en Personalizar.");
        }

        return ComputerUseAutonomyPolicy.CanExecute(_preferences.ComputerUseAutonomyLevel)
            ? SakuraCommandAvailability.Available
            : SakuraCommandAvailability.Unavailable(
                "Sube el nivel de autonomía a «Ejecutar un paso» en Personalizar.");
    }

    /// <summary>
    /// Diseño D18 — ejecuta un comando de la lista. La confirmación la pide el broker; el
    /// coordinador comprueba permiso, nivel y método antes de lanzar nada.
    /// </summary>
    private void RunSafeCommand(SafeShellCommand command)
    {
        if (!TryGetPermission(new PermissionRequest(
                SakuraCapability.ComputerUse,
                $"{command.Title} ({command.Executable} {command.Arguments})")))
        {
            return;
        }

        var result = _computerUseCoordinator.Execute(
            new ComputerUseRequest(
                ComputerUseMethod.ShellSeguro,
                command.Title,
                SafeCommandId: command.Id),
            _preferences.Permissions,
            _preferences.ComputerUseAutonomyLevel);

        var message = new StringBuilder();
        message.AppendLine(result.Detail);

        if (!string.IsNullOrWhiteSpace(result.Output))
        {
            message.AppendLine();
            message.Append(result.Output.Trim());
        }

        _assistantView.AddSakuraMessage(message.ToString().TrimEnd());
        NavigateTo("Assistant", animate: true);
        RefreshAuditPanel();
    }

    private void ShowComputerUseMethods()
    {
        var available = _computerUseProbe.GetAvailableMethods(targetApp: null);

        var message = new StringBuilder();
        message.AppendLine(
            "Cuando actúo sobre el equipo, siempre elijo la forma más segura disponible, en este " +
            "orden. Ratón y teclado simulados van los últimos a propósito: no distinguen ventanas " +
            "ni pueden comprobar qué hicieron.");
        message.AppendLine();

        foreach (var method in Enum.GetValues<ComputerUseMethod>().OrderBy(method => (int)method))
        {
            message.Append((int)method).Append(". ")
                .Append(ComputerUseMethodText.Describe(method))
                .Append(available.Contains(method) ? " — disponible" : " — todavía no implementado")
                .AppendLine();
        }

        _assistantView.AddSakuraMessage(message.ToString().TrimEnd());
        NavigateTo("Assistant", animate: true);
    }

    /// <summary>
    /// Diseño D17 — arma el plan y lo enseña. Si algo lo bloquea, se dice **qué** lo bloquea: un
    /// plan que no llega a formarse deja a la persona sin saber qué haría falta para que sí.
    /// </summary>
    private bool TryHandleComputerUseIntent(string prompt)
    {
        if (!_awaitingComputerUseIntent)
        {
            return false;
        }

        _awaitingComputerUseIntent = false;

        if (IsVoiceCancellation(SpanishVoiceTranscriptNormalizer.Normalize(prompt)))
        {
            _assistantView.AddSakuraMessage("De acuerdo, no propongo nada.");
            return true;
        }

        var intent = new ComputerUseIntent(prompt.Trim());
        var plan = ComputerUsePlanner.Build(
            intent,
            _computerUseProbe.GetAvailableMethods(intent.TargetApp),
            _preferences.Permissions,
            _preferences.ComputerUseAutonomyLevel);

        var message = new StringBuilder();
        message.Append("Lo que pides: ").AppendLine(intent.Description);
        message.AppendLine();
        message.Append("Cómo lo haría: ").AppendLine(plan.Choice.Reason);

        foreach (var rejection in plan.Choice.Rejected)
        {
            message.Append("· Descarté ")
                .Append(ComputerUseMethodText.Describe(rejection.Method))
                .Append(": ").AppendLine(rejection.Reason);
        }

        message.Append("Vuelta atrás: ").AppendLine(plan.ReversalNote);

        if (plan.Blocker is not null)
        {
            message.AppendLine();
            message.Append("Ahora mismo no puedo hacerlo: ").AppendLine(plan.Blocker);
        }

        _assistantView.AddSakuraMessage(message.ToString().TrimEnd());

        RecordAudit(
            AuditCapability.Permisos,
            "Plan de acción propuesto",
            $"{intent.Description} — {plan.Choice.Reason}",
            "Solo propuesta: no se ejecutó nada",
            (int)plan.Level);

        return true;
    }

    /// <summary>
    /// Diseño D15 (Fase 8 — Skills Platform) — activar un pack deja configuradas de una vez varias
    /// capacidades que ya existen. Un pack **nunca concede un permiso**: si le falta alguno, lo dice
    /// y explica dónde se da.
    /// </summary>
    private IEnumerable<SakuraCommandDescriptor> BuildSkillPackCommands()
    {
        foreach (var pack in SkillPackCatalog.All)
        {
            var current = pack;

            yield return new SakuraCommandDescriptor(
                $"skills.{current.Id}".ToLowerInvariant(),
                $"Activar {current.Name}",
                current.Purpose,
                SakuraCommandCategory.System,
                _ =>
                {
                    ApplySkillPack(current);
                    return Task.FromResult(CommandExecutionResult.Success());
                },
                keywords: ["pack", "modo", "skills", current.Name.ToLowerInvariant()]);
        }

        yield return new SakuraCommandDescriptor(
            "skills.off",
            "Desactivar el pack activo",
            "Devuelve los ajustes a como estaban antes de activarlo.",
            SakuraCommandCategory.System,
            _ => Task.FromResult(RevertSkillPack()),
            keywords: ["pack", "desactivar", "quitar", "skills"],
            availability: () => _skillPackCoordinator.ActivePackId is not null
                ? SakuraCommandAvailability.Available
                : SakuraCommandAvailability.Unavailable("No hay ningún pack activo."));
    }

    private void ApplySkillPack(SkillPack pack)
    {
        var plan = _skillPackCoordinator.Preview(pack, _preferences);

        var message = new StringBuilder();
        message.AppendLine(pack.Purpose);

        if (plan.Changes.Count == 0)
        {
            message.AppendLine().Append("Ya está todo como lo dejaría este pack.");
        }
        else
        {
            message.AppendLine();
            message.AppendLine("Cambiaría estos ajustes:");
            foreach (var change in plan.Changes)
            {
                message.Append("· ").Append(change.Title)
                    .Append(" (ahora: ").Append(change.Current).AppendLine(")");
            }
        }

        // Lo que el pack NO puede hacer se enseña con el mismo peso que lo que sí: enseñar solo lo
        // que gana la persona sería vender el pack, no explicarlo.
        if (plan.UnmetRequirements.Count > 0)
        {
            message.AppendLine();
            message.AppendLine("Esto le haría falta y NO lo activo yo, lo decides tú:");
            foreach (var requirement in plan.UnmetRequirements)
            {
                message.Append("· ").Append(requirement.Title).Append(" — ")
                    .Append(requirement.WhyItMatters).Append(' ')
                    .AppendLine(requirement.HowToEnable);
            }
        }

        _assistantView.AddSakuraMessage(message.ToString().TrimEnd());
        NavigateTo("Assistant", animate: true);

        if (!plan.HasSomethingToDo)
        {
            return;
        }

        var confirmation = MessageBox.Show(
            this,
            $"Voy a cambiar {plan.Changes.Count} ajustes para dejar {pack.Name} listo." +
                Environment.NewLine + Environment.NewLine +
                "No activo ningún permiso: la memoria, Vision y la carpeta de proyecto siguen como " +
                "las tengas." + Environment.NewLine + Environment.NewLine +
                "Puedes desactivarlo cuando quieras y todo vuelve a como estaba. ¿Lo activo?",
            $"Activar {pack.Name}",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        var result = _skillPackCoordinator.Apply(pack, _preferences);
        AfterSkillPackChange(result, result.Success ? "Pack activado" : "No activé el pack");
    }

    private CommandExecutionResult RevertSkillPack()
    {
        var result = _skillPackCoordinator.Revert(_preferences);
        AfterSkillPackChange(result, result.Success ? "Pack desactivado" : "No había pack activo");

        return result.Success
            ? CommandExecutionResult.Success(result.Detail)
            : CommandExecutionResult.Failure(result.Detail);
    }

    /// <summary>
    /// Diseño D15 — un pack toca preferencias que ya tienen efectos vivos (el atajo de dictado, el
    /// plan de motores, los módulos visibles). Se reaplican todas en vez de una lista a mano: una
    /// lista se queda corta en cuanto un pack cambia un ajuste nuevo.
    /// </summary>
    private void RefreshSkillPackPanel() =>
        _settingsView.ApplySkillPacks(_skillPackCoordinator.ActivePackId, _preferences);

    private void AfterSkillPackChange(SkillPackResult result, string title)
    {
        SavePreferences();
        ApplyPreferences();
        _settingsView.ApplyPreferences(_preferences);
        ApplyFlowHotkeyRegistration();
        RefreshAdaptiveEnginePlan();
        RefreshAuditPanel();
        RefreshSkillPackPanel();

        _assistantView.AddSakuraMessage(result.Detail);
        ShowFlowNotice(result.Success ? CapsuleKind.Success : CapsuleKind.Warning, title, result.Detail);
    }

    // ---------- Diseño D14 (Fase 5, nivel 4 — "Ejecutar un paso") ----------

    private void RequestWorkspaceEdit()
    {
        _awaitingWorkspaceEditInstruction = true;
        _assistantView.AddSakuraMessage(
            "¿Qué quieres que cambie? Dímelo y te enseño el cambio antes de tocar nada.");
        NavigateTo("Assistant", animate: true);
    }

    /// <summary>
    /// Diseño D14 — la instrucción va a la IA con el proyecto como contexto y con el formato de
    /// cambio exigido. La respuesta se parsea; si no cumple el formato exacto se enseña como texto y
    /// no se escribe nada. Adivinar lo que el modelo quiso decir es como se acaba escribiendo en el
    /// archivo equivocado.
    /// </summary>
    private async Task<bool> TryHandleWorkspaceEditInstructionAsync(string prompt, bool fromVoice)
    {
        if (!_awaitingWorkspaceEditInstruction)
        {
            return false;
        }

        _awaitingWorkspaceEditInstruction = false;

        var workspace = _preferences.Workspace;
        if (IsVoiceCancellation(SpanishVoiceTranscriptNormalizer.Normalize(prompt)))
        {
            _assistantView.AddSakuraMessage("De acuerdo, no cambio nada.");
            return true;
        }

        // Se vuelve a comprobar aquí: el permiso pudo revocarse o bajarse de nivel entre el comando
        // y la respuesta, y eso tiene que surtir efecto en el acto.
        if (!workspace.HasAuthorizedFolder ||
            !WorkspaceAutonomyPolicy.CanWrite(workspace.AutonomyLevel))
        {
            _assistantView.AddSakuraMessage(
                "Ya no tengo permiso para modificar ese proyecto, así que no cambié nada.");
            return true;
        }

        var files = _workspaceReader.ListFiles(workspace.AuthorizedPath, maximumFiles: 500);
        var structure = WorkspaceContextBuilder.BuildStructure(
            workspace.AuthorizedPath, files, workspace.AutonomyLevel, editRequested: true);

        // Corrección de un defecto real: antes de esto, el modelo nunca veía el contenido ACTUAL
        // del archivo que se le pedía cambiar — solo su nombre en la lista. Como KOHANA-EDIT exige
        // el archivo COMPLETO, sin el original solo podía inventarlo, y eso es justo lo que pasó al
        // probarlo a mano: "agrega un comentario al README" sustituyó el archivo entero por una
        // línea. Aquí se resuelve por NOMBRE (nunca por IA) a qué archivos existentes se refiere la
        // instrucción, y se lee su contenido de verdad para incluirlo.
        var resolvedTargets = WorkspaceEditTargetResolver.Resolve(prompt, files);
        var knownContentPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var successfullyRead = new List<WorkspaceFile>();

        if (resolvedTargets.Count > 0)
        {
            var reads = new List<WorkspaceReadResult>();

            foreach (var target in resolvedTargets)
            {
                var read = _workspaceReader.ReadFile(workspace.AuthorizedPath, target.FullPath);
                if (!read.Success)
                {
                    continue;
                }

                reads.Add(read);
                successfullyRead.Add(target);

                // Se guarda como RUTA RESUELTA, no como el texto que produjo Path.GetRelativePath.
                // La plantilla que ve el modelo (WorkspaceEditParser.ModelInstructions) usa "/"
                // como separador de ejemplo ("ruta/relativa/del/archivo"), y Path.GetRelativePath
                // en Windows produce "\". Comparar las cadenas tal cual habría rechazado por error
                // cualquier archivo dentro de una subcarpeta — que es la mayoría de un proyecto
                // real — por un choque de formato, no por falta de contenido.
                knownContentPaths.Add(WorkspacePathPolicy.ResolveFullPath(workspace.AuthorizedPath, target.RelativePath));
            }

            var excerpts = WorkspaceContextBuilder.BuildFileExcerpts(reads);
            if (!string.IsNullOrWhiteSpace(excerpts))
            {
                structure = string.IsNullOrWhiteSpace(structure)
                    ? excerpts
                    : structure + Environment.NewLine + excerpts;

                structure += Environment.NewLine +
                    WorkspaceContextBuilder.EditPreservationNotice(successfullyRead);
            }
        }

        // La salvaguarda de verdad no es esta lista — es que OfferWorkspaceEdit se niegue a
        // aplicar un cambio sobre un archivo existente cuyo contenido no viajó en este contexto.
        // Esto solo decide qué archivos SÍ pudieron incluirse.
        _pendingWorkspaceEditKnownContentPaths = knownContentPaths;

        _pendingWorkspaceContext = string.IsNullOrWhiteSpace(structure)
            ? WorkspaceEditParser.ModelInstructions
            : structure + Environment.NewLine + WorkspaceEditParser.ModelInstructions;

        _pendingWorkspaceEditRequested = true;
        await SendPromptToAiAsync(prompt, fromVoice);
        return true;
    }

    /// <summary>
    /// Diseño D14 — vista previa y confirmación. La confirmación dice el archivo, si existe o se
    /// crearía, y cuánto ocupa lo nuevo: aceptar un cambio sin saber sobre qué archivo cae es
    /// aceptar a ciegas.
    /// </summary>
    /// <summary>
    /// Diseño D24 — con el nivel 5, una respuesta puede traer varios cambios y se ejecutan como
    /// secuencia: parada al primer fallo, confirmación en los puntos de riesgo y reversión de lo ya
    /// aplicado. Por debajo del nivel 5 se sigue exigiendo exactamente uno.
    /// </summary>
    private void OfferWorkspaceEdit(string answer)
    {
        // Un solo turno: los archivos cuyo contenido viajó en ESTE contexto no valen para el
        // siguiente. Se captura y se limpia aquí, antes de cualquier retorno anticipado.
        var knownContentPaths = _pendingWorkspaceEditKnownContentPaths;
        _pendingWorkspaceEditKnownContentPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (_preferences.Workspace.AutonomyLevel == AutonomyLevel.ColaborarConConfirmaciones)
        {
            var sequence = WorkspaceEditParser.ParseSequence(answer);
            if (sequence.Count > 1)
            {
                RunWorkspaceEditSequence(sequence, knownContentPaths);
                return;
            }
        }

        var edit = WorkspaceEditParser.Parse(answer);
        if (edit is null)
        {
            return;
        }

        var workspace = _preferences.Workspace;

        // Diseño D16 — el broker decide primero si la capacidad puede actuar siquiera. Solo se mira
        // la denegación: la confirmación de este camino es el diálogo de abajo, que dice qué archivo
        // y por qué, y preguntar dos veces enseñaría a aceptar sin leer.
        var permission = PermissionBroker.Decide(
            new PermissionRequest(SakuraCapability.Proyecto, edit.Description),
            _preferences.Permissions);

        if (permission.IsDenied)
        {
            _assistantView.AddSakuraMessage(permission.Reason);
            return;
        }

        var verdict = _workspaceEditCoordinator.CanApply(edit, workspace);
        if (!verdict.IsAllowed)
        {
            _assistantView.AddSakuraMessage($"No puedo aplicar ese cambio: {verdict.Message}");
            return;
        }

        // El modelo puede anteponer el nombre de la propia carpeta autorizada (ver
        // WorkspacePathPolicy.NormalizeRelativePath). Se normaliza ANTES de decidir si el archivo
        // existe: de esa decisión cuelgan tanto el texto de la confirmación como la salvaguarda de
        // "no toco un archivo cuyo contenido no vi".
        edit = edit with
        {
            RelativePath = WorkspacePathPolicy.NormalizeRelativePath(
                workspace.AuthorizedPath, edit.RelativePath)
        };

        var fullPath = WorkspacePathPolicy.ResolveFullPath(workspace.AuthorizedPath, edit.RelativePath);
        var exists = File.Exists(fullPath);

        // La salvaguarda de verdad contra el defecto real que esto corrige: un archivo que YA
        // EXISTÍA y cuyo contenido no viajó en el contexto de este turno no se aplica, sin importar
        // qué tan razonable parezca el cambio propuesto. Confiar en que el modelo conservó un
        // archivo que nunca vio es exactamente la apuesta que perdió al probarlo a mano.
        //
        // La comparación es por ruta RESUELTA (Path.GetFullPath), no por el texto crudo que
        // escribió el modelo: la plantilla del formato usa "/" de ejemplo y el modelo puede
        // devolver cualquier estilo de separador, así que comparar cadenas literales habría
        // rechazado por error archivos cuyo contenido sí se había leído.
        if (exists && !knownContentPaths.Contains(WorkspacePathPolicy.ResolveFullPath(workspace.AuthorizedPath, edit.RelativePath)))
        {
            _assistantView.AddSakuraMessage(
                $"No apliqué el cambio a «{edit.RelativePath}»: no tenía el contenido actual de " +
                "ese archivo en esta consulta, así que no puedo garantizar que lo conservara. " +
                "Pídemelo otra vez nombrando el archivo tal cual (por ejemplo «modifica " +
                $"{edit.RelativePath}») para que lo lea antes de proponer el cambio.");
            return;
        }

        // Lo que de verdad hay que poder ver antes de decir que sí no es el tamaño, sino qué se
        // pierde: el modelo puede leer el archivo entero y aun así devolverlo sin una línea.
        var removalWarning = DescribeWorkspaceEditRemovals(exists ? fullPath : null, edit.NewContent);

        var confirmation = MessageBox.Show(
            this,
            $"{(exists ? "Voy a REEMPLAZAR" : "Voy a CREAR")} este archivo:{Environment.NewLine}" +
                $"{edit.RelativePath}{Environment.NewLine}{Environment.NewLine}" +
                $"Motivo: {edit.Description}{Environment.NewLine}" +
                $"Tamaño del contenido nuevo: {edit.NewContent.Length} caracteres." +
                (removalWarning is null
                    ? string.Empty
                    : Environment.NewLine + Environment.NewLine + removalWarning) +
                $"{Environment.NewLine}{Environment.NewLine}" +
                "Guardaré una copia previa para que puedas deshacerlo. ¿Lo aplico?",
            "Aplicar un cambio en el proyecto",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
        {
            _assistantView.AddSakuraMessage("No apliqué el cambio.");
            return;
        }

        var result = _workspaceEditCoordinator.Apply(edit, workspace);

        _assistantView.AddSakuraMessage(result.Detail);
        ShowFlowNotice(
            result.Success ? CapsuleKind.Success : CapsuleKind.Warning,
            result.Success ? "Cambio aplicado" : "No apliqué el cambio",
            result.Detail);

        RefreshAuditPanel();
    }

    /// <summary>
    /// Diseño D24 — arma la secuencia y la ejecuta. **Todo cambio de archivo es punto de riesgo**:
    /// tocar el trabajo de alguien lo es por definición, y marcar unos sí y otros no exigiría un
    /// criterio que Sakura no tiene. Que el nivel 5 pregunte en cada archivo no lo vuelve inútil —
    /// sigue ahorrando redactar y encadenar los cambios uno a uno.
    /// </summary>
    /// <summary>
    /// Lee el archivo actual y describe qué líneas desaparecerían al aplicar el contenido propuesto.
    /// Devuelve <c>null</c> si no hay archivo previo o si no se pierde nada. Si el archivo no se
    /// puede leer, tampoco se inventa un aviso: se avisa de que no se pudo comprobar, porque callar
    /// aquí se interpretaría como "no se pierde nada".
    /// </summary>
    private static string? DescribeWorkspaceEditRemovals(string? currentFullPath, string newContent)
    {
        if (string.IsNullOrEmpty(currentFullPath))
        {
            return null;
        }

        try
        {
            var current = File.ReadAllText(currentFullPath);
            return WorkspaceEditImpactAnalyzer.DescribeRemovals(
                WorkspaceEditImpactAnalyzer.Compare(current, newContent));
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return "No pude leer el archivo actual para comprobar qué se pierde con este cambio.";
        }
    }

    private void RunWorkspaceEditSequence(
        IReadOnlyList<WorkspaceEdit> edits,
        HashSet<string> knownContentPaths)
    {
        var workspace = _preferences.Workspace;
        var checkpoints = new List<Guid>();

        // Misma normalización que en el cambio suelto, y por el mismo motivo: si el prefijo sobrante
        // llega hasta aquí, File.Exists da false y la salvaguarda de abajo se salta sola.
        edits = [.. edits.Select(edit => edit with
        {
            RelativePath = WorkspacePathPolicy.NormalizeRelativePath(
                workspace.AuthorizedPath, edit.RelativePath)
        })];

        var steps = edits.Select(edit => new SequenceStep(
            edit.RelativePath,
            $"{edit.RelativePath} — {edit.Description}",
            IsRiskPoint: true,
            CanRevert: true,
            Execute: () =>
            {
                // Misma salvaguarda que en el cambio suelto: un archivo existente cuyo contenido no
                // viajó en el contexto de este turno no se toca, ni siquiera dentro de una secuencia
                // ya confirmada. Que falle AQUÍ hace que el coordinador de secuencias lo trate como
                // cualquier otro paso fallido: se detiene y se ofrece deshacer lo anterior.
                var fullPath = WorkspacePathPolicy.ResolveFullPath(
                    workspace.AuthorizedPath, edit.RelativePath);
                if (File.Exists(fullPath) &&
                    !knownContentPaths.Contains(WorkspacePathPolicy.ResolveFullPath(workspace.AuthorizedPath, edit.RelativePath)))
                {
                    return SequenceStepResult.Failed(
                        "No tenía el contenido actual de ese archivo en esta consulta, así que no " +
                        "puedo garantizar que lo conservara.");
                }

                var result = _workspaceEditCoordinator.Apply(edit, workspace);
                if (result.Success)
                {
                    checkpoints.Add(result.CheckpointId);
                }

                return result.Success
                    ? SequenceStepResult.Ok(result.Detail)
                    : SequenceStepResult.Failed(result.Detail);
            },
            Revert: () =>
            {
                var last = checkpoints.LastOrDefault();
                if (last == Guid.Empty)
                {
                    return SequenceStepResult.Failed("No tengo copia previa de ese paso.");
                }

                checkpoints.Remove(last);
                var reverted = _workspaceEditCoordinator.Revert(last, workspace);

                return reverted.Success
                    ? SequenceStepResult.Ok(reverted.Detail)
                    : SequenceStepResult.Failed(reverted.Detail);
            })).ToArray();

        var plan = new SequencePlan($"{edits.Count} cambios en el proyecto", steps);

        var start = MessageBox.Show(
            this,
            $"Sakura propone {edits.Count} cambios:" + Environment.NewLine +
                string.Join(Environment.NewLine, edits.Select(edit => $"· {edit.RelativePath}")) +
                Environment.NewLine + Environment.NewLine + plan.ReversalWarning +
                Environment.NewLine + Environment.NewLine +
                "Te preguntaré antes de cada archivo. ¿Empiezo?",
            "Aplicar varios cambios",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (start != MessageBoxResult.Yes)
        {
            _assistantView.AddSakuraMessage("No apliqué ningún cambio.");
            return;
        }

        var report = _sequenceCoordinator.Run(
            plan,
            workspace.AutonomyLevel,
            confirmRiskPoint: step =>
            {
                // Cada paso enseña lo que se pierde, igual que el cambio suelto. Aquí importa más:
                // en una secuencia se confirma varias veces seguidas y es donde más fácil es decir
                // que sí por inercia.
                var stepEdit = edits.FirstOrDefault(item => item.RelativePath == step.Id);
                var stepPath = stepEdit is null
                    ? null
                    : WorkspacePathPolicy.ResolveFullPath(workspace.AuthorizedPath, stepEdit.RelativePath);
                var stepWarning = stepEdit is not null && File.Exists(stepPath!)
                    ? DescribeWorkspaceEditRemovals(stepPath, stepEdit.NewContent)
                    : null;

                return MessageBox.Show(
                    this,
                    step.Title +
                        (stepWarning is null
                            ? string.Empty
                            : Environment.NewLine + Environment.NewLine + stepWarning) +
                        $"{Environment.NewLine}{Environment.NewLine}¿Aplico este?",
                    "Siguiente cambio",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) == MessageBoxResult.Yes;
            },
            confirmRollback: applied => MessageBox.Show(
                this,
                $"Algo falló y hay {applied.Count} cambios ya aplicados." + Environment.NewLine +
                    Environment.NewLine + "¿Los deshago y lo dejo como estaba?",
                "Deshacer lo aplicado",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) == MessageBoxResult.Yes);

        var message = new StringBuilder();
        message.AppendLine(report.Detail);
        foreach (var line in report.Log)
        {
            message.Append("· ").AppendLine(line);
        }

        _assistantView.AddSakuraMessage(message.ToString().TrimEnd());
        RefreshAuditPanel();

        ShowFlowNotice(
            report.Outcome == SequenceOutcome.Completada ? CapsuleKind.Success : CapsuleKind.Warning,
            report.Outcome == SequenceOutcome.Completada ? "Cambios aplicados" : "Secuencia detenida",
            report.Detail);
    }

    private CommandExecutionResult UndoLastWorkspaceEdit()
    {
        var result = _workspaceEditCoordinator.RevertLast(_preferences.Workspace);

        _assistantView.AddSakuraMessage(result.Detail);
        RefreshAuditPanel();

        if (!result.Success)
        {
            return CommandExecutionResult.Failure(result.Detail);
        }

        ShowFlowNotice(CapsuleKind.Success, "Cambio deshecho", result.Detail);
        return CommandExecutionResult.Success(result.Detail);
    }

    /// <summary>
    /// Diseño D41 — el recuento de archivos se hace fuera del hilo de interfaz.
    ///
    /// Antes se listaba aquí mismo, y recorrer una carpeta grande deja la ventana congelada hasta
    /// que termina: Windows llega a ofrecer cerrar el programa. Pasó de verdad. El recorrido ya está
    /// acotado en el lector, pero incluso acotado es trabajo de disco, y el disco no tiene por qué
    /// responder rápido.
    /// </summary>
    private async void ShowWorkspaceStatus()
    {
        var workspace = _preferences.Workspace;

        if (!workspace.HasAuthorizedFolder)
        {
            _assistantView.AddSakuraMessage(
                "No tengo ninguna carpeta de proyecto autorizada. Puedes autorizar una desde la " +
                "paleta de comandos, y quitármela igual de rápido.");
            NavigateTo("Assistant", animate: true);
            return;
        }

        var message = new StringBuilder();
        message.AppendLine($"Carpeta autorizada: {workspace.AuthorizedPath}");
        message.AppendLine(
            $"Autorizada el {workspace.AuthorizedAt?.ToString("dd/MM/yyyy HH:mm") ?? "—"}.");
        message.AppendLine(WorkspaceAutonomyPolicy.Describe(workspace.AutonomyLevel));
        message.AppendLine(
            "Solo lectura. No leo .env, claves privadas ni carpetas de dependencias, y oculto " +
            "los valores que parecen secretos antes de enviar nada.");
        message.Append("Contando archivos legibles…");

        _assistantView.AddSakuraMessage(message.ToString());
        NavigateTo("Assistant", animate: true);

        var authorizedPath = workspace.AuthorizedPath;
        var count = await Task.Run(
            () => _workspaceReader.ListFiles(authorizedPath, maximumFiles: 500).Count);

        if (_isClosed)
        {
            return;
        }

        _assistantView.AddSakuraMessage($"Archivos legibles ahora mismo: {count}.");
    }

    private CommandExecutionResult RevokeWorkspace()
    {
        if (!_preferences.Workspace.HasAuthorizedFolder)
        {
            return CommandExecutionResult.Failure("No hay ninguna carpeta autorizada.");
        }

        var revoked = _preferences.Workspace.AuthorizedPath;
        _preferences.Workspace.Revoke();
        _preferences.Workspace.Normalize();
        SavePreferences();

        RecordAudit(
            AuditCapability.Permisos,
            "Acceso al proyecto revocado",
            revoked,
            "Revocación explícita del usuario");

        ShowFlowNotice(
            CapsuleKind.Success,
            "Acceso revocado",
            "Ya no puedo leer esa carpeta.");
        _settingsView.ApplyWorkspaceSettings(_preferences.Workspace);
        return CommandExecutionResult.Success("Ya no puedo leer esa carpeta.");
    }

    /// <summary>
    /// Diseño D12 — explica el proyecto usando la estructura, no el código entero. Mandar un
    /// proyecto completo a un proveedor remoto para que diga de qué va es desproporcionado; con el
    /// árbol de archivos se explica casi igual de bien y sale del equipo una fracción de lo mismo.
    /// </summary>
    private async Task<CommandExecutionResult> ExplainWorkspaceAsync()
    {
        var workspace = _preferences.Workspace;
        if (!workspace.HasAuthorizedFolder)
        {
            return CommandExecutionResult.Failure("Todavía no autorizaste ninguna carpeta de proyecto.");
        }

        var files = _workspaceReader.ListFiles(workspace.AuthorizedPath, maximumFiles: 500);
        if (files.Count == 0)
        {
            return CommandExecutionResult.Failure(
                "No encontré archivos legibles en esa carpeta. Puede que sea todo binarios o dependencias.");
        }

        _pendingWorkspaceContext = WorkspaceContextBuilder.BuildStructure(
            workspace.AuthorizedPath, files, workspace.AutonomyLevel);

        await ProcessPromptAsync(
            "Explícame de qué trata este proyecto y por dónde empezaría a leerlo.",
            fromVoice: false);

        return CommandExecutionResult.Success();
    }

    private IEnumerable<SakuraCommandDescriptor> BuildFocusStartCommands()
    {
        (int Minutes, string Id)[] presets =
        [
            (15, "focus.start.15"),
            (25, "focus.start.25"),
            (45, "focus.start.45")
        ];

        foreach (var (minutes, id) in presets)
        {
            yield return new SakuraCommandDescriptor(
                id,
                $"Iniciar enfoque · {minutes} min",
                $"Comienza una sesión de enfoque de {minutes} minutos.",
                SakuraCommandCategory.Focus,
                _ =>
                {
                    _focusView.StartPreset(TimeSpan.FromMinutes(minutes));
                    CheckFocusTimer();
                    return Task.FromResult(CommandExecutionResult.Success());
                },
                keywords: ["enfoque", "iniciar", "concentración", "pomodoro", minutes.ToString(CultureInfo.InvariantCulture)],
                availability: () => _focusManager.GetSnapshot(DateTimeOffset.Now).ActiveTimer is null
                    ? SakuraCommandAvailability.Available
                    : SakuraCommandAvailability.Unavailable("Ya hay una sesión de enfoque en curso."));
        }
    }

    /// <summary>
    /// Diseño D3 — un comando por cada rutina, no un único comando genérico "ejecutar rutina": así
    /// cada una aparece por su propio nombre en la búsqueda y su disponibilidad refleja si esa
    /// rutina en concreto está habilitada. El registro se reconstruye cada vez que se abre el
    /// Command Center (ver ShowCommandCenter), así que esta lista nunca queda desactualizada.
    /// </summary>
    private IEnumerable<SakuraCommandDescriptor> BuildRoutineExecutionCommands()
    {
        foreach (var routine in _routineManager.GetAll())
        {
            var routineId = routine.Id;
            var routineName = routine.Name;
            yield return new SakuraCommandDescriptor(
                $"routine.execute.{routineId}",
                $"Ejecutar {routineName}",
                $"Ejecuta la rutina «{routineName}».",
                SakuraCommandCategory.System,
                async _ =>
                {
                    var current = _routineManager.GetAll().FirstOrDefault(candidate => candidate.Id == routineId);
                    if (current is null)
                    {
                        return CommandExecutionResult.Failure($"La rutina «{routineName}» ya no existe.");
                    }

                    await RunRoutineAsync(current);
                    return CommandExecutionResult.Success();
                },
                keywords: ["rutina", "routine", "ejecutar", routineName],
                availability: () =>
                {
                    var current = _routineManager.GetAll().FirstOrDefault(candidate => candidate.Id == routineId);
                    if (current is null)
                    {
                        return SakuraCommandAvailability.Unavailable("Esta rutina ya no existe.");
                    }

                    return current.IsEnabled
                        ? SakuraCommandAvailability.Available
                        : SakuraCommandAvailability.Unavailable($"La rutina «{routineName}» está desactivada.");
                });
        }
    }

    /// <summary>
    /// Diseño D3 — misma lógica que <c>FocusView.FinishButton_Click</c>, para el comando
    /// "focus.finish" del Command Center y para el mini temporizador global. Diseño D3.1:
    /// FocusOperationResult.Completion ya trae la tarea asociada, así que no hace falta leer el
    /// timer por separado antes de llamar a Finish().
    /// </summary>
    private CommandExecutionResult FinishActiveFocusSession()
    {
        var now = DateTimeOffset.Now;
        var result = _focusManager.Finish(now);
        _focusView.Refresh(now);
        RefreshHomeView();

        if (!result.Success)
        {
            return CommandExecutionResult.Failure(result.Message);
        }

        if (result.Completion is { } completion)
        {
            _focusView.ShowSessionCompletionNotice(completion);
        }

        return CommandExecutionResult.Success(result.Message);
    }

    private CommandExecutionResult ApplyMasterMute(bool muted)
    {
        var result = _audioMixerService.SetMasterMuted(muted);
        if (!result.Succeeded)
        {
            return CommandExecutionResult.Failure(result.Detail);
        }

        // Refresca la vista de Audio para que el estado mostrado no quede desfasado respecto al
        // cambio que se acaba de aplicar. La Task se observa dentro de RefreshAsync.
        _ = _audioView.RefreshAsync(force: true);
        return CommandExecutionResult.Success(result.Title);
    }

    private IEnumerable<SakuraCommandDescriptor> BuildNavigationCommands()
    {
        // Un comando por destino conocido: la lista sale de ShellNavigationPolicy, así que no
        // puede desincronizarse de la navegación real del shell.
        (string Destination, string Title, string[] Keywords)[] destinations =
        [
            (ShellNavigationPolicy.Home, "Ir a Inicio", ["inicio", "home", "principal"]),
            (ShellNavigationPolicy.Assistant, "Ir a Asistente", ["asistente", "chat", "conversación"]),
            (ShellNavigationPolicy.Tasks, "Ir a Hoy", ["hoy", "tareas", "pendientes"]),
            (ShellNavigationPolicy.Focus, "Ir a Enfoque", ["enfoque", "concentración"]),
            (ShellNavigationPolicy.Routines, "Ir a Rutinas", ["rutinas", "automatización"]),
            (ShellNavigationPolicy.Audio, "Ir a Audio", ["audio", "volumen", "sonido"]),
            (ShellNavigationPolicy.Capture, "Ir a Captura", ["captura", "pantalla", "screenshot"]),
            (ShellNavigationPolicy.System, "Ir a Sistema", ["sistema", "estado", "diagnóstico", "hardware"]),
            (ShellNavigationPolicy.Settings, "Ir a Personalizar", ["personalizar", "configuración", "ajustes", "preferencias"])
        ];

        foreach (var (destination, title, keywords) in destinations)
        {
            var target = destination;
            yield return new SakuraCommandDescriptor(
                $"navigate.{target.ToLowerInvariant()}",
                title,
                "Abre esta sección de Sakura.",
                SakuraCommandCategory.Navigation,
                _ =>
                {
                    NavigateTo(target, animate: _preferences.AnimationsEnabled);
                    return Task.FromResult(CommandExecutionResult.Success());
                },
                keywords: keywords);
        }
    }

    private async void CommandPaletteWindow_PromptSubmitted(
        object? sender,
        CommandPalettePromptEventArgs e)
    {
        _promptFromCommandPalette = true;
        try
        {
            await ProcessPromptAsync(e.Prompt, fromVoice: false);
        }
        finally
        {
            _promptFromCommandPalette = false;
        }
    }

    private void CommandPaletteWindow_WorkspaceRequested(object? sender, EventArgs e)
    {
        ShowAnimated();
        NavigateTo("Assistant", animate: true);
    }

    private void HomeView_CommandRequested(object? sender, EventArgs e)
    {
        ShowCommandPalette();
    }

    private void HomeView_TasksRequested(object? sender, EventArgs e)
    {
        NavigateTo("Tasks", animate: true);
    }

    private void HomeView_FocusRequested(object? sender, EventArgs e)
    {
        NavigateTo("Focus", animate: true);
    }

    private void HomeView_RoutinesRequested(object? sender, EventArgs e)
    {
        NavigateTo(ShellNavigationPolicy.Routines, animate: true);
    }

    private void HomeView_NewTaskRequested(object? sender, EventArgs e)
    {
        NavigateTo(ShellNavigationPolicy.Tasks, animate: true);
        _tasksView.OpenNewEditor();
    }

    private void HomeView_StartFocusRequested(object? sender, EventArgs e)
    {
        NavigateTo(ShellNavigationPolicy.Focus, animate: true);
        _focusView.FocusPrimaryControl();
    }

    /// <summary>
    /// Diseño D3 — "Enfocarme" desde una tarea en Hoy. Prepara la asociación en FocusView (la
    /// próxima sesión que se inicie ahí quedará asociada) y navega; no inicia la sesión por sí
    /// solo, para que la persona elija la duración como con cualquier otra sesión.
    /// </summary>
    private void TasksView_FocusRequested(object? sender, TaskFocusRequestedEventArgs e)
    {
        _focusView.PrepareTaskAssociation(e.TaskId, e.TaskTitle);
        NavigateTo(ShellNavigationPolicy.Focus, animate: true);
        _focusView.FocusPrimaryControl();
    }

    /// <summary>
    /// Diseño D3 — el usuario confirmó explícitamente que quiere marcar como completada la tarea
    /// asociada a una sesión de enfoque que acaba de terminar. Nunca ocurre automáticamente.
    /// </summary>
    private void FocusView_CompleteAssociatedTaskRequested(object? sender, TaskFocusRequestedEventArgs e)
    {
        _taskManager.Complete(e.TaskId);
        _tasksView.Refresh();
        RefreshHomeView();
    }

    private async void HomeView_ContextRequested(object? sender, EventArgs e)
    {
        await LookAtForegroundWindowAsync();
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        var windowHandle = new WindowInteropHelper(this).Handle;
        _windowSource = HwndSource.FromHwnd(windowHandle);
        _windowSource?.AddHook(WindowMessageHook);

        ApplySystemBackdrop(windowHandle);

        if (!RegisterHotKey(windowHandle, ShellHotkeyId, ModAlt, VirtualKeyA))
        {
            _assistantView.AddSakuraMessage("Alt + A ya está siendo utilizado por otra aplicación.");
        }

        if (!RegisterHotKey(windowHandle, PeekHotkeyId, ModAlt | ModShift, VirtualKeyA))
        {
            _assistantView.AddSakuraMessage("Alt + Shift + A ya está siendo utilizado por otra aplicación.");
        }

        if (!RegisterHotKey(
                windowHandle,
                CommandPaletteHotkeyId,
                ModControl,
                VirtualKeySpace))
        {
            _assistantView.AddSakuraMessage(
                "Ctrl + Espacio ya está siendo utilizado por otra aplicación.");
        }

        if (!RegisterHotKey(
                windowHandle,
                LookHotkeyId,
                ModControl | ModShift,
                VirtualKeySpace))
        {
            _assistantView.AddSakuraMessage(
                "Ctrl + Shift + Espacio ya está siendo utilizado por otra aplicación.");
        }

        // Diseño D6.3 — el dictado global se registra igual que los demás atajos: como atajo de
        // sistema, para que funcione con Sakura sin foco (que es todo el sentido de dictar en otra
        // aplicación).
        if (_preferences.FlowEnabled &&
            !RegisterHotKey(windowHandle, FlowHotkeyId, ModControl | ModShift, VirtualKeyD))
        {
            _assistantView.AddSakuraMessage(
                "Ctrl + Shift + D ya está siendo utilizado por otra aplicación; el dictado global no quedó disponible.");
        }
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        PositionWindow();
        UpdateClock();
        _clockTimer.Start();
        _metricsTimer.Start();
        _taskReminderTimer.Start();
        _focusTickTimer.Start();
        CheckTaskReminders();
        CheckFocusTimer();

        if (_startHidden)
        {
            Hide();
            SetMetricsCadence(isShellVisible: false);
        }
        else
        {
            SetMetricsCadence(isShellVisible: true);
            ShowAnimated();
        }

        ConfigureManagedOllamaSupervisor();
        await RefreshMetricsAsync();
        _ = InitializeVoiceFeaturesAsync();
        _ = CheckForUpdateInBackgroundAsync();
    }

    /// <summary>
    /// Diseño D68 — mira si hay versión nueva sin que nadie lo pida, y como mucho una vez al día.
    ///
    /// **Mira, pero no instala.** Descargar cien megas y sustituir la aplicación son decisiones de la
    /// persona; lo único que se hace solo es enterarse, que no cuesta nada y evita que haya que
    /// acordarse de comprobar.
    ///
    /// Se espera un poco antes de empezar. Al arrancar, Sakura está levantando la voz, las métricas
    /// y el runtime de IA; meter ahí una petición de red solo hace el arranque más lento a cambio de
    /// una respuesta que no corre ninguna prisa.
    /// </summary>
    private async Task CheckForUpdateInBackgroundAsync()
    {
        if (!UpdateCheckPolicy.ShouldCheck(
                _preferences.LastUpdateCheckAt,
                DateTimeOffset.UtcNow,
                _preferences.AutomaticUpdateCheckEnabled))
        {
            return;
        }

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(45), _lifetimeCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (_isClosed)
        {
            return;
        }

        SakuraVersion? skipped =
            SakuraVersion.TryParse(_preferences.SkippedUpdateVersion, out var parsed)
                ? parsed
                : null;

        var lookup = await _updateService.LookForUpdateAsync(
            CurrentVersion, skipped, _lifetimeCancellation.Token);

        // La fecha se apunta haya salido lo que haya salido. Si no, un equipo sin conexión
        // reintentaría en cada arranque, que es justo lo que este límite viene a evitar.
        _preferences.LastUpdateCheckAt = DateTimeOffset.UtcNow;
        SavePreferences();

        if (!lookup.Found || _isClosed)
        {
            return;
        }

        _offeredUpdate = lookup.Manifest;
        _settingsView.SetUpdateStatus("Hay una versión más nueva disponible.", busy: false);
        _settingsView.ShowUpdateOffer(
            lookup.Manifest!.Version.ToString(),
            lookup.Manifest.Notes,
            Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory));

        // El aviso no se enseña encima de un juego ni de una presentación: una versión nueva no es
        // urgente, y la tarjeta ya queda esperando en Personalizar de todas formas. La cápsula lo
        // respeta sola, pero decirlo aquí evita que mañana alguien la haga «forzada» sin pensar.
        _capsuleWindow.ShowMessage(
            CapsuleKind.Information,
            $"Sakura {lookup.Manifest.Version} disponible",
            "Puedes instalarla desde Personalizar › Actualizaciones.",
            _preferences.Position);
    }

    private void OnSystemUserPreferenceChanged(
        object sender,
        Microsoft.Win32.UserPreferenceChangedEventArgs e)
    {
        if (_isClosed || _preferences.AccentSource == AccentSource.Manual)
        {
            return;
        }

        // Desktop es la categoría con la que Windows avisa de un cambio de fondo de escritorio;
        // Color, la del acento del sistema. General llega con cambios de tema completos, que
        // pueden mover cualquiera de los dos.
        var relevant = e.Category is Microsoft.Win32.UserPreferenceCategory.Color
            or Microsoft.Win32.UserPreferenceCategory.Desktop
            or Microsoft.Win32.UserPreferenceCategory.General;

        if (relevant)
        {
            Dispatcher.BeginInvoke(ApplyEffectiveAccent);
        }
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _isClosed = true;
        Microsoft.Win32.SystemEvents.UserPreferenceChanged -= OnSystemUserPreferenceChanged;
        _clockTimer.Stop();
        _metricsTimer.Stop();
        _taskReminderTimer.Stop();
        _focusTickTimer.Stop();
        _visualContextExpiryTimer.Stop();
        _ambientStallWatch.Stop();
        _peekWindow.HideImmediately();
        _capsuleWindow.HideImmediately();
        _answerPillWindow.HideImmediately();
        _quickControlsWindow.HideImmediately();
        _dashboardWindow.PrepareForShutdown();
        _dashboardWindow.HideImmediately();
        _sakuraPillWindow.Hide();
        _voiceHaloWindow.HideImmediately();

        if (_preferences.SaveConversationHistory)
        {
            _conversationStore.Save(_assistantView.GetConversationSnapshot());
        }

        _wakeWordTestActive = false;
        _wakeWordTestCancellation?.Cancel();
        _wakeWordTestCancellation?.Dispose();
        _wakeWordTestCancellation = null;
        _lifetimeCancellation.Cancel();
        _capsuleWindow.Close();
        _sakuraPillWindow.Close();
        _ambientHistoryWindow?.Close();
        _lensHighlightOverlay.Close();
        _ambientForegroundTracker.Dispose();
        _edgeRevealWatcher.RevealRequested -= HandleEdgeRevealRequested;
        _edgeRevealWatcher.Dispose();
        _topRevealWatcher.RevealRequested -= HandleTopRevealRequested;
        _topRevealWatcher.Dispose();
        _dashboardWindow.Close();
        _quickControlsWatcher.RevealRequested -= HandleQuickControlsRequested;
        _quickControlsWatcher.Dispose();
        _brightnessService.Dispose();
        _commandPaletteWindow.Close();
        // MainWindow desuscribe los eventos de wake word (a través del coordinador) y cancela
        // el token de vida, pero NO libera los tres motores de voz: su propiedad y Dispose
        // viven en SakuraCompositionRoot y se ejecutan en App.OnExit, justo después de que
        // esta ventana se cierre. La guardia _isClosed y el token cancelado impiden que
        // cualquier operación en vuelo o evento encolado opere durante el cierre; el Dispose
        // de cada motor detiene su grabador.
        _voiceCoordinator.WakeWordDetected -= WakeWordService_WakeWordDetected;
        _voiceCoordinator.RecognitionObserved -= WakeWordService_RecognitionObserved;
        if (_aiChatService is IDisposable disposableAiService)
        {
            disposableAiService.Dispose();
        }
        _trayIcon.Dispose();
        _lifetimeCancellation.Dispose();

        var windowHandle = new WindowInteropHelper(this).Handle;
        if (windowHandle != IntPtr.Zero)
        {
            UnregisterHotKey(windowHandle, ShellHotkeyId);
            UnregisterHotKey(windowHandle, PeekHotkeyId);
            UnregisterHotKey(windowHandle, CommandPaletteHotkeyId);
            UnregisterHotKey(windowHandle, LookHotkeyId);
            UnregisterHotKey(windowHandle, FlowHotkeyId);
            UnregisterHotKey(windowHandle, EscapeHotkeyId);
        }

        _windowSource?.RemoveHook(WindowMessageHook);
    }

    private IntPtr WindowMessageHook(
        IntPtr hwnd,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (message == WmPowerBroadcast)
        {
            var powerEvent = wParam.ToInt32();
            if (powerEvent is PbtApmResumeSuspend or PbtApmResumeAutomatic)
            {
                Dispatcher.BeginInvoke(new Action(HandleSystemResume));
            }

            return IntPtr.Zero;
        }

        if (message != WmHotkey)
        {
            return IntPtr.Zero;
        }

        if (wParam.ToInt32() == ShellHotkeyId)
        {
            ToggleWindow();
            handled = true;
        }
        else if (wParam.ToInt32() == PeekHotkeyId)
        {
            _ = ShowPeekAsync();
            handled = true;
        }
        else if (wParam.ToInt32() == CommandPaletteHotkeyId)
        {
            ShowCommandPalette();
            handled = true;
        }
        else if (wParam.ToInt32() == LookHotkeyId)
        {
            RememberForegroundWindow();
            _ = LookAtForegroundWindowAsync();
            handled = true;
        }
        else if (wParam.ToInt32() == EscapeHotkeyId)
        {
            // El rail desplegado se recoge primero, igual que con Escape desde dentro: son dos
            // niveles y saltarse uno cierra más de lo que la persona pidió.
            if (_sideRailExpanded)
            {
                SetSideRailExpanded(expanded: false, animate: true);
            }
            else
            {
                HideAnimated();
            }

            handled = true;
        }
        else if (wParam.ToInt32() == FlowHotkeyId)
        {
            ToggleFlowDictation();
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void ShowFromBackground()
    {
        if (_isClosed)
        {
            return;
        }

        RememberForegroundWindow();
        ShowAnimated();

        // Diseño D3.1: mientras Sakura estaba oculto en la bandeja, _focusTickTimer siguió
        // corriendo (nunca se detiene salvo al cerrar), así que el dominio está al día — pero el
        // reloj visible de Enfoque (y el resumen de Inicio) no se refrescan solos hasta el
        // siguiente tick de hasta un segundo. Se sincroniza de inmediato al reactivar, igual que
        // ya hace HandleSystemResume() al volver de suspensión.
        CheckFocusTimer();
    }

    private void ConfigureManagedOllamaSupervisor()
    {
        if (_managedOllamaSupervisor is null || _isClosed)
        {
            return;
        }

        if (!_managedOllamaSupervisor.Configure(_preferences))
        {
            return;
        }

        SetManagedAiRuntimePreparing();
        _managedOllamaSupervisor.StartMonitoring(snapshot =>
            Dispatcher.BeginInvoke(new Action(() =>
                UpdateManagedAiRuntimeState(snapshot))));
    }

    public void SetManagedAiRuntimePreparing()
    {
        if (_isClosed)
        {
            return;
        }

        var model = string.IsNullOrWhiteSpace(_preferences.AiModel)
            ? "modelo local"
            : _preferences.AiModel;
        _assistantView.SetAiProviderStatus(
            $"Ollama · {model} · preparando IA local…");
    }

    public void UpdateManagedAiRuntimeState(OllamaRuntimeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _latestOllamaRuntimeSnapshot = snapshot;

        if (_isClosed)
        {
            return;
        }

        RefreshAdaptiveEnginePlan();

        if (snapshot.IsRunning)
        {
            _runtimeAiStatus = "Ollama listo";
            _runtimeAiHealthy = true;
            RefreshRuntimeDashboard();
            var recovered = _managedAiRuntimeFailureNotified;
            _managedAiRuntimeFailureNotified = false;
            UpdateAiProviderStatus();

            if (recovered)
            {
                _assistantView.AddSakuraMessage(
                    "La IA local volvió a estar disponible automáticamente.");
                _capsuleWindow.ShowMessage(
                    CapsuleKind.Success,
                    "IA local recuperada",
                    "Sakura volvió a iniciar el motor local.",
                    _preferences.Position);
            }

            return;
        }

        _runtimeAiStatus = "Ollama necesita atención";
        _runtimeAiHealthy = false;
        RefreshRuntimeDashboard();
        var model = string.IsNullOrWhiteSpace(_preferences.AiModel)
            ? "modelo local"
            : _preferences.AiModel;
        _assistantView.SetAiProviderStatus(
            $"Ollama · {model} · IA local no disponible");

        if (_managedAiRuntimeFailureNotified)
        {
            return;
        }

        _managedAiRuntimeFailureNotified = true;
        _assistantView.AddSakuraMessage(
            $"No pude preparar la IA local: {snapshot.Message}");
        _capsuleWindow.ShowMessage(
            CapsuleKind.Error,
            "IA local no disponible",
            snapshot.Message,
            _preferences.Position);
    }

    private void HandleSystemResume()
    {
        if (_isClosed)
        {
            return;
        }

        CheckTaskReminders();
        CheckFocusTimer();
        _ = RefreshMetricsAsync();
        _ = EnsureManagedAiRuntimeAfterResumeAsync();
    }

    private async Task EnsureManagedAiRuntimeAfterResumeAsync()
    {
        if (_managedOllamaSupervisor is null || _isClosed)
        {
            return;
        }

        try
        {
            await Task.Delay(
                TimeSpan.FromMilliseconds(800),
                _lifetimeCancellation.Token);
            var snapshot = await _managedOllamaSupervisor.EnsureRunningAsync(
                _lifetimeCancellation.Token);
            UpdateManagedAiRuntimeState(snapshot);
        }
        catch (OperationCanceledException)
        {
            // Nexo se está cerrando.
        }
        catch (Exception exception)
        {
            UpdateManagedAiRuntimeState(new OllamaRuntimeSnapshot(
                OllamaRuntimeState.ManagedInstalled,
                OllamaRuntimeEndpoints.ManagedBaseUrl,
                null,
                exception.Message));
        }
    }

    /// <summary>Si el shell está de verdad a la vista, no solo mostrado con opacidad cero.</summary>
    private bool IsShellOnScreen => IsVisible && Opacity > 0.1 && !_isHiding;

    private void ToggleWindow()
    {
        if (IsShellOnScreen)
        {
            HideAnimated();
            return;
        }

        RememberForegroundWindow();

        // Pedido a propósito —atajo o bandeja—, así que se queda hasta que se cierre a propósito.
        _openedByHover = false;
        ShowAnimated();
    }

    private void ShowAnimated()
    {
        _isHiding = false;
        _touchedSinceReveal = false;
        _pointerOutsideSince = null;
        _unattendedWatch.IsEnabled = _openedByHover;
        PositionWindow();
        SetMetricsCadence(isShellVisible: true);
        _ = RefreshMetricsAsync();

        if (!IsVisible)
        {
            Show();
        }

        Activate();
        Topmost = true;
        AcquireEscapeHotkey();

        ShellBorder.BeginAnimation(OpacityProperty, null);
        ShellTranslate.BeginAnimation(TranslateTransform.XProperty, null);
        ShellScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        ShellScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);

        if (!ShellAnimationsAllowed)
        {
            ShellTranslate.X = 0;
            ShellScale.ScaleX = 1;
            ShellScale.ScaleY = 1;
            ShellBorder.Opacity = 1;
            FocusCurrentView();
            return;
        }

        // El shell entra desde el borde de la pantalla al que está acoplado y crece un punto al
        // llegar. El desplazamiento es más largo que antes (48 en vez de 34) porque ahora lo empuja
        // una curva que cubre casi todo el recorrido pronto: con el desplazamiento corto el gesto
        // se perdía antes de poder leerse.
        var offset = _preferences.Position == SidebarPosition.Right ? 48d : -48d;
        ShellTranslate.X = offset;
        ShellScale.ScaleX = 0.985;
        ShellScale.ScaleY = 0.985;
        ShellBorder.Opacity = 0;

        ShellBorder.Animate(OpacityProperty, 1, SakuraMotion.Reveal, SakuraMotion.DecelerateCurve);
        ShellTranslate.AnimateTransform(
            TranslateTransform.XProperty, 0, SakuraMotion.Emphasized, SakuraMotion.EmphasizedCurve);
        ShellScale.AnimateTransform(
            ScaleTransform.ScaleXProperty, 1, SakuraMotion.Emphasized, SakuraMotion.SubtleSpringCurve);
        ShellScale.AnimateTransform(
            ScaleTransform.ScaleYProperty, 1, SakuraMotion.Emphasized, SakuraMotion.SubtleSpringCurve);

        StaggerNavigationEntrance();
        FocusCurrentView();
    }

    /// <summary>
    /// Diseño D26 — los elementos del rail entran uno detrás de otro en vez de todos a la vez.
    /// El retardo es corto y tiene tope (ver <see cref="SakuraMotion.MaximumStagger"/>): lo que se
    /// busca es que el ojo recorra la lista, no que el último botón se haga esperar.
    /// </summary>
    private void StaggerNavigationEntrance()
    {
        if (!ShellAnimationsAllowed)
        {
            return;
        }

        var fromOffset = RailIsOnLeft ? -14d : 14d;
        var index = 0;

        foreach (var button in NavigationGrid.Children.OfType<Button>())
        {
            var transform = new TranslateTransform();
            button.RenderTransform = transform;
            button.EnterFrom(transform, fromOffset, beginTime: SakuraMotion.StaggerAt(index));
            index++;
        }
    }

    private void HideAnimated()
    {
        if (_isHiding)
        {
            return;
        }

        _unattendedWatch.Stop();
        _openedByHover = false;
        _pointerOutsideSince = null;
        ReleaseEscapeHotkey();

        if (!ShellAnimationsAllowed)
        {
            _edgeRevealWatcher.SuppressBriefly();
            Hide();
            SetMetricsCadence(isShellVisible: false);
            return;
        }

        _isHiding = true;

        // Al ocultarse, el ratón suele seguir justo donde estaba. Sin esta pausa, cerrar Sakura con
        // el cursor cerca del borde la volvería a abrir de inmediato.
        _edgeRevealWatcher.SuppressBriefly();

        // La salida es más corta que la entrada y usa la curva que acelera: lo que se retira no
        // pide atención, y hacerlo esperar solo retrasa lo que la persona quería hacer a continuación.
        var offset = _preferences.Position == SidebarPosition.Right ? 48d : -48d;
        var duration = SakuraMotion.Exit;
        var easing = SakuraMotion.AccelerateCurve;

        var slideAnimation = new DoubleAnimation(offset, duration)
        {
            EasingFunction = easing
        };

        var opacityAnimation = new DoubleAnimation(0, duration)
        {
            EasingFunction = easing
        };

        opacityAnimation.Completed += (_, _) =>
        {
            Hide();
            SetMetricsCadence(isShellVisible: false);
            _isHiding = false;
        };

        ShellTranslate.BeginAnimation(TranslateTransform.XProperty, slideAnimation);
        ShellBorder.BeginAnimation(OpacityProperty, opacityAnimation);
    }

    /// <summary>
    /// Separación entre el shell y el borde de la pantalla.
    ///
    /// Diseño D62 — antes eran doce píxeles de ventana más dieciséis de margen interior, que
    /// existían para que cupiera la sombra pintada a mano. La sombra ahora la dibuja DWM fuera del
    /// rectángulo de la ventana, así que el margen desapareció y la separación se declara entera
    /// aquí, que es donde se decide dónde va la ventana.
    /// </summary>
    private const double ShellScreenInset = 24;

    private void PositionWindow()
    {
        var workArea = SystemParameters.WorkArea;
        Height = Math.Max(MinHeight, workArea.Height - (ShellScreenInset * 2));
        Top = workArea.Top + ShellScreenInset;
        Left = _preferences.Position == SidebarPosition.Right
            ? workArea.Right - Width - ShellScreenInset
            : workArea.Left + ShellScreenInset;
    }

    /// <summary>
    /// Diseño D26 — el rail de navegación vive siempre en el lado interior del shell, el que mira
    /// al centro de la pantalla. Con Sakura acoplada a la derecha eso es el lado izquierdo, que es
    /// como ha estado siempre; acoplada a la izquierda, el rail pasa a la derecha.
    ///
    /// Antes el rail estaba clavado en la columna 0 pasara lo que pasara, así que al mover Sakura
    /// al borde izquierdo el menú se quedaba donde estaba: pegado al borde de la pantalla, con el
    /// contenido empujado hacia dentro y el gesto de "abrir el menú" apuntando al lado equivocado.
    /// </summary>
    private bool RailIsOnLeft => _preferences.Position == SidebarPosition.Right;

    private void ApplyShellSide()
    {
        var railOnLeft = RailIsOnLeft;
        var flexible = new GridLength(1, GridUnitType.Star);

        Grid.SetColumn(SideRailBorder, railOnLeft ? 0 : 1);
        Grid.SetColumn(ShellContentGrid, railOnLeft ? 1 : 0);

        ShellFirstColumn.Width = railOnLeft ? GridLength.Auto : flexible;
        ShellSecondColumn.Width = railOnLeft ? flexible : GridLength.Auto;

        // El rail se ancla al borde por el que crece. Sin esto, al expandirse aparecería el ancho
        // nuevo por el lado contrario y los iconos darían un salto lateral en mitad de la animación.
        var railEdge = railOnLeft ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        SideRailBorder.HorizontalAlignment = railEdge;
        SideRailContentGrid.HorizontalAlignment = railEdge;

        ShellContentGrid.Margin = railOnLeft
            ? new Thickness(13, 2, 2, 2)
            : new Thickness(2, 2, 13, 2);

        ApplyToggleButtonSide(railOnLeft);
        ApplyNavigationItemsSide(railOnLeft);
        ApplySideRailButtonLayout(_sideRailExpanded);
    }

    private void ApplyToggleButtonSide(bool railOnLeft)
    {
        SideRailToggleIconColumn.Width = railOnLeft ? new GridLength(46) : new GridLength(1, GridUnitType.Star);
        SideRailToggleTextColumn.Width = railOnLeft ? new GridLength(1, GridUnitType.Star) : new GridLength(46);

        Grid.SetColumn(SideRailToggleIcon, railOnLeft ? 0 : 1);
        SideRailToggleIcon.HorizontalAlignment = railOnLeft
            ? HorizontalAlignment.Left
            : HorizontalAlignment.Right;

        Grid.SetColumn(SideRailBrandText, railOnLeft ? 1 : 0);
        SideRailBrandText.Margin = railOnLeft
            ? new Thickness(10, 0, 0, 0)
            : new Thickness(0, 0, 10, 0);
        SideRailBrandText.HorizontalAlignment = railOnLeft
            ? HorizontalAlignment.Left
            : HorizontalAlignment.Right;

        foreach (var child in SideRailBrandText.Children.OfType<TextBlock>())
        {
            child.TextAlignment = railOnLeft ? TextAlignment.Left : TextAlignment.Right;
        }
    }

    /// <summary>
    /// Cada botón de navegación es una rejilla de tres columnas: [indicador 4][icono 42][etiqueta *].
    /// Espejarla mueve el indicador y la etiqueta a los extremos contrarios y deja el icono en su
    /// sitio, que es la columna del medio. Se fijan valores absolutos en vez de alternarlos, para
    /// que llamar a esto dos veces seguidas no deshaga el resultado.
    /// </summary>
    private void ApplyNavigationItemsSide(bool railOnLeft)
    {
        var indicatorWidth = new GridLength(4);
        var flexible = new GridLength(1, GridUnitType.Star);

        foreach (var (indicator, label) in NavigationRowParts())
        {
            if (indicator.Parent is not Grid row || row.ColumnDefinitions.Count != 3)
            {
                continue;
            }

            row.ColumnDefinitions[0].Width = railOnLeft ? indicatorWidth : flexible;
            row.ColumnDefinitions[2].Width = railOnLeft ? flexible : indicatorWidth;

            Grid.SetColumn(indicator, railOnLeft ? 0 : 2);
            Grid.SetColumn(label, railOnLeft ? 2 : 0);

            label.Margin = railOnLeft
                ? new Thickness(10, 0, 0, 0)
                : new Thickness(0, 0, 10, 0);
            label.TextAlignment = railOnLeft ? TextAlignment.Left : TextAlignment.Right;
        }
    }

    private IEnumerable<(Border Indicator, TextBlock Label)> NavigationRowParts()
    {
        yield return (HomeNavIndicator, HomeNavLabel);
        yield return (AssistantNavIndicator, AssistantNavLabel);
        yield return (TasksNavIndicator, TasksNavLabel);
        yield return (FocusNavIndicator, FocusNavLabel);
        yield return (RoutinesNavIndicator, RoutinesNavLabel);
        yield return (AudioNavIndicator, AudioNavLabel);
        yield return (CaptureNavIndicator, CaptureNavLabel);
        yield return (SystemNavIndicator, SystemNavLabel);
        yield return (SettingsNavIndicator, SettingsNavLabel);
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        ClockText.Text = now.ToString("HH:mm", CultureInfo.InvariantCulture);
        DateText.Text = now.ToString("dddd, d 'de' MMMM", new CultureInfo("es-MX"));

        if (_currentDestination.Equals("Home", StringComparison.OrdinalIgnoreCase))
        {
            RefreshHomeView();
        }
    }

    /// <summary>
    /// Diseño D58 — una imagen pegada entra por la misma puerta que una captura.
    ///
    /// No se inventa un segundo camino para adjuntar imágenes: es el mismo adjunto pendiente, la
    /// misma vista previa y el mismo envío. Lo único distinto es de dónde vino, y eso solo cambia
    /// el nombre que se enseña.
    /// </summary>
    private void AssistantView_ImagePasted(object? sender, PastedImageEventArgs e)
    {
        // Un proveedor que no ve imágenes se lo dice ahora, no después de escribir la pregunta.
        // Aceptar el adjunto en silencio y descartarlo al enviar es la peor de las dos.
        if (_preferences.AiProvider == AiProviderKind.Disabled)
        {
            _capsuleWindow.ShowMessage(
                CapsuleKind.Information,
                "La IA está desactivada",
                "Actívala en Personalizar para poder mandarle imágenes.",
                _preferences.Position);
            return;
        }

        _visualContextPersistent = false;
        _silentVisualContext = false;
        _visualContextMetadata = null;
        _pendingVisionAttachment = AiImageAttachment.FromBytes(
            e.PngBytes,
            "image/png",
            e.Title);

        _assistantView.SetVisionAttachment(e.Title, e.PngBytes);
    }

    /// <summary>
    /// Apunta el modelo en uso bajo el proveedor actual. Un modelo en blanco se borra en vez de
    /// guardarse: recordar «nada» impediría que el preset volviera a proponer su valor de fábrica.
    /// </summary>
    private void RememberModelForCurrentProvider()
    {
        if (_preferences.AiProvider == AiProviderKind.Disabled)
        {
            return;
        }

        _preferences.AiModelsByProvider ??= [];
        var key = _preferences.AiProvider.ToString();
        var model = (_preferences.AiModel ?? string.Empty).Trim();

        if (model.Length == 0)
        {
            _preferences.AiModelsByProvider.Remove(key);
        }
        else
        {
            _preferences.AiModelsByProvider[key] = model;
        }
    }

    private string RecallModelFor(AiProviderKind provider, string presetDefault)
    {
        if (_preferences.AiModelsByProvider is { } remembered &&
            remembered.TryGetValue(provider.ToString(), out var model) &&
            !string.IsNullOrWhiteSpace(model))
        {
            return model.Trim();
        }

        return presetDefault;
    }

    /// <summary>
    /// Diseño D58 — elegir la imagen del hueco libre del Panel.
    ///
    /// Se abre en Imágenes, que es donde está lo que la gente pondría aquí, en vez de en la última
    /// carpeta que tocara Sakura. Y si ya hay una elegida, se ofrece quitarla: sin esa salida, poner
    /// una imagen sería una decisión sin vuelta atrás salvo editando un archivo de ajustes.
    /// </summary>
    private void PickPanelImage()
    {
        if (!string.IsNullOrWhiteSpace(_preferences.PanelImagePath))
        {
            var choice = MessageBox.Show(
                this,
                "¿Quieres quitar la imagen del panel? Elige «No» para cambiarla por otra.",
                "Imagen del panel",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (choice == MessageBoxResult.Cancel)
            {
                return;
            }

            if (choice == MessageBoxResult.Yes)
            {
                _preferences.PanelImagePath = string.Empty;
                _dashboardWindow.View.SetPanelImage(null);
                SavePreferences();
                return;
            }
        }

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Elige una imagen para el panel",
            Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|Todos los archivos|*.*",
            CheckFileExists = true,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        _preferences.PanelImagePath = dialog.FileName;
        _dashboardWindow.View.SetPanelImage(dialog.FileName);
        SavePreferences();
    }

    /// <summary>
    /// Diseño D59 — deja una respuesta en el escritorio como documento de Word.
    ///
    /// El título sale del primer apartado y no se pregunta. Un cuadro pidiendo nombre convierte
    /// «guárdame esto» en un formulario, y el nombre correcto casi siempre está ya escrito en la
    /// propia respuesta; si no acierta, cambiarlo es renombrar un archivo que ya está delante.
    ///
    /// Los apartados son los mismos con los que se dibuja la respuesta en el chat. Una respuesta sin
    /// estructura se guarda como un único bloque en vez de rechazarse: que no tenga apartados no la
    /// hace menos digna de conservarse.
    /// </summary>
    private void AssistantView_DocumentSaveRequested(object? sender, DocumentSaveEventArgs e)
    {
        var sections = AnswerSections.Parse(e.Answer);
        if (sections.Count == 0)
        {
            sections = [new AnswerSection(string.Empty, e.Answer.Trim())];
        }

        var title = sections.FirstOrDefault(section => section.Title.Length > 0)?.Title;
        if (string.IsNullOrWhiteSpace(title))
        {
            title = "Respuesta de Sakura";
        }

        var target = DocumentDestination.Resolve(DocumentFolder.Desktop, title, ".docx");
        var result = _documentDropService.Save(target, WordDocumentBuilder.Build(title, sections));

        if (!result.Saved)
        {
            _capsuleWindow.ShowMessage(
                CapsuleKind.Warning,
                "No pude guardar el documento",
                result.Message,
                _preferences.Position);
            return;
        }

        // Se enseña el nombre del archivo y el sitio en palabras, no la ruta: quien lo pidió dijo
        // «el escritorio» y ahí es donde tiene que ir a mirar.
        _capsuleWindow.ShowMessage(
            CapsuleKind.Success,
            "Documento guardado",
            $"{Path.GetFileName(result.FullPath)} está en el escritorio.",
            _preferences.Position);

        _homeView.AddRecentAction(
            "Documento guardado", Path.GetFileName(result.FullPath));
    }

    // ---------- Actualizaciones (Diseño D65) ----------

    /// <summary>
    /// La versión que está corriendo, tal cual la grabó la compilación.
    ///
    /// Se lee del ensamblado y no de una constante escrita a mano: una constante se olvida de
    /// actualizar exactamente en la versión en la que importa, y entonces Sakura se cree otra.
    /// </summary>
    private static string CurrentVersionText =>
        System.Reflection.CustomAttributeExtensions
            .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(
                System.Reflection.Assembly.GetExecutingAssembly())?
            .InformationalVersion ?? "desconocida";

    private static SakuraVersion CurrentVersion =>
        SakuraVersion.TryParse(CurrentVersionText, out var version)
            ? version
            : new SakuraVersion(0, 0, 0, string.Empty);

    private string UpdateWorkFolder =>
        Path.Combine(NexoDataPaths.RootDirectory, "actualizaciones");

    private async Task CheckForUpdateAsync()
    {
        _settingsView.SetUpdateStatus("Comprobando…", busy: true);
        _settingsView.ShowUpdateOffer(null, null);
        _offeredUpdate = null;

        SakuraVersion? skipped =
            SakuraVersion.TryParse(_preferences.SkippedUpdateVersion, out var parsed)
                ? parsed
                : null;

        var lookup = await _updateService.LookForUpdateAsync(
            CurrentVersion, skipped, _lifetimeCancellation.Token);

        _preferences.LastUpdateCheckAt = DateTimeOffset.UtcNow;
        SavePreferences();

        if (!lookup.Found)
        {
            _settingsView.SetUpdateStatus(lookup.Message, busy: false);
            return;
        }

        _offeredUpdate = lookup.Manifest;
        _settingsView.SetUpdateStatus(
            "Hay una versión más nueva disponible.", busy: false);
        _settingsView.ShowUpdateOffer(
            lookup.Manifest!.Version.ToString(), lookup.Manifest.Notes);
    }

    /// <summary>
    /// Descarga, prepara y entrega el relevo al ayudante.
    ///
    /// Sakura se cierra **después** de lanzarlo, no antes: el guión espera a que este proceso
    /// termine, así que si se cerrara primero no habría nadie para lanzarlo.
    /// </summary>
    private async Task InstallOfferedUpdateAsync()
    {
        if (_offeredUpdate is not { } manifest)
        {
            return;
        }

        var install = AppContext.BaseDirectory;

        _settingsView.SetUpdateStatus("Descargando…", busy: true);

        var progress = new Progress<double>(_settingsView.SetUpdateProgress);
        var prepared = await _updateService.PrepareAsync(
            manifest, install, UpdateWorkFolder, progress, _lifetimeCancellation.Token);

        if (!prepared.Ready)
        {
            _settingsView.SetUpdateStatus(prepared.Problem, busy: false);
            _settingsView.ShowUpdateOffer(
                manifest.Version.ToString(), manifest.Notes);
            return;
        }

        if (!WindowsUpdateService.LaunchHelper(prepared.HelperPath))
        {
            _settingsView.SetUpdateStatus(
                "No se pudo iniciar el instalador de la actualización.", busy: false);
            return;
        }

        _settingsView.SetUpdateStatus(
            "Sakura se va a cerrar para terminar de instalarse…", busy: true);

        // Se sale por la puerta de siempre y no llamando a Shutdown a pelo, y esto lo enseñó la
        // primera prueba real: RequestExit para antes el runtime de IA administrado, y saltarse ese
        // paso deja un proceso hijo vivo y a Sakura sin morir del todo. El ayudante esperaba, no la
        // veía morir, y se retiraba sin tocar nada — desde fuera, Sakura se cerraba y no volvía.
        RequestExit();
    }

    private void SkipOfferedUpdate()
    {
        if (_offeredUpdate is not { } manifest)
        {
            return;
        }

        // Se guarda la versión concreta, no un «no molestar»: la siguiente sí se ofrece.
        _preferences.SkippedUpdateVersion = manifest.Version.ToString();
        SavePreferences();

        _offeredUpdate = null;
        _settingsView.ShowUpdateOffer(null, null);
        _settingsView.SetUpdateStatus(
            $"Se dejó pasar la {manifest.Version}. Se avisará cuando salga otra.", busy: false);
    }

    private void HideShellButton_Click(object sender, RoutedEventArgs e) => HideAnimated();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Diseño D2: Ctrl + K abre el Sakura Command Center. Se enlaza a la ventana, no con
        // RegisterHotKey, porque sus acciones operan sobre el shell ya visible; capturar Ctrl + K
        // en todo el sistema se lo quitaría a cualquier otra aplicación. Alt + A, Alt + Shift + A,
        // Ctrl + Espacio y Ctrl + Shift + Espacio siguen siendo globales y no se tocan.
        if (e.Key == Key.K && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            ShowCommandCenter();
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Escape)
        {
            return;
        }

        if (_sideRailExpanded)
        {
            SetSideRailExpanded(expanded: false, animate: true);
        }
        else
        {
            HideAnimated();
        }

        e.Handled = true;
    }

    private void AssistantView_ConversationChanged(object? sender, EventArgs e)
    {
        if (_preferences.SaveConversationHistory)
        {
            _conversationStore.Save(_assistantView.GetConversationSnapshot());
        }
    }

    private void AssistantView_ConversationCleared(object? sender, EventArgs e)
    {
        _conversationStore.Clear();
    }

    private async void AssistantView_VisionCaptureRequested(object? sender, EventArgs e)
    {
        await LookAtForegroundWindowAsync();
    }

    private void AssistantView_VisionAttachmentCleared(object? sender, EventArgs e)
    {
        ClearPendingVisionAttachment();
    }

    private async void CaptureView_CaptureRequested(object? sender, EventArgs e)
    {
        await CaptureForVisionAsync();
    }

    private async void AssistantView_PromptSubmitted(
        object? sender,
        PromptSubmittedEventArgs e)
    {
        await ProcessPromptAsync(e.Prompt, fromVoice: false);
    }

    private async Task ProcessPromptAsync(string prompt, bool fromVoice)
    {
        if (await TryHandlePendingVoiceDecisionAsync(prompt, fromVoice))
        {
            return;
        }

        _voicePromptActive = fromVoice;

        try
        {
            if (_pendingVisionAttachment is null &&
                VisualContextPromptPolicy.ShouldAcquireVisualContext(prompt, fromVoice))
            {
                await PrepareVisualContextAsync(
                    showWorkspace: false,
                    showFeedback: false,
                    silentContext: true);
            }

            _assistantView.AddUserMessage(prompt);

            // Las rutas locales ya muestran su propio resultado. Evitamos una
            // cápsula genérica que parpadee antes de acciones instantáneas.
            await Task.Yield();

            // Diseño D10 — antes de los parsers: "recuerda que prefiero X" es una orden de memoria,
            // no una consulta a la IA. Va después de AddUserMessage para que la frase quede en la
            // conversación igual que cualquier otra.
            if (TryHandleWorkspaceSearchPrompt(prompt))
            {
                return;
            }

            if (TryHandleComputerUseIntent(prompt))
            {
                return;
            }

            if (await TryHandleWorkspaceEditInstructionAsync(prompt, fromVoice))
            {
                return;
            }

            if (TryHandleMemoryPrompt(prompt))
            {
                return;
            }

            // La precedencia entre subsistemas vive en `PromptDispatchPolicy`, no aquí.
            // Se evalúan los cuatro parsers y la política decide, de modo que "inicia" deje
            // de significar automáticamente "rutina". Ver defecto D1 de la fase 1.1.
            var routineCommand = _routineCommandParser.Parse(prompt);
            var focusCommand = _focusCommandParser.Parse(prompt);
            var taskCommand = _taskCommandParser.Parse(prompt, DateTimeOffset.Now);
            var interpretation = _commandParser.Parse(prompt);

            var dispatch = PromptDispatchPolicy.Resolve(
                routineCommand,
                focusCommand,
                taskCommand,
                interpretation,
                name => _routineManager.FindBestMatch(name) is not null);

            switch (dispatch.Target)
            {
                case PromptDispatchTarget.Routine:
                    await ExecuteRoutineCommandAsync(routineCommand);
                    return;

                case PromptDispatchTarget.Focus:
                    await ExecuteFocusCommandAsync(focusCommand);
                    return;

                case PromptDispatchTarget.Task:
                    await ExecuteTaskCommandAsync(taskCommand);
                    return;

                case PromptDispatchTarget.LocalCommand:
                    await ExecuteLocalCommandAsync(interpretation.Intent!);
                    return;

                default:
                    await SendPromptToAiAsync(prompt, fromVoice);
                    return;
            }
        }
        finally
        {
            _voicePromptActive = false;
        }
    }

    /// <summary>
    /// Diseño D34 — quien pulsó Ctrl+Espacio no quiso abrir Sakura: estaba en otra aplicación y
    /// preguntó desde ahí. Antes esto traía la ventana entera encima y dejaba la respuesta en la
    /// pestaña de chat, obligando a volver a donde se estaba. Ahora la respuesta va a una píldora de
    /// esquina y la ventana se queda como estaba. La conversación se guarda igual, así que sigue
    /// estando en el chat para quien la busque después.
    ///
    /// La píldora se abre y se cierra <b>aquí</b>, envolviendo al método que hace el trabajo, y no
    /// dentro de él. La primera versión la cerraba en el <c>finally</c> interno y se quedó colgada
    /// en «Pensando…» la primera vez que se probó: hay cuatro salidas anticipadas antes de ese
    /// bloque —IA desactivada, Modo Juego, IA local que no arranca, cancelación— y la que saltó fue
    /// el Modo Juego. Añadir la llamada en cada una habría durado hasta la quinta; envolviendo, no
    /// hay salida que pueda dejarla abierta, ni siquiera una que se añada más adelante.
    /// </summary>
    private async Task SendPromptToAiAsync(string prompt, bool fromVoice)
    {
        // Diseño D58 — una pregunta hablada con el shell escondido también merece la píldora.
        //
        // Antes solo la usaba la paleta, así que preguntar por voz no abría nada: la respuesta se
        // escribía en un chat que no estaba a la vista y lo único que se veía era un aviso diciendo
        // que había respuesta. Es decir, la parte de «preguntar sin manos» funcionaba y la de
        // «recibir sin manos» no: había que ir a buscarla.
        //
        // Con el shell delante no se usa. La respuesta ya se está escribiendo en el chat que la
        // persona tiene abierto, y una píldora encima sería la misma respuesta dos veces.
        _answerInPill = _promptFromCommandPalette || (fromVoice && !IsShellOnScreen);
        if (!_answerInPill)
        {
            await SendPromptToAiCoreAsync(prompt, fromVoice);
            return;
        }

        _pillFailure = null;
        _answerPillWindow.BeginAnswer(prompt, _preferences.Position);

        try
        {
            await SendPromptToAiCoreAsync(prompt, fromVoice);
        }
        finally
        {
            _answerInPill = false;

            // Si el camino bueno ya la cerró, esto no hace nada. Si no, se cierra con el motivo —o
            // sin él, pero cerrada— en vez de quedarse pensando encima de lo que se estuviera
            // haciendo.
            _answerPillWindow.CompleteAnswer(
                _pillFailure ?? "No llegó ninguna respuesta.");
        }
    }

    private async Task SendPromptToAiCoreAsync(string prompt, bool fromVoice)
    {
        var configuration = BuildAiConfiguration();
        if (!configuration.IsEnabled)
        {
            const string unavailableMessage =
                "La consulta es abierta, pero la IA está desactivada. Puedes elegir OpenAI, Ollama, LM Studio o un servidor compatible en Personalización.";
            _assistantView.AddSakuraMessage(unavailableMessage);
            _capsuleWindow.ShowMessage(
                CapsuleKind.Information,
                "IA desactivada",
                "Elige un proveedor desde Personalización.",
                _preferences.Position);
            SpeakVoiceResult("La inteligencia artificial está desactivada.");
            _pillFailure = "La IA está desactivada. Elígela en Personalización.";
            return;
        }

        var resourceDecision = await EnsureFreshResourceDecisionAsync();
        var usesLocalRuntime = AiExecutionLocationPolicy.UsesLocalRuntime(configuration);
        var aiAllowed = usesLocalRuntime
            ? resourceDecision.AllowLocalAi
            : resourceDecision.AllowRemoteAi;

        if (!aiAllowed)
        {
            var restriction = usesLocalRuntime
                ? "La IA local está pausada para proteger el rendimiento."
                : "Las consultas de IA están pausadas durante el Modo Juego.";
            PresentResourceRestriction(resourceDecision, restriction, fromVoice);
            _pillFailure = restriction;
            return;
        }

        if (_managedOllamaSupervisor is not null &&
            OllamaRuntimeEndpoints.IsManagedBaseUrl(configuration.BaseUrl))
        {
            _assistantView.SetAiActivity("preparando IA local…");

            OllamaRuntimeSnapshot runtimeSnapshot;
            try
            {
                runtimeSnapshot = await _managedOllamaSupervisor.EnsureRunningAsync(
                    _lifetimeCancellation.Token);
            }
            catch (OperationCanceledException)
            {
                _assistantView.SetAiActivity(null);
                return;
            }
            catch (Exception exception)
            {
                runtimeSnapshot = new OllamaRuntimeSnapshot(
                    OllamaRuntimeState.ManagedInstalled,
                    OllamaRuntimeEndpoints.ManagedBaseUrl,
                    null,
                    exception.Message);
            }

            UpdateManagedAiRuntimeState(runtimeSnapshot);
            if (!runtimeSnapshot.IsRunning)
            {
                _assistantView.SetAiActivity(null);
                SpeakVoiceResult("La inteligencia artificial local no está disponible.");
                _pillFailure = "La IA local no está disponible.";
                return;
            }
        }

        await _aiGate.WaitAsync(_lifetimeCancellation.Token);
        var streamingStarted = false;

        // Diseño D14 — respuesta que trae un cambio propuesto, si la hubo. Se ofrece fuera del
        // bloque, con el turno de IA ya liberado.
        string? proposedEditAnswer = null;

        try
        {
            string? systemContext = null;
            if (_preferences.ShareSystemMetricsWithAi &&
                AiContextPolicy.ShouldIncludeSystemMetrics(prompt))
            {
                var snapshotAge = DateTimeOffset.Now - _latestSnapshot.CapturedAt;
                if (_latestSnapshot.CapturedAt == DateTimeOffset.MinValue ||
                    snapshotAge > TimeSpan.FromSeconds(5))
                {
                    await RefreshMetricsAsync();
                }

                systemContext = BuildAiSystemContext(_latestSnapshot);
            }

            if (_pendingVisionAttachment is not null &&
                !string.IsNullOrWhiteSpace(_visualContextMetadata))
            {
                systemContext = string.IsNullOrWhiteSpace(systemContext)
                    ? _visualContextMetadata
                    : systemContext + Environment.NewLine + _visualContextMetadata;
            }

            // Diseño D10 — la memoria se usa, no solo se guarda: sin esto no hay continuidad
            // ninguna entre sesiones. El constructor decide qué sale (solo categorías activas
            // ahora, acotado y redactado de nuevo), porque este texto viaja al proveedor.
            var memoryContext = MemoryContextBuilder.Build(
                _memoryManager.GetAll(_preferences.Memory, DateTimeOffset.Now),
                _preferences.Memory,
                DateTimeOffset.Now);

            if (!string.IsNullOrWhiteSpace(memoryContext))
            {
                systemContext = string.IsNullOrWhiteSpace(systemContext)
                    ? memoryContext
                    : systemContext + Environment.NewLine + memoryContext;
            }

            // Diseño D12 — el contexto del proyecto se consume aquí y se apaga. Que dure una sola
            // consulta es la garantía de que autorizar una carpeta no convierte cada pregunta
            // posterior en un envío de código.
            var workspaceContext = _pendingWorkspaceContext;
            _pendingWorkspaceContext = null;

            if (!string.IsNullOrWhiteSpace(workspaceContext))
            {
                systemContext = string.IsNullOrWhiteSpace(systemContext)
                    ? workspaceContext
                    : systemContext + Environment.NewLine + workspaceContext;
            }

            var images = _pendingVisionAttachment is { } image
                ? new[] { image }
                : null;
            var requestMode = VisionIntentPolicy.Resolve(
                prompt,
                images is { Length: > 0 });
            var activity = requestMode == AiRequestMode.VisionTechnicalDiagnostic
                ? "leyendo el error…"
                : "pensando…";

            _assistantView.SetAiActivity(activity);
            _assistantView.BeginSakuraStreamingMessage(
                requestMode == AiRequestMode.VisionTechnicalDiagnostic
                    ? "Analizando la evidencia visible…"
                    : "Pensando…");
            streamingStarted = true;

            _capsuleWindow.ShowMessage(
                CapsuleKind.Processing,
                requestMode == AiRequestMode.VisionTechnicalDiagnostic
                    ? "Diagnosticando captura"
                    : $"Consultando {configuration.DisplayName}",
                string.IsNullOrWhiteSpace(configuration.Model)
                    ? "Preparando la solicitud…"
                    : configuration.Model,
                _preferences.Position);

            var request = new AiChatRequest(
                _assistantView.GetConversationSnapshot(),
                NexoAiInstructions.Default,
                systemContext,
                images,
                requestMode);

            var receivedFirstChunk = false;

            await foreach (var chunk in _aiChatService.StreamAsync(
                configuration,
                request,
                _lifetimeCancellation.Token))
            {
                if (!receivedFirstChunk)
                {
                    receivedFirstChunk = true;
                    _assistantView.SetAiActivity("respondiendo…");
                }

                _assistantView.AppendSakuraStreamingText(chunk);

                if (_answerInPill)
                {
                    _answerPillWindow.AppendAnswer(chunk);
                }
            }

            var finalText = _assistantView.CompleteSakuraStreamingMessage();
            streamingStarted = false;

            if (_answerInPill)
            {
                _answerPillWindow.CompleteAnswer();
            }

            if (string.IsNullOrWhiteSpace(finalText))
            {
                throw new AiChatStreamException(
                    "El proveedor terminó la respuesta sin enviar texto utilizable.");
            }

            // El aviso solo cuando la respuesta no se esté enseñando ya. Con la píldora abierta,
            // anunciar que hay respuesta al lado de la respuesta sobra.
            if (!_answerInPill)
            {
                _capsuleWindow.ShowMessage(
                    CapsuleKind.Success,
                    "Respuesta lista",
                    SummarizeForCapsule(finalText),
                    _preferences.Position);
            }

            if (_visualContextPersistent)
            {
                RestartVisualContextExpiry();
            }
            else
            {
                ClearPendingVisionAttachment();
            }

            if (fromVoice)
            {
                SpeakVoiceResult(finalText);
            }

            // Diseño D14 — solo si esta consulta se pidió para cambiar algo. Se anota y se ofrece
            // DESPUÉS de soltar el turno de IA: la confirmación es un diálogo modal, y dejar el
            // turno tomado mientras alguien lo lee bloquearía cualquier otra consulta.
            if (_pendingWorkspaceEditRequested)
            {
                proposedEditAnswer = finalText;
            }
        }
        catch (AiChatStreamException exception)
        {
            if (streamingStarted)
            {
                _assistantView.CancelSakuraStreamingMessage();
            }

            _assistantView.AddSakuraMessage(
                $"No pude obtener una respuesta: {exception.Message}");
            _pillFailure = exception.Message;
            _capsuleWindow.ShowMessage(
                CapsuleKind.Error,
                "La IA no respondió",
                exception.Message,
                _preferences.Position);
            SpeakVoiceResult("No pude obtener una respuesta de la inteligencia artificial.");
        }
        catch (OperationCanceledException)
        {
            if (streamingStarted)
            {
                _assistantView.CancelSakuraStreamingMessage();
            }

            if (!_isClosed)
            {
                _assistantView.AddSakuraMessage("La consulta fue cancelada.");
            }
        }
        catch (Exception exception) when (
            exception is HttpRequestException or IOException or System.Text.Json.JsonException)
        {
            if (streamingStarted)
            {
                _assistantView.CancelSakuraStreamingMessage();
            }

            const string detail =
                "La conexión se interrumpió mientras Sakura recibía la respuesta.";
            _assistantView.AddSakuraMessage($"No pude obtener una respuesta: {detail}");
            _pillFailure = detail;
            _capsuleWindow.ShowMessage(
                CapsuleKind.Error,
                "Respuesta interrumpida",
                detail,
                _preferences.Position);
        }
        finally
        {
            // Si la respuesta se cortó, se canceló o falló, la oferta de escribir no sobrevive: un
            // cambio propuesto a partir de media respuesta no es un cambio, es un riesgo.
            _pendingWorkspaceEditRequested = false;
            _assistantView.SetAiActivity(null);

            _aiGate.Release();
        }

        if (proposedEditAnswer is not null)
        {
            OfferWorkspaceEdit(proposedEditAnswer);
        }
    }

    private Task LookAtForegroundWindowAsync() =>
        PrepareVisualContextAsync(
            showWorkspace: true,
            showFeedback: true,
            silentContext: false);

    private async Task<bool> PrepareVisualContextAsync(
        bool showWorkspace,
        bool showFeedback,
        bool silentContext)
    {
        if (!_preferences.VisionEnabled)
        {
            if (showFeedback)
            {
                _capsuleWindow.ShowMessage(
                    CapsuleKind.Warning,
                    "Sakura Vision desactivado",
                    "Actívalo desde Personalización.",
                    _preferences.Position,
                    force: true);
            }

            return false;
        }

        var resourceDecision = await EnsureFreshResourceDecisionAsync();
        if (_preferences.ProtectVisionWhenBusy && !resourceDecision.AllowVision)
        {
            PresentResourceRestriction(
                resourceDecision,
                "Mirar está pausado para evitar tirones o pérdida de rendimiento.",
                fromVoice: silentContext);
            return false;
        }

        RememberForegroundWindow();

        var ownHandle = new WindowInteropHelper(this).Handle;
        var targets = _screenCaptureService.GetAvailableTargets(ownHandle.ToInt64());
        var target = targets.FirstOrDefault(candidate =>
            candidate.Kind == VisionCaptureKind.Window &&
            candidate.NativeHandle == _lastExternalWindowHandle);

        if (target is null)
        {
            if (showFeedback)
            {
                _capsuleWindow.ShowMessage(
                    CapsuleKind.Information,
                    "No encontré qué mirar",
                    "Activa una ventana y vuelve a intentarlo.",
                    _preferences.Position,
                    force: true);
            }

            return false;
        }

        if (showFeedback)
        {
            _capsuleWindow.ShowMessage(
                CapsuleKind.Processing,
                "Mirando la ventana activa",
                target.Title,
                _preferences.Position);
        }

        VisionCaptureResult result;
        try
        {
            result = await _screenCaptureService.CaptureAsync(
                target,
                _lifetimeCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            return false;
        }

        if (!result.IsSuccess || result.PngBytes is null)
        {
            if (showFeedback)
            {
                _capsuleWindow.ShowMessage(
                    CapsuleKind.Error,
                    "No pude mirar esta ventana",
                    result.Detail,
                    _preferences.Position,
                    force: true);
            }

            return false;
        }

        _visualContextPersistent = true;
        _silentVisualContext = silentContext;
        _visualContextMetadata =
            "Contexto visual temporal de Windows.\n" +
            $"Aplicación: {target.Subtitle}\n" +
            $"Ventana: {target.Title}\n" +
            $"Tamaño visible: {result.Width} × {result.Height} píxeles.\n" +
            "La imagen se procesó en memoria y no se guardó en disco.";
        _pendingVisionAttachment = AiImageAttachment.FromBytes(
            result.PngBytes,
            "image/png",
            target.Title);

        if (!silentContext)
        {
            _assistantView.SetVisionAttachment(
                target.Title,
                result.PngBytes,
                isVisualContext: true);
        }

        RestartVisualContextExpiry();

        if (showWorkspace)
        {
            ShowAnimated();
            NavigateTo("Assistant", animate: true);
        }

        if (showFeedback)
        {
            _capsuleWindow.ShowMessage(
                CapsuleKind.Success,
                "Contexto visual listo",
                $"Estoy viendo {target.Title}. Pregunta lo que necesites.",
                _preferences.Position);
        }

        return true;
    }

    private void RestartVisualContextExpiry()
    {
        if (!_visualContextPersistent)
        {
            return;
        }

        _visualContextExpiryTimer.Stop();
        _visualContextExpiryTimer.Start();
    }

    private async Task CaptureForVisionAsync()
    {
        if (!_preferences.VisionEnabled)
        {
            _assistantView.AddSakuraMessage(
                "Sakura Vision está desactivado. Puedes activarlo en Personalización → Inteligencia artificial.");
            _capsuleWindow.ShowMessage(
                CapsuleKind.Warning,
                "Sakura Vision desactivado",
                "Actívalo desde Personalización.",
                _preferences.Position);
            return;
        }

        RememberForegroundWindow();
        ShowAnimated();
        NavigateTo("Assistant", animate: true);

        var ownHandle = new WindowInteropHelper(this).Handle;
        var targets = _screenCaptureService.GetAvailableTargets(ownHandle.ToInt64());
        if (targets.Count == 0)
        {
            _capsuleWindow.ShowMessage(
                CapsuleKind.Warning,
                "No encontré qué capturar",
                "Abre la ventana que quieras analizar e inténtalo de nuevo.",
                _preferences.Position);
            return;
        }

        var picker = new VisionTargetPickerWindow(targets, _lastExternalWindowHandle)
        {
            Owner = this
        };

        if (picker.ShowDialog() != true || picker.SelectedTarget is null)
        {
            return;
        }

        var selectedTarget = picker.SelectedTarget;
        _capsuleWindow.ShowMessage(
            CapsuleKind.Processing,
            "Preparando captura",
            selectedTarget.Title,
            _preferences.Position);

        VisionCaptureResult result;
        try
        {
            Hide();
            await Task.Delay(180, _lifetimeCancellation.Token);
            result = await _screenCaptureService.CaptureAsync(
                selectedTarget,
                _lifetimeCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            if (!_isClosed)
            {
                ShowAnimated();
            }
        }

        if (!result.IsSuccess || result.PngBytes is null)
        {
            _capsuleWindow.ShowMessage(
                CapsuleKind.Error,
                "No pude capturar",
                result.Detail,
                _preferences.Position);
            return;
        }

        var preview = new VisionPreviewWindow(result.Title, result.PngBytes)
        {
            Owner = this
        };

        if (preview.ShowDialog() != true)
        {
            _capsuleWindow.ShowMessage(
                CapsuleKind.Information,
                "Captura descartada",
                "La imagen no se compartió ni se guardó.",
                _preferences.Position);
            return;
        }

        _visualContextExpiryTimer.Stop();
        _visualContextPersistent = false;
        _silentVisualContext = false;
        _visualContextMetadata = null;
        _pendingVisionAttachment = AiImageAttachment.FromBytes(
            result.PngBytes,
            "image/png",
            result.Title);
        _assistantView.SetVisionAttachment(result.Title, result.PngBytes);
        NavigateTo("Assistant", animate: true);
        _capsuleWindow.ShowMessage(
            CapsuleKind.Success,
            "Captura lista",
            "Escribe o di qué quieres saber sobre la imagen.",
            _preferences.Position);
    }

    private void ClearPendingVisionAttachment()
    {
        _visualContextExpiryTimer.Stop();
        _visualContextPersistent = false;
        _silentVisualContext = false;
        _visualContextMetadata = null;
        _pendingVisionAttachment = null;
        _assistantView.ClearVisionAttachment();
    }

    private void RememberForegroundWindow()
    {
        var foreground = GetForegroundWindow();
        var ownHandle = new WindowInteropHelper(this).Handle;
        var paletteHandle = new WindowInteropHelper(_commandPaletteWindow).Handle;

        if (foreground != IntPtr.Zero &&
            foreground != ownHandle &&
            foreground != paletteHandle)
        {
            _lastExternalWindowHandle = foreground.ToInt64();
        }
    }

    private async Task TestAiConnectionAsync()
    {
        var configuration = BuildAiConfiguration();
        _settingsView.SetAiTestInProgress(true);
        _settingsView.SetAiConnectionStatus(
            $"Probando {configuration.DisplayName}…",
            isSuccess: null);

        try
        {
            var result = await _aiChatService.TestConnectionAsync(
                configuration,
                _lifetimeCancellation.Token);

            var detail = result.Detail;
            if (result.IsSuccess && result.Models.Count > 0)
            {
                if (string.IsNullOrWhiteSpace(_preferences.AiModel) &&
                    result.Models.Count == 1)
                {
                    _preferences.AiModel = result.Models[0];
                    _settingsView.SetAiModel(result.Models[0]);
                    SavePreferences();
                    detail += $" Modelo seleccionado: {result.Models[0]}.";
                }
                else
                {
                    var preview = string.Join(", ", result.Models.Take(4));
                    detail += $" Modelos: {preview}";
                    if (result.Models.Count > 4)
                    {
                        detail += "…";
                    }
                }
            }

            _settingsView.SetAiConnectionStatus(detail, result.IsSuccess);
            _capsuleWindow.ShowMessage(
                result.IsSuccess ? CapsuleKind.Success : CapsuleKind.Error,
                result.IsSuccess ? "Proveedor conectado" : "No pude conectar",
                detail,
                _preferences.Position);
        }
        catch (OperationCanceledException)
        {
            _settingsView.SetAiConnectionStatus(
                "La prueba fue cancelada.",
                isSuccess: false);
        }
        finally
        {
            _settingsView.SetAiTestInProgress(false);
        }
    }

    private void UpdateAiProviderStatus()
    {
        if (_preferences.AiProvider == AiProviderKind.Disabled)
        {
            _runtimeAiStatus = "Desactivada";
            _runtimeAiHealthy = false;
            _assistantView.SetAiProviderStatus(
                "IA desactivada · los comandos locales siguen disponibles");
            RefreshRuntimeDashboard();
            return;
        }

        var providerName = AiProviderDefaults.Get(_preferences.AiProvider).DisplayName;
        var model = string.IsNullOrWhiteSpace(_preferences.AiModel)
            ? "sin modelo seleccionado"
            : _preferences.AiModel;
        _runtimeAiStatus = $"{providerName} configurado";
        _runtimeAiHealthy = true;
        _assistantView.SetAiProviderStatus($"{providerName} · {model}");
        RefreshRuntimeDashboard();
    }

    private AiProviderConfiguration BuildAiConfiguration()
    {
        return new AiProviderConfiguration(
            _preferences.AiProvider,
            _preferences.AiBaseUrl,
            _preferences.AiModel,
            _preferences.AiApiKeyEnvironmentVariable)
        {
            StoredApiKey = _apiKeyStore.Read(_preferences.AiProvider)
        };
    }

    private static string BuildAiSystemContext(SystemSnapshot snapshot)
    {
        var topProcess = string.IsNullOrWhiteSpace(snapshot.TopProcessName)
            ? "no disponible"
            : $"{snapshot.TopProcessName} ({snapshot.TopProcessWorkingSetBytes.GetValueOrDefault() / 1024d / 1024d:0} MB)";

        return
            $"Captura: {snapshot.CapturedAt:O}\n" +
            $"CPU: {FormatPercentage(snapshot.CpuUsagePercent)}\n" +
            $"RAM: {FormatPercentage(snapshot.MemoryUsagePercent)}\n" +
            $"GPU: {FormatPercentage(snapshot.GpuUsagePercent)}\n" +
            $"Disco del sistema: {FormatPercentage(snapshot.SystemDriveUsagePercent)}\n" +
            $"Proceso con mayor memoria: {topProcess}";
    }

    private static string SummarizeForCapsule(string text)
    {
        var compact = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return compact.Length <= 120
            ? compact
            : compact[..120] + "…";
    }

    private void ConfigureVoiceInputDevices()
    {
        var devices = _voiceCoordinator.GetInputDevices();
        var selectedDeviceNumber = devices.Any(device =>
            device.DeviceNumber == _preferences.VoiceInputDeviceNumber)
            ? _preferences.VoiceInputDeviceNumber
            : devices.FirstOrDefault()?.DeviceNumber ?? -1;

        _preferences.VoiceInputDeviceNumber = selectedDeviceNumber;

        // VoiceCoordinator.InputDeviceNumber aplica el valor a la entrada de voz y al wake
        // word en un único setter: efecto idéntico a las dos asignaciones directas que
        // sustituyó (confirmado leyendo VoiceCoordinator.cs antes de este
        // cambio), en el mismo orden (entrada de voz primero, wake word después).
        _voiceCoordinator.InputDeviceNumber = selectedDeviceNumber;
        _settingsView.SetVoiceInputDevices(devices, selectedDeviceNumber);
        SavePreferences();
    }

    private async Task ChangeVoiceInputDeviceAsync(int deviceNumber)
    {
        await using var voiceScope = await _voiceCoordinator.AcquireVoiceInputScopeAsync();
        try
        {
            await PauseWakeWordAsync();

            // La cancelación de la entrada de voz corre sobre el ámbito de voz del
            // coordinador, el único dominio de exclusión para Whisper.
            await voiceScope.CancelAsync();

            _preferences.VoiceInputDeviceNumber = deviceNumber;
            _voiceCoordinator.InputDeviceNumber = deviceNumber;
            SavePreferences();

            var selectedName = _voiceCoordinator
                .GetInputDevices()
                .FirstOrDefault(device => device.DeviceNumber == deviceNumber)
                ?.Name ?? "micrófono seleccionado";

            _assistantView.SetVoiceAvailability(
                _voiceCoordinator.IsVoiceInputReady,
                $"Micrófono activo: {selectedName}");
            _capsuleWindow.ShowMessage(
                CapsuleKind.Success,
                "Micrófono actualizado",
                selectedName,
                _preferences.Position);
        }
        finally
        {
            await ResumeWakeWordIfEnabledAsync();
        }

        // El ámbito de voz se libera aquí, al salir del método (después de la reanudación
        // del finally), preservando el orden reanudar → liberar del código anterior.
    }

    private async Task<bool> TryHandlePendingVoiceDecisionAsync(
        string prompt,
        bool fromVoice)
    {
        if (string.IsNullOrWhiteSpace(_pendingVoicePrompt))
        {
            return false;
        }

        var normalized = SpanishVoiceTranscriptNormalizer.Normalize(prompt);
        if (IsVoiceConfirmation(normalized))
        {
            var confirmedPrompt = _pendingVoicePrompt;
            _pendingVoicePrompt = null;

            _capsuleWindow.ShowMessage(
                CapsuleKind.Success,
                "Orden confirmada",
                confirmedPrompt,
                _preferences.Position);
            await ProcessPromptAsync(confirmedPrompt, fromVoice);
            return true;
        }

        if (IsVoiceCancellation(normalized))
        {
            _pendingVoicePrompt = null;
            _assistantView.AddUserMessage(prompt);
            _assistantView.AddSakuraMessage("Orden cancelada. No hice ningún cambio.");
            _capsuleWindow.ShowMessage(
                CapsuleKind.Information,
                "Orden cancelada",
                "No se ejecutó ninguna acción.",
                _preferences.Position);
            return true;
        }

        // Una orden nueva reemplaza la transcripción dudosa anterior.
        _pendingVoicePrompt = null;
        return false;
    }

    private static bool IsVoiceConfirmation(string text) =>
        text is "si" or "confirmar" or "confirma" or "correcto" or "adelante";

    private static bool IsVoiceCancellation(string text) =>
        text is "no" or "cancela" or "cancelar" or "olvidalo";

    private async Task PrepareVoiceAsync()
    {
        var requiresDownload = !_voiceCoordinator.IsVoiceInputReady;
        _assistantView.SetVoiceAvailability(
            available: false,
            "Preparando Whisper local…");

        if (requiresDownload && !_isClosed)
        {
            _capsuleWindow.ShowMessage(
                CapsuleKind.Information,
                "Preparando voz local",
                "La primera vez Sakura descarga un modelo multilingüe.",
                _preferences.Position);
        }

        var progress = new Progress<VoicePreparationProgress>(update =>
        {
            _assistantView.SetVoiceAvailability(
                available: false,
                update.Detail);
        });

        try
        {
            var result = await _voiceCoordinator.PrepareVoiceInputAsync(
                progress,
                _lifetimeCancellation.Token);

            _assistantView.SetVoiceAvailability(result.IsReady, result.Detail);
            if (result.IsReady && requiresDownload && !_isClosed)
            {
                _capsuleWindow.ShowMessage(
                    CapsuleKind.Success,
                    "Voz local lista",
                    "Whisper ya puede transcribir órdenes en español.",
                    _preferences.Position);
            }
            else if (!result.IsReady && !_isClosed)
            {
                _capsuleWindow.ShowMessage(
                    CapsuleKind.Error,
                    "Voz local no disponible",
                    result.Detail,
                    _preferences.Position);
            }
        }
        catch (OperationCanceledException)
        {
            // Nexo se está cerrando.
        }
    }

    private async Task InitializeVoiceFeaturesAsync()
    {
        await PrepareVoiceAsync();
        if (_preferences.WakeWordEnabled && !_isClosed)
        {
            await ApplyWakeWordPreferenceAsync(showCapsule: false);
        }
    }

    private async void AssistantView_VoiceInputStarted(object? sender, EventArgs e)
    {
        await using var voiceScope = await _voiceCoordinator.AcquireVoiceInputScopeAsync();
        var listeningStarted = false;

        try
        {
            await PauseWakeWordAsync();
            _voiceCoordinator.StopSpeaking();

            if (!_voiceCoordinator.IsVoiceInputReady)
            {
                await PrepareVoiceAsync();
                if (!_voiceCoordinator.IsVoiceInputReady)
                {
                    return;
                }
            }

            var result = await voiceScope.StartListeningAsync();

            if (!result.IsAvailable)
            {
                _assistantView.SetVoiceState(AssistantVoiceState.Error, result.Detail);
                _capsuleWindow.ShowMessage(
                    CapsuleKind.Error,
                    "Micrófono no disponible",
                    result.Detail,
                    _preferences.Position);
                return;
            }

            listeningStarted = true;
            _assistantView.SetVoiceState(
                AssistantVoiceState.Listening,
                "Escuchando… suelta Mic cuando termines.");
            _capsuleWindow.ShowMessage(
                CapsuleKind.Processing,
                "Escuchando",
                "Suelta Mic cuando termines de hablar.",
                _preferences.Position);
        }
        finally
        {
            if (!listeningStarted)
            {
                await ResumeWakeWordIfEnabledAsync();
            }
        }
    }

    private async void AssistantView_VoiceInputStopped(object? sender, EventArgs e)
    {
        await using var voiceScope = await _voiceCoordinator.AcquireVoiceInputScopeAsync();
        try
        {
            if (!_voiceCoordinator.IsVoiceInputListening)
            {
                return;
            }

            _assistantView.SetVoiceState(
                AssistantVoiceState.Processing,
                "Transcribiendo localmente con Whisper…");

            var result = await voiceScope.StopListeningAsync();
            await HandleVoiceRecognitionResultAsync(result);
        }
        catch (OperationCanceledException)
        {
            _assistantView.SetVoiceState(
                AssistantVoiceState.Idle,
                "La escucha fue cancelada.");
        }
        finally
        {
            await ResumeWakeWordIfEnabledAsync();
        }
    }

    private void WakeWordService_WakeWordDetected(
        object? sender,
        WakeWordDetectedEventArgs e)
    {
        _ = Dispatcher.InvokeAsync(() => HandleWakeWordDetectedAsync(e)).Task.Unwrap();
    }

    private void WakeWordService_RecognitionObserved(
        object? sender,
        WakeWordRecognitionObservedEventArgs e)
    {
        _ = Dispatcher.BeginInvoke(new Action(() =>
        {
            _lastWakeWordObservation = e;
            if (_wakeWordTestActive)
            {
                _settingsView.SetWakeWordObservation(e);
            }
        }));
    }

    private async Task StartWakeWordTestAsync()
    {
        if (!_preferences.WakeWordEnabled)
        {
            _settingsView.SetWakeWordTestStatus(
                "Activa primero la frase de voz.",
                isSuccess: false);
            return;
        }

        _wakeWordTestCancellation?.Cancel();
        _wakeWordTestCancellation?.Dispose();
        _wakeWordTestCancellation = new CancellationTokenSource();
        _wakeWordTestActive = true;
        _lastWakeWordObservation = null;
        _settingsView.ClearWakeWordObservation();

        _settingsView.SetWakeWordTestStatus(
            $"Escuchando durante 12 segundos. Di “{_preferences.WakeWordPhrase.ToSpokenText()}”.",
            isSuccess: null);
        _capsuleWindow.ShowMessage(
            CapsuleKind.Information,
            "Prueba de activación",
            $"Di “{_preferences.WakeWordPhrase.ToSpokenText()}” con voz natural.",
            _preferences.Position);

        if (!_voiceCoordinator.IsWakeWordListening)
        {
            await ApplyWakeWordPreferenceAsync(showCapsule: false);
        }

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(12), _wakeWordTestCancellation.Token);
            if (_wakeWordTestActive)
            {
                _wakeWordTestActive = false;
                var detail = _lastWakeWordObservation is null
                    ? "Vosk no produjo texto. Revisa el micrófono o prueba sensibilidad Alta."
                    : $"Lo último que escuchó Vosk fue “{_lastWakeWordObservation.RecognizedText}”. " +
                      _lastWakeWordObservation.Match.Detail;
                _settingsView.SetWakeWordTestStatus(detail, isSuccess: false);
            }
        }
        catch (OperationCanceledException)
        {
            // La prueba terminó al detectar la frase o al iniciar otra prueba.
        }
    }

    private async Task AddLastWakeWordObservationAsAliasAsync()
    {
        var observed = _lastWakeWordObservation?.RecognizedText;
        if (!WakeWordAliasPolicy.TryNormalize(observed, out var alias, out var detail))
        {
            _settingsView.SetWakeWordTestStatus(detail, isSuccess: false);
            return;
        }

        if (_preferences.WakeWordAliases.Contains(alias, StringComparer.Ordinal))
        {
            _settingsView.SetWakeWordTestStatus($"“{alias}” ya está guardado.", isSuccess: true);
            return;
        }

        if (_preferences.WakeWordAliases.Count >= WakeWordAliasPolicy.MaximumAliases)
        {
            _settingsView.SetWakeWordTestStatus(
                $"Puedes guardar hasta {WakeWordAliasPolicy.MaximumAliases} aliases.",
                isSuccess: false);
            return;
        }

        _preferences.WakeWordAliases.Add(alias);
        _preferences.WakeWordAliases = WakeWordAliasPolicy.NormalizeMany(_preferences.WakeWordAliases);
        _voiceCoordinator.WakeWordCustomAliases = _preferences.WakeWordAliases;
        SavePreferences();
        _settingsView.SetWakeWordAliases(_preferences.WakeWordAliases);
        _settingsView.SetWakeWordTestStatus($"Alias “{alias}” guardado.", isSuccess: true);
        await ApplyWakeWordPreferenceAsync(showCapsule: false);
    }

    private async Task ClearWakeWordAliasesAsync()
    {
        _preferences.WakeWordAliases.Clear();
        _voiceCoordinator.WakeWordCustomAliases = [];
        SavePreferences();
        _settingsView.SetWakeWordAliases(_preferences.WakeWordAliases);
        _settingsView.SetWakeWordTestStatus("Aliases personales eliminados.", isSuccess: true);
        await ApplyWakeWordPreferenceAsync(showCapsule: false);
    }

    private async Task RestartWakeWordAsync()
    {
        if (!_preferences.WakeWordEnabled)
        {
            _capsuleWindow.ShowMessage(
                CapsuleKind.Information,
                "Voz desactivada",
                "Activa una frase desde Personalización → Voz.",
                _preferences.Position);
            return;
        }

        await PauseWakeWordAsync();
        await ApplyWakeWordPreferenceAsync(showCapsule: false);
        _capsuleWindow.ShowMessage(
            _voiceCoordinator.IsWakeWordListening ? CapsuleKind.Success : CapsuleKind.Warning,
            _voiceCoordinator.IsWakeWordListening ? "Voz reiniciada" : "Voz no disponible",
            _voiceCoordinator.IsWakeWordListening
                ? $"Esperando “{_preferences.WakeWordPhrase.ToSpokenText()}”."
                : "Revisa el micrófono y el diagnóstico.",
            _preferences.Position);
    }

    private static string GetWakeWordMatchLabel(WakeWordMatchKind kind) => kind switch
    {
        WakeWordMatchKind.Phonetic => "pronunciación española",
        WakeWordMatchKind.Approximate => "coincidencia aproximada",
        WakeWordMatchKind.CustomAlias => "alias personal",
        WakeWordMatchKind.Legacy => "frase heredada",
        _ => "coincidencia exacta"
    };

    private async Task HandleWakeWordDetectedAsync(WakeWordDetectedEventArgs e)
    {
        if (_isClosed || !_preferences.WakeWordEnabled)
        {
            return;
        }

        if (_wakeWordTestActive)
        {
            _wakeWordTestActive = false;
            _wakeWordTestCancellation?.Cancel();
            _settingsView.SetWakeWordTestStatus(
                $"Detecté “{e.RecognizedText}” como {GetWakeWordMatchLabel(e.MatchKind)}. La frase funciona.",
                isSuccess: true);
            _capsuleWindow.ShowMessage(
                CapsuleKind.Success,
                "Frase detectada",
                e.RecognizedText,
                _preferences.Position);
            await ApplyWakeWordPreferenceAsync(showCapsule: false);
            return;
        }

        RememberForegroundWindow();
        await using var voiceScope = await _voiceCoordinator.AcquireVoiceInputScopeAsync();
        try
        {
            await PauseWakeWordAsync();
            _voiceCoordinator.StopSpeaking();

            if (!_voiceCoordinator.IsVoiceInputReady)
            {
                await PrepareVoiceAsync();
                if (!_voiceCoordinator.IsVoiceInputReady)
                {
                    return;
                }
            }

            _assistantView.SetVoiceState(
                AssistantVoiceState.Listening,
                $"{e.Phrase.ToSpokenText()} detectado. Habla con calma; no cortaré las pausas breves.");
            // Diseño D74 — el halo sustituye a la cápsula "Te escucho". Salían las dos a la vez,
            // una arriba y otra abajo, diciendo lo mismo con distinta voz. El halo lo dice mejor:
            // no solo avisa de que escucha, sino de que te está oyendo a ti.
            ShowVoiceHalo();

            var result = await voiceScope.ListenForUtteranceAsync(
                maximumDuration: TimeSpan.FromSeconds(20),
                trailingSilence: TimeSpan.FromMilliseconds(1_500),
                initialPcmAudio: e.PreRollAudio,
                initialSpeechPcmAudio: e.PostWakeAudio,
                cancellationToken: _lifetimeCancellation.Token);

            _assistantView.SetVoiceState(
                AssistantVoiceState.Processing,
                "Transcribiendo localmente con Whisper…");
            await HandleVoiceRecognitionResultAsync(result);
        }
        catch (OperationCanceledException)
        {
            if (!_isClosed)
            {
                _assistantView.SetVoiceState(
                    AssistantVoiceState.Idle,
                    "La escucha fue cancelada.");
            }
        }
        finally
        {
            // Se apaga aquí y no en cada camino de salida: el halo tiene que irse tanto si hubo
            // respuesta como si se canceló, se agotó el tiempo o falló la transcripción.
            _voiceHaloWindow.HideHalo();
            await ResumeWakeWordIfEnabledAsync();
        }
    }

    /// <summary>
    /// Diseño D72 — enciende el halo con la marca y el acento vigentes.
    ///
    /// La geometría y el color se pasan desde aquí para que la ventana del halo no tenga que
    /// conocer los diccionarios de recursos: recibe qué dibujar, no dónde buscarlo.
    /// </summary>
    private void ShowVoiceHalo()
    {
        if (FindResource("IconSakuraMark") is not Geometry mark ||
            FindResource("BrushAccent") is not SolidColorBrush accent)
        {
            return;
        }

        _voiceHaloWindow.ShowHalo(mark, accent.Color);
    }

    private async Task HandleVoiceRecognitionResultAsync(VoiceRecognitionResult result)
    {
        // Diseño D74 — decir "nada" cierra la escucha sin hacer nada. Es lo que uno le diría a una
        // persona al arrepentirse a mitad de frase, y hasta ahora la única salida era esperar el
        // silencio y arriesgarse a que Sakura entendiera algo.
        if (result.IsRecognized && VoiceDismissalPolicy.IsDismissal(result.Text))
        {
            _assistantView.SetVoiceState(AssistantVoiceState.Idle, "Listo, no hago nada.");
            return;
        }

        if (!result.IsRecognized)
        {
            _assistantView.SetVoiceState(AssistantVoiceState.Error, result.Detail);
            _capsuleWindow.ShowMessage(
                CapsuleKind.Warning,
                "No entendí la orden",
                result.Detail,
                _preferences.Position);
            return;
        }

        _assistantView.SetVoiceState(
            AssistantVoiceState.Idle,
            $"Entendí: “{result.Text}”");

        var normalizedDecision =
            SpanishVoiceTranscriptNormalizer.Normalize(result.Text);
        if (!string.IsNullOrWhiteSpace(_pendingVoicePrompt) &&
            (IsVoiceConfirmation(normalizedDecision) ||
             IsVoiceCancellation(normalizedDecision)))
        {
            await ProcessPromptAsync(result.Text, fromVoice: true);
            return;
        }

        if (result.RequiresConfirmation)
        {
            _pendingVoicePrompt = result.Text;
            var question =
                $"Escuché “{result.Text}”, pero no estoy totalmente seguro. " +
                "Di “Sakura, confirmar”, repite la orden o di “Sakura, cancelar”.";

            _assistantView.AddSakuraMessage(question);
            _capsuleWindow.ShowMessage(
                CapsuleKind.Warning,
                "¿Confirmas la orden?",
                result.Text,
                _preferences.Position);
            return;
        }

        _pendingVoicePrompt = null;
        _capsuleWindow.ShowMessage(
            CapsuleKind.Information,
            "Te escuché",
            result.Text,
            _preferences.Position);

        await ProcessPromptAsync(result.Text, fromVoice: true);
    }

    private async Task ApplyWakeWordPreferenceAsync(bool showCapsule)
    {
        await using var wakeWordScope = await _voiceCoordinator.AcquireWakeWordScopeAsync();
        try
        {
            await wakeWordScope.StopListeningAsync();
            SetWakeWordIndicator(active: false);
            RefreshRuntimeDashboard();

            if (!_preferences.WakeWordEnabled || _isClosed || _voiceCoordinator.IsVoiceInputListening)
            {
                return;
            }

            if (_preferences.PauseWakeWordInGameMode && _resourceDecision.PauseWakeWord)
            {
                SetWakeWordIndicator(active: false);
                _assistantView.SetVoiceAvailability(
                    _voiceCoordinator.IsVoiceInputReady,
                    "Activación por voz pausada por Modo Juego.");
                return;
            }

            var requiresDownload = !_voiceCoordinator.IsWakeWordReady;
            var progress = new Progress<VoicePreparationProgress>(update =>
            {
                WakeWordIndicatorText.Text = "Preparando voz";
                WakeWordIndicator.Visibility = Visibility.Visible;
                _assistantView.SetVoiceAvailability(
                    _voiceCoordinator.IsVoiceInputReady,
                    update.Detail);
            });

            var preparation = await _voiceCoordinator.PrepareWakeWordAsync(
                progress,
                _lifetimeCancellation.Token);

            if (!preparation.IsReady)
            {
                SetWakeWordIndicator(active: false);
                _assistantView.SetVoiceAvailability(
                    _voiceCoordinator.IsVoiceInputReady,
                    preparation.Detail);
                if (showCapsule && !_isClosed)
                {
                    _capsuleWindow.ShowMessage(
                        CapsuleKind.Error,
                        "Activación no disponible",
                        preparation.Detail,
                        _preferences.Position);
                }
                return;
            }

            if (!_preferences.WakeWordEnabled || _isClosed)
            {
                SetWakeWordIndicator(active: false);
                return;
            }

            _voiceCoordinator.WakeWordSensitivity = _preferences.WakeWordSensitivity;
            _voiceCoordinator.WakeWordCustomAliases = _preferences.WakeWordAliases;
            var start = await wakeWordScope.StartListeningAsync(
                _preferences.WakeWordPhrase,
                _lifetimeCancellation.Token);

            if (!start.IsAvailable)
            {
                SetWakeWordIndicator(active: false);
                _assistantView.SetVoiceAvailability(
                    _voiceCoordinator.IsVoiceInputReady,
                    start.Detail);
                if (showCapsule && !_isClosed)
                {
                    _capsuleWindow.ShowMessage(
                        CapsuleKind.Error,
                        "No pude escuchar Sakura",
                        start.Detail,
                        _preferences.Position);
                }
                return;
            }

            SetWakeWordIndicator(active: true);
            RefreshRuntimeDashboard();
            _assistantView.SetVoiceAvailability(
                _voiceCoordinator.IsVoiceInputReady,
                $"Di “{_preferences.WakeWordPhrase.ToSpokenText()}” y la orden de corrido, o espera “Te escucho”.");

            if (showCapsule && !_isClosed)
            {
                _capsuleWindow.ShowMessage(
                    CapsuleKind.Success,
                    "Activación por voz lista",
                    $"Puedes decir “{_preferences.WakeWordPhrase.ToSpokenText()}, abre PowerShell” de corrido.",
                    _preferences.Position);
            }
            else if (requiresDownload && !_isClosed)
            {
                _capsuleWindow.ShowMessage(
                    CapsuleKind.Success,
                    "Detector local instalado",
                    "La frase de activación ya funciona sin cuenta ni clave.",
                    _preferences.Position);
            }
        }
        catch (OperationCanceledException)
        {
            // Nexo se está cerrando o la preparación fue cancelada.
        }

        // El ámbito de wake word se libera aquí, al salir del método (igual que el
        // antiguo finally): la sección crítica conserva exactamente la misma duración.
    }

    private async Task PauseWakeWordAsync()
    {
        await using var wakeWordScope = await _voiceCoordinator.AcquireWakeWordScopeAsync();
        await wakeWordScope.StopListeningAsync();
        SetWakeWordIndicator(active: false);
    }

    private Task ResumeWakeWordIfEnabledAsync()
    {
        return _preferences.WakeWordEnabled &&
               !_isClosed &&
               !(_preferences.PauseWakeWordInGameMode && _resourceDecision.PauseWakeWord)
            ? ApplyWakeWordPreferenceAsync(showCapsule: false)
            : Task.CompletedTask;
    }

    private void SetWakeWordIndicator(bool active)
    {
        WakeWordIndicator.Visibility = active
            ? Visibility.Visible
            : Visibility.Collapsed;
        WakeWordIndicatorText.Text = active
            ? $"{_preferences.WakeWordPhrase.ToSpokenText()} atento"
            : "Voz pausada";
    }

    private async void RoutinesView_ExecuteRequested(
        object? sender,
        RoutineRequestedEventArgs e)
    {
        var routine = _routineManager.GetAll()
            .FirstOrDefault(candidate => candidate.Id == e.RoutineId);
        if (routine is not null)
        {
            await RunRoutineAsync(routine);
        }
    }

    private async Task ExecuteRoutineCommandAsync(RoutineCommand command)
    {
        switch (command.Type)
        {
            case RoutineCommandType.OpenRoutines:
                ShowAnimated();
                NavigateTo("Routines", animate: true);
                _assistantView.AddSakuraMessage("Abrí el módulo de rutinas.");
                return;

            case RoutineCommandType.ListRoutines:
                var available = _routineManager.GetAll()
                    .Where(routine => routine.IsEnabled)
                    .Select(routine => $"• {routine.Name}: “{routine.TriggerPhrase}”")
                    .ToArray();
                _assistantView.AddSakuraMessage(
                    available.Length == 0
                        ? "No hay rutinas activas."
                        : "Rutinas disponibles:" + Environment.NewLine + string.Join(Environment.NewLine, available));
                _capsuleWindow.ShowMessage(
                    CapsuleKind.Information,
                    "Rutinas disponibles",
                    available.Length == 0 ? "No hay rutinas activas." : $"{available.Length} rutinas activas.",
                    _preferences.Position);
                return;

            case RoutineCommandType.RunRoutine:
                var routine = _routineManager.FindBestMatch(command.RoutineName);
                if (routine is null)
                {
                    _assistantView.AddSakuraMessage(
                        $"No encontré una rutina que coincida con “{command.RoutineName}”.");
                    _capsuleWindow.ShowMessage(
                        CapsuleKind.Warning,
                        "Rutina no encontrada",
                        command.RoutineName,
                        _preferences.Position);
                    return;
                }

                await RunRoutineAsync(routine);
                return;
        }
    }

    private async Task RunRoutineAsync(RoutineDefinition routine)
    {
        if (!routine.IsEnabled)
        {
            _assistantView.AddSakuraMessage($"La rutina {routine.Name} está desactivada.");
            return;
        }

        // La aprobación es por ejecución y se pasa explícitamente al runner. Crear la rutina
        // no concede permiso permanente para ejecutar comandos arbitrarios (defecto D2).
        var approval = RoutineExecutionApproval.NotConfirmed;

        if (AutomationPermissionPolicy.RequiresConfirmation(routine))
        {
            var preview = string.Join(
                Environment.NewLine,
                routine.Steps
                    .Where(step => step.IsEnabled)
                    .Select((step, index) => $"{index + 1}. {DescribeAutomationAction(step)}"));
            var decision = MessageBox.Show(
                $"Sakura ejecutará estas acciones:" + Environment.NewLine + Environment.NewLine + preview,
                $"Ejecutar {routine.Name}",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (decision != MessageBoxResult.Yes)
            {
                _assistantView.AddSakuraMessage($"Cancelé la rutina {routine.Name}.");
                return;
            }

            approval = RoutineExecutionApproval.ConfirmedByUser;
        }

        _capsuleWindow.ShowMessage(
            CapsuleKind.Processing,
            $"Ejecutando {routine.Name}",
            $"{routine.Steps.Count(step => step.IsEnabled)} acciones permitidas",
            _preferences.Position);

        try
        {
            var report = await _routineRunner.RunAsync(
                routine,
                approval,
                _lifetimeCancellation.Token);
            // Diseño D3: el runner no conoce el registro de rutinas (no le corresponde); se
            // registra aquí, justo después, con el resultado real de la ejecución.
            _routineManager.RecordExecution(routine.Id, report.CompletedAt, report.Succeeded);
            _assistantView.AddSakuraMessage(report.BuildSummary());
            _tasksView.Refresh();
            _focusView.Refresh(DateTimeOffset.Now);
            await _audioView.RefreshAsync(force: true);
            _routinesView.Refresh();
            RefreshHomeView();
            _dailyFlowHub.RaiseRoutinesChanged();

            _capsuleWindow.ShowMessage(
                report.Succeeded ? CapsuleKind.Success : CapsuleKind.Warning,
                report.Succeeded ? "Rutina completada" : "Rutina completada con avisos",
                $"{report.SucceededCount} de {report.Results.Count} acciones listas.",
                _preferences.Position,
                TimeSpan.FromSeconds(8));
            SpeakVoiceResult(
                report.Succeeded
                    ? $"La rutina {routine.Name} terminó correctamente."
                    : $"La rutina {routine.Name} terminó con algunos avisos.");
        }
        catch (OperationCanceledException)
        {
            if (!_isClosed)
            {
                _assistantView.AddSakuraMessage($"La rutina {routine.Name} fue cancelada.");
            }
        }
    }

    private static string DescribeAutomationAction(AutomationAction action) => action.Type switch
    {
        AutomationActionType.OpenApplication => $"Abrir {action.Target}",
        AutomationActionType.OpenFolder => $"Abrir carpeta {action.WorkingDirectory}",
        AutomationActionType.OpenTerminal => $"Abrir PowerShell en {action.WorkingDirectory}",
        AutomationActionType.SetApplicationVolume => $"Poner {action.Target} al {action.NumericValue:0}%",
        AutomationActionType.MuteApplication => $"Silenciar {action.Target}",
        AutomationActionType.UnmuteApplication => $"Activar el sonido de {action.Target}",
        AutomationActionType.StartFocus => $"Iniciar enfoque por {action.NumericValue:0} minutos",
        AutomationActionType.StartBreak => $"Iniciar descanso por {action.NumericValue:0} minutos",
        AutomationActionType.CreateTask => $"Crear tarea: {action.Text}",
        _ => "Acción no permitida"
    };

    private void FocusView_FocusChanged(object? sender, EventArgs e)
    {
        CheckFocusTimer();
        RefreshHomeView();
    }

    private void CheckFocusTimer()
    {
        var now = DateTimeOffset.Now;
        var completion = _focusManager.CollectCompletion(now);
        _focusView.Refresh(now);
        RefreshHomeView();
        _focusContinuity.Refresh(now);

        // El mini temporizador no se muestra dentro de la propia sección Enfoque: mostraría el
        // mismo estado dos veces en la misma pantalla.
        if (_currentDestination.Equals(ShellNavigationPolicy.Focus, StringComparison.OrdinalIgnoreCase))
        {
            FocusMiniTimerControl.Visibility = Visibility.Collapsed;
        }

        _dailyFlowHub.RaiseFocusChanged();

        if (completion is null)
        {
            return;
        }

        var detail = completion.Kind == FocusSessionKind.Break
            ? "Tu descanso terminó."
            : $"Terminaste {completion.Label.ToLowerInvariant()}.";
        var notificationTitle = completion.Kind == FocusSessionKind.Break
            ? "Descanso terminado"
            : "Sesión completada";
        _capsuleWindow.ShowMessage(
            CapsuleKind.Success,
            notificationTitle,
            detail,
            _preferences.Position,
            TimeSpan.FromSeconds(8));
        _trayIcon.Notify(
            notificationTitle,
            detail,
            TrayNotificationKind.Success,
            _preferences.ShowWindowsNotifications,
            _preferences.PlayNotificationSounds);
        SpeakVoiceResult(detail);

        // Diseño D3.1: el aviso no modal de fin de sesión se muestra para toda finalización
        // natural, con tarea asociada o sin ella — no solo cuando hay una tarea que ofrecer
        // completar. El capsule/tray de arriba es un aviso ambiental transitorio (se ve desde
        // cualquier sección); este otro vive en Enfoque con las acciones reales (completar tarea,
        // iniciar otra sesión, cerrar).
        _focusView.ShowSessionCompletionNotice(completion);
    }

    /// <summary>
    /// Diseño D4 — punto único de refresco del Sakura Pill Host, igual que
    /// <see cref="CheckFocusTimer"/> lo es para el mini temporizador. Se llama después de cualquier
    /// mutación de <see cref="_ambientRequestManager"/> (comandos del Command Center, botones de la
    /// propia ventana a través de <see cref="_ambientCoordinator"/>).
    /// </summary>
    private void CheckAmbientRequest() => _ambientCoordinator.Refresh();

    private void AmbientCoordinator_QuickActionInvoked(object? sender, string actionId)
    {
        if (!string.Equals(actionId, "copy", StringComparison.Ordinal))
        {
            return;
        }

        var text = _ambientRequestManager.GetSnapshot(recentCount: 0).ActiveRequest?.Result?.ShortText;
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        try
        {
            Clipboard.SetText(text);
        }
        catch (Exception exception) when (
            exception is COMException or ExternalException)
        {
            // El portapapeles puede estar bloqueado por otro proceso; no es un fallo de Sakura.
        }
    }

    /// <summary>
    /// Diseño D4 — comando inicial del Command Center para el Sakura Pill Host: captura un
    /// Context Snapshot real de la última ventana externa en primer plano
    /// (<see cref="_ambientForegroundTracker"/>, actualizado en tiempo real) y recorre el ciclo
    /// completo Escuchando → Pensando → Resultado/Error. No hay procesamiento de IA todavía (eso
    /// llega con Lens/Flow, Fases 2-3): el "resultado" es honesto sobre lo que puede observar hoy —
    /// título y proceso de la ventana activa.
    /// </summary>
    private async Task<CommandExecutionResult> ExecuteAmbientContextPeekAsync()
    {
        var now = DateTimeOffset.Now;
        var context = _ambientContextProvider.Capture(_ambientForegroundTracker.LastExternalWindowHandle);
        var beginResult = _ambientRequestManager.Begin("¿Qué ventana tengo activa?", context, now);
        if (!beginResult.Success)
        {
            return CommandExecutionResult.Failure(beginResult.Message);
        }

        CheckAmbientRequest();
        _ambientRequestManager.BeginThinking(DateTimeOffset.Now);
        CheckAmbientRequest();

        await Task.Delay(TimeSpan.FromMilliseconds(450));

        if (context is null || string.IsNullOrWhiteSpace(context.WindowTitle))
        {
            var message = context is { IsSensitive: true }
                ? "Esa ventana está marcada como sensible; Sakura no expone su título."
                : "No pude identificar una ventana activa distinta de Sakura.";
            _ambientRequestManager.Fail(message, DateTimeOffset.Now);
        }
        else
        {
            var shortText = string.IsNullOrWhiteSpace(context.ProcessName)
                ? context.WindowTitle
                : $"{context.WindowTitle} — {context.ProcessName}";
            var result = new AmbientRequestResult(
                shortText,
                $"Proceso: {context.ProcessName}\nTítulo completo: {context.WindowTitle}",
                [new AmbientQuickAction("copy", "Copiar", AmbientAutonomyLevel.Ver)],
                CanUndo: false);
            _ambientRequestManager.CompleteWithResult(result, DateTimeOffset.Now);
        }

        CheckAmbientRequest();
        return CommandExecutionResult.Success();
    }

    /// <summary>
    /// Diseño D5.5/D5.6 (Fase 2 — Sakura Lens) — captura la ventana activa, la procesa con OCR y
    /// UI Automation (ambos redactados de contenido sensible antes de usarse en cualquier lugar,
    /// incluida la propia imagen — ver <see cref="SensitiveContentRedactor"/>/<see cref="ImageRedactor"/>),
    /// arma el contexto del modo elegido y pregunta a la IA. El resultado se muestra en el mismo
    /// Sakura Pill Host de D4 — Lens es, en esencia, otra fuente de solicitudes ambientales, no una
    /// superficie nueva. El indicador "Mirando" (<see cref="LensIndicator"/>) permanece visible
    /// mientras dura la captura y el análisis, nunca más — el modelo de confianza exige que ese
    /// paso sea siempre visible, nunca silencioso.
    /// </summary>
    private async Task<CommandExecutionResult> ExecuteLensAsync(LensMode mode)
    {
        var now = DateTimeOffset.Now;
        var context = _ambientContextProvider.Capture(_ambientForegroundTracker.LastExternalWindowHandle);

        var beginResult = _ambientRequestManager.Begin(
            $"Sakura Lens — {LensModeLabel(mode)}", context, now);
        if (!beginResult.Success)
        {
            return CommandExecutionResult.Failure(beginResult.Message);
        }

        CheckAmbientRequest();
        _ambientRequestManager.BeginThinking(DateTimeOffset.Now);
        CheckAmbientRequest();

        LensIndicator.Visibility = Visibility.Visible;
        try
        {
            var windowHandle = _ambientForegroundTracker.LastExternalWindowHandle;
            if (context is null || context.IsSensitive || windowHandle == 0)
            {
                var message = context is { IsSensitive: true }
                    ? "Esa ventana está marcada como sensible; Sakura Lens no la observa."
                    : "No pude identificar una ventana activa distinta de Sakura.";
                _ambientRequestManager.Fail(message, DateTimeOffset.Now);
                return CommandExecutionResult.Success();
            }

            var target = new VisionCaptureTarget(
                $"window:{windowHandle}",
                windowHandle,
                context.WindowTitle ?? "Ventana activa",
                context.ProcessName ?? string.Empty,
                VisionCaptureKind.Window,
                0, 0, 0, 0);

            var captureResult = await _screenCaptureService.CaptureAsync(target);
            if (!captureResult.IsSuccess || captureResult.PngBytes is null)
            {
                _ambientRequestManager.Fail(
                    $"No pude capturar la ventana: {captureResult.Detail}", DateTimeOffset.Now);
                return CommandExecutionResult.Success();
            }

            var ocrResult = await _lensOcrService.RecognizeAsync(captureResult.PngBytes);
            var uiaSnapshot = _lensUiAutomationReader.Read(windowHandle);

            var redactedOcr = SensitiveContentRedactor.Redact(ocrResult);
            var redactedElements = SensitiveContentRedactor.Redact(uiaSnapshot.Elements);
            var sensitiveLines = SensitiveContentRedactor.FindSensitiveLines(ocrResult);
            var imageBytes = ImageRedactor.RedactRegions(captureResult.PngBytes, sensitiveLines);

            var lensContext = LensContextBuilder.Build(
                mode,
                context.WindowTitle ?? captureResult.Title,
                redactedOcr,
                redactedElements);

            var image = AiImageAttachment.FromBytes(imageBytes);
            var messages = new[]
            {
                new ConversationMessage(ConversationRole.User, lensContext.Prompt, DateTimeOffset.Now)
            };

            var aiRequest = new AiChatRequest(
                messages,
                NexoAiInstructions.Default,
                lensContext.SystemContext,
                [image],
                lensContext.RequestMode);

            // Diseño D7 — se transmite por partes en vez de esperar la respuesta completa: el
            // usuario ve el texto aparecer conforme la IA lo escribe, que es lo que pidió tras
            // probar Lens. Si el proveedor falla a mitad, lo ya recibido no se descarta en
            // silencio: se conserva como resultado parcial y se avisa.
            _ambientRequestManager.BeginStreaming(DateTimeOffset.Now);
            CheckAmbientRequest();

            var streamed = new StringBuilder();
            string? streamFailure = null;
            try
            {
                await foreach (var chunk in _aiChatService.StreamAsync(
                    BuildAiConfiguration(), aiRequest, _lifetimeCancellation.Token))
                {
                    if (string.IsNullOrEmpty(chunk))
                    {
                        continue;
                    }

                    streamed.Append(chunk);
                    _ambientRequestManager.AppendStreamedText(chunk, DateTimeOffset.Now);
                    CheckAmbientRequest();
                }
            }
            catch (OperationCanceledException) when (!_lifetimeCancellation.IsCancellationRequested)
            {
                // Diseño D63 — el corte de noventa segundos del cliente de IA llega hasta aquí como
                // cancelación, y no es la nuestra: Sakura no se está cerrando. Excluirla dejaba la
                // solicitud en "Pensando…" para siempre, que es el fallo que Adler reportó. Cuando
                // sí es nuestro cierre la excepción sigue subiendo, porque entonces no hay nada
                // que enseñarle a nadie.
                streamFailure = "La respuesta tardó demasiado y Sakura dejó de esperarla.";
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                streamFailure = exception.Message;
            }

            if (streamed.Length == 0)
            {
                _ambientRequestManager.Fail(
                    string.IsNullOrWhiteSpace(streamFailure)
                        ? "No pude analizar la ventana activa."
                        : streamFailure,
                    DateTimeOffset.Now);
            }
            else
            {
                var answer = streamed.ToString();
                _ambientRequestManager.CompleteStreamedResult(
                    [], canUndo: false, DateTimeOffset.Now, SummarizeForCapsule);
                ShowLensHighlights(answer, redactedOcr, redactedElements, uiaSnapshot);

                if (streamFailure is not null)
                {
                    ShowFlowNotice(
                        CapsuleKind.Warning,
                        "Respuesta incompleta",
                        "La conexión se cortó mientras respondía; lo que alcanzó a escribir sigue visible.");
                }
            }
        }
        catch (OperationCanceledException) when (!_lifetimeCancellation.IsCancellationRequested)
        {
            // Diseño D63 — lo mismo que arriba, para la parte de antes del streaming: capturar,
            // OCR y UI Automation también pueden acabar en una cancelación que no es el cierre de
            // Sakura, y dejarla pasar volvía a colgar la píldora.
            _ambientRequestManager.Fail(
                "La respuesta tardó demasiado y Sakura dejó de esperarla.", DateTimeOffset.Now);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Sin esto, cualquier falla en captura/OCR/UI Automation/redacción (todo lo que pasa
            // ANTES del try/catch de streaming de más abajo) dejaba la solicitud ambiental
            // atascada en "Pensando" para siempre, porque SakuraCommandDescriptor.ExecuteAsync
            // atrapa la excepción a un nivel que no conoce el AmbientRequestManager. Reportado por
            // Adler: Lens en modo estudio se quedaba en "Pensando…" sin salir nunca de ahí.
            _ambientRequestManager.Fail(
                $"No pude analizar la ventana activa: {exception.Message}", DateTimeOffset.Now);
        }
        finally
        {
            LensIndicator.Visibility = Visibility.Collapsed;
        }

        CheckAmbientRequest();
        return CommandExecutionResult.Success();
    }

    /// <summary>
    /// Diseño D5.7 — resalta, sobre la ventana real observada, las regiones de OCR/UI Automation
    /// que la respuesta de la IA parece mencionar (ver <see cref="LensHighlightMatcher"/> para la
    /// heurística y sus límites). Se omite en silencio si la lectura de UI Automation falló o no
    /// tiene límites válidos: sin ellos no hay dónde posicionar la superposición con confianza.
    /// </summary>
    private void ShowLensHighlights(
        string answerText,
        OcrResult redactedOcr,
        IReadOnlyList<UiAutomationElement> redactedElements,
        UiAutomationSnapshot uiaSnapshot)
    {
        if (!uiaSnapshot.IsSuccess || uiaSnapshot.WindowWidth <= 0 || uiaSnapshot.WindowHeight <= 0)
        {
            return;
        }

        var regions = LensHighlightMatcher.FindMatches(
            answerText, redactedOcr, redactedElements, uiaSnapshot.WindowLeft, uiaSnapshot.WindowTop);

        _lensHighlightOverlay.ShowHighlights(
            uiaSnapshot.WindowLeft,
            uiaSnapshot.WindowTop,
            uiaSnapshot.WindowWidth,
            uiaSnapshot.WindowHeight,
            regions);
    }

    /// <summary>
    /// Diseño D6.3 (Fase 3 — Sakura Flow) — el atajo global funciona como interruptor: una pulsación
    /// empieza a dictar, la siguiente termina y escribe.
    ///
    /// No es "mantener presionado" por una razón concreta: <c>RegisterHotKey</c> solo avisa de la
    /// pulsación, nunca del soltado, así que un verdadero push-to-talk exigiría un hook de teclado
    /// de bajo nivel — la misma API que usan los registradores de teclas, que dispara falsos
    /// positivos de antivirus y encaja mal con el empaquetado para la Store. Además, el valor que
    /// el roadmap le atribuye a Flow es "escribir texto largo por voz", y sostener una tecla dos
    /// minutos es peor experiencia que alternar. El botón de micrófono que ya existía en el
    /// Asistente también es un interruptor, así que esto es consistente con lo que ya había.
    /// </summary>
    /// <summary>
    /// Diseño D7 — aplica el atajo global sin reiniciar Sakura: al activarlo desde Ajustes se
    /// registra en el momento, y al desactivarlo se libera para que otra aplicación pueda usar la
    /// combinación. Registrar dos veces el mismo id es inofensivo (Windows lo rechaza y se ignora),
    /// así que se desregistra siempre antes.
    /// </summary>
    private void ApplyFlowHotkeyRegistration()
    {
        var windowHandle = new WindowInteropHelper(this).Handle;
        if (windowHandle == IntPtr.Zero)
        {
            return;
        }

        UnregisterHotKey(windowHandle, FlowHotkeyId);

        if (_preferences.FlowEnabled &&
            !RegisterHotKey(windowHandle, FlowHotkeyId, ModControl | ModShift, VirtualKeyD))
        {
            _assistantView.AddSakuraMessage(
                "Ctrl + Shift + D ya está siendo utilizado por otra aplicación; el dictado global no quedó disponible.");
        }
    }

    private void ToggleFlowDictation()
    {
        if (_isClosed || !_preferences.FlowEnabled)
        {
            return;
        }

        var pendingStop = _flowStopSignal;
        if (pendingStop is not null)
        {
            // Ya hay un dictado en curso: esta pulsación lo cierra.
            pendingStop.TrySetResult(true);
            return;
        }

        _ = RunFlowDictationAsync();
    }

    private async Task RunFlowDictationAsync()
    {
        // La ventana destino se fija AQUÍ, antes de grabar nada: es la referencia contra la que el
        // insertor comprobará después que el foco no cambió.
        _flowTargetWindowHandle = _ambientForegroundTracker.LastExternalWindowHandle;

        var stopSignal = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _flowStopSignal = stopSignal;
        FlowIndicator.Visibility = Visibility.Visible;

        try
        {
            await using var voiceScope = await _voiceCoordinator.AcquireVoiceInputScopeAsync();
            await PauseWakeWordAsync();
            _voiceCoordinator.StopSpeaking();

            try
            {
                var startResult = await voiceScope.StartListeningAsync();
                if (!startResult.IsAvailable)
                {
                    ShowFlowNotice(CapsuleKind.Warning, "No pude escuchar", startResult.Detail);
                    return;
                }

                // El ámbito de voz debe sostenerse durante toda la sección crítica, así que se
                // espera aquí a la segunda pulsación en vez de repartir el ámbito entre dos
                // manejadores de mensajes distintos.
                await stopSignal.Task;

                var recognition = await voiceScope.StopListeningAsync(
                    VoiceTranscriptionMode.Dictation);

                HandleFlowDictationResult(recognition);
            }
            finally
            {
                await ResumeWakeWordIfEnabledAsync();
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException)
        {
            ShowFlowNotice(CapsuleKind.Error, "Dictado interrumpido", exception.Message);
        }
        finally
        {
            _flowStopSignal = null;
            FlowIndicator.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Diseño D6.3 — rama propia para el dictado: NO pasa por
    /// <c>HandleVoiceRecognitionResultAsync</c> ni por <c>ProcessPromptAsync</c>. Lo dictado es
    /// texto que la persona quiere escribir en otra aplicación, no una orden para Sakura; mezclarlo
    /// con el camino de comandos haría, por ejemplo, que dictar "abre Spotify" dentro de un correo
    /// abriera Spotify en vez de escribir esas palabras.
    /// </summary>
    private void HandleFlowDictationResult(VoiceRecognitionResult recognition)
    {
        if (!recognition.IsRecognized)
        {
            ShowFlowNotice(CapsuleKind.Warning, "No te entendí", recognition.Detail);
            return;
        }

        var options = new FlowDictationOptions(
            _preferences.FlowMode,
            FlowSettingsParser.ParseDictionary(_preferences.FlowDictionary),
            FlowSettingsParser.ParseSnippets(_preferences.FlowSnippets));

        var text = SpanishDictationNormalizer.Normalize(recognition.Text, options);
        var insertion = _flowTextInserter.Insert(text, _flowTargetWindowHandle);

        if (insertion.IsInserted)
        {
            ShowFlowNotice(CapsuleKind.Success, "Dictado escrito", SummarizeForCapsule(text));
            return;
        }

        HandleFlowInsertionFailure(insertion, text);
    }

    /// <summary>
    /// Diseño D6.3 — cuando no se pudo escribir, el texto dictado NO se tira. Se copia al
    /// portapapeles para que no se pierda el trabajo… salvo si la ventana destino era sensible: en
    /// ese caso lo dictado pudo ser una contraseña, y dejarla en el portapapeles (accesible a
    /// cualquier otro programa) sería peor que perderla.
    /// </summary>
    private void HandleFlowInsertionFailure(FlowInsertionResult insertion, string text)
    {
        if (insertion.Failure == FlowInsertionFailure.SensitiveWindow)
        {
            ShowFlowNotice(CapsuleKind.Warning, "Dictado descartado", insertion.Detail);
            return;
        }

        if (insertion.Failure == FlowInsertionFailure.EmptyText || string.IsNullOrEmpty(text))
        {
            ShowFlowNotice(CapsuleKind.Warning, "No te entendí", insertion.Detail);
            return;
        }

        try
        {
            Clipboard.SetText(text);
            ShowFlowNotice(
                CapsuleKind.Warning,
                "Copiado, no escrito",
                $"{insertion.Detail} Lo dejé en el portapapeles.");
        }
        catch (Exception exception) when (exception is COMException or ExternalException)
        {
            ShowFlowNotice(CapsuleKind.Error, "Dictado perdido", insertion.Detail);
        }
    }

    private void ShowFlowNotice(CapsuleKind kind, string title, string detail) =>
        _capsuleWindow.ShowMessage(kind, title, detail, _preferences.Position);

    private static string LensModeLabel(LensMode mode) => mode switch
    {
        LensMode.Soporte => "modo soporte",
        LensMode.Estudio => "modo estudio",
        LensMode.Desarrollo => "modo desarrollo",
        _ => mode.ToString()
    };

    /// <summary>
    /// Diseño D4.4 — abre (o refresca, si ya estaba abierto) el historial de solicitudes
    /// ambientales. Los datos ya existían desde D4.1 (<c>AmbientRequestManager.GetHistory()</c>,
    /// usados internamente para el archivado automático); esta es la primera superficie visible.
    /// </summary>
    private void ShowAmbientHistory()
    {
        if (_isClosed)
        {
            return;
        }

        if (_ambientHistoryWindow is null)
        {
            _ambientHistoryWindow = new AmbientHistoryWindow();
            _ambientHistoryWindow.UndoRequested += AmbientHistoryWindow_UndoRequested;
        }

        _ambientHistoryWindow.ShowFor(
            this,
            AmbientRequestHistorySummaryBuilder.Build(_ambientRequestManager.GetHistory()));
    }

    private void AmbientHistoryWindow_UndoRequested(object? sender, Guid requestId)
    {
        _ambientRequestManager.Undo(requestId, DateTimeOffset.Now);
        CheckAmbientRequest();
        _ambientHistoryWindow?.Apply(
            AmbientRequestHistorySummaryBuilder.Build(_ambientRequestManager.GetHistory()));
    }

    private Task ExecuteFocusCommandAsync(FocusCommand command)
    {
        FocusOperationResult? operation = null;
        string response;
        CapsuleKind capsuleKind;
        string capsuleTitle;

        switch (command.Type)
        {
            case FocusCommandType.OpenFocus:
                ShowAnimated();
                NavigateTo("Focus", animate: true);
                response = "Abrí el módulo de enfoque.";
                capsuleTitle = "Enfoque abierto";
                capsuleKind = CapsuleKind.Success;
                break;

            case FocusCommandType.Start when command.Duration.HasValue:
                operation = _focusManager.Start(
                    command.Duration.Value,
                    command.Label,
                    command.Kind,
                    DateTimeOffset.Now);
                response = operation.Message;
                capsuleTitle = operation.Success ? "Temporizador iniciado" : "No pude iniciar";
                capsuleKind = operation.Success ? CapsuleKind.Success : CapsuleKind.Warning;
                break;

            case FocusCommandType.Pause:
                operation = _focusManager.Pause(DateTimeOffset.Now);
                response = operation.Message;
                capsuleTitle = operation.Success ? "Temporizador en pausa" : "No pude pausar";
                capsuleKind = operation.Success ? CapsuleKind.Information : CapsuleKind.Warning;
                break;

            case FocusCommandType.Resume:
                operation = _focusManager.Resume(DateTimeOffset.Now);
                response = operation.Message;
                capsuleTitle = operation.Success ? "Temporizador reanudado" : "No pude continuar";
                capsuleKind = operation.Success ? CapsuleKind.Success : CapsuleKind.Warning;
                break;

            case FocusCommandType.Cancel:
                operation = _focusManager.Cancel();
                response = operation.Message;
                capsuleTitle = operation.Success ? "Temporizador cancelado" : "Nada que cancelar";
                capsuleKind = operation.Success ? CapsuleKind.Information : CapsuleKind.Warning;
                break;

            case FocusCommandType.Status:
                response = _focusManager.BuildStatus(DateTimeOffset.Now);
                capsuleTitle = "Estado del temporizador";
                capsuleKind = CapsuleKind.Information;
                break;

            default:
                response = "No pude interpretar esa instrucción de enfoque.";
                capsuleTitle = "Instrucción incompleta";
                capsuleKind = CapsuleKind.Warning;
                break;
        }

        _focusView.Refresh(DateTimeOffset.Now);
        _assistantView.AddSakuraMessage(response);
        _capsuleWindow.ShowMessage(
            capsuleKind,
            capsuleTitle,
            response,
            _preferences.Position);

        if (operation?.Success == true)
        {
            _homeView.AddRecentAction(capsuleTitle, response);
        }

        RefreshHomeView();
        SpeakVoiceResult(response);
        return Task.CompletedTask;
    }

    private void TasksView_TasksChanged(object? sender, EventArgs e)
    {
        CheckTaskReminders();
        RefreshHomeView();
        _dailyFlowHub.RaiseTasksChanged();
    }

    private void CheckTaskReminders()
    {
        var reminders = _taskManager.CollectDueReminders(DateTimeOffset.Now);
        RefreshHomeView();
        if (reminders.Count == 0)
        {
            return;
        }

        _tasksView.Refresh();
        var first = reminders[0];
        var detail = reminders.Count == 1
            ? first.Title
            : $"{first.Title} y {reminders.Count - 1} más";

        _capsuleWindow.ShowMessage(
            CapsuleKind.Information,
            "Recordatorio",
            detail,
            _preferences.Position,
            TimeSpan.FromSeconds(8));
        _trayIcon.Notify(
            "Recordatorio",
            detail,
            TrayNotificationKind.Information,
            _preferences.ShowWindowsNotifications,
            _preferences.PlayNotificationSounds);
    }

    private Task ExecuteTaskCommandAsync(TaskCommand command)
    {
        string response;
        CapsuleKind capsuleKind;
        string capsuleTitle;

        switch (command.Type)
        {
            case TaskCommandType.OpenTasks:
                ShowAnimated();
                NavigateTo("Tasks", animate: true);
                response = "Abrí tus tareas.";
                capsuleTitle = "Tareas abiertas";
                capsuleKind = CapsuleKind.Success;
                break;

            case TaskCommandType.ListToday:
                response = _taskManager.BuildTodaySummary(DateTimeOffset.Now);
                capsuleTitle = "Pendientes de hoy";
                capsuleKind = CapsuleKind.Information;
                break;

            case TaskCommandType.ListPending:
                response = _taskManager.BuildPendingSummary(DateTimeOffset.Now);
                capsuleTitle = "Tareas pendientes";
                capsuleKind = CapsuleKind.Information;
                break;

            case TaskCommandType.Create when !string.IsNullOrWhiteSpace(command.Title):
            {
                if (command.ReminderEnabled && !command.DueAt.HasValue)
                {
                    response = "Dime cuándo debo recordártelo, por ejemplo: mañana a las 8.";
                    capsuleTitle = "Falta la fecha";
                    capsuleKind = CapsuleKind.Warning;
                    break;
                }

                var task = _taskManager.Create(
                    command.Title,
                    dueAt: command.DueAt,
                    priority: command.Priority,
                    reminderEnabled: command.ReminderEnabled);
                _tasksView.Refresh();

                var schedule = task.DueAt.HasValue
                    ? task.DueAt.Value.ToString("ddd d MMM · HH:mm", new CultureInfo("es-MX"))
                    : "sin fecha";
                response = task.ReminderEnabled
                    ? $"Guardé el recordatorio “{task.Title}” para {schedule}."
                    : $"Agregué “{task.Title}” · {schedule}.";
                capsuleTitle = task.ReminderEnabled ? "Recordatorio guardado" : "Tarea agregada";
                capsuleKind = CapsuleKind.Success;
                break;
            }

            case TaskCommandType.Complete when !string.IsNullOrWhiteSpace(command.Title):
            {
                var result = _taskManager.CompleteMatching(command.Title);
                _tasksView.Refresh();
                response = result.Message;
                capsuleTitle = result.Success ? "Tarea completada" : "Tarea no encontrada";
                capsuleKind = result.Success ? CapsuleKind.Success : CapsuleKind.Warning;
                break;
            }

            case TaskCommandType.Delete when !string.IsNullOrWhiteSpace(command.Title):
            {
                var result = _taskManager.DeleteMatching(command.Title);
                _tasksView.Refresh();
                response = result.Message;
                capsuleTitle = result.Success ? "Tarea eliminada" : "Tarea no encontrada";
                capsuleKind = result.Success ? CapsuleKind.Success : CapsuleKind.Warning;
                break;
            }

            default:
                response = "No pude interpretar esa instrucción de tareas.";
                capsuleTitle = "Instrucción incompleta";
                capsuleKind = CapsuleKind.Warning;
                break;
        }

        _assistantView.AddSakuraMessage(response);
        _capsuleWindow.ShowMessage(
            capsuleKind,
            capsuleTitle,
            response.Replace("\n", " "),
            _preferences.Position);
        SpeakVoiceResult(response);
        return Task.CompletedTask;
    }

    private async Task ExecuteLocalCommandAsync(LocalCommandIntent intent)
    {
        switch (intent.Type)
        {
            case LocalCommandType.ShowPeek:
                if (!_preferences.PeekEnabled)
                {
                    _assistantView.AddSakuraMessage("La vista Peek está desactivada en Personalización.");
                    _capsuleWindow.ShowMessage(
                        CapsuleKind.Warning,
                        "Peek está desactivado",
                        "Puedes activarlo desde Personalización.",
                        _preferences.Position);
                    break;
                }

                await ShowPeekAsync();
                ShowCommandSuccess("Vista rápida abierta", "Peek muestra el estado actual del equipo.");
                break;

            case LocalCommandType.ShowSystemStatus:
                await ShowSystemStatusAsync();
                break;

            case LocalCommandType.ShowCurrentDate:
                ShowCurrentDate();
                break;

            case LocalCommandType.ShowCurrentTime:
                ShowCurrentTime();
                break;

            case LocalCommandType.CaptureForVision:
                await LookAtForegroundWindowAsync();
                break;

            case LocalCommandType.NavigateAssistant:
                ShowShellModule(ShellNavigationPolicy.Assistant, "Asistente abierto");
                break;

            case LocalCommandType.NavigateAudio:
                ShowShellModule(ShellNavigationPolicy.Audio, "Audio abierto");
                break;

            case LocalCommandType.NavigateCapture:
                ShowShellModule(ShellNavigationPolicy.Capture, "Captura abierta");
                break;

            case LocalCommandType.NavigateSystem:
                ShowShellModule(ShellNavigationPolicy.System, "Sistema abierto");
                break;

            case LocalCommandType.NavigateSettings:
                ShowShellModule(ShellNavigationPolicy.Settings, "Ajustes abiertos");
                break;

            case LocalCommandType.OpenPowerShell:
                OpenShell("powershell.exe", "-NoExit", "PowerShell abierto");
                break;

            case LocalCommandType.OpenCommandPrompt:
                OpenShell("cmd.exe", string.Empty, "CMD abierto");
                break;

            case LocalCommandType.OpenWindowsTerminal:
                OpenShell("wt.exe", string.Empty, "Terminal abierta");
                break;

            case LocalCommandType.OpenKnownFolder:
                OpenKnownFolder(intent.Target);
                break;

            case LocalCommandType.OpenKnownApplication:
                OpenKnownApplication(intent.Target);
                break;

            case LocalCommandType.SetApplicationVolume:
            case LocalCommandType.ScaleApplicationVolume:
            case LocalCommandType.ChangeApplicationVolume:
            case LocalCommandType.MuteApplication:
            case LocalCommandType.UnmuteApplication:
            case LocalCommandType.LowerAllExcept:
                await ExecuteAudioCommandAsync(intent);
                break;

            default:
                _assistantView.AddSakuraMessage("No pude ejecutar esa orden local todavía.");
                _capsuleWindow.ShowMessage(
                    CapsuleKind.Warning,
                    "Comando no disponible",
                    "La orden fue reconocida, pero aún no tiene una acción conectada.",
                    _preferences.Position);
                break;
        }
    }

    private void ShowCurrentDate()
    {
        var culture = new CultureInfo("es-MX");
        var date = DateTime.Now.ToString(
            "dddd d 'de' MMMM 'de' yyyy",
            culture);
        var response = $"Hoy es {date}.";

        _assistantView.AddSakuraMessage(response);
        _capsuleWindow.ShowMessage(
            CapsuleKind.Success,
            "Fecha actual",
            date,
            _preferences.Position);
        SpeakVoiceResult(response);
    }

    private void ShowCurrentTime()
    {
        var time = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture);
        var response = $"Son las {time}.";

        _assistantView.AddSakuraMessage(response);
        _capsuleWindow.ShowMessage(
            CapsuleKind.Success,
            "Hora actual",
            time,
            _preferences.Position);
        SpeakVoiceResult(response);
    }

    private async Task ShowSystemStatusAsync()
    {
        var snapshotAge = DateTimeOffset.Now - _latestSnapshot.CapturedAt;
        if (_latestSnapshot.CapturedAt == DateTimeOffset.MinValue || snapshotAge > TimeSpan.FromSeconds(4))
        {
            await RefreshMetricsAsync();
        }

        var topProcess = string.IsNullOrWhiteSpace(_latestSnapshot.TopProcessName)
            ? "sin proceso destacado"
            : $"{_latestSnapshot.TopProcessName} · {(_latestSnapshot.TopProcessWorkingSetBytes.GetValueOrDefault() / 1024d / 1024d):0} MB";

        var summary =
            $"CPU {FormatPercentage(_latestSnapshot.CpuUsagePercent)} · " +
            $"RAM {FormatPercentage(_latestSnapshot.MemoryUsagePercent)} · " +
            $"GPU {FormatPercentage(_latestSnapshot.GpuUsagePercent)}. " +
            $"Mayor uso de memoria: {topProcess}.";

        _assistantView.AddSakuraMessage(summary);
        _capsuleWindow.ShowMessage(
            CapsuleKind.Success,
            "Estado del equipo listo",
            $"CPU {FormatPercentage(_latestSnapshot.CpuUsagePercent)} · RAM {FormatPercentage(_latestSnapshot.MemoryUsagePercent)} · GPU {FormatPercentage(_latestSnapshot.GpuUsagePercent)}",
            _preferences.Position);
        SpeakVoiceResult(summary);
    }

    private void ShowShellModule(string destination, string confirmation)
    {
        ShowAnimated();
        NavigateTo(destination, animate: true);
        ShowCommandSuccess(confirmation, "La orden se ejecutó localmente.");
    }

    private void OpenKnownFolder(string? target)
    {
        var (argument, displayName) = target switch
        {
            "downloads" => ("shell:Downloads", "Descargas"),
            "documents" => (QuoteExplorerPath(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)), "Documentos"),
            "pictures" => (QuoteExplorerPath(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)), "Imágenes"),
            "desktop" => (QuoteExplorerPath(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)), "Escritorio"),
            "profile" => (QuoteExplorerPath(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)), "Carpeta personal"),
            _ => (string.Empty, string.Empty)
        };

        if (string.IsNullOrWhiteSpace(argument))
        {
            ShowLocalLaunchFailure("Carpeta no reconocida");
            return;
        }

        if (!TryStartProcess("explorer.exe", argument))
        {
            ShowLocalLaunchFailure(displayName);
            return;
        }

        _assistantView.AddSakuraMessage($"Abrí {displayName}.");
        ShowCommandSuccess($"{displayName} abierto", "La carpeta se abrió localmente.");
    }

    private static string QuoteExplorerPath(string path) =>
        string.IsNullOrWhiteSpace(path) ? string.Empty : $"\"{path}\"";

    private void OpenKnownApplication(string? target)
    {
        var opened = target switch
        {
            "vscode" => TryOpenVisualStudioCode(),
            "calculator" => TryStartProcess("calc.exe"),
            "taskmanager" => TryStartProcess("taskmgr.exe"),
            "explorer" => TryStartProcess("explorer.exe"),
            "windows-settings" => TryStartProcess("ms-settings:"),
            _ => false
        };

        var displayName = target switch
        {
            "vscode" => "Visual Studio Code",
            "calculator" => "Calculadora",
            "taskmanager" => "Administrador de tareas",
            "explorer" => "Explorador de archivos",
            "windows-settings" => "Configuración de Windows",
            _ => "Aplicación"
        };

        if (!opened)
        {
            ShowLocalLaunchFailure(displayName);
            return;
        }

        _assistantView.AddSakuraMessage($"Abrí {displayName}.");
        ShowCommandSuccess($"{displayName} abierto", "La acción se ejecutó localmente.");
    }

    private static bool TryOpenVisualStudioCode()
    {
        var candidates = new[]
        {
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs",
                "Microsoft VS Code",
                "Code.exe"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Microsoft VS Code",
                "Code.exe"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Microsoft VS Code",
                "Code.exe")
        };

        foreach (var candidate in candidates.Where(File.Exists))
        {
            if (TryStartProcess(candidate))
            {
                return true;
            }
        }

        return TryStartProcess("code");
    }

    private static bool TryStartProcess(string fileName, string arguments = "")
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or Win32Exception)
        {
            return false;
        }
    }

    private void ShowLocalLaunchFailure(string displayName)
    {
        _assistantView.AddSakuraMessage($"No pude abrir {displayName}.");
        _capsuleWindow.ShowMessage(
            CapsuleKind.Error,
            "No se pudo abrir",
            displayName,
            _preferences.Position);
    }

    private void OpenShell(string fileName, string arguments, string confirmation)
    {
        try
        {
            var userFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = userFolder,
                UseShellExecute = true
            });

            _assistantView.AddSakuraMessage($"{confirmation} en {userFolder}.");
            ShowCommandSuccess(confirmation, userFolder);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            _assistantView.AddSakuraMessage($"No pude abrir {fileName}.");
            _capsuleWindow.ShowMessage(
                CapsuleKind.Error,
                "No se pudo abrir",
                fileName,
                _preferences.Position);
        }
    }

    private async Task ExecuteAudioCommandAsync(LocalCommandIntent intent)
    {
        var result = await Task.Run(() => intent.Type switch
        {
            LocalCommandType.SetApplicationVolume =>
                _audioMixerService.SetApplicationVolume(
                    intent.Target ?? string.Empty,
                    intent.Percent.GetValueOrDefault()),

            LocalCommandType.ScaleApplicationVolume =>
                _audioMixerService.ScaleApplicationVolume(
                    intent.Target ?? string.Empty,
                    intent.Factor.GetValueOrDefault(0.5)),

            LocalCommandType.ChangeApplicationVolume =>
                _audioMixerService.ChangeApplicationVolume(
                    intent.Target ?? string.Empty,
                    intent.DeltaPoints.GetValueOrDefault()),

            LocalCommandType.MuteApplication =>
                _audioMixerService.SetApplicationMuted(
                    intent.Target ?? string.Empty,
                    muted: true),

            LocalCommandType.UnmuteApplication =>
                _audioMixerService.SetApplicationMuted(
                    intent.Target ?? string.Empty,
                    muted: false),

            LocalCommandType.LowerAllExcept =>
                _audioMixerService.LowerAllExcept(
                    intent.Target ?? string.Empty,
                    intent.Factor.GetValueOrDefault(0.5)),

            _ => AudioActionResult.Failed("La orden de audio no tiene una acción asociada.")
        });

        PresentAudioResult(result, addToConversation: true);
        await _audioView.RefreshAsync(force: true);
    }

    private void AudioView_ActionCompleted(object? sender, AudioActionEventArgs e)
    {
        PresentAudioResult(e.Result, addToConversation: false);
    }

    private void PresentAudioResult(AudioActionResult result, bool addToConversation)
    {
        if (addToConversation)
        {
            _assistantView.AddSakuraMessage(result.Detail);
        }

        var capsuleKind = result.Status switch
        {
            AudioActionStatus.Success => CapsuleKind.Success,
            AudioActionStatus.NotFound => CapsuleKind.Warning,
            AudioActionStatus.Unavailable => CapsuleKind.Warning,
            _ => CapsuleKind.Error
        };

        _capsuleWindow.ShowMessage(
            capsuleKind,
            result.Title,
            result.Detail,
            _preferences.Position);

        if (result.Status == AudioActionStatus.Success)
        {
            _homeView.AddRecentAction(result.Title, result.Detail);
        }

        SpeakVoiceResult(result.Detail);
    }

    private void ShowCommandSuccess(string title, string detail)
    {
        _capsuleWindow.ShowMessage(
            CapsuleKind.Success,
            title,
            detail,
            _preferences.Position);
        _homeView.AddRecentAction(title, detail);
        RefreshHomeView();
        SpeakVoiceResult(title);
    }

    private void SpeakVoiceResult(string text)
    {
        if (_voicePromptActive && _preferences.SpeakVoiceResponses)
        {
            _voiceCoordinator.Speak(text);
        }
    }

    private static string ToDisplayName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return char.ToUpperInvariant(value[0]) + value[1..];
    }

    private void NavigationButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string destination })
        {
            NavigateTo(destination, animate: true);
            if (_sideRailExpanded)
            {
                SetSideRailExpanded(expanded: false, animate: true);
            }
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var destination = ShellNavigationPolicy.ResolveSettingsToggle(
            _currentDestination,
            _previousDestination);

        _previousDestination = ShellNavigationPolicy.ResolvePreviousDestination(
            _currentDestination,
            _previousDestination);

        NavigateTo(destination, animate: true);
    }

    private async void PeekButton_Click(object sender, RoutedEventArgs e)
    {
        await ShowPeekAsync();
    }

    private void NavigateTo(string destination, bool animate)
    {
        if (!_views.TryGetValue(destination, out var view))
        {
            return;
        }

        _currentDestination = destination;
        ModuleHost.Content = view;
        UpdateNavigationState(destination);
        UpdateWorkspaceHeader(destination);

        // Diseño D3.1: el temporizador de Enfoque sigue corriendo cada segundo en segundo plano
        // (_focusTickTimer) sin importar qué sección esté activa, así que el dominio nunca se
        // atrasa. Pero antes, solo Inicio forzaba un refresco inmediato de sus propios controles
        // al navegar — Enfoque no lo hacía, así que su reloj visible podía quedarse hasta un
        // segundo desactualizado hasta el siguiente tick programado. Se llama al mismo punto
        // central que ya usa HandleSystemResume() (reanudar desde suspensión) para no duplicar
        // esta condición por cada uno de los nueve destinos: siempre sincroniza tanto Enfoque
        // como Inicio, sin crear un timer nuevo ni cambiar la fuente de verdad (los timestamps).
        CheckFocusTimer();

        if (!animate || !ShellAnimationsAllowed)
        {
            ModuleHost.Opacity = 1;
            ModuleHost.RenderTransform = Transform.Identity;
            FocusCurrentView();
            return;
        }

        // El módulo entra desde el lado por el que está el rail, así que el contenido parece salir
        // de la navegación que se acaba de pulsar. Con el shell acoplado a la izquierda el rail está
        // a la derecha, y entonces el sentido se invierte: entrar siempre desde el mismo lado
        // rompería esa relación de causa y efecto.
        var transform = new TranslateTransform();
        ModuleHost.RenderTransform = transform;
        ModuleHost.RenderTransformOrigin = new Point(0.5, 0.5);
        ModuleHost.EnterFrom(transform, RailIsOnLeft ? 18 : -18);

        FocusCurrentView();
    }

    private void FocusCurrentView()
    {
        if (_currentDestination == "Assistant")
        {
            _assistantView.FocusPrompt();
        }
        else if (_currentDestination == "Tasks")
        {
            _tasksView.FocusPrimaryControl();
        }
        else if (_currentDestination == "Focus")
        {
            _focusView.FocusPrimaryControl();
        }
        else if (_currentDestination == "Routines")
        {
            _routinesView.FocusPrimaryControl();
        }
    }

    /// <summary>
    /// Diseño D1 (Sakura Shell): el estado seleccionado nunca depende solo del color. Cada
    /// entrada combina superficie elevada (fondo), indicador tipo "tallo" a la izquierda,
    /// ícono con trazo más grueso, y texto con más peso — la misma combinación que se
    /// verifica en <c>AdaptiveEngineUiInvariantTests</c>-equivalentes de este sprint.
    ///
    /// Diseño D2.0: la cuarta señal era "ícono relleno en vez de solo trazo", pero rellenar
    /// geometrías de línea las convertía en bloques sólidos (y hacía desaparecer las que solo
    /// tienen segmentos). Ahora es el grosor del trazo, que preserva la silueta.
    /// </summary>
    private void UpdateNavigationState(string destination)
    {
        var items = new (string Key, Button Button, Border Indicator, System.Windows.Shapes.Path Icon, TextBlock Label)[]
        {
            ("Home", HomeNavButton, HomeNavIndicator, HomeNavIcon, HomeNavLabel),
            ("Assistant", AssistantNavButton, AssistantNavIndicator, AssistantNavIcon, AssistantNavLabel),
            ("Tasks", TasksNavButton, TasksNavIndicator, TasksNavIcon, TasksNavLabel),
            ("Focus", FocusNavButton, FocusNavIndicator, FocusNavIcon, FocusNavLabel),
            ("Routines", RoutinesNavButton, RoutinesNavIndicator, RoutinesNavIcon, RoutinesNavLabel),
            ("Audio", AudioNavButton, AudioNavIndicator, AudioNavIcon, AudioNavLabel),
            ("Capture", CaptureNavButton, CaptureNavIndicator, CaptureNavIcon, CaptureNavLabel),
            ("System", SystemNavButton, SystemNavIndicator, SystemNavIcon, SystemNavLabel),
            ("Settings", SettingsNavButton, SettingsNavIndicator, SettingsNavIcon, SettingsNavLabel)
        };

        foreach (var item in items)
        {
            var selected = item.Key.Equals(destination, StringComparison.OrdinalIgnoreCase);
            ApplyNavigationItemState(item.Button, item.Indicator, item.Icon, item.Label, selected);
        }
    }

    /// <summary>
    /// Diseño D36 — el elemento activo se señala distinto según el rail esté plegado o desplegado, y
    /// la regla está en <see cref="NavigationSelection"/> y no aquí: el rail se pliega desde varios
    /// sitios —el botón, restaurar apariencia, el arranque— y cada uno tendría que acordarse.
    /// </summary>
    private void ApplyNavigationItemState(Button button, Border indicator, System.Windows.Shapes.Path icon, TextBlock label, bool selected)
    {
        var style = NavigationSelection.For(selected, _sideRailExpanded);

        button.Background = style == NavigationSelectionStyle.Pill
            ? (Brush)FindResource("BrushAccentSoft")
            : Brushes.Transparent;
        button.Foreground = selected
            ? (Brush)FindResource("BrushAccent")
            : (Brush)FindResource("BrushTextSecondary");

        // Plegado, la barrita sobra: el brillo ya dice cuál es el activo, y las dos señales juntas
        // en una franja tan estrecha se estorban.
        indicator.Visibility = style == NavigationSelectionStyle.Pill
            ? Visibility.Visible
            : Visibility.Collapsed;

        icon.Style = (Style)FindResource(selected ? "SakuraNavigationIconSelectedStyle" : "SakuraNavigationIconStyle");
        label.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal;

        ApplyNavigationGlow(icon, style == NavigationSelectionStyle.Glow);
    }

    /// <summary>
    /// El brillo es una sombra proyectada sin desplazamiento y del color del acento, que es como se
    /// consigue un halo en WPF. Se aplica al icono y no al botón porque lo que tiene que brillar es
    /// el símbolo: puesto en el botón, el halo dibuja el rectángulo del botón y vuelve a ser una
    /// caja, justo lo que el boceto evita al plegar.
    /// </summary>
    private void ApplyNavigationGlow(System.Windows.Shapes.Path icon, bool glowing)
    {
        if (!glowing)
        {
            icon.Effect = null;
            return;
        }

        var accent = ((SolidColorBrush)FindResource("BrushAccent")).Color;

        // El halo se ensancha por encima del ancho del trazo del icono. Con dieciséis se quedaba
        // pegado a la línea y se leía como un icono de otro color, no como uno encendido; a
        // veintidós el resplandor se separa lo suficiente para verse como luz alrededor del
        // símbolo, que es lo que pide el boceto, sin llegar a pintar la caja del botón.
        icon.Effect = new DropShadowEffect
        {
            Color = accent,
            ShadowDepth = 0,
            BlurRadius = 22,
            Opacity = 0.95
        };
    }

    private void UpdateWorkspaceHeader(string destination)
    {
        var (title, subtitle) = destination switch
        {
            "Home" => ("Inicio", "Una vista breve de lo que importa ahora"),
            "Assistant" => ("Asistente", "Consulta, conversa o comparte contexto"),
            "Tasks" => ("Hoy", "Tareas, prioridades y recordatorios"),
            "Focus" => ("Enfoque", "Sesiones cortas sin perder el ritmo"),
            "Routines" => ("Rutinas", "Acciones repetibles, claras y controladas"),
            "Audio" => ("Audio", "Control local por aplicación"),
            "Capture" => ("Captura", "Selecciona qué puede ver Sakura"),
            "System" => ("Sistema", "Estado y diagnóstico del equipo"),
            "Settings" => ("Personalizar", "Apariencia, privacidad y comportamiento"),
            _ => ("Sakura", "Tu espacio de acciones y contexto")
        };

        WorkspaceTitleText.Text = title;
        WorkspaceSubtitleText.Text = subtitle;
    }

    private void RefreshHomeView()
    {
        // Diseño D3: el cálculo del resumen vive en DailyFlowSummaryBuilder (Nexo.App/DailyFlow),
        // no aquí — MainWindow solo reúne las entradas y aplica el resultado a la vista.
        var model = DailyFlowSummaryBuilder.BuildHomeDashboard(
            _taskManager,
            _focusManager,
            _routineManager.GetAll(),
            _lastExternalWindowHandle != 0,
            DateTimeOffset.Now);

        _homeView.Refresh(model);

        // El mismo resumen alimenta el cajón. Sale del mismo cálculo y no de otro propio: dos
        // cuentas separadas acabarían discrepando, y ver «3 pendientes» arriba y «2» abajo hace
        // dudar de las dos.
        _dashboardWindow.UpdateDailySummary(
            model.TaskValue,
            model.TaskDetail,
            model.FocusValue,
            model.FocusDetail,
            model.RoutineValue,
            model.RoutineDetail);
    }

    private void ApplyPreferences()
    {
        _preferences.Normalize();
        Width = _preferences.Width;

        // Un único punto donde el vocabulario de movimiento aprende si puede animar. Respeta tanto
        // la preferencia de Sakura como la de Windows (Accesibilidad → Efectos visuales): quien
        // apagó las animaciones del sistema no debería tener que apagarlas otra vez aquí.
        SakuraMotion.AnimationsEnabled = ShellAnimationsAllowed;

        _edgeRevealWatcher.Configure(_preferences.EdgeRevealEnabled, _preferences.Position);
        _quickControlsWatcher.Configure(
            _preferences.EdgeRevealEnabled,
            QuickControlsPolicy.ControlsEdgeFor(_preferences.Position));

        ApplyShellSide();

        // Aplica el tema y, dentro, refresca la opacidad del shell con el fondo ya teñido.
        ApplyEffectiveAccent();

        _voiceCoordinator.WakeWordSensitivity = _preferences.WakeWordSensitivity;
        ApplyModuleVisibility();
        _assistantView.SetVisionAvailability(_preferences.VisionEnabled);
    }

    /// <summary>
    /// Diseño D31 — punto único para decidir qué acento se ve. Cada origen puede fallar por motivos
    /// legítimos —Windows sin la clave en el registro, un fondo en blanco y negro del que no hay
    /// color que sacar— y en todos ellos se cae al color manual, que siempre existe. Eso es lo que
    /// permite prometer que activar cualquiera de las dos opciones nunca deja la aplicación peor de
    /// como estaba.
    /// </summary>
    private void ApplyEffectiveAccent()
    {
        var accent = _preferences.AccentSource switch
        {
            AccentSource.Windows => WindowsAccentColorReader.TryRead() ?? _preferences.AccentColor,
            AccentSource.Wallpaper => TryReadWallpaperAccent() ?? _preferences.AccentColor,
            _ => _preferences.AccentColor
        };

        ApplyAccent(accent);
    }

    private static string? TryReadWallpaperAccent()
    {
        if (WindowsWallpaperReader.TryReadPixels() is not { } pixels)
        {
            return null;
        }

        // El acento se elige contra el fondo neutro de partida y no contra el que hay pintado
        // ahora: si se midiera contra el actual, el tema ya teñido movería el listón y el resultado
        // dependería de qué tema estaba puesto antes.
        return AccentPicker
            .Pick(WallpaperPalette.Quantize(pixels), SakuraThemeBuilder.AccentReferenceSurface)
            ?.ToHex();
    }

    /// <summary>
    /// Diseño D62 — le pide a DWM el fondo, las esquinas y la sombra de la ventana. El cómo está
    /// en <see cref="SakuraWindowChrome"/>, que es el mismo camino que siguen los paneles: tener
    /// dos copias de estos tres pasos era garantizar que una se quedara a medias.
    /// </summary>
    private void ApplySystemBackdrop(IntPtr windowHandle)
    {
        _ = windowHandle;

        // Las ventanas sueltas no conocen las preferencias, así que el shell publica el modo por
        // ellas: el modo eco tiene que valer para todas o para ninguna.
        SakuraWindowChrome.PerformanceMode = _preferences.HardwarePerformanceMode;

        _backdropDecision = SakuraWindowChrome.Apply(
            this,
            ShellSurface,
            "BrushBackground",
            _preferences.Opacity);
    }

    private void ApplyShellOpacity()
    {
        // Diseño D62 — la opacidad elegida solo significa algo si detrás hay un fondo del sistema
        // que se pueda ver a través de ella. Sin acrílico ni Mica lo único que hay detrás es el
        // escritorio sin desenfocar, y una superficie a medias se leería peor que una sólida.
        //
        // Antes esto se aplicaba al Grid contenedor, que quedaba tapado por un borde opaco encima:
        // el deslizador existía y no cambiaba nada.
        if (_backdropDecision is not { } decision)
        {
            return;
        }

        SakuraWindowChrome.PaintSurface(
            this,
            ShellSurface,
            "BrushBackground",
            _preferences.Opacity,
            decision);
    }

    /// <summary>
    /// Diseño D31 — todo el cálculo del tema vive en <see cref="SakuraThemeBuilder"/>, en Nexo.Core.
    /// Aquí solo queda convertir a brochas de WPF y publicarlas. La razón de moverlo es que el
    /// acento ya no siempre lo elegimos nosotros: cuando sale del fondo de escritorio, lo pone una
    /// foto que nadie revisó, y la garantía de que el texto se siga leyendo encima tiene que poder
    /// comprobarse con una prueba y no mirando la pantalla con cada fondo posible.
    /// </summary>
    private void ApplyAccent(string accentHex)
    {
        RgbColor accent;
        try
        {
            accent = RgbColor.FromHex(accentHex);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            accent = RgbColor.FromHex("#8B6CFF");
        }

        var theme = SakuraThemeBuilder.FromAccent(accent);
        var resources = Application.Current.Resources;

        resources["BrushAccent"] = ToBrush(theme.Accent);
        resources["BrushAccentSoft"] = ToBrush(theme.AccentSoft);
        resources["BrushAccentBorder"] = new SolidColorBrush(
            Color.FromArgb(112, theme.Accent.R, theme.Accent.G, theme.Accent.B));
        resources["BrushBackground"] = ToBrush(theme.Background);
        resources["BrushSurface"] = ToBrush(theme.Surface);
        resources["BrushSurfaceRaised"] = ToBrush(theme.SurfaceRaised);
        resources["BrushSurfaceHover"] = ToBrush(theme.SurfaceHover);
        resources["BrushSidebarSurface"] = ToBrush(theme.SidebarSurface);
        resources["BrushInput"] = ToBrush(theme.Input);
        resources["BrushBorder"] = ToBrush(theme.Border);
        resources["BrushFocusRing"] = new SolidColorBrush(
            Color.FromArgb(112, theme.Accent.R, theme.Accent.G, theme.Accent.B));

        // Rastro del tema original: era un rosa fijo, y con cualquier otro tema aparecía como una
        // mancha de un color que no pertenecía a nada.
        resources["BrushSakuraGlow"] = ToBrush(theme.AccentSoft);

        // Imprescindible que sea DESPUÉS de publicar BrushBackground, y no antes: ApplyShellOpacity
        // no usa la brocha del recurso sino que copia su color en una nueva con la opacidad
        // aplicada. Esa copia se queda como estaba a menos que se rehaga aquí — era la causa de que
        // el fondo del shell siguiera morado oscuro mientras el interior sí cambiaba de tema.
        ApplyShellOpacity();
    }

    private static SolidColorBrush ToBrush(RgbColor color) =>
        new(Color.FromRgb(color.R, color.G, color.B));

    /// <summary>
    /// Diseño D52 — el rail solo enseña lo que se ha pedido tener a un clic.
    ///
    /// Chat y Personalizar no aparecen aquí porque no se pueden quitar: el primero es la
    /// aplicación y el segundo es desde donde se devuelven los demás.
    /// </summary>
    private void ApplyModuleVisibility()
    {
        HomeNavButton.Visibility = Show(_preferences.ShowHomeModule);
        TasksNavButton.Visibility = Show(_preferences.ShowTasksModule);
        FocusNavButton.Visibility = Show(_preferences.ShowFocusModule);
        RoutinesNavButton.Visibility = Show(_preferences.ShowRoutinesModule);
        AudioNavButton.Visibility = Show(_preferences.ShowAudioModule);
        CaptureNavButton.Visibility = Show(_preferences.ShowCaptureModule);
        SystemNavButton.Visibility = Show(_preferences.ShowSystemModule);
        UpdateNavigationColumns();

        static Visibility Show(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SetModuleVisibility(string module, bool visible)
    {
        switch (module)
        {
            case "Home":
                _preferences.ShowHomeModule = visible;
                break;
            case "Tasks":
                _preferences.ShowTasksModule = visible;
                break;
            case "Focus":
                _preferences.ShowFocusModule = visible;
                break;
            case "Routines":
                _preferences.ShowRoutinesModule = visible;
                break;
            case "Audio":
                _preferences.ShowAudioModule = visible;
                break;
            case "Capture":
                _preferences.ShowCaptureModule = visible;
                break;
            case "System":
                _preferences.ShowSystemModule = visible;
                break;
        }

        ApplyModuleVisibility();

        if (ShellNavigationPolicy.TryResolveHiddenModuleFallback(
                module,
                visible,
                _currentDestination,
                out var fallbackDestination))
        {
            NavigateTo(fallbackDestination, animate: true);
        }
    }

    private void ApplyPeekOption(string option, bool enabled)
    {
        switch (option)
        {
            case "Enabled":
                _preferences.PeekEnabled = enabled;
                if (!enabled)
                {
                    _peekWindow.HideImmediately();
                }
                break;
            case "Cpu":
                _preferences.ShowCpuInPeek = enabled;
                break;
            case "Memory":
                _preferences.ShowMemoryInPeek = enabled;
                break;
            case "Gpu":
                _preferences.ShowGpuInPeek = enabled;
                break;
            case "Disk":
                _preferences.ShowDiskInPeek = enabled;
                break;
            case "TopProcess":
                _preferences.ShowTopProcessInPeek = enabled;
                break;
        }
    }

    private void UpdateNavigationColumns()
    {
        // La navegación ahora es vertical. Las preferencias solo cambian
        // la visibilidad de cada acceso, no el número de columnas.
    }

    private void SetMetricsCadence(bool isShellVisible)
    {
        _metricsTimer.Interval = isShellVisible
            ? TimeSpan.FromSeconds(2)
            : TimeSpan.FromSeconds(8);
    }

    private async Task RefreshMetricsAsync()
    {
        if (Interlocked.Exchange(ref _metricsRefreshInProgress, 1) == 1)
        {
            return;
        }

        try
        {
            var ownHandle = new WindowInteropHelper(this).Handle.ToInt64();
            var preferredExternalWindow = _lastExternalWindowHandle;
            var snapshot = await Task.Run(_metricsService.ReadSnapshot);
            var decision = _preferences.ResourceGovernorEnabled
                ? await Task.Run(() => _resourceGovernorService.Evaluate(
                    snapshot,
                    preferredExternalWindow,
                    ownHandle))
                : ResourceGovernorDecision.Normal;

            if (_isClosed)
            {
                return;
            }

            _latestSnapshot = snapshot;
            UpdateMetricControls(snapshot);
            await RefreshMediaAsync();
            await ApplyResourceGovernorDecisionAsync(decision);
        }
        catch (Exception)
        {
            // Las métricas son informativas: un fallo de lectura nunca debe cerrar Nexo.
        }
        finally
        {
            Interlocked.Exchange(ref _metricsRefreshInProgress, 0);
        }
    }

    private async Task RefreshHardwareCapabilityAsync()
    {
        _systemView.ShowHardwareCapabilityUpdating();
        try
        {
            var profile = await _hardwareCapabilityService.RefreshAsync(_lifetimeCancellation.Token);
            if (_isClosed)
            {
                return;
            }

            _systemView.UpdateHardwareCapability(profile);
            _dashboardWindow.UpdateHardwareNames(
                profile.Snapshot.Processor.Name,
                profile.Snapshot.PreferredGraphicsAdapter.Name);
            RefreshAdaptiveEnginePlan();
        }
        catch (OperationCanceledException)
        {
            // La ventana se cerró antes de que terminara la detección.
        }
        catch (Exception)
        {
            // La detección de hardware es informativa: un fallo nunca debe afectar el resto de Sakura.
            if (!_isClosed)
            {
                _systemView.UpdateHardwareCapability(_hardwareCapabilityService.GetCachedProfile());
                RefreshAdaptiveEnginePlan();
            }
        }
    }

    private void RefreshAdaptiveEnginePlan()
    {
        if (_isClosed)
        {
            return;
        }

        var hardwareProfile = _hardwareCapabilityService.GetCachedProfile();
        var descriptors = _adaptiveEngineRegistry.GetDescriptors();
        var runtimeStates = _adaptiveEngineRegistry.CaptureRuntimeStates(_preferences, _latestOllamaRuntimeSnapshot);

        var plan = AdaptiveEnginePolicy.Evaluate(
            hardwareProfile,
            _preferences.HardwarePerformanceMode,
            descriptors,
            runtimeStates,
            DateTimeOffset.Now);

        _systemView.UpdateAdaptiveEnginePlan(plan, descriptors);
    }

    private async Task<ResourceGovernorDecision> EnsureFreshResourceDecisionAsync()
    {
        if (!_preferences.ResourceGovernorEnabled)
        {
            return ResourceGovernorDecision.Normal;
        }

        var snapshotAge = DateTimeOffset.Now - _latestSnapshot.CapturedAt;
        if (_latestSnapshot.CapturedAt == DateTimeOffset.MinValue ||
            snapshotAge > TimeSpan.FromSeconds(4))
        {
            await RefreshMetricsAsync();
        }

        return _resourceDecision;
    }

    private async Task ApplyResourceGovernorDecisionAsync(
        ResourceGovernorDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);

        var previous = _resourceDecision;
        _resourceDecision = decision;
        _capsuleWindow.SuppressTransientMessages = decision.SuppressTransientOverlays;
        if (decision.SuppressTransientOverlays)
        {
            _capsuleWindow.HideImmediately();

            // Diseño D56 — y el cajón, si estaba abierto cuando la pantalla completa empezó. No
            // basta con no abrirlo: se puede estar mirando el panel y lanzar el juego desde ahí.
            _dashboardWindow.HideImmediately();
        }

        UpdateResourceModeIndicator(decision);

        var decisionChanged =
            previous.Mode != decision.Mode ||
            !previous.Reason.Equals(decision.Reason, StringComparison.Ordinal);

        if (decisionChanged)
        {
            WriteResourceGovernorLog(previous, decision);
            if (previous.Mode != decision.Mode)
            {
                var title = decision.Mode switch
                {
                    ResourceMode.Game => "Modo Juego activo",
                    ResourceMode.Busy => "Rendimiento protegido",
                    _ => "Modo normal restaurado"
                };
                _homeView.AddRecentAction(title, decision.Reason);
            }
        }

        await _resourceGovernorDecisionGate.WaitAsync();
        try
        {
            var shouldPauseWakeWord =
                _preferences.PauseWakeWordInGameMode && decision.PauseWakeWord;

            if (shouldPauseWakeWord)
            {
                if (_voiceCoordinator.IsWakeWordListening)
                {
                    await PauseWakeWordAsync();
                }

                _resourceGovernorWakeWordPaused = _preferences.WakeWordEnabled;
                return;
            }

            if (_resourceGovernorWakeWordPaused)
            {
                _resourceGovernorWakeWordPaused = false;
                await ResumeWakeWordIfEnabledAsync();
            }
        }
        finally
        {
            _resourceGovernorDecisionGate.Release();
        }
    }

    private void UpdateResourceModeIndicator(ResourceGovernorDecision decision)
    {
        ResourceModeIndicator.Visibility = decision.Mode == ResourceMode.Normal
            ? Visibility.Collapsed
            : Visibility.Visible;
        ResourceModeText.Text = decision.Mode switch
        {
            ResourceMode.Game => "Modo Juego",
            ResourceMode.Busy => "Equipo ocupado",
            _ => "Normal"
        };
        ResourceModeIndicator.ToolTip = decision.Reason;
        ResourceModeDot.Fill = decision.Mode == ResourceMode.Game
            ? (Brush)FindResource("BrushDanger")
            : (Brush)FindResource("BrushWarning");
        RefreshRuntimeDashboard();
    }

    private void RefreshRuntimeDashboard()
    {
        _systemView.UpdateRuntimeStatus(
            _voiceCoordinator.IsVoiceInputReady,
            _preferences.WakeWordEnabled,
            _voiceCoordinator.IsWakeWordListening,
            _preferences.VisionEnabled,
            _runtimeAiStatus,
            _runtimeAiHealthy,
            _resourceDecision.Mode,
            _resourceDecision.Reason);
    }

    private void PresentResourceRestriction(
        ResourceGovernorDecision decision,
        string detail,
        bool fromVoice)
    {
        var title = decision.Mode == ResourceMode.Game
            ? "Sakura está en Modo Juego"
            : "El equipo está ocupado";
        var message = $"{detail} {decision.Reason}";

        _assistantView.AddSakuraMessage(message);
        _capsuleWindow.ShowMessage(
            CapsuleKind.Warning,
            title,
            detail,
            _preferences.Position,
            force: true);

        if (fromVoice)
        {
            _voiceCoordinator.Speak(detail);
        }
    }

    private static void WriteResourceGovernorLog(
        ResourceGovernorDecision previous,
        ResourceGovernorDecision current)
    {
        try
        {
            Directory.CreateDirectory(Nexo.Core.Diagnostics.NexoDataPaths.LogsDirectory);
            File.AppendAllText(
                Nexo.Core.Diagnostics.NexoDataPaths.ResourceGovernorLog,
                $"{DateTimeOffset.Now:O} | {previous.Mode} -> {current.Mode} | {current.Reason}{Environment.NewLine}");
        }
        catch (IOException)
        {
            // El registro no debe afectar el funcionamiento de Nexo.
        }
        catch (UnauthorizedAccessException)
        {
            // El registro no debe afectar el funcionamiento de Nexo.
        }
    }

    /// <summary>
    /// Diseño D44 — el cajón baja en el monitor donde está el ratón. El área viaja con el aviso
    /// porque con dos pantallas «arriba y en medio» no es el mismo sitio en cada una, y para cuando
    /// el hilo de la interfaz atienda el aviso el cursor puede haberse movido ya.
    /// </summary>
    private void HandleTopRevealRequested(TopRevealArea area)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_isClosed || !_preferences.EdgeRevealEnabled)
            {
                return;
            }

            // Diseño D56 — con algo a pantalla completa el cajón tampoco se asoma. Es la misma
            // condición que ya silenciaba el shell y el mando de volumen, y a este se me olvidó
            // ponérsela.
            //
            // El efecto era peor que el de los otros dos, y por eso pasó desapercibido: un juego a
            // pantalla completa queda por encima, así que el cajón bajaba **sin verse** y aun así
            // se quedaba con los clics de la zona. Desde el juego eso se siente como que el ratón
            // deja de responder en una franja de la pantalla, sin nada que explique por qué.
            if (_resourceDecision.SuppressTransientOverlays)
            {
                return;
            }

            _dashboardWindow.Reveal(area);
        }));
    }

    private void UpdateMetricControls(SystemSnapshot snapshot)
    {
        HeaderCpuText.Text = FormatPercentage(snapshot.CpuUsagePercent);
        HeaderMemoryText.Text = FormatPercentage(snapshot.MemoryUsagePercent);
        HeaderGpuText.Text = FormatPercentage(snapshot.GpuUsagePercent);
        _systemView.UpdateSnapshot(snapshot);
        _dashboardWindow.UpdateSnapshot(snapshot);

        var network = _throughputSource.ReadNetwork();
        var drive = _throughputSource.ReadSystemDrive();
        _dashboardWindow.UpdateThroughput(
            network.DownloadBytesPerSecond,
            network.UploadBytesPerSecond,
            drive?.UsedBytes,
            drive?.TotalBytes,
            drive?.Name);

        // Diseño D44 — el reloj y el tiempo encendido del cajón viajan con las métricas en vez de
        // llevar temporizador propio: son dos textos que cambian como mucho una vez por minuto.
        _dashboardWindow.RefreshClock();
        _dashboardWindow.UpdateSession(
            Environment.UserName,
            TimeSpan.FromMilliseconds(Environment.TickCount64));
    }

    /// <summary>
    /// Diseño D43 — la lectura de lo que suena. Va aparte de las métricas porque cruza a WinRT y
    /// puede tardar más que ellas; un fallo aquí no puede dejar sin actualizar el resto del panel.
    /// </summary>
    private async Task RefreshMediaAsync()
    {
        try
        {
            // Con tope de tiempo. Un reproductor que deja de responder no devuelve nunca sus
            // propiedades, y sin este corte esa espera se quedaría dentro del ciclo de métricas:
            // el semáforo de refresco no se soltaría y Sakura dejaría de leer el equipo entero por
            // culpa de una canción.
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var media = await _mediaSessionReader.ReadAsync(timeout.Token);
            if (!_isClosed)
            {
                _dashboardWindow.UpdateMedia(media);
            }
        }
        catch (OperationCanceledException)
        {
            // La lectura tardó demasiado. Se reintenta en el siguiente refresco.
        }
        catch (Exception)
        {
            // Lo que suena es informativo, igual que las métricas: nunca puede cerrar Sakura.
        }
    }

    private async Task RunMediaCommandAsync(Func<Task<bool>> command)
    {
        try
        {
            await command();
            await RefreshMediaAsync();
        }
        catch (Exception)
        {
            // El reproductor puede haberse cerrado justo al pulsar. La siguiente lectura lo dirá.
        }
    }

    private async Task ShowPeekAsync()
    {
        if (!_preferences.PeekEnabled)
        {
            _assistantView.AddSakuraMessage("La vista Peek está desactivada en Personalización.");
            return;
        }

        var snapshotAge = DateTimeOffset.Now - _latestSnapshot.CapturedAt;
        if (_latestSnapshot.CapturedAt == DateTimeOffset.MinValue || snapshotAge > TimeSpan.FromSeconds(5))
        {
            await RefreshMetricsAsync();
        }

        _peekWindow.ShowSnapshot(_latestSnapshot, _preferences);
    }

    private void SavePreferences()
    {
        try
        {
            _settingsStore.Save(_preferences);
        }
        catch (IOException)
        {
            _assistantView.AddSakuraMessage("No se pudo guardar la configuración en este momento.");
        }
        catch (UnauthorizedAccessException)
        {
            _assistantView.AddSakuraMessage("Windows no permitió guardar la configuración.");
        }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (Nexo.Core.WindowsIntegration.WindowsClosePolicy.ShouldHideInsteadOfClose(
                _preferences.MinimizeToTray,
                _allowExit))
        {
            e.Cancel = true;
            HideToTray(showHint: true);
            return;
        }

        if (!_allowExit)
        {
            e.Cancel = true;
            RequestExit();
        }
    }

    private void HideButton_Click(object sender, RoutedEventArgs e)
    {
        HideAnimated();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_preferences.MinimizeToTray)
        {
            HideToTray(showHint: true);
            return;
        }

        RequestExit();
    }

    private void HideToTray(bool showHint)
    {
        HideAnimated();

        if (!showHint || _trayHintShown)
        {
            return;
        }

        _trayHintShown = true;
        _trayIcon.Notify(
            "Sakura sigue activo",
            "Ábrelo con Alt + A o desde el icono de la bandeja.",
            TrayNotificationKind.Information,
            _preferences.ShowWindowsNotifications,
            playSound: false);
    }

    private void RequestExit()
    {
        if (_isClosed || _exitRequested)
        {
            return;
        }

        // Un único inicio de apagado. La fase asíncrona (detener el runtime de IA
        // administrado) se ejecuta MIENTRAS el Dispatcher aún bombea, antes de
        // Application.Shutdown, para no bloquear el hilo de UI con sync-sobre-async durante
        // App.OnExit. No se inician nuevas operaciones después de esto.
        _exitRequested = true;
        _ = RequestExitAsync();
    }

    private async Task RequestExitAsync()
    {
        try
        {
            if (_managedOllamaSupervisor is not null)
            {
                await _managedOllamaSupervisor.StopAsync();
            }
        }
        catch (Exception)
        {
            // El apagado del runtime administrado es best-effort; el cierre continúa igual.
        }
        finally
        {
            _allowExit = true;
            System.Windows.Application.Current.Shutdown();
        }
    }

    private static string FormatPercentage(double? value)
    {
        return value.HasValue ? $"{value.Value:0}%" : "—";
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(
        IntPtr windowHandle,
        int id,
        uint modifiers,
        uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr windowHandle, int id);
}
