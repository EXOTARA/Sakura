using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nexo.Core.Ai;
using Nexo.Core.Assistant;

namespace Nexo.Windows.Ai;

public sealed class OpenAiCompatibleChatService : IAiChatService, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;

    public OpenAiCompatibleChatService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _ownsClient = httpClient is null;
    }

    public async Task<AiConnectionResult> TestConnectionAsync(
        AiProviderConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateConfiguration(configuration);
        if (validation is not null)
        {
            return AiConnectionResult.Failed(validation);
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                BuildEndpoint(configuration.BaseUrl, "models"));
            ApplyAuthentication(request, configuration);

            using var timeout = CreateTimeout(cancellationToken, TimeSpan.FromSeconds(20));
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token);
            var body = await response.Content.ReadAsStringAsync(timeout.Token);

            if (!response.IsSuccessStatusCode)
            {
                return AiConnectionResult.Failed(
                    BuildHttpError(response.StatusCode, body, includedImages: false));
            }

            var models = ParseModels(body);
            var detail = models.Count == 0
                ? $"{configuration.DisplayName} respondió, pero no informó modelos disponibles."
                : $"{configuration.DisplayName} conectado · {models.Count} modelo(s) disponible(s).";

            if (!string.IsNullOrWhiteSpace(configuration.Model) &&
                models.Count > 0 &&
                !models.Contains(configuration.Model, StringComparer.OrdinalIgnoreCase))
            {
                detail += $" El modelo “{configuration.Model}” no apareció en la lista.";
            }

            return AiConnectionResult.Success(detail, models);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return AiConnectionResult.Failed(
                "La conexión tardó demasiado. Comprueba que el proveedor esté iniciado y que la URL sea correcta.");
        }
        catch (HttpRequestException exception)
        {
            return AiConnectionResult.Failed(
                $"No pude conectar con {configuration.DisplayName}: {exception.Message}");
        }
        catch (JsonException)
        {
            return AiConnectionResult.Failed(
                "El proveedor respondió, pero el formato de la lista de modelos no era compatible.");
        }
    }

    public async Task<AiChatResult> SendAsync(
        AiProviderConfiguration configuration,
        AiChatRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = ValidateConfiguration(configuration);
        if (validation is not null)
        {
            return AiChatResult.Failed(validation);
        }

        if (string.IsNullOrWhiteSpace(configuration.Model))
        {
            return AiChatResult.Failed(
                "Selecciona o escribe un modelo en Personalización → Inteligencia artificial.");
        }

        var messages = BuildMessages(request);
        if (messages.Count == 1)
        {
            return AiChatResult.Failed("No había una consulta para enviar al proveedor.");
        }

        var payload = new ChatCompletionRequest(
            configuration.Model.Trim(),
            messages,
            Stream: false,
            request.MaxOutputTokens);

        try
        {
            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                BuildEndpoint(configuration.BaseUrl, "chat/completions"))
            {
                Content = JsonContent.Create(payload, options: SerializerOptions)
            };
            ApplyAuthentication(httpRequest, configuration);

            using var timeout = CreateTimeout(cancellationToken, TimeSpan.FromSeconds(90));
            using var response = await _httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token);
            var body = await response.Content.ReadAsStringAsync(timeout.Token);

            if (!response.IsSuccessStatusCode)
            {
                return AiChatResult.Failed(BuildHttpError(
                    response.StatusCode, body, includedImages: request.Images is { Count: > 0 }));
            }

            var text = ParseAssistantText(body);
            return string.IsNullOrWhiteSpace(text)
                ? AiChatResult.Failed("El proveedor respondió sin texto utilizable.")
                : AiChatResult.Success(text);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return AiChatResult.Failed(
                "La respuesta tardó demasiado. Puedes intentar de nuevo o usar un modelo más ligero.");
        }
        catch (HttpRequestException exception)
        {
            return AiChatResult.Failed(
                $"No pude conectar con {configuration.DisplayName}: {exception.Message}");
        }
        catch (JsonException)
        {
            return AiChatResult.Failed(
                "El proveedor respondió con un formato que Sakura todavía no reconoce.");
        }
    }

    public async IAsyncEnumerable<string> StreamAsync(
        AiProviderConfiguration configuration,
        AiChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = ValidateConfiguration(configuration);
        if (validation is not null)
        {
            throw new AiChatStreamException(validation);
        }

        if (string.IsNullOrWhiteSpace(configuration.Model))
        {
            throw new AiChatStreamException(
                "Selecciona o escribe un modelo en Personalización → Inteligencia artificial.");
        }

        var messages = BuildMessages(request);
        if (messages.Count == 1)
        {
            throw new AiChatStreamException("No había una consulta para enviar al proveedor.");
        }

        var payload = new ChatCompletionRequest(
            configuration.Model.Trim(),
            messages,
            Stream: true,
            request.MaxOutputTokens);

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            BuildEndpoint(configuration.BaseUrl, "chat/completions"))
        {
            Content = JsonContent.Create(payload, options: SerializerOptions)
        };
        ApplyAuthentication(httpRequest, configuration);

        using var timeout = CreateTimeout(cancellationToken, TimeSpan.FromSeconds(90));
        using var response = await SendStreamingRequestAsync(
            httpRequest,
            configuration,
            includedImages: request.Images is { Count: > 0 },
            cancellationToken,
            timeout.Token);

        var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        var isLineStream = mediaType.Contains("event-stream", StringComparison.OrdinalIgnoreCase) ||
                           mediaType.Contains("ndjson", StringComparison.OrdinalIgnoreCase);

        if (!isLineStream)
        {
            var body = await response.Content.ReadAsStringAsync(timeout.Token);
            var text = ParseAssistantText(body);
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new AiChatStreamException(
                    "El proveedor respondió sin texto utilizable.");
            }

            yield return text;
            yield break;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var reader = new StreamReader(stream);
        var emittedText = false;

        while (true)
        {
            var line = await reader.ReadLineAsync(timeout.Token);
            if (line is null)
            {
                break;
            }

            var payloadLine = line.Trim();
            if (payloadLine.Length == 0 || payloadLine.StartsWith(':'))
            {
                continue;
            }

            if (payloadLine.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                payloadLine = payloadLine[5..].Trim();
            }

            if (payloadLine.Equals("[DONE]", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            var delta = TryParseStreamingText(payloadLine);
            if (string.IsNullOrEmpty(delta))
            {
                continue;
            }

            emittedText = true;
            yield return delta;
        }

        if (!emittedText)
        {
            throw new AiChatStreamException(
                "El proveedor terminó la respuesta sin enviar texto utilizable.");
        }
    }

    private async Task<HttpResponseMessage> SendStreamingRequestAsync(
        HttpRequestMessage request,
        AiProviderConfiguration configuration,
        bool includedImages,
        CancellationToken originalCancellationToken,
        CancellationToken timeoutToken)
    {
        try
        {
            var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutToken);

            if (response.IsSuccessStatusCode)
            {
                return response;
            }

            var body = await response.Content.ReadAsStringAsync(timeoutToken);
            var detail = BuildHttpError(response.StatusCode, body, includedImages);
            response.Dispose();
            throw new AiChatStreamException(detail);
        }
        catch (OperationCanceledException) when (!originalCancellationToken.IsCancellationRequested)
        {
            throw new AiChatStreamException(
                "La respuesta tardó demasiado. Puedes intentar de nuevo o usar un modelo más ligero.");
        }
        catch (HttpRequestException exception)
        {
            throw new AiChatStreamException(
                $"No pude conectar con {configuration.DisplayName}: {exception.Message}",
                exception);
        }
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _httpClient.Dispose();
        }
    }

    private static string? ValidateConfiguration(AiProviderConfiguration configuration)
    {
        if (!configuration.IsEnabled)
        {
            return "El proveedor de IA está desactivado.";
        }

        if (!Uri.TryCreate(
                AiProviderDefaults.NormalizeBaseUrl(configuration.BaseUrl),
                UriKind.Absolute,
                out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return "La URL del proveedor no es válida.";
        }

        if (configuration.RequiresApiKey &&
            string.IsNullOrWhiteSpace(configuration.ReadApiKey()))
        {
            var variable = string.IsNullOrWhiteSpace(configuration.ApiKeyEnvironmentVariable)
                ? "OPENAI_API_KEY"
                : configuration.ApiKeyEnvironmentVariable;
            return $"No encontré la variable de entorno {variable}. Sakura no guarda claves dentro del proyecto.";
        }

        return null;
    }

    private static List<ChatMessage> BuildMessages(AiChatRequest request)
    {
        var systemText = request.Instructions.Trim();
        if (!string.IsNullOrWhiteSpace(request.SystemContext))
        {
            systemText += $"\n\nContexto autorizado del equipo:\n{request.SystemContext.Trim()}";
        }

        var messages = new List<ChatMessage>
        {
            new("system", systemText)
        };

        var conversation = request.Messages
            .Where(item => !string.IsNullOrWhiteSpace(item.Text))
            .TakeLast(20)
            .ToArray();

        for (var index = 0; index < conversation.Length; index++)
        {
            var message = conversation[index];
            var role = message.Role == ConversationRole.User ? "user" : "assistant";
            var isLastUserMessage =
                index == conversation.Length - 1 &&
                message.Role == ConversationRole.User &&
                request.Images is { Count: > 0 };

            if (!isLastUserMessage)
            {
                messages.Add(new ChatMessage(role, message.Text.Trim()));
                continue;
            }

            var content = new List<ChatContentPart>
            {
                new("text", Text: message.Text.Trim())
            };

            foreach (var image in request.Images!)
            {
                content.Add(new ChatContentPart(
                    "image_url",
                    ImageUrl: new ChatImageUrl(image.DataUrl)));
            }

            messages.Add(new ChatMessage(role, content));
        }

        return messages;
    }

    private static void ApplyAuthentication(
        HttpRequestMessage request,
        AiProviderConfiguration configuration)
    {
        var apiKey = configuration.ReadApiKey();
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                apiKey.Trim());
        }
    }

    private static Uri BuildEndpoint(string baseUrl, string relativePath)
    {
        var normalized = AiProviderDefaults.NormalizeBaseUrl(baseUrl);
        var requestedSuffix = "/" + relativePath.TrimStart('/');

        if (normalized.EndsWith(requestedSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return new Uri(normalized, UriKind.Absolute);
        }

        return new Uri(normalized + requestedSuffix, UriKind.Absolute);
    }

    private static CancellationTokenSource CreateTimeout(
        CancellationToken cancellationToken,
        TimeSpan duration)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        source.CancelAfter(duration);
        return source;
    }

    private static IReadOnlyList<string> ParseModels(string body)
    {
        using var document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return data.EnumerateArray()
            .Where(item => item.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
            .Select(item => item.GetProperty("id").GetString())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string TryParseStreamingText(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            if (!document.RootElement.TryGetProperty("choices", out var choices) ||
                choices.ValueKind != JsonValueKind.Array ||
                choices.GetArrayLength() == 0)
            {
                return string.Empty;
            }

            var choice = choices[0];
            if (choice.TryGetProperty("delta", out var delta) &&
                delta.ValueKind == JsonValueKind.Object &&
                delta.TryGetProperty("content", out var deltaContent))
            {
                return ReadContentText(deltaContent);
            }

            if (choice.TryGetProperty("message", out var message) &&
                message.ValueKind == JsonValueKind.Object &&
                message.TryGetProperty("content", out var messageContent))
            {
                return ReadContentText(messageContent);
            }
        }
        catch (JsonException)
        {
            // Algunos servidores envían eventos auxiliares; se ignoran.
        }

        return string.Empty;
    }

    private static string ReadContentText(JsonElement content)
    {
        if (content.ValueKind == JsonValueKind.String)
        {
            return content.GetString() ?? string.Empty;
        }

        if (content.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        foreach (var item in content.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var text = item.GetString();
                if (!string.IsNullOrEmpty(text))
                {
                    parts.Add(text);
                }
                continue;
            }

            if (item.ValueKind == JsonValueKind.Object &&
                item.TryGetProperty("text", out var textProperty) &&
                textProperty.ValueKind == JsonValueKind.String)
            {
                var text = textProperty.GetString();
                if (!string.IsNullOrEmpty(text))
                {
                    parts.Add(text);
                }
            }
        }

        return string.Concat(parts);
    }

    private static string ParseAssistantText(string body)
    {
        using var document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("choices", out var choices) ||
            choices.ValueKind != JsonValueKind.Array ||
            choices.GetArrayLength() == 0)
        {
            return string.Empty;
        }

        var choice = choices[0];
        if (!choice.TryGetProperty("message", out var message) ||
            !message.TryGetProperty("content", out var content))
        {
            return string.Empty;
        }

        if (content.ValueKind == JsonValueKind.String)
        {
            return content.GetString()?.Trim() ?? string.Empty;
        }

        if (content.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        foreach (var item in content.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var text = item.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    parts.Add(text);
                }
                continue;
            }

            if (item.ValueKind == JsonValueKind.Object &&
                item.TryGetProperty("text", out var textProperty) &&
                textProperty.ValueKind == JsonValueKind.String)
            {
                var text = textProperty.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    parts.Add(text);
                }
            }
        }

        return string.Join(Environment.NewLine, parts).Trim();
    }

    /// <summary>
    /// Diseño D30 — encontrado con una captura real adjunta a Groq: el modelo de texto la rechazó
    /// con «messages[5].content must be a string», un mensaje que no dice nada sobre imágenes.
    /// Adivinar de la redacción del error que la causa fue una imagen es fràgil —cada proveedor
    /// redacta distinto y el texto cambiaría sin avisar—, así que la pista no es el mensaje: es la
    /// combinación de "se mandó una imagen" y "400 Bad Request", lo bastante fuerte como para
    /// ofrecer la explicación más probable sin fingir certeza absoluta.
    /// </summary>
    private static string BuildHttpError(HttpStatusCode statusCode, string body, bool includedImages)
    {
        var providerMessage = TryReadErrorMessage(body);
        var status = $"{(int)statusCode} {statusCode}";
        var baseError = string.IsNullOrWhiteSpace(providerMessage)
            ? $"El proveedor rechazó la solicitud ({status})."
            : $"El proveedor rechazó la solicitud ({status}): {providerMessage}";

        if (includedImages && statusCode == HttpStatusCode.BadRequest)
        {
            return baseError +
                " Es probable que el modelo elegido no admita imágenes — repite la pregunta sin " +
                "la captura, o cambia a un modelo que sí las admita.";
        }

        return baseError;
    }

    private static string? TryReadErrorMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            return ReadErrorFrom(document.RootElement);
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidOperationException)
        {
            // El cuerpo se resumirá como texto plano.
        }

        var compact = body.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return compact.Length <= 240 ? compact : compact[..240] + "…";
    }

    /// <summary>
    /// Diseño D37 — saca el mensaje de error mire como mire el proveedor.
    ///
    /// Corrección de una caída real y reproducible: con Gemini, Sakura se cerraba entera al primer
    /// error. Gemini envuelve sus errores en un array —<c>[{"error":{"message":"..."}}]</c>— y
    /// <see cref="JsonElement.TryGetProperty(string, out JsonElement)"/> sobre un array no devuelve
    /// falso: <b>lanza</b> <see cref="InvalidOperationException"/>. Se llamaba sobre la raíz sin
    /// mirar de qué tipo era, y esa excepción no estaba entre las capturadas, así que subía hasta
    /// arriba y se llevaba el proceso por delante. Lo peor del caso es que ocurría justo en el
    /// código que existe para explicar un error: fallar ahí convierte «el proveedor rechazó la
    /// petición» en «la aplicación se cerró».
    ///
    /// Ahora se comprueba el tipo antes de preguntar por cualquier propiedad, y se entra en los
    /// arrays en vez de rendirse, que es lo que además saca el mensaje bueno de Gemini en lugar de
    /// volcar el JSON crudo.
    /// </summary>
    private static string? ReadErrorFrom(JsonElement element, int depth = 0)
    {
        // Un tope de profundidad: el cuerpo lo escribe el proveedor, y recorrer sin límite algo que
        // no controlamos es una invitación a quedarse dando vueltas.
        if (depth > 4)
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (ReadErrorFrom(item, depth + 1) is { } fromArray)
                {
                    return fromArray;
                }
            }

            return null;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (element.TryGetProperty("error", out var error))
        {
            if (error.ValueKind == JsonValueKind.String)
            {
                return error.GetString();
            }

            if (error.ValueKind == JsonValueKind.Object &&
                error.TryGetProperty("message", out var message) &&
                message.ValueKind == JsonValueKind.String)
            {
                return message.GetString();
            }

            if (ReadErrorFrom(error, depth + 1) is { } nested)
            {
                return nested;
            }
        }

        if (element.TryGetProperty("detail", out var detail) &&
            detail.ValueKind == JsonValueKind.String)
        {
            return detail.GetString();
        }

        if (element.TryGetProperty("message", out var direct) &&
            direct.ValueKind == JsonValueKind.String)
        {
            return direct.GetString();
        }

        return null;
    }

    private sealed record ChatCompletionRequest(
        string Model,
        IReadOnlyList<ChatMessage> Messages,
        bool Stream,
        [property: JsonPropertyName("max_tokens")] int? MaxTokens = null);

    private sealed record ChatMessage(string Role, object Content);

    private sealed record ChatContentPart(
        string Type,
        string? Text = null,
        [property: JsonPropertyName("image_url")] ChatImageUrl? ImageUrl = null);

    private sealed record ChatImageUrl(string Url);
}
