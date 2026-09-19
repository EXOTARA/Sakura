using System.Runtime.InteropServices;

namespace Nexo.Windows.Shell;

/// <summary>
/// Dice si el borde de un monitor es el límite del escritorio virtual. Un borde compartido con
/// otro monitor no frena el cursor, lo cruza (D38), y por eso ahí se conserva la franja ancha.
/// Sin datos fiables se responde «compartido»: es la franja que nunca rompe nada.
/// </summary>
internal static class DesktopEdges
{
    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;

    public static bool IsOuterLeft(int monitorLeft) =>
        VirtualWidth() > 0 && monitorLeft <= GetSystemMetrics(SmXVirtualScreen);

    public static bool IsOuterRight(int monitorRight) =>
        VirtualWidth() > 0 &&
        monitorRight >= GetSystemMetrics(SmXVirtualScreen) + VirtualWidth();

    public static bool IsOuterTop(int monitorTop) =>
        VirtualWidth() > 0 && monitorTop <= GetSystemMetrics(SmYVirtualScreen);

    private static int VirtualWidth() => GetSystemMetrics(SmCxVirtualScreen);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);
}
