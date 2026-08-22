using System.Net.Http;
using System.Text;
using NAudio;
using NAudio.Wave;
using Nexo.Core.Diagnostics;
using Nexo.Core.Voice;
using Whisper.net;
using Whisper.net.Ggml;

namespace Nexo.Windows.Voice;

/// <summary>
/// Entrada de voz local basada en Whisper. El modelo se conserva en LocalAppData.
/// Después de una transcripción se mantiene unos minutos en memoria para que las
/// siguientes órdenes por voz comiencen más rápido y luego se libera.
/// </summary>
public sealed class WhisperVoiceInputService : IVoiceInputService
{
    /// <summary>
    /// Diseño D6.3 — el prompt de Whisper condiciona el estilo de la salida. El de comandos sesga
    /// hacia frases cortas de orden; para dictar hace falta lo contrario: prosa continua y con
    /// puntuación, que es de donde <c>SpanishDictationNormalizer</c> parte.
    /// </summary>
    private const string DictationPrompt =
        "Dictado en español con puntuación y acentos correctos. Texto natural en oraciones " +
        "completas, con comas, puntos y mayúscula al inicio de cada oración.";

    private const string CommandPrompt =
        "Conversación natural y órdenes para Sakura en español. Frases frecuentes: " +
        "Oye Sakura; qué es esto; qué problema es este; por qué falla; mira la pantalla; " +
        "abre Calculadora; abre PowerShell; muestra Peek; cómo está mi PC; " +
        "baja Spotify; sube Spotify al 50 por ciento; silencia Discord.";

    private const int SampleRate = 16_000;
    private const int BitsPerSample = 16;
    private const int Channels = 1;
    private const long MinimumModelBytes = 50L * 1024 * 1024;
    private const long MinimumRecordedBytes = 4_800;
    private const long ProgressStepBytes = 4L * 1024 * 1024;
    private const int MinimumSpeechAverageAmplitudeThreshold = 180;
    private const int MaximumSpeechAverageAmplitudeThreshold = 1_200;
    private const int NoiseCalibrationBufferCount = 5;
    private const int SpeechStartConfirmationMilliseconds = 250;

    private static readonly TimeSpan RecordingStopTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ModelMemoryKeepAlive = TimeSpan.FromMinutes(5);

    private readonly object _sync = new();
    private readonly SemaphoreSlim _prepareGate = new(1, 1);
    private readonly SemaphoreSlim _transcriptionGate = new(1, 1);
    private readonly string _modelPath;
    private readonly string _temporaryDirectory;

    private WhisperFactory? _whisperFactory;
    private Timer? _factoryReleaseTimer;
    private WaveInEvent? _recorder;
    private WaveFileWriter? _writer;
    private TaskCompletionSource<Exception?>? _recordingStopped;
    private string? _recordingPath;
    private long _recordedBytes;
    private int _audioBufferCount;
    private int _speechBufferCount;
    private int _peakAmplitude;
    private double _noiseFloorAmplitude = 160;
    private int _noiseCalibrationBuffers;
    private TaskCompletionSource<bool>? _automaticUtteranceCompleted;
    private TimeSpan _automaticTrailingSilence;
    private int _automaticSilentMilliseconds;
    private int _automaticSpeechMilliseconds;
    private int _automaticConsecutiveSpeechMilliseconds;
    private int _automaticLiveAudioMilliseconds;
    private bool _automaticSpeechDetected;
    private bool _automaticListening;
    private byte[] _pendingInitialPcmAudio = [];
    private byte[] _pendingInitialSpeechPcmAudio = [];
    private bool _disposed;

    public WhisperVoiceInputService()
    {
        _modelPath = NexoDataPaths.WhisperModel;
        _temporaryDirectory = NexoDataPaths.TempDirectory;
        IsReady = IsUsableModelFile();
    }

    public bool IsReady { get; private set; }

    public bool IsListening { get; private set; }

    public int InputDeviceNumber { get; set; } = -1;

