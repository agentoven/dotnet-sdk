using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace AgentOven;

/// <summary>An agent's ingredients as the control plane resolved them at bake time.</summary>
public sealed record ResolvedIngredients
{
    /// <summary>The resolved model.</summary>
    public ResolvedModel? Model { get; init; }

    /// <summary>Resolved MCP tools.</summary>
    public IReadOnlyList<ResolvedTool>? Tools { get; init; }

    /// <summary>The resolved prompt.</summary>
    public ResolvedPrompt? Prompt { get; init; }

    /// <summary>Resolved data sources.</summary>
    public IReadOnlyList<ResolvedDataSource>? Data { get; init; }

    /// <summary>Resolved embedding models.</summary>
    public IReadOnlyList<ResolvedEmbedding>? Embeddings { get; init; }

    /// <summary>Resolved vector stores.</summary>
    public IReadOnlyList<ResolvedVectorStore>? VectorStores { get; init; }

    /// <summary>Resolved retrievers.</summary>
    public IReadOnlyList<ResolvedRetriever>? Retrievers { get; init; }

    /// <summary>Resolved scenarios.</summary>
    public IReadOnlyList<ResolvedScenario>? Scenarios { get; init; }

    /// <summary>Fields this SDK version does not model.</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

/// <summary>A resolved model.</summary>
public sealed record ResolvedModel
{
    /// <summary>Provider name.</summary>
    public string? Provider { get; init; }

    /// <summary>Provider kind.</summary>
    public ProviderKind? Kind { get; init; }

    /// <summary>Model name.</summary>
    public string? Model { get; init; }

    /// <summary>Provider endpoint.</summary>
    public string? Endpoint { get; init; }

    /// <summary>Provider API key (returned in plain text by the server).</summary>
    public string? ApiKey { get; init; }

    /// <summary>Provider config.</summary>
    public JsonObject? Config { get; init; }
}

/// <summary>
/// A resolved MCP tool. Agent processes receive these in <c>AGENT_TOOLS_JSON</c>.
/// </summary>
public sealed record ResolvedTool
{
    /// <summary>Tool name (the function name the model calls).</summary>
    public string Name { get; init; } = "";

    /// <summary>MCP endpoint (JSON-RPC 2.0 <c>tools/call</c>).</summary>
    public string? Endpoint { get; init; }

    /// <summary>Transport, e.g. <c>http</c> or <c>sse</c>.</summary>
    public string? Transport { get; init; }

    /// <summary>JSON Schema of the tool's arguments. May carry a top-level <c>description</c>.</summary>
    public JsonObject? Schema { get; init; }

    /// <summary>Version pinned at bake time.</summary>
    public string? Version { get; init; }

    /// <summary>SHA-256 of the schema at bake time.</summary>
    public string? SchemaHash { get; init; }

    /// <summary>When the tool was resolved.</summary>
    public DateTimeOffset? BakedAt { get; init; }
}

/// <summary>A resolved prompt.</summary>
public sealed record ResolvedPrompt
{
    /// <summary>Prompt name.</summary>
    public string? Name { get; init; }

    /// <summary>Prompt version.</summary>
    public int? Version { get; init; }

    /// <summary>Template text.</summary>
    public string? Template { get; init; }

    /// <summary>Rendered text.</summary>
    public string? Rendered { get; init; }
}

/// <summary>A resolved data source. Agent processes receive these in <c>AGENT_DATA_SOURCES_JSON</c>.</summary>
public sealed record ResolvedDataSource
{
    /// <summary>Name.</summary>
    public string Name { get; init; } = "";

    /// <summary>Location.</summary>
    public string? Uri { get; init; }

    /// <summary>Config.</summary>
    public JsonObject? Config { get; init; }
}

/// <summary>A resolved embedding model.</summary>
public sealed record ResolvedEmbedding
{
    /// <summary>Provider kind.</summary>
    public string? Provider { get; init; }

    /// <summary>Provider name.</summary>
    public string? ProviderName { get; init; }

    /// <summary>Model.</summary>
    public string? Model { get; init; }

    /// <summary>Vector dimensions.</summary>
    public int? Dimensions { get; init; }

    /// <summary>Max texts per call.</summary>
    public int? BatchSize { get; init; }

    /// <summary>cosine, euclidean or dot.</summary>
    public string? DistanceMetric { get; init; }

    /// <summary>Endpoint.</summary>
    public string? Endpoint { get; init; }

    /// <summary>Config.</summary>
    public JsonObject? Config { get; init; }
}

/// <summary>A resolved vector store.</summary>
public sealed record ResolvedVectorStore
{
    /// <summary>Backend.</summary>
    public string? Backend { get; init; }

    /// <summary>Index.</summary>
    public string? Index { get; init; }

    /// <summary>Namespace.</summary>
    public string? Namespace { get; init; }

    /// <summary>Vector dimensions.</summary>
    public int? Dimensions { get; init; }

    /// <summary>Config.</summary>
    public JsonObject? Config { get; init; }
}

/// <summary>A resolved retriever.</summary>
public sealed record ResolvedRetriever
{
    /// <summary>Embedding ingredient name.</summary>
    public string? EmbeddingRef { get; init; }

    /// <summary>Vector store ingredient name.</summary>
    [JsonPropertyName("vectorstore_ref")]
    public string? VectorStoreRef { get; init; }

    /// <summary>Results to return.</summary>
    public int? TopK { get; init; }

    /// <summary>Minimum score.</summary>
    public double? ScoreThreshold { get; init; }

    /// <summary>Reranking strategy.</summary>
    public string? RerankStrategy { get; init; }

    /// <summary>Hybrid keyword + vector search.</summary>
    public bool? HybridSearch { get; init; }

    /// <summary>Provider.</summary>
    public string? Provider { get; init; }

    /// <summary>Strategy.</summary>
    public string? Strategy { get; init; }

    /// <summary>Namespace.</summary>
    public string? Namespace { get; init; }
}

/// <summary>A resolved scenario.</summary>
public sealed record ResolvedScenario
{
    /// <summary>Ingredient name.</summary>
    public string? Name { get; init; }

    /// <summary>Scenario id.</summary>
    public string? Scenario { get; init; }

    /// <summary>Episodes per run.</summary>
    public int? Rollouts { get; init; }

    /// <summary>Required pass rate.</summary>
    public double? MinPassRate { get; init; }
}
