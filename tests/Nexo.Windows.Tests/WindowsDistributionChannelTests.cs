using Nexo.Core.Distribution;
using Nexo.Windows.Distribution;

namespace Nexo.Windows.Tests;

public sealed class WindowsDistributionChannelTests
{
    [Fact]
    public void TestRunner_IsNotPackaged_SoItReadsAsTheDirectCopy()
    {
        // El proceso de pruebas no tiene identidad de paquete. Esta prueba no demuestra la rama de la
        // Store —eso se comprueba registrando el paquete de verdad—, pero sí que la llamada nativa
        // existe, no lanza y no confunde «sin paquete» con «Store».
        Assert.Equal(DistributionChannel.Direct, WindowsDistributionChannel.Current);
    }
}
