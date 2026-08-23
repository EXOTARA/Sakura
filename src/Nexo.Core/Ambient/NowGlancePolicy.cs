namespace Nexo.Core.Ambient;

/// <summary>Lo que la tarjeta «Ahora» enseña: una línea grande y una pequeña debajo.</summary>
public readonly record struct NowGlance(string Headline, string Detail);

/// <summary>
/// Diseño D80 — qué decir sobre lo que tienes delante.
///
/// La tarjeta contesta a una sola pregunta: **¿en qué estás ahora mismo?** Y tiene que contestarla
/// sin convertirse en un vigilante. De ahí las tres reglas que hay aquí:
///
/// **Una ventana marcada como sensible no se nombra.** Un gestor de contraseñas o una pantalla de
/// banco ya vienen marcados por <see cref="Nexo.Core.Vision.VisionPrivacyPolicy"/>, y esta tarjeta
/// vive en un panel que se abre delante de quien sea que esté al lado. Se dice que hay algo
/// abierto, nunca qué es. Enseñar «1Password — Bóveda personal» en un panel de vistazo sería
/// exactamente el fallo que esa política existe para evitar.
///
/// **El nombre del programa manda sobre el título.** El título cambia cada vez que cambias de
/// pestaña o de archivo; el programa es lo que de verdad describe en qué estás. Va arriba, y el
/// título va debajo, en pequeño, porque es el detalle y no el titular.
///
/// **Sin nada delante no se inventa nada.** Cuando Sakura es lo único abierto —o todavía no ha
/// visto otra ventana— la tarjeta lo dice y se calla. Rellenar ese hueco con la última ventana
/// recordada sería enseñar algo falso con toda la confianza del mundo.
/// </summary>
public static class NowGlancePolicy
{
    /// <summary>Cuánto título cabe antes de que deje de leerse de un vistazo.</summary>
    public const int MaximumDetailLength = 64;

    public static NowGlance Describe(AmbientContextSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return new NowGlance("Sakura", "No hay otra ventana delante");
        }

        if (snapshot.IsSensitive)
        {
            return new NowGlance("Una ventana privada", "No se muestra de cuál se trata");
        }

        var application = ToDisplayName(snapshot.ProcessName);
        var title = Trim(snapshot.WindowTitle);

        if (string.IsNullOrEmpty(application))
        {
            // Sin nombre de proceso el título es lo único que queda, así que asciende a titular.
            return string.IsNullOrEmpty(title)
                ? new NowGlance("Una ventana sin nombre", "Windows no dice cuál es")
                : new NowGlance(title, "Ventana en primer plano");
        }

        return new NowGlance(
            application,
            string.IsNullOrEmpty(title) ? "Sin título de ventana" : title);
    }

    /// <summary>
    /// «chrome» pasa a «Chrome». Nada más: no hay diccionario de nombres bonitos, porque una lista
    /// de traducciones de marcas envejece mal y falla justo con el programa que no está en ella.
    /// </summary>
    private static string ToDisplayName(string? processName)
    {
        var value = processName?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return char.ToUpperInvariant(value[0]) + value[1..];
    }

    private static string Trim(string? title)
    {
        var value = title?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Length <= MaximumDetailLength
            ? value
            : value[..(MaximumDetailLength - 1)].TrimEnd() + "…";
    }
}
