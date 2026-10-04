using System.Runtime.CompilerServices;
using System.Text.Json;
using AgentOven.Internal;

namespace AgentOven;

/// <summary>
/// Agent operations: register, bake, invoke, cool, rewarm, re-cook, delete.
/// Get one from <see cref="AgentOvenClient.Agents"/>; index it (<c>oven.Agents["name"]</c>) for a handle bound to one agent.
/// </summary>
public sealed class AgentsClient
{
    private readonly AgentOvenHttp _http;

    internal AgentsClient(AgentOvenHttp http)
    {
        _http = http;
        Sessions = new SessionsClient(http);
    }

    /// <summary>A handle bound to one agent. No network call.</summary>
    public AgentHandle this[string name] => new(this, name);

    /// <summary>Session operations, addressed by agent name.</summary>
    public SessionsClient Sessions { get; }

    // ── Definitions ──────────────────────────────────────────────────────

    /// <summary>Lists agents, optionally filtered by status, tag or environment.</summary>
    public Task<IReadOnlyList<Agent>> ListAsync(
        AgentStatus? status = null, string? tag = null, string? environment = null, CancellationToken cancellationToken = default) =>
        _http.GetAsync(
            ApiPath.WithQuery(ApiPath.Of("agents"), ("status", status?.Value), ("tag", tag), ("environment", environment)),
            AgentOvenJsonContext.Default.IReadOnlyListAgent,
            cancellationToken);

    /// <summary>Gets an agent. Throws <see cref="AgentOvenApiException"/> (404) when it does not exist.</summary>
    public Task<Agent> GetAsync(string name, CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("agents", name), AgentOvenJsonContext.Default.Agent, cancellationToken);

