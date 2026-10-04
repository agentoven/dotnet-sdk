namespace AgentOven;

/// <summary>
/// Fluent definition of an <see cref="Agent"/>. Start with <see cref="Agent.Create"/>, finish with <see cref="Build"/>.
/// </summary>
/// <example>
/// <code>
/// var agent = Agent.Create("summarizer")
///     .Describe("Summarizes long documents")
///     .UseModel("gpt-4o", provider: "azure-openai")
///     .WithSystemPrompt("You are a concise summarizer.")
///     .AddTool("doc-reader")
///     .AddGuardrail(Guardrail.Input(GuardrailKind.PiiDetection))
///     .Build();
/// </code>
/// </example>
public sealed class AgentBuilder
{
    /// <summary>Name of the prompt ingredient <see cref="WithSystemPrompt"/> creates.</summary>
    public const string SystemPromptIngredientName = "system-prompt";

    private Agent _agent;
    private readonly List<Ingredient> _ingredients = [];
    private readonly List<Guardrail> _guardrails = [];
    private readonly List<string> _skills = [];
    private readonly Dictionary<string, string> _tags = [];

    internal AgentBuilder(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _agent = new Agent(name);
    }

    /// <summary>Sets the description.</summary>
    public AgentBuilder Describe(string description) => With(_agent with { Description = description });

    /// <summary>Sets the version (semver).</summary>
    public AgentBuilder Version(string version) => With(_agent with { Version = version });

    /// <summary>Sets the framework label.</summary>
    public AgentBuilder Framework(AgentFramework framework) => With(_agent with { Framework = framework });

    /// <summary>
    /// Sets the primary model. <paramref name="provider"/> is the name of a provider registered
    /// with the control plane (see <see cref="ProvidersClient"/>).
    /// </summary>
    public AgentBuilder UseModel(string model, string provider) =>
        With(_agent with { ModelName = model, ModelProvider = provider });

    /// <summary>Sets the failover model.</summary>
    public AgentBuilder WithBackupModel(string model, string provider) =>
        With(_agent with { BackupModel = model, BackupProvider = provider });

    /// <summary>
    /// Sets the system prompt. The control plane takes instructions from a prompt ingredient,
    /// so this adds (or replaces) an inline prompt ingredient named <c>system-prompt</c>.
    /// </summary>
    public AgentBuilder WithSystemPrompt(string text)
    {
        _ingredients.RemoveAll(i => i.Kind == IngredientKind.Prompt && i.Name == SystemPromptIngredientName);
        _ingredients.Add(Ingredient.Prompt(SystemPromptIngredientName, text));
        return this;
    }

    /// <summary>Uses a prompt from the prompt store as the system prompt.</summary>
    public AgentBuilder WithPrompt(string promptName, int? version = null) => AddIngredient(Ingredient.PromptRef(promptName, version));

