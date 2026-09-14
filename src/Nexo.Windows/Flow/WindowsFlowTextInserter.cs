using System.Runtime.InteropServices;
using Nexo.Core.Ambient;
using Nexo.Core.Flow;

namespace Nexo.Windows.Flow;

/// <summary>
/// Diseño D6.2 (Fase 3 — Sakura Flow) — escribe texto en la aplicación activa mediante
/// <c>SendInput</c> con <c>KEYEVENTF_UNICODE</c>, la vía documentada de Win32 para inyectar texto:
/// entrega directamente el carácter Unicode, así que acentos y "ñ" funcionan sin depender de la
/// distribución de teclado del usuario. Es el ÚNICO punto de todo Sakura que envía entrada a otro
/// programa (antes de D6 no existía ninguno).
///
/// Tres negativas deliberadas antes de escribir una sola tecla:
/// 1. Si no se recordó una ventana destino, no se escribe a ciegas.
/// 2. Si la ventana en primer plano cambió desde que empezó el dictado, NO se escribe — es
///    exactamente el riesgo que `SAKURA_TECHNOLOGY_ROADMAP.md` señala para esta fase ("inserción de
///    texto en el lugar equivocado si el foco cambia durante el dictado"). Dictar un mensaje
///    privado dentro de la ventana equivocada es un daño real e irreversible.
/// 3. Si la ventana está marcada como sensible (gestor de contraseñas, diálogo de credenciales),
///    tampoco — reutiliza la misma <see cref="Nexo.Core.Vision.VisionPrivacyPolicy"/> que ya
///    protege a Lens, a través de <see cref="IAmbientContextProvider"/>.
/// </summary>
public sealed class WindowsFlowTextInserter : IFlowTextInserter
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint KeyEventUnicode = 0x0004;
    private const ushort VirtualKeyReturn = 0x0D;

    private readonly IAmbientContextProvider _contextProvider;

    public WindowsFlowTextInserter(IAmbientContextProvider contextProvider)
    {
        _contextProvider = contextProvider;
    }

    public FlowInsertionResult Insert(string text, long expectedWindowHandle)
    {
        if (string.IsNullOrEmpty(text))
        {
            return FlowInsertionResult.Failed(
                FlowInsertionFailure.EmptyText, "No entendí nada que escribir.");
        }

        if (expectedWindowHandle == 0)
        {
            return FlowInsertionResult.Failed(
                FlowInsertionFailure.NoTargetWindow,
                "No recordé en qué ventana estabas cuando empezaste a dictar.");
        }

        var foreground = GetForegroundWindow().ToInt64();
        if (foreground != expectedWindowHandle)
        {
            return FlowInsertionResult.Failed(
                FlowInsertionFailure.FocusChanged,
                "Cambiaste de ventana mientras dictabas, así que no escribí nada.");
        }

        var context = _contextProvider.Capture(expectedWindowHandle);
        if (context is null)
        {
            return FlowInsertionResult.Failed(
                FlowInsertionFailure.NoTargetWindow, "La ventana destino ya no está disponible.");
        }

        if (context.IsSensitive)
        {
            return FlowInsertionResult.Failed(
                FlowInsertionFailure.SensitiveWindow,
                "Esa ventana está marcada como sensible; Sakura no escribe en ella.");
        }

        var inputs = BuildInputs(text);
        if (inputs.Length == 0)
        {
            return FlowInsertionResult.Failed(
                FlowInsertionFailure.EmptyText, "No entendí nada que escribir.");
        }

        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        return sent == inputs.Length
            ? FlowInsertionResult.Inserted(text.Length)
            : FlowInsertionResult.Failed(
                FlowInsertionFailure.SendFailed,
                "Windows no aceptó la escritura automática en esa ventana.");
    }

    /// <summary>
    /// Se arma el lote completo y se envía en UNA sola llamada a <c>SendInput</c>: así el sistema
    /// trata la secuencia como atómica y no se puede colar la pulsación real del usuario en medio
    /// del texto dictado.
    /// </summary>
    private static Input[] BuildInputs(string text)
    {
        var inputs = new List<Input>(text.Length * 2);

        foreach (var character in text)
        {
            switch (character)
            {
                case '\r':
                    // Un "\r\n" produciría dos saltos: el "\n" ya inserta el suyo.
                    continue;
                case '\n':
                    inputs.Add(CreateKeyInput(VirtualKeyReturn, scan: 0, flags: 0));
                    inputs.Add(CreateKeyInput(VirtualKeyReturn, scan: 0, flags: KeyEventKeyUp));
                    continue;
                default:
                    inputs.Add(CreateKeyInput(virtualKey: 0, scan: character, flags: KeyEventUnicode));
                    inputs.Add(CreateKeyInput(
                        virtualKey: 0, scan: character, flags: KeyEventUnicode | KeyEventKeyUp));
                    continue;
            }
        }

        return [.. inputs];
    }

    private static Input CreateKeyInput(ushort virtualKey, ushort scan, uint flags) => new()
    {
        Type = InputKeyboard,
        Data = new InputUnion
        {
            Keyboard = new KeyboardInput
            {
                VirtualKey = virtualKey,
                Scan = scan,
                Flags = flags,
                Time = 0,
                ExtraInfo = IntPtr.Zero
            }
        }
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    /// <summary>
    /// La unión debe declarar también ratón y hardware aunque Flow solo use teclado: su tamaño lo
    /// fija el miembro más grande, y <c>SendInput</c> rechaza la llamada si el tamaño no coincide
    /// exactamente con el que espera Windows.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MouseInput Mouse;
        [FieldOffset(0)] public KeyboardInput Keyboard;
        [FieldOffset(0)] public HardwareInput Hardware;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HardwareInput
    {
        public uint Message;
        public ushort ParamL;
        public ushort ParamH;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
}
