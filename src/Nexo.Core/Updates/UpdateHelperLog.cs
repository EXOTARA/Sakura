using System.Text.RegularExpressions;

namespace Nexo.Core.Updates;

/// <summary>
/// Bloque de instalación y actualización (0.30.36) — traduce el registro que deja el ayudante de
/// actualización (<c>ultima-actualizacion.log</c>) en un aviso para la persona.
///
/// Hasta ahora nadie leía ese registro: si el intercambio fallaba, Sakura se cerraba y no volvía, y al
/// abrirla a mano Personalizar decía «No se ha comprobado todavía» y volvía a ofrecer la misma versión.
///
/// Lógica pura, sin disco: quien llama lee el archivo.
/// </summary>
public static partial class UpdateHelperLog
{
    [GeneratedRegex(@"Version que se instala: (\S+)")]
    private static partial Regex TargetVersionLine();

    /// <summary>
    /// Devuelve el aviso, o <see langword="null"/> si no hay nada que decir.
    ///
    /// Se mira el registro **entero** y no solo la última línea: el guion lo reescribe de cero en cada
    /// intento, así que todo lo que hay es de la última actualización, y tras un fallo el ayudante aún
    /// apunta líneas (la reapertura de Sakura) detrás de la que dice qué falló.
    ///
    /// Ante cualquier forma que no se reconoce —registro vacío, ausente, cortado a mitad por un
    /// antivirus— devuelve <see langword="null"/>: antes callarse que inventar un fallo.
    ///
    /// **Solo avisa mientras la Sakura en marcha sea más antigua que la versión que se intentaba
    /// instalar** (el guion la apunta en el registro). Si la persona se actualizó después con el
    /// instalador o a mano, el aviso ya no es verdad y no debe quedarse para siempre. Un registro sin
    /// esa línea (de un ayudante anterior) o con una versión ilegible no dice nada: no se puede
    /// comprobar, y antes callarse que inventar.
    ///
    /// **El texto que Windows apuntó detrás de «FALLO:» no se enseña**: puede llevar rutas y mensajes
    /// del sistema, y lo que la persona lee no las lleva (mismo criterio que L16 y L17). El registro
    /// sigue en disco para quien quiera leerlo.
    /// </summary>
    public static string? Describe(string? logText, SakuraVersion running)
    {
        if (string.IsNullOrWhiteSpace(logText))
        {
            return null;
        }

        if (logText.Contains("Terminado.", StringComparison.Ordinal))
        {
            return null;
        }

        var versionLine = TargetVersionLine().Match(logText);
        if (!versionLine.Success ||
            !SakuraVersion.TryParse(versionLine.Groups[1].Value, out var attempted) ||
            running >= attempted)
        {
            return null;
        }

        if (logText.Contains("Sakura sigue abierta tras la espera", StringComparison.Ordinal))
        {
            return "La última actualización no se aplicó porque Sakura seguía abierta. Vuelve a intentarlo.";
        }

        if (logText.Contains("No hay carpeta preparada", StringComparison.Ordinal))
        {
            return "La última actualización no llegó a prepararse. Vuelve a intentarlo.";
        }

        if (logText.Contains("FALLO:", StringComparison.Ordinal))
        {
            // «Se quedó en la versión que tenías» solo es cierto si el ayudante comprobó que la
            // instalación anterior está en su sitio; si no, se dice sin prometerlo.
            if (!logText.Contains("Instalacion anterior en su sitio.", StringComparison.Ordinal))
            {
                return "La última actualización no se pudo aplicar y no se pudo confirmar que Sakura quedara entera. Si algo no funciona, vuelve a instalarla con el instalador.";
            }

            return "La última actualización no se pudo aplicar y Sakura se quedó en la versión que tenías. Vuelve a intentarlo.";
        }

        return null;
    }
}
