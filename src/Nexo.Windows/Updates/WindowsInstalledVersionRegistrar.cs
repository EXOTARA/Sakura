using Microsoft.Win32;
using Nexo.Core.Updates;

namespace Nexo.Windows.Updates;

/// <summary>
/// Diseño D87 — deja la entrada de «Aplicaciones instaladas» diciendo la verdad.
///
/// La decisión de qué escribir vive en <see cref="InstalledVersionPolicy"/>, en Core y con pruebas.
/// Aquí solo está lo que no se puede probar sin un registro de Windows delante: dónde mira y cómo
/// escribe.
///
/// **Es de la rama del usuario, no de la máquina.** El instalador declara
/// <c>PrivilegesRequired=lowest</c> e instala en la carpeta del usuario, así que su entrada de
/// desinstalación vive en HKCU y se puede escribir sin elevación. Si algún día apareciera una
/// instalación para todo el equipo, esa estaría en HKLM y necesitaría permisos: no se intenta, y
/// quedarse sin corregir es preferible a pedir una elevación por un dato cosmético.
///
/// **Nunca falla hacia fuera.** Esto corre al arrancar. Un error escribiendo un dato de una lista
/// de Windows no puede impedir que la aplicación abra.
/// </summary>
public sealed class WindowsInstalledVersionRegistrar
{
    /// <summary>
    /// El identificador que Inno Setup pone en <c>AppId</c>, con el sufijo que él mismo añade.
    /// Tiene que coincidir con <c>installer/Sakura.iss</c>; si allí cambia, esto deja de encontrar
    /// nada y la corrección se apaga en silencio, que es el fallo seguro de los dos posibles.
    /// </summary>
    private const string UninstallKey =
        @"Software\Microsoft\Windows\CurrentVersion\Uninstall\" +
        "{5F9D061E-33C8-4F85-BE6E-8C3BAF240B85}_is1";

    private const string DisplayNameValue = "DisplayName";
    private const string DisplayVersionValue = "DisplayVersion";

    /// <summary>
    /// Compara lo escrito con lo que corre y corrige si hace falta. Devuelve lo que dejó escrito, o
    /// <c>null</c> si no había nada que hacer o no se pudo.
    /// </summary>
    public InstalledVersionCorrection? Reconcile(string runningVersion)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(UninstallKey, writable: true);

            // Sin clave, instalación portable: la política ya sabe que entonces no se inventa nada,
            // pero ni siquiera hace falta preguntárselo.
            if (key is null)
            {
                return null;
            }

            var recorded = key.GetValue(DisplayVersionValue) as string;
            var correction = InstalledVersionPolicy.Reconcile(recorded, runningVersion);

            if (correction is not { } fix)
            {
                return null;
            }

            key.SetValue(DisplayVersionValue, fix.DisplayVersion, RegistryValueKind.String);
            key.SetValue(DisplayNameValue, fix.DisplayName, RegistryValueKind.String);

            return fix;
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException or System.Security.SecurityException or
                IOException)
        {
            // Una directiva de grupo, un permiso raro o una clave dañada. La lista de aplicaciones
            // se queda como estaba, que es exactamente igual de mal que antes de esta corrección y
            // muchísimo mejor que no abrir.
            return null;
        }
    }
}
