using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace AgentOven;

/// <summary>A model provider registered with the control plane (OpenAI, Anthropic, Ollama, …).</summary>
public sealed record ModelProvider
{
    /// <summary>Creates a provider definition.</summary>
    public ModelProvider(string name, ProviderKind kind)
    {
        Name = name;
        Kind = kind;
    }

    /// <summary>Name agents refer to in <see cref="Agent.ModelProvider"/>.</summary>
    public string Name { get; init; }

    /// <summary>Provider kind.</summary>
    public ProviderKind Kind { get; init; }

    /// <summary>Server-assigned identifier.</summary>
    public string? Id { get; init; }

    /// <summary>Kitchen.</summary>
    public string? Kitchen { get; init; }

    /// <summary>API endpoint override (Azure OpenAI, Ollama, LiteLLM, …).</summary>
    public string? Endpoint { get; init; }

    /// <summary>Models offered.</summary>
    public IReadOnlyList<string>? Models { get; init; }

    /// <summary>
    /// Provider config. The API key goes in <c>config.api_key</c>; the server masks it on reads.
    /// </summary>
    public JsonObject? Config { get; init; }

    /// <summary>Whether this is the kitchen's default provider. Always sent: an update without it clears the flag.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public bool IsDefault { get; init; }

    /// <summary>Secret-store reference for the API key.</summary>
    public string? SecretRef { get; init; }

    /// <summary>PEM CA bundle for private endpoints.</summary>
    public string? CaBundle { get; init; }

    /// <summary>Skip TLS verification (development only).</summary>
    public bool? TlsSkipVerify { get; init; }

    /// <summary>Key rotation strategy: round-robin, random or weighted.</summary>
    public string? RotationStrategy { get; init; }

    /// <summary>Creation time.</summary>
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>Last health check time.</summary>
    public DateTimeOffset? LastTestedAt { get; init; }

    /// <summary>Last health check outcome.</summary>
    public bool? LastTestHealthy { get; init; }

    /// <summary>Last health check error.</summary>
    public string? LastTestError { get; init; }

    /// <summary>Last health check latency.</summary>
    public long? LastTestLatencyMs { get; init; }

    /// <summary>Fields this SDK version does not model (for example <c>api_keys</c>).</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

/// <summary>Result of a provider update.</summary>
public sealed record ProviderUpdateResult
{
    /// <summary>The updated provider.</summary>
    public ModelProvider? Provider { get; init; }

    /// <summary>Ready agents using this provider that were marked burnt and need a re-bake.</summary>
    public int AgentsBurnt { get; init; }
}

/// <summary>Result of a provider health check.</summary>
public sealed record ProviderTestResult
{
    /// <summary>Provider name.</summary>
    public string? Provider { get; init; }

    /// <summary>Provider kind.</summary>
    public ProviderKind? Kind { get; init; }

    /// <summary>Whether the provider answered.</summary>
    public bool Healthy { get; init; }

    /// <summary>Latency in milliseconds.</summary>
    public long? LatencyMs { get; init; }

    /// <summary>Model used for the check.</summary>
    public string? Model { get; init; }

    /// <summary>Failure reason.</summary>
    public string? Error { get; init; }
}

/// <summary>Models a provider reports.</summary>
public sealed record ProviderDiscoveryResult
{
    /// <summary>Provider name.</summary>
    public string? Provider { get; init; }

    /// <summary>Models found.</summary>
    public IReadOnlyList<DiscoveredModel> Discovered { get; init; } = [];

    /// <summary>Count.</summary>
    public int Count { get; init; }
}

/// <summary>A model a provider reported.</summary>
public sealed record DiscoveredModel
{
    /// <summary>Model id.</summary>
    public string Id { get; init; } = "";

    /// <summary>Provider name.</summary>
    public string? Provider { get; init; }

    /// <summary>Provider kind.</summary>
    public ProviderKind? Kind { get; init; }

    /// <summary>Owner.</summary>
    public string? OwnedBy { get; init; }

    /// <summary>Creation time (Unix seconds).</summary>
    public long? CreatedAt { get; init; }

    /// <summary>Metadata.</summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}

/// <summary>A provider template offered by the dashboard.</summary>
public sealed record ProviderTemplate
{
    /// <summary>Kind.</summary>
    public ProviderKind Kind { get; init; }

    /// <summary>Display name.</summary>
    public string? DisplayName { get; init; }

    /// <summary>Description.</summary>
    public string? Description { get; init; }

    /// <summary>Default endpoint.</summary>
    public string? DefaultEndpoint { get; init; }

    /// <summary>Default models.</summary>
    public IReadOnlyList<string>? DefaultModels { get; init; }

    /// <summary>Required config keys.</summary>
    public IReadOnlyList<string>? RequiredConfig { get; init; }

    /// <summary>Documentation link.</summary>
    public string? HelpUrl { get; init; }
}
