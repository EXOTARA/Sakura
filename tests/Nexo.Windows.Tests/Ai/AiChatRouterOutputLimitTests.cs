using System.Net;
using System.Text;
using Nexo.Core.Ai;
using Nexo.Core.Assistant;
using Nexo.Windows.Ai;

namespace Nexo.Windows.Tests.Ai;

/// <summary>
/// 2026-09-16 — regresión de lo que le pasó a Adler: después de explicar una ventana, cada pregunta
/// de seguimiento iba al modelo con visión, y Groq la rechazaba entera por el tamaño *estimado* de la
/// respuesta. El chat enseñaba el error del proveedor en inglés en lugar de contestar.
/// </summary>
public sealed class AiChatRouterOutputLimitTests
{
    private const string TooLarge = """
        {"error":{"message":"Request too large for model `qwen/qwen3.8-27b` in organization org_01 service tier on_demand on output tokens per minute (OTPM): Limit 1000, Requested 1935. The request's expected output tokens exceed the enforced limit; reduce max_tokens and try again.","type":"tokens","code":"rate_limit_exceeded"}}
        """;

    private const string Answer = """
        {"choices":[{"message":{"role":"assistant","content":"El error 0x80070005 es un problema de permisos."}}]}
        """;

    private static AiProviderConfiguration Configuration() =>
        new(AiProviderKind.Groq, "https://api.groq.com/openai/v1", "qwen/qwen3.8-27b", "GROQ_API_KEY")
        {
            StoredApiKey = "clave-de-prueba"
        };

    private static AiChatRequest Request() =>
        new([new ConversationMessage(ConversationRole.User, "Explícamelo más fácil", DateTimeOffset.Now)], "Eres Sakura.");

    [Fact]
    public async Task WhenTheProviderRejectsBySize_ItRetriesAskingForLess()
    {
        var handler = new FakeHandler([(HttpStatusCode.TooManyRequests, TooLarge), (HttpStatusCode.OK, Answer)]);
        using var router = new AiChatRouterService(new HttpClient(handler));

        var result = await router.SendAsync(Configuration(), Request());

        Assert.True(result.IsSuccess);
        Assert.Contains("permisos", result.Text);
        // La primera va sin máximo; la segunda, con el que el proveedor dijo que admite.
        Assert.DoesNotContain("max_tokens", handler.Bodies[0]);
        Assert.Contains("\"max_tokens\":872", handler.Bodies[1]);
    }

    [Fact]
    public async Task WhenNotEvenTheShorterOneFits_ItIsExplainedInSpanish()
    {
        var handler = new FakeHandler([(HttpStatusCode.TooManyRequests, TooLarge), (HttpStatusCode.TooManyRequests, TooLarge)]);
        using var router = new AiChatRouterService(new HttpClient(handler));

        var result = await router.SendAsync(Configuration(), Request());

        Assert.False(result.IsSuccess);
        Assert.Contains("tokens por minuto", result.Detail);
        Assert.Contains("más corto", result.Detail);
        // Se intenta una vez más, no una y otra vez.
        Assert.Equal(2, handler.Bodies.Count);
    }

    [Fact]
    public async Task TheSameHappensWhileTheAnswerIsBeingWritten()
    {
        var stream = "data: {\"choices\":[{\"delta\":{\"content\":\"Es un problema de permisos.\"}}]}\n\ndata: [DONE]\n\n";
        var handler = new FakeHandler([(HttpStatusCode.TooManyRequests, TooLarge), (HttpStatusCode.OK, stream)]);
        using var router = new AiChatRouterService(new HttpClient(handler));

        var text = string.Empty;
        await foreach (var chunk in router.StreamAsync(Configuration(), Request()))
        {
            text += chunk;
        }

        Assert.Contains("permisos", text);
        Assert.Contains("\"max_tokens\":872", handler.Bodies[1]);
    }

    [Fact]
    public async Task WhileWriting_WhenNotEvenTheShorterOneFits_ItIsExplainedInSpanish()
    {
        var handler = new FakeHandler([(HttpStatusCode.TooManyRequests, TooLarge), (HttpStatusCode.TooManyRequests, TooLarge)]);
        using var router = new AiChatRouterService(new HttpClient(handler));

        var failure = await Assert.ThrowsAsync<AiChatStreamException>(async () =>
        {
            await foreach (var _ in router.StreamAsync(Configuration(), Request()))
            {
            }
        });

        Assert.Contains("tokens por minuto", failure.Message);
        Assert.Contains("más corto", failure.Message);
    }

    [Fact]
    public async Task OtherFailures_AreNotRetried()
    {
        var handler = new FakeHandler([(HttpStatusCode.Unauthorized, """{"error":{"message":"Invalid API Key"}}""")]);
        using var router = new AiChatRouterService(new HttpClient(handler));

        var result = await router.SendAsync(Configuration(), Request());

        Assert.False(result.IsSuccess);
        Assert.Single(handler.Bodies);
    }

    private sealed class FakeHandler(IReadOnlyList<(HttpStatusCode Status, string Body)> responses) : HttpMessageHandler
    {
        private int _index;

        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
            var (status, body) = _index < responses.Count ? responses[_index++] : (HttpStatusCode.OK, Answer);
            // Una respuesta que llega por trozos viaja como «text/event-stream», igual que la de verdad.
            var media = body.StartsWith("data:", StringComparison.Ordinal) ? "text/event-stream" : "application/json";
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, media)
            };
        }
    }
}
