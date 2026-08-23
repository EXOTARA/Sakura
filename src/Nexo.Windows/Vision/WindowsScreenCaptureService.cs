using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using Nexo.Core.Vision;

namespace Nexo.Windows.Vision;

public sealed class WindowsScreenCaptureService : IScreenCaptureService
{
    private const int DwmwaCloaked = 14;
    private const uint PwRenderFullContent = 0x00000002;

    public IReadOnlyList<VisionCaptureTarget> GetAvailableTargets(long excludedWindowHandle = 0)
    {
        var targets = new List<VisionCaptureTarget>();
        AddMonitorTargets(targets);
        AddWindowTargets(targets, new IntPtr(excludedWindowHandle));

        return targets
            .OrderBy(target => target.Kind)
            .ThenBy(target => target.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public Task<VisionCaptureResult> CaptureAsync(
        VisionCaptureTarget target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (target.IsSensitive)
            {
                return VisionCaptureResult.Failed(
                    "Sakura bloqueó esa ventana porque podría mostrar contraseñas o credenciales.");
            }

            return target.Kind switch
            {
                // La zona y el monitor comparten implementación porque son la misma operación: un
                // rectángulo de la pantalla. Lo que cambia es de dónde salen sus medidas.
                VisionCaptureKind.Monitor or VisionCaptureKind.Region => CaptureMonitor(target),
                VisionCaptureKind.Window => CaptureWindow(target),
                _ => VisionCaptureResult.Failed("El tipo de captura no es compatible.")
            };
        }, cancellationToken);
    }

    private static VisionCaptureResult CaptureMonitor(VisionCaptureTarget target)
    {
        if (target.Width <= 0 || target.Height <= 0)
        {
            return VisionCaptureResult.Failed("El monitor seleccionado no tiene un tamaño válido.");
        }

        try
        {
            using var bitmap = new Bitmap(
                target.Width,
                target.Height,
                PixelFormat.Format24bppRgb);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(
                target.Left,
                target.Top,
                0,
                0,
                new Size(target.Width, target.Height),
                CopyPixelOperation.SourceCopy);

            return Encode(bitmap, target.Title);
        }
        catch (Exception exception) when (
            exception is ExternalException or ArgumentException)
        {
            return VisionCaptureResult.Failed(
                $"No pude capturar el monitor: {exception.Message}");
        }
    }

    private static VisionCaptureResult CaptureWindow(VisionCaptureTarget target)
    {
        var handle = new IntPtr(target.NativeHandle);
        if (handle == IntPtr.Zero || !IsWindow(handle))
        {
            return VisionCaptureResult.Failed("La ventana ya no está disponible.");
        }

        if (!TryGetWindowBounds(handle, out var bounds) || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return VisionCaptureResult.Failed("No pude obtener el tamaño de la ventana.");
        }

        try
        {
            using var bitmap = new Bitmap(
                bounds.Width,
                bounds.Height,
                PixelFormat.Format24bppRgb);
            using var graphics = Graphics.FromImage(bitmap);
            var deviceContext = graphics.GetHdc();
            var printed = false;

            try
            {
                printed = PrintWindow(handle, deviceContext, PwRenderFullContent);
            }
            finally
            {
                graphics.ReleaseHdc(deviceContext);
            }

            if (!printed || IsMostlyBlank(bitmap))
            {
                graphics.CopyFromScreen(
                    bounds.Left,
                    bounds.Top,
                    0,
                    0,
                    new Size(bounds.Width, bounds.Height),
                    CopyPixelOperation.SourceCopy);
            }

            return Encode(bitmap, target.Title);
        }
        catch (Exception exception) when (
            exception is ExternalException or ArgumentException)
        {
            return VisionCaptureResult.Failed(
                $"No pude capturar la ventana: {exception.Message}");
        }
    }

    private static VisionCaptureResult Encode(Bitmap bitmap, string title)
    {
        using var scaled = CreateScaledCopy(bitmap, 1600);
        var output = scaled ?? bitmap;
        using var stream = new MemoryStream();
        output.Save(stream, ImageFormat.Png);
        return VisionCaptureResult.Success(
            title,
            stream.ToArray(),
            output.Width,
            output.Height);
    }

