using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace AgentOven;

/// <summary>A tool in the kitchen's MCP tool catalog. Agents use it through a tool ingredient.</summary>
public sealed record McpTool
{
    /// <summary>Creates a tool definition.</summary>
    public McpTool(string name, string endpoint)
    {
        Name = name;
        Endpoint = endpoint;
    }

    /// <summary>Tool name (what the model calls).</summary>
    public string Name { get; init; }

    /// <summary>MCP endpoint.</summary>
    public string Endpoint { get; init; }

    /// <summary>Server-assigned identifier.</summary>
    public string? Id { get; init; }

    /// <summary>Description shown to the model.</summary>
    public string? Description { get; init; }

    /// <summary>Kitchen.</summary>
    public string? Kitchen { get; init; }

    /// <summary><c>http</c> (default), <c>sse</c> or <c>stdio</c>. Tools with the <c>mcp</c> transport are created by skills.</summary>
    public string Transport { get; init; } = "http";

    /// <summary>Name of the tool on a remote MCP server (<c>mcp</c> transport).</summary>
    public string? RemoteName { get; init; }

    /// <summary>The skill that registered the tool, if any.</summary>
    public string? Skill { get; init; }

    /// <summary>JSON Schema of the arguments.</summary>
    public JsonObject? Schema { get; init; }

    /// <summary>
    /// Authentication, by reference to a kitchen credential:
    /// <c>{"type":"bearer","credential_ref":"name"}</c> or <c>{"type":"api-key","credential_ref":"name","header":"X-Api-Key"}</c>.
    /// Use <see cref="ToolAuth"/> to build it. The server redacts everything else on reads.
    /// </summary>
    public JsonObject? AuthConfig { get; init; }

    /// <summary><c>tool</c>, <c>notify</c>, or both.</summary>
    public IReadOnlyList<string>? Capabilities { get; init; }

    /// <summary>Whether the tool is enabled. Always sent.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public bool Enabled { get; init; } = true;

    /// <summary>Creation time.</summary>
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>Last update.</summary>
    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>Fields this SDK version does not model.</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

/// <summary>Builders for <see cref="McpTool.AuthConfig"/>: tool credentials by reference, resolved per call.</summary>
public static class ToolAuth
{
    /// <summary><c>Authorization: Bearer</c> with the value of a kitchen credential.</summary>
    public static JsonObject Bearer(string credentialRef) => new() { ["type"] = "bearer", ["credential_ref"] = credentialRef };

    /// <summary>An API key header with the value of a kitchen credential.</summary>
    public static JsonObject ApiKey(string credentialRef, string header = "X-Api-Key") =>
        new() { ["type"] = "api-key", ["credential_ref"] = credentialRef, ["header"] = header };
}

/// <summary>Result of a bulk tool registration.</summary>
public sealed record BulkToolResult
{
    /// <summary>Tools created.</summary>
    public int Created { get; init; }

    /// <summary>The tools.</summary>
    public IReadOnlyList<McpTool> Tools { get; init; } = [];
}

/// <summary>A versioned prompt template in the kitchen's prompt store.</summary>
public sealed record Prompt
{
    /// <summary>Creates a prompt.</summary>
    public Prompt(string name, string template)
    {
        Name = name;
        Template = template;
    }

    /// <summary>Name.</summary>
    public string Name { get; init; }

    /// <summary>Template text with <c>{{variable}}</c> placeholders.</summary>
    public string Template { get; init; }

    /// <summary>Server-assigned identifier.</summary>
    public string? Id { get; init; }

    /// <summary>Version (the server increments it on update).</summary>
    public int? Version { get; init; }

    /// <summary>Placeholders found in the template.</summary>
    public IReadOnlyList<string>? Variables { get; init; }

    /// <summary>Kitchen.</summary>
    public string? Kitchen { get; init; }

    /// <summary>Tags.</summary>
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>Creation time.</summary>
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>Last update.</summary>
    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>Fields this SDK version does not model.</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

/// <summary>A prompt's security and quality review.</summary>
public sealed record PromptValidationReport
{
    /// <summary>Prompt name.</summary>
    public string? PromptName { get; init; }

    /// <summary>Version reviewed.</summary>
    public int Version { get; init; }

    /// <summary>0–100.</summary>
    public int Score { get; init; }

