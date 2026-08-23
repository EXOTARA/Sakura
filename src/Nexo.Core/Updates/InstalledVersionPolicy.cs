using Nexo.Core.Branding;

namespace Nexo.Core.Updates;

/// <summary>Lo que habría que dejar escrito en la entrada de «Aplicaciones instaladas».</summary>
public readonly record struct InstalledVersionCorrection(string DisplayName, string DisplayVersion);

/// <summary>
/// Diseño D87 — que la lista de aplicaciones de Windows diga la versión que hay de verdad.
///
/// **L12, y estaba confirmada.** En el equipo de Adler, con el ejecutable ya en 0.28, la entrada de
/// «Aplicaciones instaladas» seguía diciendo `Sakura 0.27.0-beta`. La causa se conocía: el
/// actualizador sustituye los archivos de la carpeta mediante su ayudante, no ejecuta el
/// instalador, así que nadie toca el registro. Cada actualización aumentaba la mentira.
///
/// Es cosmético hasta el momento en que alguien intenta averiguar qué versión tiene instalada — que
/// es exactamente para lo que existe esa lista. Y con el instalador ya publicado, quien lo mire va
/// a ser gente que no conoce el proyecto.
///
/// **Se reconcilia al arrancar, no al actualizar.** Escribirlo solo durante la actualización
/// arreglaría las futuras y dejaría mintiendo a todas las instalaciones que ya están mal — la de
/// Adler entre ellas. Comprobarlo en cada arranque las corrige todas, incluida cualquiera que se
/// quede a medias por lo que sea.
///
/// **Sin entrada no se inventa una.** Quien usa el zip portable no aparece en esa lista y no debe
/// aparecer: crear una entrada de desinstalación para algo que se instala descomprimiendo sería
/// prometer un desinstalador que no existe.
/// </summary>
public static class InstalledVersionPolicy
{
    /// <summary>
    /// Qué corregir, o <c>null</c> si no hay nada que hacer.
    /// </summary>
    /// <param name="recordedVersion">
    /// Lo que dice el registro hoy, o <c>null</c> si no hay entrada — es decir, instalación
    /// portable.
    /// </param>
    /// <param name="runningVersion">La versión que de verdad se está ejecutando.</param>
    public static InstalledVersionCorrection? Reconcile(
        string? recordedVersion,
        string runningVersion)
    {
        if (recordedVersion is null)
        {
            // No hay entrada: portable. No se crea ninguna.
            return null;
        }

        if (string.IsNullOrWhiteSpace(runningVersion))
        {
            // Sin saber qué versión corre no se puede corregir nada, y escribir algo vacío sería
            // cambiar un dato viejo por uno inútil.
            return null;
        }

        var running = runningVersion.Trim();

        if (string.Equals(recordedVersion.Trim(), running, StringComparison.Ordinal))
        {
            return null;
        }

        // El mismo formato que escribe el instalador (`AppVerName` en Sakura.iss): si eso cambia
        // allí, tiene que cambiar aquí, o cada arranque reescribiría lo que el instalador acaba de
        // poner.
        return new InstalledVersionCorrection(
            $"{ProductIdentity.ProductName} {running}",
            running);
    }
}
