using System.Windows.Media;
using System.Windows.Threading;
using Nexo.App.Views.Controls;

namespace Nexo.App.Tests;

/// <summary>
/// Diseño D43 — la tira de pestañas del panel superior, con WPF de verdad.
///
/// La prueba central de aquí reproduce un defecto que se encontró usando la aplicación y no
/// leyéndola: la vista Sistema se construye al arrancar Sakura aunque nadie la abra, así que la
/// tira existe con ancho cero durante toda la sesión. La primera versión, al no poder medir el
/// subrayado, se volvía a encolar en el Dispatcher para reintentarlo — y como el ancho seguía
/// siendo cero, se encolaba otra vez, para siempre. La ventana no se rompía: se ponía pastosa y se
/// quedaba «cargando», con un tercio de un núcleo ocupado en no hacer nada.
///
/// Un análisis de texto del XAML no puede ver eso. Hace falta un Dispatcher real y contar lo que
/// se le encola.
/// </summary>
[Collection(StaWpfCollection.Name)]
public sealed class DashboardTabStripTests
{
    private readonly StaWpfFixture _wpf;

    public DashboardTabStripTests(StaWpfFixture wpf) => _wpf = wpf;

    [Fact]
    public void SetTabs_WhileNeverMeasured_DoesNotQueueWorkForever()
    {
        var posted = 0;

        // Solo cuenta lo que se encola desde el propio hilo del Dispatcher. OperationPosted se
        // dispara en el hilo que encola, y el Dispatcher es el compartido por toda la colección
        // STA: mientras SetTabs corre, otro hilo puede mandarle trabajo que no tiene nada que ver
        // con la tira (la continuación de una prueba anterior, una llamada de UI Automation...).
        // En CI eso dio un 4 una vez y un 0 al repetir. SetTabs es síncrono y no bombea mensajes,
        // así que todo lo que se encole desde este hilo durante la llamada sale de la tira — que
        // es justo donde el defecto original se volvía a encolar.
        DispatcherHookEventHandler counter = (_, e) =>
        {
            if (e.Dispatcher.CheckAccess())
            {
                Interlocked.Increment(ref posted);
            }
        };

        _wpf.Invoke(() =>
        {
            var strip = new DashboardTabStrip();
            Dispatcher.CurrentDispatcher.Hooks.OperationPosted += counter;

            try
            {
                strip.SetTabs(
                [
                    new DashboardTabDefinition("Panel", Geometry.Parse("M0,0 L1,1")),
                    new DashboardTabDefinition("Media", Geometry.Parse("M0,0 L1,1")),
                    new DashboardTabDefinition("Rendimiento", Geometry.Parse("M0,0 L1,1"))
                ]);
            }
            finally
            {
                Dispatcher.CurrentDispatcher.Hooks.OperationPosted -= counter;
            }
        });

        // Sin medir no hay nada que colocar, así que no debe encolarse nada. El defecto original
        // encolaba una operación por vuelta sin fin; cualquier cifra que crezca aquí es esa misma
        // rueda otra vez.
        Assert.Equal(0, posted);
    }

    [Fact]
    public void SetTabs_SelectsTheFirstTabWithoutRaisingSelectionChanged()
    {
        var raised = 0;
        var selected = -1;

        _wpf.Invoke(() =>
        {
            var strip = new DashboardTabStrip();
            strip.SelectionChanged += (_, _) => raised++;

            strip.SetTabs(
            [
                new DashboardTabDefinition("Panel", null),
                new DashboardTabDefinition("Media", null)
            ]);

            selected = strip.SelectedIndex;
        });

        Assert.Equal(0, selected);

        // La selección inicial no es una elección de nadie: avisar de ella haría que la vista
        // animara una entrada de panel nada más construirse, antes incluso de estar en pantalla.
        Assert.Equal(0, raised);
    }

    [Fact]
    public void Select_RaisesOnceForARealChangeAndNeverForTheSameTab()
    {
        var changes = new List<int>();

        _wpf.Invoke(() =>
        {
            var strip = new DashboardTabStrip();
            strip.SetTabs(
            [
                new DashboardTabDefinition("Panel", null),
                new DashboardTabDefinition("Media", null),
                new DashboardTabDefinition("Rendimiento", null)
            ]);

            strip.SelectionChanged += (_, index) => changes.Add(index);

            strip.Select(2);
            strip.Select(2);
            strip.Select(1);
        });

        Assert.Equal([2, 1], changes);
    }

    [Fact]
    public void Select_OutOfRange_IsIgnored()
    {
        var selected = -1;

        _wpf.Invoke(() =>
        {
            var strip = new DashboardTabStrip();
            strip.SetTabs([new DashboardTabDefinition("Panel", null)]);

            strip.Select(7);
            strip.Select(-3);

            selected = strip.SelectedIndex;
        });

        Assert.Equal(0, selected);
    }
}
