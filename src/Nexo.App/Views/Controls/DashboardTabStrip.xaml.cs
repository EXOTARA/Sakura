using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Nexo.App.Motion;

namespace Nexo.App.Views.Controls;

/// <summary>Una pestaña de la tira superior: su texto y su icono.</summary>
public sealed record DashboardTabDefinition(string Label, Geometry? Icon);

/// <summary>
/// Diseño D43 — el panel de arriba: la tira de pestañas de la referencia.
///
/// El subrayado es una sola pieza que se desliza, no uno por pestaña que se enciende y se apaga.
/// Con uno por pestaña el cambio se ve como un parpadeo en dos sitios a la vez; con uno que viaja,
/// la vista sigue el movimiento y sabe de dónde a dónde ha ido sin leer nada.
///
/// El ancho y la posición del subrayado se miden del texto, no de la celda: la celda es igual para
/// todas y una raya de ese ancho debajo de «Media» se saldría por los dos lados de la palabra.
/// </summary>
public partial class DashboardTabStrip : UserControl
{
    private readonly List<Button> _buttons = [];
    private readonly List<TextBlock> _labels = [];
    private int _selectedIndex = -1;

    public DashboardTabStrip()
    {
        InitializeComponent();

        // El subrayado se recoloca cuando la tira se mide, y solo entonces. Ambos avisos hacen
        // falta: Loaded para la primera vez y SizeChanged para cuando la vista pasa de oculta a
        // visible, que es cuando de verdad adquiere ancho.
        Loaded += (_, _) => MoveIndicator(animate: false);
        SizeChanged += (_, _) => MoveIndicator(animate: false);
    }

    /// <summary>Se dispara solo cuando la pestaña cambia de verdad, no al volver a pulsar la activa.</summary>
    public event EventHandler<int>? SelectionChanged;

    public int SelectedIndex => _selectedIndex;

    public void SetTabs(IReadOnlyList<DashboardTabDefinition> tabs)
    {
        ArgumentNullException.ThrowIfNull(tabs);

        TabsPanel.Children.Clear();
        _buttons.Clear();
        _labels.Clear();
        _selectedIndex = -1;

        for (var i = 0; i < tabs.Count; i++)
        {
            var index = i;
            var tab = tabs[i];

            // Diseño D75 — medidas tomadas de la referencia, no elegidas a ojo: el icono manda y el
            // texto va pegado debajo. Antes el icono era más pequeño que en la referencia y estaba
            // más lejos de su palabra, y el conjunto se leía como dos cosas sueltas en vez de una.
            var icon = new Path
            {
                Width = 21,
                Height = 21,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                StrokeThickness = 1.8,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Data = tab.Icon
            };

            var label = new TextBlock
            {
                Text = tab.Label,
                Margin = new Thickness(0, 5, 0, 0),
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var content = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            content.Children.Add(icon);
            content.Children.Add(label);

            var button = new Button
            {
                Style = (Style)FindResource("DashboardTabButtonStyle"),
                Content = content,
                Tag = index
            };

            AutomationProperties.SetName(button, tab.Label);
            button.Click += (_, _) => Select(index);

            TabsPanel.Children.Add(button);
            _buttons.Add(button);
            _labels.Add(label);
        }

        if (_buttons.Count > 0)
        {
            Select(0, notify: false);
        }
    }

    public void Select(int index) => Select(index, notify: true);

    private void Select(int index, bool notify)
    {
        if (index < 0 || index >= _buttons.Count || index == _selectedIndex)
        {
            return;
        }

        var first = _selectedIndex < 0;
        _selectedIndex = index;

        for (var i = 0; i < _buttons.Count; i++)
        {
            var active = i == index;
            var brush = (Brush)FindResource(active ? "BrushAccent" : "BrushTextSecondary");

            _labels[i].Foreground = brush;

            // Medium y no SemiBold: en la referencia la pestaña activa se distingue por el color y
            // el subrayado, no por engordar. Una negrita fuerte además ensancha la palabra, y como
            // el subrayado se mide del texto, el subrayado daba un salto al cambiar de pestaña.
            _labels[i].FontWeight = active ? FontWeights.Medium : FontWeights.Normal;

            if (_buttons[i].Content is StackPanel { Children: [Path icon, ..] })
            {
                icon.Stroke = brush;
            }
        }

        // La primera colocación no se anima: el subrayado aparecería viajando desde el borde
        // izquierdo cada vez que se entra en la vista.
        MoveIndicator(animate: !first);

        if (notify)
        {
            SelectionChanged?.Invoke(this, index);
        }
    }

    private void MoveIndicator(bool animate)
    {
        if (_selectedIndex < 0 || _selectedIndex >= _buttons.Count)
        {
            return;
        }

        var button = _buttons[_selectedIndex];
        var label = _labels[_selectedIndex];

        if (button.ActualWidth <= 0 || label.ActualWidth <= 0)
        {
            // Todavía no se ha medido y aquí no hay nada que colocar. No se reintenta desde dentro:
            // la vista Sistema se construye al arrancar aunque no se abra, así que la tira pasa
            // largos ratos con ancho cero, y volver a encolarse a sí misma en ese estado es un
            // bucle infinito de despachador que se come un tercio de un núcleo y deja la ventana
            // pastosa sin que nada parezca estar pasando. La colocación llega con Loaded y con
            // SizeChanged, que es exactamente cuando ya se puede medir.
            return;
        }

        var cellWidth = ActualWidth / _buttons.Count;

        // Un pelo más ancho que la palabra. Exactamente del ancho del texto, el subrayado parece
        // que se queda corto: las letras no llegan a los bordes de su propia caja.
        var width = Math.Max(label.ActualWidth + 8, 28);
        var target = (cellWidth * _selectedIndex) + ((cellWidth - width) / 2);

        Indicator.Width = width;

        if (animate)
        {
            IndicatorTranslate.AnimateTransform(
                TranslateTransform.XProperty,
                target,
                SakuraMotion.Emphasized,
                SakuraMotion.EmphasizedCurve);
            Indicator.Animate(OpacityProperty, 1, SakuraMotion.Fast, SakuraMotion.DecelerateCurve);
        }
        else
        {
            IndicatorTranslate.BeginAnimation(TranslateTransform.XProperty, null);
            IndicatorTranslate.X = target;
            Indicator.BeginAnimation(OpacityProperty, null);
            Indicator.Opacity = 1;
        }
    }
}
