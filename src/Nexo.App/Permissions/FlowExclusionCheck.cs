using Nexo.Core.Ambient;
using Nexo.Core.Permissions;

namespace Nexo.App.Permissions;

/// <summary>
/// Lo que Flow consulta al broker. No es una puerta nueva: solo construye la petición con la
/// aplicación destino y aplica la parte de la decisión que Flow puede cumplir hoy.
///
/// Flow se dispara con el teclado mientras otra aplicación tiene el foco. Un diálogo de
/// confirmación le robaría ese foco y el dictado acabaría en el portapapeles en vez de escrito,
/// así que «Preguntar» NO se resuelve aquí: sigue comportándose como antes (dicta sin diálogo)
/// hasta que se decida cómo preguntar sin perder el foco. Bloqueado y las aplicaciones excluidas
/// sí se respetan.
/// </summary>
public static class FlowExclusionCheck
{
    /// <summary>Texto contra el que se comparan las exclusiones: título y proceso de la ventana.</summary>
    public static string? DescribeTarget(AmbientContextSnapshot? context)
    {
        if (context is null)
        {
            return null;
        }

        var parts = new[] { context.WindowTitle, context.ProcessName }
            .Where(part => !string.IsNullOrWhiteSpace(part));
        var text = string.Join(" — ", parts);
        return text.Length == 0 ? null : text;
    }

    public static PermissionRequest BuildRequest(string description, string? targetApp) =>
        new(SakuraCapability.Flow, description, targetApp);

    /// <summary>Devuelve la decisión del broker; solo <c>IsDenied</c> debe detener el dictado.</summary>
    public static PermissionDecision Evaluate(
        PermissionSettings settings,
        string description,
        string? targetApp) =>
        PermissionBroker.Decide(BuildRequest(description, targetApp), settings);
}
