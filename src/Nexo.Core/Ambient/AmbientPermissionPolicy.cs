namespace Nexo.Core.Ambient;

/// <summary>
/// Diseño D4 — primitivas de permisos para las acciones rápidas de un resultado ambiental. No es
/// el Permission Broker completo de la Fase 7 (docs/security/SAKURA_TRUST_AND_AUTONOMY_MODEL.md):
/// no hay todavía registro de auditoría ni exclusión por aplicación/carpeta. Lo que sí aplica ya
/// es el principio rector del modelo de confianza — "Sakura nunca debe pasar silenciosamente de
/// Mirando a Actuando" — por lo que <see cref="IsAllowed"/> falla cerrado ante cualquier nivel de
/// autonomía que no reconozca explícitamente, en vez de asumir que es seguro.
/// </summary>
public static class AmbientPermissionPolicy
{
    public static bool RequiresConfirmation(AmbientQuickAction action) =>
        action.RequiresConfirmation;

    public static bool IsAllowed(AmbientQuickAction action) => action.Level switch
    {
        AmbientAutonomyLevel.Ver or AmbientAutonomyLevel.Guiar or AmbientAutonomyLevel.Proponer =>
            true,
        _ => false
    };
}
