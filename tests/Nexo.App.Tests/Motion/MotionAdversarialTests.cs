using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Nexo.App.Motion;
using Nexo.App.Shell;
using Nexo.Core.Commands.CommandCenter;
using Nexo.Core.Settings;
using Xunit;

namespace Nexo.App.Tests.Motion;

/// <summary>Pruebas del tester adversario sobre la auditoría de movimiento (0.30.38).</summary>
[Collection(StaWpfCollection.Name)]
public sealed class MotionAdversarialTests(StaWpfFixture wpf) : IDisposable
{
    private readonly string _dataRoot = Path.Combine(Path.GetTempPath(), "sakura-adv-" + Guid.NewGuid().ToString("N"));
    private readonly string? _previousRoot = Environment.GetEnvironmentVariable("SAKURA_DATA_ROOT");

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SAKURA_DATA_ROOT", _previousRoot);
        try { Directory.Delete(_dataRoot, true); } catch (IOException) { }
    }

    private static void Pump(int ms)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private void UseIsolatedData() => Environment.SetEnvironmentVariable("SAKURA_DATA_ROOT", _dataRoot);

    private static int Subscribers(Type type, string eventName)
    {
        var field = type.GetField(eventName, BindingFlags.Static | BindingFlags.NonPublic)!;
        return (field.GetValue(null) as Delegate)?.GetInvocationList().Length ?? 0;
    }

    private void WithMotion(bool enabled, Action body) => wpf.Invoke(() =>
    {
        var original = SakuraMotion.AnimationsEnabled;
        SakuraMotion.AnimationsEnabled = enabled;
        try { body(); }
        finally { SakuraMotion.AnimationsEnabled = original; }
    });

    private static Window OffscreenOwner()
    {
        var owner = new Window { Width = 300, Height = 200, Left = -4000, ShowInTaskbar = false, WindowStyle = WindowStyle.None, ShowActivated = false };
        owner.Show();
        return owner;
    }

    // ---- 1. abrir/cerrar/abrir muy rápido ----

    [Fact]
    public void PaletteRapidOpenCloseCyclesEndOpenAndVisible()
    {
        UseIsolatedData();
        WithMotion(true, () =>
        {
            var palette = new CommandPaletteWindow();
            try
            {
                for (var i = 0; i < 4; i++)
                {
                    palette.ShowPalette();
                    Pump(40);
                    palette.HidePalette();
                    Pump(40);
                }

                palette.ShowPalette();
                Pump(900);

                var root = (Border)palette.FindName("RootBorder")!;
                var translate = (TranslateTransform)palette.FindName("PaletteTranslate")!;
                var scale = (ScaleTransform)palette.FindName("PaletteScale")!;
                Assert.True(palette.IsVisible, "la paleta debía quedar mostrada");
                Assert.Equal(1, root.Opacity, 2);
                Assert.Equal(0, translate.Y, 2);
                Assert.Equal(1, scale.ScaleX, 2);
                Assert.Equal(1, scale.ScaleY, 2);
            }
            finally { palette.Close(); }
        });
    }

    [Fact]
    public void PaletteHideThenQuickReopenDoesNotGetHiddenByTheOldHideAnimation()
    {
        UseIsolatedData();
        WithMotion(true, () =>
        {
            var palette = new CommandPaletteWindow();
            try
            {
                palette.ShowPalette();
                Pump(300);
                palette.HidePalette();
                Pump(60); // la salida dura 118 ms: se reabre a mitad
                palette.ShowPalette();
                Pump(1200);
                Assert.True(palette.IsVisible, "la Completed de la salida reemplazada ocultó la paleta recién reabierta");
            }
            finally { palette.Close(); }
        });
    }

    [Fact]
    public void QuickControlsRapidShowDismissShowEndsInPlaceAndClickable()
    {
        WithMotion(true, () =>
        {
            var window = new QuickControlsWindow();
            try
            {
                var dismiss = typeof(QuickControlsWindow).GetMethod("Dismiss", BindingFlags.Instance | BindingFlags.NonPublic)!;
                for (var i = 0; i < 4; i++)
                {
                    window.ShowControls(SidebarPosition.Right, 50, true, 50, true);
                    Pump(30);
                    dismiss.Invoke(window, null);
                    Pump(30);
                }

                window.ShowControls(SidebarPosition.Right, 50, true, 50, true);
                Pump(900);

                var panel = (FrameworkElement)window.FindName("PanelBorder")!;
                var translate = (TranslateTransform)window.FindName("PanelTranslate")!;
                Assert.True(window.IsVisible);
                Assert.True(window.IsHitTestVisible);
                Assert.Equal(1, panel.Opacity, 2);
                Assert.Equal(0, translate.X, 2);
            }
            finally { window.HideImmediately(); window.Close(); }
        });
    }

    [Fact]
    public void DashboardRapidRevealDismissRevealEndsInPlaceAndClickable()
    {
        UseIsolatedData();
        WithMotion(true, () =>
        {
            var window = new DashboardWindow();
            try
            {
                var area = new Nexo.Windows.Shell.TopRevealArea(0, 0, 1600);
                for (var i = 0; i < 4; i++)
                {
                    window.Reveal(area);
                    Pump(40);
                    window.Dismiss();
                    Pump(40);
                }

                window.Reveal(area);

                // 600 ms: la entrada dura 450, y el cajón se recoge solo a los ~770 ms si el cursor
                // real no está dentro (vigilante del ratón), lo que con 1000 ms lo hacía intermitente.
                Pump(600);

                var panel = (FrameworkElement)window.FindName("PanelBorder")!;
                var translate = (TranslateTransform)window.FindName("PanelTranslate")!;
                var scale = (ScaleTransform)window.FindName("PanelScale")!;
                Assert.True(window.IsVisible);
                Assert.True(window.IsHitTestVisible);
                Assert.Equal(1, panel.Opacity, 2);
                Assert.Equal(0, translate.Y, 2);
                Assert.Equal(1, scale.ScaleY, 2);
            }
            finally { window.HideImmediately(); window.Close(); }
        });
    }


    [Fact]
    public void QuickControlsReopenedMidExitStaysVisible()
    {
        WithMotion(true, () =>
        {
            var window = new QuickControlsWindow();
            try
            {
                var dismiss = typeof(QuickControlsWindow).GetMethod("Dismiss", BindingFlags.Instance | BindingFlags.NonPublic)!;
                window.ShowControls(SidebarPosition.Right, 50, true, 50, true);
                Pump(400);
                dismiss.Invoke(window, null);
                Pump(40);
                window.ShowControls(SidebarPosition.Right, 50, true, 50, true);
                Pump(900);
                Assert.True(window.IsVisible, "la Completed de la salida reemplazada ocultó el panel recién reabierto");
            }
            finally { window.HideImmediately(); window.Close(); }
        });
    }

    [Fact]
    public void CommandCenterReopenedMidExitStaysVisible()
    {
        WithMotion(true, () =>
        {
            var owner = OffscreenOwner();
            var cc = new CommandCenterWindow(new SakuraCommandRegistry());
            try
            {
                var close = typeof(CommandCenterWindow).GetMethod("CloseAndRestoreFocus", BindingFlags.Instance | BindingFlags.NonPublic)!;
                cc.ShowFor(owner, null);
                Pump(400);
                close.Invoke(cc, null);
                Pump(40);
                cc.ShowFor(owner, null);
                Pump(900);
                Assert.True(cc.IsVisible);
                Assert.True(cc.IsHitTestVisible);
                Assert.Equal(1, ((FrameworkElement)cc.FindName("Surface")!).Opacity, 2);
            }
            finally { cc.Close(); owner.Close(); }
        });
    }

    [Fact]
    public void QuickCaptureReopenedMidExitStaysVisible()
    {
        WithMotion(true, () =>
        {
            var window = new QuickCaptureWindow();
            try
            {
                var dismiss = typeof(QuickCaptureWindow).GetMethod("Dismiss", BindingFlags.Instance | BindingFlags.NonPublic)!;
                window.ShowAtTop();
                Pump(400);
                dismiss.Invoke(window, null);
                Pump(40);
                window.ShowAtTop();
                Pump(900);
                Assert.True(window.IsVisible);
                Assert.Equal(1, ((FrameworkElement)window.FindName("Card")!).Opacity, 2);
            }
            finally { window.Close(); }
        });
    }

    // ---- 2. animaciones apagadas a mitad ----

    [Fact]
    public void PaletteTurningAnimationsOffMidEntranceStillEndsInPlace_AndReopensFine()
    {
        UseIsolatedData();
        WithMotion(true, () =>
        {
            var palette = new CommandPaletteWindow();
            try
            {
                palette.ShowPalette();
                Pump(30);
                SakuraMotion.AnimationsEnabled = false;
                Pump(500);
                var root = (Border)palette.FindName("RootBorder")!;
                Assert.Equal(1, root.Opacity, 2);

                palette.HidePalette();
                Assert.False(palette.IsVisible);
                Pump(80);
                palette.ShowPalette();
                Pump(100);
                Assert.True(palette.IsVisible);
                Assert.Equal(1, root.Opacity, 2);

                SakuraMotion.AnimationsEnabled = true;
                palette.HidePalette(immediate: true);
                Pump(80);
                palette.ShowPalette();
                Pump(900);
                Assert.Equal(1, root.Opacity, 2);
            }
            finally { palette.Close(); }
        });
    }

    [Fact]
    public void CommandCenterTurningAnimationsOffWhileClosingStillHides()
    {
        WithMotion(true, () =>
        {
            var owner = OffscreenOwner();
            var cc = new CommandCenterWindow(new SakuraCommandRegistry());
            try
            {
                cc.ShowFor(owner, null);
                Pump(400);
                var close = typeof(CommandCenterWindow).GetMethod("CloseAndRestoreFocus", BindingFlags.Instance | BindingFlags.NonPublic)!;
                close.Invoke(cc, null);
                Pump(20);
                SakuraMotion.AnimationsEnabled = false;
                Pump(600);
                Assert.False(cc.IsVisible, "se quedó mostrada tras apagar animaciones a mitad de salida");
            }
            finally { cc.Close(); owner.Close(); }
        });
    }

    // ---- CommandCenter: Enter durante la salida ----

    [Fact]
    public void CommandCenterEnterWhileClosingDoesNotExecuteTheCommandTwice()
    {
        WithMotion(true, () =>
        {
            var owner = OffscreenOwner();
            var executed = 0;
            var registry = new SakuraCommandRegistry();
            registry.Register(new SakuraCommandDescriptor(
                "a", "Abrir tarea", "d", SakuraCommandCategory.Navigation,
                _ => { executed++; return Task.FromResult(CommandExecutionResult.Success()); }));
            var cc = new CommandCenterWindow(registry);
            try
            {
                cc.ShowFor(owner, null);
                cc.UpdateLayout();
                Pump(300);
                for (var i = 0; i < 2; i++)
                {
                    cc.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(cc)!, 0, Key.Enter)
                    { RoutedEvent = Keyboard.PreviewKeyDownEvent });
                    Pump(20);
                }

                Pump(500);
                Assert.Equal(1, executed);
            }
            finally { cc.Close(); owner.Close(); }
        });
    }

    // ---- 3. fugas ----

    [Fact]
    public void ClosingWindowsReleasesEveryStaticEventSubscription()
    {
        UseIsolatedData();
        WithMotion(true, () =>
        {
            var motionBefore = Subscribers(typeof(SakuraMotion), "AnimationsEnabledChanged");
            var appearanceBefore = Subscribers(typeof(SakuraWindowChrome), "AppearanceChanged");

            for (var i = 0; i < 3; i++)
            {
                var palette = new CommandPaletteWindow();
                palette.ShowPalette();
                palette.Close();

                var owner = OffscreenOwner();
                var cc = new CommandCenterWindow(new SakuraCommandRegistry());
                cc.ShowFor(owner, null);
                cc.Close();
                owner.Close();

                var dash = new DashboardWindow();
                dash.Reveal(new Nexo.Windows.Shell.TopRevealArea(0, 0, 1600));
                dash.HideImmediately();
                dash.Close();

                var rec = new RecordingIndicatorWindow();
                rec.ShowOn(new Rect(0, 0, 800, 600));
                rec.Close();

                var peek = new PeekWindow();
                peek.Close();
            }

            Pump(200);
            Assert.Equal(motionBefore, Subscribers(typeof(SakuraMotion), "AnimationsEnabledChanged"));
            Assert.Equal(appearanceBefore, Subscribers(typeof(SakuraWindowChrome), "AppearanceChanged"));
        });
    }

    [Fact]
    public void LoaderAndViewsUnsubscribeWhenTheirWindowCloses()
    {
        WithMotion(true, () =>
        {
            var before = Subscribers(typeof(SakuraMotion), "AnimationsEnabledChanged");
            for (var i = 0; i < 3; i++)
            {
                var loader = new Nexo.App.Views.Controls.SakuraLoader();
                var capture = new Nexo.App.Views.CaptureView();
                var dashboardView = new Nexo.App.Views.DashboardView();
                var panel = new StackPanel();
                panel.Children.Add(loader);
                panel.Children.Add(capture);
                panel.Children.Add(dashboardView);
                var window = new Window { Width = 400, Height = 400, Left = -4000, ShowInTaskbar = false, WindowStyle = WindowStyle.None, ShowActivated = false, Content = panel };
                window.Show();
                Pump(50);
                Assert.True(Subscribers(typeof(SakuraMotion), "AnimationsEnabledChanged") > before);
                window.Close();
                Pump(50);
            }

            Assert.Equal(before, Subscribers(typeof(SakuraMotion), "AnimationsEnabledChanged"));
        });
    }
}

