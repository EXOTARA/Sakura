using Nexo.Core.Voice;

namespace Nexo.Windows.Tests.Voice;

/// <summary>
/// Registro de llamadas compartido entre los tres dobles de prueba, para poder afirmar
/// el orden relativo en que <see cref="Nexo.Windows.Voice.VoiceCoordinator"/> invoca cada
/// servicio (p. ej. "wake word se detiene antes de que arranque push-to-talk").
/// </summary>
internal sealed class VoiceCallLog
{
    private readonly List<string> _entries = [];
    private readonly object _sync = new();

    public void Add(string entry)
    {
        lock (_sync)
        {
            _entries.Add(entry);
        }
    }

    public IReadOnlyList<string> Entries
    {
        get
        {
            lock (_sync)
            {
                return _entries.ToArray();
            }
        }
    }
}

internal sealed class FakeVoiceInputService : IVoiceInputService
{
    /// <summary>Diseño D72 — el doble no captura audio; el nivel se simula cuando hace falta.</summary>
    public event EventHandler<VoiceLevelEventArgs>? LevelObserved;

    public void RaiseLevel(double level) =>
        LevelObserved?.Invoke(this, new VoiceLevelEventArgs(level));

    private readonly VoiceCallLog _log;
    private readonly object _concurrencySync = new();
    private int _activeStartListeningCalls;

    public FakeVoiceInputService(VoiceCallLog log) => _log = log;

    public bool IsReady { get; set; } = true;

    public bool IsListening { get; set; }

    public int InputDeviceNumber { get; set; } = -1;

    public IReadOnlyList<VoiceInputDevice> Devices { get; set; } = [];

    public VoiceStartResult StartResult { get; set; } = VoiceStartResult.Started("fake");

    public VoiceRecognitionResult StopResult { get; set; } =
        VoiceRecognitionResult.Recognized("orden de prueba", 1);

    public VoiceRecognitionResult ListenResult { get; set; } =
        VoiceRecognitionResult.Recognized("orden de prueba tras wake word", 1);

    public VoicePreparationResult PrepareResult { get; set; } = VoicePreparationResult.Ready();

    /// <summary>Punto de control opcional para forzar solapamiento en pruebas de concurrencia.</summary>
    public Func<Task>? BeforeStartListeningReturns { get; set; }

    public int StartListeningCallCount { get; private set; }

    public int StopListeningCallCount { get; private set; }

    public int ListenForUtteranceCallCount { get; private set; }

    public int PrepareCallCount { get; private set; }

    public int CancelCallCount { get; private set; }

    public int MaxObservedConcurrentStartListeningCalls { get; private set; }

    public bool WasDisposed { get; private set; }

    // Captura de argumentos, para verificar que las delegaciones los preservan tal cual.
    public CancellationToken LastStartListeningToken { get; private set; }

    public CancellationToken LastStopListeningToken { get; private set; }

    public TimeSpan LastMaximumDuration { get; private set; }

    public TimeSpan LastTrailingSilence { get; private set; }

    public ReadOnlyMemory<byte> LastInitialPcmAudio { get; private set; }

    public ReadOnlyMemory<byte> LastInitialSpeechPcmAudio { get; private set; }

    public CancellationToken LastListenForUtteranceToken { get; private set; }

    public IReadOnlyList<VoiceInputDevice> GetInputDevices() => Devices;

    public Task<VoicePreparationResult> PrepareAsync(
        IProgress<VoicePreparationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PrepareCallCount++;
        _log.Add("voiceInput.prepare");
        return Task.FromResult(PrepareResult);
    }

    public async Task<VoiceStartResult> StartListeningAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastStartListeningToken = cancellationToken;
        lock (_concurrencySync)
        {
            _activeStartListeningCalls++;
            MaxObservedConcurrentStartListeningCalls = Math.Max(
                MaxObservedConcurrentStartListeningCalls,
                _activeStartListeningCalls);
        }

