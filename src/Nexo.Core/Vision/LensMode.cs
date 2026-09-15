namespace Nexo.Core.Vision;

/// <summary>
/// Diseño D5.6 (Fase 2 — Sakura Lens) — los tres modos que exige el criterio de terminado de
/// `docs/roadmap/SAKURA_TECHNOLOGY_ROADMAP.md` (Fase 2). Cada uno enmarca la misma captura
/// (imagen + OCR + UI Automation, ya redactados) con una pregunta y un tono distintos — ninguno
/// ejecuta ninguna acción sobre la ventana observada.
/// </summary>
public enum LensMode
{
    Soporte,
    Estudio,
    Desarrollo,

    /// <summary>
    /// 2026-09-15 — Ctrl + Shift + Espacio: qué es lo que se ve, qué está pasando, cómo resolverlo y
    /// los pasos, sin tener que preguntar nada (Adler: «debería darte la información que necesitas»).
    /// </summary>
    Explicar
}