    public IReadOnlyList<VoiceInputDevice> GetInputDevices()
    {
        var devices = new List<VoiceInputDevice>();

        try
        {
            for (var deviceNumber = 0; deviceNumber < WaveIn.DeviceCount; deviceNumber++)
            {
                var capabilities = WaveIn.GetCapabilities(deviceNumber);
                var name = string.IsNullOrWhiteSpace(capabilities.ProductName)
                    ? $"Micrófono {deviceNumber + 1}"
                    : capabilities.ProductName.Trim();

                devices.Add(new VoiceInputDevice(deviceNumber, name));
            }
        }
        catch (MmException)
        {
            // Windows puede cambiar la lista mientras se consulta.
        }

        return devices;
    }

    public async Task<VoicePreparationResult> PrepareAsync(
        IProgress<VoicePreparationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (IsUsableModelFile())
        {
            IsReady = true;
            return VoicePreparationResult.Ready(
                "Voz local lista · Whisper base · español");
        }

        await _prepareGate.WaitAsync(cancellationToken);
        try
        {
            if (IsUsableModelFile())
            {
                IsReady = true;
                return VoicePreparationResult.Ready(
                    "Voz local lista · Whisper base · español");
            }

            progress?.Report(VoicePreparationProgress.Preparing(
                "Preparando la voz local por primera vez…"));

            var modelDirectory = Path.GetDirectoryName(_modelPath)
                ?? throw new InvalidOperationException("No se pudo resolver la carpeta del modelo.");
            Directory.CreateDirectory(modelDirectory);

            var partialPath = _modelPath + ".download";
            TryDeleteFile(partialPath);

            long totalBytes = 0;
            await using (var modelStream = await WhisperGgmlDownloader.Default
                .GetGgmlModelAsync(
                    GgmlType.Base,
                    cancellationToken: cancellationToken))
            await using (var modelWriter = new FileStream(
                partialPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81_920,
                useAsync: true))
            {
                var buffer = new byte[81_920];
                long lastReportedBytes = 0;

                while (true)
                {
                    var bytesRead = await modelStream.ReadAsync(
                        buffer.AsMemory(0, buffer.Length),
                        cancellationToken);
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    await modelWriter.WriteAsync(
                        buffer.AsMemory(0, bytesRead),
                        cancellationToken);
                    totalBytes += bytesRead;

                    if (totalBytes - lastReportedBytes >= ProgressStepBytes)
                    {
                        lastReportedBytes = totalBytes;
                        progress?.Report(VoicePreparationProgress.Downloading(totalBytes));
                    }
                }

                await modelWriter.FlushAsync(cancellationToken);
            }

            if (totalBytes < MinimumModelBytes)
            {
                TryDeleteFile(partialPath);
                IsReady = false;
                return VoicePreparationResult.Unavailable(
                    "La descarga del modelo quedó incompleta. Revisa tu conexión e inténtalo de nuevo.");
            }

            File.Move(partialPath, _modelPath, overwrite: true);
            IsReady = true;

            progress?.Report(VoicePreparationProgress.Preparing(
                "Modelo descargado. La voz local está lista."));

            return VoicePreparationResult.Ready(
                "Voz local lista · Whisper base · español");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            IsReady = false;
            return VoicePreparationResult.Unavailable(
                "La descarga del modelo tardó demasiado. Inténtalo otra vez.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is HttpRequestException or IOException or UnauthorizedAccessException)
        {
            IsReady = false;
            return VoicePreparationResult.Unavailable(
                "No pude preparar Whisper local. Revisa tu conexión y el espacio disponible.");
        }
        finally
        {
            _prepareGate.Release();
        }
    }

    public async Task<VoiceStartResult> StartListeningAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsReady)
        {
            var preparation = await PrepareAsync(cancellationToken: cancellationToken);
            if (!preparation.IsReady)
            {
                return VoiceStartResult.Unavailable(preparation.Detail);
            }
        }

        lock (_sync)
        {
            if (IsListening)
            {
                return VoiceStartResult.Started(
                    "Whisper local · base · español");
            }
        }

        try
        {
            Directory.CreateDirectory(_temporaryDirectory);
            var recordingPath = Path.Combine(
                _temporaryDirectory,
                $"voice-{Guid.NewGuid():N}.wav");

            var recorder = new WaveInEvent
            {
                DeviceNumber = ResolveInputDeviceNumber(),
                WaveFormat = new WaveFormat(SampleRate, BitsPerSample, Channels),
                BufferMilliseconds = 100,
                NumberOfBuffers = 3
            };
            var writer = new WaveFileWriter(recordingPath, recorder.WaveFormat);
            var stopped = new TaskCompletionSource<Exception?>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            recorder.DataAvailable += Recorder_DataAvailable;
            recorder.RecordingStopped += Recorder_RecordingStopped;

            lock (_sync)
            {
                _recorder = recorder;
                _writer = writer;
                _recordingStopped = stopped;
                _recordingPath = recordingPath;
                _recordedBytes = 0;
                _audioBufferCount = 0;
                _speechBufferCount = 0;
                _peakAmplitude = 0;
                _noiseFloorAmplitude = 160;
                _noiseCalibrationBuffers = 0;
                IsListening = true;
            }

            WritePendingInitialAudio();

            recorder.StartRecording();
            return VoiceStartResult.Started(
                "Whisper local · base · español");
        }
        catch (Exception exception) when (
            exception is MmException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            CleanupRecorder(deleteRecording: true);
            return VoiceStartResult.Unavailable(
                "No pude abrir el micrófono. Revisa el dispositivo de entrada y los permisos de Windows.");
        }
    }

