using System.Runtime.InteropServices;
using Nexo.Core.Shell;

namespace Nexo.Windows.Shell;

/// <summary>
/// Diseño D44 — vigila el centro del borde de arriba para bajar el cajón del panel.
///
/// Es hermano de <see cref="WindowsEdgeRevealWatcher"/> y no una opción suya: los dos gestos
/// conviven —el lateral saca la ventana principal, este baja el cajón— y tienen permanencias y
/// silencios propios. Meterlos en la misma clase obligaría a llevar dos de cada estado dentro del
/// mismo candado por ahorrarse un temporizador que cuesta una llamada barata cada cuarenta
/// milisegundos.
///
/// Como el otro, sondea la posición del cursor en vez de instalar un enganche global de ratón, y
/// emite desde un hilo de fondo: quien lo escuche tiene que volver al hilo de la interfaz antes de
/// tocar ninguna ventana.
/// </summary>
public sealed class WindowsTopRevealWatcher : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(40);

    private readonly System.Timers.Timer _timer;
    private readonly object _sync = new();

    private readonly RevealDwellGate _gate;
    private DateTimeOffset _suppressedUntil = DateTimeOffset.MinValue;
    private bool _enabled;
    private bool _disposed;

    public WindowsTopRevealWatcher()
        : this(MouseButtons.AnyDown)
    {
    }

    /// <summary>El acceso a los botones va por un delegado para poder sustituirlo en pruebas.</summary>
    public WindowsTopRevealWatcher(Func<bool> mouseButtonDown)
    {
        _gate = new RevealDwellGate(mouseButtonDown);
        _timer = new System.Timers.Timer(PollInterval.TotalMilliseconds) { AutoReset = true };
        _timer.Elapsed += (_, _) => Poll();
    }

    /// <summary>
    /// Se dispara desde un hilo de fondo con el área de trabajo del monitor donde está el cursor.
    /// El cajón la necesita para colocarse: con dos monitores, «arriba y en medio» no es el mismo
    /// sitio según dónde tengas el ratón.
    /// </summary>
    public event Action<TopRevealArea>? RevealRequested;

    public void Configure(bool enabled)
    {
        lock (_sync)
        {
            _enabled = enabled;
            _gate.Reset();

            if (_disposed)
            {
                return;
            }

            _timer.Enabled = enabled;
        }
    }

    /// <summary>
    /// Silencia la zona durante <see cref="TopRevealPolicy.CooldownAfterHide"/>. Se llama al cerrar
    /// el cajón: el ratón sigue arriba, y sin esto volvería a bajar antes de poder apartarlo.
    /// </summary>
    public void SuppressBriefly()
    {
        lock (_sync)
        {
            _suppressedUntil = DateTimeOffset.UtcNow + TopRevealPolicy.CooldownAfterHide;
            _gate.Reset();
        }
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
            _timer.Enabled = false;
        }

        _timer.Dispose();
    }

    private void Poll()
    {
        TopRevealArea? area;

        lock (_sync)
        {
            if (_disposed || !_enabled)
            {
                return;
            }

            area = EvaluateLocked();
        }

        if (area is { } resolved)
        {
            RevealRequested?.Invoke(resolved);
        }
    }

    private TopRevealArea? EvaluateLocked()
    {
        var now = DateTimeOffset.UtcNow;
        if (now < _suppressedUntil)
        {
            return null;
        }

        var read = TryReadProbe(out var probe, out var area);
        var inZone = read && TopRevealPolicy.IsInHotZone(probe);
        var nearZone = read && TopRevealPolicy.IsNearHotZone(probe);

        // 2026-09-18 — con un botón pulsado se está arrastrando o pulsando algo, no llamando al
        // panel; el propio gate consulta el botón y desarma el borde hasta que el cursor se aparte
        // (soltar una ventana maximizada en y=0 y quedarse quieto no lo abre).
        if (!_gate.Observe(now, inZone, nearZone, TopRevealPolicy.Dwell))
        {
            return null;
        }

        _suppressedUntil = now + TopRevealPolicy.CooldownAfterHide;
        return area;
    }

    private static bool TryReadProbe(out TopRevealProbe probe, out TopRevealArea area)
    {
        probe = null!;
        area = default;

        if (!GetCursorPos(out var cursor))
        {
            return false;
        }

        var monitor = MonitorFromPoint(cursor, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return false;
        }

        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info))
        {
            return false;
        }

        probe = new TopRevealProbe(cursor.X, cursor.Y, info.Work.Left, info.Work.Right, info.Work.Top,
            EdgeRevealPolicy.UsesNarrowStrip(
                DesktopEdges.IsOuterTop(info.Monitor.Top), info.Monitor.Top, info.Work.Top));
        area = new TopRevealArea(info.Work.Left, info.Work.Top, info.Work.Right - info.Work.Left);
        return true;
    }

    private const uint MonitorDefaultToNearest = 2;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

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
        public NativeRect Work;
        public uint Flags;
    }
}

/// <summary>El monitor en el que hay que bajar el cajón, en píxeles de escritorio.</summary>
public readonly record struct TopRevealArea(double Left, double Top, double Width);
