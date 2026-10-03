using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Nexo.App.Motion;
using Xunit;

namespace Nexo.App.Tests.Motion;

/// <summary>
/// Regresión del defecto que dejó la aplicación entera invisible.
///
/// En WPF, <see cref="Timeline.BeginTime"/> con valor <c>null</c> no significa "empieza ya": significa
/// que la línea de tiempo **no se reproduce nunca**. El ayudante de movimiento recibe el retraso como
/// <c>TimeSpan?</c> —vacío cuando no hay escalonado— y lo pasaba tal cual, así que toda animación sin
/// escalonado quedaba desactivada en silencio: el shell se mostraba con opacidad 0, los módulos salían
/// en blanco y las secciones de Ajustes no abrían.
///
/// No falló ninguna prueba y tampoco lo vi al probarlo en pantalla, porque el equipo tenía las
/// animaciones de Windows desactivadas y todo iba por la ruta sin animación. Solo apareció cuando
/// alguien las activó. De ahí que esto se compruebe sobre el objeto de animación y no sobre la
/// pantalla: es lo único que se puede afirmar sin depender de un ajuste del sistema.
/// </summary>
public sealed class SakuraMotionTests
{
    private static readonly IEasingFunction Easing = new CubicBezierEase();
    private static readonly Duration OneSecond = new(TimeSpan.FromSeconds(1));

    [Fact]
    public void AnAnimationWithoutAStaggerStillPlays()
    {
        var animation = SakuraMotion.CreateAnimation(1, OneSecond, Easing);

        Assert.NotNull(animation.BeginTime);
        Assert.Equal(TimeSpan.Zero, animation.BeginTime);
    }

    [Fact]
    public void AnExplicitlyEmptyStaggerStillPlays()
    {
        // El caso exacto que se rompía: el retraso llega como TimeSpan? vacío desde la ruta normal.
        var animation = SakuraMotion.CreateAnimation(1, OneSecond, Easing, beginTime: null);

        Assert.Equal(TimeSpan.Zero, animation.BeginTime);
    }

    [Fact]
    public void AStaggerIsRespectedWhenThereIsOne()
    {
        var delay = TimeSpan.FromMilliseconds(90);

        var animation = SakuraMotion.CreateAnimation(1, OneSecond, Easing, delay);

        Assert.Equal(delay, animation.BeginTime);
    }

    [Fact]
    public void TheAnimationCarriesItsTargetDurationAndCurve()
    {
        var animation = SakuraMotion.CreateAnimation(0.75, OneSecond, Easing);

        Assert.Equal(0.75, animation.To);
        Assert.Equal(OneSecond, animation.Duration);
        Assert.Same(Easing, animation.EasingFunction);
    }

    [Fact]
    public void EveryStaggerStepProducesAPlayableDelay()
    {
        // StaggerAt alimenta BeginTime en las entradas escalonadas. Un valor negativo o nulo en
        // cualquier posición reintroduciría el mismo defecto para ese elemento en concreto.
        for (var index = 0; index < 40; index++)
        {
            var delay = SakuraMotion.StaggerAt(index);

            Assert.True(
                delay >= TimeSpan.Zero,
                $"El retraso de la posición {index} era negativo: {delay}.");
            Assert.True(
                delay <= SakuraMotion.MaximumStagger,
                $"El retraso de la posición {index} superó el tope: {delay}.");

            var animation = SakuraMotion.CreateAnimation(1, OneSecond, Easing, delay);
            Assert.NotNull(animation.BeginTime);
        }
    }

    [Fact]
    public void TheFirstItemOfAStaggeredListStartsImmediately()
    {
        Assert.Equal(TimeSpan.Zero, SakuraMotion.StaggerAt(0));
    }

    [Fact]
    public void TheStaggerStopsGrowingOnceItHitsTheCap()
    {
        // Sin tope, una lista larga haría esperar al último elemento más de lo que dura la propia
        // animación, y entonces deja de leerse como coreografía y pasa a leerse como lentitud.
        Assert.Equal(SakuraMotion.MaximumStagger, SakuraMotion.StaggerAt(500));
        Assert.Equal(SakuraMotion.StaggerAt(100), SakuraMotion.StaggerAt(500));
    }

    [Theory]
    [InlineData(10, 10, 0, 10)]   // quieta en el punto de partida: arranca desde ahí
    [InlineData(0, 10, 0, 10)]    // ya en su destino (abierta): se repite la entrada completa
    [InlineData(4, 10, 0, 4)]     // a medio camino: sigue desde donde está
    [InlineData(48, -48, 0, -48)] // retenida al otro lado (cambió de borde): parte del nuevo origen
    [InlineData(0.99, 0.96, 1, 0.99)]
    public void EnterStartKeepsOnlyAValueThatIsMidFlight(double current, double from, double to, double expected) =>
        Assert.Equal(expected, SakuraMotion.EnterStart(current, from, to), 6);
}

