using System.Windows;
using System.Windows.Controls;

namespace Nexo.App.Tests;

[Collection(StaWpfCollection.Name)]
public sealed class ImageSearchConsentWindowTests(StaWpfFixture wpf)
{
    [Fact]
    public void TheQuestion_SaysWhatSeVaABuscar_AndOffersBothAnswers()
    {
        wpf.Invoke(() =>
        {
            var window = new ImageSearchConsentWindow(["parque solar", "turbinas eólicas", "líneas de alta tensión", "presa hidroeléctrica"]);
            try
            {
                var content = Assert.IsAssignableFrom<FrameworkElement>(window.Content);
                content.Measure(new Size(560, 600));
                content.Arrange(new Rect(0, 0, 560, 600));
                content.UpdateLayout();

                var detail = (TextBlock)window.FindName("DetailText");
                Assert.Contains("4 imágenes", detail.Text);
                Assert.Contains("«parque solar»", detail.Text);
                // Con más de tres se resume: el aviso no es la lista completa.
                Assert.DoesNotContain("presa hidroeléctrica", detail.Text);

                // Sin pulsar nada no se busca: la respuesta por defecto es la prudente.
                Assert.False(window.Search);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void WithASingleImage_TheQuestionNamesIt()
    {
        wpf.Invoke(() =>
        {
            var window = new ImageSearchConsentWindow(["parque solar"]);
            try
            {
                Assert.Equal("La presentación pide una imagen: «parque solar».", ((TextBlock)window.FindName("DetailText")).Text);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
