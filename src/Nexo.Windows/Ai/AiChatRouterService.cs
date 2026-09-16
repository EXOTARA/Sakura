using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Nexo.Core.Ai;

namespace Nexo.Windows.Ai;

public sealed class AiChatRouterService : IAiChatService, IDisposable
{
    private readonly OpenAiCompatibleChatService _compatibleService;
    private readonly OllamaNativeChatService _ollamaService;

    /// <summary>La lista de modelos de cada proveedor, pedida una vez por sesión y solo si hace falta.</summary>
    private readonly ConcurrentDictionary<string, IReadOnlyList<string>> _modelsByEndpoint = new(StringComparer.OrdinalIgnoreCase);

    public AiChatRouterService(HttpClient? compatibleClient = null, HttpClient? ollamaClient = null)
    {
        _compatibleService = new OpenAiCompatibleChatService(compatibleClient);
        _ollamaService = new OllamaNativeChatService(ollamaClient);
    }

    public Task<AiConnectionResult> TestConnectionAsync(
        AiProviderConfiguration configuration,
        CancellationToken cancellationToken = default) =>
        Resolve(configuration).TestConnectionAsync(configuration, cancellationToken);

    public async Task<AiChatResult> SendAsync(
        AiProviderConfiguration configuration,
        AiChatRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!HasImages(request))
        {
            return await ShorterIfRejectedAsync(configuration, request, cancellationToken);
        }

        if (AiVisionModelPolicy.IsKnownTextOnly(configuration.Model) &&
            await VisionConfigurationAsync(configuration, cancellationToken) is { } borrowed)
        {
            return await ShorterIfRejectedAsync(borrowed, request, cancellationToken);
        }

        // Este camino no lanza: devuelve el fallo. Con imagen y fallo, se repite con el modelo de visión.
        var result = await ShorterIfRejectedAsync(configuration, request, cancellationToken);
        if (result.IsSuccess || await VisionConfigurationAsync(configuration, cancellationToken) is not { } fallback)
        {
            return result;
        }

