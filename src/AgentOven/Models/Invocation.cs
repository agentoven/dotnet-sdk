using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace AgentOven;

/// <summary>Token counts and cost of a call.</summary>
public sealed record TokenUsage
{
    /// <summary>Prompt tokens.</summary>
    public long InputTokens { get; init; }

    /// <summary>Completion tokens.</summary>
    public long OutputTokens { get; init; }

    /// <summary>Reasoning tokens.</summary>
    public long? ThinkingTokens { get; init; }

    /// <summary>Total tokens.</summary>
    public long TotalTokens { get; init; }

    /// <summary>Estimated cost in USD.</summary>
    [JsonPropertyName("estimated_cost_usd")]
    public double? EstimatedCostUsd { get; init; }

    /// <summary>Prompt-cache hits.</summary>
    public int? CacheHits { get; init; }

    /// <summary>Tokens served from cache.</summary>
    public long? CachedTokens { get; init; }

    /// <summary>Tokens written to cache.</summary>
    public long? CacheCreation { get; init; }

    /// <summary>Savings from caching in USD.</summary>
    [JsonPropertyName("cache_savings_usd")]
    public double? CacheSavingsUsd { get; init; }

    /// <summary>Adds two usages (counts only).</summary>
    public static TokenUsage operator +(TokenUsage a, TokenUsage b) => new()
    {
        InputTokens = a.InputTokens + b.InputTokens,
        OutputTokens = a.OutputTokens + b.OutputTokens,
        TotalTokens = a.TotalTokens + b.TotalTokens,
    };
}

/// <summary>Options for <see cref="AgentsClient.InvokeAsync(string, string, InvokeOptions?, CancellationToken)"/>.</summary>
public sealed record InvokeOptions
{
    /// <summary>Values for <c>{{placeholders}}</c> in the agent's prompt.</summary>
    public IReadOnlyDictionary<string, string>? Variables { get; init; }

    /// <summary>Ask the model for extended thinking (managed agents).</summary>
    public bool ThinkingEnabled { get; init; }
}

/// <summary>The result of invoking an agent.</summary>
public sealed record InvokeResult
{
    /// <summary>Agent name.</summary>
    public string? Agent { get; init; }

    /// <summary>The agent's final answer.</summary>
    public string Response { get; init; } = "";

    /// <summary>Trace id; look it up with the traces API or dashboard.</summary>
    public string? TraceId { get; init; }

    /// <summary>Agentic-loop turns used.</summary>
    public int? Turns { get; init; }

    /// <summary>Token usage.</summary>
    public TokenUsage? Usage { get; init; }

    /// <summary>End-to-end latency in milliseconds.</summary>
    public long? LatencyMs { get; init; }

    /// <summary>Turn-by-turn trace. Present for agents run by the built-in executor.</summary>
    public ExecutionTrace? ExecutionTrace { get; init; }

    /// <summary>Fields this SDK version does not model.</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; set; }

    /// <inheritdoc />
    public override string ToString() => Response;
}

/// <summary>The result of the one-shot <c>/test</c> endpoint.</summary>
public sealed record AgentTestResult
{
    /// <summary>Agent name.</summary>
    public string? Agent { get; init; }

    /// <summary>The model's answer.</summary>
    public string Response { get; init; } = "";

    /// <summary>Provider (or runtime, for framework-native agents).</summary>
    public string? Provider { get; init; }

    /// <summary>Model.</summary>
    public string? Model { get; init; }

    /// <summary>Token usage.</summary>
    public TokenUsage? Usage { get; init; }

    /// <summary>Latency in milliseconds.</summary>
    public long? LatencyMs { get; init; }

    /// <summary>Trace id.</summary>
    public string? TraceId { get; init; }

    /// <summary>Turns, when tools were enabled.</summary>
    public int? Turns { get; init; }

    /// <summary>Extended-thinking blocks.</summary>
    public IReadOnlyList<ThinkingBlock>? ThinkingBlocks { get; init; }

    /// <summary>Turn-by-turn trace, when tools were enabled.</summary>
    public ExecutionTrace? ExecutionTrace { get; init; }

    /// <summary>Fields this SDK version does not model.</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

/// <summary>The turns of an agentic loop.</summary>
public sealed record ExecutionTrace
{
    /// <summary>Trace id.</summary>
    public string? TraceId { get; init; }

    /// <summary>Session id, for session messages.</summary>
    public string? SessionId { get; init; }

    /// <summary>Agent name.</summary>
    public string? AgentName { get; init; }

    /// <summary>Kitchen.</summary>
    public string? Kitchen { get; init; }

    /// <summary>Turns in order.</summary>
    public IReadOnlyList<Turn>? Turns { get; init; }

    /// <summary>Total time in milliseconds.</summary>
    public long? TotalMs { get; init; }

    /// <summary>Total usage.</summary>
    public TokenUsage? Usage { get; init; }
}

/// <summary>One model round-trip in an agentic loop.</summary>
public sealed record Turn
{
    /// <summary>1-based turn number.</summary>
    public int Number { get; init; }

    /// <summary>Messages sent to the model.</summary>
    public IReadOnlyList<ConversationMessage>? Request { get; init; }

    /// <summary>Text the model returned.</summary>
    public string? Response { get; init; }

    /// <summary>Tools the model asked to call.</summary>
    public IReadOnlyList<ToolCall>? ToolCalls { get; init; }

    /// <summary>Results of those calls.</summary>
    public IReadOnlyList<ToolResult>? ToolResults { get; init; }

