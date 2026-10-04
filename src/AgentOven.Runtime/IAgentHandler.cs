using System.Runtime.CompilerServices;
using System.Text.Json;

namespace AgentOven.Runtime;

/// <summary>
/// Your agent. The runtime calls it for every <c>/invoke</c>, <c>/invoke/stream</c> and A2A task.
/// Implement <see cref="InvokeAsync"/>; override <see cref="StreamAsync"/> to stream tokens.
/// </summary>
public interface IAgentHandler
{
    /// <summary>Answers one message.</summary>
    Task<AgentReply> InvokeAsync(AgentRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Answers one message as a stream of events. The default runs <see cref="InvokeAsync"/> and sends the
    /// answer as a single token. The runtime appends a <see cref="DoneStreamEvent"/> if you do not.
    /// </summary>
    IAsyncEnumerable<AgentStreamEvent> StreamAsync(AgentRequest request, CancellationToken cancellationToken) =>
        AgentHandler.StreamFromInvokeAsync(this, request, cancellationToken);
}

/// <summary>A message for the agent, as the control plane sends it.</summary>
public sealed record AgentRequest
{
    /// <summary>Creates a request.</summary>
    public AgentRequest(string message) => Message = message;

    /// <summary>The user's message.</summary>
    public string Message { get; init; }

    /// <summary>Prompt variables for <c>{{placeholders}}</c>.</summary>
    public IReadOnlyDictionary<string, string> Variables { get; init; } = new Dictionary<string, string>();

    /// <summary>Trace id assigned by the control plane.</summary>
    public string TraceId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>Loop limit for this request, if the caller set one.</summary>
    public int? MaxTurns { get; init; }

    /// <summary>TLS settings for a provider with a private CA, if the control plane sent them.</summary>
    public ProviderTlsConfig? ProviderConfig { get; init; }

    internal static AgentRequest Parse(JsonElement root)
    {
        string? GetString(string name) =>
            root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        var variables = new Dictionary<string, string>();
        if (root.TryGetProperty("variables", out var vars) && vars.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in vars.EnumerateObject())
            {
                variables[property.Name] = property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString() ?? ""
                    : property.Value.GetRawText();
            }
        }

        ProviderTlsConfig? tls = null;
        if (root.TryGetProperty("provider_config", out var pc) && pc.ValueKind == JsonValueKind.Object)
        {
            tls = new ProviderTlsConfig
            {
                Name = pc.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null,
                CaBundle = pc.TryGetProperty("ca_bundle", out var ca) && ca.ValueKind == JsonValueKind.String ? ca.GetString() : null,
                TlsSkipVerify = pc.TryGetProperty("tls_skip_verify", out var skip) && skip.ValueKind == JsonValueKind.True,
            };
        }

        return new AgentRequest(GetString("message") ?? "")
        {
            Variables = variables,
            TraceId = GetString("trace_id") ?? Guid.NewGuid().ToString(),
            MaxTurns = root.TryGetProperty("max_turns", out var mt) && mt.TryGetInt32(out var turns) && turns > 0 ? turns : null,
            ProviderConfig = tls,
        };
    }
}

/// <summary>TLS settings for calling a provider behind a private CA.</summary>
public sealed record ProviderTlsConfig
{
    /// <summary>Provider name.</summary>
    public string? Name { get; init; }

    /// <summary>PEM CA bundle.</summary>
    public string? CaBundle { get; init; }

    /// <summary>Skip certificate verification.</summary>
    public bool TlsSkipVerify { get; init; }
}

/// <summary>The agent's answer. A <see cref="string"/> converts implicitly.</summary>
/// <param name="Text">The answer.</param>
public sealed record AgentReply(string Text)
{
    /// <summary>Token usage, reported back to the control plane for cost tracking.</summary>
    public TokenUsage? Usage { get; init; }

    /// <summary>Wraps a string.</summary>
    public static implicit operator AgentReply(string text) => new(text);
}

/// <summary>Helpers for <see cref="IAgentHandler"/> implementations.</summary>
public static class AgentHandler
{
    /// <summary>Wraps a delegate.</summary>
    public static IAgentHandler From(Func<AgentRequest, CancellationToken, Task<AgentReply>> invoke)
    {
        ArgumentNullException.ThrowIfNull(invoke);
        return new DelegateAgentHandler(invoke);
    }

    /// <summary>Wraps a delegate that maps a message to an answer.</summary>
    public static IAgentHandler From(Func<string, CancellationToken, Task<string>> invoke)
    {
        ArgumentNullException.ThrowIfNull(invoke);
        return new DelegateAgentHandler(async (request, ct) => await invoke(request.Message, ct).ConfigureAwait(false));
    }

    /// <summary>Runs <see cref="IAgentHandler.InvokeAsync"/> and yields its answer as one token, then done.</summary>
    public static async IAsyncEnumerable<AgentStreamEvent> StreamFromInvokeAsync(
        IAgentHandler handler, AgentRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var reply = await handler.InvokeAsync(request, cancellationToken).ConfigureAwait(false);
        if (reply.Text.Length > 0)
        {
            yield return new TokenStreamEvent(reply.Text);
        }

        yield return new DoneStreamEvent(reply.Usage);
    }

    private sealed class DelegateAgentHandler(Func<AgentRequest, CancellationToken, Task<AgentReply>> invoke) : IAgentHandler
    {
        public Task<AgentReply> InvokeAsync(AgentRequest request, CancellationToken cancellationToken) => invoke(request, cancellationToken);
    }
}
