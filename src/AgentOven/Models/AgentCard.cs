using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentOven;

/// <summary>An A2A agent card. The wire format is camelCase, unlike the rest of the API.</summary>
public sealed record AgentCard
{
    /// <summary>Agent name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    /// <summary>Description.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>A2A URL.</summary>
    [JsonPropertyName("url")]
    public string? Url { get; init; }

    /// <summary>Version.</summary>
    [JsonPropertyName("version")]
    public string? Version { get; init; }

    /// <summary>Provider organisation.</summary>
    [JsonPropertyName("provider")]
    public AgentCardProvider? Provider { get; init; }

    /// <summary>Capabilities.</summary>
    [JsonPropertyName("capabilities")]
    public AgentCardCapabilities? Capabilities { get; init; }

    /// <summary>Skills.</summary>
    [JsonPropertyName("skills")]
    public IReadOnlyList<AgentCardSkill>? Skills { get; init; }

    /// <summary>Accepted input modes.</summary>
    [JsonPropertyName("defaultInputModes")]
    public IReadOnlyList<string>? DefaultInputModes { get; init; }

    /// <summary>Produced output modes.</summary>
    [JsonPropertyName("defaultOutputModes")]
    public IReadOnlyList<string>? DefaultOutputModes { get; init; }

    /// <summary>Fields this SDK version does not model (for example <c>authentication</c>, <c>tools</c>).</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

/// <summary>Who publishes an agent.</summary>
public sealed record AgentCardProvider
{
    /// <summary>Organisation.</summary>
    [JsonPropertyName("organization")]
    public string? Organization { get; init; }

    /// <summary>URL.</summary>
    [JsonPropertyName("url")]
    public string? Url { get; init; }
}

/// <summary>A2A capabilities.</summary>
public sealed record AgentCardCapabilities
{
    /// <summary>Streams responses.</summary>
    [JsonPropertyName("streaming")]
    public bool? Streaming { get; init; }

    /// <summary>Push notifications.</summary>
    [JsonPropertyName("pushNotifications")]
    public bool? PushNotifications { get; init; }

    /// <summary>Keeps task state history.</summary>
    [JsonPropertyName("stateTransitionHistory")]
    public bool? StateTransitionHistory { get; init; }

    /// <summary>Supports sessions.</summary>
    [JsonPropertyName("sessions")]
    public bool? Sessions { get; init; }

    /// <summary>Asks for human input.</summary>
    [JsonPropertyName("humanInput")]
    public bool? HumanInput { get; init; }

    /// <summary>Calls tools.</summary>
    [JsonPropertyName("toolCalling")]
    public bool? ToolCalling { get; init; }

    /// <summary>Accepts images.</summary>
    [JsonPropertyName("vision")]
    public bool? Vision { get; init; }

    /// <summary>Produces structured output.</summary>
    [JsonPropertyName("structuredOutput")]
    public bool? StructuredOutput { get; init; }
}

/// <summary>A skill on an agent card.</summary>
public sealed record AgentCardSkill
{
    /// <summary>Id.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    /// <summary>Name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    /// <summary>Description.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>Tags.</summary>
    [JsonPropertyName("tags")]
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>Example prompts.</summary>
    [JsonPropertyName("examples")]
    public IReadOnlyList<string>? Examples { get; init; }
}
