using System.Runtime.InteropServices;
using Microsoft.Win32;
using Nexo.Core.AdaptiveEngine;
using Nexo.Core.Shell;

namespace Nexo.Windows.Shell;

/// <summary>
/// Diseño D62 — le pide a DWM el fondo, las esquinas y la sombra de una ventana.
///
/// Es la parte que toca Win32 de <see cref="WindowBackdropPolicy"/>: la decisión de qué pedir vive
/// en Nexo.Core y se puede probar; aquí solo quedan las tres llamadas a
/// <c>DwmSetWindowAttribute</c> y la lectura de los dos ajustes de Windows que mandan sobre ellas.
///
/// Ninguna de las llamadas es obligatoria. Un atributo que esta versión de Windows no conozca
/// devuelve un HRESULT de error y la ventana se queda como estaba, que es un resultado legítimo:
/// opaca y con las esquinas del sistema.
/// </summary>
public static class WindowsDwmChrome
{
    private const int DwmwaNcRenderingPolicy = 2;
    private const int DwmncrpEnabled = 2;

    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaSystemBackdropType = 38;

    /// <summary>Valor documentado de <c>DWMWA_COLOR_NONE</c>: quita el borde en vez de pintarlo.</summary>
    private const uint DwmColorNone = 0xFFFFFFFE;

    private const int DwmwcpDefault = 0;
    private const int DwmwcpDoNotRound = 1;
    private const int DwmwcpRound = 2;
    private const int DwmwcpRoundSmall = 3;

    private const int DwmsbtNone = 1;
    private const int DwmsbtMainWindow = 2;      // Mica
    private const int DwmsbtTransientWindow = 3; // Acrílico

    private const uint SpiGetHighContrast = 0x0042;
    private const uint HcfHighContrastOn = 0x00000001;

    /// <summary>Lee del sistema lo que la política necesita saber.</summary>
    public static WindowBackdropProbe ReadProbe(HardwarePerformanceMode performanceMode) =>
        new(
            Environment.OSVersion.Version.Build,
            IsTransparencyEnabled(),
            IsHighContrastOn(),
            performanceMode);

    /// <summary>
    /// Aplica la decisión a la ventana. Devuelve cierto solo si el fondo pedido se aceptó: quien
    /// llama necesita saberlo para pintar su propio fondo si no hubo desenfoque.
    /// </summary>
    public static bool TryApply(IntPtr windowHandle, WindowBackdropDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);

        if (windowHandle == IntPtr.Zero)
        {
            return false;
        }

        // Una ventana sin barra de título tampoco tiene área no-cliente que DWM componga, y es ahí
        // donde vive la sombra del marco. Pedirla explícitamente es lo único que puede devolverla
        // sin volver a dibujarla nosotros.
        TrySetAttribute(windowHandle, DwmwaNcRenderingPolicy, DwmncrpEnabled);

        // El modo oscuro va primero: es lo que decide de qué color dibuja DWM el borde y la sombra,
        // y sin él una ventana oscura queda enmarcada en un filo blanco.
        TrySetAttribute(windowHandle, DwmwaUseImmersiveDarkMode, 1);

        if (decision.Corner != WindowCorner.System)
        {
            TrySetAttribute(windowHandle, DwmwaWindowCornerPreference, CornerValue(decision.Corner));
        }

        // El borde de DWM es lo que se veía como filo oscuro recorriendo el arco de cada esquina:
        // un trazo pensado para ventanas con marco, dibujado sobre una que no lo tiene. Quitarlo es
        // un valor documentado, no un truco, y deja que el canto lo defina la propia superficie.
        TrySetAttribute(windowHandle, DwmwaBorderColor, unchecked((int)DwmColorNone));

        var backdrop = decision.Backdrop switch
        {
            WindowBackdrop.Acrylic => DwmsbtTransientWindow,
            WindowBackdrop.Mica => DwmsbtMainWindow,
            _ => DwmsbtNone
        };

        var applied = TrySetAttribute(windowHandle, DwmwaSystemBackdropType, backdrop);

        if (!applied || decision.Backdrop == WindowBackdrop.None)
        {
            return false;
        }

        // Sin esto el fondo del sistema se compone solo donde hay marco, y una ventana sin barra de
        // título no tiene marco: el área de cliente se queda pintada de negro y el acrílico existe
        // pero no se ve por ninguna parte. Un margen de -1 es la «hoja de cristal» documentada:
        // extiende el marco a la ventana entera. Medido antes de ponerlo, el hueco entre tarjetas
        // daba R8 G5 B6 con la superficie al 45% —es decir, negro debajo— en vez del fondo de
        // escritorio desenfocado.
        return TryExtendFrame(windowHandle);
    }

    /// <summary>
    /// Ventana de cristal transparente: sin fondo del sistema, sin esquinas ni borde de DWM, y con el
    /// marco extendido a toda la ventana para que los píxeles transparentes de WPF dejen ver lo que hay
    /// detrás. Así la propia superficie puede tener el radio que quiera y seguir pintándose en la GPU,
    /// sin <c>AllowsTransparency</c>. Lo que se pierde es el desenfoque del acrílico.
    /// Devuelve falso si no se pudo extender el marco; entonces quien llama debe volver al camino normal.
    /// </summary>
    public static bool TryApplyClearGlass(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return false;
        }

        TrySetAttribute(windowHandle, DwmwaUseImmersiveDarkMode, 1);
        TrySetAttribute(windowHandle, DwmwaWindowCornerPreference, DwmwcpDoNotRound);
        TrySetAttribute(windowHandle, DwmwaBorderColor, unchecked((int)DwmColorNone));
        TrySetAttribute(windowHandle, DwmwaSystemBackdropType, DwmsbtNone);

        return TryExtendFrame(windowHandle);
    }

    private static bool TryExtendFrame(IntPtr windowHandle)
    {
        var margins = new Margins
        {
            LeftWidth = -1,
            RightWidth = -1,
            TopHeight = -1,
            BottomHeight = -1
        };

        try
        {
            return DwmExtendFrameIntoClientArea(windowHandle, ref margins) == 0;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    private static int CornerValue(WindowCorner corner) => corner switch
    {
        WindowCorner.Round => DwmwcpRound,
        WindowCorner.RoundSmall => DwmwcpRoundSmall,
        WindowCorner.Square => DwmwcpDoNotRound,
        _ => DwmwcpDefault
    };

    private static bool TrySetAttribute(IntPtr windowHandle, int attribute, int value)
    {
        try
        {
            return DwmSetWindowAttribute(windowHandle, attribute, ref value, sizeof(int)) == 0;
        }
        catch (DllNotFoundException)
        {
            // dwmapi.dll existe desde Windows Vista; si faltara, no hay nada que componer.
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    private static bool IsTransparencyEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

            // Sin la clave, Windows se comporta como si estuviera activado.
            return key?.GetValue("EnableTransparency") is not int enabled || enabled != 0;
        }
        catch (Exception exception) when (
            exception is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return true;
        }
    }

    private static bool IsHighContrastOn()
    {
        var info = new HighContrast { CbSize = (uint)Marshal.SizeOf<HighContrast>() };

        try
        {
            if (!SystemParametersInfo(SpiGetHighContrast, info.CbSize, ref info, 0))
            {
                return false;
            }
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }

        return (info.DwFlags & HcfHighContrastOn) != 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HighContrast
    {
        public uint CbSize;
        public uint DwFlags;
        public IntPtr LpszDefaultScheme;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins
    {
        public int LeftWidth;
        public int RightWidth;
        public int TopHeight;
        public int BottomHeight;
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint action, uint param, ref HighContrast data, uint winIni);
}
