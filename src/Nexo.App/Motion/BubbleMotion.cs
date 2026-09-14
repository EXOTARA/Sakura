using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Nexo.Core.Shell;

namespace Nexo.App.Motion;

/// <summary>
/// Píldoras y avisos que aparecen como una burbuja (Adler, 2026-09-14): nacen como un círculo
/// pequeño que se infla, se estiran hasta su forma de píldora con un pequeño temblor al llegar, y
/// solo entonces aparece lo que llevan dentro. Al irse hacen el camino contrario: el contenido se
/// apaga, la píldora se recoge en círculo y el círculo se desvanece encogiéndose.
///
/// **Se anima la forma de verdad —ancho, alto y radio—, no un recorte.** Recortar sería más barato,
/// pero un recorte en WPF se lleva por delante la sombra y el trazo del borde en los lados cortados:
/// se probó y la burbuja salía plana, sin sombra, con los cantos sin perfilar. Cambiando el tamaño
/// del propio borde, la sombra y el trazo lo acompañan en cada fotograma.
///
/// Mientras dura, el contenido se queda con el ancho que tendrá al final aunque no quepa: así no se
/// recoloca el texto en cada fotograma, y como aún es transparente no se ve que sobresale.
///
/// Los tamaños intermedios los calcula <see cref="BubbleShape"/>, que se puede probar sin ventana.
/// </summary>
public static class BubbleMotion
{
    private static readonly TimeSpan GrowDelay = TimeSpan.FromMilliseconds(70);
    private static readonly Duration GrowDuration = TimeSpan.FromMilliseconds(430);
    private static readonly TimeSpan ContentDelay = TimeSpan.FromMilliseconds(300);
    private static readonly Duration ShrinkDuration = TimeSpan.FromMilliseconds(210);

    private static readonly DependencyProperty ProgressProperty = DependencyProperty.RegisterAttached(
        "Progress", typeof(double), typeof(BubbleMotion), new PropertyMetadata(1d, OnProgressChanged));

    private static readonly DependencyProperty MorphProperty = DependencyProperty.RegisterAttached(
        "Morph", typeof(MorphState), typeof(BubbleMotion), new PropertyMetadata(null));

    private sealed record MorphState(
        BubbleShape Shape,
        HorizontalAlignment OriginalHorizontal,
        VerticalAlignment OriginalVertical,
        CornerRadius OriginalRadius,
        FrameworkElement? Content,
        HorizontalAlignment ContentHorizontal);

    /// <summary>
    /// Infla <paramref name="body"/> desde un círculo. <paramref name="anchor"/> es el lado desde el
    /// que crece: el centro para lo que va arriba en medio, el lado de Sakura para lo que va en su esquina.
    /// </summary>
    public static void Inflate(Border body, HorizontalAlignment anchor, Action? completed = null)
    {
        var content = body.Child as FrameworkElement;
        StopAll(body, content);
        RestoreLayout(body);

        // Una salida interrumpida puede haber dejado el contenido apagado y la burbuja encogida.
        if (content is not null)
        {
            content.Opacity = 1;
        }

        if (body.RenderTransform is ScaleTransform resting && !resting.IsFrozen)
        {
            resting.ScaleX = 1;
            resting.ScaleY = 1;
        }

        if (!SakuraMotion.AnimationsEnabled)
        {
            body.Opacity = 1;
            completed?.Invoke();
            return;
        }

        // Se mide la forma final antes de tocar nada: es a lo que tiene que llegar la burbuja.
        (Window.GetWindow(body) as UIElement ?? body).UpdateLayout();
        var fullWidth = body.ActualWidth;
        var fullHeight = body.ActualHeight;

        if (fullWidth <= 0 || fullHeight <= 0)
        {
            body.Animate(UIElement.OpacityProperty, 1, SakuraMotion.Reveal, SakuraMotion.DecelerateCurve, completed: completed);
            return;
        }

        var scale = BeginMorph(body, content, anchor, new BubbleShape(fullWidth, fullHeight, body.CornerRadius.TopLeft));
        body.SetValue(ProgressProperty, 0d);
        body.Opacity = 0;
        scale.ScaleX = 0.5;
        scale.ScaleY = 0.5;

        if (content is not null)
        {
            content.Opacity = 0;
        }

        // 1. El círculo se infla.
        body.Animate(UIElement.OpacityProperty, 1, SakuraMotion.Fast, SakuraMotion.DecelerateCurve);
        scale.AnimateTransform(ScaleTransform.ScaleXProperty, 1, SakuraMotion.Emphasized, SakuraMotion.SpringCurve);
        scale.AnimateTransform(ScaleTransform.ScaleYProperty, 1, SakuraMotion.Emphasized, SakuraMotion.SpringCurve);

        // 2. Se estira hasta la píldora y, al llegar, tiembla una vez.
        var grow = SakuraMotion.CreateAnimation(1, GrowDuration, SakuraMotion.EmphasizedCurve, GrowDelay);
        grow.Completed += (_, _) =>
        {
            if (body.GetValue(MorphProperty) is null)
            {
                return;
            }

            EndMorph(body);
            Wobble(scale);
            completed?.Invoke();
        };
        body.BeginAnimation(ProgressProperty, grow);

        // 3. Lo de dentro, cuando ya hay sitio para leerlo.
        if (content is not null)
        {
            content.Animate(UIElement.OpacityProperty, 1, SakuraMotion.Reveal, SakuraMotion.DecelerateCurve, ContentDelay);
        }
    }