    /// <summary>Caps the agentic loop.</summary>
    public AgentBuilder WithMaxTurns(int maxTurns)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxTurns);
        return With(_agent with { MaxTurns = maxTurns });
    }

    /// <summary>Sets reactive or agentic behavior.</summary>
    public AgentBuilder WithBehavior(AgentBehavior behavior) => With(_agent with { Behavior = behavior });

    /// <summary>Sets the reasoning strategy.</summary>
    public AgentBuilder WithReasoning(ReasoningStrategy strategy) => With(_agent with { ReasoningStrategy = strategy });

    /// <summary>Sets the context window budget and an optional cheaper model for summarising history.</summary>
    public AgentBuilder WithContextBudget(int tokens, string? summaryModel = null) =>
        With(_agent with { ContextBudget = tokens, SummaryModel = summaryModel ?? _agent.SummaryModel });

    /// <summary>Adds an ingredient.</summary>
    public AgentBuilder AddIngredient(Ingredient ingredient)
    {
        ArgumentNullException.ThrowIfNull(ingredient);
        _ingredients.Add(ingredient);
        return this;
    }

    /// <summary>Adds a tool from the MCP tool catalog.</summary>
    public AgentBuilder AddTool(string name, bool required = true) => AddIngredient(Ingredient.Tool(name, required: required));

    /// <summary>Adds a data source.</summary>
    public AgentBuilder AddData(string name, string uri) => AddIngredient(Ingredient.Data(name, uri));

    /// <summary>Adds a guardrail.</summary>
    public AgentBuilder AddGuardrail(Guardrail guardrail)
    {
        ArgumentNullException.ThrowIfNull(guardrail);
        _guardrails.Add(guardrail);
        return this;
    }

    /// <summary>Advertises a skill on the agent card.</summary>
    public AgentBuilder Skill(string skill)
    {
        _skills.Add(skill);
        return this;
    }

    /// <summary>Adds a tag.</summary>
    public AgentBuilder Tag(string key, string value)
    {
        _tags[key] = value;
        return this;
    }

    /// <summary>
    /// Runs the agent in your own process instead of the built-in executor. The control plane starts
    /// <paramref name="entrypoint"/> (for example <c>dotnet MyAgent.dll</c>) at bake time; build that
    /// process with AgentOven.Runtime.
    /// </summary>
    public AgentBuilder RunsOwnProcess(string entrypoint, AgentRuntime? runtime = null, ExecutionMode? executionMode = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entrypoint);
        return With(_agent with
        {
            Mode = AgentMode.Managed,
            Runtime = runtime ?? AgentRuntime.Custom,
            Entrypoint = entrypoint,
            ExecutionMode = executionMode ?? _agent.ExecutionMode,
        });
    }

    /// <summary>Clones a Git repository before starting the process (local execution).</summary>
    public AgentBuilder FromRepository(string repoUrl, string? branch = null) =>
        With(_agent with { RepoUrl = repoUrl, RepoBranch = branch });

    /// <summary>Sets where the process runs: local, Docker or Kubernetes.</summary>
    public AgentBuilder RunOn(ExecutionMode executionMode) => With(_agent with { ExecutionMode = executionMode });

    /// <summary>
    /// Makes this an external agent: the control plane proxies A2A calls to <paramref name="backendEndpoint"/>.
    /// </summary>
    public AgentBuilder External(string backendEndpoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backendEndpoint);
        return With(_agent with { Mode = AgentMode.External, BackendEndpoint = backendEndpoint });
    }

    /// <summary>Applies any other change: <c>.Configure(a => a with { Order = 3 })</c>.</summary>
    public AgentBuilder Configure(Func<Agent, Agent> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        return With(configure(_agent));
    }

    /// <summary>Returns the immutable <see cref="Agent"/>.</summary>
    public Agent Build() => _agent with
    {
        Ingredients = _ingredients.Count > 0 ? [.. _agent.Ingredients ?? [], .. _ingredients] : _agent.Ingredients,
        Guardrails = _guardrails.Count > 0 ? [.. _agent.Guardrails ?? [], .. _guardrails] : _agent.Guardrails,
        Skills = _skills.Count > 0 ? [.. _agent.Skills ?? [], .. _skills] : _agent.Skills,
        Tags = _tags.Count > 0 ? MergeTags(_agent.Tags, _tags) : _agent.Tags,
    };

    /// <summary>Builds the agent.</summary>
    public static implicit operator Agent(AgentBuilder builder) => builder.Build();

    private AgentBuilder With(Agent agent)
    {
        _agent = agent;
        return this;
    }

    private static Dictionary<string, string> MergeTags(IReadOnlyDictionary<string, string>? existing, Dictionary<string, string> added)
    {
        var merged = existing is null ? new Dictionary<string, string>() : new Dictionary<string, string>(existing);
        foreach (var (key, value) in added)
        {
            merged[key] = value;
        }

        return merged;
    }
}
