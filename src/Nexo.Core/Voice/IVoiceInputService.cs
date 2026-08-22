namespace Nexo.Core.Voice;

public interface IVoiceInputService : IDisposable
{
    /// <summary>
    /// Diseño D72 — nivel del micrófono mientras se escucha una orden, de 0 a 1.
    ///
    /// Hace falta aquí además de en la palabra de activación porque son dos capturas distintas: la
    /// de vigilancia se pausa justo cuando empieza la de la orden, y el halo tiene que seguir
    /// respirando durante la segunda, que es cuando la persona está hablando de verdad.
    /// </summary>
    event EventHandler<VoiceLevelEventArgs>? LevelObserved;

    bool IsReady { get; }

    bool IsListening { get; }

    int InputDeviceNumber { get; set; }

    IReadOnlyList<VoiceInputDevice> GetInputDevices();

    Task<VoicePreparationResult> PrepareAsync(
        IProgress<VoicePreparationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<VoiceStartResult> StartListeningAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Diseño D6.3 — <paramref name="transcriptionMode"/> es opcional y su valor predeterminado
    /// conserva exactamente el comportamiento anterior a Flow, para no alterar a ningún llamador
    /// existente.
    /// </summary>
    Task<VoiceRecognitionResult> StopListeningAsync(
        VoiceTranscriptionMode transcriptionMode = VoiceTranscriptionMode.Command,
        CancellationToken cancellationToken = default);

    Task<VoiceRecognitionResult> ListenForUtteranceAsync(
        TimeSpan maximumDuration,
        TimeSpan trailingSilence,
        ReadOnlyMemory<byte> initialPcmAudio = default,
        ReadOnlyMemory<byte> initialSpeechPcmAudio = default,
        CancellationToken cancellationToken = default);

    Task CancelAsync();
}