    private static Bitmap? CreateScaledCopy(Bitmap source, int maximumDimension)
    {
        var largestDimension = Math.Max(source.Width, source.Height);
        if (largestDimension <= maximumDimension)
        {
            return null;
        }

        var scale = maximumDimension / (double)largestDimension;
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));
        var resized = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        using var graphics = Graphics.FromImage(resized);
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        graphics.DrawImage(source, 0, 0, width, height);
        return resized;
    }

    private static bool IsMostlyBlank(Bitmap bitmap)
    {
        var samples = 0;
        var nearBlack = 0;
        var stepX = Math.Max(1, bitmap.Width / 8);
        var stepY = Math.Max(1, bitmap.Height / 8);

        for (var y = stepY / 2; y < bitmap.Height; y += stepY)
        {
            for (var x = stepX / 2; x < bitmap.Width; x += stepX)
            {
                var pixel = bitmap.GetPixel(x, y);
                samples++;
                if (pixel.R < 6 && pixel.G < 6 && pixel.B < 6)
                {
                    nearBlack++;
                }
            }
        }

        return samples > 0 && nearBlack >= samples * 0.92;
    }

    private static void AddMonitorTargets(List<VisionCaptureTarget> targets)
    {
        var index = 0;
        EnumDisplayMonitors(
            IntPtr.Zero,
            IntPtr.Zero,
            (IntPtr monitor, IntPtr deviceContext, ref NativeRect rect, IntPtr data) =>
            {
                index++;
                var info = new MonitorInfoEx
                {
                    Size = Marshal.SizeOf<MonitorInfoEx>(),
                    DeviceName = string.Empty
                };
                _ = GetMonitorInfo(monitor, ref info);
                var title = (info.Flags & 1) != 0
                    ? $"Monitor {index} (principal)"
                    : $"Monitor {index}";

                targets.Add(new VisionCaptureTarget(
                    $"monitor:{monitor.ToInt64()}",
                    monitor.ToInt64(),
                    title,
                    info.DeviceName?.TrimEnd('\0') ?? string.Empty,
                    VisionCaptureKind.Monitor,
                    rect.Left,
                    rect.Top,
                    rect.Right - rect.Left,
                    rect.Bottom - rect.Top));
                return true;
            },
            IntPtr.Zero);
    }

    private static void AddWindowTargets(
        List<VisionCaptureTarget> targets,
        IntPtr excludedWindowHandle)
    {
        EnumWindows((handle, _) =>
        {
            if (handle == excludedWindowHandle ||
                !IsWindowVisible(handle) ||
                IsIconic(handle) ||
                IsCloaked(handle))
            {
                return true;
            }

            var title = ReadWindowTitle(handle);
            if (string.IsNullOrWhiteSpace(title) ||
                !TryGetWindowBounds(handle, out var bounds) ||
                bounds.Width < 160 || bounds.Height < 100)
            {
                return true;
            }

            GetWindowThreadProcessId(handle, out var processId);
            var processName = ReadProcessName(processId);
            var sensitive = VisionPrivacyPolicy.IsSensitive(title, processName);
            if (sensitive)
            {
                return true;
            }

            targets.Add(new VisionCaptureTarget(
                $"window:{handle.ToInt64()}",
                handle.ToInt64(),
                title,
                string.IsNullOrWhiteSpace(processName) ? "Aplicación" : processName,
                VisionCaptureKind.Window,
                bounds.Left,
                bounds.Top,
                bounds.Width,
                bounds.Height,
                sensitive));
            return true;
        }, IntPtr.Zero);
    }

    private static string ReadWindowTitle(IntPtr handle)
    {
        var length = GetWindowTextLength(handle);
        if (length <= 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(length + 1);
        _ = GetWindowText(handle, builder, builder.Capacity);
        return builder.ToString().Trim();
    }

    private static string ReadProcessName(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool IsCloaked(IntPtr handle)
    {
        if (DwmGetWindowAttribute(
                handle,
                DwmwaCloaked,
                out var cloaked,
                Marshal.SizeOf<int>()) != 0)
        {
            return false;
        }

        return cloaked != 0;
    }

    private static bool TryGetWindowBounds(IntPtr handle, out CaptureBounds bounds)
    {
        bounds = default;
        if (!GetWindowRect(handle, out var rect))
        {
            return false;
        }

        bounds = new CaptureBounds(
            rect.Left,
            rect.Top,
            rect.Right - rect.Left,
            rect.Bottom - rect.Top);
        return true;
    }

    private readonly record struct CaptureBounds(int Left, int Top, int Width, int Height);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfoEx
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect WorkArea;
        public int Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
    }

    private delegate bool EnumWindowsCallback(IntPtr handle, IntPtr parameter);
    private delegate bool MonitorEnumCallback(
        IntPtr monitor,
        IntPtr deviceContext,
        ref NativeRect monitorRect,
        IntPtr data);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr handle);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr handle, StringBuilder text, int maxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr handle, out NativeRect rect);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool PrintWindow(IntPtr handle, IntPtr deviceContext, uint flags);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(
        IntPtr handle,
        int attribute,
        out int value,
        int size);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(
        IntPtr deviceContext,
        IntPtr clipRect,
        MonitorEnumCallback callback,
        IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfoEx info);
}
