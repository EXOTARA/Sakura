using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Nexo.App.Views;
using Nexo.App.Views.Controls;
using Nexo.Core.Ai;
using Nexo.Core.Vision;

namespace Nexo.App.Tests;

[Collection(StaWpfCollection.Name)]
public sealed class PopupAccessibilityTests(StaWpfFixture wpf)
{
    [Fact]
    public void PopupMarkup_LoadsWithRealWpfResources_AndHistoryRowsHaveHumanNames()
    {
        wpf.Invoke(() =>
        {
            // Construir sin Show evita lanzar capturas, diagnósticos o servicios reales.
            var history = new AmbientHistoryWindow();
            var windows = new Window[] { history, new AnswerPillWindow(), new CapsuleWindow(),
                new DashboardWindow(), new PeekWindow(), new QuickControlsWindow(),
                new RegionPickerWindow(), new SakuraPillWindow(), new VisionTargetPickerWindow([]) };
            try
            {
                history.Apply([new Ambient.AmbientRequestHistoryItem(Guid.NewGuid(), "Abrir editor", "Listo", "Ahora", "Abierto", false, false)]);
                var rows = (ItemsControl)history.FindName("HistoryItemsControl");
                Assert.Equal("Abrir editor", rows.Items[0].ToString());
                foreach (var window in windows)
                {
                    var content = Assert.IsAssignableFrom<FrameworkElement>(window.Content);
                    content.Measure(new Size(1100, 800));
                    content.Arrange(new Rect(0, 0, 1100, 800));
                    content.UpdateLayout();
                    foreach (var button in Descendants(content).OfType<Button>())
                        Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(button)),
                            $"{window.GetType().Name}: {button.Name}");
                }
            }
            finally
            {
                foreach (var window in windows) window.Close();
            }
        });
    }

    [Fact]
    public void LevelTrack_ExposesWritableRangeAndUsesTheSameActionAsKeyboard()
    {
        wpf.Invoke(() =>
        {
            var track = new AccessibleLevelTrack();
            AutomationProperties.SetName(track, "Volumen");
            var peer = UIElementAutomationPeer.CreatePeerForElement(track);
            var range = Assert.IsAssignableFrom<IRangeValueProvider>(peer.GetPattern(PatternInterface.RangeValue));
            double requested = -1;
            track.ValueRequested += value => requested = value;
            range.SetValue(65);
            Assert.Equal(65, requested);
            Assert.Equal(65, range.Value);
            Assert.Equal("Volumen", peer.GetName());
            Assert.Equal(AutomationControlType.Slider, peer.GetAutomationControlType());
            using var source = new System.Windows.Interop.HwndSource(new System.Windows.Interop.HwndSourceParameters("Prueba de teclado")
                { Width = 1, Height = 1, WindowStyle = 0 });
            track.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Home) { RoutedEvent = Keyboard.KeyDownEvent });
            Assert.Equal(0, requested);
            Assert.Throws<ArgumentOutOfRangeException>(() => range.SetValue(101));
            track.IsEnabled = false;
            Assert.Throws<ElementNotEnabledException>(() => range.SetValue(50));
        });
    }

    [Fact]
    public void ListRows_AnnounceTheirVisibleText()
    {
        Assert.Equal("Calculadora", new CommandPaletteSuggestion("Calculadora", "calc", "Abrir", "", "", []).ToString());
        Assert.Equal("modelo", new OllamaModelInfo("modelo", 1024, null).ToString());
        Assert.Equal("Editor", new VisionCaptureTarget("editor", 0, "Editor", "Ventana", VisionCaptureKind.Window, 0, 0, 100, 100).ToString());
        wpf.Invoke(() =>
        {
            Assert.Equal("Micrófono: Listo", new DashboardView.AccessibleVoiceRow("Micrófono", "Listo", Brushes.White, Visibility.Collapsed).ToString());
            Assert.Equal("Volumen: baja Spotify al 50, silencia Discord", new DashboardView.VoiceExampleRow("Volumen", ["baja Spotify al 50", "silencia Discord"]).ToString());
            Assert.Equal("Conexión", new DiagnosticsWindow.DiagnosticItemRow("Conexión", "Lista", Brushes.White).ToString());
            Assert.Null(UIElementAutomationPeer.CreatePeerForElement(new DecorativeMark()));
            Assert.Null(UIElementAutomationPeer.CreatePeerForElement(new DecorativeText()));
        });
    }

    [Fact]
    public void KeyboardRegion_CannotLeaveTheDesktopOrCollapseBelowMinimum()
    {
        var bounds = new Size(1920, 1080);
        var area = new Rect(0, 0, 100, 100);
        Assert.Equal(area, RegionPickerWindow.AdjustKeyboardArea(area, bounds, Key.Left, false, 10));
        Assert.Equal(new Rect(10, 0, 100, 100), RegionPickerWindow.AdjustKeyboardArea(area, bounds, Key.Right, false, 10));
        Assert.Equal(12, RegionPickerWindow.AdjustKeyboardArea(area, bounds, Key.Left, true, 1000).Width);
        Assert.Equal(1920, RegionPickerWindow.AdjustKeyboardArea(area, bounds, Key.Right, true, 3000).Width);
    }

    [Fact]
    public void TextTokens_AndActualComboSelection_HaveReadableContrast()
    {
        wpf.Invoke(() =>
        {
            // Otras pruebas cambian el acento global. Aquí se comprueba Colors.xaml, no su residuo.
            var colors = new ResourceDictionary { Source = new Uri("/Sakura;component/Themes/Colors.xaml", UriKind.Relative) };
            Color ColorOf(string key) => ((SolidColorBrush)colors[key]).Color;
            foreach (var foreground in new[] { "BrushTextPrimary", "BrushTextSecondary", "BrushTextTertiary" })
            foreach (var background in new[] { "BrushBackground", "BrushInput", "BrushSurface", "BrushSurfaceRaised", "BrushSurfaceHover", "BrushAccentSoft" })
                Assert.True(Contrast(ColorOf(foreground), ColorOf(background)) >= 4.5, $"{foreground} sobre {background}");

            var combo = new ComboBox { Resources = colors, Style = (Style)Application.Current.FindResource(typeof(ComboBox)), Width = 280, ItemsSource = new[] { "Modelo seleccionado" }, SelectedIndex = 0 };
            combo.Measure(new Size(280, 60));
            combo.Arrange(new Rect(0, 0, 280, 60));
            combo.UpdateLayout();
            var presenter = Assert.IsType<ContentPresenter>(combo.Template.FindName("SelectedItemPresenter", combo));
            var text = Descendants(presenter).OfType<TextBlock>().Single(t => t.Text == "Modelo seleccionado");
            Assert.True(Contrast(((SolidColorBrush)text.Foreground).Color, ((SolidColorBrush)combo.Background).Color) >= 4.5);

            var primary = new Button { Resources = colors, Content = "Usar captura", Style = (Style)Application.Current.FindResource("PrimaryButtonStyle") };
            Assert.True(Contrast(((SolidColorBrush)primary.Foreground).Color, ((SolidColorBrush)primary.Background).Color) >= 4.5);

            // Se conserva una imagen del control real para revisar también lo que dibujó WPF.
            var bitmap = new RenderTargetBitmap(280, 60, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(combo);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            var folder = Path.Combine(AppContext.BaseDirectory, "a11y-evidence");
            Directory.CreateDirectory(folder);
            using var stream = File.Create(Path.Combine(folder, "combo-selected.png"));
            encoder.Save(stream);
        });
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    private static double Contrast(Color a, Color b)
    {
        static double Channel(byte value) => value / 255d <= 0.04045 ? value / 255d / 12.92 : Math.Pow((value / 255d + 0.055) / 1.055, 2.4);
        static double Luminance(Color color) => 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
        var x = Luminance(a); var y = Luminance(b);
        return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
    }
}
