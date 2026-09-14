using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using Nexo.App.Views.Controls;

namespace Nexo.App.Tests;

/// <summary>
/// Lo que el Narrador oye de la cabecera de una sección de Personalizar.
///
/// Se encontró recorriendo la aplicación con Automatización de la interfaz de usuario: la cabecera
/// era un botón de alternancia, y el Narrador decía «Apariencia, desactivado» de una sección que solo
/// estaba cerrada. Ninguna prueba lo veía porque ninguna preguntaba qué patrón expone el control.
/// </summary>
[Collection(StaWpfCollection.Name)]
public sealed class SectionHeaderToggleAutomationTests
{
    private readonly StaWpfFixture _wpf;

    public SectionHeaderToggleAutomationTests(StaWpfFixture wpf) => _wpf = wpf;

    [Fact]
    public void ExposesExpandCollapse_AndNotToggle()
    {
        _wpf.Invoke(() =>
        {
            var header = new SectionHeaderToggle();
            var peer = UIElementAutomationPeer.CreatePeerForElement(header);

            Assert.IsAssignableFrom<IExpandCollapseProvider>(peer.GetPattern(PatternInterface.ExpandCollapse));
            Assert.Null(peer.GetPattern(PatternInterface.Toggle));
            Assert.Equal(AutomationControlType.Button, peer.GetAutomationControlType());
        });
    }

    [Fact]
    public void StateFollowsTheSection_BothWays()
    {
        _wpf.Invoke(() =>
        {
            var header = new SectionHeaderToggle();
            var provider = (IExpandCollapseProvider)UIElementAutomationPeer
                .CreatePeerForElement(header)
                .GetPattern(PatternInterface.ExpandCollapse);

            Assert.Equal(ExpandCollapseState.Collapsed, provider.ExpandCollapseState);

            header.IsChecked = true;
            Assert.Equal(ExpandCollapseState.Expanded, provider.ExpandCollapseState);

            // Y al revés: la orden de contraer del lector de pantalla cierra la sección de verdad.
            provider.Collapse();
            Assert.False(header.IsChecked);
        });
    }
}
