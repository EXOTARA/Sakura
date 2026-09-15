using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using Nexo.Core.Vision;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;

namespace Nexo.Windows.Recording;

/// <summary>Cómo terminó una grabación.</summary>
public sealed record ScreenRecordingResult(bool Success, string Path, TimeSpan Duration, string Detail);

/// <summary>
/// 2026-09-15 — grabar la pantalla en MP4 (Adler: «una forma de grabar la pantalla desde Kohana, tipo
/// NVIDIA»).
///
/// Todo con piezas de Windows, sin programas aparte: Windows.Graphics.Capture entrega los fotogramas
/// de la pantalla ya en la tarjeta gráfica, se copian a una textura propia —el fotograma de la captura
/// se recicla en cuanto llega el siguiente— y MediaTranscoder los codifica a H.264 con el codificador
/// de la tarjeta, junto con el sonido en AAC. Los fotogramas nunca pasan por la memoria del procesador.
///
/// Windows solo manda un fotograma cuando algo cambia. Con la pantalla quieta se repite el último cada
/// cierto tiempo para que el vídeo no se congele ni se desfase del sonido.
/// </summary>
public sealed class WindowsScreenRecorder : IAsyncDisposable
{
    private static readonly TimeSpan RepeatAfter = TimeSpan.FromMilliseconds(100);

    private readonly BlockingCollection<(IDirect3DSurface Surface, TimeSpan Timestamp)> _frames = new(boundedCapacity: 8);
    private readonly CancellationTokenSource _stop = new();

    private ID3D11Device? _device;
    private IDirect3DDevice? _winRtDevice;
    private GraphicsCaptureItem? _item;
    private Direct3D11CaptureFramePool? _framePool;
    private GraphicsCaptureSession? _session;
    private RecordingAudioMixer? _audio;
    private ScreenRecordingPlan? _plan;
    private Task? _transcode;
    private Stream? _output;
    private string _path = string.Empty;
    private long _startTimestamp;
    private TimeSpan _lastFrameInterval;
    private (IDirect3DSurface Surface, TimeSpan Timestamp)? _lastFrame;
    private string? _failure;

    public static bool IsSupported => GraphicsCaptureSession.IsSupported();

    public bool IsRecording => _transcode is { IsCompleted: false };

    public TimeSpan Elapsed => _startTimestamp == 0
        ? TimeSpan.Zero
        : TimeSpan.FromSeconds((Stopwatch.GetTimestamp() - _startTimestamp) / (double)Stopwatch.Frequency);

    /// <summary>
    /// Empieza a grabar la pantalla <paramref name="monitor"/> en <paramref name="path"/>. Vuelve en
    /// cuanto la grabación está en marcha.
    /// </summary>
    public async Task StartAsync(IntPtr monitor, string path, ScreenRecordingOptions options)
    {
        if (IsRecording)
        {
            throw new InvalidOperationException("Ya hay una grabación en marcha.");
        }

        _path = path;
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);

        _device = CaptureInterop.CreateDevice();

        // El codificador usa el mismo dispositivo desde sus propios hilos mientras aquí se copian
        // fotogramas: sin esta protección, dos hilos en el contexto a la vez corrompen la imagen.
        using (var multithread = _device.QueryInterface<ID3D11Multithread>())
        {
            multithread.SetMultithreadProtected(true);
        }

        _winRtDevice = CaptureInterop.CreateWinRtDevice(_device);
        _item = CaptureInterop.CreateItemForMonitor(monitor);
        _plan = ScreenRecordingPlan.For(_item.Size.Width, _item.Size.Height, options.FramesPerSecond);
        _lastFrameInterval = TimeSpan.FromSeconds(1d / _plan.FramesPerSecond);

        _startTimestamp = Stopwatch.GetTimestamp();
        _audio = options.SystemAudio || options.Microphone
            ? new RecordingAudioMixer(options.SystemAudio, options.Microphone, _startTimestamp)
            : null;

        var videoProperties = VideoEncodingProperties.CreateUncompressed(
            MediaEncodingSubtypes.Bgra8, (uint)_plan.Width, (uint)_plan.Height);
        var videoDescriptor = new VideoStreamDescriptor(videoProperties);

