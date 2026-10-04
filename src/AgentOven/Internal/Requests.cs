using System.Text.Json.Nodes;

namespace AgentOven.Internal;

// Request and wrapper bodies that are not part of the public model.

internal sealed record InvokeRequest(string Message)
{
    public IReadOnlyDictionary<string, string>? Variables { get; init; }

    public bool? ThinkingEnabled { get; init; }
}

internal sealed record TestRequest(string Message)
{
    public bool? ThinkingEnabled { get; init; }

    public bool? Tools { get; init; }
}

internal sealed record RecipeBakeRequest
{
    public JsonNode? Input { get; init; }

    public string? Environment { get; init; }
}

internal sealed record RecipeRunEnvelope
{
    public RecipeRun? Run { get; init; }

    public IReadOnlyList<string>? PendingGates { get; init; }
}

internal sealed record GateDecision(bool Approved);

internal sealed record SessionMessageRequest(string Content)
{
    public IReadOnlyDictionary<string, string>? PromptVars { get; init; }

    public JsonObject? Metadata { get; init; }
}

internal sealed record KitchenCreateRequest(string Name)
{
    public string? Description { get; init; }

    public IReadOnlyDictionary<string, string>? Tags { get; init; }
}

internal sealed record ServerHealth
{
    public string? Status { get; init; }

    public string? Service { get; init; }

    public string? Version { get; init; }
}
