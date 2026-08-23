using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Nexo.App.Tests;

/// <summary>
/// Qué hace WPF exactamente con un radio que no cabe en la caja.
///
/// El manual del repositorio afirma una cosa y una lectura apresurada de otro retrato sugirió la
/// contraria, así que se mide en vez de discutirlo. Dibuja bordes con radios imposibles al lado de
/// los radios correctos, y además comprueba si <c>CornerRadius</c> conserva el valor pedido — que es
/// la parte que se puede afirmar sin mirar píxeles.
/// </summary>
[Collection(StaWpfCollection.Name)]
public sealed class CornerRadiusClampingTests
{
    private readonly StaWpfFixture _wpf;

    public CornerRadiusClampingTests(StaWpfFixture wpf) => _wpf = wpf;

    [Fact]
    public void ThePropertyKeepsWhateverItWasGiven_EvenIfItCannotFit()
    {
        // Esto es lo importante para cualquiera que lea el valor desde código: WPF NO corrige la
        // propiedad. Lo que se guarda es lo que se pidió, quepa o no. Cualquier recorte que haya
        // ocurre al dibujar, y no se puede consultar.
        _wpf.Invoke(() =>
        {
            var border = new Border
            {
                Width = 200,
                Height = 40,
                CornerRadius = new CornerRadius(999)
            };

            border.Measure(new Size(200, 40));
            border.Arrange(new Rect(0, 0, 200, 40));

            Assert.Equal(999, border.CornerRadius.TopLeft);
        });
    }

    [Fact]
    public void Renders_ImpossibleRadiiNextToCorrectOnes()
    {
        var output = Path.Combine(
            Path.GetTempPath(), "claude", "C--Dev-Nexo", "corner-clamping.png");

        Directory.CreateDirectory(Path.GetDirectoryName(output)!);

        _wpf.Invoke(() =>
        {
            var column = new StackPanel { Orientation = Orientation.Vertical };

            void Add(double width, double height, double radius, string _)
            {
                column.Children.Add(new Border
                {
                    Width = width,
                    Height = height,
                    Margin = new Thickness(0, 0, 0, 14),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    CornerRadius = new CornerRadius(radius),
                    Background = (Brush)Application.Current.FindResource("BrushAccent")
                });
            }

            Add(200, 40, 999, "ancho, radio imposible");
            Add(200, 40, 20, "ancho, radio correcto");
            Add(52, 12, 26, "estrecho y bajo, radio imposible");
            Add(52, 12, 6, "estrecho y bajo, radio correcto");
            Add(420, 72, 999, "pildora ancha, radio imposible");
            Add(420, 72, 36, "pildora ancha, radio correcto");

            var host = new Border
            {
                Background = (Brush)Application.Current.FindResource("BrushBackground"),
                Padding = new Thickness(16),
                Child = column
            };

            const double width = 460;
            const double height = 330;

            host.Measure(new Size(width, height));
            host.Arrange(new Rect(0, 0, width, height));
            host.UpdateLayout();

            var bitmap = new RenderTargetBitmap(
                (int)(width * 2), (int)(height * 2), 192, 192, PixelFormats.Pbgra32);
            bitmap.Render(host);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            using var stream = File.Create(output);
            encoder.Save(stream);
        });

        Assert.True(File.Exists(output));
    }
}
