using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Nexo.Core.Ai;
using Nexo.Core.Storage;
using Nexo.Core.Vision;
using Nexo.Windows.Recording;
using Nexo.Windows.Storage;

namespace Nexo.App;

/// <summary>
/// 2026-09-15 — la pestaña Captura: capturar una región o la pantalla con retraso, y grabar la
/// pantalla (Adler). Va en su propio archivo porque MainWindow ya es muy largo y esto es un bloque con
/// su propio ciclo: empezar, esperar, guardar o compartir.
/// </summary>
public partial class MainWindow
{
    /// <summary>Alt + Shift + S — capturar una región.</summary>
    private const int RegionCaptureHotkeyId = 0x4E60;

    /// <summary>
    /// Alt + Shift + G — empezar o parar la grabación de la pantalla. No Alt + F9 ni Alt + Shift + R:
    /// en el equipo de Adler ya los tenía NVIDIA Overlay, y quien graba juegos suele tenerlo.
    /// </summary>
    private const int RecordHotkeyId = 0x4E61;

    private const uint VirtualKeyS = 0x53;
    private const uint VirtualKeyG = 0x47;

    private readonly RecordingIndicatorWindow _recordingIndicator = new();
    private readonly DispatcherTimer _recordingTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private WindowsScreenRecorder? _recorder;
    private bool _recordingTransition;
    private bool _capturing;

    private void WireCaptureFeatures()
    {
        _captureView.RegionCaptureRequested += async (_, _) => await CaptureRegionAsync();
        _captureView.DelayedCaptureRequested += async (_, seconds) => await CaptureDelayedAsync(seconds);
        _captureView.RecordingToggleRequested += async (_, _) => await ToggleRecordingAsync();
        _recordingIndicator.StopRequested += async (_, _) => await ToggleRecordingAsync();
        _recordingTimer.Tick += (_, _) =>
        {
            if (_recorder is null)
            {
                return;
            }

            _recordingIndicator.UpdateElapsed(_recorder.Elapsed);
            _captureView.SetRecordingState(true, _recorder.Elapsed);
        };
    }

    private void RegisterCaptureHotkeys(IntPtr windowHandle)
    {
        if (!RegisterHotKey(windowHandle, RegionCaptureHotkeyId, ModAlt | ModShift, VirtualKeyS))
        {
            _assistantView.AddSakuraMessage(
                "Alt + Shift + S ya está siendo utilizado por otra aplicación; la captura de región sigue en la pestaña Captura.");
        }

        if (!RegisterHotKey(windowHandle, RecordHotkeyId, ModAlt | ModShift, VirtualKeyG))
        {
            _assistantView.AddSakuraMessage(
                "Alt + Shift + G ya está siendo utilizado por otra aplicación; la grabación sigue en la pestaña Captura.");
        }
    }

    private void UnregisterCaptureHotkeys(IntPtr windowHandle)
    {
        UnregisterHotKey(windowHandle, RegionCaptureHotkeyId);
        UnregisterHotKey(windowHandle, RecordHotkeyId);
    }

    /// <summary>Atiende los atajos de captura; devuelve si el mensaje era suyo.</summary>
    private bool HandleCaptureHotkey(int hotkeyId)
    {
        switch (hotkeyId)
        {
            case RegionCaptureHotkeyId:
                _ = CaptureRegionAsync();
                return true;
            case RecordHotkeyId:
                _ = ToggleRecordingAsync();
                return true;
            default:
                return false;
        }
    }

    // ───────────────────────────── capturas ─────────────────────────────

    private async Task CaptureRegionAsync()
    {
        if (_isClosed || _capturing || _translatingRegion)
        {
            return;
        }

        _capturing = true;
        try
        {
            await GetShellOutOfTheWayAsync();
            var area = await _regionPickerWindow.PickAsync("Arrastra sobre lo que quieras capturar");
            if (area is not { Width: > 0, Height: > 0 } region)
            {
                return;
            }

            var target = new VisionCaptureTarget(
                "region", 0, "Zona de la pantalla", string.Empty, VisionCaptureKind.Region,
                region.X, region.Y, region.Width, region.Height);
            await CaptureAndOfferAsync(target);
        }
        finally
        {
            _capturing = false;
        }
    }

