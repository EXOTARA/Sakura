using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Nexo.App.Motion;

namespace Nexo.App.Views.Controls;

/// <summary>
/// Diseño D26 — una sección de ajustes que empieza cerrada y se abre cuando alguien la pide.
///
/// Ajustes era un único <see cref="StackPanel"/> con quince apartados uno detrás de otro: todo a la
/// vista siempre, sin jerarquía, y la única forma de encontrar algo era desplazarse leyéndolo entero.
/// El problema no era la cantidad de opciones sino que todas pesaban lo mismo. Aquí lo que se ve de
/// entrada es la lista corta de apartados con lo que hay dentro de cada uno, y el detalle aparece
/// solo donde se pide.
///
/// La altura se anima midiendo el contenido y volviendo a <c>Auto</c> al terminar. Animar
/// directamente hacia <c>Auto</c> no es posible en WPF —no es un número—, y dejar una altura fija
/// después de la animación congelaría la sección: no crecería al escribir en un cuadro de texto ni
/// al cambiar el tamaño de la ventana.
/// </summary>
public sealed class SettingsSection : HeaderedContentControl
{
    private const string ContentHostPart = "PART_ContentHost";
    private const string ChevronPart = "PART_ChevronRotation";

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description),
        typeof(string),
        typeof(SettingsSection),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
        nameof(IsOpen),
        typeof(bool),
        typeof(SettingsSection),
        new PropertyMetadata(false, OnIsOpenChanged));

    private FrameworkElement? _contentHost;
    private RotateTransform? _chevronRotation;

    static SettingsSection()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(SettingsSection),
            new FrameworkPropertyMetadata(typeof(SettingsSection)));
    }

    /// <summary>La línea que resume qué hay dentro, para no tener que abrir la sección para saberlo.</summary>
    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _contentHost = GetTemplateChild(ContentHostPart) as FrameworkElement;
        _chevronRotation = GetTemplateChild(ChevronPart) as RotateTransform;

        ApplyState(animate: false);
    }

    private static void OnIsOpenChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        ((SettingsSection)sender).ApplyState(animate: true);
    }

    private void ApplyState(bool animate)
    {
        if (_contentHost is null)
        {
            return;
        }

        if (_chevronRotation is not null)
        {
            _chevronRotation.AnimateTransform(
                RotateTransform.AngleProperty,
                IsOpen ? 90 : 0,
                animate ? SakuraMotion.Base : SakuraMotion.Fast,
                SakuraMotion.EmphasizedCurve);
        }

        if (IsOpen)
        {
            Open(animate);
        }
        else
        {
            Close(animate);
        }
    }

    private void Open(bool animate)
    {
        var host = _contentHost!;
        host.Visibility = Visibility.Visible;

        if (!animate || !SakuraMotion.AnimationsEnabled)
        {
            host.BeginAnimation(HeightProperty, null);
            host.Height = double.NaN;
            host.Opacity = 1;
            return;
        }

        host.Animate(OpacityProperty, 1, SakuraMotion.Reveal, SakuraMotion.DecelerateCurve);
        StaggerContent();
        host.Animate(
            HeightProperty,
            MeasureContentHeight(host),
            SakuraMotion.Emphasized,
            SakuraMotion.EmphasizedCurve,
            completed: () =>
            {
                // Se devuelve a Auto en cuanto termina: una altura fija dejaría la sección
                // incapaz de crecer si su contenido cambia después.
                host.BeginAnimation(HeightProperty, null);
                host.Height = double.NaN;
            });
    }

    /// <summary>
    /// 2026-09-14 — al abrirse, las opciones de dentro llegan una detrás de otra en vez de a la vez,
    /// como el resto de Sakura. Solo las primeras: en una sección larga, esperar a la vigésima opción
    /// sería hacer esperar a alguien que ya sabe lo que busca.
    /// </summary>
    private void StaggerContent()
    {
        if (Content is not Panel panel)
        {
            return;
        }

        var index = 0;
        foreach (var child in panel.Children.OfType<FrameworkElement>().Where(child => child.Visibility == Visibility.Visible).Take(10))
        {
            EntranceMotion.Rise(child, TimeSpan.FromMilliseconds(40) + SakuraMotion.StaggerAt(index++), offset: 6);
        }
    }

    private void Close(bool animate)
    {
        var host = _contentHost!;

        if (!animate || !SakuraMotion.AnimationsEnabled)
        {
            host.BeginAnimation(HeightProperty, null);
            host.Height = 0;
            host.Opacity = 0;
            host.Visibility = Visibility.Collapsed;
            return;
        }

        var current = host.ActualHeight;
        host.BeginAnimation(HeightProperty, null);
        host.Height = current;

        host.Animate(OpacityProperty, 0, SakuraMotion.Exit, SakuraMotion.AccelerateCurve);
        host.Animate(
            HeightProperty,
            0,
            SakuraMotion.Exit,
            SakuraMotion.AccelerateCurve,
            completed: () =>
            {
                // Collapsed y no Hidden: una sección cerrada no debe seguir participando en el
                // recorrido del tabulador ni ser anunciada por el lector de pantalla.
                host.Visibility = Visibility.Collapsed;
            });
    }

    private double MeasureContentHeight(FrameworkElement host)
    {
        var availableWidth = ActualWidth > 0 ? ActualWidth : double.PositiveInfinity;

        host.BeginAnimation(HeightProperty, null);
        host.Height = double.NaN;
        host.Measure(new Size(availableWidth, double.PositiveInfinity));

        var measured = host.DesiredSize.Height;
        host.Height = 0;

        return measured;
    }
}