    /// <summary>Gets an agent, or <see langword="null"/> when it does not exist.</summary>
    public async Task<Agent?> FindAsync(string name, CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetAsync(name, cancellationToken).ConfigureAwait(false);
        }
        catch (AgentOvenApiException ex) when (ex.IsNotFound)
        {
            return null;
        }
    }

    /// <summary>
    /// Registers an agent definition (status <see cref="AgentStatus.Draft"/>). Registering an existing name replaces it.
    /// </summary>
    public async Task<Agent> RegisterAsync(Agent agent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agent);
        var created = await _http.PostAsync(
            ApiPath.Of("agents"), agent, AgentOvenJsonContext.Default.Agent, AgentOvenJsonContext.Default.Agent, cancellationToken).ConfigureAwait(false);

        // The server echoes the request body, so defaults it applied when storing (the version) are missing.
        return string.IsNullOrEmpty(created.Version) ? await GetAsync(agent.Name, cancellationToken).ConfigureAwait(false) : created;
    }

    /// <summary>
    /// Updates an agent. Only non-empty properties are applied, lists replace the current ones and tags are merged;
    /// the server bumps the patch version.
    /// </summary>
    public Task<Agent> UpdateAsync(Agent agent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agent);
        return _http.PutAsync(
            ApiPath.Of("agents", agent.Name), agent, AgentOvenJsonContext.Default.Agent, AgentOvenJsonContext.Default.Agent, cancellationToken);
    }

    /// <summary>Deletes an agent and all its versions.</summary>
    public Task DeleteAsync(string name, CancellationToken cancellationToken = default) =>
        _http.DeleteAsync(ApiPath.Of("agents", name), cancellationToken);

    /// <summary>Lists all versions of an agent, oldest first.</summary>
    public Task<IReadOnlyList<Agent>> ListVersionsAsync(string name, CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("agents", name, "versions"), AgentOvenJsonContext.Default.IReadOnlyListAgent, cancellationToken);

    /// <summary>Gets one version of an agent.</summary>
    public Task<Agent> GetVersionAsync(string name, string version, CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("agents", name, "versions", version), AgentOvenJsonContext.Default.Agent, cancellationToken);

    /// <summary>Resolves an agent's ingredients now and returns them with the agent.</summary>
    public Task<AgentConfig> GetConfigAsync(string name, CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("agents", name, "config"), AgentOvenJsonContext.Default.AgentConfig, cancellationToken);

    /// <summary>Gets the agent's A2A card.</summary>
    public Task<AgentCard> GetCardAsync(string name, CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("agents", name, "card"), AgentOvenJsonContext.Default.AgentCard, cancellationToken);

    // ── Lifecycle ────────────────────────────────────────────────────────

    /// <summary>
    /// Bakes (deploys) an agent. Returns as soon as baking starts; the agent then becomes
    /// <see cref="AgentStatus.Ready"/> or <see cref="AgentStatus.Burnt"/>. Use <see cref="WaitUntilReadyAsync"/> to wait.
    /// </summary>
    public Task<BakeResult> BakeAsync(string name, BakeOptions? options = null, CancellationToken cancellationToken = default) =>
        _http.PostAsync(
            ApiPath.Of("agents", name, "bake"),
            options ?? new BakeOptions(),
            AgentOvenJsonContext.Default.BakeOptions,
            AgentOvenJsonContext.Default.BakeResult,
            cancellationToken);

    /// <summary>Bakes an agent.</summary>
    public Task<BakeResult> BakeAsync(Agent agent, BakeOptions? options = null, CancellationToken cancellationToken = default) =>
        BakeAsync(NameOf(agent), options, cancellationToken);

    /// <summary>
    /// Re-bakes a ready or cooled agent, optionally changing it first. The server bumps the minor
    /// version unless <see cref="RecookOptions.Version"/> is set.
    /// </summary>
    public Task<BakeResult> RecookAsync(string name, RecookOptions? changes = null, CancellationToken cancellationToken = default) =>
        _http.PostAsync(
            ApiPath.Of("agents", name, "recook"),
            changes ?? new RecookOptions(),
            AgentOvenJsonContext.Default.RecookOptions,
            AgentOvenJsonContext.Default.BakeResult,
            cancellationToken);

    /// <summary>Stops a ready agent (status <see cref="AgentStatus.Cooled"/>).</summary>
    public Task<AgentStatusResult> CoolAsync(string name, CancellationToken cancellationToken = default) =>
        _http.PostAsync(ApiPath.Of("agents", name, "cool"), AgentOvenJsonContext.Default.AgentStatusResult, cancellationToken);

    /// <summary>Stops a ready agent.</summary>
    public Task<AgentStatusResult> CoolAsync(Agent agent, CancellationToken cancellationToken = default) =>
        CoolAsync(NameOf(agent), cancellationToken);

    /// <summary>Restarts a cooled agent.</summary>
    public Task<AgentStatusResult> RewarmAsync(string name, CancellationToken cancellationToken = default) =>
        _http.PostAsync(ApiPath.Of("agents", name, "rewarm"), AgentOvenJsonContext.Default.AgentStatusResult, cancellationToken);

    /// <summary>Restarts a cooled agent.</summary>
    public Task<AgentStatusResult> RewarmAsync(Agent agent, CancellationToken cancellationToken = default) =>
        RewarmAsync(NameOf(agent), cancellationToken);

    /// <summary>
    /// Polls until the agent leaves <see cref="AgentStatus.Baking"/>. Returns the ready agent, or throws
    /// <see cref="AgentBurntException"/> when baking failed.
    /// </summary>
    /// <param name="name">Agent name.</param>
    /// <param name="timeout">How long to wait. Default: 5 minutes.</param>
    /// <param name="pollInterval">Time between checks. Default: 1 second.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    public async Task<Agent> WaitUntilReadyAsync(
        string name, TimeSpan? timeout = null, TimeSpan? pollInterval = null, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout ?? TimeSpan.FromMinutes(5));
        var delay = pollInterval ?? TimeSpan.FromSeconds(1);
        try
        {
            while (true)
            {
                var agent = await GetAsync(name, cts.Token).ConfigureAwait(false);
                if (agent.Status == AgentStatus.Ready)
                {
                    return agent;
                }

                if (agent.Status == AgentStatus.Burnt)
                {
                    throw new AgentBurntException(agent);
                }

                if (agent.Status != AgentStatus.Baking)
                {
                    throw new AgentOvenException($"Agent '{name}' is {agent.Status}, not baking; bake it first.");
                }

                await Task.Delay(delay, cts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Agent '{name}' was still baking after {timeout ?? TimeSpan.FromMinutes(5)}.");
        }
    }

    // ── Invocation ───────────────────────────────────────────────────────

    /// <summary>
    /// Runs the agent's full loop on one message. The control plane requires an identity here
    /// (an API key, scoped key or service token).
    /// </summary>
    public Task<InvokeResult> InvokeAsync(string name, string message, InvokeOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(message);
        var request = new InvokeRequest(message)
        {
            Variables = options?.Variables,
            ThinkingEnabled = options?.ThinkingEnabled == true ? true : null,
        };
        return _http.PostAsync(
            ApiPath.Of("agents", name, "invoke"),
            request,
            InternalJsonContext.Default.InvokeRequest,
            AgentOvenJsonContext.Default.InvokeResult,
            cancellationToken);
    }

    /// <summary>
    /// Runs the agent and streams tokens and tool activity as they happen. The stream ends with a
    /// <see cref="DoneStreamEvent"/>. Errors inside the agent arrive as <see cref="ErrorStreamEvent"/>.
    /// Only agents that run their own process (local, Docker or Kubernetes) can stream.
    /// </summary>
    public async IAsyncEnumerable<AgentStreamEvent> InvokeStreamingAsync(
        string name, string message, InvokeOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(message);
        var request = new InvokeRequest(message) { Variables = options?.Variables };
        var content = AgentOvenHttp.Json(request, InternalJsonContext.Default.InvokeRequest);
        await foreach (var evt in _http.StreamAsync(HttpMethod.Post, ApiPath.Of("agents", name, "invoke", "stream"), content, cancellationToken)
                           .ConfigureAwait(false))
        {
            if (string.IsNullOrWhiteSpace(evt.Data))
            {
                continue;
            }

            var parsed = AgentStreamEvent.Parse(evt.Data);
            yield return parsed;
            if (parsed is DoneStreamEvent)
            {
                yield break;
            }
        }
    }

    /// <summary>One-shot model call with the agent's configuration, without the agentic loop (unless <paramref name="tools"/> is set).</summary>
    public Task<AgentTestResult> TestAsync(
        string name, string message, bool thinking = false, bool tools = false, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(message);
        var request = new TestRequest(message) { ThinkingEnabled = thinking ? true : null, Tools = tools ? true : null };
        return _http.PostAsync(
            ApiPath.Of("agents", name, "test"),
            request,
            InternalJsonContext.Default.TestRequest,
            AgentOvenJsonContext.Default.AgentTestResult,
            cancellationToken);
    }

    // ── Process ──────────────────────────────────────────────────────────

    /// <summary>Gets the agent's process, or <see langword="null"/> when it has none.</summary>
    public async Task<ProcessInfo?> GetProcessAsync(string name, CancellationToken cancellationToken = default)
    {
        var info = await _http.GetAsync(ApiPath.Of("agents", name, "process"), AgentOvenJsonContext.Default.ProcessInfo, cancellationToken)
            .ConfigureAwait(false);
        return info.Status?.Value == "no_process" ? null : info;
    }

    /// <summary>Gets the most recent lines the agent's process wrote (up to 500).</summary>
    public Task<IReadOnlyList<LogEntry>> GetRecentLogsAsync(string name, CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("agents", name, "logs", "recent"), AgentOvenJsonContext.Default.IReadOnlyListLogEntry, cancellationToken);

    /// <summary>
    /// Follows the agent's process output: the last 200 lines, then live lines until <paramref name="cancellationToken"/> fires.
    /// </summary>
    public async IAsyncEnumerable<LogEntry> StreamLogsAsync(string name, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var evt in _http.StreamAsync(HttpMethod.Get, ApiPath.Of("agents", name, "logs"), content: null, cancellationToken)
                           .ConfigureAwait(false))
        {
            if (string.IsNullOrWhiteSpace(evt.Data))
            {
                continue;
            }

            var entry = JsonSerializer.Deserialize(evt.Data, AgentOvenJsonContext.Default.LogEntry);
            if (entry is not null)
            {
                yield return entry;
            }
        }
    }

    private static string NameOf(Agent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        return agent.Name;
    }
}

/// <summary>Thrown by <see cref="AgentsClient.WaitUntilReadyAsync"/> when an agent failed to bake.</summary>
public sealed class AgentBurntException : AgentOvenException
{
    internal AgentBurntException(Agent agent)
        : base($"Agent '{agent.Name}' failed to bake: {agent.BurntReason ?? "no reason given"}.") => Agent = agent;

    /// <summary>The burnt agent; <see cref="Agent.BurntReason"/> says why.</summary>
    public Agent Agent { get; }
}