    /// <summary>
    /// Espera unos segundos —para abrir un menú o preparar lo que se quiere enseñar— y captura la
    /// pantalla donde está el ratón. La cuenta atrás se ve en el aviso de arriba, que se quita antes
    /// de capturar para no salir en la imagen.
    /// </summary>
    private async Task CaptureDelayedAsync(int seconds)
    {
        if (_isClosed || _capturing)
        {
            return;
        }

        _capturing = true;
        try
        {
            // El permiso se pregunta ANTES de la cuenta atrás: si se preguntara al final, el
            // diálogo le robaría el foco a la aplicación y cerraría el menú que la persona acaba
            // de preparar. Una vez concedido, la captura de abajo no vuelve a preguntar.
            if (!TryGetLensPermission(
                    "Capturar la pantalla con cuenta atrás.",
                    targetApp: null,
                    out var delayedDenial))
            {
                _capsuleWindow.ShowMessage(
                    CapsuleKind.Warning, "Lens no permitido", delayedDenial, _preferences.Position, force: true);
                return;
            }

            await GetShellOutOfTheWayAsync();
            for (var remaining = seconds; remaining > 0; remaining--)
            {
                _capsuleWindow.ShowMessage(
                    CapsuleKind.Processing,
                    $"Captura en {remaining}…",
                    "Prepara la pantalla; el aviso no saldrá en la imagen.",
                    _preferences.Position);
                await Task.Delay(1000, _lifetimeCancellation.Token);
            }

            _capsuleWindow.HideImmediately();
            await Task.Delay(180, _lifetimeCancellation.Token);

            var bounds = MonitorBoundsUnderCursor();
            var target = new VisionCaptureTarget(
                "screen", 0, "Pantalla", string.Empty, VisionCaptureKind.Region,
                bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top);
            await CaptureAndOfferAsync(target, permissionAlreadyGranted: true);
        }
        catch (OperationCanceledException)
        {
            // Sakura se cerró durante la cuenta atrás.
        }
        finally
        {
            _capturing = false;
        }
    }

    private async Task GetShellOutOfTheWayAsync()
    {
        if (IsShellOnScreen)
        {
            HideAnimated();
            await Task.Delay(260);
        }
    }

    private async Task CaptureAndOfferAsync(VisionCaptureTarget target, bool permissionAlreadyGranted = false)
    {
        // Una captura de pantalla es «ver la pantalla» igual que Lens: pasa por el mismo permiso.
        // Una zona o la pantalla entera no dicen qué aplicación hay debajo, así que aquí solo
        // cuentan el nivel y no las exclusiones por aplicación. La captura con cuenta atrás ya
        // preguntó antes de empezar y no repite el diálogo.
        if (!permissionAlreadyGranted)
        {
            if (!TryGetLensPermission(
                    $"Capturar la pantalla ({target.Title}).",
                    targetApp: null,
                    out var captureDenial))
            {
                _capsuleWindow.ShowMessage(
                    CapsuleKind.Warning, "Lens no permitido", captureDenial, _preferences.Position, force: true);
                return;
            }

            // Se acaba de cerrar el cuadro: se le da un instante a desaparecer para que no salga
            // en la imagen.
            await Task.Delay(150, _lifetimeCancellation.Token);
        }

        var capture = await _screenCaptureService.CaptureAsync(target, _lifetimeCancellation.Token);
        if (!capture.IsSuccess || capture.PngBytes is null)
        {
            _capsuleWindow.ShowMessage(CapsuleKind.Error, "No pude capturar", capture.Detail, _preferences.Position);
            return;
        }

        var preview = new VisionPreviewWindow("Captura de la pantalla", capture.PngBytes, offerKeep: true)
        {
            Topmost = true,
            WindowStartupLocation = WindowStartupLocation.CenterScreen
        };
        preview.ShowDialog();

        switch (preview.Choice)
        {
            case CapturePreviewChoice.Save:
                SaveScreenshot(capture.PngBytes);
                break;
            case CapturePreviewChoice.Copy:
                CopyScreenshot(capture.PngBytes);
                break;
            case CapturePreviewChoice.Ask:
                ShowAnimated();
                UseImageInAssistant(capture.PngBytes, "Captura de la pantalla");
                break;
        }
    }

