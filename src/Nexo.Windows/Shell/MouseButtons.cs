using System.Runtime.InteropServices;

namespace Nexo.Windows.Shell;

/// <summary>
/// Lee si hay algún botón del ratón pulsado. Se usa <c>GetAsyncKeyState</c> porque los vigilantes
/// de borde sondean desde un hilo de fondo, sin ventana. El bit alto es «pulsado ahora»; el bajo
/// («pulsado desde la última lectura») se ignora a propósito. Con los botones intercambiados,
/// VK_LBUTTON sigue siendo el físico izquierdo; como se miran los tres, da igual cuál sea el
/// principal.
/// </summary>
internal static class MouseButtons
{
    private const int VkLButton = 0x01;
    private const int VkRButton = 0x02;
    private const int VkMButton = 0x04;

    public static bool AnyDown() =>
        IsDown(VkLButton) || IsDown(VkRButton) || IsDown(VkMButton);

    private static bool IsDown(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);
}
