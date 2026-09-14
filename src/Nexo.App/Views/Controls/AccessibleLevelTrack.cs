using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Nexo.App.Views.Controls;

/// <summary>
/// Conserva el dibujo y el arrastre del mando, pero expone su valor al lector y al teclado.
/// Un Border normal no tiene patrón de rango y solo se podía operar apuntando con el ratón.
/// </summary>
public sealed class AccessibleLevelTrack : Border
{
    public double Value { get; private set; }
    public event Action<double>? ValueRequested;

    public AccessibleLevelTrack() => Focusable = true;

    public void UpdateValue(double value)
    {
        var previous = Value;
        Value = Math.Clamp(value, 0, 100);
        if (UIElementAutomationPeer.FromElement(this) is { } peer && previous != Value)
            peer.RaisePropertyChangedEvent(RangeValuePatternIdentifiers.ValueProperty, previous, Value);
    }

    public void RequestValue(double value)
    {
        if (!IsEnabled) throw new ElementNotEnabledException();
        if (!double.IsFinite(value) || value < 0 || value > 100) throw new ArgumentOutOfRangeException(nameof(value));
        UpdateValue(value);
        ValueRequested?.Invoke(value);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var value = e.Key switch
        {
            Key.Up or Key.Right => Math.Min(100, Value + 5),
            Key.Down or Key.Left => Math.Max(0, Value - 5),
            Key.PageUp => Math.Min(100, Value + 10),
            Key.PageDown => Math.Max(0, Value - 10),
            Key.Home => 0,
            Key.End => 100,
            _ => double.NaN
        };
        if (double.IsNaN(value)) return;
        RequestValue(value);
        e.Handled = true;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new LevelPeer(this);

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        InvalidateVisual();
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        // El Border no trae el indicador de foco de un Slider; sin él no se sabe qué mando cambia.
        if (IsKeyboardFocused && ActualWidth > 4 && ActualHeight > 4)
            dc.DrawRoundedRectangle(null, new Pen((Brush)FindResource("BrushTextPrimary"), 2),
                new Rect(2, 2, ActualWidth - 4, ActualHeight - 4), ActualWidth / 2, ActualWidth / 2);
    }

    private sealed class LevelPeer(AccessibleLevelTrack owner) : FrameworkElementAutomationPeer(owner), IRangeValueProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;
        protected override string GetClassNameCore() => nameof(AccessibleLevelTrack);
        public override object? GetPattern(PatternInterface pattern) => pattern == PatternInterface.RangeValue ? this : base.GetPattern(pattern);
        public bool IsReadOnly => false;
        public double LargeChange => 10;
        public double SmallChange => 5;
        public double Maximum => 100;
        public double Minimum => 0;
        public double Value => owner.Value;
        public void SetValue(double value) => owner.RequestValue(value);
    }
}