    /// <summary>La salida: se apaga el contenido, se recoge en círculo y el círculo se va.</summary>
    public static void Deflate(Border body, HorizontalAlignment anchor, Action completed)
    {
        var content = body.Child as FrameworkElement;
        StopAll(body, content);
        RestoreLayout(body);

        var fullWidth = body.ActualWidth;
        var fullHeight = body.ActualHeight;

        if (!SakuraMotion.AnimationsEnabled || fullWidth <= 0 || fullHeight <= 0)
        {
            body.Animate(UIElement.OpacityProperty, 0, SakuraMotion.Exit, SakuraMotion.AccelerateCurve, completed: () =>
            {
                RestoreLayout(body);
                completed();
            });
            return;
        }

        var scale = BeginMorph(body, content, anchor, new BubbleShape(fullWidth, fullHeight, body.CornerRadius.TopLeft));
        body.SetValue(ProgressProperty, 1d);

        content?.Animate(UIElement.OpacityProperty, 0, SakuraMotion.Fast, SakuraMotion.AccelerateCurve);

        body.BeginAnimation(
            ProgressProperty,
            SakuraMotion.CreateAnimation(0, ShrinkDuration, SakuraMotion.AccelerateCurve, TimeSpan.FromMilliseconds(40)));

        var vanishAt = TimeSpan.FromMilliseconds(40) + ShrinkDuration.TimeSpan - TimeSpan.FromMilliseconds(30);
        scale.AnimateTransform(ScaleTransform.ScaleXProperty, 0.55, SakuraMotion.Fast, SakuraMotion.AccelerateCurve, vanishAt);
        scale.AnimateTransform(ScaleTransform.ScaleYProperty, 0.55, SakuraMotion.Fast, SakuraMotion.AccelerateCurve, vanishAt);
        body.Animate(UIElement.OpacityProperty, 0, SakuraMotion.Fast, SakuraMotion.AccelerateCurve, vanishAt, () =>
        {
            RestoreLayout(body);
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            scale.ScaleX = 1;
            scale.ScaleY = 1;
            completed();
        });
    }

    private static ScaleTransform BeginMorph(Border body, FrameworkElement? content, HorizontalAlignment anchor, BubbleShape shape)
    {
        body.SetValue(MorphProperty, new MorphState(
            shape, body.HorizontalAlignment, body.VerticalAlignment, body.CornerRadius, content,
            content?.HorizontalAlignment ?? HorizontalAlignment.Stretch));

        if (content is not null)
        {
            content.Width = content.ActualWidth > 0 ? content.ActualWidth : double.NaN;
            content.HorizontalAlignment = HorizontalAlignment.Left;
        }

        body.HorizontalAlignment = anchor;
        body.VerticalAlignment = VerticalAlignment.Top;

        var scale = EnsureScale(body);
        body.RenderTransformOrigin = new Point(
            anchor switch { HorizontalAlignment.Left => 0, HorizontalAlignment.Right => 1, _ => 0.5 },
            0);
        return scale;
    }

