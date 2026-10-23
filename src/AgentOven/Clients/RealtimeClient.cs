using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgentOven.Internal;

namespace AgentOven;

/// <summary>
/// Realtime voice conversations with an agent over one WebSocket (Pro). Audio is base64 PCM16, 24 kHz, mono.
/// </summary>
/// <example>
/// <code>
/// await using var call = await oven.Realtime.ConnectAsync("support", voice: "alloy");
/// await call.SendAudioAsync(pcm16Chunk);
/// await call.CommitAsync();
/// await foreach (var evt in call.ReadEventsAsync())
///     if (evt.Type == "transcript") Console.WriteLine($"{evt.Role}: {evt.Text}");
/// </code>
/// </example>
public sealed class RealtimeClient
{
    /// <summary>The protocol the server speaks.</summary>
    public const string SubProtocol = "agentoven.v1";

    /// <summary>Audio format of every <c>audio</c> frame.</summary>
    public const string AudioFormat = "pcm16-24khz-mono";

    private readonly AgentOvenHttp _http;

    internal RealtimeClient(AgentOvenHttp http) => _http = http;

    /// <summary>
    /// Mints a short-lived token (1–300 s) that lets a browser or device connect without the client's credentials.
    /// </summary>
    public Task<RealtimeToken> CreateTokenAsync(string agentName, int ttlSeconds = 60, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(ttlSeconds, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(ttlSeconds, 300);
        return _http.PostAsync(ApiPath.Of("realtime", agentName, "token"), new RealtimeTokenRequest(ttlSeconds),
            InternalJsonContext.Default.RealtimeTokenRequest, AgentOvenJsonContext.Default.RealtimeToken, cancellationToken);
    }

    /// <summary>
    /// Opens a realtime session. With <paramref name="token"/>, authenticates with it instead of the client's credentials.
    /// Returns once the server's <c>ready</c> frame arrives.
    /// </summary>
    public async Task<RealtimeSession> ConnectAsync(
        string agentName, string? voice = null, string? model = null, string? token = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);
        var http = _http.Resolve(ApiPath.WithQuery(ApiPath.Of("realtime", agentName), ("voice", voice), ("model", model)));
        var uri = new UriBuilder(http) { Scheme = http.Scheme == Uri.UriSchemeHttps ? "wss" : "ws" }.Uri;

        var socket = new ClientWebSocket();
        try
        {
            socket.Options.AddSubProtocol(SubProtocol);
            if (token is not null)
            {
                socket.Options.AddSubProtocol("agentoven-token." + token);
            }
            else
            {
                _http.ApplyAuthHeaders(socket.Options.SetRequestHeader);
            }

            await socket.ConnectAsync(uri, cancellationToken).ConfigureAwait(false);
            var session = new RealtimeSession(socket);
            var ready = await session.ReadEventAsync(cancellationToken).ConfigureAwait(false);
            if (ready is null || ready.Type != "ready")
            {
                throw new AgentOvenException($"The realtime session did not start: {ready?.Error ?? ready?.Type ?? "connection closed"}.");
            }

            session.SessionId = ready.SessionId;
            return session;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}

/// <summary>An open realtime voice session.</summary>
public sealed class RealtimeSession : IAsyncDisposable
{
    private readonly ClientWebSocket _socket;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    internal RealtimeSession(ClientWebSocket socket) => _socket = socket;

    /// <summary>Server-assigned session id.</summary>
    public string? SessionId { get; internal set; }

    /// <summary>Whether the socket is open.</summary>
    public bool IsOpen => _socket.State == WebSocketState.Open;

    /// <summary>Close status, after the server closed (1008 = blocked by a guardrail).</summary>
    public WebSocketCloseStatus? CloseStatus => _socket.CloseStatus;

    /// <summary>Appends PCM16 24 kHz mono audio to the input buffer.</summary>
    public Task SendAudioAsync(ReadOnlyMemory<byte> pcm16, CancellationToken cancellationToken = default) =>
        SendAsync(new RealtimeClientFrame("audio") { Audio = Convert.ToBase64String(pcm16.Span) }, cancellationToken);

    /// <summary>Ends the user's turn: the agent answers what was sent since the last commit.</summary>
    public Task CommitAsync(CancellationToken cancellationToken = default) => SendAsync(new RealtimeClientFrame("commit"), cancellationToken);

    /// <summary>Sends a typed message instead of audio.</summary>
    public Task SendTextAsync(string text, CancellationToken cancellationToken = default) =>
        SendAsync(new RealtimeClientFrame("text") { Text = text }, cancellationToken);

    /// <summary>Stops the agent mid-answer (barge-in).</summary>
    public Task InterruptAsync(CancellationToken cancellationToken = default) => SendAsync(new RealtimeClientFrame("interrupt"), cancellationToken);

    /// <summary>Reads server events until the socket closes.</summary>
    public async IAsyncEnumerable<RealtimeEvent> ReadEventsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (true)
        {
            var evt = await ReadEventAsync(cancellationToken).ConfigureAwait(false);
            if (evt is null)
            {
                yield break;
            }

            yield return evt;
        }
    }

    /// <summary>Reads one server event, or <see langword="null"/> once the socket closed.</summary>
    public async Task<RealtimeEvent?> ReadEventAsync(CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        while (true)
        {
            if (_socket.State is not (WebSocketState.Open or WebSocketState.CloseSent))
            {
                return null;
            }

            var result = await _socket.ReceiveAsync(chunk, cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            buffer.Write(chunk, 0, result.Count);
            if (result.EndOfMessage)
            {
                break;
            }
        }

        return JsonSerializer.Deserialize(buffer.GetBuffer().AsSpan(0, (int)buffer.Length), AgentOvenJsonContext.Default.RealtimeEvent);
    }

    /// <summary>Closes the session.</summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_socket.State == WebSocketState.Open)
            {
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (WebSocketException)
        {
            // Already gone.
        }
        finally
        {
            _socket.Dispose();
            _sendLock.Dispose();
        }
    }

    private async Task SendAsync(RealtimeClientFrame frame, CancellationToken cancellationToken)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(frame, AgentOvenJsonContext.Default.RealtimeClientFrame);
        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }
    }
}

