using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Interop;

namespace Nexo.App.Views.Controls;

/// <summary>
/// Los avisos se muestran sin interrumpir la escritura. Un atajo registrado solo mientras están
/// visibles permite entrar en ellos sin ratón y sin robar el foco cuando llegan automáticamente.
/// </summary>
internal static class PopupKeyboardAccess
{
    public static void Attach(Window window, Key key, Action dismiss)
    {
        HwndSource? source = null;
        bool registered = false;
        bool restoreNoActivate = false;
        const int id = 0x5341;
        void Register()
        {
            if (source is null) return;
            if (registered) UnregisterHotKey(source.Handle, id);
            if (!window.IsVisible && restoreNoActivate)
            {
                SetWindowLong(source.Handle, -20, GetWindowLong(source.Handle, -20) | 0x08000000);
                restoreNoActivate = false;
            }
            registered = window.IsVisible && RegisterHotKey(source.Handle, id, 0x4003,
                (uint)KeyInterop.VirtualKeyFromKey(key));
            AutomationProperties.SetHelpText(window, registered
                ? $"Ctrl+Alt+{key} para entrar; Tab para recorrer; Escape para salir."
                : "El atajo de teclado no está disponible en este momento.");
        }
        window.SourceInitialized += (_, _) =>
        {
            source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
            source?.AddHook(Hook);
            Register();
        };
        window.IsVisibleChanged += (_, _) => Register();
        window.Closed += (_, _) =>
        {
            if (source is null) return;
            if (registered) UnregisterHotKey(source.Handle, id);
            source.RemoveHook(Hook);
        };
        window.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            e.Handled = true;
            dismiss();
        };
        IntPtr Hook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message != 0x0312 || wParam.ToInt32() != id) return IntPtr.Zero;
            // NOACTIVATE impedía incluso el foco solicitado explícitamente por la persona.
            var style = GetWindowLong(hwnd, -20);
            restoreNoActivate |= (style & 0x08000000) != 0;
            SetWindowLong(hwnd, -20, style & ~0x08000000);
            window.Activate();
            window.Focus();
            window.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            handled = true;
            return IntPtr.Zero;
        }
    }

    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hwnd, int index, int value);
}