    public async Task<VoiceRecognitionResult> StopListeningAsync(
        VoiceTranscriptionMode transcriptionMode = VoiceTranscriptionMode.Command,
        CancellationToken cancellationToken = default)
    {
        WaveInEvent? recorder;
        Task<Exception?> stoppedTask;

        lock (_sync)
        {
            if (!IsListening || _recorder is null)
            {
                return VoiceRecognitionResult.NoSpeech("Sakura no estaba escuchando.");
            }

            IsListening = false;
            recorder = _recorder;
            stoppedTask = _recordingStopped?.Task
                ?? Task.FromResult<Exception?>(null);
        }

        try
        {
            recorder.StopRecording();
            await Task.WhenAny(
                stoppedTask,
                Task.Delay(RecordingStopTimeout, cancellationToken));

            var recordingError = stoppedTask.IsCompletedSuccessfully
                ? await stoppedTask
                : null;

            string? recordingPath;
            long recordedBytes;
            VoiceCaptureQuality captureQuality;
            lock (_sync)
            {
                recordingPath = _recordingPath;
                recordedBytes = _recordedBytes;
                captureQuality = new VoiceCaptureQuality(
                    _audioBufferCount,
                    _speechBufferCount,
                    _peakAmplitude,
                    _noiseFloorAmplitude);
            }

            CleanupRecorder(deleteRecording: false);

            if (recordingError is not null)
            {
                TryDeleteFile(recordingPath);
                return VoiceRecognitionResult.NoSpeech(
                    "Windows interrumpió la grabación. Revisa el micrófono e inténtalo otra vez.");
            }

            if (string.IsNullOrWhiteSpace(recordingPath) ||
                !File.Exists(recordingPath) ||
                recordedBytes < MinimumRecordedBytes)
            {
                TryDeleteFile(recordingPath);
                return VoiceRecognitionResult.NoSpeech(
                    "No detecté suficiente audio. Mantén Mic presionado mientras terminas de hablar.");
            }

            try
            {
                var result = await TranscribeAsync(
                    recordingPath,
                    recordedBytes,
                    captureQuality,
                    transcriptionMode,
                    cancellationToken);
                AppendVoiceCaptureLog(
                    recordedBytes,
                    captureQuality,
                    result);
                return result;
            }
            finally
            {
                TryDeleteFile(recordingPath);
            }
        }
        catch (OperationCanceledException)
        {
            await CancelAsync();
            throw;
        }
        catch (Exception exception) when (
            exception is MmException or InvalidOperationException or IOException)
        {
            CleanupRecorder(deleteRecording: true);
            return VoiceRecognitionResult.NoSpeech(
                "No pude completar la grabación. Inténtalo otra vez.");
        }
    }

