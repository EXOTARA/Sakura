using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Nexo.App.Motion;
using Nexo.Core.Tasks;

namespace Nexo.App;

/// <summary>2026-09-16 — captura rápida global (Alt+Shift+N).</summary>
public partial class QuickCaptureWindow : Window
{
    private bool _closing;

    public QuickCaptureWindow()
    {
        InitializeComponent();
        Deactivated += (_, _) => Dismiss();
    }

    /// <summary>Se apuntó una línea; quien tiene el TaskManager la guarda.</summary>
    public event EventHandler<QuickCaptureResult>? Captured;

    public void ShowAtTop()
    {
        _closing = false;
        LineTextBox.Clear();
        var area = SystemParameters.WorkArea;
        Left = area.Left + (area.Width - Width) / 2;
        Top = area.Top + area.Height * 0.18;

        Show();
        Activate();
        LineTextBox.Focus();
        Keyboard.Focus(LineTextBox);

        if (SakuraMotion.AnimationsEnabled)
        {
            Card.Opacity = 0;
            CardScale.ScaleX = CardScale.ScaleY = 0.96;
            Card.Animate(OpacityProperty, 1, SakuraMotion.Reveal, SakuraMotion.DecelerateCurve);
            CardScale.AnimateTransform(ScaleTransform.ScaleXProperty, 1, SakuraMotion.Emphasized, SakuraMotion.SpringCurve);
            CardScale.AnimateTransform(ScaleTransform.ScaleYProperty, 1, SakuraMotion.Emphasized, SakuraMotion.SpringCurve);
        }
    }

    private void LineTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        Placeholder.Visibility = LineTextBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (string.IsNullOrWhiteSpace(LineTextBox.Text))
        {
            PreviewText.Text = "Enter guarda · Esc cierra";
            return;
        }

        var now = DateTimeOffset.Now;
        var parsed = QuickCapture.Parse(LineTextBox.Text, now, DateOnly.FromDateTime(now.Date));
        var when = parsed.DueAt is null ? "Sin fecha" : TodayPlan.Describe(new NexoTask { DueAt = parsed.DueAt }, now);
        PreviewText.Text = $"↵  {parsed.Title} · {when}" + (parsed.Priority == TaskPriority.High ? " · Importante" : string.Empty);
    }

    private void LineTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Dismiss();
            return;
        }

        if (e.Key != Key.Enter || string.IsNullOrWhiteSpace(LineTextBox.Text))
        {
            return;
        }

        e.Handled = true;
        var now = DateTimeOffset.Now;
        Captured?.Invoke(this, QuickCapture.Parse(LineTextBox.Text, now, DateOnly.FromDateTime(now.Date)));
        Dismiss();
    }

    private void Dismiss()
    {
        if (_closing || !IsVisible)
        {
            return;
        }

        _closing = true;
        if (!SakuraMotion.AnimationsEnabled)
        {
            Hide();
            return;
        }

        Card.Animate(OpacityProperty, 0, SakuraMotion.Exit, SakuraMotion.AccelerateCurve, completed: Hide);
    }
}
