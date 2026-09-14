namespace Nexo.Core.Permissions;

/// <summary>
/// Los seis niveles de autonomía del modelo de confianza
/// (docs/security/SAKURA_TRUST_AND_AUTONOMY_MODEL.md), completos y numerados como allí.
///
/// Nació en el Diseño D12 como <c>WorkspaceAutonomyLevel</c>, dentro del acompañante de proyecto.
/// **Diseño D17 lo mueve aquí**, junto a los permisos, porque Computer Use necesitaba la misma
/// escalera y tener dos copias del mismo concepto es exactamente el riesgo de fragmentación que el
/// roadmap señala: dos enums iguales se separan en cuanto uno crece, y entonces "nivel 4" significa
/// cosas distintas según quién pregunte. La escalera es una y es del modelo de confianza, no de
/// ninguna capacidad concreta.
///
/// El enum declara los seis a propósito aunque ninguna capacidad ofrezca hoy los dos últimos: un
/// enum recortado obligaría a renumerar cuando lleguen, y renumerar niveles de autonomía es la clase
/// de cambio en la que un archivo de ajustes viejo pasa a significar otra cosa. **Qué nivel puede
/// usarse lo decide la política de cada capacidad, no la existencia del valor.**
/// </summary>
public enum AutonomyLevel
{
    /// <summary>Sakura observa y describe, sin proponer acción.</summary>
    Ver = 1,

    /// <summary>Sakura señala qué podría hacerse; lo ejecuta la persona.</summary>
    Guiar = 2,

    /// <summary>Sakura redacta el plan de una acción concreta, sin ejecutarla.</summary>
    Proponer = 3,

    /// <summary>
    /// Ejecuta un único paso confirmado explícitamente. Disponible en el acompañante de proyecto
    /// desde D14, cuando aparecieron el checkpoint por archivo y el Audit Log que el modelo de
    /// confianza exige antes de escribir.
    /// </summary>
    EjecutarUnPaso = 4,

    /// <summary>Ejecuta varios pasos, confirmando en los puntos de riesgo. **Ninguna capacidad lo ofrece todavía.**</summary>
    ColaborarConConfirmaciones = 5,

    /// <summary>Ejecuta una secuencia aprobada de antemano. **Ninguna capacidad lo ofrece todavía.**</summary>
    AutomatizarSecuencia = 6
}
