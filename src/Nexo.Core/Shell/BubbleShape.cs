namespace Nexo.Core.Shell;

/// <summary>Un instante de la burbuja: su ancho, su alto y el radio de sus esquinas.</summary>
public readonly record struct BubbleFrame(double Width, double Height, double Radius);

/// <summary>
/// La forma de una píldora que nace como burbuja: un círculo al principio y la píldora entera al final.
///
/// El círculo mide lo que el alto de la píldora, con un tope: una píldora alta que empezara como un
/// círculo de ese alto ya no se leería como una burbuja sino como una tarjeta redonda. Con el tope
/// se ve siempre un botón pequeño que se infla.
///
/// El radio nunca pasa de la mitad del alto. Si lo hiciera, WPF lo recortaría a su manera en cada
/// esquina y la forma se deformaría a mitad de camino.
/// </summary>
public readonly record struct BubbleShape(double FullWidth, double FullHeight, double FinalRadius)
{
    public const double MaximumSeed = 52;

    public double Seed => Math.Max(1, Math.Min(Math.Min(FullWidth, FullHeight), MaximumSeed));

    /// <param name="progress">De 0 (círculo) a 1 (píldora). Se recorta a ese rango.</param>
    public BubbleFrame At(double progress)
    {
        var p = double.IsFinite(progress) ? Math.Clamp(progress, 0, 1) : 1;
        var seed = Seed;

        // El ancho se adelanta al alto: primero se estira hacia los lados y luego gana cuerpo, que es
        // lo que hace que parezca una gota abriéndose y no un rectángulo escalándose.
        var widthProgress = Math.Min(1, p * 1.15);
        var heightProgress = p * p;

        var width = seed + ((FullWidth - seed) * widthProgress);
        var height = seed + ((FullHeight - seed) * heightProgress);
        var radius = (seed / 2) + ((Math.Max(0, FinalRadius) - (seed / 2)) * p);

        return new BubbleFrame(width, height, Math.Min(radius, height / 2));
    }
}
