// AgentOven Pro models. Shapes follow the Pro server (github.com/agentoven/agentoven-pro), not the Python/TS clients,
// where they disagree (see docs/design/0001-api-surface.md, section 9).

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace AgentOven;

/// <summary>License state of a Pro server.</summary>
public sealed record LicenseStatus
{
    /// <summary>Whether a valid license is installed.</summary>
    public bool Valid { get; init; }

    /// <summary>community, pro or enterprise.</summary>
    public string? Edition { get; init; }

    /// <summary>Licensed organisation.</summary>
    public string? Org { get; init; }

    /// <summary>Plan.</summary>
    public string? Plan { get; init; }

    /// <summary>Expiry (RFC 3339, or empty).</summary>
    public string? ExpiresAt { get; init; }

    /// <summary>Explanation when unlicensed.</summary>
    public string? Message { get; init; }
}

/// <summary>Status of the license phone-home (read-only).</summary>
public sealed record PhoneHomeStatus
{
    /// <summary>Last success.</summary>
    public DateTimeOffset? LastSuccess { get; init; }

    /// <summary>Last attempt.</summary>
    public DateTimeOffset? LastAttempt { get; init; }

    /// <summary>Last error.</summary>
    public string? LastError { get; init; }

    /// <summary>Consecutive failures.</summary>
    public int ConsecutiveFails { get; init; }

    /// <summary>License revoked.</summary>
    public bool Revoked { get; init; }

    /// <summary>Running with degraded limits.</summary>
    public bool Degraded { get; init; }
}

/// <summary>The identity a login or token resolves to.</summary>
public sealed record AuthIdentity
{
    /// <summary>Subject.</summary>
    public string Subject { get; init; } = "";

    /// <summary>Email.</summary>
    public string? Email { get; init; }

    /// <summary>Display name.</summary>
    public string? DisplayName { get; init; }

    /// <summary>Auth provider.</summary>
    public string? Provider { get; init; }

    /// <summary>Kitchen.</summary>
    public string? Kitchen { get; init; }

    /// <summary>Role.</summary>
    public string? Role { get; init; }

    /// <summary>Groups.</summary>
    public IReadOnlyList<string>? Groups { get; init; }
}

/// <summary>A dashboard session token from <c>/auth/login</c>.</summary>
public sealed record LoginResult
{
    /// <summary>Bearer token.</summary>
    public string Token { get; init; } = "";

    /// <summary>Who it belongs to.</summary>
    public AuthIdentity? Identity { get; init; }
}

/// <summary>A user in the Pro user directory.</summary>
public sealed record ProUser
{
    /// <summary>Id.</summary>
    public string Id { get; init; } = "";

    /// <summary>Email.</summary>
    public string Email { get; init; } = "";

    /// <summary>Name.</summary>
    public string? Name { get; init; }

    /// <summary>admin, chef, baker, taster, auditor, finance or viewer.</summary>
    public string? Role { get; init; }

    /// <summary>Creation time.</summary>
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>Last update.</summary>
    public DateTimeOffset? UpdatedAt { get; init; }
}

/// <summary>A user's membership in a kitchen.</summary>
public sealed record KitchenMember
{
    /// <summary>Kitchen id.</summary>
    public string? KitchenId { get; init; }

    /// <summary>Kitchen name.</summary>
    public string? KitchenName { get; init; }

    /// <summary>User id.</summary>
    public string UserId { get; init; } = "";

    /// <summary>Email.</summary>
    public string? Email { get; init; }

    /// <summary>Name.</summary>
    public string? Name { get; init; }

    /// <summary>Role in the kitchen.</summary>
    public string? Role { get; init; }

    /// <summary>Who added them.</summary>
    public string? AddedBy { get; init; }

    /// <summary>When.</summary>
    public DateTimeOffset? JoinedAt { get; init; }
}

/// <summary>A workspace guardrail: applied to every agent in the kitchen, on top of the agent's own.</summary>
public sealed record WorkspaceGuardrail
{
    /// <summary>Creates a guardrail.</summary>
    public WorkspaceGuardrail(string name, GuardrailKind kind)
    {
        Name = name;
        Kind = kind;
    }

    /// <summary>Name.</summary>
    public string Name { get; init; }

    /// <summary>Kind.</summary>
    public GuardrailKind Kind { get; init; }

    /// <summary>Id.</summary>
    public string? Id { get; init; }

    /// <summary>Kitchen id.</summary>
    public string? KitchenId { get; init; }

    /// <summary>input, output or both (default).</summary>
    public GuardrailStage? Stage { get; init; }

    /// <summary>Kind-specific config.</summary>
    public JsonObject? Config { get; init; }

    /// <summary>Enabled (default true).</summary>
    public bool? Enabled { get; init; }

    /// <summary>Whether agents may be exempted.</summary>
    public bool? Overridable { get; init; }

