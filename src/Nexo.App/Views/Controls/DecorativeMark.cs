using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Nexo.App.Views.Controls;

// La marca repite la identidad de la ventana; no debe añadir una parada al recorrido del lector.
public sealed class DecorativeMark : ContentControl
{
    protected override AutomationPeer OnCreateAutomationPeer() => null!;
}

// Los glifos de estado acompañan a una descripción textual que ya comunica el mismo resultado.
public sealed class DecorativeText : TextBlock
{
    protected override AutomationPeer OnCreateAutomationPeer() => null!;
}
