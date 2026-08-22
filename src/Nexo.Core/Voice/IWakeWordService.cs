namespace Nexo.Core.Voice;

public interface IWakeWordService : IDisposable
{
    event EventHandler<WakeWordDetectedEventArgs>? WakeWordDetected;

    event EventHandler<WakeWordRecognitionObservedEventArgs>? RecognitionObserved;

    /// <summary>
    /// Diseño D72 — cuánta voz está entrando ahora mismo, de 0 a 1.
    ///
    /// Sale de aquí y no de una captura aparte porque el micrófono ya está abierto: montar un
    /// segundo <c>WaveIn</c> sobre el mismo dispositivo para medir el nivel sería abrirlo dos veces
    /// para oír lo mismo, con el riesgo de que el segundo no consiga acceso exclusivo.
    /// </summary>
    event EventHandler<VoiceLevelEventArgs>? LevelObserved;

    bool IsReady { get; }

    bool IsListening { get; }

    int InputDeviceNumber { get; set; }

    WakeWordSensitivity Sensitivity { get; set; }

    IReadOnlyList<string> CustomAliases { get; set; }

    Task<VoicePreparationResult> PrepareAsync(
        IProgress<VoicePreparationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<VoiceStartResult> StartListeningAsync(
        WakeWordPhrase phrase,
        CancellationToken cancellationToken = default);

    Task StopListeningAsync();
}