    /// <summary>Evaluation order.</summary>
    public int? Priority { get; init; }

    /// <summary>Creation time.</summary>
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>Last update.</summary>
    public DateTimeOffset? UpdatedAt { get; init; }
}

/// <summary>An agent exempted from a workspace guardrail.</summary>
public sealed record GuardrailExemption
{
    /// <summary>Exception id (use it to remove the exception).</summary>
    public string Id { get; init; } = "";

    /// <summary>Kitchen id.</summary>
    public string? KitchenId { get; init; }

    /// <summary>Agent.</summary>
    public string AgentName { get; init; } = "";

    /// <summary>Guardrail.</summary>
    public string? GuardrailId { get; init; }

    /// <summary>Why.</summary>
    public string? Reason { get; init; }

    /// <summary>Approver.</summary>
    public string? ApprovedBy { get; init; }

    /// <summary>Approval time.</summary>
    public DateTimeOffset? ApprovedAt { get; init; }

    /// <summary>Expiry.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Creation time.</summary>
    public DateTimeOffset? CreatedAt { get; init; }
}

/// <summary>A deployment environment (dev, staging, prod, …).</summary>
public sealed record DeploymentEnvironment
{
    /// <summary>Creates an environment.</summary>
    public DeploymentEnvironment(string name, string slug)
    {
        Name = name;
        Slug = slug;
    }

    /// <summary>Name.</summary>
    public string Name { get; init; }

    /// <summary>URL-safe identifier.</summary>
    public string Slug { get; init; }

    /// <summary>Id.</summary>
    public string? Id { get; init; }

    /// <summary>Kitchen.</summary>
    public string? Kitchen { get; init; }

    /// <summary>Description.</summary>
    public string? Description { get; init; }

    /// <summary>URL.</summary>
    public string? Url { get; init; }

    /// <summary>Order in the promotion path.</summary>
    public int? Order { get; init; }

    /// <summary>Default environment.</summary>
    public bool? IsDefault { get; init; }

    /// <summary>Production: runs against it need explicit opt-in. Always sent.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public bool IsProduction { get; init; }

    /// <summary>Promotions into it need approval. Always sent.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public bool ApprovalRequired { get; init; }

    /// <summary>manual (default), auto or gated.</summary>
    public string? PromotionPolicy { get; init; }

    /// <summary>Config.</summary>
    public JsonObject? Config { get; init; }

    /// <summary>Provider overrides (logical name → provider).</summary>
    public IReadOnlyDictionary<string, string>? Providers { get; init; }

    /// <summary>Tags.</summary>
    public IReadOnlyDictionary<string, string>? Tags { get; init; }

    /// <summary>Creation time.</summary>
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>Last update.</summary>
    public DateTimeOffset? UpdatedAt { get; init; }
}

/// <summary>A request to promote an agent between environments.</summary>
public sealed record PromotionRequest(string AgentName, string FromEnv, string ToEnv)
{
    /// <summary>Version (default: the one active in <see cref="FromEnv"/>).</summary>
    public string? Version { get; init; }

    /// <summary>Report what would change without promoting.</summary>
    public bool? DryRun { get; init; }

    /// <summary>Comment.</summary>
    public string? Comment { get; init; }

    /// <summary>Ticket reference.</summary>
    public string? TrackerRef { get; init; }
}

/// <summary>The outcome of a promotion or rollback.</summary>
public sealed record PromotionResult
{
    /// <summary>Agent.</summary>
    public string AgentName { get; init; } = "";

    /// <summary>Source.</summary>
    public string? FromEnv { get; init; }

    /// <summary>Target.</summary>
    public string? ToEnv { get; init; }

    /// <summary>Version.</summary>
    public string? Version { get; init; }

    /// <summary>promoted, pending_approval, dry_run or failed.</summary>
    public string Status { get; init; } = "";

    /// <summary>Configuration diff.</summary>
    public JsonNode? Diff { get; init; }

    /// <summary>Error.</summary>
    public string? Error { get; init; }

    /// <summary>Approval id, when pending.</summary>
    public string? ApprovalId { get; init; }

    /// <summary>Deployment created.</summary>
    public string? DeploymentId { get; init; }

    /// <summary>Who.</summary>
    public string? PromotedBy { get; init; }

    /// <summary>When.</summary>
    public DateTimeOffset? PromotedAt { get; init; }
}

/// <summary>An agent version deployed to an environment.</summary>
public sealed record AgentDeployment
{
    /// <summary>Id.</summary>
    public string Id { get; init; } = "";

    /// <summary>Agent.</summary>
    public string AgentName { get; init; } = "";

    /// <summary>Kitchen.</summary>
    public string? Kitchen { get; init; }

    /// <summary>Environment.</summary>
    public string? Environment { get; init; }

    /// <summary>Version.</summary>
    public string? Version { get; init; }

    /// <summary>pending, active, rolled_back or superseded.</summary>
    public string? Status { get; init; }

