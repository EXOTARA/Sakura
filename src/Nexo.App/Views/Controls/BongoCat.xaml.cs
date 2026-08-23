using System.Windows;
using System.Windows.Controls;
using Nexo.Core.Media;

namespace Nexo.App.Views.Controls;

/// <summary>
/// Diseño D83 — el gato que acompaña a la música en el hueco de abajo del panel.
///
/// No decide nada: recibe la pose que <see cref="BongoBeatPolicy"/> ya resolvió a partir del
/// espectro. Cuándo es un golpe es una cuestión de ritmo, se puede probar sin abrir una ventana, y
/// por eso vive en Core; aquí solo se enseñan unas patas u otras.
/// </summary>
public partial class BongoCat : UserControl
{
    public BongoCat() => InitializeComponent();

    public static readonly DependencyProperty PoseProperty = DependencyProperty.Register(
        nameof(Pose),
        typeof(BongoPose),
        typeof(BongoCat),
        new PropertyMetadata(BongoPose.Raised, OnPoseChanged));

    public BongoPose Pose
    {
        get => (BongoPose)GetValue(PoseProperty);
        set => SetValue(PoseProperty, value);
    }

    private static void OnPoseChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is BongoCat cat)
        {
            cat.ApplyPose((BongoPose)e.NewValue);
        }
    }

    private void ApplyPose(BongoPose pose)
    {
        var struck = pose == BongoPose.Struck;

        // Sin animación a propósito: el golpe es instantáneo. Una transición de aunque sean ochenta
        // milisegundos llega tarde al compás siguiente y el gato se queda a medio camino entre las
        // dos poses, que es donde deja de leerse como un golpe.
        PawsRaised.Visibility = struck ? Visibility.Collapsed : Visibility.Visible;
        PawsStruck.Visibility = struck ? Visibility.Visible : Visibility.Collapsed;
    }
}