    public async Task<VoiceRecognitionResult> ListenForUtteranceAsync(
        TimeSpan maximumDuration,
        TimeSpan trailingSilence,
        ReadOnlyMemory<byte> initialPcmAudio = default,
        ReadOnlyMemory<byte> initialSpeechPcmAudio = default,
        CancellationToken cancellationToken = default)
    {
        if (maximumDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDuration));
        }

        if (trailingSilence < TimeSpan.FromMilliseconds(300))
        {
            throw new ArgumentOutOfRangeException(nameof(trailingSilence));
        }

        var utteranceCompleted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_sync)
        {
            _automaticUtteranceCompleted = utteranceCompleted;
            _automaticTrailingSilence = trailingSilence;
            _automaticSilentMilliseconds = 0;
            _automaticSpeechMilliseconds = 0;
            _automaticConsecutiveSpeechMilliseconds = 0;
            _automaticLiveAudioMilliseconds = 0;
            _automaticSpeechDetected = false;
            _automaticListening = true;
            _pendingInitialPcmAudio = initialPcmAudio.IsEmpty
                ? []
                : initialPcmAudio.ToArray();
            _pendingInitialSpeechPcmAudio = initialSpeechPcmAudio.IsEmpty
                ? []
                : initialSpeechPcmAudio.ToArray();
        }

        var start = await StartListeningAsync(cancellationToken);
        if (!start.IsAvailable)
        {
            ResetAutomaticListening();
            return VoiceRecognitionResult.NoSpeech(start.Detail);
        }

        try
        {
            var timeoutTask = Task.Delay(maximumDuration, cancellationToken);
            await Task.WhenAny(utteranceCompleted.Task, timeoutTask);
            cancellationToken.ThrowIfCancellationRequested();

            bool speechDetected;
            lock (_sync)
            {
                speechDetected = _automaticSpeechDetected;
            }

            if (!speechDetected)
            {
                await CancelAsync();
                return VoiceRecognitionResult.NoSpeech(
                    "No escuché una orden después de activarme.");
            }

            // La ruta de palabra de activación siempre produce una ORDEN, nunca dictado.
            return await StopListeningAsync(VoiceTranscriptionMode.Command, cancellationToken);
        }
        finally
        {
            ResetAutomaticListening();
        }
    }

    public async Task CancelAsync()
    {
        WaveInEvent? recorder;
        Task completionTask;

        lock (_sync)
        {
            recorder = _recorder;
            completionTask = _recordingStopped?.Task ?? Task.CompletedTask;
            IsListening = false;
        }

        if (recorder is not null)
        {
            try
            {
                recorder.StopRecording();
                await Task.WhenAny(completionTask, Task.Delay(400));
            }
            catch (MmException)
            {
                // El dispositivo ya se había detenido.
            }
        }

        CleanupRecorder(deleteRecording: true);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            _recorder?.StopRecording();
        }
        catch (MmException)
        {
            // El micrófono ya no estaba activo.
        }

        CleanupRecorder(deleteRecording: true);
        ReleaseWhisperFactory();
        GC.SuppressFinalize(this);
    }

    private async Task<VoiceRecognitionResult> TranscribeAsync(
        string recordingPath,
        long recordedBytes,
        VoiceCaptureQuality captureQuality,
        VoiceTranscriptionMode transcriptionMode,
        CancellationToken cancellationToken)
    {
        await _transcriptionGate.WaitAsync(cancellationToken);
        try
        {
            var isDictation = transcriptionMode == VoiceTranscriptionMode.Dictation;
            var whisperFactory = GetOrCreateWhisperFactory();
            using var processor = whisperFactory.CreateBuilder()
                .WithLanguage("es")
                .WithPrompt(isDictation ? DictationPrompt : CommandPrompt)
                .Build();
            await using var audioStream = File.OpenRead(recordingPath);

            var transcript = new StringBuilder();
            await foreach (var segment in processor.ProcessAsync(
                audioStream,
                cancellationToken))
            {
                var text = segment.Text?.Trim();
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                if (transcript.Length > 0)
                {
                    transcript.Append(' ');
                }

                transcript.Append(text);
            }

            // En dictado se devuelve el texto de Whisper intacto: el normalizador de comandos
            // quitaría acentos, mayúsculas y toda la puntuación, que es justo lo que se quiere
            // escribir. El acondicionamiento del dictado ocurre después, en Nexo.Core.Flow.
            var normalizedText = isDictation
                ? transcript.ToString().Trim()
                : SpanishVoiceTranscriptNormalizer.Normalize(transcript.ToString());

            if (string.IsNullOrWhiteSpace(normalizedText))
            {
                return VoiceRecognitionResult.NoSpeech(
                    isDictation
                        ? "No entendí nada de lo que dictaste. Habla un poco más cerca del micrófono."
                        : "Whisper no encontró una orden clara. Habla un poco más cerca del micrófono.");
            }

            var confidence = EstimateRecognitionConfidence(
                normalizedText,
                recordedBytes,
                captureQuality);

            // Dictar no admite un paso de confirmación: la persona ve el texto ya escrito y lo
            // corrige si hace falta. Pedirle que confirme cada frase anularía el sentido de dictar.
            var requiresConfirmation = !isDictation && confidence < 0.60;

            return VoiceRecognitionResult.Recognized(
                normalizedText,
                confidence,
                requiresConfirmation,
                requiresConfirmation
                    ? "La orden se escuchó con poca claridad."
                    : "Voz reconocida.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return VoiceRecognitionResult.NoSpeech(
                "No pude transcribir el audio localmente. Reinicia Sakura e inténtalo otra vez.");
        }
        finally
        {
            ScheduleWhisperFactoryRelease();
            _transcriptionGate.Release();
        }
    }

    private WhisperFactory GetOrCreateWhisperFactory()
    {
        lock (_sync)
        {
            _factoryReleaseTimer?.Dispose();
            _factoryReleaseTimer = null;
            _whisperFactory ??= WhisperFactory.FromPath(_modelPath);
            return _whisperFactory;
        }
    }

    private void ScheduleWhisperFactoryRelease()
    {
        lock (_sync)
        {
            if (_disposed || _whisperFactory is null)
            {
                return;
            }

            _factoryReleaseTimer?.Dispose();
            _factoryReleaseTimer = new Timer(
                _ => ReleaseWhisperFactoryIfIdle(),
                null,
                ModelMemoryKeepAlive,
                Timeout.InfiniteTimeSpan);
        }
    }

    private void ReleaseWhisperFactoryIfIdle()
    {
        WhisperFactory? factory = null;

        lock (_sync)
        {
            if (_transcriptionGate.CurrentCount == 0)
            {
                _factoryReleaseTimer?.Change(
                    TimeSpan.FromSeconds(30),
                    Timeout.InfiniteTimeSpan);
                return;
            }

            factory = _whisperFactory;
            _whisperFactory = null;
            _factoryReleaseTimer?.Dispose();
            _factoryReleaseTimer = null;
        }

        factory?.Dispose();
    }

    private void ReleaseWhisperFactory()
    {
        WhisperFactory? factory;

        lock (_sync)
        {
            factory = _whisperFactory;
            _whisperFactory = null;
            _factoryReleaseTimer?.Dispose();
            _factoryReleaseTimer = null;
        }

        factory?.Dispose();
    }

    /// <summary>Diseño D72 — ver <see cref="IVoiceInputService.LevelObserved"/>.</summary>
    public event EventHandler<VoiceLevelEventArgs>? LevelObserved;

    private void Recorder_DataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded > 0 && LevelObserved is { } levelObserved)
        {
            levelObserved(this, new VoiceLevelEventArgs(
                VoiceLevelMeter.Measure(e.Buffer, e.BytesRecorded)));
        }

        lock (_sync)
        {
            if (_writer is null || e.BytesRecorded <= 0)
            {
                return;
            }

            _writer.Write(e.Buffer, 0, e.BytesRecorded);
            _recordedBytes += e.BytesRecorded;

            var audio = AnalyzeAudioBuffer(e.Buffer, e.BytesRecorded);
            TrackAudioQuality(audio);

            if (_automaticListening)
            {
                _automaticLiveAudioMilliseconds += Math.Max(
                    1,
                    e.BytesRecorded * 1000 /
                    (_recorder?.WaveFormat.AverageBytesPerSecond ??
                     SampleRate * Channels * (BitsPerSample / 8)));
                ObserveAutomaticUtterance(audio, e.BytesRecorded);
            }
        }
    }

    private AudioBufferAnalysis AnalyzeAudioBuffer(byte[] buffer, int bytesRecorded)
    {
        long amplitudeTotal = 0;
        var peakAmplitude = 0;
        var sampleCount = 0;

        for (var index = 0; index + 1 < bytesRecorded; index += 2)
        {
            var sample = (short)(buffer[index] | (buffer[index + 1] << 8));
            var amplitude = Math.Abs((int)sample);
            amplitudeTotal += amplitude;
            peakAmplitude = Math.Max(peakAmplitude, amplitude);
            sampleCount++;
        }

        var averageAmplitude = sampleCount == 0
            ? 0
            : amplitudeTotal / (double)sampleCount;

        return new AudioBufferAnalysis(averageAmplitude, peakAmplitude);
    }

    private void TrackAudioQuality(AudioBufferAnalysis audio)
    {
        _audioBufferCount++;
        _peakAmplitude = Math.Max(_peakAmplitude, audio.PeakAmplitude);

        var threshold = GetSpeechThreshold();
        if (audio.AverageAmplitude >= threshold)
        {
            _speechBufferCount++;
            return;
        }

        if (!_automaticSpeechDetected &&
            _noiseCalibrationBuffers < NoiseCalibrationBufferCount)
        {
            _noiseFloorAmplitude =
                ((_noiseFloorAmplitude * _noiseCalibrationBuffers) + audio.AverageAmplitude) /
                (_noiseCalibrationBuffers + 1);
            _noiseCalibrationBuffers++;
        }
    }

    private void ObserveAutomaticUtterance(
        AudioBufferAnalysis audio,
        int bytesRecorded)
    {
        if (_recorder is null || bytesRecorded < 2)
        {
            return;
        }

        var bufferMilliseconds = Math.Max(
            1,
            bytesRecorded * 1000 / _recorder.WaveFormat.AverageBytesPerSecond);
        var threshold = GetSpeechThreshold();
        var activeThreshold = _automaticSpeechDetected
            ? threshold * 0.72
            : threshold;
        var isSpeech = audio.AverageAmplitude >= activeThreshold;

        if (isSpeech)
        {
            _automaticConsecutiveSpeechMilliseconds += bufferMilliseconds;
            _automaticSpeechMilliseconds += bufferMilliseconds;

            if (_automaticConsecutiveSpeechMilliseconds >=
                SpeechStartConfirmationMilliseconds)
            {
                _automaticSpeechDetected = true;
            }

            if (_automaticSpeechDetected)
            {
                _automaticSilentMilliseconds = 0;
            }

            return;
        }

        if (!_automaticSpeechDetected)
        {
            _automaticConsecutiveSpeechMilliseconds = 0;
            _automaticSpeechMilliseconds = 0;
            return;
        }

        _automaticSilentMilliseconds += bufferMilliseconds;
        var snapshot = new VoiceUtteranceTimingSnapshot(
            _automaticSpeechDetected,
            _automaticSpeechMilliseconds,
            _automaticSilentMilliseconds,
            _automaticLiveAudioMilliseconds);

        if (VoiceUtteranceEndPolicy.ShouldComplete(
                snapshot,
                _automaticTrailingSilence))
        {
            _automaticUtteranceCompleted?.TrySetResult(true);
        }
    }

    private double GetSpeechThreshold() =>
        Math.Clamp(
            _noiseFloorAmplitude * 1.8,
            MinimumSpeechAverageAmplitudeThreshold,
            MaximumSpeechAverageAmplitudeThreshold);

    private void ResetAutomaticListening()
    {
        lock (_sync)
        {
            _automaticUtteranceCompleted = null;
            _automaticTrailingSilence = TimeSpan.Zero;
            _automaticSilentMilliseconds = 0;
            _automaticSpeechMilliseconds = 0;
            _automaticConsecutiveSpeechMilliseconds = 0;
            _automaticLiveAudioMilliseconds = 0;
            _automaticSpeechDetected = false;
            _automaticListening = false;
            _pendingInitialPcmAudio = [];
            _pendingInitialSpeechPcmAudio = [];
        }
    }

    private void Recorder_RecordingStopped(object? sender, StoppedEventArgs e)
    {
        _recordingStopped?.TrySetResult(e.Exception);
    }

    private void CleanupRecorder(bool deleteRecording)
    {
        WaveInEvent? recorder;
        WaveFileWriter? writer;
        string? recordingPath;

        lock (_sync)
        {
            recorder = _recorder;
            writer = _writer;
            recordingPath = _recordingPath;

            _recorder = null;
            _writer = null;
            _recordingStopped = null;
            _recordingPath = null;
            _recordedBytes = 0;
            _audioBufferCount = 0;
            _speechBufferCount = 0;
            _peakAmplitude = 0;
            IsListening = false;
        }

        if (recorder is not null)
        {
            recorder.DataAvailable -= Recorder_DataAvailable;
            recorder.RecordingStopped -= Recorder_RecordingStopped;
            recorder.Dispose();
        }

        writer?.Dispose();

        if (deleteRecording)
        {
            TryDeleteFile(recordingPath);
        }
    }

    private void WritePendingInitialAudio()
    {
        byte[] initialAudio;
        byte[] initialSpeechAudio;
        lock (_sync)
        {
            initialAudio = _pendingInitialPcmAudio;
            initialSpeechAudio = _pendingInitialSpeechPcmAudio;
            _pendingInitialPcmAudio = [];
            _pendingInitialSpeechPcmAudio = [];
        }

        lock (_sync)
        {
            if (_writer is null)
            {
                return;
            }

            var chunkSize = SampleRate * Channels * (BitsPerSample / 8) / 10;

            if (initialAudio.Length > 0)
            {
                _writer.Write(initialAudio, 0, initialAudio.Length);
                _recordedBytes += initialAudio.Length;

                for (var offset = 0; offset < initialAudio.Length; offset += chunkSize)
                {
                    var length = Math.Min(chunkSize, initialAudio.Length - offset);
                    var chunk = new byte[length];
                    Buffer.BlockCopy(initialAudio, offset, chunk, 0, length);

                    var audio = AnalyzeAudioBuffer(chunk, length);
                    TrackAudioQuality(audio);
                }
            }

            if (_automaticListening && initialSpeechAudio.Length > 0)
            {
                for (var offset = 0; offset < initialSpeechAudio.Length; offset += chunkSize)
                {
                    var length = Math.Min(
                        chunkSize,
                        initialSpeechAudio.Length - offset);
                    var chunk = new byte[length];
                    Buffer.BlockCopy(
                        initialSpeechAudio,
                        offset,
                        chunk,
                        0,
                        length);

                    var audio = AnalyzeAudioBuffer(chunk, length);
                    ObserveAutomaticUtterance(audio, length);
                }
            }
        }
    }

    private int ResolveInputDeviceNumber()
    {
        var deviceCount = WaveIn.DeviceCount;
        if (deviceCount <= 0)
        {
            throw new InvalidOperationException("Windows no encontró micrófonos disponibles.");
        }

        return InputDeviceNumber >= 0 && InputDeviceNumber < deviceCount
            ? InputDeviceNumber
            : 0;
    }

    private static double EstimateRecognitionConfidence(
        string text,
        long recordedBytes,
        VoiceCaptureQuality quality)
    {
        var durationSeconds = recordedBytes /
            (double)(SampleRate * Channels * (BitsPerSample / 8));
        var speechRatio = quality.BufferCount == 0
            ? 0
            : quality.SpeechBufferCount / (double)quality.BufferCount;

        var confidence = 0.82;

        if (durationSeconds < 0.45)
        {
            confidence -= 0.22;
        }
        else if (durationSeconds < 0.8)
        {
            confidence -= 0.08;
        }

        if (quality.PeakAmplitude < 700)
        {
            confidence -= 0.20;
        }
        else if (quality.PeakAmplitude > 31_500)
        {
            confidence -= 0.08;
        }

        if (speechRatio < 0.10)
        {
            confidence -= 0.18;
        }
        else if (speechRatio < 0.20)
        {
            confidence -= 0.08;
        }

        var wordCount = text.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries).Length;
        if (wordCount == 1 &&
            text is not ("confirmar" or "cancela" or "cancelar"))
        {
            confidence -= 0.06;
        }

        return Math.Clamp(confidence, 0.35, 0.96);
    }

    private readonly record struct AudioBufferAnalysis(
        double AverageAmplitude,
        int PeakAmplitude);

    private readonly record struct VoiceCaptureQuality(
        int BufferCount,
        int SpeechBufferCount,
        int PeakAmplitude,
        double NoiseFloorAmplitude);

    private static void AppendVoiceCaptureLog(
        long recordedBytes,
        VoiceCaptureQuality quality,
        VoiceRecognitionResult result)
    {
        try
        {
            Directory.CreateDirectory(NexoDataPaths.LogsDirectory);

            var durationMilliseconds = (long)Math.Round(
                recordedBytes * 1000d /
                (SampleRate * Channels * (BitsPerSample / 8)));
            var speechRatio = quality.BufferCount == 0
                ? 0
                : quality.SpeechBufferCount / (double)quality.BufferCount;
            var wordCount = result.Text.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries).Length;

            var line =
                $"{DateTimeOffset.Now:O}\t" +
                $"duration_ms={durationMilliseconds}\t" +
                $"peak={quality.PeakAmplitude}\t" +
                $"noise={quality.NoiseFloorAmplitude:F1}\t" +
                $"speech_ratio={speechRatio:F3}\t" +
                $"recognized={result.IsRecognized}\t" +
                $"words={wordCount}\t" +
                $"confirmation={result.RequiresConfirmation}";

            File.AppendAllText(
                NexoDataPaths.VoiceCaptureLog,
                line + Environment.NewLine);
        }
        catch (IOException)
        {
            // El diagnóstico de voz nunca debe interrumpir una orden.
        }
        catch (UnauthorizedAccessException)
        {
            // El diagnóstico es opcional.
        }
    }

    private bool IsUsableModelFile()
    {
        try
        {
            return File.Exists(_modelPath) &&
                   new FileInfo(_modelPath).Length >= MinimumModelBytes;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void TryDeleteFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // El archivo temporal se limpiará en el siguiente inicio.
        }
        catch (UnauthorizedAccessException)
        {
            // No se interrumpe Nexo por un archivo temporal bloqueado.
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
