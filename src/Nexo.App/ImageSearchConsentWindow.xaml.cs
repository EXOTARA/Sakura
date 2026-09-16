using System.Windows;
using Nexo.App.Motion;

namespace Nexo.App;

/// <summary>
/// 2026-09-15 — la primera vez que una respuesta pide imágenes para un documento, Sakura pregunta si
/// puede buscarlas.
///
/// Se pregunta porque buscar imágenes es salir a internet con lo que el modelo escribió, y eso no debe
/// pasar sin que se sepa. Se pregunta una sola vez: la respuesta queda en Ajustes, donde se cambia.
/// </summary>
public partial class ImageSearchConsentWindow : Window
{
    public ImageSearchConsentWindow(IReadOnlyList<string> queries)
    {
        InitializeComponent();
        DetailText.Text = queries.Count == 1
            ? $"El documento pide una imagen: «{queries[0]}»."
            : $"El documento pide {queries.Count} imágenes: {string.Join(", ", queries.Take(3).Select(query => $"«{query}»"))}{(queries.Count > 3 ? "…" : ".")}";

        ContentRendered += (_, _) =>
        {
            SearchButton.Focus();
            EntranceMotion.Rise(DetailText, TimeSpan.Zero, offset: 6);
        };
    }

    private void Window_SourceInitialized(object? sender, EventArgs e) =>
        Shell.SakuraWindowChrome.MatchCaptionToTheme(this);

    /// <summary>Verdadero si se pueden buscar las imágenes.</summary>
    public bool Search { get; private set; }

    private void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        Search = true;
        DialogResult = true;
    }

    private void SkipButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
