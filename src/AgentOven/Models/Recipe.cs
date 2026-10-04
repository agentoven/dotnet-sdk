using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace AgentOven;

/// <summary>
/// A workflow of steps (agents, human gates, conditions, fan-out, …) wired as a DAG through
/// <see cref="Step.DependsOn"/>. Steps whose dependencies are satisfied run in parallel.
/// Build one with <see cref="Create"/>.
/// </summary>
public sealed record Recipe
{
    /// <summary>Creates a recipe. Names must match <c>^[a-z0-9][a-z0-9_-]{1,62}$</c>.</summary>
    public Recipe(string name) => Name = name;

    /// <summary>Starts a fluent definition.</summary>
    public static RecipeBuilder Create(string name) => new(name);

    /// <summary>Name, unique within the kitchen.</summary>
    public string Name { get; init; }

    /// <summary>Server-assigned identifier.</summary>
    public string? Id { get; init; }

    /// <summary>Description.</summary>
    public string? Description { get; init; }

    /// <summary>Kitchen.</summary>
    public string? Kitchen { get; init; }

    /// <summary>Steps.</summary>
    public IReadOnlyList<Step>? Steps { get; init; }

    /// <summary>Version.</summary>
    public string? Version { get; init; }

    /// <summary>Environment runs use by default (Pro).</summary>
    public string? DefaultEnvironment { get; init; }

    /// <summary>Creator.</summary>
    public string? CreatedBy { get; init; }

    /// <summary>Creation time.</summary>
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>Last update.</summary>
    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>Fields this SDK version does not model.</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

/// <summary>One step of a <see cref="Recipe"/>.</summary>
public sealed record Step
{
    /// <summary>Creates a step.</summary>
    public Step(string name, StepKind kind)
    {
        Name = name;
        Kind = kind;
    }

    /// <summary>Step name, unique within the recipe.</summary>
    public string Name { get; init; }

    /// <summary>Kind.</summary>
    public StepKind Kind { get; init; }

    /// <summary>Agent run by an <see cref="StepKind.Agent"/> step.</summary>
    public string? AgentRef { get; init; }

    /// <summary>Steps that must finish first.</summary>
    public IReadOnlyList<string>? DependsOn { get; init; }

    /// <summary>Kind-specific config (<c>expression</c>, <c>max_wait_minutes</c>, <c>top_k</c>, …).</summary>
    public JsonObject? Config { get; init; }

    /// <summary>Retries on failure, with exponential backoff (1 s, 2 s, 4 s, …).</summary>
    public int? MaxRetries { get; init; }

    /// <summary>Timeout in seconds.</summary>
    [JsonPropertyName("timeout_secs")]
    public int? TimeoutSeconds { get; init; }

    /// <summary>Branches of a <see cref="StepKind.Condition"/> or <see cref="StepKind.Router"/> step.</summary>
    public IReadOnlyList<StepBranch>? Branches { get; init; }

    /// <summary>Step taken when no branch matches.</summary>
    public string? DefaultNext { get; init; }

    /// <summary>Loop condition.</summary>
    public string? LoopCondition { get; init; }

    /// <summary>Loop limit.</summary>
    public int? MaxIterations { get; init; }

    /// <summary>Input path a <see cref="StepKind.Map"/> step iterates over.</summary>
    public string? SourcePath { get; init; }

    /// <summary>Parallelism of a <see cref="StepKind.Map"/> step.</summary>
    public int? MaxConcurrency { get; init; }

    /// <summary>Recipe run by a <see cref="StepKind.SubRecipe"/> step.</summary>
    public string? RecipeRef { get; init; }

    /// <summary>Maps parent fields into the step's input.</summary>
    public IReadOnlyDictionary<string, string>? InputMapping { get; init; }

    /// <summary>Maps step output fields back.</summary>
    public IReadOnlyDictionary<string, string>? OutputMapping { get; init; }

    /// <summary>Notification tools called when the step runs (for example, a human gate).</summary>
    public IReadOnlyList<string>? NotifyTools { get; init; }

    /// <summary>Auth key for the step.</summary>
    public string? AuthKey { get; init; }

    /// <summary>Credential reference for the step's auth key.</summary>
    public string? AuthKeyRef { get; init; }

    /// <summary>Who may approve a human gate, by email.</summary>
    public IReadOnlyList<string>? ApproverEmails { get; init; }

    /// <summary>Who may approve a human gate, by role.</summary>
    public IReadOnlyList<string>? ApproverRoles { get; init; }

    /// <summary>Who may approve a human gate, by email domain.</summary>
    public string? ApproverDomain { get; init; }

