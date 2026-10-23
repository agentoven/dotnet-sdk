using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace AgentOven;

/// <summary>A recorded invocation (agent or recipe), with its spans.</summary>
public sealed record Trace
{
    /// <summary>Trace id.</summary>
    public string Id { get; init; } = "";

    /// <summary>Agent.</summary>
    public string? AgentName { get; init; }

    /// <summary>Recipe, for recipe runs.</summary>
    public string? RecipeName { get; init; }

    /// <summary>Kitchen.</summary>
    public string? Kitchen { get; init; }

    /// <summary>Status.</summary>
    public string? Status { get; init; }

    /// <summary>Duration in milliseconds.</summary>
    public long DurationMs { get; init; }

    /// <summary>Tokens.</summary>
    public long TotalTokens { get; init; }

    /// <summary>Cost in USD.</summary>
    [JsonPropertyName("cost_usd")]
    public double CostUsd { get; init; }

    /// <summary>Input text.</summary>
    public string? InputText { get; init; }

    /// <summary>Output text.</summary>
    public string? OutputText { get; init; }

    /// <summary>User.</summary>
    public string? UserId { get; init; }

    /// <summary>Parent trace (delegation, sub-recipes).</summary>
    public string? ParentTraceId { get; init; }

    /// <summary>Session.</summary>
    public string? SessionId { get; init; }

    /// <summary>Tags.</summary>
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>Usage breakdown.</summary>
    public TokenUsage? Usage { get; init; }

    /// <summary>Extended-thinking blocks.</summary>
    public IReadOnlyList<ThinkingBlock>? ThinkingBlocks { get; init; }

    /// <summary>Metadata.</summary>
    public JsonObject? Metadata { get; init; }

    /// <summary>Metadata reported by framework-native runtimes.</summary>
    public JsonObject? RuntimeMetadata { get; init; }

    /// <summary>Spans, when included.</summary>
    public IReadOnlyList<Span>? Spans { get; init; }

    /// <summary>Creation time.</summary>
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>Fields this SDK version does not model.</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

/// <summary>One timed operation within a trace (an LLM call, a tool call, …).</summary>
public sealed record Span
{
    /// <summary>Span id.</summary>
    public string Id { get; init; } = "";

    /// <summary>Trace id.</summary>
    public string? TraceId { get; init; }

    /// <summary>Parent span.</summary>
    public string? ParentSpanId { get; init; }

    /// <summary>Name.</summary>
    public string? Name { get; init; }

    /// <summary>Kind (llm, tool, …).</summary>
    public string? Kind { get; init; }

    /// <summary>running, completed or failed.</summary>
    public string? Status { get; init; }

    /// <summary>Start.</summary>
    public DateTimeOffset? StartTime { get; init; }

    /// <summary>End.</summary>
    public DateTimeOffset? EndTime { get; init; }

    /// <summary>Duration in milliseconds.</summary>
    public long DurationMs { get; init; }

    /// <summary>Request payload.</summary>
    public JsonNode? Input { get; init; }

    /// <summary>Response payload.</summary>
    public JsonNode? Output { get; init; }

    /// <summary>Usage (LLM spans).</summary>
    public TokenUsage? Usage { get; init; }

    /// <summary>Model (LLM spans).</summary>
    public string? Model { get; init; }

    /// <summary>Provider (LLM spans).</summary>
    public string? Provider { get; init; }

    /// <summary>Error.</summary>
    public string? Error { get; init; }

    /// <summary>Metadata.</summary>
    public JsonObject? Metadata { get; init; }

    /// <summary>Fields this SDK version does not model (for example <c>events</c>).</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

/// <summary>Filters for <see cref="TracesClient.ListAsync"/>.</summary>
public sealed record TraceQuery
{
    /// <summary>Agent.</summary>
    public string? Agent { get; init; }

    /// <summary>Recipe.</summary>
    public string? Recipe { get; init; }

    /// <summary>Status.</summary>
    public string? Status { get; init; }

    /// <summary>Maximum results (default 50).</summary>
    public int Limit { get; init; } = 50;
}

/// <summary>An entry of the audit log.</summary>
public sealed record AuditEvent
{
    /// <summary>Id.</summary>
    public string Id { get; init; } = "";

    /// <summary>When.</summary>
    public DateTimeOffset? Timestamp { get; init; }

    /// <summary>User id.</summary>
    public string? UserId { get; init; }

    /// <summary>User email.</summary>
    public string? UserEmail { get; init; }

    /// <summary>user, service_account or api_key.</summary>
    public string? ActorType { get; init; }

    /// <summary>Display name of the actor.</summary>
    public string? ActorDisplayName { get; init; }

    /// <summary>Action, e.g. <c>agent.bake</c>.</summary>
    public string Action { get; init; } = "";

    /// <summary>Resource type.</summary>
    public string? Resource { get; init; }

    /// <summary>Resource id.</summary>
    public string? ResourceId { get; init; }

    /// <summary>Kitchen.</summary>
    public string? Kitchen { get; init; }

    /// <summary>Details.</summary>
    public JsonObject? Details { get; init; }

    /// <summary>Client IP.</summary>
    public string? Ip { get; init; }

