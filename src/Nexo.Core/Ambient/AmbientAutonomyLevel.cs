namespace Nexo.Core.Ambient;

/// <summary>
/// Diseño D4 — subconjunto de los niveles de autonomía descritos en
/// docs/security/SAKURA_TRUST_AND_AUTONOMY_MODEL.md que D4 puede ofrecer. D4 sienta las bases
/// ambientales, no ejecuta nada por sí mismo: los niveles 4+ (ejecución) llegan con las Fases 5/7
/// y su Permission Broker completo.
/// </summary>
public enum AmbientAutonomyLevel
{
    Ver = 1,
    Guiar = 2,
    Proponer = 3
}
