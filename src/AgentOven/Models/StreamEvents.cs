using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AgentOven;

/// <summary>
/// One event of a streamed agent invocation. The wire format is the agent process stream:
/// <c>data: {"type": "token" | "tool_call" | "tool_result" | "error" | "done", …}</c>.
/// The stream always ends with a <see cref="DoneStreamEvent"/>, even after an <see cref="ErrorStreamEvent"/>.
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

    /// <summary>Text to show the user: the token content for <see cref="TokenStreamEvent"/>, otherwise <see langword="null"/>.</summary>
    public virtual string? Text => null;

    /// <summary>Writes this event as a JSON object.</summary>
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

    /// <summary>Parses one event from its JSON wire form.</summary>
    public static AgentStreamEvent Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var type = root.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() ?? "" : "";
        return type switch
        {
            "token" => new TokenStreamEvent(GetString(root, "content") ?? ""),
            "tool_call" => new ToolCallStreamEvent(
                GetString(root, "name") ?? "",
                root.TryGetProperty("args", out var args) && args.ValueKind == JsonValueKind.Object ? JsonNode.Parse(args.GetRawText())!.AsObject() : null),
            "tool_result" => new ToolResultStreamEvent(GetString(root, "name") ?? "", GetString(root, "result") ?? ""),
            "error" => new ErrorStreamEvent(GetString(root, "message") ?? ""),
            "done" => new DoneStreamEvent(
                root.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object
                    ? usage.Deserialize(AgentOvenJsonContext.Default.TokenUsage)
                    : null),
            _ => new UnknownStreamEvent(type, root.Clone()),
        };
    }

    private static string? GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) ? value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText() : null;
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

/// <summary>An event type this SDK version does not know.</summary>
public sealed record UnknownStreamEvent(string RawType, JsonElement Raw) : AgentStreamEvent
{
    /// <inheritdoc />
    public override string Type => RawType;

    /// <inheritdoc />
    public override void WriteTo(Utf8JsonWriter writer) => Raw.WriteTo(writer);
}