/// <summary>A frame the client sends.</summary>
/// <param name="Type">audio, commit, text or interrupt.</param>
public sealed record RealtimeClientFrame(string Type)
{
    /// <summary>Base64 PCM16 audio.</summary>
    public string? Audio { get; init; }

    /// <summary>Text.</summary>
    public string? Text { get; init; }
}

/// <summary>
/// A frame from the server: <c>ready</c>, <c>audio</c>, <c>transcript</c>, <c>tool_call</c>, <c>tool_result</c>,
/// <c>turn_done</c>, <c>interrupted</c> or <c>error</c>.
/// </summary>
public sealed record RealtimeEvent
{
    /// <summary>Type.</summary>
    public string Type { get; init; } = "";

    /// <summary>Session id (on <c>ready</c>).</summary>
    public string? SessionId { get; init; }

    /// <summary>Audio format (on <c>ready</c>).</summary>
    public string? Format { get; init; }

    /// <summary>Base64 PCM16 audio (on <c>audio</c>).</summary>
    public string? Audio { get; init; }

    /// <summary>user or assistant (on <c>transcript</c>).</summary>
    public string? Role { get; init; }

    /// <summary>Text (on <c>transcript</c>).</summary>
    public string? Text { get; init; }

    /// <summary>Whether a transcript is final.</summary>
    public bool? Final { get; init; }

    /// <summary>Tool name.</summary>
    public string? ToolName { get; init; }

    /// <summary>Tool arguments.</summary>
    public JsonElement? ToolArgs { get; init; }

    /// <summary>Tool output.</summary>
    public string? ToolOutput { get; init; }

    /// <summary>Whether the tool failed.</summary>
    public bool? IsError { get; init; }

    /// <summary>Error (on <c>error</c>).</summary>
    public string? Error { get; init; }

    /// <summary>The decoded audio bytes of an <c>audio</c> frame.</summary>
    [JsonIgnore]
    public byte[]? AudioBytes => Audio is null ? null : Convert.FromBase64String(Audio);
}
