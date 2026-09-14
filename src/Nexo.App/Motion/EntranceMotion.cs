using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Nexo.App.Motion;

/// <summary>
/// Las piezas de la entrada escalonada: bloques que crecen, contenido que sube, burbujas que se inflan
/// y un texto que aparece palabra a palabra. Los tiempos los decide
/// <see cref="Nexo.Core.Shell.EntranceChoreography"/>; esto solo sabe mover cada pieza.
///
/// Todas dejan la pieza visible y en su sitio aunque las animaciones estén apagadas o la animación se
/// interrumpa: la opacidad termina en 1 con <c>HoldEnd</c>, y lo que se añade —las transformaciones,
/// las palabras sueltas— queda en reposo o se deshace al acabar.
/// </summary>
public static class EntranceMotion
{
    /// <summary>
    /// Un bloque que nace vacío y crece desde abajo, como en la referencia: se ve primero la forma de la
    /// tarjeta y después, con <see cref="Rise"/>, lo que lleva dentro.
    /// </summary>
    public static void GrowBlock(FrameworkElement block, TimeSpan begin)
    {
        var (scale, translate) = EnsureTransforms(block, new Point(0.5, 1));

        scale.ScaleX = 0.96;
        scale.ScaleY = 0.86;
        translate.Y = 22;
        block.Opacity = 0;

        block.Animate(UIElement.OpacityProperty, 1, SakuraMotion.Reveal, SakuraMotion.DecelerateCurve, begin);
        scale.AnimateTransform(ScaleTransform.ScaleXProperty, 1, SakuraMotion.Emphasized, SakuraMotion.EmphasizedCurve, begin);
        scale.AnimateTransform(ScaleTransform.ScaleYProperty, 1, SakuraMotion.Emphasized, SakuraMotion.SubtleSpringCurve, begin);
        translate.AnimateTransform(TranslateTransform.YProperty, 0, SakuraMotion.Emphasized, SakuraMotion.EmphasizedCurve, begin);
    }

    /// <summary>El contenido de un bloque: sube un poco mientras aparece.</summary>
    public static void Rise(FrameworkElement element, TimeSpan begin, double offset = 10)
    {
        var (_, translate) = EnsureTransforms(element, new Point(0.5, 0.5));

        translate.Y = offset;
        element.Opacity = 0;

        element.Animate(UIElement.OpacityProperty, 1, SakuraMotion.Reveal, SakuraMotion.DecelerateCurve, begin);
        translate.AnimateTransform(TranslateTransform.YProperty, 0, SakuraMotion.Emphasized, SakuraMotion.EmphasizedCurve, begin);
    }

    /// <summary>Una burbuja: se infla desde más pequeña y se asienta con un rebote corto.</summary>
    public static void Pop(FrameworkElement element, TimeSpan begin, double from = 0.82)
    {
        var (scale, _) = EnsureTransforms(element, new Point(0.5, 0.5));

        scale.ScaleX = from;
        scale.ScaleY = from;
        element.Opacity = 0;

        element.Animate(UIElement.OpacityProperty, 1, SakuraMotion.Reveal, SakuraMotion.DecelerateCurve, begin);
        scale.AnimateTransform(ScaleTransform.ScaleXProperty, 1, SakuraMotion.Emphasized, SakuraMotion.SpringCurve, begin);
        scale.AnimateTransform(ScaleTransform.ScaleYProperty, 1, SakuraMotion.Emphasized, SakuraMotion.SpringCurve, begin);
    }

    /// <summary>
    /// El texto aparece palabra a palabra. Cada palabra es un <see cref="Run"/> con su propia brocha,
    /// y lo que se anima es la opacidad de esa brocha.
    ///
    /// Al terminar se devuelve el texto original a la propiedad <c>Text</c>: las palabras sueltas
    /// llevan un color copiado en el momento de empezar, y si se quedaran, un cambio de tema posterior
    /// ya no llegaría a este título.
    /// </summary>
    public static void RevealWords(TextBlock text, IReadOnlyList<TimeSpan> begins)
    {
        var original = text.Text;
        var words = original.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (words.Length == 0 || begins.Count < words.Length || !SakuraMotion.AnimationsEnabled)
        {
            return;
        }

        var color = (text.Foreground as SolidColorBrush)?.Color ?? Colors.White;
        text.Inlines.Clear();

        for (var i = 0; i < words.Length; i++)
        {
            var brush = new SolidColorBrush(color) { Opacity = 0 };
            text.Inlines.Add(new Run(i == 0 ? words[i] : " " + words[i]) { Foreground = brush });

            var animation = SakuraMotion.CreateAnimation(1, SakuraMotion.Reveal, SakuraMotion.DecelerateCurve, begins[i]);
            if (i == words.Length - 1)
            {
                animation.Completed += (_, _) => text.Text = original;
            }

            brush.BeginAnimation(Brush.OpacityProperty, animation);
        }
    }

    private static (ScaleTransform Scale, TranslateTransform Translate) EnsureTransforms(UIElement element, Point origin)
    {
        if (element.RenderTransform is TransformGroup { Children.Count: 2 } group &&
            group.Children[0] is ScaleTransform existingScale &&
            group.Children[1] is TranslateTransform existingTranslate &&
            !group.IsFrozen)
        {
            element.RenderTransformOrigin = origin;
            return (existingScale, existingTranslate);
        }

        var scale = new ScaleTransform(1, 1);
        var translate = new TranslateTransform();
        element.RenderTransform = new TransformGroup { Children = { scale, translate } };
        element.RenderTransformOrigin = origin;
        return (scale, translate);
    }
}
