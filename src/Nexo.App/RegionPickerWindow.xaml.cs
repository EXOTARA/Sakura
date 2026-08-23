using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Nexo.App;

/// <summary>
/// Diseño D84 — el selector de zona: se arrastra un rectángulo y sale en píxeles de pantalla.
///
/// Lo único delicado aquí es el DPI, y tiene una solución de una línea que conviene no perder: el
/// rectángulo se convierte con <see cref="Visual.PointToScreen"/> sobre las dos esquinas, en vez de
/// multiplicar por una escala. Con varios monitores a distintos DPI —que es el caso de Adler— no
/// existe «la escala» de un rectángulo que los cruza; `PointToScreen` sí sabe contestar punto a
/// punto, y devuelve exactamente los píxeles físicos que espera `CopyFromScreen`.
/// </summary>
public partial class RegionPickerWindow : Window
{
    private Point _origin;
    private bool _dragging;
    private TaskCompletionSource<Int32Rect?>? _result;

    public RegionPickerWindow()
    {
        InitializeComponent();

        MouseLeftButtonDown += OnPressed;
        MouseMove += OnMoved;
        MouseLeftButtonUp += OnReleased;
        KeyDown += OnKeyDown;

        // Perder el foco cuenta como cancelar: un velo oscuro sobre toda la pantalla que se queda
        // puesto porque alguien hizo Alt+Tab es la peor manera de descubrir que existe.
        Deactivated += (_, _) => Finish(null);
    }

    /// <summary>El área más pequeña que se acepta, en píxeles de pantalla.</summary>
    private const int MinimumSide = 12;

    /// <summary>
    /// Enseña el velo y espera. Devuelve la zona en píxeles físicos, o <c>null</c> si se canceló o
    /// si lo seleccionado era tan pequeño que casi seguro fue un clic sin querer.
    /// </summary>
    public Task<Int32Rect?> PickAsync()
    {
        _result = new TaskCompletionSource<Int32Rect?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        _dragging = false;
        SelectionBorder.Visibility = Visibility.Collapsed;
        Hint.Visibility = Visibility.Visible;

        LayoutVeil(new Rect(0, 0, 0, 0));

        Show();
        Activate();
        Focus();

        return _result.Task;
    }

    private void OnPressed(object sender, MouseButtonEventArgs e)
    {
        _origin = e.GetPosition(Root);
        _dragging = true;
        Hint.Visibility = Visibility.Collapsed;
        SelectionBorder.Visibility = Visibility.Visible;
        Root.CaptureMouse();
        Update(_origin);
    }

    private void OnMoved(object sender, MouseEventArgs e)
    {
        if (_dragging)
        {
            Update(e.GetPosition(Root));
        }
    }

    private void OnReleased(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        Root.ReleaseMouseCapture();

        var area = Between(_origin, e.GetPosition(Root));

        // Las dos esquinas por separado: es lo que hace que esto funcione con monitores a distinto
        // DPI sin tener que saber en cuál de ellos cayó el arrastre.
        var topLeft = PointToScreen(area.TopLeft);
        var bottomRight = PointToScreen(area.BottomRight);

        var width = (int)Math.Round(bottomRight.X - topLeft.X);
        var height = (int)Math.Round(bottomRight.Y - topLeft.Y);

        Finish(width < MinimumSide || height < MinimumSide
            ? null
            : new Int32Rect((int)Math.Round(topLeft.X), (int)Math.Round(topLeft.Y), width, height));
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Finish(null);
        }
    }

    private void Update(Point current)
    {
        var area = Between(_origin, current);

        Canvas.SetLeft(SelectionBorder, area.X);
        Canvas.SetTop(SelectionBorder, area.Y);
        SelectionBorder.Width = area.Width;
        SelectionBorder.Height = area.Height;

        LayoutVeil(area);
    }

    /// <summary>Los cuatro trozos de velo que rodean el hueco.</summary>
    private void LayoutVeil(Rect hole)
    {
        var width = ActualWidth > 0 ? ActualWidth : Width;
        var height = ActualHeight > 0 ? ActualHeight : Height;

        Place(VeilTop, 0, 0, width, hole.Y);
        Place(VeilBottom, 0, hole.Bottom, width, Math.Max(0, height - hole.Bottom));
        Place(VeilLeft, 0, hole.Y, hole.X, hole.Height);
        Place(VeilRight, hole.Right, hole.Y, Math.Max(0, width - hole.Right), hole.Height);

        static void Place(System.Windows.Shapes.Rectangle piece, double x, double y, double w, double h)
        {
            Canvas.SetLeft(piece, x);
            Canvas.SetTop(piece, y);
            piece.Width = Math.Max(0, w);
            piece.Height = Math.Max(0, h);
        }
    }

    private static Rect Between(Point a, Point b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    private void Finish(Int32Rect? area)
    {
        var pending = _result;
        _result = null;

        if (_dragging)
        {
            _dragging = false;
            Root.ReleaseMouseCapture();
        }

        Hide();
        pending?.TrySetResult(area);
    }
}
