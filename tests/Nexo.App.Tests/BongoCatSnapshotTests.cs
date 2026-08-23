using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Nexo.App.Views.Controls;
using Nexo.Core.Media;

namespace Nexo.App.Tests;

/// <summary>
/// Retrato del gato en sus dos poses, para poder mirarlo.
///
/// Un dibujo hecho a mano con curvas de Bézier no se puede juzgar leyendo los números: o se ve, o
/// se está adivinando.
/// </summary>
[Collection(StaWpfCollection.Name)]
public sealed class BongoCatSnapshotTests
{
    private readonly StaWpfFixture _wpf;

    public BongoCatSnapshotTests(StaWpfFixture wpf) => _wpf = wpf;

    [Fact]
    public void Renders_BothPoses()
    {
        var output = Path.Combine(
            Path.GetTempPath(), "claude", "C--Dev-Nexo", "bongo-cat.png");

        Directory.CreateDirectory(Path.GetDirectoryName(output)!);

        _wpf.Invoke(() =>
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };

            foreach (var pose in new[] { BongoPose.Raised, BongoPose.Struck })
            {
                row.Children.Add(new BongoCat
                {
                    Pose = pose,
                    Width = 260,
                    Height = 170,
                    Margin = new Thickness(12)
                });
            }

            var host = new Border
            {
                Background = (Brush)Application.Current.FindResource("BrushSurface"),
                Padding = new Thickness(16),
                Child = row
            };

            const double width = 592;
            const double height = 226;

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
