using Nexo.Core.Voice;

namespace Nexo.Windows.Voice;

/// <summary>
/// Punto único de acceso al subsistema de voz (entrada de voz, salida de voz y wake word)
/// por encima de <see cref="IVoiceInputService"/>, <see cref="IVoiceOutputService"/> e
/// <see cref="IWakeWordService"/>.
///
/// Sincronización: este coordinador **posee** los dos únicos <see cref="SemaphoreSlim"/>
/// que serializan las operaciones mutantes del subsistema —uno para entrada de voz y otro
/// para wake word—, ambos privados. No se exponen: el orquestador (la vista principal)
/// adquiere un ámbito (<see cref="AcquireVoiceInputScopeAsync"/> /
/// <see cref="AcquireWakeWordScopeAsync"/>), hace su sección crítica dentro de él y lo
/// libera al desecharlo. Las operaciones mutantes de cada servicio solo son alcanzables a
/// través de su ámbito, así que es imposible mutarlos sin sostener la exclusión. El orden
/// de adquisición es siempre entrada de voz primero y wake word después, nunca al revés.
///
/// Propiedad: este coordinador **no es dueño** del ciclo de vida de los tres servicios que
/// recibe; no los libera nunca. Quien los construye y los libera es
/// <c>SakuraCompositionRoot</c> (ver su <c>Dispose</c>). <see cref="Dispose"/> aquí solo
/// libera los dos semáforos que este tipo crea para sí mismo.
///
/// No depende de infraestructura de interfaz de usuario, de la ventana principal de la
/// aplicación, ni de las preferencias guardadas o la decisión del gobernador de recursos:
/// toda decisión de política (frase activa, modo juego, etc.) la sigue tomando quien
/// llame a este coordinador.
/// </summary>
public sealed class VoiceCoordinator : IDisposable
{
    private readonly IVoiceInputService _voiceInputService;
    private readonly IVoiceOutputService _voiceOutputService;
    private readonly IWakeWordService _wakeWordService;

    // Los dos únicos semáforos del subsistema de voz: recursos propios del coordinador (no
    // los tres servicios), se crean y se liberan aquí. Un solo dominio de exclusión por
    // servicio mutante; se sostienen a través de los ámbitos, nunca se exponen.
    private readonly SemaphoreSlim _voiceGate = new(1, 1);
    private readonly SemaphoreSlim _wakeWordGate = new(1, 1);

    private bool _disposed;

    public VoiceCoordinator(
        IVoiceInputService voiceInputService,
        IVoiceOutputService voiceOutputService,
        IWakeWordService wakeWordService)
    {
        _voiceInputService = voiceInputService ?? throw new ArgumentNullException(nameof(voiceInputService));
        _voiceOutputService = voiceOutputService ?? throw new ArgumentNullException(nameof(voiceOutputService));
        _wakeWordService = wakeWordService ?? throw new ArgumentNullException(nameof(wakeWordService));
    }

    /// <summary>
    /// Paso directo al evento del servicio subyacente: no hay suscripción interna que
    /// limpiar, así que <see cref="Dispose"/> no necesita desuscribirse de nada aquí.
    /// </summary>
    public event EventHandler<WakeWordDetectedEventArgs>? WakeWordDetected
    {
        add => _wakeWordService.WakeWordDetected += value;
        remove => _wakeWordService.WakeWordDetected -= value;
    }

    /// <summary>Paso directo, mismo motivo que <see cref="WakeWordDetected"/>.</summary>
    public event EventHandler<WakeWordRecognitionObservedEventArgs>? RecognitionObserved
    {
        add => _wakeWordService.RecognitionObserved += value;
        remove => _wakeWordService.RecognitionObserved -= value;
    }

    /// <summary>
    /// Diseño D72 — el nivel del micrófono para el halo de voz, venga de donde venga.
    ///
    /// Se suscribe a las dos capturas porque son dos y se turnan: la de vigilancia oye la palabra de
    /// activación y se pausa justo cuando arranca la de la orden. Quien escuche esto no tiene por
    /// qué saber cuál de las dos está abierta en cada momento; solo hay una que hable a la vez.
    /// </summary>
    public event EventHandler<VoiceLevelEventArgs>? LevelObserved
    {
        add
        {
            _wakeWordService.LevelObserved += value;
            _voiceInputService.LevelObserved += value;
        }
        remove
        {
            _wakeWordService.LevelObserved -= value;
            _voiceInputService.LevelObserved -= value;
        }
    }

    public bool IsVoiceInputReady => _voiceInputService.IsReady;

    public bool IsVoiceInputListening => _voiceInputService.IsListening;

    public bool IsWakeWordReady => _wakeWordService.IsReady;

    public bool IsWakeWordListening => _wakeWordService.IsListening;

    /// <summary>Se aplica a los dos servicios: entrada de voz y wake word comparten micrófono.</summary>
    public int InputDeviceNumber
    {
        get => _voiceInputService.InputDeviceNumber;
        set
        {
            _voiceInputService.InputDeviceNumber = value;
            _wakeWordService.InputDeviceNumber = value;
        }
    }

    public WakeWordSensitivity WakeWordSensitivity
    {
        get => _wakeWordService.Sensitivity;
        set => _wakeWordService.Sensitivity = value;
    }

    public IReadOnlyList<string> WakeWordCustomAliases
    {
        get => _wakeWordService.CustomAliases;
        set => _wakeWordService.CustomAliases = value;
    }

    public IReadOnlyList<VoiceInputDevice> GetInputDevices() =>
        _voiceInputService.GetInputDevices();

