using System.Runtime.InteropServices;
using System.Threading;

namespace Nexo.Windows.Ambient;

/// <summary>
/// Diseño D4 (corrección post smoke test manual, 2026-07-28) — el usuario reportó que, tras cerrar
/// el Sakura Pill Host y cambiar de ventana, volver a ejecutar "¿Qué ventana tengo activa?" seguía
/// mostrando la ventana anterior. Causa raíz: el comando reutilizaba
/// <c>MainWindow._lastExternalWindowHandle</c>, que solo se actualiza en puntos concretos ya
/// existentes para Vision/Peek (<c>RememberForegroundWindow()</c>, llamado al invocar atajos
/// globales) — nunca al alternar ventanas con Alt+Tab normal, ni al abrir el Command Center (que
/// solo puede abrirse con Sakura ya enfocada, momento en el que "la ventana activa" ya es la propia
/// Sakura). Este rastreador es una fuente independiente y correcta para el Context Snapshot
/// ambiental: escucha <c>EVENT_SYSTEM_FOREGROUND</c> a nivel de sistema con
/// <c>SetWinEventHook</c> y recuerda el último handle ajeno al propio proceso, actualizado en
/// tiempo real ante cualquier cambio de foco, sin tocar el mecanismo existente de Vision/Peek.
/// </summary>
public sealed class ForegroundWindowTracker : IDisposable
{
    private const uint EventSystemForeground = 0x0003;
    private const uint WinEventOutOfContext = 0x0000;

    private readonly WinEventProc _callback;
    private readonly IntPtr _hookHandle;
    private readonly uint _ownProcessId;
    private long _lastExternalWindowHandle;
    private bool _disposed;

    public ForegroundWindowTracker()
    {
        _ownProcessId = (uint)Environment.ProcessId;
        _callback = OnForegroundChanged;
        _hookHandle = SetWinEventHook(
            EventSystemForeground,
            EventSystemForeground,
            IntPtr.Zero,
            _callback,
            0,
            0,
            WinEventOutOfContext);

        // Semilla inicial: la ventana en primer plano en el instante de construir el rastreador
        // (normalmente la aplicación desde la que se lanzó Sakura) ya cuenta, sin esperar al
        // primer cambio de foco futuro.
        RememberIfExternal(GetForegroundWindow());
    }

    /// <summary>
    /// Último handle de ventana en primer plano que no pertenece al propio proceso de Sakura, o 0
    /// si todavía no se observó ninguno (p. ej. Sakura es la primera ventana de la sesión).
    /// </summary>
    public long LastExternalWindowHandle => Interlocked.Read(ref _lastExternalWindowHandle);

    /// <summary>
    /// Se dispara en cada cambio de ventana activa (también a la propia). Quien lo escuche debe
    /// volver a su hilo de interfaz: no se garantiza en cuál llega.
    /// </summary>
    public event Action? ForegroundChanged;

    private void OnForegroundChanged(
        IntPtr hookHandle,
        uint eventType,
        IntPtr windowHandle,
        int objectId,
        int childId,
        uint eventThreadId,
        uint eventTimeMs)
    {
        RememberIfExternal(windowHandle);
        ForegroundChanged?.Invoke();
    }

    private void RememberIfExternal(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return;
        }

        GetWindowThreadProcessId(windowHandle, out var processId);
        if (processId != _ownProcessId)
        {
            Interlocked.Exchange(ref _lastExternalWindowHandle, windowHandle.ToInt64());
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_hookHandle != IntPtr.Zero)
        {
            UnhookWinEvent(_hookHandle);
        }
    }

    private delegate void WinEventProc(
        IntPtr hookHandle,
        uint eventType,
        IntPtr windowHandle,
        int objectId,
        int childId,
        uint eventThreadId,
        uint eventTimeMs);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(
        uint eventMin,
        uint eventMax,
        IntPtr hookModule,
        WinEventProc callback,
        uint processId,
        uint threadId,
        uint flags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hookHandle);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
}
