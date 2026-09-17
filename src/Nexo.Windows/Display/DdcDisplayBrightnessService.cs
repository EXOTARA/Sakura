using System.Runtime.InteropServices;
using Nexo.Core.Display;
using Nexo.Core.Shell;

namespace Nexo.Windows.Display;

/// <summary>
/// Diseño D35 — el brillo del monitor por DDC/CI, el canal de control que viaja por el propio cable
/// de vídeo.
///
/// No se usa WMI, que es la vía habitual, porque solo funciona con paneles de portátil: en un
/// equipo de escritorio con monitor externo, la consulta a <c>WmiMonitorBrightness</c> responde
/// «Incompatible» y no hay brillo que leer. Se comprobó en el equipo antes de decidir. DDC/CI, en
/// cambio, habla con el monitor igual que lo hacen sus propios botones físicos.
///
/// Puede no estar disponible —hay monitores que no lo traen y otros que lo traen desactivado en su
/// menú—, y por eso todo devuelve disponibilidad en vez de lanzar: quien llama enseña el mando solo
/// si de verdad hay algo que mover.
/// </summary>
public sealed class DdcDisplayBrightnessService : IDisplayBrightnessService, IDisposable
{
    private const uint MonitorInfoPrimary = 1;

    private readonly object _sync = new();

    /// <summary>
    /// 2026-09-16 — los monitores físicos de cada pantalla, con la principal primero. Antes solo se
    /// pedían los de la principal y la segunda pantalla no se podía tocar (Adler).
    /// </summary>
    private List<PhysicalMonitor[]>? _screens;
    private int _selected;
    private bool _disposed;

    public int DisplayCount
    {
        get
        {
            lock (_sync)
            {
                return TryEnsureMonitorsLocked(out _) ? _screens!.Count : 0;
            }
        }
    }

    public int SelectedDisplay
    {
        get
        {
            lock (_sync)
            {
                return _selected;
            }
        }
        set
        {
            lock (_sync)
            {
                _selected = BrightnessTarget.Clamp(value, _screens?.Count ?? int.MaxValue);
            }
        }
    }

    /// <summary>Los monitores a los que va la orden según la pantalla elegida.</summary>
    private IEnumerable<PhysicalMonitor> TargetsLocked()
    {
        var screens = _screens!;
        var selected = BrightnessTarget.Clamp(_selected, screens.Count);
        return selected == BrightnessTarget.All
            ? screens.SelectMany(screen => screen)
            : screens[selected];
    }

    public BrightnessSnapshot ReadSnapshot()
    {
        lock (_sync)
        {
            if (!TryEnsureMonitorsLocked(out var message))
            {
                return BrightnessSnapshot.Unavailable(message);
            }

            if (TryReadPercentLocked(out var percent))
            {
                return new BrightnessSnapshot(true, QuickControlsPolicy.NormalizePercent(percent));
            }

            // Diseño D57 — un fallo en todos los monitores puede no ser del monitor, sino de los
            // asideros: se sueltan y se vuelve a preguntar una sola vez.
            if (RefreshMonitorsLocked() && TryReadPercentLocked(out percent))
            {
                return new BrightnessSnapshot(true, QuickControlsPolicy.NormalizePercent(percent));
            }

            return BrightnessSnapshot.Unavailable(
                "Este monitor no permite ajustar el brillo desde el equipo.");
        }
    }

    public bool TrySetBrightness(int percent)
    {
        lock (_sync)
        {
            if (!TryEnsureMonitorsLocked(out _))
            {
                return false;
            }

            var target = QuickControlsPolicy.NormalizePercent(percent);
            if (ApplyLocked(target))
            {
                return true;
            }

            return RefreshMonitorsLocked() && ApplyLocked(target);
        }
    }

    private bool ApplyLocked(int target)
    {
        var applied = false;

        foreach (var monitor in TargetsLocked())
        {
            // El rango se relee por monitor y no se asume 0-100: hay monitores cuyo mínimo no es
            // cero, y mandarles el porcentaje tal cual los dejaría más oscuros de lo pedido.
            if (!GetMonitorBrightness(monitor.Handle, out var minimum, out var current, out var maximum) ||
                maximum <= minimum)
            {
                continue;
            }

            var value = (uint)Math.Round(minimum + ((maximum - minimum) * (target / 100.0)));
            if (value != current && SetMonitorBrightness(monitor.Handle, value))
            {
                applied = true;
            }
            else if (value == current)
            {
                applied = true;
            }
        }

        return applied;
    }