    /// <summary>Findings.</summary>
    public IReadOnlyList<ValidationIssue> Issues { get; init; } = [];

    /// <summary>Token count.</summary>
    public int? TokenCount { get; init; }

    /// <summary>Compatibility per model.</summary>
    public IReadOnlyDictionary<string, bool>? ModelCompat { get; init; }

    /// <summary>LLM-as-judge summary (Pro).</summary>
    [JsonPropertyName("llm_analysis")]
    public string? LlmAnalysis { get; init; }

    /// <summary>When.</summary>
    public DateTimeOffset? ValidatedAt { get; init; }

    /// <summary><c>community</c> or <c>pro</c>.</summary>
    public string? ValidatedBy { get; init; }
}

/// <summary>One finding of a validation.</summary>
public sealed record ValidationIssue
{
    /// <summary>error, warning or info.</summary>
    public string Severity { get; init; } = "";

    /// <summary>Category.</summary>
    public string? Category { get; init; }

    /// <summary>Message.</summary>
    public string Message { get; init; } = "";

    /// <summary>Line.</summary>
    public int? Line { get; init; }

    /// <summary>Position.</summary>
    public int? Position { get; init; }

    /// <summary>Suggested fix.</summary>
    public string? Suggestion { get; init; }
}

/// <summary>What the model catalog knows about a model.</summary>
public sealed record ModelCapability
{
    /// <summary>Canonical id, e.g. <c>openai/gpt-5-mini</c>.</summary>
    public string ModelId { get; init; } = "";

    /// <summary>Provider kind.</summary>
    public ProviderKind? ProviderKind { get; init; }

    /// <summary>Provider-specific model name.</summary>
    public string? ModelName { get; init; }

    /// <summary>Display name.</summary>
    public string? DisplayName { get; init; }

    /// <summary>Max input tokens.</summary>
    public int? ContextWindow { get; init; }

    /// <summary>Max output tokens.</summary>
    public int? MaxOutputTokens { get; init; }

    /// <summary>USD per 1K input tokens.</summary>
    [JsonPropertyName("input_cost_per_1k")]
    public double? InputCostPer1K { get; init; }

    /// <summary>USD per 1K output tokens.</summary>
    [JsonPropertyName("output_cost_per_1k")]
    public double? OutputCostPer1K { get; init; }

    /// <summary>Tool calling.</summary>
    public bool? SupportsTools { get; init; }

    /// <summary>Images.</summary>
    public bool? SupportsVision { get; init; }

    /// <summary>Streaming.</summary>
    public bool? SupportsStreaming { get; init; }

    /// <summary>Extended thinking.</summary>
    public bool? SupportsThinking { get; init; }

    /// <summary>Structured JSON output.</summary>
    [JsonPropertyName("supports_json")]
    public bool? SupportsJson { get; init; }

    /// <summary>Modalities.</summary>
    public IReadOnlyList<string>? Modalities { get; init; }

    /// <summary>Deprecation date.</summary>
    public string? DeprecatedAt { get; init; }

    /// <summary>catalog, discovery or manual.</summary>
    public string? Source { get; init; }
}

/// <summary>A kitchen's settings (prompt validation, retention, approval requirements).</summary>
public sealed record KitchenSettings
{
    /// <summary>Kitchen id.</summary>
    public string? KitchenId { get; init; }

    /// <summary>Provider used for LLM-based prompt validation.</summary>
    public string? ValidationProvider { get; init; }

    /// <summary>Model used for prompt validation.</summary>
    public string? ValidationModel { get; init; }

    /// <summary>API key for prompt validation.</summary>
    [JsonPropertyName("validation_api_key")]
    public string? ValidationApiKey { get; init; }

    /// <summary>Endpoint for prompt validation.</summary>
    public string? ValidationEndpoint { get; init; }

    /// <summary>Patterns rejected in prompts.</summary>
    public IReadOnlyList<string>? DenyPatterns { get; init; }

    /// <summary>Maximum template size.</summary>
    public int? MaxTemplateSize { get; init; }

    /// <summary>Validate prompts automatically. Always sent.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public bool AutoValidate { get; init; }

    /// <summary>Require approval for prompt changes.</summary>
    public bool? RequireApproval { get; init; }

    /// <summary>Require an audit record of extended thinking.</summary>
    public bool? RequireThinkingAudit { get; init; }

