using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Nexo.App.Motion;

namespace Nexo.App;

public partial class VisionPreviewWindow : Window
{
    public VisionPreviewWindow(string sourceTitle, byte[] pngBytes)
    {
        InitializeComponent();
        ContentRendered += (_, _) =>
        {
            DiscardButton.Focus();
            EntranceMotion.Rise(HeaderPanel, TimeSpan.Zero, offset: 6);
            EntranceMotion.Pop(ImageCard, SakuraMotion.StaggerAt(1), from: 0.96);
        };
        SourceTitleText.Text = sourceTitle;
        PreviewImage.Source = LoadBitmap(pngBytes);
    }

    private void Window_SourceInitialized(object? sender, EventArgs e) =>
        Shell.SakuraWindowChrome.MatchCaptionToTheme(this);

    private void UseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void DiscardButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private static BitmapImage LoadBitmap(byte[] data)
    {
        using var stream = new MemoryStream(data, writable: false);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
