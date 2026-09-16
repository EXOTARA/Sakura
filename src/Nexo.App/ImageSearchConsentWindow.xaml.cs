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
    /// <param name="queries">Lo que hay que buscar en imágenes; vacío si no se piden.</param>
    /// <param name="topic">El tema del que buscar fuentes, o nulo si no se piden.</param>
    public ImageSearchConsentWindow(IReadOnlyList<string> queries, string? topic = null)
    {
        InitializeComponent();

        var wants = new List<string>();
        if (queries.Count > 0)
        {
            wants.Add(queries.Count == 1
                ? $"una imagen: «{queries[0]}»"
                : $"{queries.Count} imágenes: {string.Join(", ", queries.Take(3).Select(query => $"«{query}»"))}{(queries.Count > 3 ? "…" : string.Empty)}");
        }

        if (!string.IsNullOrWhiteSpace(topic))
        {
            wants.Add($"fuentes sobre «{topic!.Trim()}»");
        }

        QuestionText.Text = wants.Count > 1
            ? "¿Busco imágenes y fuentes para el documento?"
            : queries.Count > 0
                ? "¿Busco imágenes para el documento?"
                : "¿Busco fuentes para el documento?";
        DetailText.Text = "El documento pide " + string.Join(" y ", wants) + ".";
        ExplanationText.Text = queries.Count > 0
            ? "Las imágenes salen de Wikimedia Commons, de licencia libre, y las fuentes de catálogos académicos abiertos. Solo sale de tu equipo lo que hay que buscar. El documento incluye el autor y la licencia de cada imagen y la referencia de cada fuente. Puedes cambiarlo en Ajustes."
            : "Las fuentes salen de catálogos académicos abiertos, en español y de los últimos años. Solo sale de tu equipo el tema a buscar, y cada referencia se escribe con los datos que devuelve el catálogo, sin inventar ninguna. Puedes cambiarlo en Ajustes.";

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