    /// <summary>Source environment.</summary>
    public string? PromotedFrom { get; init; }

    /// <summary>Who deployed.</summary>
    public string? DeployedBy { get; init; }

    /// <summary>When.</summary>
    public DateTimeOffset? DeployedAt { get; init; }

    /// <summary>Rollback time.</summary>
    public DateTimeOffset? RolledBackAt { get; init; }

    /// <summary>Comment.</summary>
    public string? Comment { get; init; }

    /// <summary>Fields this SDK version does not model.</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

/// <summary>An agent baked into an environment, with per-environment overrides.</summary>
public sealed record AgentEnvironment
{
    /// <summary>Id.</summary>
    public string? Id { get; init; }

    /// <summary>Kitchen id.</summary>
    public string? KitchenId { get; init; }

    /// <summary>Agent.</summary>
    public string AgentName { get; init; } = "";

    /// <summary>Environment slug.</summary>
    public string EnvSlug { get; init; } = "";

    /// <summary>pending, baking, ready, burnt or cooled.</summary>
    public string? Status { get; init; }

    /// <summary>Agent version deployed.</summary>
    public string? AgentVersion { get; init; }

    /// <summary>Provider override.</summary>
    public string? ProviderName { get; init; }

    /// <summary>Model override.</summary>
    public string? ModelName { get; init; }

    /// <summary>Provider config overrides.</summary>
    public JsonObject? ProviderOverrides { get; init; }

    /// <summary>Tool overrides.</summary>
    public JsonObject? ToolOverrides { get; init; }

    /// <summary>inherit, strict, relaxed or disabled.</summary>
    public string? GuardrailPolicy { get; init; }

    /// <summary>Guardrails required here.</summary>
    public IReadOnlyList<string>? RequiredGuardrails { get; init; }

    /// <summary>Guardrails disabled here.</summary>
    public IReadOnlyList<string>? DisabledGuardrails { get; init; }

    /// <summary>Backend URL (external agents).</summary>
    public string? BackendEndpoint { get; init; }

    /// <summary>A2A URL.</summary>
    [JsonPropertyName("a2a_endpoint")]
    public string? A2AEndpoint { get; init; }

    /// <summary>Error.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Who baked it.</summary>
    public string? BakedBy { get; init; }

    /// <summary>When.</summary>
    public DateTimeOffset? BakedAt { get; init; }

    /// <summary>Fields this SDK version does not model.</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

/// <summary>Per-environment settings for <see cref="EnvironmentsClient.BakeAgentAsync"/>.</summary>
public sealed record AgentEnvironmentRequest
{
    /// <summary>Agent version.</summary>
    public string? Version { get; init; }

    /// <summary>Provider override.</summary>
    public string? ProviderName { get; init; }

    /// <summary>Model override.</summary>
    public string? ModelName { get; init; }

    /// <summary>Provider config overrides.</summary>
    public JsonObject? ProviderOverrides { get; init; }

    /// <summary>Tool overrides.</summary>
    public JsonObject? ToolOverrides { get; init; }

    /// <summary>inherit (default), strict, relaxed or disabled.</summary>
    public string? GuardrailPolicy { get; init; }

    /// <summary>Guardrails required here.</summary>
    public IReadOnlyList<string>? RequiredGuardrails { get; init; }

    /// <summary>Guardrails disabled here.</summary>
    public IReadOnlyList<string>? DisabledGuardrails { get; init; }

    /// <summary>Backend URL, for external agents (the environment is then ready immediately).</summary>
    public string? BackendEndpoint { get; init; }
}

/// <summary>Which version of each agent runs in each environment.</summary>
public sealed record TraceabilityMatrix
{
    /// <summary>Agents.</summary>
    public IReadOnlyList<string> Agents { get; init; } = [];

    /// <summary>Environments.</summary>
    public IReadOnlyList<string> Environments { get; init; } = [];

    /// <summary>agent → environment → deployment.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, MatrixCell>> Matrix { get; init; } =
        new Dictionary<string, IReadOnlyDictionary<string, MatrixCell>>();
}

/// <summary>One cell of the traceability matrix.</summary>
public sealed record MatrixCell
{
    /// <summary>Deployment id.</summary>
    public string? Id { get; init; }

    /// <summary>Version.</summary>
    public string? Version { get; init; }

    /// <summary>Status.</summary>
    public string? Status { get; init; }

    /// <summary>When.</summary>
    public DateTimeOffset? DeployedAt { get; init; }

    /// <summary>Who.</summary>
    public string? DeployedBy { get; init; }
}

/// <summary>A machine identity for CI and services.</summary>
public sealed record ServiceAccount
{
    /// <summary>Id.</summary>
    public string Id { get; init; } = "";

    /// <summary>Name.</summary>
    public string Name { get; init; } = "";

    /// <summary>Kitchen.</summary>
    public string? Kitchen { get; init; }

