using System.Runtime.InteropServices;
using Nexo.Core.Distribution;

namespace Nexo.Windows.Distribution;

/// <summary>
/// Diseño D89 — averigua si este proceso corre dentro de un paquete MSIX.
///
/// <c>GetCurrentPackageFullName</c> es la forma documentada: devuelve
/// <c>APPMODEL_ERROR_NO_PACKAGE</c> cuando el proceso no tiene identidad de paquete. Se pregunta una
/// vez; la identidad de un proceso no cambia mientras vive.
/// </summary>
public static class WindowsDistributionChannel
{
    private const int AppModelErrorNoPackage = 15700;
    private const int ErrorInsufficientBuffer = 122;

    private static readonly Lazy<DistributionChannel> Detected = new(Detect);

    public static DistributionChannel Current => Detected.Value;

    /// <summary>
    /// Si Windows abrió Sakura por la tarea de inicio del paquete. La clave <c>Run</c> le pasa
    /// <c>--background</c> para que arranque escondida; una tarea de inicio no admite argumentos, así
    /// que dentro del paquete se pregunta a Windows cómo se activó.
    /// </summary>
    public static bool WasLaunchedByStartupTask()
    {
        if (Current != DistributionChannel.MicrosoftStore)
        {
            return false;
        }

        try
        {
            return global::Windows.ApplicationModel.AppInstance.GetActivatedEventArgs()?.Kind ==
                global::Windows.ApplicationModel.Activation.ActivationKind.StartupTask;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static DistributionChannel Detect()
    {
        try
        {
            var length = 0;
            var result = GetCurrentPackageFullName(ref length, null);

            // Sin paquete no hay nombre que medir. Con paquete, la primera llamada pide el tamaño del
            // búfer; cualquier otro código se trata como «sin paquete», que es lo que era Sakura hasta
            // hoy y lo que menos sorpresas da si algo falla.
            return result == ErrorInsufficientBuffer
                ? DistributionChannel.MicrosoftStore
                : DistributionChannel.Direct;
        }
        catch (EntryPointNotFoundException)
        {
            return DistributionChannel.Direct;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, char[]? packageFullName);
}