    private static void EndMorph(Border body)
    {
        body.BeginAnimation(ProgressProperty, null);
        body.SetValue(ProgressProperty, 1d);
        RestoreLayout(body);
    }

    /// <summary>Devuelve el borde y su contenido a su tamaño y alineación normales.</summary>
    private static void RestoreLayout(Border body)
    {
        if (body.GetValue(MorphProperty) is not MorphState state)
        {
            return;
        }

        body.SetValue(MorphProperty, null);
        body.Width = double.NaN;
        body.Height = double.NaN;
        body.HorizontalAlignment = state.OriginalHorizontal;
        body.VerticalAlignment = state.OriginalVertical;
        body.CornerRadius = state.OriginalRadius;

        if (state.Content is not null)
        {
            state.Content.Width = double.NaN;
            state.Content.HorizontalAlignment = state.ContentHorizontal;
        }
    }

    private static void OnProgressChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is not Border body || body.GetValue(MorphProperty) is not MorphState state)
        {
            return;
        }

        var frame = state.Shape.At((double)e.NewValue);

        // El hueco puede encoger mientras la burbuja crece —la píldora de respuesta ajusta su alto al
        // texto en esos mismos milisegundos—, y un alto fijo mayor que el hueco dejaba el borde de
        // abajo cortado en recto, sin curva ni trazo. Se limita al sitio que hay en cada fotograma.
        var height = frame.Height;
        if (VisualTreeHelper.GetParent(body) is FrameworkElement { ActualHeight: > 0 } parent)
        {
            height = Math.Min(height, Math.Max(1, parent.ActualHeight - body.Margin.Top - body.Margin.Bottom));
        }

        body.Width = frame.Width;
        body.Height = height;
        body.CornerRadius = new CornerRadius(Math.Min(frame.Radius, height / 2));
    }

    /// <summary>Un temblor al llegar: se ensancha y aplasta un pelo y vuelve con un rebote.</summary>
    private static void Wobble(ScaleTransform scale)
    {
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        scale.ScaleX = 1.025;
        scale.ScaleY = 0.95;
        scale.AnimateTransform(ScaleTransform.ScaleXProperty, 1, SakuraMotion.Reveal, SakuraMotion.SpringCurve);
        scale.AnimateTransform(ScaleTransform.ScaleYProperty, 1, SakuraMotion.Reveal, SakuraMotion.SpringCurve);
    }

    /// <summary>
    /// Para lo que estuviera en marcha dejando cada valor donde está. Quitar una animación sin más
    /// devuelve la propiedad a su valor base —y el de la opacidad era el 0 de antes de aparecer—, así
    /// que la salida arrancaba con la píldora ya invisible y no se veía irse.
    /// </summary>
    private static void StopAll(Border body, FrameworkElement? content)
    {
        body.BeginAnimation(ProgressProperty, null);
        Hold(body, UIElement.OpacityProperty);

        if (content is not null)
        {
            Hold(content, UIElement.OpacityProperty);
        }

        if (body.RenderTransform is ScaleTransform scale && !scale.IsFrozen)
        {
            Hold(scale, ScaleTransform.ScaleXProperty);
            Hold(scale, ScaleTransform.ScaleYProperty);
        }
    }

    private static void Hold(IAnimatable target, DependencyProperty property)
    {
        var owner = (DependencyObject)target;
        var current = owner.GetValue(property);
        target.BeginAnimation(property, null);
        owner.SetValue(property, current);
    }

    private static ScaleTransform EnsureScale(UIElement element)
    {
        if (element.RenderTransform is ScaleTransform existing && !existing.IsFrozen)
        {
            return existing;
        }

        var scale = new ScaleTransform(1, 1);
        element.RenderTransform = scale;
        return scale;
    }
}
