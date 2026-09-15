using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Nexo.App.Motion;

namespace Nexo.App;

/// <summary>Lo que se decidió en la vista previa.</summary>
public enum CapturePreviewChoice
{
    Discard,
    Ask,
    Save,
    Copy
}

public partial class VisionPreviewWindow : Window
{
    public VisionPreviewWindow(string sourceTitle, byte[] pngBytes, bool offerKeep = false)
    {
        InitializeComponent();
        if (offerKeep)
        {
            // Una captura hecha para quedársela: lo principal es guardarla o copiarla, y preguntar a
            // Sakura es una opción más. Nada se envía hasta pulsar «Preguntar a Sakura».
            Title = "Captura de Sakura";
            KeepActions.Visibility = Visibility.Visible;
            UseButton.Content = "Preguntar a Sakura";
            FooterNote.Text = "No se guarda ni se envía nada hasta que elijas qué hacer.";
        }

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

    public CapturePreviewChoice Choice { get; private set; } = CapturePreviewChoice.Discard;

    private void UseButton_Click(object sender, RoutedEventArgs e)
    {
        Choice = CapturePreviewChoice.Ask;
        DialogResult = true;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        Choice = CapturePreviewChoice.Save;
        DialogResult = true;
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        Choice = CapturePreviewChoice.Copy;
        DialogResult = true;
    }

    private void DiscardButton_Click(object sender, RoutedEventArgs e)
    {
        Choice = CapturePreviewChoice.Discard;
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
