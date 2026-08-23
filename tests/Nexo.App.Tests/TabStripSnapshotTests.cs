using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Nexo.App.Views;
using Nexo.App.Views.Controls;

namespace Nexo.App.Tests;

/// <summary>
/// Retrato de la tira de pestañas, para poder MIRARLA.
///
/// El manual de este repositorio dice que lo que no se ha visto en pantalla no está terminado, y
/// esta tira se está comparando contra una referencia visual. Abrir Sakura entera en el equipo de
/// Adler para ver una franja de 120 píxeles es interrumpirle la sesión; renderizarla aparte no.
///
/// No afirma nada sobre el aspecto — no es una prueba de regresión visual, que exigiría una imagen
/// de referencia versionada y fallaría con cada retoque legítimo. Solo comprueba que se dibuja algo
/// y deja el PNG en TEMP para que alguien lo abra.
/// </summary>
[Collection(StaWpfCollection.Name)]
public sealed class TabStripSnapshotTests
{
    private readonly StaWpfFixture _wpf;

    public TabStripSnapshotTests(StaWpfFixture wpf) => _wpf = wpf;

    [Fact]
    public void Renders_ToAnImageSomeoneCanLookAt()
    {
        const double width = 880;
        const double height = 96;

        var output = Path.Combine(
            Path.GetTempPath(), "claude", "C--Dev-Nexo", "tabstrip.png");

        Directory.CreateDirectory(Path.GetDirectoryName(output)!);

        _wpf.Invoke(() =>
        {
            var strip = new DashboardTabStrip();

            // El fondo real de la ventana debajo, para juzgar contraste y no un rectángulo blanco.
            var host = new Border
            {
                Background = (Brush)Application.Current.FindResource("BrushSurface"),
                Padding = new Thickness(18, 12, 18, 0),
                Width = width,
                Height = height,
                Child = strip
            };

            // Las pestañas ANTES de medir. Al revés, Select(0) coloca el subrayado cuando los
            // botones aún miden cero, se rinde —correctamente, para no entrar en el bucle de
            // Dispatcher que documenta DashboardTabStripTests— y sin un SizeChanged posterior el
            // subrayado se queda invisible. En la aplicación no pasa porque Loaded llega después.
            strip.SetTabs(
            [
                new DashboardTabDefinition("Panel", Application.Current.FindResource("IconTabPanel") as Geometry),
                new DashboardTabDefinition("Media", Application.Current.FindResource("IconTabMedia") as Geometry),
                new DashboardTabDefinition("Rendimiento", Application.Current.FindResource("IconTabPerformance") as Geometry)
            ]);

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
        Assert.True(new FileInfo(output).Length > 1_000, "El PNG salió vacío; no se dibujó nada.");
    }

    [Fact]
    public void Panel_RendersToAnImageSomeoneCanLookAt()
    {
        // El panel entero, para juzgar la retícula y no una pieza suelta. El ancho es el que tiene
        // de verdad la ventana del panel; el alto, holgado, para que nada quede recortado.
        const double width = 920;
        const double height = 560;

        var output = Path.Combine(
            Path.GetTempPath(), "claude", "C--Dev-Nexo", "panel.png");

        Directory.CreateDirectory(Path.GetDirectoryName(output)!);

        _wpf.Invoke(() =>
        {
            var host = new Border
            {
                Background = (Brush)Application.Current.FindResource("BrushBackground"),
                Width = width,
                Height = height,
                Child = new DashboardView()
            };

            host.Measure(new Size(width, height));
            host.Arrange(new Rect(0, 0, width, height));
            host.UpdateLayout();

            var bitmap = new RenderTargetBitmap(
                (int)(width * 1.5), (int)(height * 1.5), 144, 144, PixelFormats.Pbgra32);
            bitmap.Render(host);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            using var stream = File.Create(output);
            encoder.Save(stream);
        });

        Assert.True(File.Exists(output));
        Assert.True(new FileInfo(output).Length > 1_000, "El PNG salió vacío; no se dibujó nada.");
    }
}