    /// <summary>Description.</summary>
    public string? Description { get; init; }

    /// <summary>Role.</summary>
    public string? Role { get; init; }

    /// <summary>Scopes.</summary>
    public IReadOnlyList<string>? Scopes { get; init; }

    /// <summary>Token prefix.</summary>
    public string? TokenPrefix { get; init; }

    /// <summary>Expiry.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Creator.</summary>
    public string? CreatedBy { get; init; }

    /// <summary>Creation time.</summary>
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>Last use.</summary>
    public DateTimeOffset? LastUsedAt { get; init; }

    /// <summary>Revoked.</summary>
    public bool Revoked { get; init; }
}

/// <summary>A service account and its token (shown once).</summary>
public sealed record ServiceAccountToken
{
    /// <summary>The account (absent on rotate).</summary>
    public ServiceAccount? ServiceAccount { get; init; }

    /// <summary>The <c>ao_sa_…</c> token. Pass it as <see cref="AgentOvenClientOptions.ApiKey"/>.</summary>
    public string Token { get; init; } = "";

    /// <summary>Server warning.</summary>
    public string? Warning { get; init; }
}

/// <summary>A cron schedule that runs a recipe.</summary>
public sealed record Schedule
{
    /// <summary>Id.</summary>
    public string Id { get; init; } = "";

    /// <summary>Kitchen id.</summary>
    public string? KitchenId { get; init; }

    /// <summary>Recipe.</summary>
    public string RecipeName { get; init; } = "";

    /// <summary>Cron expression (UTC).</summary>
    public string CronExpr { get; init; } = "";

    /// <summary>Enabled.</summary>
    public bool Enabled { get; init; }

    /// <summary>Last run.</summary>
    public DateTimeOffset? LastRunAt { get; init; }

    /// <summary>Next run.</summary>
    public DateTimeOffset? NextRunAt { get; init; }

    /// <summary>Next run, in words.</summary>
    public string? NextRunHuman { get; init; }

    /// <summary>Creator.</summary>
    public string? CreatedBy { get; init; }

    /// <summary>Creation time.</summary>
    public DateTimeOffset? CreatedAt { get; init; }
}

/// <summary>A suite of test cases for one agent.</summary>
public sealed record TestSuite
{
    /// <summary>Creates a suite.</summary>
    public TestSuite(string name, string agentName)
    {
        Name = name;
        AgentName = agentName;
    }

    /// <summary>Name.</summary>
    public string Name { get; init; }

    /// <summary>Agent under test.</summary>
    public string AgentName { get; init; }

    /// <summary>Id.</summary>
    public string? Id { get; init; }

    /// <summary>Kitchen.</summary>
    public string? Kitchen { get; init; }

    /// <summary>Description.</summary>
    public string? Description { get; init; }

    /// <summary>Cases.</summary>
    public IReadOnlyList<TestCase> Cases { get; init; } = [];

    /// <summary>Cron schedule.</summary>
    public string? Schedule { get; init; }

    /// <summary>Next scheduled run.</summary>
    public DateTimeOffset? NextRunAt { get; init; }

    /// <summary>Enabled. Always sent (an update without it disables the suite).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public bool Enabled { get; init; } = true;

    /// <summary>Ticket reference.</summary>
    public string? TrackerRef { get; init; }

    /// <summary>Creator.</summary>
    public string? CreatedBy { get; init; }

    /// <summary>Creation time.</summary>
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>Starts a fluent definition.</summary>
    public static TestSuiteBuilder For(string agentName, string? name = null) => new(agentName, name ?? $"{agentName}-tests");
}

/// <summary>
/// One test case: either a response check (<see cref="Input"/> and optional <see cref="ExpectedOutput"/>) or a scenario
/// auto-pick check (<see cref="ExpectedScenarioId"/> and <see cref="MinPassRate"/>), never both.
/// </summary>
public sealed record TestCase(string Name)
{
    /// <summary>Id.</summary>
    public string? Id { get; init; }

    /// <summary>Message sent to the agent.</summary>
    public string? Input { get; init; }

    /// <summary>Expected output.</summary>
    public string? ExpectedOutput { get; init; }

    /// <summary>Tags.</summary>
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>Prompt variables.</summary>
    public IReadOnlyDictionary<string, string>? Variables { get; init; }

    /// <summary>Latency budget.</summary>
    public long? MaxLatencyMs { get; init; }

    /// <summary>The scenario the agent should pick and pass.</summary>
    public string? ExpectedScenarioId { get; init; }

    /// <summary>Required pass rate (0–1) for a scenario check.</summary>
    public double? MinPassRate { get; init; }
}

/// <summary>Fluent definition of a <see cref="TestSuite"/>.</summary>
public sealed class TestSuiteBuilder
{
    private readonly List<TestCase> _cases = [];
    private TestSuite _suite;