    /// <summary>Approver must belong to the recipe's kitchen.</summary>
    public bool? RequireSameTenant { get; init; }

    /// <summary>Fields this SDK version does not model.</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

/// <summary>A conditional branch.</summary>
/// <param name="Condition">Expression evaluated against the run state.</param>
/// <param name="NextStep">Step to run when it holds.</param>
public sealed record StepBranch(string Condition, string NextStep);

/// <summary>A recipe run has been accepted.</summary>
public sealed record RecipeRunStarted
{
    /// <summary>Recipe name.</summary>
    public string Recipe { get; init; } = "";

    /// <summary>Run id.</summary>
    public string RunId { get; init; } = "";

    /// <summary>Initial status.</summary>
    public RecipeRunStatus? Status { get; init; }

    /// <summary>Path to poll.</summary>
    public string? Poll { get; init; }

    /// <summary>Environment.</summary>
    public string? Environment { get; init; }
}

/// <summary>One execution of a recipe.</summary>
public sealed record RecipeRun
{
    /// <summary>Run id.</summary>
    public string Id { get; init; } = "";

    /// <summary>Recipe id.</summary>
    public string? RecipeId { get; init; }

    /// <summary>Kitchen.</summary>
    public string? Kitchen { get; init; }

    /// <summary>Environment.</summary>
    public string? Environment { get; init; }

    /// <summary>Status.</summary>
    public RecipeRunStatus Status { get; init; }

    /// <summary>Input.</summary>
    public JsonNode? Input { get; init; }

    /// <summary>Output, once completed.</summary>
    public JsonNode? Output { get; init; }

    /// <summary>Per-step results.</summary>
    public IReadOnlyList<StepResult>? StepResults { get; init; }

    /// <summary>Parent run, for sub-recipes.</summary>
    public string? ParentRunId { get; init; }

    /// <summary>Who or what started the run.</summary>
    public string? TriggeredBy { get; init; }

    /// <summary>Start time.</summary>
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>End time.</summary>
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>Duration in milliseconds.</summary>
    public long? DurationMs { get; init; }

    /// <summary>Tokens used.</summary>
    public long? TotalTokens { get; init; }

    /// <summary>Cost in USD.</summary>
    [JsonPropertyName("total_cost_usd")]
    public double? TotalCostUsd { get; init; }

    /// <summary>Failure reason.</summary>
    public string? Error { get; init; }

    /// <summary>Human gates waiting for approval, when <see cref="Status"/> is <see cref="RecipeRunStatus.Paused"/>.</summary>
    [JsonIgnore]
    public IReadOnlyList<string> PendingGates { get; init; } = [];

    /// <summary><see langword="true"/> once the run completed, failed or was canceled.</summary>
    [JsonIgnore]
    public bool IsFinished =>
        Status == RecipeRunStatus.Completed || Status == RecipeRunStatus.Failed || Status == RecipeRunStatus.Canceled;

    /// <summary>Fields this SDK version does not model.</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

/// <summary>The result of one step in a run.</summary>
public sealed record StepResult
{
    /// <summary>Step name.</summary>
    public string StepName { get; init; } = "";

    /// <summary>Step kind.</summary>
    public StepKind? StepKind { get; init; }

    /// <summary>Status.</summary>
    public StepStatus? Status { get; init; }

    /// <summary>Output.</summary>
    public JsonNode? Output { get; init; }

    /// <summary>Agent that ran.</summary>
    public string? AgentRef { get; init; }

    /// <summary>Start time.</summary>
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>Duration in milliseconds.</summary>
    public long? DurationMs { get; init; }

    /// <summary>Failure reason.</summary>
    public string? Error { get; init; }

    /// <summary>Tokens used.</summary>
    public long? Tokens { get; init; }

    /// <summary>Cost in USD.</summary>
    [JsonPropertyName("cost_usd")]
    public double? CostUsd { get; init; }

    /// <summary>Human gate state.</summary>
    public GateStatus? GateStatus { get; init; }

    /// <summary>Branch a condition step took.</summary>
    public string? BranchTaken { get; init; }

    /// <summary>Nested results (map, sub-recipe).</summary>
    public IReadOnlyList<StepResult>? SubResults { get; init; }

    /// <summary>Items a map step processed.</summary>
    public int? ItemCount { get; init; }

    /// <summary>Loop iterations.</summary>
    public int? LoopIterations { get; init; }

    /// <summary>Sub-recipe run id.</summary>
    public string? SubRunId { get; init; }

    /// <summary>Fields this SDK version does not model.</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; set; }
}
