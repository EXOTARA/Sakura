using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Nexo.App.Motion;
using Xunit;

namespace Nexo.App.Tests.Motion;

/// <summary>
/// Auditoría del 2026-09-14: las entradas escalonadas dejaban la opacidad sujeta a su valor final y
/// tapaban los cambios posteriores. Un panel de Personalizar que se atenúa al desactivar su opción
/// dejaba de atenuarse después de abrir la sección animada.
/// </summary>
[Collection(StaWpfCollection.Name)]
public sealed class EntranceMotionTests(StaWpfFixture wpf)
{
    private static void Pump(TimeSpan wait)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = wait };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    [Fact]
    public void AfterRising_TheElementKeepsItsOwnOpacity_EvenIfTheCodeChangedItMeanwhile()
    {
        wpf.Invoke(() =>
        {
            var animations = SakuraMotion.AnimationsEnabled;
            SakuraMotion.AnimationsEnabled = true;
            var window = new Window { Width = 200, Height = 200, Left = -3000, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
            var panel = new Border { Width = 100, Height = 40, Opacity = 0.55 };
            window.Content = panel;

            try
            {
                window.Show();
                EntranceMotion.Rise(panel, TimeSpan.Zero);

                // La opción se vuelve a activar mientras entra: el panel tiene que acabar entero.
                panel.Opacity = 1;
                Pump(SakuraMotion.Reveal.TimeSpan + TimeSpan.FromMilliseconds(400));
                Assert.Equal(1, panel.Opacity, 3);

                // Y después de entrar, atenuarlo tiene que verse.
                panel.Opacity = 0.55;
                Assert.Equal(0.55, panel.Opacity, 3);
            }
            finally
            {
                window.Close();
                SakuraMotion.AnimationsEnabled = animations;
            }
        });
    }

    [Fact]
    public void WithAnimationsOff_NothingIsTouched()
    {
        wpf.Invoke(() =>
        {
            var animations = SakuraMotion.AnimationsEnabled;
            SakuraMotion.AnimationsEnabled = false;
            try
            {
                var panel = new Border { Opacity = 0.55 };
                EntranceMotion.Pop(panel, TimeSpan.Zero);
                Assert.Equal(0.55, panel.Opacity, 3);
            }
            finally
            {
                SakuraMotion.AnimationsEnabled = animations;
            }
        });
    }
}
