namespace Nexo.Core.Optimization;

/// <summary>
/// Diseño D8 (Fase 4 — Adaptive Computer Optimization) — los siete escenarios que nombra el
/// criterio de terminado de la fase en `docs/roadmap/SAKURA_TECHNOLOGY_ROADMAP.md`.
/// </summary>
public enum OptimizationScenario
{
    Jugar,
    Programar,
    EdicionVideo,
    Videollamada,
    Bateria,
    General,

    /// <summary>Deshacer: devuelve el equipo al estado guardado antes del último plan aplicado.</summary>
    Restaurar
}
