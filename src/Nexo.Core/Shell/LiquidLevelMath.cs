namespace Nexo.Core.Shell;

/// <summary>
/// La matemática del mando líquido de volumen y brillo, sin WPF para poder probarla.
///
/// Adler (2026-09-14) pidió que las barras se movieran «algo similar» a un vídeo de referencia: una
/// línea vertical que se abomba alrededor del pomo como si fuera líquido, con un brillo detrás, un
/// número grande al lado y un color que cambia según el valor. Aquí se decide la forma de esa comba,
/// el color de cada nivel y el muelle que la hace temblar al moverse; el control solo dibuja lo que
/// esto calcula.
///
/// El color es el acento de quien usa Sakura, no el amarillo y naranja del vídeo: con la primera
/// versión Adler pidió que los mandos tuvieran «el color que tenga el usuario».
/// </summary>
public static class LiquidLevelMath
{
    /// <summary>
    /// La comba: 1 en el centro, 0 a una distancia <paramref name="t"/> de ±1, y con pendiente cero en
    /// los dos extremos. Es media onda de coseno; al no tener esquinas en ningún punto, la línea entra y
    /// sale del bulto sin que se note dónde empieza, que es lo que la hace parecer líquida y no doblada.
    /// </summary>
    public static double Bump(double t)
    {
        if (!double.IsFinite(t) || Math.Abs(t) >= 1)
        {
            return 0;
        }

        return (1 + Math.Cos(Math.PI * t)) / 2;
    }

    /// <summary>
    /// Cuánto se aparta la línea de su sitio a la altura <paramref name="y"/>.
    ///
    /// <paramref name="stretch"/> va de -1 a 1 y es la inercia del movimiento: al arrastrar, la cola
    /// de la comba se alarga por detrás del pomo, como una gota, y al soltar vuelve a su forma con un
    /// pequeño rebote.
    ///
    /// **El punto más abombado se queda siempre a la altura del pomo.** La primera versión desplazaba
    /// el centro entero hacia atrás, y en movimiento el pomo quedaba fuera de su hueco: Adler lo vio
    /// «desfasado». Ahora solo se estira el lado de atrás, así que el hueco sigue abrazando el círculo.
    /// </summary>
    public static double LineOffset(double y, double knobY, double amplitude, double radius, double stretch)
    {
        if (radius <= 0 || amplitude <= 0)
        {
            return 0;
        }

        var s = Math.Clamp(stretch, -1, 1);
        var dy = y - knobY;

        // Subir (s > 0) deja la cola por debajo del pomo; bajar, por encima.
        var trailing = (dy > 0 && s > 0) || (dy < 0 && s < 0);
        var reach = trailing ? radius * (1 + (0.6 * Math.Abs(s))) : radius;
        var height = amplitude * (1 + (0.08 * Math.Abs(s)));

        return height * Bump(dy / reach);
    }

    /// <summary>
    /// El color de un nivel a partir del acento: pálido abajo, el acento tal cual a media altura y más
    /// vivo arriba. Se queda en el mismo tono para que siga siendo el color elegido; lo que cambia con
    /// el valor es la intensidad, igual que en la referencia cambiaba el calor.
    /// </summary>
    public static RgbColor ColorAt(double percent, RgbColor accent)
    {
        var p = double.IsFinite(percent) ? Math.Clamp(percent, 0, 100) / 100 : 0;
        var (low, high) = RampEnds(accent);
        return p <= 0.5
            ? ColorMath.Mix(low, accent, p * 2)
            : ColorMath.Mix(accent, high, (p - 0.5) * 2);
    }

    /// <summary>Los dos extremos de la rampa: el acento aclarado y desaturado, y el acento más saturado.</summary>
    public static (RgbColor Low, RgbColor High) RampEnds(RgbColor accent)
    {
        var r = accent.R / 255d;
        var g = accent.G / 255d;
        var b = accent.B / 255d;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var lightness = (max + min) / 2;
        var chroma = max - min;
        var saturation = chroma <= 0 ? 0 : chroma / (1 - Math.Abs((2 * lightness) - 1));
        var hue = ColorMath.Hue(accent);

        var low = ColorMath.FromHsl(hue, saturation * 0.7, Math.Min(0.88, lightness + ((1 - lightness) * 0.45)));
        // Un acento gris sigue siendo gris: sumarle saturación le inventaría un tono que nadie eligió.
        var vivid = saturation <= 0 ? 0 : Math.Min(1, (saturation * 1.15) + 0.05);
        var high = ColorMath.FromHsl(hue, vivid, Math.Max(0.5, lightness - 0.04));
        return (low, high);
    }

    /// <summary>
    /// Un paso de muelle amortiguado. Con <paramref name="dampingRatio"/> por debajo de 1 se pasa un
    /// poco y vuelve, que es el temblor de líquido; con 1 llega sin pasarse.
    ///
    /// El paso de tiempo se limita: si un fotograma llega tarde —la ventana estaba tapada, el equipo
    /// iba cargado— un salto de medio segundo haría que el muelle explotara en vez de asentarse.
    /// </summary>
    public static (double Position, double Velocity) SpringStep(
        double position,
        double velocity,
        double target,
        double deltaSeconds,
        double stiffness,
        double dampingRatio)
    {
        var dt = Math.Clamp(double.IsFinite(deltaSeconds) ? deltaSeconds : 0, 0, 1d / 30);
        if (dt == 0 || stiffness <= 0)
        {
            return (position, velocity);
        }

        var damping = 2 * Math.Sqrt(stiffness) * Math.Max(0, dampingRatio);

        // Euler semiimplícito: primero la velocidad y con ella la posición. Es estable con estos pasos
        // cortos, a diferencia del explícito, que gana energía y acaba oscilando cada vez más.
        var acceleration = (stiffness * (target - position)) - (damping * velocity);
        velocity += acceleration * dt;
        position += velocity * dt;
        return (position, velocity);
    }

    /// <summary>Si el muelle ya está quieto, para dejar de pedir fotogramas.</summary>
    public static bool IsSettled(double position, double velocity, double target, double tolerance = 0.05) =>
        Math.Abs(target - position) < tolerance && Math.Abs(velocity) < tolerance;
}
