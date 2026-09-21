using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Nexo.App.Motion;

/// <summary>
/// Diseño D26 — el vocabulario de movimiento de Sakura en un solo sitio.
///
/// Antes cada vista construía su propia <see cref="DoubleAnimation"/> con su propia duración y su
/// propia curva. El resultado era que el mismo gesto —abrir algo— tardaba 170 ms en el shell,
/// 200 ms en un panel y 350 ms en otro, con tres curvas distintas. Nadie lo nota nombrándolo, pero
/// se siente: la aplicación parecía cosida de piezas ajenas.
///
/// Todo lo de aquí respeta <see cref="AnimationsEnabled"/>. Cuando está apagado, los métodos no
/// animan: dejan la propiedad en su valor final de inmediato. Ese es el contrato — apagar las
/// animaciones nunca puede dejar algo a medio camino ni invisible.
/// </summary>
public static class SakuraMotion
{
    /// <summary>
    /// Retardo entre elementos consecutivos de una lista que entra. Corto a propósito: un
    /// escalonado largo se lee como lentitud, no como elegancia.
    /// </summary>
    public static readonly TimeSpan Stagger = TimeSpan.FromMilliseconds(45);

    /// <summary>
    /// Tope del escalonado acumulado. Sin él, una lista de veinte elementos haría esperar casi un
    /// segundo al último, y a esa altura ya no es coreografía sino retraso.
    /// </summary>
    public static readonly TimeSpan MaximumStagger = TimeSpan.FromMilliseconds(270);

    /// <summary>
    /// Interruptor global, enlazado a la preferencia de la persona y al modo de bajo consumo.
    /// Se consulta en cada llamada, no se captura: cambiar la preferencia surte efecto al momento.
    /// </summary>
    public static bool AnimationsEnabled { get; set; } = true;

    public static Duration Fast => Resolve<Duration>("MotionFast");

    public static Duration Base => Resolve<Duration>("MotionBase");

    public static Duration Slow => Resolve<Duration>("MotionSlow");

    public static Duration Emphasized => Resolve<Duration>("MotionEmphasizedDuration");

    public static Duration Exit => Resolve<Duration>("MotionExitDuration");

    public static Duration Reveal => Resolve<Duration>("MotionRevealDuration");

    public static IEasingFunction EmphasizedCurve => Resolve<IEasingFunction>("MotionEmphasized");

    public static IEasingFunction DecelerateCurve => Resolve<IEasingFunction>("MotionDecelerate");

    public static IEasingFunction AccelerateCurve => Resolve<IEasingFunction>("MotionAccelerate");

    public static IEasingFunction SpringCurve => Resolve<IEasingFunction>("MotionSpring");

    public static IEasingFunction SubtleSpringCurve => Resolve<IEasingFunction>("MotionSpringSubtle");

    public static IEasingFunction StandardCurve => Resolve<IEasingFunction>("MotionStandard");

    /// <summary>
    /// Construye la animación. Existe como método aparte —y no en línea— porque encierra la
    /// corrección de un defecto que dejó la aplicación entera invisible: en WPF,
    /// <see cref="Timeline.BeginTime"/> con valor <c>null</c> **no** significa "sin retraso", sino
    /// que la línea de tiempo no se reproduce nunca. Pasar directamente un <c>TimeSpan?</c> vacío
    /// desactivaba en silencio cada animación que no llevara escalonado: el shell se mostraba con
    /// opacidad 0, los módulos salían en blanco y las secciones de Ajustes no abrían. Todo estaba
    /// "funcionando", solo que a cero.
    ///
    /// Teniéndolo aquí, una prueba puede comprobar la propiedad sin necesitar una ventana.
    /// </summary>
    public static DoubleAnimation CreateAnimation(
        double to,
        Duration duration,
        IEasingFunction easing,
        TimeSpan? beginTime = null) =>
        new(to, duration)
        {
            EasingFunction = easing,
            BeginTime = beginTime ?? TimeSpan.Zero
        };

    /// <summary>
    /// Diseño D58 — arranca desde donde la cosa ESTÁ, no desde donde estaba antes de moverse.
    ///
    /// Quitar una animación con <c>BeginAnimation(prop, null)</c> no congela el valor que se ve: lo
    /// devuelve al valor base, el último que se escribió a mano. Mientras una animación corre, ese
    /// valor base sigue siendo el de antes de empezar. Como resultado, reemplazar una animación a
    /// medio camino hacía que la propiedad diera un salto atrás y la nueva animación saliera desde
    /// ahí.
    ///
    /// Se veía clarísimo en el subrayado de las pestañas: de Panel a Media iba bien, pero de Media a
    /// Rendimiento **volvía de golpe a Panel y desde ahí se deslizaba hasta Rendimiento**, cruzando
    /// toda la tira. No era el subrayado: era esto, y le pasaba a cualquier animación interrumpida
    /// de la aplicación.
    ///
    /// Por eso el valor en curso se lee antes de quitarla y se vuelve a escribir como valor base.
    /// </summary>
    private static void HoldCurrentValue(UIElement element, DependencyProperty property)
    {
        var current = element.GetValue(property);
        element.BeginAnimation(property, null);
        element.SetValue(property, current);
    }

    /// <inheritdoc cref="HoldCurrentValue(UIElement, DependencyProperty)"/>
    private static void HoldCurrentValue(Transform transform, DependencyProperty property)
    {
        var current = transform.GetValue(property);
        transform.BeginAnimation(property, null);
        transform.SetValue(property, current);
    }

