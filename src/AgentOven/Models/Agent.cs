using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentOven;

/// <summary>
/// An agent registered in AgentOven: a versioned, deployable AI unit.
/// Build one with <see cref="Create"/> or an object initializer.
/// </summary>
/// <remarks>
/// Properties the server assigns (<see cref="Id"/>, <see cref="Status"/>, <see cref="Kitchen"/>,
/// timestamps, <see cref="A2AEndpoint"/>, <see cref="ResolvedConfig"/>, <see cref="Process"/>) are
/// ignored when registering.
/// </remarks>
public sealed record Agent
{
    /// <summary>Creates an agent with the given name.</summary>
    public Agent(string name) => Name = name;

    /// <summary>Starts a fluent definition: <c>Agent.Create("summarizer").UseModel("gpt-4o", "openai").Build()</c>.</summary>
    public static AgentBuilder Create(string name) => new(name);

    /// <summary>Unique name within the kitchen.</summary>
    public string Name { get; init; }

    /// <summary>Server-assigned identifier.</summary>
    public string? Id { get; init; }

    /// <summary>What the agent does.</summary>
    public string? Description { get; init; }

    /// <summary>Framework label.</summary>
    public AgentFramework? Framework { get; init; }

    /// <summary><see cref="AgentMode.Managed"/> (default) or <see cref="AgentMode.External"/>.</summary>
    public AgentMode? Mode { get; init; }

    /// <summary>Lifecycle state.</summary>
    public AgentStatus? Status { get; init; }

    /// <summary>Kitchen the agent belongs to.</summary>
    public string? Kitchen { get; init; }

    /// <summary>Semantic version. The server defaults it to <c>0.1.0</c>.</summary>
    public string? Version { get; init; }

    /// <summary>Display order within the kitchen.</summary>
    public int? Order { get; init; }

    /// <summary>Where the agent process runs.</summary>
    public ExecutionMode? ExecutionMode { get; init; }

    /// <summary>Which framework runs the loop of a managed agent.</summary>
    public AgentRuntime? Runtime { get; init; }

    /// <summary>Command that starts your own agent process, for a non-<c>agentoven</c> runtime.</summary>
    public string? Entrypoint { get; init; }

    /// <summary>Git repository cloned before starting the process (local execution).</summary>
    public string? RepoUrl { get; init; }

    /// <summary>Branch or tag of <see cref="RepoUrl"/>.</summary>
    public string? RepoBranch { get; init; }

    /// <summary>Maximum agentic-loop turns. The server defaults it to 10.</summary>
    public int? MaxTurns { get; init; }

    /// <summary>Reactive or agentic behavior.</summary>
    public AgentBehavior? Behavior { get; init; }

    /// <summary>Token budget of the sliding context window.</summary>
    public int? ContextBudget { get; init; }

    /// <summary>Cheap model used for context compression.</summary>
    public string? SummaryModel { get; init; }

    /// <summary>Reasoning strategy.</summary>
    public ReasoningStrategy? ReasoningStrategy { get; init; }

    /// <summary>Stable A2A URL on the control plane (server-assigned).</summary>
    [JsonPropertyName("a2a_endpoint")]
    public string? A2AEndpoint { get; init; }

    /// <summary>Backend URL of an external agent or its process.</summary>
    public string? BackendEndpoint { get; init; }

    /// <summary>Per-environment backend endpoints.</summary>
    public IReadOnlyDictionary<string, string>? EnvEndpoints { get; init; }

    /// <summary>Skills advertised on the agent card.</summary>
    public IReadOnlyList<string>? Skills { get; init; }

    /// <summary>Name of the primary model provider (as registered with the control plane).</summary>
    public string? ModelProvider { get; init; }

    /// <summary>Primary model name.</summary>
    public string? ModelName { get; init; }

    /// <summary>Provider used when the primary fails.</summary>
    public string? BackupProvider { get; init; }

    /// <summary>Model used when the primary fails.</summary>
    public string? BackupModel { get; init; }

    /// <summary>Input/output guardrails.</summary>
    public IReadOnlyList<Guardrail>? Guardrails { get; init; }

    /// <summary>Models, tools, prompts and data sources.</summary>
    public IReadOnlyList<Ingredient>? Ingredients { get; init; }

    /// <summary>Key/value tags. When an agent is <see cref="AgentStatus.Burnt"/>, <c>tags["error"]</c> holds the reason.</summary>
    public IReadOnlyDictionary<string, string>? Tags { get; init; }

    /// <summary>Ingredients as resolved at bake time.</summary>
    public ResolvedIngredients? ResolvedConfig { get; init; }

    /// <summary>The running process, if any.</summary>
    public ProcessInfo? Process { get; init; }

    /// <summary>Who created the agent.</summary>
    public string? CreatedBy { get; init; }

    /// <summary>When this version was created.</summary>
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>When this version was last updated.</summary>
    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary><see cref="CreatedAt"/> as Unix seconds.</summary>
    public long? CreatedEpoch { get; init; }

    /// <summary>The reason a <see cref="AgentStatus.Burnt"/> agent failed to bake, if any.</summary>
    [JsonIgnore]
    public string? BurntReason => Tags is not null && Tags.TryGetValue("error", out var reason) ? reason : null;

    /// <summary>Fields this SDK version does not model.</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

/// <summary>A process the control plane started for an agent.</summary>
public sealed record ProcessInfo
{
    /// <summary>Agent name.</summary>
    public string? AgentName { get; init; }

    /// <summary>Kitchen.</summary>
    public string? Kitchen { get; init; }

    /// <summary>Local, Docker or Kubernetes.</summary>
    public ExecutionMode? Mode { get; init; }

    /// <summary>Process state.</summary>
    public ProcessStatus? Status { get; init; }

    /// <summary>Listening port.</summary>
    public int? Port { get; init; }

    /// <summary>Base URL of the process.</summary>
    public string? Endpoint { get; init; }

    /// <summary>OS process id (local mode).</summary>
    public int? Pid { get; init; }

    /// <summary>Container id (Docker mode).</summary>
    public string? ContainerId { get; init; }

    /// <summary>Pod name (Kubernetes mode).</summary>
    public string? PodName { get; init; }

    /// <summary>Failure reason.</summary>
    public string? Error { get; init; }

    /// <summary>Start time.</summary>
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>Fields this SDK version does not model.</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; set; }
}
