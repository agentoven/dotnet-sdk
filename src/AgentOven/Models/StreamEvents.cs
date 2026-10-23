using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AgentOven;

/// <summary>
/// One event of a streamed agent invocation. Two wire formats exist and both are understood:
/// <list type="bullet">
/// <item>Agent processes (and the built-in runner): <c>data: {"type": "token" | "tool_call" | "tool_result" | "error" | "done", …}</c>.</item>
/// <item>The control plane's in-process executor: <c>turn_start</c>, <c>token</c>, <c>thinking_token</c>,
/// <c>tool_call_start</c>, <c>tool_call_end</c>, <c>turn_end</c>, <c>paused</c>, <c>done</c>, <c>error</c>, with
/// <c>tool_call</c>/<c>tool_result</c>/<c>pending</c>/<c>trace</c> objects. Keys are matched case-insensitively,
/// because the executor currently writes <c>Type</c>/<c>Turn</c>/<c>Content</c>.</item>
/// </list>
/// A stream ends with a <see cref="DoneStreamEvent"/> (or, for approval-gated tools, a <see cref="PausedStreamEvent"/>).
/// </summary>
public abstract record AgentStreamEvent
{
    private protected AgentStreamEvent() { }

    /// <summary>
    /// Writer options for the wire format: relaxed escaping, so apostrophes and non-ASCII text stay readable
    /// (the payload is JSON over HTTP, never embedded in HTML).
    /// </summary>
    public static JsonWriterOptions WriterOptions { get; } = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>The wire <c>type</c>.</summary>
    public abstract string Type { get; }

    /// <summary>Agentic-loop turn the event belongs to, when the server reports it.</summary>
    public int? Turn { get; init; }

    /// <summary>Text to show the user: the token content for <see cref="TokenStreamEvent"/>, otherwise <see langword="null"/>.</summary>
    public virtual string? Text => null;

    /// <summary><see langword="true"/> for the event that ends a stream (<see cref="DoneStreamEvent"/> or <see cref="PausedStreamEvent"/>).</summary>
    public virtual bool IsTerminal => false;

    /// <summary>Writes this event as a JSON object (agent process wire format).</summary>
    public abstract void WriteTo(Utf8JsonWriter writer);