    public Task<VoicePreparationResult> PrepareVoiceInputAsync(
        IProgress<VoicePreparationProgress>? progress = null,
        CancellationToken cancellationToken = default) =>
        _voiceInputService.PrepareAsync(progress, cancellationToken);

    public Task<VoicePreparationResult> PrepareWakeWordAsync(
        IProgress<VoicePreparationProgress>? progress = null,
        CancellationToken cancellationToken = default) =>
        _wakeWordService.PrepareAsync(progress, cancellationToken);

    public void Speak(string text) => _voiceOutputService.SpeakShort(text);

    public void StopSpeaking() => _voiceOutputService.Stop();

    // ---------------------------------------------------------------------------------
    // Ámbitos de exclusión: el diseño de sincronización del subsistema de voz.
    //
    // El único mecanismo de sincronización real del subsistema de voz vive aquí: los dos
    // SemaphoreSlim privados de arriba. Un orquestador externo (la vista principal) ya no
    // tiene semáforos propios: adquiere un ámbito, hace su sección crítica dentro de él —
    // incluida toda su orquestación visual— y lo libera al terminar. Las operaciones
    // mutantes de cada servicio solo son alcanzables a través del ámbito correspondiente,
    // así que es imposible mutar un servicio sin sostener su exclusión.
    //
    // El orden de adquisición es siempre entrada de voz primero y wake word después
    // (nunca al revés): cuando un ámbito de voz envuelve a uno de wake word —lo habitual
    // en push-to-talk y en la escucha tras la palabra de activación— la anidación respeta
    // ese orden porque son dos semáforos distintos.
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Adquiere el semáforo de entrada de voz y devuelve un ámbito que lo libera al
    /// desecharse. Si el token se cancela antes de adquirirlo, no se toma el candado y no
    /// hay nada que liberar.
    /// </summary>
    public async Task<IVoiceInputScope> AcquireVoiceInputScopeAsync(
        CancellationToken cancellationToken = default)
    {
        await _voiceGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new VoiceInputScope(this);
    }

    /// <summary>
    /// Adquiere el semáforo de wake word y devuelve un ámbito que lo libera al desecharse.
    /// Mismo contrato que <see cref="AcquireVoiceInputScopeAsync"/>.
    /// </summary>
    public async Task<IWakeWordScope> AcquireWakeWordScopeAsync(
        CancellationToken cancellationToken = default)
    {
        await _wakeWordGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new WakeWordScope(this);
    }

    /// <summary>
    /// Ámbito de entrada de voz: delega cada operación en el servicio subyacente y libera
    /// <see cref="_voiceGate"/> una sola vez, aunque se deseche más de una vez.
    /// </summary>
    private sealed class VoiceInputScope : IVoiceInputScope
    {
        private readonly VoiceCoordinator _owner;
        private bool _released;

        public VoiceInputScope(VoiceCoordinator owner) => _owner = owner;

        public Task<VoiceStartResult> StartListeningAsync(CancellationToken cancellationToken = default) =>
            _owner._voiceInputService.StartListeningAsync(cancellationToken);

        public Task<VoiceRecognitionResult> StopListeningAsync(
            VoiceTranscriptionMode transcriptionMode = VoiceTranscriptionMode.Command,
            CancellationToken cancellationToken = default) =>
            _owner._voiceInputService.StopListeningAsync(transcriptionMode, cancellationToken);

        public Task CancelAsync() => _owner._voiceInputService.CancelAsync();

        public Task<VoiceRecognitionResult> ListenForUtteranceAsync(
            TimeSpan maximumDuration,
            TimeSpan trailingSilence,
            ReadOnlyMemory<byte> initialPcmAudio = default,
            ReadOnlyMemory<byte> initialSpeechPcmAudio = default,
            CancellationToken cancellationToken = default) =>
            _owner._voiceInputService.ListenForUtteranceAsync(
                maximumDuration,
                trailingSilence,
                initialPcmAudio,
                initialSpeechPcmAudio,
                cancellationToken);

        public ValueTask DisposeAsync()
        {
            if (!_released)
            {
                _released = true;
                _owner._voiceGate.Release();
            }

            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Ámbito de wake word: delega cada operación en el servicio subyacente y libera
    /// <see cref="_wakeWordGate"/> una sola vez, aunque se deseche más de una vez.
    /// </summary>
    private sealed class WakeWordScope : IWakeWordScope
    {
        private readonly VoiceCoordinator _owner;
        private bool _released;

        public WakeWordScope(VoiceCoordinator owner) => _owner = owner;

        public Task<VoiceStartResult> StartListeningAsync(
            WakeWordPhrase phrase,
            CancellationToken cancellationToken = default) =>
            _owner._wakeWordService.StartListeningAsync(phrase, cancellationToken);

        public Task StopListeningAsync() => _owner._wakeWordService.StopListeningAsync();

        public ValueTask DisposeAsync()
        {
            if (!_released)
            {
                _released = true;
                _owner._wakeWordGate.Release();
            }

            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Libera únicamente los dos <see cref="SemaphoreSlim"/> que este coordinador crea.
    /// No llama <c>Dispose()</c> sobre <see cref="IVoiceInputService"/>,
    /// <see cref="IVoiceOutputService"/> ni <see cref="IWakeWordService"/>: el coordinador
    /// no es su dueño — lo es <c>SakuraCompositionRoot</c>, que los libera después de este.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _voiceGate.Dispose();
        _wakeWordGate.Dispose();
    }
}
