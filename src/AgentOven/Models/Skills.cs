using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentOven;

/// <summary>
/// A skill (Agent Skills format: a <c>SKILL.md</c> plus optional files and MCP servers) registered in a kitchen.
/// A skill is usable only once a provider-based intent review accepts it. Agents use it through
/// <see cref="Ingredient.Skill"/>.
/// </summary>
public sealed record Skill
{
    /// <summary>Id.</summary>
    public string Id { get; init; } = "";

    /// <summary>Name.</summary>
    public string Name { get; init; } = "";

    /// <summary>Kitchen.</summary>
    public string? Kitchen { get; init; }

    /// <summary>inline, upload, path, git or plugin.</summary>
    public string? Source { get; init; }

    /// <summary>Where it came from (path or git URL).</summary>
    public string? SourceRef { get; init; }

    /// <summary>Parsed manifest.</summary>
    public SkillManifest? Manifest { get; init; }

    /// <summary>pending, accepted, rejected or needs_review.</summary>
    public string Status { get; init; } = "";

    /// <summary>Verification verdict.</summary>
    public string? VerificationVerdict { get; init; }

    /// <summary>Why the reviewer decided as it did.</summary>
    public string? VerificationReasoning { get; init; }

    /// <summary>When it was verified.</summary>
    public DateTimeOffset? VerifiedAt { get; init; }

    /// <summary>Provider that reviewed it.</summary>
    public string? VerifiedByProvider { get; init; }

    /// <summary>MCP tools the skill registered.</summary>
    public IReadOnlyList<string>? RegisteredTools { get; init; }

    /// <summary>Creation time.</summary>
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>Last update.</summary>
    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>Creator.</summary>
    public string? CreatedBy { get; init; }

    /// <summary><see langword="true"/> once the skill passed review and agents can use it.</summary>
    [JsonIgnore]
    public bool IsAccepted => string.Equals(Status, "accepted", StringComparison.OrdinalIgnoreCase);

    /// <summary>Fields this SDK version does not model.</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

/// <summary>A skill's <c>SKILL.md</c> frontmatter and body.</summary>
public sealed record SkillManifest
{
    /// <summary>Name.</summary>
    public string Name { get; init; } = "";

    /// <summary>Description.</summary>
    public string? Description { get; init; }

    /// <summary>License.</summary>
    public string? License { get; init; }

    /// <summary>Tools the skill may use.</summary>
    public IReadOnlyList<string>? AllowedTools { get; init; }

    /// <summary>MCP servers bundled with the skill.</summary>
    public IReadOnlyList<SkillMcpServer>? McpTools { get; init; }

    /// <summary>Instructions (the Markdown body).</summary>
    public string? Instructions { get; init; }
}

/// <summary>An MCP server a skill brings along.</summary>
public sealed record SkillMcpServer
{
    /// <summary>Name.</summary>
    public string Name { get; init; } = "";

    /// <summary>Description.</summary>
    public string? Description { get; init; }

    /// <summary>http, sse or mcp.</summary>
    public string? Transport { get; init; }

    /// <summary>Endpoint.</summary>
    public string? Endpoint { get; init; }

    /// <summary>bearer or api-key.</summary>
    public string? AuthType { get; init; }

    /// <summary>Header for api-key auth.</summary>
    public string? AuthHeader { get; init; }

    /// <summary>Kitchen credential holding the secret.</summary>
    public string? CredentialRef { get; init; }
}

/// <summary>
/// The outcome of registering or refreshing a skill: <see cref="Accepted"/> (usable now), <see cref="NeedsReview"/>
/// (waiting for a human — approve or reject it), or <see cref="Rejected"/> (nothing registered).
/// </summary>
public sealed record SkillRegistration
{
    /// <summary>accepted, needs_review or rejected.</summary>
    public string Status { get; init; } = "";

    /// <summary>The registered skill, when accepted (or the skill awaiting review, when the server returns it).</summary>
    public Skill? Skill { get; init; }

    /// <summary>The reviewer's reasoning.</summary>
    public string? Reasoning { get; init; }

