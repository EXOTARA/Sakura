using System.Net.Http.Headers;
using System.Text.Json;

namespace Nexo.Windows.Documents;

/// <summary>
/// 2026-09-16 — lo común de las consultas a catálogos públicos: identificarse, cortar a tiempo y leer
/// el JSON con prudencia.
///
/// Estaba copiado en los dos servicios que salen a internet para un documento —imágenes y fuentes— y
/// va a estarlo en el tercero. Identificarse es lo que piden las APIs de Wikimedia y Crossref para
/// saber quién consulta; cortar a tiempo es lo que impide que guardar un documento se quede colgado
/// esperando a un servidor que no contesta.
/// </summary>
internal static class WebLookup
{
    /// <summary>La identificación que se manda: quién es, qué versión y dónde mirar si molesta.</summary>
    public static void Identify(HttpClient client)
    {
        if (client.DefaultRequestHeaders.UserAgent.Count > 0)
        {
            return;
        }

        var version = typeof(WebLookup).Assembly.GetName().Version?.ToString(3) ?? "1.0";
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SakuraAssistant", version));
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(https://github.com/EXOTARA/Sakura)"));
    }

    public static CancellationTokenSource Timeout(CancellationToken cancellationToken, TimeSpan limit)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        source.CancelAfter(limit);
        return source;
    }

    /// <summary>Una cadena del JSON, o vacío si no está o no es una cadena.</summary>
    public static string Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    /// <summary>La primera cadena de una lista del JSON: Crossref manda el título y la revista así.</summary>
    public static string FirstText(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.Array && value.GetArrayLength() > 0 && value[0].ValueKind == JsonValueKind.String
            ? value[0].GetString() ?? string.Empty
            : string.Empty;

    public static int Number(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.TryGetInt32(out var number)
            ? number
            : 0;
}
