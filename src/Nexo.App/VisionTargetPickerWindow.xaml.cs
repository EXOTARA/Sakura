using System.Windows;
using System.Windows.Input;
using Nexo.Core.Vision;

namespace Nexo.App;

public partial class VisionTargetPickerWindow : Window
{
    public VisionTargetPickerWindow(
        IReadOnlyList<VisionCaptureTarget> targets,
        long preferredWindowHandle = 0)
    {
        InitializeComponent();
        ContentRendered += (_, _) => TargetsList.Focus();
        TargetsList.ItemsSource = targets;

        var preferred = targets.FirstOrDefault(target =>
            target.Kind == VisionCaptureKind.Window &&
            target.NativeHandle == preferredWindowHandle);
        TargetsList.SelectedItem = preferred ?? targets.FirstOrDefault();
    }

    public VisionCaptureTarget? SelectedTarget =>
        TargetsList.SelectedItem as VisionCaptureTarget;

    private void CaptureButton_Click(object sender, RoutedEventArgs e)
    {
        AcceptSelection();
    }

    private void TargetsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        AcceptSelection();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void AcceptSelection()
    {
        if (SelectedTarget is null)
        {
            MessageBox.Show(
                this,
                "Selecciona una ventana o un monitor.",
                "Sakura Vision",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }
}