    internal TestSuiteBuilder(string agentName, string name) => _suite = new TestSuite(name, agentName);

    /// <summary>Sets the description.</summary>
    public TestSuiteBuilder Describe(string description)
    {
        _suite = _suite with { Description = description };
        return this;
    }

    /// <summary>Runs the suite on a cron schedule.</summary>
    public TestSuiteBuilder OnSchedule(string cron)
    {
        _suite = _suite with { Schedule = cron };
        return this;
    }

    /// <summary>Adds a response check.</summary>
    public TestSuiteBuilder Case(string name, string input, string? expect = null, long? maxLatencyMs = null)
    {
        _cases.Add(new TestCase(name) { Input = input, ExpectedOutput = expect, MaxLatencyMs = maxLatencyMs });
        return this;
    }

    /// <summary>Adds a scenario auto-pick check.</summary>
    public TestSuiteBuilder ScenarioCase(string name, string scenarioId, double minPassRate = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(minPassRate, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minPassRate, 1);
        _cases.Add(new TestCase(name) { ExpectedScenarioId = scenarioId, MinPassRate = minPassRate });
        return this;
    }

    /// <summary>Returns the suite.</summary>
    public TestSuite Build()
    {
        if (_cases.Count == 0)
        {
            throw new InvalidOperationException("A test suite needs at least one case.");
        }

        return _suite with { Cases = [.. _cases] };
    }

    /// <summary>Builds the suite.</summary>
    public static implicit operator TestSuite(TestSuiteBuilder builder) => builder.Build();
}

/// <summary>A run of a test suite.</summary>
public sealed record TestRun
{
    /// <summary>Id.</summary>
    public string Id { get; init; } = "";

    /// <summary>Suite.</summary>
    public string? SuiteId { get; init; }

    /// <summary>Suite name.</summary>
    public string? SuiteName { get; init; }

    /// <summary>Agent.</summary>
    public string? AgentName { get; init; }

    /// <summary>pending, running, completed or failed.</summary>
    public string Status { get; init; } = "";

    /// <summary>Per-case results.</summary>
    public IReadOnlyList<TestResult>? Results { get; init; }

    /// <summary>Summary.</summary>
    public TestRunSummary? Summary { get; init; }

    /// <summary>manual, schedule, …</summary>
    public string? Trigger { get; init; }

    /// <summary>Environment.</summary>
    public string? Environment { get; init; }

    /// <summary>Agent version.</summary>
    public string? AgentVersion { get; init; }

    /// <summary>Start.</summary>
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>End.</summary>
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>Duration.</summary>
    public long? DurationMs { get; init; }

    /// <summary>Finished (completed or failed).</summary>
    [JsonIgnore]
    public bool IsFinished => Status is "completed" or "failed" or "cancelled";
}

/// <summary>The result of one test case.</summary>
public sealed record TestResult
{
    /// <summary>Case id.</summary>
    public string? CaseId { get; init; }

    /// <summary>Case name.</summary>
    public string? CaseName { get; init; }

    /// <summary>Input.</summary>
    public string? Input { get; init; }

    /// <summary>Expected output.</summary>
    public string? ExpectedOutput { get; init; }

    /// <summary>Actual output.</summary>
    public string? ActualOutput { get; init; }

    /// <summary>Passed.</summary>
    public bool Passed { get; init; }

    /// <summary>Latency.</summary>
    public long LatencyMs { get; init; }

    /// <summary>Tokens.</summary>
    public long TokensUsed { get; init; }

    /// <summary>Cost.</summary>
    [JsonPropertyName("cost_usd")]
    public double CostUsd { get; init; }

    /// <summary>Error.</summary>
    public string? Error { get; init; }

    /// <summary>Trace.</summary>
    public string? TraceId { get; init; }

    /// <summary>Scenario the agent picked.</summary>
    public string? ActualScenarioId { get; init; }
}

/// <summary>Totals of a test run.</summary>
public sealed record TestRunSummary
{
    /// <summary>Cases.</summary>
    public int TotalCases { get; init; }

    /// <summary>Passed.</summary>
    public int Passed { get; init; }

    /// <summary>Failed.</summary>
    public int Failed { get; init; }

    /// <summary>Errors.</summary>
    public int Errors { get; init; }

    /// <summary>Pass rate (0–1).</summary>
    public double PassRate { get; init; }

    /// <summary>Average latency.</summary>
    public double AvgLatencyMs { get; init; }

    /// <summary>Tokens.</summary>
    public long TotalTokens { get; init; }

    /// <summary>Cost.</summary>
    [JsonPropertyName("total_cost_usd")]
    public double TotalCostUsd { get; init; }
}

/// <summary>
/// A world schema (ontology) that scenarios' worlds conform to. Classes, relationships and constraints are kept as
/// JSON; author them in YAML or with your own types.
/// </summary>
public sealed record WorldSchema
{
    /// <summary>Creates a schema.</summary>
    public WorldSchema(string id, string version)
    {
        Id = id;
        Version = version;
    }

