namespace Nexo.Core.Vision;

/// <summary>
/// Diseño D5.3 (Fase 2 — Sakura Lens) — un elemento de la interfaz de una ventana ajena, leído por
/// <see cref="IUiAutomationReader"/>. Solo lectura: nombre, tipo de control y posición en pantalla
/// (píxeles, origen arriba-izquierda). Nunca incluye una acción para invocarlo — Lens observa y
/// guía, no actúa (ver `docs/roadmap/SAKURA_TECHNOLOGY_ROADMAP.md`, Fase 2, "No objetivos").
/// </summary>
public sealed record UiAutomationElement(
    string? Name,
    string ControlType,
    int Left,
    int Top,
    int Width,
    int Height);
