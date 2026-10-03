namespace Nexo.Core.Shell;

/// <summary>Los colores de superficie que definen un tema, ya derivados de un acento.</summary>
public sealed record SakuraThemePalette(
    RgbColor Accent,
    RgbColor AccentSoft,
    RgbColor Background,
    RgbColor Surface,
    RgbColor SurfaceRaised,
    RgbColor SurfaceHover,
    RgbColor SidebarSurface,
    RgbColor Input,
    RgbColor Border);

/// <summary>
/// Diseño D33 — construye el tema completo a partir de un solo acento, venga de la galería, de
/// Windows o del fondo de escritorio.
///
/// Antes esto mezclaba una pizca de acento sobre unos grises fijos, y estaba mal de raíz: esos
/// grises no eran neutros. El de las tarjetas, <c>#111528</c>, tenía el azul a más del doble que el
/// rojo. Mezclarle un 8% de un acento naranja lo dejaba azul igual, así que con un tema cálido el
/// panel se teñía pero las tarjetas seguían viéndose moradas — «todavía hay colores que se quedan
/// diferentes». No era que faltara teñir más sitios: era que se partía de un color que ya tenía
/// tono propio y ganaba siempre.
///
/// Ahora cada superficie se construye desde el tono del acento con una saturación y una luminosidad
/// fijas por capa. No queda ningún tono heredado que arrastrar, y la jerarquía visual —qué capa se
/// ve más clara que cuál— la fija la luminosidad, que es explícita y no depende del acento.
///
/// El límite duro sigue siendo el mismo: el texto tiene que leerse encima. Con los cuatro colores de
/// la galería eso nunca fue un problema; con un acento sacado de una foto cualquiera, sí puede
/// serlo, así que cada superficie se comprueba y cede saturación si hace falta.
/// </summary>
public static class SakuraThemeBuilder
{
    /// <summary>El color del texto principal, que es lo que hay que mantener legible.</summary>
    public const string TextPrimaryHex = "#DFE1EA";

    /// <summary>Umbral de WCAG para texto normal. Por debajo de esto, el texto deja de leerse cómodo.</summary>
    public const double MinimumTextContrast = 4.5;

    /// <summary>
    /// La superficie contra la que se decide si un acento candidato se ve, antes de que exista el
    /// tema. Es un gris neutro a la luminosidad del fondo: no puede ser una superficie ya teñida
    /// porque esas se derivan del acento que todavía se está eligiendo, y medir contra el tema
    /// anterior haría que el resultado dependiera de qué tema estaba puesto antes.
    /// </summary>
    public static RgbColor AccentReferenceSurface { get; } = ColorMath.FromHsl(0, 0, 0.055);

    /// <summary>
    /// Una capa del tema: cuánto color tiene y cuánta luz. Las luminosidades van en orden creciente
    /// —fondo, franja, tarjeta, tarjeta elevada, tarjeta bajo el ratón— porque es así como se lee la
    /// profundidad, y mantenerlo explícito evita que un acento claro u oscuro aplane la jerarquía.
    /// </summary>
    private readonly record struct Layer(double Saturation, double Lightness);

    private static readonly Layer BackgroundLayer = new(0.22, 0.055);
    private static readonly Layer InputLayer = new(0.24, 0.068);
    private static readonly Layer SidebarLayer = new(0.24, 0.078);
    private static readonly Layer SurfaceLayer = new(0.22, 0.098);
    private static readonly Layer SurfaceRaisedLayer = new(0.20, 0.125);
    private static readonly Layer SurfaceHoverLayer = new(0.20, 0.170);

    /// <summary>
    /// El borde admite bastante más color porque no lleva texto encima: es una línea de un píxel, y
    /// es justo donde el tono del tema se aprecia sin restarle legibilidad a nada.
    /// </summary>
    private static readonly Layer BorderLayer = new(0.26, 0.270);

    public static SakuraThemePalette FromAccent(RgbColor accent)
    {
        var hue = ColorMath.Hue(accent);
        var text = RgbColor.FromHex(TextPrimaryHex);

        return new SakuraThemePalette(
            Accent: accent,
            AccentSoft: BuildReadable(hue, new Layer(0.32, 0.145), text),
            Background: BuildReadable(hue, BackgroundLayer, text),
            Surface: BuildReadable(hue, SurfaceLayer, text),
            SurfaceRaised: BuildReadable(hue, SurfaceRaisedLayer, text),
            SurfaceHover: BuildReadable(hue, SurfaceHoverLayer, text),
            SidebarSurface: BuildReadable(hue, SidebarLayer, text),
            Input: BuildReadable(hue, InputLayer, text),

            // El borde no pasa por la comprobación de contraste a propósito: no hay texto encima que
            // proteger, y someterlo al mismo límite le quitaría el color sin que eso mejorara la
            // legibilidad de nada.
            Border: ColorMath.FromHsl(hue, BorderLayer.Saturation, BorderLayer.Lightness));
    }

    /// <summary>Umbral de WCAG 1.4.11 para elementos gráficos y componentes de interfaz.</summary>
    public const double MinimumFocusContrast = 3.0;

    /// <summary>
    /// El color del anillo de foco del teclado: el acento OPACO, y más claro solo si hiciera falta.
    ///
    /// Auditoría de movimiento y accesibilidad (2026-10): el anillo era el acento con alfa 112 sobre
    /// un borde de 1 px, y compuesto sobre las superficies medía 2,1–2,2:1, por debajo del 3:1 que
    /// pide WCAG para saber dónde está el foco. El acento sólido da 6,7:1 con el acento de siempre,
    /// pero un acento sacado de una foto o de Windows puede quedarse corto contra la superficie más
    /// clara (la de pasar el ratón), así que se aclara en pasos hasta cumplirlo contra todas.
    /// </summary>
    public static RgbColor FocusRing(SakuraThemePalette theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        RgbColor[] surfaces =
        [
            theme.Background, theme.Surface, theme.SurfaceRaised,
            theme.SurfaceHover, theme.SidebarSurface, theme.Input
        ];
        var white = new RgbColor(255, 255, 255);

        for (var amount = 0.0; amount <= 1.0; amount += 0.1)
        {
            var candidate = ColorMath.Mix(theme.Accent, white, amount);
            if (surfaces.All(surface => ColorMath.ContrastRatio(candidate, surface) >= MinimumFocusContrast))
            {
                return candidate;
            }
        }

        return white;
    }

    /// <summary>
    /// Construye la capa y le baja la saturación en pasos si el texto deja de cumplir el contraste
    /// mínimo. Se cede saturación y no luminosidad porque la luminosidad es la que sostiene la
    /// jerarquía de profundidad: bajarla arreglaría el contraste rompiendo qué capa va encima de
    /// cuál. El gris de la misma luminosidad es el peor caso y siempre cumple.
    /// </summary>
    private static RgbColor BuildReadable(double hue, Layer layer, RgbColor text)
    {
        const double step = 0.02;

        for (var saturation = layer.Saturation; saturation > 0; saturation -= step)
        {
            var candidate = ColorMath.FromHsl(hue, saturation, layer.Lightness);
            if (ColorMath.ContrastRatio(candidate, text) >= MinimumTextContrast)
            {
                return candidate;
            }
        }

        return ColorMath.FromHsl(hue, 0, layer.Lightness);
    }
}