    /// <summary>Note from the server.</summary>
    public string? Note { get; init; }

    /// <summary>Usable now.</summary>
    public bool Accepted => string.Equals(Status, "accepted", StringComparison.OrdinalIgnoreCase);

    /// <summary>Waiting for a human decision.</summary>
    public bool NeedsReview => string.Equals(Status, "needs_review", StringComparison.OrdinalIgnoreCase);

    /// <summary>Refused by the reviewer.</summary>
    public bool Rejected => string.Equals(Status, "rejected", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Import skills (and their MCP servers) from a Claude Code / Agent Skills plugin repository.</summary>
public sealed record SkillImportRequest(string GitUrl)
{
    /// <summary>Branch or tag.</summary>
    public string? GitRef { get; init; }

    /// <summary>Pinned commit.</summary>
    public string? GitSha { get; init; }

    /// <summary>Directory of the plugin inside the repository.</summary>
    public string? Path { get; init; }

    /// <summary>Plugin name override.</summary>
    public string? Name { get; init; }

    /// <summary>Directories to look for skills in.</summary>
    public IReadOnlyList<string>? SkillPaths { get; init; }

    /// <summary>Import only these skills.</summary>
    public IReadOnlyList<string>? Only { get; init; }

    /// <summary>Plan the import without registering anything.</summary>
    public bool? DryRun { get; init; }

    /// <summary>Kitchen credential per MCP server name.</summary>
    public IReadOnlyDictionary<string, string>? Credentials { get; init; }
}

/// <summary>The plan (and, unless dry-run, the results) of a plugin import.</summary>
public sealed record SkillImportResult
{
    /// <summary>The plugin.</summary>
    public PluginInfo? Plugin { get; init; }

    /// <summary>Skills found.</summary>
    public IReadOnlyList<PluginSkill> Skills { get; init; } = [];

    /// <summary>MCP servers found.</summary>
    public IReadOnlyList<PluginServer> Servers { get; init; } = [];

    /// <summary>Components skipped, and why.</summary>
    public IReadOnlyList<PluginSkipped> Skipped { get; init; } = [];

    /// <summary>Per-skill registration outcome (absent on a dry run).</summary>
    public IReadOnlyList<PluginImportOutcome>? Results { get; init; }

    /// <summary>Fields this SDK version does not model.</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

/// <summary>A plugin.</summary>
public sealed record PluginInfo
{
    /// <summary>Name.</summary>
    public string Name { get; init; } = "";

    /// <summary>Description.</summary>
    public string? Description { get; init; }

    /// <summary>Version.</summary>
    public string? Version { get; init; }
}

/// <summary>A skill found in a plugin.</summary>
public sealed record PluginSkill
{
    /// <summary>Name.</summary>
    public string Name { get; init; } = "";

    /// <summary>Description.</summary>
    public string? Description { get; init; }

    /// <summary>Directory.</summary>
    public string? Dir { get; init; }
}

/// <summary>An MCP server found in a plugin.</summary>
public sealed record PluginServer
{
    /// <summary>Name.</summary>
    public string Name { get; init; } = "";

    /// <summary>Endpoint.</summary>
    public string? Endpoint { get; init; }

    /// <summary>Whether it needs a credential mapping.</summary>
    public bool? NeedsCredential { get; init; }
}

/// <summary>A plugin component that was not imported.</summary>
public sealed record PluginSkipped
{
    /// <summary>Component kind.</summary>
    public string Component { get; init; } = "";

    /// <summary>Name.</summary>
    public string? Name { get; init; }

    /// <summary>Why.</summary>
    public string? Reason { get; init; }
}

/// <summary>The registration outcome of one imported skill.</summary>
public sealed record PluginImportOutcome
{
    /// <summary>Skill name.</summary>
    public string Name { get; init; } = "";

    /// <summary>accepted, needs_review, rejected or error.</summary>
    public string Status { get; init; } = "";

    /// <summary>Reviewer reasoning.</summary>
    public string? Reasoning { get; init; }

    /// <summary>Error.</summary>
    public string? Error { get; init; }
}