    private bool TryReadPercentLocked(out double percent)
    {
        percent = 0;

        foreach (var monitor in TargetsLocked())
        {
            if (GetMonitorBrightness(monitor.Handle, out var minimum, out var current, out var maximum) &&
                maximum > minimum)
            {
                percent = (current - minimum) * 100.0 / (maximum - minimum);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Diseño D57 — suelta los asideros guardados y los vuelve a pedir.
    ///
    /// Los asideros que devuelve dxva2 valen para la configuracion de pantallas que habia cuando se
    /// pidieron. Al desenchufar un monitor, cambiar su entrada, girar la pantalla o volver de
    /// suspension, Windows rehace esa configuracion y los guardados dejan de servir: todas las
    /// llamadas fallan a partir de ahi. Como solo se pedian la primera vez, el mando de brillo
    /// desaparecia del panel y no volvia hasta reiniciar Sakura — un fallo repetible, y con una
    /// causa que desde fuera no se adivina, porque el monitor sigue obedeciendo a sus botones.
    ///
    /// Se rehace una sola vez por lectura, no en bucle: si de verdad el monitor no trae DDC/CI, la
    /// respuesta correcta sigue siendo decir que no hay brillo que mover.
    /// </summary>
    private bool RefreshMonitorsLocked()
    {
        ReleaseMonitorsLocked();
        return TryEnsureMonitorsLocked(out _);
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            ReleaseMonitorsLocked();
        }
    }

    private bool TryEnsureMonitorsLocked(out string message)
    {
        message = string.Empty;

        if (_disposed)
        {
            message = "El servicio de brillo ya se cerró.";
            return false;
        }

        if (_screens is { Count: > 0 })
        {
            return true;
        }

        try
        {
            var handles = new List<(IntPtr Handle, bool Primary)>();
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (monitor, _, _, _) =>
            {
                var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
                var primary = GetMonitorInfo(monitor, ref info) && (info.Flags & MonitorInfoPrimary) != 0;
                handles.Add((monitor, primary));
                return true;
            }, IntPtr.Zero);

            var screens = new List<PhysicalMonitor[]>();
            foreach (var (handle, _) in handles.OrderByDescending(entry => entry.Primary))
            {
                if (!GetNumberOfPhysicalMonitorsFromHMONITOR(handle, out var count) || count == 0)
                {
                    continue;
                }

                var monitors = new PhysicalMonitor[count];
                if (GetPhysicalMonitorsFromHMONITOR(handle, count, monitors))
                {
                    screens.Add(monitors);
                }
            }

            if (screens.Count == 0)
            {
                message = "No encontré un monitor al que pedirle el brillo.";
                return false;
            }

            _screens = screens;
            return true;
        }
        catch (Exception exception) when (
            exception is DllNotFoundException or EntryPointNotFoundException)
        {
            // dxva2 no está en todas las ediciones de Windows.
            message = "Este Windows no expone el control de brillo del monitor.";
            return false;
        }
    }

    private void ReleaseMonitorsLocked()
    {
        if (_screens is not { Count: > 0 })
        {
            return;
        }

        try
        {
            foreach (var monitors in _screens)
            {
                DestroyPhysicalMonitors((uint)monitors.Length, monitors);
            }
        }
        catch (Exception exception) when (
            exception is DllNotFoundException or EntryPointNotFoundException)
        {
            // Nada que liberar si la biblioteca no está.
        }

        _screens = null;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PhysicalMonitor
    {
        public IntPtr Handle;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Description;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
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
        public Rect Monitor;
        public Rect Work;
        public uint Flags;
    }

    private delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, IntPtr rect, IntPtr data);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr monitor, out uint count);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool GetPhysicalMonitorsFromHMONITOR(
        IntPtr monitor, uint count, [Out] PhysicalMonitor[] monitors);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool GetMonitorBrightness(
        IntPtr monitor, out uint minimum, out uint current, out uint maximum);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool SetMonitorBrightness(IntPtr monitor, uint value);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool DestroyPhysicalMonitors(uint count, [In] PhysicalMonitor[] monitors);
}
