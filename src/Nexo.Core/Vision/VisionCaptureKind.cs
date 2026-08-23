namespace Nexo.Core.Vision;

public enum VisionCaptureKind
{
    Window,
    Monitor,

    /// <summary>
    /// Diseño D86 — un rectángulo cualquiera de la pantalla, el que alguien arrastró.
    ///
    /// Se captura igual que un monitor: <c>CopyFromScreen</c> ya trabaja por rectángulo, así que
    /// «un monitor» nunca fue más que «esta zona» con las medidas del monitor. Lo único nuevo es
    /// quién decide el rectángulo.
    /// </summary>
    Region
}
