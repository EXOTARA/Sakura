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
    private int _closeGeneration;

    public QuickCaptureWindow()
    {
        InitializeComponent();
        Deactivated += (_, _) => Dismiss();
    }

    /// <summary>Se apuntó una línea; quien tiene el TaskManager la guarda.</summary>
    public event EventHandler<QuickCaptureResult>? Captured;

    public void ShowAtTop()
    {
        // La salida solo atenúa la tarjeta: a mitad de salida la escala ya vale 1 y no se devuelve a 0,96.
        var resumingFromDismiss = _closing;
        _closing = false;
        LineTextBox.Clear();
        var area = SystemParameters.WorkArea;
        Left = area.Left + (area.Width - Width) / 2;
        Top = area.Top + area.Height * 0.18;

        Show();
        Activate();
        LineTextBox.Focus();
        Keyboard.Focus(LineTextBox);

        // «Entrar desde» (auditoría de movimiento, 2026-10): antes se escribía el punto de partida a
        // mano, y una animación terminada se queda sujetando su valor final, así que la escala solo
        // se veía la primera vez; y con «sin animaciones» activado después, la opacidad retenida en 0
        // dejaba la tarjeta invisible. EnterTo suelta lo retenido y decide desde dónde sale.
        Card.EnterTo(OpacityProperty, 0, 1, SakuraMotion.Reveal, SakuraMotion.DecelerateCurve);
        CardScale.EnterTo(ScaleTransform.ScaleXProperty, resumingFromDismiss ? 1 : 0.96, 1, SakuraMotion.Emphasized, SakuraMotion.SpringCurve);
        CardScale.EnterTo(ScaleTransform.ScaleYProperty, resumingFromDismiss ? 1 : 0.96, 1, SakuraMotion.Emphasized, SakuraMotion.SpringCurve);
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
        var generation = ++_closeGeneration;
        if (!SakuraMotion.AnimationsEnabled)
        {
            Hide();
            _closing = false;
            return;
        }

        Card.Animate(
            OpacityProperty,
            0,
            SakuraMotion.Exit,
            SakuraMotion.AccelerateCurve,
            completed: () =>
            {
                // Si se reabrió a mitad de salida (o se cerró otra vez), esta ya no es la salida
                // vigente: el Completed de una animación reemplazada se dispara igual.
                if (_closing && generation == _closeGeneration)
                {
                    Hide();

                    // Salida terminada: la próxima apertura es una apertura limpia, con su inflado
                    // desde 0,96. Si _closing se quedaba en true, ShowAtTop la tomaba por una
                    // reapertura a mitad de salida y la escala arrancaba ya en 1, sin gesto.
                    _closing = false;
                }
            });
    }
}
