using System.Runtime.InteropServices;
using Nexo.Core.Settings;
using Nexo.Core.Shell;

namespace Nexo.Windows.Shell;

/// <summary>
/// Diseño D27 — vigila si el ratón se queda en el borde donde vive Sakura.
///
/// Consulta la posición del cursor con un temporizador en vez de instalar un enganche global de
/// ratón (<c>WH_MOUSE_LL</c>). Un enganche de bajo nivel recibe **todos** los mensajes de ratón del
/// sistema, y su procedimiento corre dentro del bucle de mensajes de esta aplicación: si tarda, se
/// arrastra el puntero de todo Windows, no solo el de Sakura. Para un gesto que además exige
/// quedarse quieto casi trescientos milisegundos, esa precisión no compra nada.
///
/// El temporizador es de <see cref="System.Timers.Timer"/> y no de la interfaz: el evento se emite
/// desde un hilo de fondo, así que quien lo escuche tiene que volver al hilo de la interfaz antes de
/// tocar ventanas. Se documenta aquí porque no hacerlo funciona casi siempre y falla al azar.
/// </summary>
public sealed class WindowsEdgeRevealWatcher : IDisposable
{
    /// <summary>
    /// Diseño D38 — bajado de 90 a 40 ms. La permanencia se mide con marcas de tiempo, pero solo se
    /// comprueba cuando toca sondear: con 90 ms, entre entrar en la franja y avisar podían pasar
    /// casi trescientos milisegundos aunque la permanencia fueran ciento sesenta. Sondear la
    /// posición del cursor es una llamada barata; el retraso que añadía no lo era.
    /// </summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(40);

    private readonly System.Timers.Timer _timer;
    private readonly object _sync = new();

    private DateTimeOffset? _insideSince;
    private DateTimeOffset _suppressedUntil = DateTimeOffset.MinValue;
    private SidebarPosition _side = SidebarPosition.Right;
    private bool _enabled;
    private bool _disposed;

    /// <summary>
    /// 2026-09-16 — si el borde puede abrir algo. Se apaga al disparar y solo se vuelve a encender
    /// cuando el ratón se ha apartado (<see cref="EdgeRevealPolicy.HasLeftEdge"/>): una aparición por
    /// visita, no una cada vez que el panel se cierra con el ratón todavía ahí.
    /// </summary>
    private bool _armed = true;

    public WindowsEdgeRevealWatcher()
    {
        _timer = new System.Timers.Timer(PollInterval.TotalMilliseconds)
        {
            AutoReset = true
        };
        _timer.Elapsed += (_, _) => Poll();
    }

    /// <summary>
    /// Se dispara desde un hilo de fondo cuando el ratón lleva el tiempo suficiente en el borde.
    /// Quien lo escuche debe volver al hilo de la interfaz antes de mostrar nada.
    /// </summary>
    public event Action? RevealRequested;

    public void Configure(bool enabled, SidebarPosition side)
    {
        lock (_sync)
        {
            _side = side;
            _enabled = enabled;
            _insideSince = null;

            if (_disposed)
            {
                return;
            }

            _timer.Enabled = enabled;
        }
    }

    /// <summary>
    /// Silencia el borde durante <see cref="EdgeRevealPolicy.CooldownAfterHide"/>. Se llama justo
    /// después de ocultar el shell: al cerrarlo el ratón sigue donde estaba —encima del borde—, y
    /// sin esto Sakura se volvería a abrir sola antes de que nadie pudiera apartarlo.
    /// </summary>
    public void SuppressBriefly()
    {
        lock (_sync)
        {
            _suppressedUntil = DateTimeOffset.UtcNow + EdgeRevealPolicy.CooldownAfterHide;
            _insideSince = null;
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
        bool shouldReveal;

        lock (_sync)
        {
            if (_disposed || !_enabled)
            {
                return;
            }

            shouldReveal = EvaluateLocked();
        }

        if (shouldReveal)
        {
            RevealRequested?.Invoke();
        }
    }

    private bool EvaluateLocked()
    {
        var now = DateTimeOffset.UtcNow;
        if (now < _suppressedUntil)
        {
            return false;
        }

        if (!TryReadProbe(_side, out var probe))
        {
            _insideSince = null;
            return false;
        }

        if (EdgeRevealPolicy.HasLeftEdge(probe))
        {
            _armed = true;
        }

        if (!_armed || !EdgeRevealPolicy.IsInHotZone(probe))
        {
            _insideSince = null;
            return false;
        }

        _insideSince ??= now;

        if (now - _insideSince.Value < EdgeRevealPolicy.Dwell)
        {
            return false;
        }

        // Se reinicia el contador al emitir: sin esto, mantener el ratón en el borde dispararía la
        // apertura en cada vuelta del temporizador, diez veces por segundo.
        _insideSince = null;
        _suppressedUntil = now + EdgeRevealPolicy.CooldownAfterHide;
        _armed = false;
        return true;
    }

    /// <summary>
    /// El área de trabajo se toma del monitor donde está el cursor, no de la pantalla principal:
    /// con dos monitores, el borde derecho del principal está en mitad del escritorio y no es el
    /// borde de nada. También excluye la barra de tareas, que es lo que hace <c>rcWork</c>.
    /// </summary>
    private static bool TryReadProbe(SidebarPosition side, out EdgeRevealProbe probe)
    {
        probe = null!;

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

        probe = new EdgeRevealProbe(
            cursor.X,
            cursor.Y,
            info.Work.Left,
            info.Work.Right,
            info.Work.Top,
            info.Work.Bottom,
            side);
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
