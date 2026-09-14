using Microsoft.Win32;
using Nexo.Core.Branding;
using Nexo.Core.Distribution;
using Nexo.Core.WindowsIntegration;
using Nexo.Windows.Distribution;
using Windows.ApplicationModel;

namespace Nexo.Windows.WindowsIntegration;

public sealed class WindowsStartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = ProductIdentity.ProductName;
    private const string LegacyValueName = ProductIdentity.PreviousProductName;

    /// <summary>
    /// Diseño D89 — el identificador de la tarea de inicio en el manifiesto del paquete
    /// (<c>packaging/msix/AppxManifest.template.xml</c>). Tienen que coincidir.
    /// </summary>
    public const string PackageStartupTaskId = "SakuraStartup";

    public bool IsEnabled()
    {
        if (DistributionPolicy.Startup(WindowsDistributionChannel.Current) == StartupRegistration.PackageStartupTask)
        {
            return IsPackageTaskEnabled();
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (HasRegistration(key, ValueName))
            {
                return true;
            }

            if (!HasRegistration(key, LegacyValueName))
            {
                return false;
            }

            // Una instalación anterior puede seguir apuntando a Nexo.exe.
            // Al detectar esa entrada se reemplaza por el ejecutable actual.
            var executablePath = Environment.ProcessPath;
            if (key is not null &&
                !string.IsNullOrWhiteSpace(executablePath) &&
                File.Exists(executablePath))
            {
                key.SetValue(
                    ValueName,
                    StartupCommandBuilder.Build(executablePath),
                    RegistryValueKind.String);
                key.DeleteValue(LegacyValueName, throwOnMissingValue: false);
            }

            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (System.Security.SecurityException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    public StartupRegistrationResult SetEnabled(bool enabled)
    {
        if (DistributionPolicy.Startup(WindowsDistributionChannel.Current) == StartupRegistration.PackageStartupTask)
        {
            return SetPackageTaskEnabled(enabled);
        }

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key is null)
            {
                return StartupRegistrationResult.Failed(
                    "Windows no permitió abrir la configuración de inicio.");
            }

            if (!enabled)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                key.DeleteValue(LegacyValueName, throwOnMissingValue: false);
                return StartupRegistrationResult.Completed(
                    $"{ProductIdentity.ProductName} ya no se iniciará con Windows.");
            }

            var executablePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            {
                return StartupRegistrationResult.Failed(
                    $"No pude localizar el ejecutable actual de {ProductIdentity.ProductName}.");
            }

            key.SetValue(
                ValueName,
                StartupCommandBuilder.Build(executablePath),
                RegistryValueKind.String);
            key.DeleteValue(LegacyValueName, throwOnMissingValue: false);

            return StartupRegistrationResult.Completed(
                $"{ProductIdentity.ProductName} se iniciará en segundo plano cuando abras sesión en Windows.");
        }
        catch (UnauthorizedAccessException)
        {
            return StartupRegistrationResult.Failed(
                "Windows no permitió cambiar la configuración de inicio.");
        }
        catch (System.Security.SecurityException)
        {
            return StartupRegistrationResult.Failed(
                "La política de seguridad bloqueó el inicio automático.");
        }
        catch (IOException exception)
        {
            return StartupRegistrationResult.Failed(
                $"No pude actualizar el inicio automático: {exception.Message}");
        }
    }

    // Dentro de un paquete, escribir en la clave Run no sirve: Windows guarda la escritura en una copia
    // privada del paquete y el arranque nunca la ve. La tarea de inicio es el mecanismo soportado.
    // Las llamadas WinRT completan en el grupo de hilos, así que esperarlas aquí no bloquea la
    // finalización contra el hilo de interfaz.
    private static bool IsPackageTaskEnabled()
    {
        try
        {
            var task = StartupTask.GetAsync(PackageStartupTaskId).AsTask().GetAwaiter().GetResult();
            return task.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static StartupRegistrationResult SetPackageTaskEnabled(bool enabled)
    {
        try
        {
            var task = StartupTask.GetAsync(PackageStartupTaskId).AsTask().GetAwaiter().GetResult();
            if (!enabled)
            {
                task.Disable();
                return StartupRegistrationResult.Completed(
                    $"{ProductIdentity.ProductName} ya no se iniciará con Windows.");
            }

            var state = task.RequestEnableAsync().AsTask().GetAwaiter().GetResult();
            return state switch
            {
                StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy =>
                    StartupRegistrationResult.Completed(
                        $"{ProductIdentity.ProductName} se iniciará en segundo plano cuando abras sesión en Windows."),
                // Si la persona lo apagó en Configuración > Aplicaciones > Inicio, Windows no deja que
                // la app lo vuelva a encender sola. Hay que decir dónde se hace.
                StartupTaskState.DisabledByUser => StartupRegistrationResult.Failed(
                    "Lo desactivaste en Configuración de Windows > Aplicaciones > Inicio. Actívalo desde ahí."),
                StartupTaskState.DisabledByPolicy => StartupRegistrationResult.Failed(
                    "Una directiva de tu equipo no permite que las aplicaciones se inicien con Windows."),
                _ => StartupRegistrationResult.Failed(
                    "Windows no permitió activar el inicio automático.")
            };
        }
        catch (Exception exception)
        {
            return StartupRegistrationResult.Failed(
                $"No pude cambiar el inicio automático: {exception.Message}");
        }
    }

    private static bool HasRegistration(RegistryKey? key, string valueName) =>
        key?.GetValue(valueName) is string value &&
        !string.IsNullOrWhiteSpace(value);
}

public sealed record StartupRegistrationResult(bool Success, string Message)
{
    public static StartupRegistrationResult Completed(string message) =>
        new(true, message);

    public static StartupRegistrationResult Failed(string message) =>
        new(false, message);
}
