using System.ClientModel;
using Anthropic;
using Microsoft.Extensions.AI;
using OpenAI;

namespace AgentOven.Runtime.AI;

/// <summary>
/// Builds an <see cref="IChatClient"/> for the model the control plane resolved for this agent
/// (<see cref="AgentOvenRuntime.ModelKind"/>, <c>AGENT_MODEL_NAME</c>, <c>AGENT_API_KEY</c>, <c>AGENT_API_ENDPOINT</c>).
/// </summary>
/// <remarks>
/// Endpoints follow the control plane's built-in runner: with <c>AGENT_API_ENDPOINT</c> set, OpenAI-compatible
/// providers are called at <c>{endpoint}/v1</c>, Azure OpenAI at <c>{endpoint}/openai/v1</c>, and Ollama at
/// <c>{endpoint}/v1</c> (its OpenAI-compatible API).
/// </remarks>
public static class AgentOvenChatClients
{
    /// <summary>
    /// Creates a chat client for the runtime's model. When the provider kind cannot be determined (see
    /// <see cref="AgentOvenRuntime.ModelKind"/>), an endpoint means OpenAI-compatible; without one, set
    /// <c>AGENT_MODEL_KIND</c> or this throws <see cref="NotSupportedException"/>.
    /// </summary>
    public static IChatClient CreateChatClient(this AgentOvenRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        return Create(runtime.ModelKind ?? "", runtime.ModelName, runtime.ModelApiKey, runtime.ModelEndpoint, runtime.MaxTokens);
    }

    /// <summary>Creates a chat client for a provider kind, model, key and optional endpoint.</summary>
    public static IChatClient Create(string provider, string model, string? apiKey = null, string? endpoint = null, int? maxTokens = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        var kind = (provider ?? "").Trim().ToLowerInvariant();
        var baseUrl = string.IsNullOrWhiteSpace(endpoint) ? null : endpoint.TrimEnd('/');

        return kind switch
        {
            "anthropic" => CreateAnthropic(model, apiKey, baseUrl, maxTokens),
            "azure-openai" or "azure_openai" or "azure" => OpenAICompatible(
                model,
                apiKey,
                new Uri((baseUrl ?? throw new InvalidOperationException("Azure OpenAI needs AGENT_API_ENDPOINT (https://<resource>.openai.azure.com).")) + "/openai/v1/")),
            "ollama" => OpenAICompatible(model, apiKey ?? "ollama", new Uri((baseUrl ?? "http://localhost:11434") + "/v1/")),
            "groq" => OpenAICompatible(model, apiKey, new Uri((baseUrl ?? "https://api.groq.com/openai") + "/v1/")),
            "openrouter" => OpenAICompatible(model, apiKey, new Uri(baseUrl is null ? "https://openrouter.ai/api/v1/" : baseUrl + "/v1/")),
            "gemini" => OpenAICompatible(model, apiKey, new Uri(baseUrl is null ? "https://generativelanguage.googleapis.com/v1beta/openai/" : baseUrl + "/v1/")),
            "openai" when baseUrl is null => OpenAICompatible(model, apiKey, endpoint: null),
            _ when baseUrl is not null => OpenAICompatible(model, apiKey, new Uri(baseUrl + "/v1/")),
            _ => throw new NotSupportedException(
                $"Unknown model provider kind '{provider}'. Supported: openai, azure-openai, anthropic, ollama, groq, openrouter, gemini, " +
                "or any OpenAI-compatible provider with AGENT_API_ENDPOINT set. Set AGENT_MODEL_KIND when the provider name does not reveal its kind."),
        };
    }

    private static IChatClient OpenAICompatible(string model, string? apiKey, Uri? endpoint)
    {
        var options = new OpenAIClientOptions();
        if (endpoint is not null)
        {
            options.Endpoint = endpoint;
        }

        var key = apiKey ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            ?? throw new InvalidOperationException("No API key: AGENT_API_KEY is not set.");
        return new OpenAIClient(new ApiKeyCredential(key), options).GetChatClient(model).AsIChatClient();
    }

    private static IChatClient CreateAnthropic(string model, string? apiKey, string? baseUrl, int? maxTokens)
    {
        var key = apiKey ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        var client = baseUrl is null
            ? new AnthropicClient { ApiKey = key }
            : new AnthropicClient { ApiKey = key, BaseUrl = baseUrl };
        return client.AsIChatClient(model, maxTokens);
    }
}
