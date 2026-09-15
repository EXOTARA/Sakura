using Nexo.Core.Ai;

namespace Nexo.Core.Tests;

public sealed class AiVisionModelPolicyTests
{
    private static readonly string[] AdlersGroqModels =
    [
        "qwen/qwen3.8-27b", "openai/gpt-oss-20b", "allam-2-7b", "whisper-large-v3", "openai/gpt-oss-120b",
        "groq/compound", "groq/compound-mini"
    ];

    [Fact]
    public void GroqWithGptOss_BorrowsQwenForImages() =>
        Assert.Equal("qwen/qwen3.8-27b", AiVisionModelPolicy.Choose(AiProviderKind.Groq, "openai/gpt-oss-120b", AdlersGroqModels));

    [Fact]
    public void ACandidateTheProviderDoesNotList_IsNeverUsed() =>
        Assert.Null(AiVisionModelPolicy.Choose(AiProviderKind.Groq, "openai/gpt-oss-120b", ["openai/gpt-oss-120b"]));

    [Fact]
    public void TheCurrentModelIsNotItsOwnFallback() =>
        Assert.Null(AiVisionModelPolicy.Choose(AiProviderKind.Groq, "qwen/qwen3.8-27b", ["qwen/qwen3.8-27b"]));

    [Fact]
    public void ProvidersWithoutKnownCandidates_GetNone() =>
        Assert.Null(AiVisionModelPolicy.Choose(AiProviderKind.Ollama, "llama3.2", ["llava"]));

    [Theory]
    [InlineData("openai/gpt-oss-120b", true)]
    [InlineData("groq/compound", true)]
    [InlineData("qwen/qwen3.8-27b", false)]
    [InlineData("gemini-2.5-flash", false)]
    [InlineData("", false)]
    public void KnownTextOnlyModels(string model, bool expected) =>
        Assert.Equal(expected, AiVisionModelPolicy.IsKnownTextOnly(model));
}