        MediaStreamSource source;
        AudioStreamDescriptor? audioDescriptor = null;
        if (_audio is { HasAudio: true })
        {
            audioDescriptor = new AudioStreamDescriptor(AudioEncodingProperties.CreatePcm(
                RecordingAudioMixer.SampleRate, RecordingAudioMixer.Channels, RecordingAudioMixer.BitsPerSample));
            source = new MediaStreamSource(videoDescriptor, audioDescriptor);
        }
        else
        {
            source = new MediaStreamSource(videoDescriptor);
        }

        source.BufferTime = TimeSpan.Zero;
        source.CanSeek = false;
        source.SampleRequested += (_, e) => OnSampleRequested(e, audioDescriptor);

        var profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD1080p);
        profile.Video.Width = (uint)_plan.Width;
        profile.Video.Height = (uint)_plan.Height;
        profile.Video.Bitrate = _plan.VideoBitsPerSecond;
        profile.Video.FrameRate.Numerator = (uint)_plan.FramesPerSecond;
        profile.Video.FrameRate.Denominator = 1;
        profile.Video.PixelAspectRatio.Numerator = 1;
        profile.Video.PixelAspectRatio.Denominator = 1;
        profile.Audio = audioDescriptor is null
            ? null
            : AudioEncodingProperties.CreateAac(RecordingAudioMixer.SampleRate, RecordingAudioMixer.Channels, 192_000);

        _output = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read);
        var transcoder = new MediaTranscoder { HardwareAccelerationEnabled = true };
        var prepared = await transcoder.PrepareMediaStreamSourceTranscodeAsync(
            source, _output.AsRandomAccessStream(), profile);

        if (!prepared.CanTranscode)
        {
            await DisposeCaptureAsync();
            throw new InvalidOperationException($"Windows no puede codificar la grabación ({prepared.FailureReason}).");
        }

        _framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
            _winRtDevice, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, _item.Size);
        _framePool.FrameArrived += (pool, _) => OnFrameArrived(pool);
        _session = _framePool.CreateCaptureSession(_item);
        _session.IsCursorCaptureEnabled = true;
        TryHideCaptureBorder(_session);

        _audio?.Start();
        _session.StartCapture();
        _transcode = prepared.TranscodeAsync().AsTask();
    }

    /// <summary>Para la grabación y espera a que el MP4 quede cerrado y completo.</summary>
    public async Task<ScreenRecordingResult> StopAsync()
    {
        var duration = Elapsed;
        _stop.Cancel();
        _frames.CompleteAdding();

        try
        {
            if (_transcode is not null)
            {
                await _transcode.WaitAsync(TimeSpan.FromSeconds(20));
            }
        }
        catch (Exception exception) when (exception is TimeoutException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            _failure ??= exception.Message;
        }
        finally
        {
            await DisposeCaptureAsync();
        }

        var size = File.Exists(_path) ? new FileInfo(_path).Length : 0;
        return _failure is null && size > 0
            ? new ScreenRecordingResult(true, _path, duration, "Grabación guardada.")
            : new ScreenRecordingResult(false, _path, duration, _failure ?? "La grabación quedó vacía.");
    }

    private void OnFrameArrived(Direct3D11CaptureFramePool pool)
    {
        using var frame = pool.TryGetNextFrame();
        if (frame is null || _stop.IsCancellationRequested || _device is null || _plan is null)
        {
            return;
        }

        var timestamp = frame.SystemRelativeTime -
                        TimeSpan.FromSeconds(_startTimestamp / (double)Stopwatch.Frequency);
        if (timestamp < TimeSpan.Zero)
        {
            return;
        }

        // Más fotogramas de los que caben en la tasa elegida no mejoran el vídeo: se descartan.
        if (_lastFrame is { } last && timestamp - last.Timestamp < _lastFrameInterval * 0.5)
        {
            return;
        }

        try
        {
            using var source = CaptureInterop.TextureFrom(frame.Surface);
            var description = new Texture2DDescription
            {
                Width = (uint)_plan.Width,
                Height = (uint)_plan.Height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
                CPUAccessFlags = CpuAccessFlags.None,
                MiscFlags = ResourceOptionFlags.None
            };

            using var copy = _device.CreateTexture2D(description);
            var width = Math.Min(_plan.Width, frame.ContentSize.Width);
            var height = Math.Min(_plan.Height, frame.ContentSize.Height);
            _device.ImmediateContext.CopySubresourceRegion(
                copy, 0, 0, 0, 0, source, 0, new Vortice.Mathematics.Box(0, 0, 0, width, height, 1));

            var surface = CaptureInterop.CreateWinRtSurface(copy);
            var item = (surface, timestamp);
            _lastFrame = item;
            _frames.TryAdd(item);
        }
        catch (InvalidOperationException)
        {
            // Llegó un fotograma justo al parar: la cola ya está cerrada.
        }
        catch (Exception exception) when (exception is System.Runtime.InteropServices.COMException or SharpGen.Runtime.SharpGenException)
        {
            _failure ??= exception.Message;
        }

        // Si la pantalla cambió de tamaño (resolución, escala), el búfer de la captura se rehace.
        if (frame.ContentSize.Width != _item!.Size.Width || frame.ContentSize.Height != _item.Size.Height)
        {
            pool.Recreate(_winRtDevice, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, frame.ContentSize);
        }
    }

    private void OnSampleRequested(MediaStreamSourceSampleRequestedEventArgs e, AudioStreamDescriptor? audioDescriptor)
    {
        var request = e.Request;
        var deferral = request.GetDeferral();
        try
        {
            if (audioDescriptor is not null && ReferenceEquals(request.StreamDescriptor, audioDescriptor))
            {
                var chunk = _audio!.NextChunk(_stop.Token);
                if (chunk is { } audio)
                {
                    var sample = MediaStreamSample.CreateFromBuffer(audio.Pcm.AsBuffer(), audio.Timestamp);
                    sample.Duration = audio.Duration;
                    request.Sample = sample;
                }

                return;
            }

            if (_frames.TryTake(out var frame, (int)RepeatAfter.TotalMilliseconds, _stop.Token) ||
                RepeatLast(out frame))
            {
                request.Sample = MediaStreamSample.CreateFromDirect3D11Surface(frame.Surface, frame.Timestamp);
            }
        }
        catch (OperationCanceledException)
        {
            // Al parar se deja la petición sin muestra: así el codificador sabe que el vídeo terminó.
        }
        finally
        {
            deferral.Complete();
        }
    }

    /// <summary>Con la pantalla quieta, el último fotograma otra vez, con la hora de ahora.</summary>
    private bool RepeatLast(out (IDirect3DSurface Surface, TimeSpan Timestamp) frame)
    {
        if (_stop.IsCancellationRequested || _lastFrame is not { } last)
        {
            frame = default;
            return false;
        }

        var now = Elapsed;
        frame = (last.Surface, now > last.Timestamp ? now : last.Timestamp + _lastFrameInterval);
        _lastFrame = frame;
        return true;
    }

    private static void TryHideCaptureBorder(GraphicsCaptureSession session)
    {
        try
        {
            // Windows 11 dibuja un borde amarillo alrededor de lo que se captura. En una grabación
            // larga molesta; Sakura ya enseña su propio aviso de que está grabando.
            session.IsBorderRequired = false;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or InvalidCastException or System.Runtime.InteropServices.COMException)
        {
            // En Windows 10 no existe o no está permitido: se queda el borde.
        }
    }

    private Task DisposeCaptureAsync()
    {
        _session?.Dispose();
        _framePool?.Dispose();
        _audio?.Dispose();
        _output?.Dispose();
        _device?.Dispose();
        _session = null;
        _framePool = null;
        _audio = null;
        _output = null;
        _device = null;
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (IsRecording)
        {
            await StopAsync();
        }
        else
        {
            await DisposeCaptureAsync();
        }

        _stop.Dispose();
        _frames.Dispose();
    }
}
