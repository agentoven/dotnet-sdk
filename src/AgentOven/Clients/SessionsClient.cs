using AgentOven.Internal;

namespace AgentOven;

/// <summary>Chat sessions, addressed by agent name. For one agent, prefer <c>oven.Agents["name"].Sessions</c>.</summary>
public sealed class SessionsClient
{
    private readonly AgentOvenHttp _http;

    internal SessionsClient(AgentOvenHttp http) => _http = http;

    /// <summary>Creates a session. The agent must be <see cref="AgentStatus.Ready"/> (409 otherwise).</summary>
    public Task<Session> CreateAsync(string agentName, SessionOptions? options = null, CancellationToken cancellationToken = default) =>
        _http.PostAsync(
            ApiPath.Of("agents", agentName, "sessions"),
            options ?? new SessionOptions(),
            AgentOvenJsonContext.Default.SessionOptions,
            AgentOvenJsonContext.Default.Session,
            cancellationToken);

    /// <summary>Lists an agent's sessions.</summary>
    public Task<IReadOnlyList<Session>> ListAsync(string agentName, CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("agents", agentName, "sessions"), AgentOvenJsonContext.Default.IReadOnlyListSession, cancellationToken);

    /// <summary>Gets a session with its history.</summary>
    public Task<Session> GetAsync(string agentName, string sessionId, CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("agents", agentName, "sessions", sessionId), AgentOvenJsonContext.Default.Session, cancellationToken);

    /// <summary>Deletes a session.</summary>
    public Task DeleteAsync(string agentName, string sessionId, CancellationToken cancellationToken = default) =>
        _http.DeleteAsync(ApiPath.Of("agents", agentName, "sessions", sessionId), cancellationToken);

    /// <summary>Sends a message and returns the agent's reply.</summary>
    public Task<SessionReply> SendAsync(
        string agentName,
        string sessionId,
        string content,
        IReadOnlyDictionary<string, string>? promptVariables = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(content);
        return _http.PostAsync(
            ApiPath.Of("agents", agentName, "sessions", sessionId, "messages"),
            new SessionMessageRequest(content) { PromptVars = promptVariables },
            InternalJsonContext.Default.SessionMessageRequest,
            AgentOvenJsonContext.Default.SessionReply,
            cancellationToken);
    }
}

/// <summary>One agent's sessions.</summary>
public sealed class AgentSessions
{
    private readonly SessionsClient _sessions;
    private readonly string _agentName;

    internal AgentSessions(SessionsClient sessions, string agentName)
    {
        _sessions = sessions;
        _agentName = agentName;
    }

    /// <summary>
    /// Starts a conversation. Disposing the returned <see cref="ChatSession"/> deletes the session on the server
    /// unless <paramref name="keep"/> is <see langword="true"/>.
    /// </summary>
    public async Task<ChatSession> StartAsync(SessionOptions? options = null, bool keep = false, CancellationToken cancellationToken = default)
    {
        var session = await _sessions.CreateAsync(_agentName, options, cancellationToken).ConfigureAwait(false);
        return new ChatSession(_sessions, _agentName, session.Id, deleteOnDispose: !keep);
    }

    /// <summary>Continues an existing session. Disposing it does not delete it.</summary>
    public ChatSession Resume(string sessionId) => new(_sessions, _agentName, sessionId, deleteOnDispose: false);

    /// <inheritdoc cref="SessionsClient.ListAsync"/>
    public Task<IReadOnlyList<Session>> ListAsync(CancellationToken cancellationToken = default) => _sessions.ListAsync(_agentName, cancellationToken);
}

/// <summary>
/// A conversation with an agent. Use with <c>await using</c>:
/// <code>
/// await using var chat = await oven.Agents["support"].Sessions.StartAsync();
/// var reply = await chat.SendAsync("Where is my order?");
/// </code>
/// </summary>
public sealed class ChatSession : IAsyncDisposable
{
    private readonly SessionsClient _sessions;
    private readonly bool _deleteOnDispose;
    private int _disposed;

    internal ChatSession(SessionsClient sessions, string agentName, string sessionId, bool deleteOnDispose)
    {
        _sessions = sessions;
        AgentName = agentName;
        Id = sessionId;
        _deleteOnDispose = deleteOnDispose;
    }

    /// <summary>Session id; pass it to <see cref="AgentSessions.Resume"/> to continue later.</summary>
    public string Id { get; }

    /// <summary>Agent.</summary>
    public string AgentName { get; }

    /// <summary>Sends a message and returns the agent's reply.</summary>
    public Task<SessionReply> SendAsync(
        string content, IReadOnlyDictionary<string, string>? promptVariables = null, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return _sessions.SendAsync(AgentName, Id, content, promptVariables, cancellationToken);
    }

    /// <summary>Gets the session with its history.</summary>
    public Task<Session> GetAsync(CancellationToken cancellationToken = default) => _sessions.GetAsync(AgentName, Id, cancellationToken);

    /// <summary>Deletes the session on the server now.</summary>
    public async Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Exchange(ref _disposed, 1);
        await _sessions.DeleteAsync(AgentName, Id, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Deletes the session if it was started with <c>keep: false</c>.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0 || !_deleteOnDispose)
        {
            return;
        }

        try
        {
            await _sessions.DeleteAsync(AgentName, Id).ConfigureAwait(false);
        }
        catch (AgentOvenApiException ex) when (ex.IsNotFound)
        {
            // Already gone.
        }
    }
}
