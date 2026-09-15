using Nexo.Windows.WindowsIntegration;

namespace Nexo.Windows.Tests.Characterization;

/// <summary>
/// Satura el thread pool a propósito, así que no puede correr en paralelo con nada: xUnit
/// ejecuta las colecciones con la paralelización desactivada después de las demás.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ThreadPoolStarvationCollection
{
    public const string Name = "ThreadPoolStarvation";
}

/// <summary>
/// Regresión de un fallo intermitente en CI (run 34941007574): la segunda instancia señalaba
/// bien, pero la primaria «no recibía» la activación en 5 s.
///
/// <para>
/// La señal nunca se perdía — el evento con nombre es auto-reset y queda señalado hasta que
/// alguien lo consume —. Lo que ocurría es que el bucle de escucha se lanzaba con
/// <c>Task.Run</c>: ocupaba un hilo del pool bloqueado para siempre y, peor aún, para
/// **empezar** necesitaba que el pool tuviera un hilo libre. Con tres ensamblados de pruebas en
/// paralelo en un runner de pocos núcleos, el pool se quedaba sin hilos, crece a razón de uno
/// cada ~500 ms, y el oyente no llegaba a ejecutarse a tiempo. En la aplicación real pasa lo
/// mismo durante un arranque cargado: la ventana tardaría en aparecer al abrir Sakura de nuevo.
/// </para>
///
/// <para>
/// Esta prueba reproduce esa situación de forma determinista: bloquea el pool entero antes de
/// escuchar y exige que la activación llegue igualmente.
/// </para>
/// </summary>
[Collection(ThreadPoolStarvationCollection.Name)]
public sealed class SingleInstanceActivationUnderLoadTests
{
    [Fact]
    public void Activation_IsDelivered_EvenWhenTheThreadPoolIsStarved()
    {
        var key = "test-" + Guid.NewGuid().ToString("n");
        // Sin `using` a propósito: tras abrir la compuerta aún puede haber trabajos en cola que
        // llamen a `Wait`, y sobre un objeto liberado lanzarían en un hilo del pool y tumbarían
        // el proceso de pruebas.
        var gate = new ManualResetEventSlim(false);
        using var activated = new ManualResetEventSlim(false);

        // Más trabajos bloqueados que hilos mínimos, con margen de sobra para que la inyección
        // de hilos del pool no los alcance durante la espera de la prueba.
        ThreadPool.GetMinThreads(out var minWorkers, out _);
        var blockers = minWorkers + 64;

        try
        {
            for (var index = 0; index < blockers; index++)
            {
                ThreadPool.UnsafeQueueUserWorkItem(_ => gate.Wait(), null);
            }

            using var primary = new SingleInstanceCoordinator(key);
            Assert.True(primary.IsPrimaryInstance);

            primary.ActivationRequested += (_, _) => activated.Set();
            primary.StartListening();

            // La segunda instancia vive en un hilo propio (no del pool), como un proceso aparte.
            var second = new Thread(() =>
            {
                using var coordinator = new SingleInstanceCoordinator(key);
                coordinator.SignalPrimaryInstance();
            })
            {
                IsBackground = true
            };
            second.Start();
            Assert.True(second.Join(TimeSpan.FromSeconds(10)), "La segunda instancia no terminó.");

            Assert.True(
                activated.Wait(TimeSpan.FromSeconds(5)),
                "Con el thread pool saturado, la instancia primaria no recibió la activación.");
        }
        finally
        {
            gate.Set();
        }
    }
}
