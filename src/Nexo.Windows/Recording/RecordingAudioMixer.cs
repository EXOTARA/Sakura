using System.Diagnostics;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Nexo.Windows.Recording;

/// <summary>
/// 2026-09-15 — el sonido de una grabación: lo que suena en el equipo y, si se pide, el micrófono,
/// mezclados en PCM de 48 kHz estéreo para el codificador AAC.
///
/// **El ritmo lo marca el reloj, no el sonido.** La captura en bucle de Windows no manda nada mientras
/// no suena nada, así que contar solo lo recibido haría que el audio se adelantara al vídeo tras cada
/// silencio. Se entregan trozos de 20 ms cuando el reloj llega a su hora, y lo que falte se rellena con
/// silencio: el sonido queda siempre alineado con la imagen.
/// </summary>
internal sealed class RecordingAudioMixer : IDisposable
{
    public const int SampleRate = 48_000;
    public const int Channels = 2;
    public const int BitsPerSample = 16;

    private static readonly TimeSpan ChunkLength = TimeSpan.FromMilliseconds(20);

    /// <summary>
    /// Margen de retraso con que se entrega cada trozo: da tiempo a que llegue el sonido capturado
    /// de ese instante antes de darlo por silencio.
    /// </summary>
    private static readonly TimeSpan Latency = TimeSpan.FromMilliseconds(120);

    private readonly WasapiLoopbackCapture? _loopback;
    private readonly WasapiCapture? _microphone;
    private readonly BufferedWaveProvider? _loopbackBuffer;
    private readonly BufferedWaveProvider? _microphoneBuffer;
    private readonly ISampleProvider _mix;
    private readonly long _startTimestamp;
    private long _framesDelivered;

    public RecordingAudioMixer(bool systemAudio, bool microphone, long startTimestamp)
    {
        _startTimestamp = startTimestamp;
        var target = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, Channels);
        var mixer = new MixingSampleProvider(target) { ReadFully = true };

        if (systemAudio)
        {
            _loopback = new WasapiLoopbackCapture();
            _loopbackBuffer = Buffer(_loopback.WaveFormat);
            _loopback.DataAvailable += (_, e) => _loopbackBuffer.AddSamples(e.Buffer, 0, e.BytesRecorded);
            mixer.AddMixerInput(ToTarget(_loopbackBuffer));
        }

        if (microphone)
        {
            try
            {
                var device = new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
                _microphone = new WasapiCapture(device) { ShareMode = AudioClientShareMode.Shared };
                _microphoneBuffer = Buffer(_microphone.WaveFormat);
                _microphone.DataAvailable += (_, e) => _microphoneBuffer.AddSamples(e.Buffer, 0, e.BytesRecorded);
                mixer.AddMixerInput(ToTarget(_microphoneBuffer));
            }
            catch (Exception exception) when (exception is System.Runtime.InteropServices.COMException or InvalidOperationException)
            {
                // Sin micrófono se graba igual; la interfaz ya avisó de que se pidió.
                _microphone = null;
            }
        }

        _mix = mixer;
    }

    public bool HasAudio => _loopback is not null || _microphone is not null;

    public void Start()
    {
        _loopback?.StartRecording();
        _microphone?.StartRecording();
    }

    /// <summary>
    /// El siguiente trozo de 20 ms en PCM de 16 bits y su marca de tiempo desde el inicio de la
    /// grabación. Espera a que el reloj llegue a esa hora; devuelve nulo si se canceló.
    /// </summary>
    public (byte[] Pcm, TimeSpan Timestamp, TimeSpan Duration)? NextChunk(CancellationToken cancellationToken)
    {
        var frames = (int)(SampleRate * ChunkLength.TotalSeconds);
        var timestamp = TimeSpan.FromSeconds(_framesDelivered / (double)SampleRate);

        while (Elapsed() < timestamp + Latency)
        {
            if (cancellationToken.WaitHandle.WaitOne(5))
            {
                return null;
            }
        }

        var floats = new float[frames * Channels];
        _mix.Read(floats, 0, floats.Length);

        var pcm = new byte[floats.Length * 2];
        for (var i = 0; i < floats.Length; i++)
        {
            var value = (short)Math.Clamp(floats[i] * short.MaxValue, short.MinValue, short.MaxValue);
            pcm[i * 2] = (byte)value;
            pcm[(i * 2) + 1] = (byte)(value >> 8);
        }

        _framesDelivered += frames;
        return (pcm, timestamp, ChunkLength);
    }

    private TimeSpan Elapsed() =>
        TimeSpan.FromSeconds((Stopwatch.GetTimestamp() - _startTimestamp) / (double)Stopwatch.Frequency);

    private static BufferedWaveProvider Buffer(WaveFormat format) =>
        new(format) { DiscardOnBufferOverflow = true, BufferDuration = TimeSpan.FromSeconds(2), ReadFully = true };

    private static ISampleProvider ToTarget(IWaveProvider source)
    {
        var samples = source.ToSampleProvider();
        if (samples.WaveFormat.Channels == 1)
        {
            samples = new MonoToStereoSampleProvider(samples);
        }
        else if (samples.WaveFormat.Channels > 2)
        {
            samples = new MultiplexingSampleProvider([samples], 2);
        }

        return samples.WaveFormat.SampleRate == SampleRate
            ? samples
            : new WdlResamplingSampleProvider(samples, SampleRate);
    }

    public void Dispose()
    {
        try
        {
            _loopback?.StopRecording();
            _microphone?.StopRecording();
        }
        catch (Exception exception) when (exception is System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
            // Un dispositivo que desapareció a mitad de la grabación ya está parado.
        }

        _loopback?.Dispose();
        _microphone?.Dispose();
    }
}