    /// <summary>Id.</summary>
    public string Id { get; init; }

    /// <summary>Version.</summary>
    public string Version { get; init; }

    /// <summary>Domain.</summary>
    public string? Domain { get; init; }

    /// <summary>Description.</summary>
    public string? Description { get; init; }

    /// <summary>Status.</summary>
    public string? Status { get; init; }

    /// <summary>Classes: <c>[{name, properties:[{name, datatype, …}], …}]</c>.</summary>
    public JsonArray? Classes { get; init; }

    /// <summary>Relationships: <c>[{name, from, to, cardinality?}]</c>.</summary>
    public JsonArray? Relationships { get; init; }

    /// <summary>Constraints: <c>[{name, expression}]</c>.</summary>
    public JsonArray? Constraints { get; init; }
}

/// <summary>A validation finding (from <c>proof</c>).</summary>
public sealed record ProofIssue
{
    /// <summary>error or warning.</summary>
    public string Level { get; init; } = "";

    /// <summary>Where.</summary>
    public string? Where { get; init; }

    /// <summary>Message.</summary>
    public string Message { get; init; } = "";
}

/// <summary>A saved world schema and the issues found in it (it is saved even when invalid).</summary>
public sealed record WorldSchemaSaveResult
{
    /// <summary>The schema.</summary>
    public WorldSchema? Schema { get; init; }

    /// <summary>Issues.</summary>
    public IReadOnlyList<ProofIssue> Issues { get; init; } = [];
}

/// <summary>
/// An evaluation scenario: a world, the tools the agent may use, a task, and assertions over the final state
/// (graded deterministically by <c>proof</c>). The nested sections are JSON; see <c>spec/scenario/v1/scenario.schema.json</c>.
/// </summary>
public sealed record Scenario
{
    /// <summary>Creates a scenario.</summary>
    public Scenario(string id) => Id = id;

    /// <summary>Id.</summary>
    public string Id { get; init; }

    /// <summary>Version.</summary>
    public int? Version { get; init; }

    /// <summary>World schema version.</summary>
    public string? OntologyVersion { get; init; }

    /// <summary>Description.</summary>
    public string? Description { get; init; }

    /// <summary>Tags.</summary>
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>World: fixtures or live, instances, invariants, setup.</summary>
    public JsonObject? World { get; init; }

    /// <summary>Tools: live, world-backed or simulated, with faults.</summary>
    public JsonArray? Tools { get; init; }

    /// <summary>Simulated actors (users, counterparties).</summary>
    public JsonArray? Actors { get; init; }

    /// <summary>The task: <c>{agent_goal, agent_context?}</c>.</summary>
    public ScenarioTask? Task { get; init; }

    /// <summary>Assertions: <c>{must, must_not, budgets, unchanged}</c>.</summary>
    public JsonObject? Assertions { get; init; }

    /// <summary>Reward terms.</summary>
    public JsonObject? Reward { get; init; }

    /// <summary>Rubric.</summary>
    public JsonObject? Rubric { get; init; }

    /// <summary>Judge.</summary>
    public JsonNode? Judge { get; init; }

    /// <summary>Simulator.</summary>
    public JsonNode? Simulator { get; init; }

    /// <summary>Validation issues (on reads).</summary>
    public IReadOnlyList<ProofIssue>? Issues { get; init; }

    /// <summary>Fields this SDK version does not model.</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

/// <summary>What the agent is asked to do in a scenario.</summary>
/// <param name="AgentGoal">The goal.</param>
public sealed record ScenarioTask(string AgentGoal)
{
    /// <summary>Context given to the agent.</summary>
    public string? AgentContext { get; init; }
}

/// <summary>A scenario search hit.</summary>
public sealed record ScenarioMatch
{
    /// <summary>Scenario.</summary>
    public string ScenarioId { get; init; } = "";

    /// <summary>Relevance.</summary>
    public double Score { get; init; }

    /// <summary>Description.</summary>
    public string? Description { get; init; }

    /// <summary>Tags.</summary>
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>Tools.</summary>
    public IReadOnlyList<string>? Tools { get; init; }

    /// <summary>Runnable with the agent's tools.</summary>
    public bool Runnable { get; init; }

    /// <summary>Tools the agent lacks.</summary>
    public IReadOnlyList<string>? MissingTools { get; init; }

    /// <summary>Why it matched.</summary>
    public IReadOnlyList<string>? Why { get; init; }
}

/// <summary>A request to run scenarios against an agent.</summary>
public sealed record ScenarioRunRequest(string AgentName)
{
    /// <summary>Scenarios to run; omit to let the agent's scenario ingredients decide.</summary>
    public IReadOnlyList<string>? ScenarioIds { get; init; }

    /// <summary>Agent config version, for comparability.</summary>
    public string? AgentConfigVersion { get; init; }