    /// <summary>Serializes this event to its JSON wire form.</summary>
    public string ToJson()
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            WriteTo(writer);
        }

        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>Parses one event from either wire form.</summary>
    public static AgentStreamEvent Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return new UnknownStreamEvent("", root.Clone());
        }

        var type = GetString(root, "type") ?? "";
        int? turn = Get(root, "turn") is { ValueKind: JsonValueKind.Number } t && t.TryGetInt32(out var n) && n > 0 ? n : null;
        var toolCall = Get(root, "tool_call") is { ValueKind: JsonValueKind.Object } tc ? tc : (JsonElement?)null;
        var toolResult = Get(root, "tool_result") is { ValueKind: JsonValueKind.Object } tr ? tr : (JsonElement?)null;

        AgentStreamEvent evt = type switch
        {
            "token" => new TokenStreamEvent(GetString(root, "content") ?? ""),
            "thinking_token" => new ThinkingStreamEvent(GetString(root, "content") ?? ""),
            "turn_start" => new TurnStartStreamEvent(),
            "turn_end" => new TurnEndStreamEvent(),
            "tool_call" => new ToolCallStreamEvent(GetString(root, "name") ?? "", ParseObject(Get(root, "args"))),
            "tool_call_start" => new ToolCallStreamEvent(
                (toolCall is { } c ? GetString(c, "name") : null) ?? "",
                toolCall is { } c2 ? ParseObject(Get(c2, "arguments")) : null)
            {
                CallId = toolCall is { } c3 ? GetString(c3, "id") : null,
            },
            "tool_result" => new ToolResultStreamEvent(GetString(root, "name") ?? "", GetString(root, "result") ?? ""),
            "tool_call_end" => new ToolResultStreamEvent(
                (toolResult is { } r ? GetString(r, "name") : null) ?? (toolCall is { } c4 ? GetString(c4, "name") : null) ?? "",
                (toolResult is { } r2 ? GetString(r2, "content") : null) ?? "")
            {
                CallId = (toolResult is { } r3 ? GetString(r3, "tool_call_id") : null) ?? (toolCall is { } c5 ? GetString(c5, "id") : null),
                IsError = toolResult is { } r4 && Get(r4, "is_error") is { ValueKind: JsonValueKind.True },
            },
            "error" => new ErrorStreamEvent(GetString(root, "message") ?? GetString(root, "err") ?? GetString(root, "error") ?? ""),
            "paused" => new PausedStreamEvent(Deserialize(Get(root, "pending"), AgentOvenJsonContext.Default.PendingApproval))
            {
                TraceId = GetString(root, "trace_id") ?? (Get(root, "trace") is { ValueKind: JsonValueKind.Object } pt ? GetString(pt, "trace_id") : null),
            },
            "done" => ParseDone(root),
            _ => new UnknownStreamEvent(type, root.Clone()),
        };

        return turn is null ? evt : evt with { Turn = turn };
    }

    private static DoneStreamEvent ParseDone(JsonElement root)
    {
        var trace = Deserialize(Get(root, "trace"), AgentOvenJsonContext.Default.ExecutionTrace);
        var usage = Deserialize(Get(root, "usage"), AgentOvenJsonContext.Default.TokenUsage) ?? trace?.Usage;
        return new DoneStreamEvent(usage)
        {
            Response = GetString(root, "content") is { Length: > 0 } content ? content : null,
            Trace = trace,
        };
    }

    /// <summary>Gets a property by its snake_case name, falling back to a case-insensitive match.</summary>
    private static JsonElement? Get(JsonElement root, string name)
    {
        if (root.TryGetProperty(name, out var exact))
        {
            return exact;
        }

        var compact = name.Replace("_", "", StringComparison.Ordinal);
        foreach (var property in root.EnumerateObject())
        {
            if (string.Equals(property.Name.Replace("_", "", StringComparison.Ordinal), compact, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        return null;
    }

    private static string? GetString(JsonElement root, string name) => Get(root, name) switch
    {
        { ValueKind: JsonValueKind.String } value => value.GetString(),
        { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined } => null,
        { } value => value.GetRawText(),
        null => null,
    };

    private static JsonObject? ParseObject(JsonElement? element) =>
        element is { ValueKind: JsonValueKind.Object } obj ? JsonNode.Parse(obj.GetRawText())!.AsObject() : null;

    private static T? Deserialize<T>(JsonElement? element, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> type) where T : class =>
        element is { ValueKind: JsonValueKind.Object } obj ? obj.Deserialize(type) : null;
}

/// <summary>A chunk of the model's answer.</summary>
public sealed record TokenStreamEvent(string Content) : AgentStreamEvent
{
    /// <inheritdoc />
    public override string Type => "token";

    /// <inheritdoc />
    public override string? Text => Content;

    /// <inheritdoc />
    public override void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("type", Type);
        writer.WriteString("content", Content);
        writer.WriteEndObject();
    }
}

/// <summary>The agent is calling a tool.</summary>
public sealed record ToolCallStreamEvent(string Name, JsonObject? Arguments) : AgentStreamEvent
{
    /// <inheritdoc />
    public override string Type => "tool_call";

    /// <summary>Tool call id, when the server reports it.</summary>
    public string? CallId { get; init; }

    /// <inheritdoc />
    public override void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("type", Type);
        writer.WriteString("name", Name);
        writer.WritePropertyName("args");
        if (Arguments is null)
        {
            writer.WriteStartObject();
            writer.WriteEndObject();
        }
        else
        {
            Arguments.WriteTo(writer);
        }

        writer.WriteEndObject();
    }
}

/// <summary>A tool returned.</summary>
public sealed record ToolResultStreamEvent(string Name, string Result) : AgentStreamEvent
{
    /// <inheritdoc />
    public override string Type => "tool_result";

    /// <summary>The call this result answers, when the server reports it.</summary>
    public string? CallId { get; init; }

    /// <summary>Whether the tool failed.</summary>
    public bool IsError { get; init; }

