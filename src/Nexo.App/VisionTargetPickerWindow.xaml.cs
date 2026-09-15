using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Nexo.App.Motion;
using Nexo.Core.Vision;

namespace Nexo.App;

public partial class VisionTargetPickerWindow : Window
{
    public VisionTargetPickerWindow(
        IReadOnlyList<VisionCaptureTarget> targets,
        long preferredWindowHandle = 0)
    {
        InitializeComponent();
        ContentRendered += (_, _) =>
        {
            FocusSelectedTarget();
            PlayEntrance();
        };
        TargetsList.ItemsSource = targets;

        var preferred = targets.FirstOrDefault(target =>
            target.Kind == VisionCaptureKind.Window &&
            target.NativeHandle == preferredWindowHandle);
        TargetsList.SelectedItem = preferred ?? targets.FirstOrDefault();
    }

    private void Window_SourceInitialized(object? sender, EventArgs e) =>
        Shell.SakuraWindowChrome.MatchCaptionToTheme(this);

    /// <summary>El foco en la fila elegida y no en la lista: así las flechas empiezan desde ella.</summary>
    private void FocusSelectedTarget()
    {
        TargetsList.UpdateLayout();
        if (TargetsList.SelectedItem is not null &&
            TargetsList.ItemContainerGenerator.ContainerFromItem(TargetsList.SelectedItem) is ListBoxItem item)
        {
            item.Focus();
            return;
        }

        TargetsList.Focus();
    }

    /// <summary>Las ventanas llegan una detrás de otra, como las tarjetas del panel.</summary>
    private void PlayEntrance()
    {
        EntranceMotion.Rise(HeaderPanel, TimeSpan.Zero, offset: 6);
        for (var i = 0; i < Math.Min(8, TargetsList.Items.Count); i++)
        {
            if (TargetsList.ItemContainerGenerator.ContainerFromIndex(i) is FrameworkElement row)
            {
                EntranceMotion.Rise(row, SakuraMotion.StaggerAt(i + 1), offset: 8);
            }
        }
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
                "Lens",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }
}
