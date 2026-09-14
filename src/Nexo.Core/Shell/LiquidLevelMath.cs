namespace Nexo.Core.Shell;

/// <summary>
/// La matemática del mando líquido de volumen y brillo, sin WPF para poder probarla.
///
/// Adler (2026-09-14) pidió que las barras se movieran «algo similar» a un vídeo de referencia: una
/// línea vertical que se abomba alrededor del pomo como si fuera líquido, con un brillo detrás, un
/// número grande al lado y un color que va del amarillo al naranja según el valor. Aquí se decide la
/// forma de esa comba, el color de cada nivel y el muelle que la hace temblar al moverse; el control
/// solo dibuja lo que esto calcula.
/// </summary>
public static class LiquidLevelMath
{
    /// <summary>El tono al 0 %, al 50 % y al 100 %: lima, amarillo y naranja, como en la referencia.</summary>
    public static readonly RgbColor Low = RgbColor.FromHex("#CFEF3F");
    public static readonly RgbColor Middle = RgbColor.FromHex("#F4DE3D");
    public static readonly RgbColor High = RgbColor.FromHex("#F79A3E");

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
    /// <paramref name="stretch"/> va de -1 a 1 y es la inercia del movimiento: al arrastrar hacia
    /// arriba la comba se alarga y su centro se queda un poco atrás, como una gota que va detrás del
    /// dedo, y al soltar vuelve a su forma con un pequeño rebote.
    /// </summary>
    public static double LineOffset(double y, double knobY, double amplitude, double radius, double stretch)
    {
        if (radius <= 0 || amplitude <= 0)
        {
            return 0;
        }

        var s = Math.Clamp(stretch, -1, 1);
        var reach = radius * (1 + (0.45 * Math.Abs(s)));
        var lag = radius * 0.28 * s;
        var height = amplitude * (1 + (0.18 * Math.Abs(s)));

        return height * Bump((y - (knobY + lag)) / reach);
    }

    /// <summary>El color de un nivel, repartido en dos tramos para pasar por el amarillo a media altura.</summary>
    public static RgbColor ColorAt(double percent)
    {
        var p = double.IsFinite(percent) ? Math.Clamp(percent, 0, 100) / 100 : 0;
        return p <= 0.5
            ? ColorMath.Mix(Low, Middle, p * 2)
            : ColorMath.Mix(Middle, High, (p - 0.5) * 2);
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
