using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Nexo.App.Views.Controls;

/// <summary>
/// Desvanece el contenido de un <see cref="ScrollViewer"/> en el borde por el que queda más por ver,
/// en vez de cortarlo en seco.
///
/// Dentro de las tarjetas redondeadas del shell, lo que se desplaza chocaba con una línea recta arriba
/// y abajo: los bordes de las secciones de Personalizar aparecían cortados a media esquina. Adler lo
/// señaló con capturas (2026-09-14). Un desvanecido corto resuelve las dos cosas a la vez: no hay línea
/// que se vea, y dice sin palabras que hay más contenido en esa dirección.
///
/// El desvanecido va sobre el presentador del contenido y no sobre el ScrollViewer entero, para que la
/// barra de desplazamiento no se desvanezca con él. Solo aparece en el lado donde hay algo oculto: arriba
/// del todo no hay nada que insinuar por arriba.
/// </summary>
public static class ScrollEdgeFade
{
    /// <summary>Alto del desvanecido, en píxeles independientes del dispositivo.</summary>
    public const double FadeLength = 18;

    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(ScrollEdgeFade),
        new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    // Lo último que se pintó. ScrollChanged llega decenas de veces por segundo al arrastrar; la
    // máscara solo cambia cuando cambia qué bordes esconden algo o el alto disponible.
    private static readonly DependencyProperty LastStateProperty = DependencyProperty.RegisterAttached(
        "LastState",
        typeof(string),
        typeof(ScrollEdgeFade),
        new PropertyMetadata(null));

    private static void OnIsEnabledChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is not ScrollViewer viewer)
        {
            return;
        }

        if ((bool)args.NewValue)
        {
            viewer.Loaded += OnChanged;
            viewer.ScrollChanged += OnChanged;
            viewer.SizeChanged += OnChanged;
            Update(viewer);
        }
        else
        {
            viewer.Loaded -= OnChanged;
            viewer.ScrollChanged -= OnChanged;
            viewer.SizeChanged -= OnChanged;
            if (FindPresenter(viewer) is { } presenter)
            {
                presenter.OpacityMask = null;
            }
        }
    }

    private static void OnChanged(object sender, EventArgs args) => Update((ScrollViewer)sender);

    private static void Update(ScrollViewer viewer)
    {
        if (FindPresenter(viewer) is not { } presenter)
        {
            return;
        }

        var height = presenter.ActualHeight;
        var hiddenAbove = viewer.VerticalOffset > 0.5;
        var hiddenBelow = viewer.VerticalOffset < viewer.ScrollableHeight - 0.5;

        var state = $"{hiddenAbove}|{hiddenBelow}|{Math.Round(height)}";
        if (Equals(viewer.GetValue(LastStateProperty), state))
        {
            return;
        }

        viewer.SetValue(LastStateProperty, state);

        if (height <= FadeLength * 2 || (!hiddenAbove && !hiddenBelow))
        {
            presenter.OpacityMask = null;
            return;
        }

        var fade = FadeLength / height;
        var mask = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        mask.GradientStops.Add(new GradientStop(hiddenAbove ? Colors.Transparent : Colors.Black, 0));
        mask.GradientStops.Add(new GradientStop(Colors.Black, fade));
        mask.GradientStops.Add(new GradientStop(Colors.Black, 1 - fade));
        mask.GradientStops.Add(new GradientStop(hiddenBelow ? Colors.Transparent : Colors.Black, 1));
        mask.Freeze();
        presenter.OpacityMask = mask;
    }

    private static ScrollContentPresenter? FindPresenter(ScrollViewer viewer)
    {
        viewer.ApplyTemplate();
        return viewer.Template?.FindName("PART_ScrollContentPresenter", viewer) as ScrollContentPresenter;
    }
}
