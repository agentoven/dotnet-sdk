using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace AgentOven;

/// <summary>A multi-turn conversation with an agent. The control plane keeps the history.</summary>
public sealed record Session
{
    /// <summary>Session id.</summary>
    public string Id { get; init; } = "";

    /// <summary>Agent.</summary>
    public string? AgentName { get; init; }

    /// <summary>Kitchen.</summary>
    public string? Kitchen { get; init; }

    /// <summary>User.</summary>
    public string? UserId { get; init; }

    /// <summary>Status.</summary>
    public SessionStatus? Status { get; init; }

    /// <summary>History.</summary>
    public IReadOnlyList<ConversationMessage>? Messages { get; init; }

    /// <summary>Metadata.</summary>
    public JsonObject? Metadata { get; init; }

    /// <summary>Turn limit.</summary>
    public int? MaxTurns { get; init; }

    /// <summary>Turns so far.</summary>
    public int TurnCount { get; init; }

    /// <summary>Tokens so far.</summary>
    public long TotalTokens { get; init; }

    /// <summary>Cost so far in USD.</summary>
    [JsonPropertyName("total_cost_usd")]
    public double TotalCostUsd { get; init; }

    /// <summary>Creation time.</summary>
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>Last update.</summary>
    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>Expiry.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Fields this SDK version does not model.</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

/// <summary>Options for starting a session.</summary>
public sealed record SessionOptions
{
    /// <summary>Metadata stored with the session.</summary>
    public JsonObject? Metadata { get; init; }

    /// <summary>Turn limit.</summary>
    public int? MaxTurns { get; init; }
}

/// <summary>The agent's reply to a session message.</summary>
public sealed record SessionReply
{
    /// <summary>Session id.</summary>
    public string SessionId { get; init; } = "";

    /// <summary>Turn number.</summary>
    public int TurnNumber { get; init; }

    /// <summary>The agent's answer.</summary>
    public string Content { get; init; } = "";

    /// <summary>Why the model stopped.</summary>
    public string? FinishReason { get; init; }

    /// <summary>Usage of this turn.</summary>
    public TokenUsage? Usage { get; init; }

    /// <summary>Context-window accounting.</summary>
    public ContextBudgetReport? ContextBudget { get; init; }

    /// <summary>Latency in milliseconds.</summary>
    public long? LatencyMs { get; init; }

    /// <summary>Session status after this turn.</summary>
    public SessionStatus? Status { get; init; }

    /// <summary>Fields this SDK version does not model (for example <c>tool_calls</c>).</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; set; }

    /// <inheritdoc />
    public override string ToString() => Content;
}

/// <summary>Context-window accounting for a session turn.</summary>
public sealed record ContextBudgetReport
{
    /// <summary>Model context limit.</summary>
    public int ModelLimit { get; init; }

    /// <summary>Budget.</summary>
    public int Budget { get; init; }

    /// <summary>Used.</summary>
    public int Used { get; init; }

    /// <summary>Remaining.</summary>
    public int Remaining { get; init; }

    /// <summary>Utilisation percentage.</summary>
    [JsonPropertyName("utilization_pct")]
    public double UtilizationPercent { get; init; }

    /// <summary>Whether history was summarised to fit.</summary>
    public bool Summarised { get; init; }
}

/// <summary>A kitchen: an isolated workspace for agents, recipes and providers.</summary>
public sealed record Kitchen
{
    /// <summary>Kitchen id (a UUID; the seeded kitchen's id is <c>default</c>).</summary>
    public string Id { get; init; } = "";

    /// <summary>Name. This is what <see cref="AgentOvenClient.WithKitchen"/> takes.</summary>
    public string Name { get; init; } = "";

    /// <summary>Description.</summary>
    public string? Description { get; init; }

    /// <summary>Owner.</summary>
    public string? Owner { get; init; }

    /// <summary>Plan.</summary>
    public Plan? Plan { get; init; }

    /// <summary>Tags.</summary>
    public IReadOnlyDictionary<string, string>? Tags { get; init; }

    /// <summary>Kubernetes namespace.</summary>
    [JsonPropertyName("k8s_namespace")]
    public string? KubernetesNamespace { get; init; }

    /// <summary>Creation time.</summary>
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>Fields this SDK version does not model.</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

/// <summary>Edition, plan, features and limits of the control plane.</summary>
public sealed record ServerInfo
{
    /// <summary>Service name.</summary>
    public string? Service { get; init; }

    /// <summary>Server version.</summary>
    public string? Version { get; init; }

    /// <summary>community, pro or enterprise.</summary>
    public Plan? Edition { get; init; }

    /// <summary>Licensed plan.</summary>
    public Plan? Plan { get; init; }

    /// <summary>Licensed organisation.</summary>
    public string? Org { get; init; }

    /// <summary>Feature flags.</summary>
    public JsonObject? Features { get; init; }

    /// <summary>Plan limits.</summary>
    public JsonObject? Limits { get; init; }

    /// <summary>Auth configuration.</summary>
    public ServerAuth? Auth { get; init; }

    /// <summary>License details.</summary>
    public JsonObject? License { get; init; }

    /// <summary>Whether a feature flag (for example <c>environments</c>) is on.</summary>
    public bool HasFeature(string feature) =>
        Features is not null && Features.TryGetPropertyValue(feature, out var value) && value is JsonValue v && v.TryGetValue<bool>(out var on) && on;

    /// <summary>Fields this SDK version does not model.</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

/// <summary>Auth configuration of the control plane.</summary>
public sealed record ServerAuth
{
    /// <summary>Enabled auth providers.</summary>
    public IReadOnlyList<string>? Providers { get; init; }

    /// <summary>Whether SSO is enabled.</summary>
    public bool SsoEnabled { get; init; }

    /// <summary>Whether every request must authenticate.</summary>
    public bool RequireAuth { get; init; }
}