    /// <summary>Client user agent.</summary>
    public string? UserAgent { get; init; }

    /// <summary>HTTP status of the audited request.</summary>
    public int? ResponseStatus { get; init; }

    /// <summary>Regulation tags (HIPAA, SOC2, …).</summary>
    public IReadOnlyList<string>? RegulationTags { get; init; }

    /// <summary>Data classification.</summary>
    public string? DataClassification { get; init; }

    /// <summary>Environment.</summary>
    public string? Environment { get; init; }

    /// <summary>Position in the hash chain (immutable audit).</summary>
    public long? Seq { get; init; }

    /// <summary>Previous entry's hash (immutable audit).</summary>
    public string? PrevHash { get; init; }

    /// <summary>Fields this SDK version does not model.</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

/// <summary>Filters for <see cref="AuditClient.ListAsync"/>.</summary>
public sealed record AuditQuery
{
    /// <summary>Maximum results (default 50).</summary>
    public int Limit { get; init; } = 50;

    /// <summary>Offset, for paging.</summary>
    public int? Offset { get; init; }

    /// <summary>Action.</summary>
    public string? Action { get; init; }

    /// <summary>Agent.</summary>
    public string? Agent { get; init; }

    /// <summary>Environment.</summary>
    public string? Environment { get; init; }

    /// <summary>User.</summary>
    public string? UserId { get; init; }

    /// <summary>Resource type.</summary>
    public string? Resource { get; init; }
}

/// <summary>A question for the kitchen's knowledge base.</summary>
public sealed record RagQuery(string Question)
{
    /// <summary>Retrieval strategy (default <c>naive</c>).</summary>
    public string? Strategy { get; init; }

    /// <summary>Chunks to retrieve (default 5).</summary>
    public int? TopK { get; init; }

    /// <summary>Minimum similarity.</summary>
    public double? MinScore { get; init; }

    /// <summary>Partition.</summary>
    public string? Namespace { get; init; }

    /// <summary>Metadata filters.</summary>
    public IReadOnlyDictionary<string, string>? Filter { get; init; }

    /// <summary>Kitchen; filled in from the client.</summary>
    public string? Kitchen { get; init; }
}

/// <summary>An answer from the knowledge base, with its sources.</summary>
public sealed record RagAnswer
{
    /// <summary>Answer.</summary>
    public string Answer { get; init; } = "";

    /// <summary>Chunks used.</summary>
    public IReadOnlyList<RagSource> Sources { get; init; } = [];

    /// <summary>Strategy.</summary>
    public string? Strategy { get; init; }

    /// <summary>Chunks retrieved.</summary>
    public int ChunksRetrieved { get; init; }

    /// <summary>Tokens used.</summary>
    public long TokensUsed { get; init; }

    /// <summary>Latency in milliseconds.</summary>
    public long LatencyMs { get; init; }
}

/// <summary>One retrieved chunk and its score.</summary>
public sealed record RagSource
{
    /// <summary>Similarity.</summary>
    public double Score { get; init; }

    /// <summary>The chunk.</summary>
    public RagChunk? Doc { get; init; }
}

/// <summary>A stored chunk.</summary>
public sealed record RagChunk
{
    /// <summary>Id.</summary>
    public string Id { get; init; } = "";

    /// <summary>Text.</summary>
    public string Content { get; init; } = "";

    /// <summary>Metadata.</summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }

    /// <summary>Partition.</summary>
    public string? Namespace { get; init; }

    /// <summary>Fields this SDK version does not model (for example <c>vector</c>).</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

/// <summary>A document to ingest. Text as-is, or a PDF as base64 with <see cref="MimeType"/> <c>application/pdf</c>.</summary>
public sealed record RagDocument(string Content)
{
    /// <summary>A PDF (or other binary the server can convert) from bytes.</summary>
    public static RagDocument FromBytes(ReadOnlySpan<byte> bytes, string mimeType, string? id = null) =>
        new(Convert.ToBase64String(bytes)) { MimeType = mimeType, Id = id };

    /// <summary>Id.</summary>
    public string? Id { get; init; }

    /// <summary>Metadata.</summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }

    /// <summary>MIME type (default text).</summary>
    public string? MimeType { get; init; }
}

/// <summary>Options for <see cref="RagClient.IngestAsync"/>.</summary>
public sealed record RagIngestOptions
{
    /// <summary>Chunk size in tokens (default 512).</summary>
    public int? ChunkSize { get; init; }

    /// <summary>Chunk overlap in tokens (default 50).</summary>
    public int? ChunkOverlap { get; init; }

    /// <summary>Embedding provider.</summary>
    public string? EmbeddingProvider { get; init; }

    /// <summary>Partition.</summary>
    public string? Namespace { get; init; }
}

/// <summary>Result of an ingestion.</summary>
public sealed record RagIngestResult
{
    /// <summary>Documents processed.</summary>
    public int DocumentsProcessed { get; init; }

    /// <summary>Chunks created.</summary>
    public int ChunksCreated { get; init; }

    /// <summary>Vectors stored.</summary>
    public int VectorsStored { get; init; }

    /// <summary>Latency in milliseconds.</summary>
    public long LatencyMs { get; init; }
}