[Collection(StaWpfCollection.Name)]
public sealed class ReplacedAnimationCompletedTests(StaWpfFixture wpf)
{
    [Fact]
    public void CompletedOfAnAnimationReplacedByEnterToDoesNotFire()
    {
        wpf.Invoke(() =>
        {
            var original = SakuraMotion.AnimationsEnabled;
            SakuraMotion.AnimationsEnabled = true;
            var t = new TranslateTransform();
            var w = new Window { Width = 100, Height = 100, Left = -3000, ShowInTaskbar = false, WindowStyle = WindowStyle.None, Content = new Border { RenderTransform = t } };
            w.Show();
            try
            {
                var fired = 0;
                t.AnimateTransform(TranslateTransform.XProperty, 50, new Duration(TimeSpan.FromMilliseconds(300)), new CubicEase(), completed: () => fired++);
                var end = DateTime.UtcNow.AddMilliseconds(50);
                while (DateTime.UtcNow < end) { w.Dispatcher.Invoke(() => { }, DispatcherPriority.Background); }
                t.EnterTo(TranslateTransform.XProperty, 20, 0, new Duration(TimeSpan.FromMilliseconds(300)), new CubicEase());
                var frame = new DispatcherFrame();
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
                timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
                timer.Start();
                Dispatcher.PushFrame(frame);
                Assert.Equal(0, fired);
            }
            finally { w.Close(); SakuraMotion.AnimationsEnabled = original; }
        });
    }
}
