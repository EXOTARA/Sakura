namespace Nexo.Core.Voice;

public sealed record VoicePreparationProgress(string Detail, long BytesDownloaded = 0)
{
    /// <summary>
    /// Lo que pesa el modelo de transcripción que se descarga (<c>ggml-base.bin</c> de Whisper),
    /// medido en un equipo donde ya estaba descargado. El servidor no siempre anuncia el tamaño, y
    /// sin un total no hay porcentaje que enseñar.
    /// </summary>
    public const long ExpectedWhisperBaseBytes = 147_951_465;

    /// <summary>
    /// Cuánto va de la descarga, de 0 a 1, o nulo si no se está descargando. Se queda en el 99 %
    /// hasta que el archivo está completo: llegar al 100 % y seguir esperando se lee como un fallo.
    /// </summary>
    public double? Fraction => BytesDownloaded > 0
        ? Math.Min(0.99, BytesDownloaded / (double)ExpectedWhisperBaseBytes)
        : null;

    public static VoicePreparationProgress Preparing(string detail) =>
        new(detail, 0);

    public static VoicePreparationProgress Downloading(long bytesDownloaded)
    {
        var safeBytes = Math.Max(0, bytesDownloaded);
        var downloadedMegabytes = safeBytes / 1024d / 1024d;

        return new(
            $"Descargando modelo de voz… {downloadedMegabytes:0} MB",
            safeBytes);
    }
}
