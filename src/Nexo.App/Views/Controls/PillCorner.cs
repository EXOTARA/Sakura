using System.Windows;
using System.Windows.Controls;

namespace Nexo.App.Views.Controls;

/// <summary>
/// Diseño D64 — mantiene el radio de un borde en la mitad de su alto, que es lo único que produce
/// una píldora de verdad.
///
/// Existía un token `RadiusFull` con valor 999 y un comentario que decía que WPF recorta el radio al
/// que quepa. **No lo hace.** Con un radio mayor que la mitad del alto, `Border` dibuja los arcos
/// pedidos igualmente y el resultado son dos puntas: la forma de lente que Adler señaló en los
/// botones "Izquierda" y "Derecha". Renderizado y comparado lado a lado: 999 da puntas, la mitad
/// del alto da la píldora.
///
/// Se resuelve midiendo en vez de adivinando un número. Un token no puede saber el alto del control
/// al que se aplica, y ese alto cambia entre un botón de 40 y una insignia de 22.
/// </summary>
public static class PillCorner
{
    public static readonly DependencyProperty IsPillProperty = DependencyProperty.RegisterAttached(
        "IsPill",
        typeof(bool),
        typeof(PillCorner),
        new PropertyMetadata(false, OnIsPillChanged));

    public static void SetIsPill(DependencyObject element, bool value) =>
        element?.SetValue(IsPillProperty, value);

    public static bool GetIsPill(DependencyObject element) =>
        element is null ? false : (bool)element.GetValue(IsPillProperty);

    /// <summary>
    /// Diseño D79 — hasta dónde puede crecer el radio.
    ///
    /// La mitad del alto es lo correcto mientras el borde tiene el alto de una píldora. Pero hay
    /// superficies que crecen con su contenido —la píldora de respuesta crece con la respuesta— y
    /// ahí la mitad del alto deja de ser una píldora y pasa a ser un óvalo, con el texto metido
    /// dentro de la curva.
    ///
    /// Con un tope, la forma es una píldora de verdad al tamaño que tiene normalmente, y al crecer
    /// se hace más alta en vez de más redonda. Sin tope —el valor por omisión— se comporta como
    /// siempre, que es lo que necesitan los botones y las insignias, de alto fijo.
    /// </summary>
    public static readonly DependencyProperty MaxRadiusProperty = DependencyProperty.RegisterAttached(
        "MaxRadius",
        typeof(double),
        typeof(PillCorner),
        new PropertyMetadata(double.PositiveInfinity, OnMaxRadiusChanged));

    public static void SetMaxRadius(DependencyObject element, double value) =>
        element?.SetValue(MaxRadiusProperty, value);

    public static double GetMaxRadius(DependencyObject element) =>
        element is null ? double.PositiveInfinity : (double)element.GetValue(MaxRadiusProperty);

    private static void OnMaxRadiusChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is Border border && GetIsPill(border))
        {
            Apply(border);
        }
    }

    private static void OnIsPillChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not Border border)
        {
            return;
        }

        if (e.NewValue is true)
        {
            border.SizeChanged += OnSizeChanged;
            Apply(border);
            return;
        }

        border.SizeChanged -= OnSizeChanged;
    }

    private static void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is Border border)
        {
            Apply(border);
        }
    }

    private static void Apply(Border border)
    {
        // Antes de la primera medida el alto es cero y el radio también: no se fuerza nada, porque
        // SizeChanged va a llegar en cuanto la haya.
        var radius = Math.Min(border.ActualHeight / 2, GetMaxRadius(border));
        if (radius > 0)
        {
            border.CornerRadius = new CornerRadius(radius);
        }
    }
}
