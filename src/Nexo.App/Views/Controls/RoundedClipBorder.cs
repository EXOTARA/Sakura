using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Nexo.App.Views.Controls;

/// <summary>
/// Un borde que recorta de verdad lo que lleva dentro a sus esquinas redondeadas.
///
/// <c>ClipToBounds</c> recorta al rectángulo e ignora el radio (manual de interfaz, §3.5), así que una
/// imagen dentro de un <c>Border</c> redondeado asoma con las esquinas en pico. Aquí el recorte se
/// recalcula con el tamaño real cada vez que cambia.
///
/// Se usa para el hueco de imagen del Panel: Adler pidió (2026-09-14) que cualquier imagen o GIF quede
/// «como en un cuadro invisible» con las mismas esquinas que la tarjeta de música, en vez de un
/// rectángulo suelto.
/// </summary>
public sealed class RoundedClipBorder : Border
{
    protected override Size ArrangeOverride(Size finalSize)
    {
        var arranged = base.ArrangeOverride(finalSize);
        UpdateClip(arranged);
        return arranged;
    }

    private void UpdateClip(Size size)
    {
        if (size.Width <= 0 || size.Height <= 0)
        {
            Clip = null;
            return;
        }

        // Un solo radio para las cuatro esquinas: el de arriba a la izquierda. Con radios distintos por
        // esquina haría falta una PathGeometry, y ningún uso actual lo necesita.
        var radius = Math.Min(CornerRadius.TopLeft, Math.Min(size.Width, size.Height) / 2);
        var clip = new RectangleGeometry(new Rect(size), radius, radius);
        clip.Freeze();
        Clip = clip;
    }
}
