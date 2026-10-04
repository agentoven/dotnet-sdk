namespace AgentOven;

/// <summary>
/// One agent, bound by name: <c>var summarizer = oven.Agents["summarizer"];</c>. Creating a handle makes no network call.
/// </summary>
public sealed class AgentHandle
{
    private readonly AgentsClient _agents;

    internal AgentHandle(AgentsClient agents, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _agents = agents;
        Name = name;
        Sessions = new AgentSessions(agents.Sessions, name);
    }

    /// <summary>Agent name.</summary>
    public string Name { get; }

    /// <summary>This agent's chat sessions.</summary>
    public AgentSessions Sessions { get; }

    /// <inheritdoc cref="AgentsClient.GetAsync"/>
    public Task<Agent> GetAsync(CancellationToken cancellationToken = default) => _agents.GetAsync(Name, cancellationToken);

    /// <inheritdoc cref="AgentsClient.FindAsync"/>
    public Task<Agent?> FindAsync(CancellationToken cancellationToken = default) => _agents.FindAsync(Name, cancellationToken);

    /// <inheritdoc cref="AgentsClient.BakeAsync(string, BakeOptions?, CancellationToken)"/>
    public Task<BakeResult> BakeAsync(BakeOptions? options = null, CancellationToken cancellationToken = default) =>
        _agents.BakeAsync(Name, options, cancellationToken);

    /// <summary>Bakes the agent and waits until it is ready (or throws <see cref="AgentBurntException"/>).</summary>
    public async Task<Agent> BakeAndWaitAsync(BakeOptions? options = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        await _agents.BakeAsync(Name, options, cancellationToken).ConfigureAwait(false);
        return await _agents.WaitUntilReadyAsync(Name, timeout, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="AgentsClient.WaitUntilReadyAsync"/>
    public Task<Agent> WaitUntilReadyAsync(TimeSpan? timeout = null, TimeSpan? pollInterval = null, CancellationToken cancellationToken = default) =>
        _agents.WaitUntilReadyAsync(Name, timeout, pollInterval, cancellationToken);

    /// <inheritdoc cref="AgentsClient.RecookAsync"/>
    public Task<BakeResult> RecookAsync(RecookOptions? changes = null, CancellationToken cancellationToken = default) =>
        _agents.RecookAsync(Name, changes, cancellationToken);

    /// <inheritdoc cref="AgentsClient.CoolAsync(string, CancellationToken)"/>
    public Task<AgentStatusResult> CoolAsync(CancellationToken cancellationToken = default) => _agents.CoolAsync(Name, cancellationToken);

    /// <inheritdoc cref="AgentsClient.RewarmAsync(string, CancellationToken)"/>
    public Task<AgentStatusResult> RewarmAsync(CancellationToken cancellationToken = default) => _agents.RewarmAsync(Name, cancellationToken);

    /// <inheritdoc cref="AgentsClient.DeleteAsync"/>
    public Task DeleteAsync(CancellationToken cancellationToken = default) => _agents.DeleteAsync(Name, cancellationToken);

    /// <inheritdoc cref="AgentsClient.InvokeAsync"/>
    public Task<InvokeResult> InvokeAsync(string message, InvokeOptions? options = null, CancellationToken cancellationToken = default) =>
        _agents.InvokeAsync(Name, message, options, cancellationToken);

    /// <inheritdoc cref="AgentsClient.InvokeStreamingAsync"/>
    public IAsyncEnumerable<AgentStreamEvent> InvokeStreamingAsync(string message, InvokeOptions? options = null, CancellationToken cancellationToken = default) =>
        _agents.InvokeStreamingAsync(Name, message, options, cancellationToken);

    /// <inheritdoc cref="AgentsClient.TestAsync"/>
    public Task<AgentTestResult> TestAsync(string message, bool thinking = false, bool tools = false, CancellationToken cancellationToken = default) =>
        _agents.TestAsync(Name, message, thinking, tools, cancellationToken);

    /// <inheritdoc cref="AgentsClient.GetConfigAsync"/>
    public Task<AgentConfig> GetConfigAsync(CancellationToken cancellationToken = default) => _agents.GetConfigAsync(Name, cancellationToken);

    /// <inheritdoc cref="AgentsClient.GetCardAsync"/>
    public Task<AgentCard> GetCardAsync(CancellationToken cancellationToken = default) => _agents.GetCardAsync(Name, cancellationToken);

    /// <inheritdoc cref="AgentsClient.ListVersionsAsync"/>
    public Task<IReadOnlyList<Agent>> ListVersionsAsync(CancellationToken cancellationToken = default) => _agents.ListVersionsAsync(Name, cancellationToken);

    /// <inheritdoc cref="AgentsClient.GetProcessAsync"/>
    public Task<ProcessInfo?> GetProcessAsync(CancellationToken cancellationToken = default) => _agents.GetProcessAsync(Name, cancellationToken);

    /// <inheritdoc cref="AgentsClient.StreamLogsAsync"/>
    public IAsyncEnumerable<LogEntry> StreamLogsAsync(CancellationToken cancellationToken = default) => _agents.StreamLogsAsync(Name, cancellationToken);

    /// <inheritdoc />
    public override string ToString() => Name;
}
