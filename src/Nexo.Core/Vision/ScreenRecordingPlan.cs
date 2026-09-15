namespace Nexo.Core.Vision;

/// <summary>Qué grabar: con qué sonido y a cuántos fotogramas por segundo.</summary>
public sealed record ScreenRecordingOptions(bool SystemAudio, bool Microphone, int FramesPerSecond = 60);

/// <summary>El tamaño y la calidad con que se codifica una grabación.</summary>
public sealed record ScreenRecordingPlan(int Width, int Height, int FramesPerSecond, uint VideoBitsPerSecond)
{
    /// <summary>
    /// 2026-09-15 — el plan para una pantalla de este tamaño. El codificador H.264 pide lados pares,
    /// así que se recorta un píxel si hace falta. La tasa de bits sigue a los píxeles por segundo con un
    /// tope: unos 12 Mb/s para 1080p a 60, que es lo que usan las grabadoras de juegos en calidad
    /// normal, sin llegar a los archivos enormes de una calidad «sin pérdidas».
    /// </summary>
    public static ScreenRecordingPlan For(int screenWidth, int screenHeight, int framesPerSecond = 60)
    {
        var width = Math.Max(2, screenWidth - screenWidth % 2);
        var height = Math.Max(2, screenHeight - screenHeight % 2);
        var fps = Math.Clamp(framesPerSecond, 15, 60);

        // 0,1 bits por píxel por fotograma es lo habitual en H.264 para pantalla con movimiento.
        var bits = width * (double)height * fps * 0.1;
        var bitrate = (uint)Math.Clamp(bits, 4_000_000, 40_000_000);
        return new ScreenRecordingPlan(width, height, fps, bitrate);
    }

    /// <summary>
    /// Cuántos megas ocupa más o menos un minuto, para decirlo antes de grabar sin prometer de más.
    /// </summary>
    public double ApproximateMegabytesPerMinute(bool withAudio) =>
        (VideoBitsPerSecond + (withAudio ? 192_000 : 0)) * 60 / 8d / 1024 / 1024;
}
