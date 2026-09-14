namespace Nexo.Core.Flow;

/// <summary>
/// Diseño D6 (Fase 3 — Sakura Flow) — los tres modos de dictado que exige el criterio de terminado
/// de la Fase 3 (`docs/roadmap/SAKURA_TECHNOLOGY_ROADMAP.md`). No son etiquetas cosméticas: cada
/// uno cambia de verdad cómo se convierte lo dictado en texto insertable (ver
/// <see cref="SpanishDictationNormalizer"/>).
/// </summary>
public enum FlowMode
{
    /// <summary>Prosa normal: puntuación hablada, mayúscula al inicio de cada oración.</summary>
    Texto,

    /// <summary>Como <see cref="Texto"/>, más símbolos de correo ("arroba", "guion bajo") sin espacios alrededor.</summary>
    Correo,

    /// <summary>
    /// Código: **sin** mayúscula automática (el código distingue mayúsculas y suele ser minúsculo)
    /// y con símbolos de programación (llaves, corchetes, flecha, igual) además de los básicos.
    /// </summary>
    Codigo
}