/// <summary>
/// «Entrar desde» sobre WPF de verdad: lo que el audit encontró a ojo (saltos al reabrir a mitad de
/// cierre, entradas que solo se movían la primera vez) se comprueba aquí leyendo el valor vivo de la
/// propiedad, que es lo que se vería en pantalla.
/// </summary>
[Collection(StaWpfCollection.Name)]
public sealed class EnterToTests(StaWpfFixture wpf)
{
    private static void Pump(TimeSpan wait)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = wait };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private static readonly IEasingFunction Linear = new PowerEase { Power = 1 };

    /// <summary>
    /// Los relojes de animación solo avanzan si hay una ventana a la vista: la transformación se cuelga
    /// de un elemento dentro de una ventana real (fuera de pantalla) y se bombea el despachador.
    /// </summary>
    private void WithAnimations(bool enabled, Action<TranslateTransform> body) =>
        wpf.Invoke(() =>
        {
            var original = SakuraMotion.AnimationsEnabled;
            SakuraMotion.AnimationsEnabled = enabled;
            var transform = new TranslateTransform();
            var window = new Window { Width = 200, Height = 200, Left = -3000, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
            window.Content = new Border { Width = 50, Height = 50, RenderTransform = transform };
            try
            {
                window.Show();
                body(transform);
            }
            finally
            {
                window.Close();
                SakuraMotion.AnimationsEnabled = original;
            }
        });

    [Fact]
    public void ASecondEntranceMovesAgainInsteadOfRisingFromZeroToZero()
    {
        // El defecto de EntranceMotion.Rise/Pop: tras la primera animación el valor queda retenido en
        // el destino y escribir el punto de partida a mano no se veía.
        WithAnimations(true, transform =>
        {
            var duration = new Duration(TimeSpan.FromMilliseconds(60));

            transform.EnterTo(TranslateTransform.YProperty, 10, 0, duration, Linear);
            Pump(TimeSpan.FromMilliseconds(400));
            Assert.Equal(0, transform.Y, 3);

            transform.EnterTo(TranslateTransform.YProperty, 10, 0, duration, Linear);

            Assert.Equal(10, transform.Y, 3);
        });
    }

    [Fact]
    public void AnInterruptedEntranceContinuesFromWhereItIsWithoutAJump()
    {
        WithAnimations(true, transform =>
        {
            var slow = new Duration(TimeSpan.FromSeconds(3));

            transform.EnterTo(TranslateTransform.YProperty, 100, 0, slow, Linear);
            Pump(TimeSpan.FromMilliseconds(900));
            var midFlight = transform.Y;
            Assert.InRange(midFlight, 1, 99);

            // Vuelve a pedirse la entrada a mitad de camino (reabrir antes de que acabe de irse).
            transform.EnterTo(TranslateTransform.YProperty, 100, 0, slow, Linear);

            // Sin salto: el valor justo después es el que había (más lo poco que avanzó el reloj entre
            // las dos lecturas), no el punto de partida de 100.
            Assert.InRange(transform.Y, midFlight - 8, midFlight + 8);
        });
    }

    [Fact]
    public void WithAnimationsOffTheValueLandsAtTheDestinationEvenIfAnAnimationWasHeld()
    {
        // El defecto de los controles rápidos: tras usarlos con animaciones, apagarlas y reabrir dejaba
        // el panel retenido fuera de su ventana, porque X = 0 no atravesaba la animación sujeta.
        WithAnimations(true, transform =>
        {
            transform.AnimateTransform(TranslateTransform.XProperty, 260, new Duration(TimeSpan.FromMilliseconds(40)), Linear);
            Pump(TimeSpan.FromMilliseconds(300));
            Assert.Equal(260, transform.X, 3);

            SakuraMotion.AnimationsEnabled = false;
            transform.EnterTo(TranslateTransform.XProperty, 28, 0, new Duration(TimeSpan.FromMilliseconds(40)), Linear);

            Assert.Equal(0, transform.X, 3);
        });
    }

    [Fact]
    public void TheAnimationsSettingAnnouncesAChangeOnlyWhenItReallyChanges()
    {
        var original = SakuraMotion.AnimationsEnabled;
        var raised = 0;
        void Count(object? sender, EventArgs e) => raised++;
        SakuraMotion.AnimationsEnabledChanged += Count;
        try
        {
            SakuraMotion.AnimationsEnabled = original;
            Assert.Equal(0, raised);

            SakuraMotion.AnimationsEnabled = !original;
            Assert.Equal(1, raised);
        }
        finally
        {
            SakuraMotion.AnimationsEnabledChanged -= Count;
            SakuraMotion.AnimationsEnabled = original;
        }
    }
}