    /// <summary>Episodes per scenario (≤ 200).</summary>
    public int? Rollouts { get; init; }

    /// <summary>Seeds.</summary>
    public IReadOnlyList<long>? Seeds { get; init; }

    /// <summary>Trigger label (default <c>api</c>).</summary>
    public string? Trigger { get; init; }

    /// <summary>Environment.</summary>
    public string? Environment { get; init; }

    /// <summary>Required to run against a production environment.</summary>
    public bool? AllowProduction { get; init; }
}

/// <summary>A scenario run: many episodes, graded and aggregated.</summary>
public sealed record ScenarioRun
{
    /// <summary>Id.</summary>
    public string Id { get; init; } = "";

    /// <summary>Kitchen.</summary>
    public string? Kitchen { get; init; }

    /// <summary>queued, running, completed, failed, cancelled or interrupted.</summary>
    public string Status { get; init; } = "";

    /// <summary>The request.</summary>
    public ScenarioRunRequest? Request { get; init; }

    /// <summary>Per-scenario results.</summary>
    public IReadOnlyList<ScenarioResult>? Results { get; init; }

    /// <summary>Error.</summary>
    public string? Error { get; init; }

    /// <summary>Aggregate statistics (<c>proof.Summary</c>).</summary>
    public JsonObject? Summary { get; init; }

    /// <summary>met or not_met.</summary>
    public string? Gate { get; init; }

    /// <summary>Episodes done.</summary>
    public int Done { get; init; }

    /// <summary>Episodes total.</summary>
    public int Total { get; init; }

    /// <summary>Creation time.</summary>
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>Start.</summary>
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>End.</summary>
    public DateTimeOffset? FinishedAt { get; init; }

    /// <summary>No longer running.</summary>
    [JsonIgnore]
    public bool IsFinished => Status is "completed" or "failed" or "cancelled" or "interrupted";
}

/// <summary>Results of one scenario within a run.</summary>
public sealed record ScenarioResult
{
    /// <summary>Scenario.</summary>
    public string ScenarioId { get; init; } = "";

    /// <summary>Episodes.</summary>
    public int Episodes { get; init; }

    /// <summary>Passed.</summary>
    public int Passed { get; init; }

    /// <summary>Failed.</summary>
    public int Failed { get; init; }

    /// <summary>Invalid.</summary>
    public int Invalid { get; init; }

    /// <summary>Pass rate.</summary>
    public double PassRate { get; init; }

    /// <summary>95% CI low.</summary>
    public double CiLow { get; init; }

    /// <summary>95% CI high.</summary>
    public double CiHigh { get; init; }

    /// <summary>Mean reward.</summary>
    public double MeanReward { get; init; }

    /// <summary>Cost.</summary>
    [JsonPropertyName("total_cost_usd")]
    public double TotalCostUsd { get; init; }

    /// <summary>Required pass rate.</summary>
    public double? MinPassRate { get; init; }

    /// <summary>met or not_met.</summary>
    public string? Gate { get; init; }
}

/// <summary>One graded episode of a scenario run.</summary>
public sealed record EpisodeRecord
{
    /// <summary>Run.</summary>
    public string RunId { get; init; } = "";

    /// <summary>Scenario.</summary>
    public string ScenarioId { get; init; } = "";

    /// <summary>Seed.</summary>
    public long Seed { get; init; }

    /// <summary>The episode: status, steps, cost, wall time, trace.</summary>
    public JsonObject? Episode { get; init; }

    /// <summary>The verdict.</summary>
    public Verdict? Verdict { get; init; }

    /// <summary>Error.</summary>
    public string? Error { get; init; }

    /// <summary>Simulated actors' cost.</summary>
    [JsonPropertyName("actor_cost_usd")]
    public double? ActorCostUsd { get; init; }
}

/// <summary>A <c>proof</c> verdict: what the agent left behind, graded against the scenario's spec.</summary>
public sealed record Verdict
{
    /// <summary>passed, failed or invalid.</summary>
    public string Status { get; init; } = "";

    /// <summary>Scalar reward.</summary>
    public double Reward { get; init; }

    /// <summary>Every assertion, with what it got and wanted.</summary>
    public IReadOnlyList<AssertionResult> Assertions { get; init; } = [];

    /// <summary>Budget penalties.</summary>
    public IReadOnlyList<Penalty>? Penalties { get; init; }

    /// <summary>Rubric result.</summary>
    public JsonObject? Rubric { get; init; }

    /// <summary>Why it is invalid or failed.</summary>
    public string? Reason { get; init; }

    /// <summary>Passed.</summary>
    [JsonIgnore]
    public bool Passed => Status == "passed";
}

/// <summary>One assertion of a verdict.</summary>
public sealed record AssertionResult
{
    /// <summary>Name.</summary>
    public string Name { get; init; } = "";