    /// <summary>
    /// Anima una propiedad de tipo <see cref="double"/>. Con las animaciones apagadas, fija el
    /// valor final y ejecuta <paramref name="completed"/> igual: quien encadene trabajo detrás no
    /// debe quedarse esperando una animación que nunca ocurrió.
    /// </summary>
    public static void Animate(
        this UIElement element,
        DependencyProperty property,
        double to,
        Duration duration,
        IEasingFunction easing,
        TimeSpan? beginTime = null,
        Action? completed = null)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(property);

        HoldCurrentValue(element, property);

        if (!AnimationsEnabled)
        {
            element.SetValue(property, to);
            completed?.Invoke();
            return;
        }

        var animation = CreateAnimation(to, duration, easing, beginTime);

        if (completed is not null)
        {
            animation.Completed += (_, _) => completed();
        }

        element.BeginAnimation(property, animation);
    }

    /// <summary>Igual que <see cref="Animate"/>, pero sobre una transformación en vez de un elemento.</summary>
    public static void AnimateTransform(
        this Transform transform,
        DependencyProperty property,
        double to,
        Duration duration,
        IEasingFunction easing,
        TimeSpan? beginTime = null,
        Action? completed = null)
    {
        ArgumentNullException.ThrowIfNull(transform);
        ArgumentNullException.ThrowIfNull(property);

        HoldCurrentValue(transform, property);

        if (!AnimationsEnabled)
        {
            transform.SetValue(property, to);
            completed?.Invoke();
            return;
        }

        var animation = CreateAnimation(to, duration, easing, beginTime);

        if (completed is not null)
        {
            animation.Completed += (_, _) => completed();
        }

        transform.BeginAnimation(property, animation);
    }

    /// <summary>
    /// La entrada estándar de un panel: aparece desplazándose desde <paramref name="fromOffset"/>.
    /// Devuelve la coreografía completa —opacidad y desplazamiento a la vez— porque separarlas es
    /// justo lo que hace que un movimiento se vea barato.
    /// </summary>
    public static void EnterFrom(
        this UIElement element,
        TranslateTransform transform,
        double fromOffset,
        bool vertical = false,
        TimeSpan? beginTime = null)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(transform);

        var axis = vertical
            ? TranslateTransform.YProperty
            : TranslateTransform.XProperty;

        if (!AnimationsEnabled)
        {
            element.BeginAnimation(UIElement.OpacityProperty, null);
            transform.BeginAnimation(axis, null);
            element.Opacity = 1;
            transform.SetValue(axis, 0d);
            return;
        }

        transform.BeginAnimation(axis, null);
        transform.SetValue(axis, fromOffset);
        element.Opacity = 0;

        element.Animate(
            UIElement.OpacityProperty,
            1,
            Reveal,
            DecelerateCurve,
            beginTime);

        transform.AnimateTransform(
            axis,
            0,
            Emphasized,
            EmphasizedCurve,
            beginTime);
    }

    /// <summary>
    /// El retardo del elemento en la posición <paramref name="index"/> de una lista que entra,
    /// ya recortado por <see cref="MaximumStagger"/>.
    /// </summary>
    public static TimeSpan StaggerAt(int index)
    {
        if (index <= 0)
        {
            return TimeSpan.Zero;
        }

        var delay = Stagger * index;
        return delay > MaximumStagger ? MaximumStagger : delay;
    }

    /// <summary>
    /// Los recursos viven en el diccionario de la aplicación. Se busca ahí en vez de recibirlos por
    /// parámetro para que una vista no tenga que arrastrar el tema por toda su jerarquía. El
    /// respaldo existe para las pruebas y el diseñador, donde no hay <see cref="Application"/>.
    /// </summary>
    private static T Resolve<T>(string key)
    {
        if (Application.Current?.TryFindResource(key) is T resource)
        {
            return resource;
        }

        return Fallback<T>(key);
    }

    private static T Fallback<T>(string key)
    {
        object value = key switch
        {
            "MotionFast" => new Duration(TimeSpan.FromMilliseconds(120)),
            "MotionBase" => new Duration(TimeSpan.FromMilliseconds(200)),
            "MotionSlow" => new Duration(TimeSpan.FromMilliseconds(350)),
            "MotionEmphasizedDuration" => new Duration(TimeSpan.FromMilliseconds(450)),
            // Auditoría de estilo 2026-09-20 — el respaldo decía 160 ms, pero Motion.xaml (Diseño
            // D58) usa 220 ms desde siempre. Solo afecta a pruebas y al diseñador, donde no hay
            // Application; en producción normal el recurso real ya se encuentra siempre.
            "MotionExitDuration" => new Duration(TimeSpan.FromMilliseconds(220)),
            "MotionRevealDuration" => new Duration(TimeSpan.FromMilliseconds(300)),
            "MotionSpring" => new CubicBezierEase { X1 = 0.34, Y1 = 1.56, X2 = 0.64, Y2 = 1 },
            "MotionSpringSubtle" => new CubicBezierEase { X1 = 0.34, Y1 = 1.24, X2 = 0.64, Y2 = 1 },
            "MotionDecelerate" => new CubicBezierEase { X1 = 0.05, Y1 = 0.7, X2 = 0.1, Y2 = 1 },
            "MotionAccelerate" => new CubicBezierEase { X1 = 0.3, Y1 = 0, X2 = 0.8, Y2 = 0.15 },
            _ => new CubicBezierEase { X1 = 0.2, Y1 = 0, X2 = 0, Y2 = 1 }
        };

        return (T)value;
    }
}
