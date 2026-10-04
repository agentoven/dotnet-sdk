using System.Text.Json.Nodes;

namespace AgentOven;

/// <summary>Fluent definition of a <see cref="Recipe"/>. Start with <see cref="Recipe.Create"/>.</summary>
/// <example>
/// <code>
/// var recipe = Recipe.Create("contract-review")
///     .Step("extract",   s => s.Agent("extractor"))
///     .Step("summarize", s => s.Agent("summarizer").DependsOn("extract").Timeout(TimeSpan.FromMinutes(2)))
///     .Step("risk",      s => s.Agent("risk-scorer").DependsOn("extract"))
///     .Step("approve",   s => s.HumanGate(approvers: ["legal@corp.com"]).DependsOn("summarize", "risk"))
///     .Build();
/// </code>
/// </example>
public sealed class RecipeBuilder
{
    private Recipe _recipe;
    private readonly List<Step> _steps = [];

    internal RecipeBuilder(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _recipe = new Recipe(name);
    }

    /// <summary>Sets the description.</summary>
    public RecipeBuilder Describe(string description)
    {
        _recipe = _recipe with { Description = description };
        return this;
    }

    /// <summary>Sets the version.</summary>
    public RecipeBuilder Version(string version)
    {
        _recipe = _recipe with { Version = version };
        return this;
    }

    /// <summary>Sets the environment runs use by default (Pro).</summary>
    public RecipeBuilder DefaultEnvironment(string environment)
    {
        _recipe = _recipe with { DefaultEnvironment = environment };
        return this;
    }

    /// <summary>Adds a step. Pick its kind inside <paramref name="configure"/> (default: <see cref="StepKind.Agent"/>).</summary>
    public RecipeBuilder Step(string name, Action<StepBuilder> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configure);
        if (_steps.Exists(s => s.Name == name))
        {
            throw new ArgumentException($"A step named '{name}' already exists.", nameof(name));
        }

        var builder = new StepBuilder(name);
        configure(builder);
        _steps.Add(builder.Build());
        return this;
    }

    /// <summary>Adds a ready-made step.</summary>
    public RecipeBuilder Step(Step step)
    {
        ArgumentNullException.ThrowIfNull(step);
        _steps.Add(step);
        return this;
    }

    /// <summary>
    /// Returns the immutable <see cref="Recipe"/>, after checking that every <see cref="Step.DependsOn"/>
    /// and branch target names a step of this recipe.
    /// </summary>
    public Recipe Build()
    {
        var names = _steps.Select(s => s.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var step in _steps)
        {
            foreach (var dependency in step.DependsOn ?? [])
            {
                if (!names.Contains(dependency))
                {
                    throw new InvalidOperationException($"Step '{step.Name}' depends on unknown step '{dependency}'.");
                }
            }

            foreach (var target in (step.Branches ?? []).Select(b => b.NextStep).Append(step.DefaultNext))
            {
                if (target is not null && !names.Contains(target))
                {
                    throw new InvalidOperationException($"Step '{step.Name}' branches to unknown step '{target}'.");
                }
            }
        }

        return _recipe with { Steps = [.. _steps] };
    }

    /// <summary>Builds the recipe.</summary>
    public static implicit operator Recipe(RecipeBuilder builder) => builder.Build();
}

/// <summary>Fluent definition of one recipe <see cref="AgentOven.Step"/>.</summary>
public sealed class StepBuilder
{
    private Step _step;
    private readonly List<string> _dependsOn = [];
    private readonly List<StepBranch> _branches = [];
    private JsonObject? _config;

    internal StepBuilder(string name) => _step = new Step(name, StepKind.Agent);

    /// <summary>Runs an agent.</summary>
    public StepBuilder Agent(string agentName) => Kind(StepKind.Agent, _step with { AgentRef = agentName });

    /// <summary>
    /// Pauses the run until someone approves. Restrict approvers by email, role or domain, and set how long
    /// to wait before the gate times out.
    /// </summary>
    public StepBuilder HumanGate(
        IEnumerable<string>? approvers = null, IEnumerable<string>? roles = null, string? domain = null, TimeSpan? maxWait = null)
    {
        if (maxWait is { } wait)
        {
            SetConfig("max_wait_minutes", (int)Math.Ceiling(wait.TotalMinutes));
        }

        return Kind(StepKind.HumanGate, _step with
        {
            ApproverEmails = approvers?.ToList() ?? _step.ApproverEmails,
            ApproverRoles = roles?.ToList() ?? _step.ApproverRoles,
            ApproverDomain = domain ?? _step.ApproverDomain,
        });
    }

    /// <summary>Evaluates an expression against the run state.</summary>
    public StepBuilder Evaluator(string expression)
    {
        SetConfig("expression", expression);
        return Kind(StepKind.Evaluator, _step);
    }

