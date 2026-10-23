using System.Text.Json.Nodes;

namespace AgentOven.Internal;

// Request and wrapper bodies that are not part of the public model.

internal sealed record InvokeRequest(string? Message)
{
    public IReadOnlyDictionary<string, string>? Variables { get; init; }

    public bool? ThinkingEnabled { get; init; }

    public string? SessionId { get; init; }

    public AudioClip? Audio { get; init; }

    public bool? VoiceOutput { get; init; }

    public string? Voice { get; init; }
}

internal sealed record ResumeRequest(bool Approved)
{
    public string? Note { get; init; }
}

internal sealed record UnfinishedRunsEnvelope
{
    public IReadOnlyList<UnfinishedRun>? Runs { get; init; }
}

internal sealed record SkillsEnvelope
{
    public IReadOnlyList<Skill>? Skills { get; init; }
}

internal sealed record SkillRegisterRequest(string Source)
{
    public string? SkillMd { get; init; }

    public IReadOnlyDictionary<string, string>? Files { get; init; }

    public string? UploadId { get; init; }

    public string? Path { get; init; }

    public string? GitUrl { get; init; }

    public string? GitRef { get; init; }

    public IReadOnlyDictionary<string, string>? Credentials { get; init; }
}

internal sealed record SkillDecision
{
    public string? Note { get; init; }
}

internal sealed record UploadBegin(long Size);

internal sealed record UploadState
{
    public string? UploadId { get; init; }

    public long? Received { get; init; }

    public string? Status { get; init; }
}

internal sealed record ModelCatalogEnvelope
{
    public IReadOnlyList<ModelCapability>? Models { get; init; }
}

internal sealed record PromptValidationEnvelope
{
    public PromptValidationReport? Report { get; init; }

    public string? Edition { get; init; }
}

internal sealed record CountEnvelope
{
    public long Count { get; init; }
}

internal sealed record ScopedKeyCreateRequest(string Label, IReadOnlyList<string> AgentNames)
{
    public int MaxCalls { get; init; }

    public string ExpiresIn { get; init; } = "";
}

internal sealed record RealtimeTokenRequest(int TtlSeconds);

internal sealed record RagIngestRequest(string Kitchen, IReadOnlyList<RagDocument> Documents)
{
    public int? ChunkSize { get; init; }

    public int? ChunkOverlap { get; init; }

    public string? EmbeddingProvider { get; init; }

    public string? Namespace { get; init; }
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
    public IReadOnlyList<Attachment>? ContentParts { get; init; }

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
