namespace Nexo.Core.Shell;

/// <summary>
/// 2026-09-18 — la permanencia de los bordes, con la regla de «un botón pulsado no cuenta».
///
/// Un amigo de Adler veía abrirse el panel de arriba mientras arrastraba una ventana: al llevarla
/// hacia el borde superior (para maximizarla, por ejemplo) el cursor se quedaba en la franja más de
/// 160 ms y el vigilante, que no miraba los botones, lo tomaba por un gesto de llamar al panel.
/// Con un botón pulsado la persona está arrastrando, seleccionando o pulsando algo, nunca llamando
/// al borde; y como la permanencia se reinicia mientras dure, soltar dentro de la franja empieza a
/// contar desde cero en vez de disparar al instante con el tiempo acumulado del arrastre. Y no
/// basta con reiniciar: quien suelta una ventana en y=0 y se queda quieto tampoco llama al panel,
/// así que el borde queda desarmado hasta que el cursor sale de la zona cercana.
///
/// Vive en Core, sin Windows, para que esta decisión se pruebe sin pulsar nada: quien lee el botón
/// es el vigilante, aquí solo se decide.
/// </summary>
public sealed class RevealDwellGate
{
    private readonly Func<bool> _mouseButtonDown;
    private DateTimeOffset? _insideSince;

    /// <summary>
    /// Si el borde quedó desarmado por un botón pulsado dentro o cerca de la franja. Solo se
    /// rearma cuando el cursor sale de la zona cercana: soltar una ventana arrastrada hasta y=0 y
    /// quedarse quieto NO abre el panel (era el gesto de maximizar que motivó el reporte).
    /// </summary>
    private bool _disarmed;

    /// <summary>
    /// El delegado de botones es obligatorio y se pide aquí, no en cada llamada: así un vigilante
    /// no puede olvidarse de consultarlo sin que ni siquiera compile.
    /// </summary>
    public RevealDwellGate(Func<bool> mouseButtonDown)
    {
        _mouseButtonDown = mouseButtonDown ?? throw new ArgumentNullException(nameof(mouseButtonDown));
    }

    /// <summary>Descarta la permanencia acumulada (cambio de ajustes, silencio tras ocultar).</summary>
    public void Reset()
    {
        _insideSince = null;
        _disarmed = false;
    }

    /// <summary>
    /// Anota una lectura y dice si ya toca abrir. <paramref name="nearZone"/> es la zona ampliada
    /// (la de rearme, más ancha que la sensible) e incluye a <paramref name="insideZone"/>.
    /// Al devolver true la permanencia se reinicia: sin eso, quedarse en el borde dispararía en
    /// cada vuelta del temporizador.
    /// </summary>
    public bool Observe(DateTimeOffset now, bool insideZone, bool nearZone, TimeSpan dwell)
    {
        nearZone |= insideZone;
        var buttonDown = _mouseButtonDown();

        if (!nearZone)
        {
            _disarmed = false;
        }
        else if (buttonDown)
        {
            _disarmed = true;
        }

        if (!insideZone || buttonDown || _disarmed)
        {
            _insideSince = null;
            return false;
        }

        _insideSince ??= now;

        if (now - _insideSince.Value < dwell)
        {
            return false;
        }

        _insideSince = null;
        return true;
    }
}