    /// <summary>Branches on conditions; add branches with <see cref="When"/> and <see cref="Otherwise"/>.</summary>
    public StepBuilder Condition(string? expression = null)
    {
        if (expression is not null)
        {
            SetConfig("expression", expression);
        }

        return Kind(StepKind.Condition, _step);
    }

    /// <summary>Routes to one of several steps; add routes with <see cref="When"/> and <see cref="Otherwise"/>.</summary>
    public StepBuilder Router() => Kind(StepKind.Router, _step);

    /// <summary>Fans out to the steps that depend on this one.</summary>
    public StepBuilder FanOut() => Kind(StepKind.FanOut, _step);

    /// <summary>Waits for its dependencies and joins their outputs.</summary>
    public StepBuilder FanIn() => Kind(StepKind.FanIn, _step);

    /// <summary>Retrieves context from the knowledge base.</summary>
    public StepBuilder Rag(string? questionFrom = null, int? topK = null, string? @namespace = null, string? strategy = null)
    {
        if (questionFrom is not null)
        {
            SetConfig("question_from", questionFrom);
        }

        if (topK is not null)
        {
            SetConfig("top_k", topK.Value);
        }

        if (@namespace is not null)
        {
            SetConfig("namespace", @namespace);
        }

        if (strategy is not null)
        {
            SetConfig("strategy", strategy);
        }

        return Kind(StepKind.Rag, _step);
    }

    /// <summary>Runs an agent for every item at <paramref name="sourcePath"/>.</summary>
    public StepBuilder Map(string sourcePath, string agentName, int? maxConcurrency = null) =>
        Kind(StepKind.Map, _step with { SourcePath = sourcePath, AgentRef = agentName, MaxConcurrency = maxConcurrency });

    /// <summary>Runs another recipe.</summary>
    public StepBuilder SubRecipe(string recipeName) => Kind(StepKind.SubRecipe, _step with { RecipeRef = recipeName });

    /// <summary>Waits for these steps first.</summary>
    public StepBuilder DependsOn(params string[] steps)
    {
        _dependsOn.AddRange(steps);
        return this;
    }

    /// <summary>Adds a branch to a condition or router step.</summary>
    public StepBuilder When(string condition, string nextStep)
    {
        _branches.Add(new StepBranch(condition, nextStep));
        return this;
    }

    /// <summary>Step taken when no branch matches.</summary>
    public StepBuilder Otherwise(string nextStep)
    {
        _step = _step with { DefaultNext = nextStep };
        return this;
    }

    /// <summary>Repeats the step while <paramref name="condition"/> holds.</summary>
    public StepBuilder LoopWhile(string condition, int maxIterations)
    {
        _step = _step with { LoopCondition = condition, MaxIterations = maxIterations };
        return this;
    }

    /// <summary>Fails the step after this long (whole seconds).</summary>
    public StepBuilder Timeout(TimeSpan timeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        _step = _step with { TimeoutSeconds = (int)Math.Ceiling(timeout.TotalSeconds) };
        return this;
    }

    /// <summary>Retries on failure with exponential backoff (1 s, 2 s, 4 s, …).</summary>
    public StepBuilder Retry(int maxRetries)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxRetries);
        _step = _step with { MaxRetries = maxRetries };
        return this;
    }

    /// <summary>Calls these notification tools when the step runs.</summary>
    public StepBuilder Notify(params string[] tools)
    {
        _step = _step with { NotifyTools = [.. _step.NotifyTools ?? [], .. tools] };
        return this;
    }

    /// <summary>Maps fields of the run state into this step's input.</summary>
    public StepBuilder MapInput(string target, string source)
    {
        var mapping = new Dictionary<string, string>(_step.InputMapping ?? new Dictionary<string, string>()) { [target] = source };
        _step = _step with { InputMapping = mapping };
        return this;
    }

    /// <summary>Maps fields of this step's output back into the run state.</summary>
    public StepBuilder MapOutput(string target, string source)
    {
        var mapping = new Dictionary<string, string>(_step.OutputMapping ?? new Dictionary<string, string>()) { [target] = source };
        _step = _step with { OutputMapping = mapping };
        return this;
    }

    /// <summary>Sets a raw config key.</summary>
    public StepBuilder Config(string key, JsonNode? value)
    {
        (_config ??= [])[key] = value;
        return this;
    }

    internal Step Build() => _step with
    {
        DependsOn = _dependsOn.Count > 0 ? [.. _dependsOn.Distinct(StringComparer.Ordinal)] : null,
        Branches = _branches.Count > 0 ? [.. _branches] : null,
        Config = _config,
    };

    private StepBuilder Kind(StepKind kind, Step step)
    {
        _step = step with { Kind = kind };
        return this;
    }

    private void SetConfig(string key, string value) => (_config ??= [])[key] = value;

    private void SetConfig(string key, int value) => (_config ??= [])[key] = value;
}
