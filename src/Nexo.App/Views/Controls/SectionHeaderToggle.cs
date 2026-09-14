using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls.Primitives;

namespace Nexo.App.Views.Controls;

/// <summary>
/// La cabecera pulsable de <see cref="SettingsSection"/>, anunciada como lo que es: algo que se
/// expande y se contrae.
///
/// Era un <see cref="ToggleButton"/> a secas, y eso es lo que le contaba a Windows. El Narrador
/// leía «Apariencia, botón de alternancia, desactivado» — y «desactivado» en una sección de ajustes
/// suena a que Apariencia está apagada, no a que está cerrada. Encontrado recorriendo Personalizar
/// con Automatización de la interfaz de usuario, que es lo mismo que lee el Narrador.
///
/// Se sigue comportando igual para el ratón, el teclado y la plantilla: solo cambia el patrón que
/// expone. Quita el de alternancia y pone el de expandir/contraer, así que el Narrador dice
/// «contraído» o «expandido» y sus órdenes de expandir funcionan.
/// </summary>
public sealed class SectionHeaderToggle : ToggleButton
{
    protected override AutomationPeer OnCreateAutomationPeer() => new SectionHeaderToggleAutomationPeer(this);

    protected override void OnChecked(System.Windows.RoutedEventArgs e)
    {
        base.OnChecked(e);
        RaiseExpandCollapseChanged(ExpandCollapseState.Collapsed, ExpandCollapseState.Expanded);
    }

    protected override void OnUnchecked(System.Windows.RoutedEventArgs e)
    {
        base.OnUnchecked(e);
        RaiseExpandCollapseChanged(ExpandCollapseState.Expanded, ExpandCollapseState.Collapsed);
    }

    private void RaiseExpandCollapseChanged(ExpandCollapseState oldState, ExpandCollapseState newState)
    {
        if (UIElementAutomationPeer.FromElement(this) is SectionHeaderToggleAutomationPeer peer)
        {
            peer.RaisePropertyChangedEvent(
                ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                oldState,
                newState);
        }
    }
}

public sealed class SectionHeaderToggleAutomationPeer(SectionHeaderToggle owner)
    : ToggleButtonAutomationPeer(owner), IExpandCollapseProvider
{
    private SectionHeaderToggle Header => (SectionHeaderToggle)Owner;

    public ExpandCollapseState ExpandCollapseState =>
        Header.IsChecked == true ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

    public override object? GetPattern(PatternInterface patternInterface) => patternInterface switch
    {
        PatternInterface.ExpandCollapse => this,
        // Sin esto el Narrador seguiría diciendo «activado/desactivado» además de «expandido».
        PatternInterface.Toggle => null,
        _ => base.GetPattern(patternInterface)
    };

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

    protected override string GetClassNameCore() => nameof(SectionHeaderToggle);

    public void Expand()
    {
        if (!IsEnabled())
        {
            throw new ElementNotEnabledException();
        }

        Header.IsChecked = true;
    }

    public void Collapse()
    {
        if (!IsEnabled())
        {
            throw new ElementNotEnabledException();
        }

        Header.IsChecked = false;
    }
}
