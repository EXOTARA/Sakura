namespace Nexo.Core.Voice;

/// <summary>
/// Diseño D72 — convierte un trozo de audio en un número entre 0 y 1.
///
/// Es la mitad medible del halo: el micrófono entrega muestras PCM de 16 bits y la ventana necesita
/// "cuánta voz hay ahora". Se calcula la raíz cuadrática media, que es la energía real de la señal
/// —no el pico, que salta con cualquier golpe de teclado— y se lleva a escala logarítmica, porque
/// el oído no es lineal: la diferencia entre susurrar y hablar bajo se nota mucho más que la que hay
/// entre hablar fuerte y gritar, y una escala lineal aplasta justo la mitad que importa.
/// </summary>
public static class VoiceLevelMeter
{
    /// <summary>
    /// Suelo de la escala. Por debajo de -50 dB está el ruido de una habitación en silencio, y todo
    /// eso se mapea a cero: si no, el halo respiraría con el ventilador del equipo.
    /// </summary>
    public const double FloorDecibels = -50;

    /// <summary>
    /// Nivel de 0 a 1 de un búfer PCM de 16 bits con signo, entrelazado o mono (da igual: la energía
    /// de la mezcla sirve).
    /// </summary>
    public static double Measure(ReadOnlySpan<byte> buffer, int bytesRecorded)
    {
        var usable = Math.Min(bytesRecorded, buffer.Length);
        if (usable < 2)
        {
            return 0;
        }

        // Un número impar de bytes significa una muestra partida por la mitad; se ignora la cola.
        var samples = usable / 2;
        double sum = 0;

        for (var i = 0; i < samples; i++)
        {
            var sample = (short)(buffer[i * 2] | (buffer[(i * 2) + 1] << 8));
            var normalized = sample / 32768.0;
            sum += normalized * normalized;
        }

        var rms = Math.Sqrt(sum / samples);
        if (rms <= 0)
        {
            return 0;
        }

        var decibels = 20 * Math.Log10(rms);
        if (decibels <= FloorDecibels)
        {
            return 0;
        }

        return Math.Clamp((decibels - FloorDecibels) / -FloorDecibels, 0, 1);
    }
}