    private void SaveScreenshot(byte[] png)
    {
        try
        {
            var path = CaptureFileNames.ScreenshotPath(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), DateTimeOffset.Now, File.Exists);
            var folder = Path.GetDirectoryName(path)!;
            Directory.CreateDirectory(folder);

            // ScreenshotPath sigue decidiendo carpeta y nombre con la marca de tiempo, pero ya no es la
            // única defensa: FreshFileWriter numera y crea en una sola operación, así que dos capturas
            // en el mismo segundo no se pisan ni con una carrera.
            var saved = FreshFileWriter.Write(folder, Path.GetFileName(path), png);
            if (!saved.Saved)
            {
                _capsuleWindow.ShowMessage(CapsuleKind.Error, "No pude guardar la captura", saved.Message, _preferences.Position);
                return;
            }

            _capsuleWindow.ShowMessage(
                CapsuleKind.Success, "Captura guardada", "En Imágenes › Sakura", _preferences.Position);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Sin el mensaje crudo: trae la ruta con el usuario de Windows.
            _capsuleWindow.ShowMessage(
                CapsuleKind.Error, "No pude guardar la captura", SakuraDataWriteException.ReasonFor(exception), _preferences.Position);
        }
    }

    private void CopyScreenshot(byte[] png)
    {
        try
        {
            using var stream = new MemoryStream(png);
            var image = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            Clipboard.SetImage(image);
            _capsuleWindow.ShowMessage(
                CapsuleKind.Success, "Captura copiada", "Pégala donde quieras con Ctrl + V.", _preferences.Position);
        }
        catch (Exception exception) when (exception is COMException or ExternalException)
        {
            _capsuleWindow.ShowMessage(CapsuleKind.Error, "No pude copiar la captura", "Otro programa tiene el portapapeles ocupado.", _preferences.Position);
        }
    }

    /// <summary>La imagen pasa a ser el adjunto de la próxima pregunta, igual que con «Analizar una ventana».</summary>
    private void UseImageInAssistant(byte[] png, string title)
    {
        _visualContextExpiryTimer.Stop();
        _visualContextPersistent = false;
        _silentVisualContext = false;
        _visualContextMetadata = null;
        _pendingVisionAttachment = AiImageAttachment.FromBytes(png, "image/png", title);
        _assistantView.SetVisionAttachment(title, png);
        NavigateTo("Assistant", animate: true);
        _capsuleWindow.ShowMessage(
            CapsuleKind.Success,
            "Captura lista",
            "Escribe o di qué quieres saber sobre la imagen.",
            _preferences.Position);
    }

    // ───────────────────────────── grabación ─────────────────────────────

    private async Task ToggleRecordingAsync()
    {
        if (_recordingTransition || _isClosed)
        {
            return;
        }

        _recordingTransition = true;
        try
        {
            if (_recorder is not null)
            {
                await StopRecordingAsync();
            }
            else
            {
                await StartRecordingAsync();
            }
        }
        finally
        {
            _recordingTransition = false;
        }
    }

    private async Task StartRecordingAsync()
    {
        if (!WindowsScreenRecorder.IsSupported)
        {
            _capsuleWindow.ShowMessage(
                CapsuleKind.Error, "No se puede grabar", "Este Windows no permite grabar la pantalla.", _preferences.Position);
            return;
        }

        var monitor = MonitorUnderCursor(out var bounds, out var workArea);
        var path = CaptureFileNames.RecordingPath(
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), DateTimeOffset.Now, File.Exists);
        var options = new ScreenRecordingOptions(_captureView.RecordSystemAudio, _captureView.RecordMicrophone);

        await GetShellOutOfTheWayAsync();

        var recorder = new WindowsScreenRecorder();
        try
        {
            await recorder.StartAsync(monitor, path, options);
        }
        catch (Exception exception) when (exception is InvalidOperationException or COMException or IOException or UnauthorizedAccessException or SharpGen.Runtime.SharpGenException)
        {
            await recorder.DisposeAsync();
            _capsuleWindow.ShowMessage(CapsuleKind.Error, "No pude empezar a grabar", exception.Message, _preferences.Position);
            return;
        }

        _recorder = recorder;
        _recordingIndicator.ShowOn(ToDeviceIndependent(workArea));
        _captureView.SetRecordingState(true, TimeSpan.Zero);
        _recordingTimer.Start();
    }

    private async Task StopRecordingAsync()
    {
        var recorder = _recorder;
        if (recorder is null)
        {
            return;
        }

        _recordingTimer.Stop();
        _recordingIndicator.SetStopping();
        _captureView.SetRecordingState(false, recorder.Elapsed, "Guardando la grabación…");

        var result = await recorder.StopAsync();
        await recorder.DisposeAsync();
        _recorder = null;

        _recordingIndicator.HideIndicator();
        _captureView.SetRecordingState(false, TimeSpan.Zero);

        if (result.Success)
        {
            var minutes = result.Duration.TotalHours >= 1 ? result.Duration.ToString(@"h\:mm\:ss") : result.Duration.ToString(@"m\:ss");
            _capsuleWindow.ShowMessage(
                CapsuleKind.Success, $"Grabación guardada · {minutes}", "En Vídeos › Sakura", _preferences.Position);
        }
        else
        {
            _capsuleWindow.ShowMessage(CapsuleKind.Error, "La grabación falló", result.Detail, _preferences.Position);
        }
    }

    /// <summary>Al cerrar Sakura con una grabación en marcha, se cierra el MP4 para que no quede roto.</summary>
    private void StopRecordingOnExit()
    {
        _recordingTimer.Stop();
        if (_recorder is { } recorder)
        {
            // Fuera del hilo de la interfaz: esperar aquí a una tarea que vuelve a él lo bloquearía.
            Task.Run(async () =>
            {
                await recorder.StopAsync();
                await recorder.DisposeAsync();
            }).Wait(TimeSpan.FromSeconds(15));
            _recorder = null;
        }

        _recordingIndicator.Close();
    }

    // ───────────────────────────── pantallas ─────────────────────────────

    private static IntPtr MonitorUnderCursor(out NativeRect bounds, out NativeRect workArea)
    {
        GetCursorPos(out var cursor);
        var monitor = MonitorFromPoint(cursor, MonitorDefaultToNearest);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        GetMonitorInfo(monitor, ref info);
        bounds = info.Monitor;
        workArea = info.WorkArea;
        return monitor;
    }

    private static NativeRect MonitorBoundsUnderCursor()
    {
        MonitorUnderCursor(out var bounds, out _);
        return bounds;
    }

    private Rect ToDeviceIndependent(NativeRect pixels)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        return new Rect(
            pixels.Left / dpi.DpiScaleX,
            pixels.Top / dpi.DpiScaleY,
            (pixels.Right - pixels.Left) / dpi.DpiScaleX,
            (pixels.Bottom - pixels.Top) / dpi.DpiScaleY);
    }

    private const uint MonitorDefaultToNearest = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect WorkArea;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(ShellCursorPoint point, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
}