    /// <summary>must, must_not or unchanged.</summary>
    public string? Kind { get; init; }

    /// <summary>Satisfied.</summary>
    public bool Satisfied { get; init; }

    /// <summary>What the state contained.</summary>
    public JsonNode? Got { get; init; }

    /// <summary>What was expected.</summary>
    public JsonNode? Want { get; init; }

    /// <summary>Query error.</summary>
    public string? Error { get; init; }
}

/// <summary>A budget penalty.</summary>
public sealed record Penalty
{
    /// <summary>Source.</summary>
    public string Source { get; init; } = "";

    /// <summary>Weight.</summary>
    public double Weight { get; init; }

    /// <summary>Amount.</summary>
    public double Amount { get; init; }

    /// <summary>Detail.</summary>
    public string? Detail { get; init; }
}

/// <summary>An LLM or System One judge used by rubric criteria.</summary>
public sealed record Judge(string Name)
{
    /// <summary>Description.</summary>
    public string? Description { get; init; }

    /// <summary>llm or system-one.</summary>
    public string? Kind { get; init; }

    /// <summary>Provider.</summary>
    public string? Provider { get; init; }

    /// <summary>Model.</summary>
    public string? Model { get; init; }

    /// <summary>Endpoint (System One).</summary>
    public string? Endpoint { get; init; }

    /// <summary>Credential holding the API key.</summary>
    public string? ApiKeyRef { get; init; }

    /// <summary>Minimum confidence before escalation.</summary>
    public double? MinConfidence { get; init; }

    /// <summary>Judge to escalate to.</summary>
    public string? Escalate { get; init; }

    /// <summary>Temperature.</summary>
    public double? Temperature { get; init; }

    /// <summary>Max tokens.</summary>
    public int? MaxTokens { get; init; }

    /// <summary>Guidance.</summary>
    public string? Guidance { get; init; }

    /// <summary>Fraction of judgments sent for human review.</summary>
    public double? ReviewRate { get; init; }

    /// <summary>Calibration.</summary>
    public JsonObject? Calibration { get; init; }

    /// <summary>Creator.</summary>
    public string? CreatedBy { get; init; }

    /// <summary>Validation issues (on reads).</summary>
    public IReadOnlyList<ProofIssue>? Issues { get; init; }

    /// <summary>Fields this SDK version does not model.</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

/// <summary>A Kubernetes workload record for an agent.</summary>
public sealed record Workload
{
    /// <summary>Id.</summary>
    public string? Id { get; init; }

    /// <summary>Kitchen id.</summary>
    public string? KitchenId { get; init; }

    /// <summary>Agent.</summary>
    public string AgentName { get; init; } = "";

    /// <summary>Image.</summary>
    public string? Image { get; init; }

    /// <summary>Environment.</summary>
    public string? EnvironmentSlug { get; init; }

    /// <summary>Replicas.</summary>
    public int? Replicas { get; init; }

    /// <summary>Phase.</summary>
    public string? Phase { get; init; }

    /// <summary>Ready replicas.</summary>
    public int? ReadyReplicas { get; init; }

    /// <summary>Message.</summary>
    public string? Message { get; init; }

    /// <summary>Namespace.</summary>
    [JsonPropertyName("k8s_namespace")]
    public string? KubernetesNamespace { get; init; }

    /// <summary>Resource name.</summary>
    [JsonPropertyName("k8s_name")]
    public string? KubernetesName { get; init; }

    /// <summary>Fields this SDK version does not model.</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

/// <summary>A kitchen credential (the value is never returned). Tools and skills reference it by name.</summary>
public sealed record Credential
{
    /// <summary>Id.</summary>
    public string? Id { get; init; }

    /// <summary>Kitchen.</summary>
    public string? Kitchen { get; init; }

    /// <summary>Name.</summary>
    public string Name { get; init; } = "";

    /// <summary>Label.</summary>
    public string? Label { get; init; }

    /// <summary>Creation time.</summary>
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>Last rotation.</summary>
    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>Creator.</summary>
    public string? CreatedBy { get; init; }
}

/// <summary>A realtime voice session token.</summary>
public sealed record RealtimeToken
{
    /// <summary>Token, offered as the <c>agentoven-token.&lt;token&gt;</c> subprotocol.</summary>
    public string Token { get; init; } = "";

    /// <summary>Lifetime in seconds.</summary>
    public int ExpiresIn { get; init; }
}

/// <summary>Result of importing agents from an <c>agents.md</c> file.</summary>
public sealed record AgentImportResult
{
    /// <summary>Agents created.</summary>
    public int AgentsCreated { get; init; }

    /// <summary>Providers created.</summary>
    public int ProvidersCreated { get; init; }

    /// <summary>Tools created.</summary>
    public int ToolsCreated { get; init; }

    /// <summary>Errors.</summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>Dry run.</summary>
    public bool DryRun { get; init; }
}
