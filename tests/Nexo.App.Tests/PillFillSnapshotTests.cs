using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Nexo.App;

namespace Nexo.App.Tests;

/// <summary>
/// Retrato del relleno de la píldora a varios niveles.
///
/// Reproduce EXACTAMENTE la geometría que construye QuickControlsWindow —carril de 52x164 con
/// recorte redondeado de radio 26, relleno anclado abajo con el mismo radio— para poder mirar qué
/// dibuja WPF cuando el relleno es más bajo que su propio diámetro de esquina.
///
/// Existe porque el manual de este repositorio afirma que WPF NO recorta el radio al que quepa, y
/// de esa afirmación depende si el relleno bajo se ve como una píldora o como una punta. Una
/// afirmación así se comprueba, no se hereda.
/// </summary>
[Collection(StaWpfCollection.Name)]
public sealed class PillFillSnapshotTests
{
    private const double TrackWidth = 52;
    private const double TrackHeight = 164;

    private readonly StaWpfFixture _wpf;

    public PillFillSnapshotTests(StaWpfFixture wpf) => _wpf = wpf;

    [Fact]
    public void Renders_TheFillAtSeveralLevels()
    {
        double[] levels = [4, 12, 26, 52, 90, 164];

        var output = Path.Combine(
            Path.GetTempPath(), "claude", "C--Dev-Nexo", "pill-fill.png");

        Directory.CreateDirectory(Path.GetDirectoryName(output)!);

        _wpf.Invoke(() =>
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };

            foreach (var level in levels)
            {
                var fill = new Border
                {
                    Width = TrackWidth,
                    Height = level,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    CornerRadius = new CornerRadius(TrackWidth / 2, TrackWidth / 2, 0, 0),
                    Background = (Brush)Application.Current.FindResource("BrushAccent")
                };

                var track = new Border
                {
                    Width = TrackWidth,
                    Height = TrackHeight,
                    Margin = new Thickness(14, 0, 14, 0),
                    CornerRadius = new CornerRadius(26),
                    Background = (Brush)Application.Current.FindResource("BrushSurfaceRaised"),
                    Clip = new RectangleGeometry(
                        new Rect(0, 0, TrackWidth, TrackHeight),
                        TrackWidth / 2,
                        TrackWidth / 2),
                    Child = fill
                };

                row.Children.Add(track);
            }

            var host = new Border
            {
                Background = (Brush)Application.Current.FindResource("BrushBackground"),
                Padding = new Thickness(10),
                Child = row
            };

            var width = (TrackWidth + 28) * levels.Length + 20;
            const double height = TrackHeight + 20;

            host.Measure(new Size(width, height));
            host.Arrange(new Rect(0, 0, width, height));
            host.UpdateLayout();

            var bitmap = new RenderTargetBitmap(
                (int)(width * 3), (int)(height * 3), 288, 288, PixelFormats.Pbgra32);
            bitmap.Render(host);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            using var stream = File.Create(output);
            encoder.Save(stream);
        });

        Assert.True(File.Exists(output));
        Assert.True(new FileInfo(output).Length > 1_000, "El PNG salió vacío; no se dibujó nada.");
    }

    [Fact]
    public void Renders_TheAnswerPill_AtItsCompactAndGrownSizes()
    {
        // La ventana de verdad, sin mostrarla: se construye, se mide y se dibuja su contenido. Así
        // se ve la forma real —con su margen de sombra, su relleno y su radio— y no una copia de la
        // geometría escrita aquí, que es exactamente lo que no sirve para juzgar una forma.
        double[] heights = [132, 220];

        var output = Path.Combine(
            Path.GetTempPath(), "claude", "C--Dev-Nexo", "answer-pill.png");

        Directory.CreateDirectory(Path.GetDirectoryName(output)!);

        _wpf.Invoke(() =>
        {
            var column = new StackPanel { Orientation = Orientation.Vertical };

            foreach (var height in heights)
            {
                var window = new AnswerPillWindow();
                var content = (FrameworkElement)window.Content;
                window.Content = null;

                var holder = new Border
                {
                    Width = 480,
                    Height = height,
                    Margin = new Thickness(0, 0, 0, 8),
                    Child = content
                };

                column.Children.Add(holder);

                // La ventana la deja invisible su propia animación de entrada; aquí se quiere ver.
                content.Opacity = 1;
                content.RenderTransform = Transform.Identity;
            }

            var host = new Border
            {
                Background = (Brush)Application.Current.FindResource("BrushBackground"),
                Child = column
            };

            const double width = 480;
            var total = heights.Sum() + 16;

            host.Measure(new Size(width, total));
            host.Arrange(new Rect(0, 0, width, total));
            host.UpdateLayout();

            var bitmap = new RenderTargetBitmap(
                (int)(width * 2), (int)(total * 2), 192, 192, PixelFormats.Pbgra32);
            bitmap.Render(host);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            using var stream = File.Create(output);
            encoder.Save(stream);
        });

        Assert.True(File.Exists(output));
    }
}