    /// <summary>Output retention in days.</summary>
    public int? MaxOutputRetentionDays { get; init; }

    /// <summary>Audit retention in days.</summary>
    public int? MaxAuditRetentionDays { get; init; }

    /// <summary>Fields this SDK version does not model.</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

/// <summary>An approval request for a recipe's human gate.</summary>
public sealed record ApprovalRecord
{
    /// <summary>Id.</summary>
    public string Id { get; init; } = "";

    /// <summary><c>runId:stepName</c>.</summary>
    public string GateKey { get; init; } = "";

    /// <summary>Run id.</summary>
    public string RunId { get; init; } = "";

    /// <summary>Step name.</summary>
    public string StepName { get; init; } = "";

    /// <summary>Kitchen.</summary>
    public string? Kitchen { get; init; }

    /// <summary>pending/waiting, approved, rejected, timed_out/expired.</summary>
    public string Status { get; init; } = "";

    /// <summary>Approver id.</summary>
    public string? ApproverId { get; init; }

    /// <summary>Approver email.</summary>
    public string? ApproverEmail { get; init; }

    /// <summary>api, slack, teams or email.</summary>
    public string? ApproverChannel { get; init; }

    /// <summary>Comments.</summary>
    public string? Comments { get; init; }

    /// <summary>Metadata.</summary>
    public JsonObject? Metadata { get; init; }

    /// <summary>When it was requested.</summary>
    public DateTimeOffset? RequestedAt { get; init; }

    /// <summary>When it was decided.</summary>
    public DateTimeOffset? ResolvedAt { get; init; }

    /// <summary>Timeout.</summary>
    public int? MaxWaitMinutes { get; init; }
}

/// <summary>A decision on a recipe's human gate.</summary>
public sealed record GateApproval(bool Approved)
{
    /// <summary>Approver id (default: the caller's identity).</summary>
    public string? ApproverId { get; init; }

    /// <summary>Approver email.</summary>
    public string? ApproverEmail { get; init; }

    /// <summary>Channel the decision came from.</summary>
    public string? Channel { get; init; }

    /// <summary>Comments.</summary>
    public string? Comments { get; init; }
}

/// <summary>A key scoped to a kitchen and some agents, for handing agents to apps and partners.</summary>
public sealed record ScopedKey
{
    /// <summary>Id.</summary>
    public string Id { get; init; } = "";

    /// <summary>The key itself. Returned once, on creation; store it securely.</summary>
    public string? Key { get; init; }

    /// <summary>First characters, for identification.</summary>
    public string? KeyPrefix { get; init; }

    /// <summary>Label.</summary>
    public string? Label { get; init; }

    /// <summary>Kitchen.</summary>
    public string? Kitchen { get; init; }

    /// <summary>Agents it may invoke (<c>*</c> = all).</summary>
    public IReadOnlyList<string>? AgentNames { get; init; }

    /// <summary>Environments it may call (<c>*</c> = all).</summary>
    public IReadOnlyList<string>? EnvironmentNames { get; init; }

    /// <summary>Call quota (0 = unlimited).</summary>
    public int MaxCalls { get; init; }

    /// <summary>Calls so far.</summary>
    public int CallCount { get; init; }

    /// <summary>Role.</summary>
    public string? Role { get; init; }

    /// <summary>Expiry.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Revoked.</summary>
    public bool Revoked { get; init; }

    /// <summary>Creator.</summary>
    public string? CreatedBy { get; init; }

    /// <summary>Creation time.</summary>
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>Fields this SDK version does not model.</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

/// <summary>Usage of a scoped key.</summary>
public sealed record ScopedKeyUsage
{
    /// <summary>Id.</summary>
    public string Id { get; init; } = "";

    /// <summary>Label.</summary>
    public string? Label { get; init; }

    /// <summary>Calls so far.</summary>
    public int CallCount { get; init; }

    /// <summary>Quota.</summary>
    public int MaxCalls { get; init; }

    /// <summary>Calls left (when there is a quota).</summary>
    public int? RemainingCalls { get; init; }

    /// <summary>Revoked.</summary>
    public bool Revoked { get; init; }

    /// <summary>Expired.</summary>
    public bool Expired { get; init; }

    /// <summary>Quota exceeded.</summary>
    public bool QuotaExceeded { get; init; }

    /// <summary>Fields this SDK version does not model.</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; set; }
}