        return await ShorterIfRejectedAsync(fallback, request, cancellationToken);
    }

    /// <summary>
    /// 2026-09-15 — una petición con imagen para un modelo que no las lee se hace con un modelo del
    /// mismo proveedor que sí (ver <see cref="AiVisionModelPolicy"/>). Si el modelo se sabe de solo
    /// texto se va directo al otro; si no se sabe, se intenta con el elegido y solo si lo rechaza antes
    /// de escribir nada se repite con el de visión. Una respuesta ya empezada nunca se repite.
    /// </summary>
    public async IAsyncEnumerable<string> StreamAsync(
        AiProviderConfiguration configuration,
        AiChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var effective = configuration;
        if (HasImages(request) &&
            AiVisionModelPolicy.IsKnownTextOnly(configuration.Model) &&
            await VisionConfigurationAsync(configuration, cancellationToken) is { } direct)
        {
            effective = direct;
        }

        var enumerator = Resolve(effective).StreamAsync(effective, request, cancellationToken).GetAsyncEnumerator(cancellationToken);
        var produced = false;
        AiProviderConfiguration? retry = null;
        AiChatRequest? shorter = null;
        try
        {
            while (true)
            {
                string chunk;
                try
                {
                    if (!await enumerator.MoveNextAsync())
                    {
                        break;
                    }

                    chunk = enumerator.Current;
                }
                catch (Exception exception) when (
                    exception is not OperationCanceledException &&
                    !produced &&
                    request.MaxOutputTokens is null &&
                    AiProviderLimits.OutputLimit(exception.Message) is not null)
                {
                    // El proveedor dijo cuánto admite: se repite con ese máximo, con el mismo modelo.
                    shorter = request with { MaxOutputTokens = AiProviderLimits.OutputLimit(exception.Message) };
                    retry = effective;
                    break;
                }
                catch (Exception exception) when (
                    exception is not OperationCanceledException &&
                    !produced &&
                    HasImages(request) &&
                    ReferenceEquals(effective, configuration))
                {
                    retry = await VisionConfigurationAsync(configuration, cancellationToken);
                    if (retry is null)
                    {
                        throw;
                    }

                    break;
                }

                produced = true;
                yield return chunk;
            }
        }
        finally
        {
            await enumerator.DisposeAsync();
        }

        if (retry is null)
        {
            yield break;
        }

        var second = Resolve(retry).StreamAsync(retry, shorter ?? request, cancellationToken).GetAsyncEnumerator(cancellationToken);
        try
        {
            while (true)
            {
                string chunk;
                try
                {
                    if (!await second.MoveNextAsync())
                    {
                        break;
                    }

                    chunk = second.Current;
                }
                catch (Exception exception) when (
                    exception is not OperationCanceledException &&
                    shorter is not null &&
                    AiProviderLimits.IsOutputLimit(exception.Message))
                {
                    // Ni pidiendo menos cabe: se dice en español y con qué hacer.
                    throw new AiChatStreamException(AiProviderLimits.Explain(shorter.MaxOutputTokens));
                }

                yield return chunk;
            }
        }
        finally
        {
            await second.DisposeAsync();
        }
    }

    /// <summary>
    /// 2026-09-16 — si el proveedor rechaza la petición por el tamaño de la respuesta, se repite
    /// pidiendo como mucho lo que él mismo dijo que admite (ver <see cref="AiProviderLimits"/>).
    ///
    /// Le pasaba a Adler en cada pregunta de seguimiento sobre una ventana: la imagen sigue en el
    /// contexto, así que la pregunta va al modelo con visión, y ese modelo en su plan solo admite
    /// 1000 tokens de respuesta por minuto. El chat enseñaba el error del proveedor, en inglés, en
    /// lugar de contestar.
    /// </summary>
    private async Task<AiChatResult> ShorterIfRejectedAsync(
        AiProviderConfiguration configuration,
        AiChatRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Resolve(configuration).SendAsync(configuration, request, cancellationToken);
        if (result.IsSuccess || request.MaxOutputTokens is not null ||
            AiProviderLimits.OutputLimit(result.Detail) is not { } limit)
        {
            return result;
        }

        var shorter = await Resolve(configuration).SendAsync(configuration, request with { MaxOutputTokens = limit }, cancellationToken);

        // Si ni pidiendo menos cabe, se dice en español y con qué hacer, no con el error del proveedor.
        return shorter.IsSuccess || !AiProviderLimits.IsOutputLimit(shorter.Detail)
            ? shorter
            : AiChatResult.Failed(AiProviderLimits.Explain(limit));
    }

    /// <summary>Nombre del último modelo prestado para leer una imagen, para poder decirlo.</summary>
    public string? LastVisionModel { get; private set; }

    private static bool HasImages(AiChatRequest request) => request.Images is { Count: > 0 };

    private async Task<AiProviderConfiguration?> VisionConfigurationAsync(
        AiProviderConfiguration configuration,
        CancellationToken cancellationToken)
    {
        if (AiVisionModelPolicy.CandidatesFor(configuration.Provider).Count == 0)
        {
            return null;
        }

        var endpoint = configuration.Provider + "|" + configuration.BaseUrl;
        if (!_modelsByEndpoint.TryGetValue(endpoint, out var models))
        {
            var connection = await Resolve(configuration).TestConnectionAsync(configuration, cancellationToken);
            if (!connection.IsSuccess)
            {
                return null;
            }

            models = connection.Models;
            _modelsByEndpoint[endpoint] = models;
        }

        var chosen = AiVisionModelPolicy.Choose(configuration.Provider, configuration.Model, models.ToArray());
        if (chosen is null)
        {
            return null;
        }

        LastVisionModel = chosen;
        return configuration with { Model = chosen };
    }

    public void Dispose()
    {
        _compatibleService.Dispose();
        _ollamaService.Dispose();
    }

    private IAiChatService Resolve(AiProviderConfiguration configuration) =>
        AiProviderDefaults.UsesOllamaProtocol(configuration.Provider)
            ? _ollamaService
            : _compatibleService;
}