    /// <inheritdoc />
    public override void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("type", Type);
        writer.WriteString("name", Name);
        writer.WriteString("result", Result);
        writer.WriteEndObject();
    }
}

/// <summary>The invocation failed. A <see cref="DoneStreamEvent"/> still follows.</summary>
public sealed record ErrorStreamEvent(string Message) : AgentStreamEvent
{
    /// <inheritdoc />
    public override string Type => "error";

    /// <inheritdoc />
    public override void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("type", Type);
        writer.WriteString("message", Message);
        writer.WriteEndObject();
    }
}

/// <summary>The last event of every stream.</summary>
public sealed record DoneStreamEvent(TokenUsage? Usage) : AgentStreamEvent
{
    /// <inheritdoc />
    public override string Type => "done";

    /// <inheritdoc />
    public override bool IsTerminal => true;

    /// <summary>The final answer, when the server sends it with the done event (in-process executor).</summary>
    public string? Response { get; init; }

    /// <summary>The run's execution trace, when the server sends it (in-process executor).</summary>
    public ExecutionTrace? Trace { get; init; }

    /// <inheritdoc />
    public override void WriteTo(Utf8JsonWriter writer)
    {
        var usage = Usage ?? new TokenUsage();
        writer.WriteStartObject();
        writer.WriteString("type", Type);
        writer.WriteStartObject("usage");
        writer.WriteNumber("input_tokens", usage.InputTokens);
        writer.WriteNumber("output_tokens", usage.OutputTokens);
        writer.WriteNumber("total_tokens", usage.TotalTokens);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }
}

/// <summary>A chunk of the model's extended thinking.</summary>
public sealed record ThinkingStreamEvent(string Content) : AgentStreamEvent
{
    /// <inheritdoc />
    public override string Type => "thinking_token";

    /// <inheritdoc />
    public override void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("type", Type);
        writer.WriteString("content", Content);
        writer.WriteEndObject();
    }
}

/// <summary>A turn of the agentic loop started.</summary>
public sealed record TurnStartStreamEvent : AgentStreamEvent
{
    /// <inheritdoc />
    public override string Type => "turn_start";

    /// <inheritdoc />
    public override void WriteTo(Utf8JsonWriter writer) => WriteTurn(writer, Type, Turn);

    internal static void WriteTurn(Utf8JsonWriter writer, string type, int? turn)
    {
        writer.WriteStartObject();
        writer.WriteString("type", type);
        if (turn is { } t)
        {
            writer.WriteNumber("turn", t);
        }

        writer.WriteEndObject();
    }
}

/// <summary>A turn of the agentic loop ended.</summary>
public sealed record TurnEndStreamEvent : AgentStreamEvent
{
    /// <inheritdoc />
    public override string Type => "turn_end";

    /// <inheritdoc />
    public override void WriteTo(Utf8JsonWriter writer) => TurnStartStreamEvent.WriteTurn(writer, Type, Turn);
}

/// <summary>
/// The run stopped because the model called a tool listed in the agent's <c>approval_tools</c>.
/// Resume it through the control plane's resume endpoint, passing <see cref="TraceId"/>.
/// </summary>
public sealed record PausedStreamEvent(PendingApproval? Pending) : AgentStreamEvent
{
    /// <inheritdoc />
    public override string Type => "paused";

    /// <inheritdoc />
    public override bool IsTerminal => true;

    /// <summary>The run to resume.</summary>
    public string? TraceId { get; init; }

    /// <inheritdoc />
    public override void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("type", Type);
        if (TraceId is not null)
        {
            writer.WriteString("trace_id", TraceId);
        }

        if (Pending is not null)
        {
            writer.WritePropertyName("pending");
            JsonSerializer.Serialize(writer, Pending, AgentOvenJsonContext.Default.PendingApproval);
        }

        writer.WriteEndObject();
    }
}

/// <summary>An event type this SDK version does not know.</summary>
public sealed record UnknownStreamEvent(string RawType, JsonElement Raw) : AgentStreamEvent
{
    /// <inheritdoc />
    public override string Type => RawType;

    /// <inheritdoc />
    public override void WriteTo(Utf8JsonWriter writer) => Raw.WriteTo(writer);
}