        try
        {
            StartListeningCallCount++;
            _log.Add("voiceInput.startListening");
            if (BeforeStartListeningReturns is not null)
            {
                await BeforeStartListeningReturns().ConfigureAwait(false);
            }

            IsListening = true;
            return StartResult;
        }
        finally
        {
            lock (_concurrencySync)
            {
                _activeStartListeningCalls--;
            }
        }
    }

    /// <summary>Diseño D6.3 — se registra el modo para poder afirmar que Flow pide dictado.</summary>
    public VoiceTranscriptionMode LastTranscriptionMode { get; private set; }
        = VoiceTranscriptionMode.Command;

    public Task<VoiceRecognitionResult> StopListeningAsync(
        VoiceTranscriptionMode transcriptionMode = VoiceTranscriptionMode.Command,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastStopListeningToken = cancellationToken;
        LastTranscriptionMode = transcriptionMode;
        StopListeningCallCount++;
        IsListening = false;
        _log.Add("voiceInput.stopListening");
        return Task.FromResult(StopResult);
    }

    public Task<VoiceRecognitionResult> ListenForUtteranceAsync(
        TimeSpan maximumDuration,
        TimeSpan trailingSilence,
        ReadOnlyMemory<byte> initialPcmAudio = default,
        ReadOnlyMemory<byte> initialSpeechPcmAudio = default,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastMaximumDuration = maximumDuration;
        LastTrailingSilence = trailingSilence;
        LastInitialPcmAudio = initialPcmAudio;
        LastInitialSpeechPcmAudio = initialSpeechPcmAudio;
        LastListenForUtteranceToken = cancellationToken;
        ListenForUtteranceCallCount++;
        _log.Add("voiceInput.listenForUtterance");
        return Task.FromResult(ListenResult);
    }

    public Task CancelAsync()
    {
        CancelCallCount++;
        _log.Add("voiceInput.cancel");
        return Task.CompletedTask;
    }

    public void Dispose() => WasDisposed = true;
}

internal sealed class FakeVoiceOutputService : IVoiceOutputService
{
    private readonly VoiceCallLog _log;

    public FakeVoiceOutputService(VoiceCallLog log) => _log = log;

    public int SpeakShortCallCount { get; private set; }

    public int StopCallCount { get; private set; }

    public string? LastSpokenText { get; private set; }

    public bool WasDisposed { get; private set; }

    public void SpeakShort(string text)
    {
        SpeakShortCallCount++;
        LastSpokenText = text;
        _log.Add("voiceOutput.speakShort");
    }

    public void Stop()
    {
        StopCallCount++;
        _log.Add("voiceOutput.stop");
    }

    public void Dispose() => WasDisposed = true;
}

internal sealed class FakeWakeWordService : IWakeWordService
{
    private readonly VoiceCallLog _log;

    public FakeWakeWordService(VoiceCallLog log) => _log = log;

    public event EventHandler<WakeWordDetectedEventArgs>? WakeWordDetected;

    public event EventHandler<WakeWordRecognitionObservedEventArgs>? RecognitionObserved;

    public event EventHandler<VoiceLevelEventArgs>? LevelObserved;

    /// <summary>El doble no captura audio; esto existe para poder simular un nivel si hace falta.</summary>
    public void RaiseLevel(double level) =>
        LevelObserved?.Invoke(this, new VoiceLevelEventArgs(level));

    public bool IsReady { get; set; } = true;

    public bool IsListening { get; set; }

    public int InputDeviceNumber { get; set; } = -1;

    public WakeWordSensitivity Sensitivity { get; set; } = WakeWordSensitivity.Balanced;

    public IReadOnlyList<string> CustomAliases { get; set; } = [];

    public VoiceStartResult StartResult { get; set; } = VoiceStartResult.Started("fake wake word");

    public VoicePreparationResult PrepareResult { get; set; } = VoicePreparationResult.Ready();

    /// <summary>Punto de control opcional para forzar una suspensión real en pruebas de cancelación.</summary>
    public Func<Task>? BeforeStopListeningReturns { get; set; }

    public int StartListeningCallCount { get; private set; }

    public int StopListeningCallCount { get; private set; }

    public int PrepareCallCount { get; private set; }

    public bool WasDisposed { get; private set; }

    // Captura de argumentos, para verificar que las delegaciones los preservan tal cual.
    public WakeWordPhrase? LastStartPhrase { get; private set; }

    public CancellationToken LastStartListeningToken { get; private set; }

    public Task<VoicePreparationResult> PrepareAsync(
        IProgress<VoicePreparationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PrepareCallCount++;
        _log.Add("wakeWord.prepare");
        return Task.FromResult(PrepareResult);
    }

    public Task<VoiceStartResult> StartListeningAsync(
        WakeWordPhrase phrase,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastStartPhrase = phrase;
        LastStartListeningToken = cancellationToken;
        StartListeningCallCount++;
        IsListening = true;
        _log.Add("wakeWord.startListening");
        return Task.FromResult(StartResult);
    }

    public async Task StopListeningAsync()
    {
        StopListeningCallCount++;
        IsListening = false;
        _log.Add("wakeWord.stopListening");
        if (BeforeStopListeningReturns is not null)
        {
            await BeforeStopListeningReturns().ConfigureAwait(false);
        }
    }

    public void RaiseWakeWordDetected(WakeWordDetectedEventArgs e) =>
        WakeWordDetected?.Invoke(this, e);

    public void RaiseRecognitionObserved(WakeWordRecognitionObservedEventArgs e) =>
        RecognitionObserved?.Invoke(this, e);

    public void Dispose() => WasDisposed = true;
}