    /// <summary>Extended-thinking blocks.</summary>
    public IReadOnlyList<ThinkingBlock>? ThinkingBlocks { get; init; }

    /// <summary>Latency in milliseconds.</summary>
    public long? LatencyMs { get; init; }

    /// <summary>Usage of this turn.</summary>
    public TokenUsage? Usage { get; init; }
}

/// <summary>A tool call requested by the model.</summary>
public sealed record ToolCall
{
    /// <summary>Call id.</summary>
    public string? Id { get; init; }

    /// <summary>Tool name.</summary>
    public string Name { get; init; } = "";

    /// <summary>Arguments.</summary>
    public JsonObject? Arguments { get; init; }
}

/// <summary>The result of a tool call.</summary>
public sealed record ToolResult
{
    /// <summary>The call it answers.</summary>
    public string? ToolCallId { get; init; }

    /// <summary>Tool name.</summary>
    public string? Name { get; init; }

    /// <summary>Result text.</summary>
    public string? Content { get; init; }

    /// <summary>Whether the tool failed.</summary>
    public bool IsError { get; init; }
}

/// <summary>A chat message as the control plane records it.</summary>
public sealed record ConversationMessage
{
    /// <summary>system, user, assistant or tool.</summary>
    public string Role { get; init; } = "";

    /// <summary>Text content.</summary>
    public string? Content { get; init; }

    /// <summary>Tool call id, for tool messages.</summary>
    public string? ToolCallId { get; init; }

    /// <summary>Author name.</summary>
    public string? Name { get; init; }

    /// <summary>Fields this SDK version does not model (content parts, tool calls, cache control).</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

/// <summary>Extended-thinking output.</summary>
public sealed record ThinkingBlock
{
    /// <summary>Thinking text.</summary>
    public string? Content { get; init; }

    /// <summary>Tokens used.</summary>
    public long? TokenCount { get; init; }

    /// <summary>Model.</summary>
    public string? Model { get; init; }

    /// <summary>Provider.</summary>
    public string? Provider { get; init; }

    /// <summary>When it was produced.</summary>
    public DateTimeOffset? Timestamp { get; init; }
}

/// <summary>Accepted bake: the agent is now <see cref="AgentStatus.Baking"/>.</summary>
public sealed record BakeResult
{
    /// <summary>Agent name.</summary>
    public string Name { get; init; } = "";

    /// <summary>Version being baked.</summary>
    public string? Version { get; init; }

    /// <summary>Environment, if any.</summary>
    public string? Environment { get; init; }

    /// <summary>Always <c>baking</c>; poll the agent (or use <c>WaitUntilReadyAsync</c>) for the outcome.</summary>
    public AgentStatus? Status { get; init; }

    /// <summary>Path of the agent card.</summary>
    public string? AgentCard { get; init; }

    /// <summary>Whether this was a re-cook.</summary>
    [JsonPropertyName("re_cooked")]
    public bool? Recooked { get; init; }
}

/// <summary>Options for baking an agent.</summary>
public sealed record BakeOptions
{
    /// <summary>Version to bake. Default: the latest.</summary>
    public string? Version { get; init; }

    /// <summary>Environment slug (Pro).</summary>
    public string? Environment { get; init; }
}

/// <summary>Changes applied by a re-cook. Unset properties keep their current value.</summary>
public sealed record RecookOptions
{
    /// <summary>New description.</summary>
    public string? Description { get; init; }

    /// <summary>New framework label.</summary>
    public AgentFramework? Framework { get; init; }

    /// <summary>New model provider.</summary>
    public string? ModelProvider { get; init; }

    /// <summary>New model.</summary>
    public string? ModelName { get; init; }

    /// <summary>New backup provider.</summary>
    public string? BackupProvider { get; init; }

    /// <summary>New backup model.</summary>
    public string? BackupModel { get; init; }

    /// <summary>New mode.</summary>
    public AgentMode? Mode { get; init; }

    /// <summary>New max turns.</summary>
    public int? MaxTurns { get; init; }

    /// <summary>Replacement ingredients.</summary>
    public IReadOnlyList<Ingredient>? Ingredients { get; init; }

    /// <summary>Replacement guardrails.</summary>
    public IReadOnlyList<Guardrail>? Guardrails { get; init; }

    /// <summary>Replacement skills.</summary>
    public IReadOnlyList<string>? Skills { get; init; }

    /// <summary>Tags to merge.</summary>
    public IReadOnlyDictionary<string, string>? Tags { get; init; }

    /// <summary>Version to assign. Default: the server bumps the minor version.</summary>
    public string? Version { get; init; }
}

/// <summary>Result of cool / rewarm.</summary>
public sealed record AgentStatusResult
{
    /// <summary>Agent name.</summary>
    public string Name { get; init; } = "";

    /// <summary>New status.</summary>
    public AgentStatus? Status { get; init; }
}

/// <summary>An agent with its ingredients resolved live.</summary>
public sealed record AgentConfig
{
    /// <summary>The agent.</summary>
    public Agent? Agent { get; init; }

    /// <summary>Resolved ingredients.</summary>
    public ResolvedIngredients? Ingredients { get; init; }
}

/// <summary>One line of agent process output.</summary>
public sealed record LogEntry
{
    /// <summary>When it was written.</summary>
    public DateTimeOffset? Timestamp { get; init; }

    /// <summary>stdout or stderr.</summary>
    public string? Stream { get; init; }

    /// <summary>The line.</summary>
    public string Line { get; init; } = "";

    /// <inheritdoc />
    public override string ToString() => Line;
}
