// AgentOven Pro resource clients. These routes exist only on a Pro server; a community server answers 404.
// Verbs, paths and bodies follow the Pro server, which differs from the Python/TS clients in places
// (guardrail toggle is PATCH, exceptions are removed by id, environments need a slug, schedules use cron_expr, …).

using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using AgentOven.Internal;

namespace AgentOven;

public sealed partial class AgentOvenClient
{
    private void InitializePro(AgentOvenHttp http)
    {
        Users = new UsersClient(http);
        Guardrails = new GuardrailsClient(http);
        Environments = new EnvironmentsClient(http);
        ServiceAccounts = new ServiceAccountsClient(http);
        Schedules = new SchedulesClient(http);
        TestSuites = new TestSuitesClient(http);
        WorldSchemas = new WorldSchemasClient(http);
        Scenarios = new ScenariosClient(http);
        Judges = new JudgesClient(http);
        Workloads = new WorkloadsClient(http);
        Credentials = new CredentialsClient(http);
        Realtime = new RealtimeClient(http);
    }

    /// <summary>Users (Pro).</summary>
    public UsersClient Users { get; private set; } = null!;

    /// <summary>Workspace guardrails (Pro).</summary>
    public GuardrailsClient Guardrails { get; private set; } = null!;

    /// <summary>Environments, promotions and deployments (Pro).</summary>
    public EnvironmentsClient Environments { get; private set; } = null!;

    /// <summary>Service accounts (Pro).</summary>
    public ServiceAccountsClient ServiceAccounts { get; private set; } = null!;

    /// <summary>Recipe schedules (Pro).</summary>
    public SchedulesClient Schedules { get; private set; } = null!;

    /// <summary>Test suites (Pro).</summary>
    public TestSuitesClient TestSuites { get; private set; } = null!;

    /// <summary>World schemas for scenarios (Pro).</summary>
    public WorldSchemasClient WorldSchemas { get; private set; } = null!;

    /// <summary>Evaluation scenarios and their runs (Pro).</summary>
    public ScenariosClient Scenarios { get; private set; } = null!;

    /// <summary>Rubric judges (Pro).</summary>
    public JudgesClient Judges { get; private set; } = null!;

    /// <summary>Kubernetes workloads (Pro).</summary>
    public WorkloadsClient Workloads { get; private set; } = null!;

    /// <summary>Kitchen credentials (Pro).</summary>
    public CredentialsClient Credentials { get; private set; } = null!;

    /// <summary>Realtime voice sessions (Pro).</summary>
    public RealtimeClient Realtime { get; private set; } = null!;

    /// <summary>Gets the license state (Pro; always answers).</summary>
    public Task<LicenseStatus> GetLicenseStatusAsync(CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("license", "status"), AgentOvenJsonContext.Default.LicenseStatus, cancellationToken);

    /// <summary>Gets the license phone-home state (Pro, licensed servers only).</summary>
    public Task<PhoneHomeStatus> GetPhoneHomeStatusAsync(CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("license", "phone-home"), AgentOvenJsonContext.Default.PhoneHomeStatus, cancellationToken);

    /// <summary>
    /// Exchanges a username and password (or an API key) for a dashboard token (Pro). Create a new client with the
    /// token as <see cref="AgentOvenClientOptions.ApiKey"/>.
    /// </summary>
    public Task<LoginResult> LoginAsync(string username, string password, CancellationToken cancellationToken = default) =>
        _http.PostAsync("auth/login", new JsonObject { ["username"] = username, ["password"] = password },
            AgentOvenJsonContext.Default.JsonObject, AgentOvenJsonContext.Default.LoginResult, cancellationToken);

    /// <summary>Returns who the client's credentials authenticate as (Pro).</summary>
    public Task<AuthIdentity> GetIdentityAsync(CancellationToken cancellationToken = default) =>
        _http.GetAsync("auth/me", AgentOvenJsonContext.Default.AuthIdentity, cancellationToken);

    internal static JsonObject Body(params (string Key, JsonNode? Value)[] fields)
    {
        var body = new JsonObject();
        foreach (var (key, value) in fields)
        {
            if (value is not null)
            {
                body[key] = value;
            }
        }

        return body;
    }

    internal static JsonArray? Array(IEnumerable<string>? values) =>
        values is null ? null : new JsonArray([.. values.Select(v => (JsonNode)JsonValue.Create(v))]);
}

/// <summary>The Pro user directory.</summary>
public sealed class UsersClient
{
    private readonly AgentOvenHttp _http;

    internal UsersClient(AgentOvenHttp http) => _http = http;

    /// <summary>Lists users.</summary>
    public Task<IReadOnlyList<ProUser>> ListAsync(CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("users"), AgentOvenJsonContext.Default.IReadOnlyListProUser, cancellationToken);

    /// <summary>Gets a user.</summary>
    public Task<ProUser> GetAsync(string id, CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("users", id), AgentOvenJsonContext.Default.ProUser, cancellationToken);

    /// <summary>Creates a user. Roles: admin, chef, baker, taster, auditor, finance, viewer (default).</summary>
    public Task<ProUser> CreateAsync(string email, string? name = null, string? role = null, string? password = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        return _http.PostAsync(ApiPath.Of("users"),
            AgentOvenClient.Body(("email", email), ("name", name), ("role", role), ("password", password)),
            AgentOvenJsonContext.Default.JsonObject, AgentOvenJsonContext.Default.ProUser, cancellationToken);
    }

    /// <summary>Updates a user's name or role.</summary>
    public Task<ProUser> UpdateAsync(string id, string? name = null, string? role = null, CancellationToken cancellationToken = default) =>
        _http.PutAsync(ApiPath.Of("users", id), AgentOvenClient.Body(("name", name), ("role", role)),
            AgentOvenJsonContext.Default.JsonObject, AgentOvenJsonContext.Default.ProUser, cancellationToken);

    /// <summary>Deletes a user.</summary>
    public Task DeleteAsync(string id, CancellationToken cancellationToken = default) =>
        _http.DeleteAsync(ApiPath.Of("users", id), cancellationToken);

    /// <summary>Lists the kitchens a user belongs to.</summary>
    public Task<IReadOnlyList<KitchenMember>> ListKitchensAsync(string id, CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("users", id, "kitchens"), AgentOvenJsonContext.Default.IReadOnlyListKitchenMember, cancellationToken);
}

/// <summary>Workspace guardrails, applied to every agent in the kitchen on top of the agent's own.</summary>
public sealed class GuardrailsClient
{
    private readonly AgentOvenHttp _http;

    internal GuardrailsClient(AgentOvenHttp http) => _http = http;

    /// <summary>Lists guardrails.</summary>
    public Task<IReadOnlyList<WorkspaceGuardrail>> ListAsync(CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("guardrails"), AgentOvenJsonContext.Default.IReadOnlyListWorkspaceGuardrail, cancellationToken);

    /// <summary>Finds a guardrail by id or name (the server has no single-guardrail route).</summary>
    public async Task<WorkspaceGuardrail?> FindAsync(string idOrName, CancellationToken cancellationToken = default) =>
        (await ListAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault(g => g.Id == idOrName || g.Name == idOrName);

    /// <summary>Creates a guardrail.</summary>
    public Task<WorkspaceGuardrail> CreateAsync(WorkspaceGuardrail guardrail, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(guardrail);
        return _http.PostAsync(ApiPath.Of("guardrails"), guardrail, AgentOvenJsonContext.Default.WorkspaceGuardrail,
            AgentOvenJsonContext.Default.WorkspaceGuardrail, cancellationToken);
    }

    /// <summary>Updates a guardrail; unset properties are left alone.</summary>
    public Task<WorkspaceGuardrail> UpdateAsync(string id, WorkspaceGuardrail changes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(changes);
        return _http.PutAsync(ApiPath.Of("guardrails", id), changes, AgentOvenJsonContext.Default.WorkspaceGuardrail,
            AgentOvenJsonContext.Default.WorkspaceGuardrail, cancellationToken);
    }

    /// <summary>Flips a guardrail between enabled and disabled.</summary>
    public Task<WorkspaceGuardrail> ToggleAsync(string id, CancellationToken cancellationToken = default) =>
        _http.SendAsync(HttpMethod.Patch, ApiPath.Of("guardrails", id, "toggle"), content: null,
            AgentOvenJsonContext.Default.WorkspaceGuardrail, cancellationToken);

    /// <summary>Deletes a guardrail.</summary>
    public Task DeleteAsync(string id, CancellationToken cancellationToken = default) =>
        _http.DeleteAsync(ApiPath.Of("guardrails", id), cancellationToken);

    /// <summary>Lists the agents exempted from a guardrail.</summary>
    public Task<IReadOnlyList<GuardrailExemption>> ListExemptionsAsync(string id, CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("guardrails", id, "exceptions"), AgentOvenJsonContext.Default.IReadOnlyListGuardrailExemption, cancellationToken);

    /// <summary>Exempts an agent from a guardrail, optionally until a date.</summary>
    public Task<GuardrailExemption> AddExemptionAsync(
        string id, string agentName, string? reason = null, DateTimeOffset? expiresAt = null, CancellationToken cancellationToken = default) =>
        _http.PostAsync(ApiPath.Of("guardrails", id, "exceptions"),
            AgentOvenClient.Body(("agent_name", agentName), ("reason", reason),
                ("expires_at", expiresAt?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture))),
            AgentOvenJsonContext.Default.JsonObject, AgentOvenJsonContext.Default.GuardrailExemption, cancellationToken);

    /// <summary>Removes an exemption, by exception id.</summary>
    public Task RemoveExemptionAsync(string id, string exemptionId, CancellationToken cancellationToken = default) =>
        _http.DeleteAsync(ApiPath.Of("guardrails", id, "exceptions", exemptionId), cancellationToken);
}

/// <summary>Deployment environments, promotions between them, deployments, and per-environment agent bakes.</summary>
public sealed class EnvironmentsClient
{
    private static readonly System.Net.HttpStatusCode[] Accepted = [System.Net.HttpStatusCode.Accepted];
    private readonly AgentOvenHttp _http;

    internal EnvironmentsClient(AgentOvenHttp http) => _http = http;

    /// <summary>Lists environments.</summary>
    public Task<IReadOnlyList<DeploymentEnvironment>> ListAsync(CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("environments"), AgentOvenJsonContext.Default.IReadOnlyListDeploymentEnvironment, cancellationToken);

    /// <summary>Gets an environment by slug.</summary>
    public Task<DeploymentEnvironment> GetAsync(string slug, CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("environments", slug), AgentOvenJsonContext.Default.DeploymentEnvironment, cancellationToken);

    /// <summary>Creates an environment.</summary>
    public Task<DeploymentEnvironment> CreateAsync(DeploymentEnvironment environment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(environment);
        return _http.PostAsync(ApiPath.Of("environments"), environment, AgentOvenJsonContext.Default.DeploymentEnvironment,
            AgentOvenJsonContext.Default.DeploymentEnvironment, cancellationToken);
    }

    /// <summary>Updates an environment.</summary>
    public Task<DeploymentEnvironment> UpdateAsync(DeploymentEnvironment environment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(environment);
        return _http.PutAsync(ApiPath.Of("environments", environment.Slug), environment, AgentOvenJsonContext.Default.DeploymentEnvironment,
            AgentOvenJsonContext.Default.DeploymentEnvironment, cancellationToken);
    }

    /// <summary>Deletes an environment.</summary>
    public Task DeleteAsync(string slug, CancellationToken cancellationToken = default) =>
        _http.DeleteAsync(ApiPath.Of("environments", slug), cancellationToken);

    /// <summary>Lists the deployments in an environment.</summary>
    public Task<IReadOnlyList<AgentDeployment>> ListDeploymentsAsync(string slug, CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("environments", slug, "deployments"), AgentOvenJsonContext.Default.IReadOnlyListAgentDeployment, cancellationToken);

    /// <summary>Lists deployments across environments, optionally for one agent.</summary>
    public Task<IReadOnlyList<AgentDeployment>> ListAllDeploymentsAsync(string? agentName = null, CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.WithQuery(ApiPath.Of("deployments"), ("agent", agentName)), AgentOvenJsonContext.Default.IReadOnlyListAgentDeployment,
            cancellationToken);

    /// <summary>Gets a deployment.</summary>
    public Task<AgentDeployment> GetDeploymentAsync(string deploymentId, CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("deployments", deploymentId), AgentOvenJsonContext.Default.AgentDeployment, cancellationToken);

    /// <summary>
    /// Promotes an agent. The result's status is <c>promoted</c>, <c>pending_approval</c> (the target requires
    /// approval), <c>dry_run</c> or <c>failed</c>.
    /// </summary>
    public async Task<PromotionResult> PromoteAsync(PromotionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (_, body) = await _http.SendExpectingAsync(HttpMethod.Post, ApiPath.Of("promotions"),
            AgentOvenHttp.Json(request, AgentOvenJsonContext.Default.PromotionRequest), Accepted, cancellationToken).ConfigureAwait(false);
        return System.Text.Json.JsonSerializer.Deserialize(body, AgentOvenJsonContext.Default.PromotionResult) ?? new PromotionResult();
    }

    /// <summary>Rolls an agent back to its previous deployment in an environment.</summary>
    public Task<PromotionResult> RollbackAsync(string agentName, string environment, CancellationToken cancellationToken = default) =>
        _http.PostAsync(ApiPath.Of("promotions", "rollback"), AgentOvenClient.Body(("agent_name", agentName), ("environment", environment)),
            AgentOvenJsonContext.Default.JsonObject, AgentOvenJsonContext.Default.PromotionResult, cancellationToken);

    /// <summary>Lists an agent's per-environment bakes.</summary>
    public Task<IReadOnlyList<AgentEnvironment>> ListAgentEnvironmentsAsync(string agentName, CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("agents", agentName, "environments"), AgentOvenJsonContext.Default.IReadOnlyListAgentEnvironment, cancellationToken);

    /// <summary>Gets an agent's bake in one environment.</summary>
    public Task<AgentEnvironment> GetAgentEnvironmentAsync(string agentName, string slug, CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("agents", agentName, "environments", slug), AgentOvenJsonContext.Default.AgentEnvironment, cancellationToken);

    /// <summary>Bakes an agent into an environment with per-environment overrides (create or update).</summary>
    public Task<AgentEnvironment> BakeAgentAsync(
        string agentName, string slug, AgentEnvironmentRequest? settings = null, CancellationToken cancellationToken = default) =>
        _http.PutAsync(ApiPath.Of("agents", agentName, "environments", slug), settings ?? new AgentEnvironmentRequest(),
            AgentOvenJsonContext.Default.AgentEnvironmentRequest, AgentOvenJsonContext.Default.AgentEnvironment, cancellationToken);

    /// <summary>Cools and removes an agent's bake in an environment.</summary>
    public Task RemoveAgentAsync(string agentName, string slug, CancellationToken cancellationToken = default) =>
        _http.DeleteAsync(ApiPath.Of("agents", agentName, "environments", slug), cancellationToken);

    /// <summary>Which version of each agent runs in each environment.</summary>
    public Task<TraceabilityMatrix> GetTraceabilityMatrixAsync(CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.WithQuery(ApiPath.Of("traces", "matrix"), ("kitchen", _http.Kitchen)), AgentOvenJsonContext.Default.TraceabilityMatrix,
            cancellationToken);
}

/// <summary>Service accounts: machine identities for CI and services.</summary>
public sealed class ServiceAccountsClient
{
    private readonly AgentOvenHttp _http;

    internal ServiceAccountsClient(AgentOvenHttp http) => _http = http;

    /// <summary>Lists service accounts.</summary>
    public Task<IReadOnlyList<ServiceAccount>> ListAsync(CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("service-accounts"), AgentOvenJsonContext.Default.IReadOnlyListServiceAccount, cancellationToken);

    /// <summary>Gets a service account.</summary>
    public Task<ServiceAccount> GetAsync(string id, CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("service-accounts", id), AgentOvenJsonContext.Default.ServiceAccount, cancellationToken);

    /// <summary>Creates a service account. The token is returned only now.</summary>
    public Task<ServiceAccountToken> CreateAsync(
        string name, string role = "baker", string? description = null, IEnumerable<string>? scopes = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _http.PostAsync(ApiPath.Of("service-accounts"),
            AgentOvenClient.Body(("name", name), ("role", role), ("description", description), ("scopes", AgentOvenClient.Array(scopes))),
            AgentOvenJsonContext.Default.JsonObject, AgentOvenJsonContext.Default.ServiceAccountToken, cancellationToken);
    }

    /// <summary>Updates description, role or scopes.</summary>
    public Task<ServiceAccount> UpdateAsync(
        string id, string? description = null, string? role = null, IEnumerable<string>? scopes = null, CancellationToken cancellationToken = default) =>
        _http.PutAsync(ApiPath.Of("service-accounts", id),
            AgentOvenClient.Body(("description", description), ("role", role), ("scopes", AgentOvenClient.Array(scopes))),
            AgentOvenJsonContext.Default.JsonObject, AgentOvenJsonContext.Default.ServiceAccount, cancellationToken);

    /// <summary>Issues a new token; the old one stops working.</summary>
    public Task<ServiceAccountToken> RotateAsync(string id, CancellationToken cancellationToken = default) =>
        _http.PostAsync(ApiPath.Of("service-accounts", id, "rotate"), AgentOvenJsonContext.Default.ServiceAccountToken, cancellationToken);

    /// <summary>Revokes the account's token.</summary>
    public Task RevokeAsync(string id, CancellationToken cancellationToken = default) =>
        _http.SendAsync(HttpMethod.Post, ApiPath.Of("service-accounts", id, "revoke"), content: null, cancellationToken);

    /// <summary>Deletes a service account.</summary>
    public Task DeleteAsync(string id, CancellationToken cancellationToken = default) =>
        _http.DeleteAsync(ApiPath.Of("service-accounts", id), cancellationToken);
}

/// <summary>Cron schedules that run recipes. Times are UTC; the server has no time-zone setting.</summary>
public sealed class SchedulesClient
{
    private readonly AgentOvenHttp _http;

    internal SchedulesClient(AgentOvenHttp http) => _http = http;

    /// <summary>Lists schedules.</summary>
    public Task<IReadOnlyList<Schedule>> ListAsync(CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("schedules"), AgentOvenJsonContext.Default.IReadOnlyListSchedule, cancellationToken);

    /// <summary>Gets a schedule.</summary>
    public Task<Schedule> GetAsync(string id, CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("schedules", id), AgentOvenJsonContext.Default.Schedule, cancellationToken);

    /// <summary>Runs <paramref name="recipeName"/> on a cron expression, e.g. <c>0 8 * * MON-FRI</c>.</summary>
    public Task<Schedule> CreateAsync(string recipeName, string cron, bool enabled = true, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(cron);
        return _http.PostAsync(ApiPath.Of("schedules"), AgentOvenClient.Body(("recipe_name", recipeName), ("cron_expr", cron), ("enabled", enabled)),
            AgentOvenJsonContext.Default.JsonObject, AgentOvenJsonContext.Default.Schedule, cancellationToken);
    }

    /// <summary>Changes the cron expression or enables/disables the schedule.</summary>
    public Task<Schedule> UpdateAsync(string id, string? cron = null, bool? enabled = null, CancellationToken cancellationToken = default) =>
        _http.PutAsync(ApiPath.Of("schedules", id), AgentOvenClient.Body(("cron_expr", cron), ("enabled", enabled)),
            AgentOvenJsonContext.Default.JsonObject, AgentOvenJsonContext.Default.Schedule, cancellationToken);

    /// <summary>Runs the schedule's recipe now.</summary>
    public Task TriggerAsync(string id, CancellationToken cancellationToken = default) =>
        _http.SendAsync(HttpMethod.Post, ApiPath.Of("schedules", id, "trigger"), content: null, cancellationToken);

    /// <summary>Deletes a schedule.</summary>
    public Task DeleteAsync(string id, CancellationToken cancellationToken = default) =>
        _http.DeleteAsync(ApiPath.Of("schedules", id), cancellationToken);
}

/// <summary>Test suites for agents and their runs.</summary>
public sealed class TestSuitesClient
{
    private readonly AgentOvenHttp _http;

    internal TestSuitesClient(AgentOvenHttp http) => _http = http;

    /// <summary>Lists suites.</summary>
    public Task<IReadOnlyList<TestSuite>> ListAsync(CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("test-suites"), AgentOvenJsonContext.Default.IReadOnlyListTestSuite, cancellationToken);

    /// <summary>Gets a suite.</summary>
    public Task<TestSuite> GetAsync(string id, CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("test-suites", id), AgentOvenJsonContext.Default.TestSuite, cancellationToken);

    /// <summary>Creates a suite.</summary>
    public Task<TestSuite> CreateAsync(TestSuite suite, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(suite);
        return _http.PostAsync(ApiPath.Of("test-suites"), suite, AgentOvenJsonContext.Default.TestSuite, AgentOvenJsonContext.Default.TestSuite,
            cancellationToken);
    }

    /// <summary>Updates a suite.</summary>
    public Task<TestSuite> UpdateAsync(string id, TestSuite suite, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(suite);
        return _http.PutAsync(ApiPath.Of("test-suites", id), suite, AgentOvenJsonContext.Default.TestSuite, AgentOvenJsonContext.Default.TestSuite,
            cancellationToken);
    }

    /// <summary>Deletes a suite.</summary>
    public Task DeleteAsync(string id, CancellationToken cancellationToken = default) =>
        _http.DeleteAsync(ApiPath.Of("test-suites", id), cancellationToken);

    /// <summary>Starts a run. Returns the run id; poll with <see cref="WaitForRunAsync"/>.</summary>
    public async Task<string> RunAsync(string id, string trigger = "manual", CancellationToken cancellationToken = default)
    {
        var started = await _http.PostAsync(ApiPath.Of("test-suites", id, "run"), AgentOvenClient.Body(("trigger", trigger)),
            AgentOvenJsonContext.Default.JsonObject, AgentOvenJsonContext.Default.JsonObject, cancellationToken).ConfigureAwait(false);
        return started["run_id"]?.GetValue<string>() ?? throw new AgentOvenException("The server did not return a run id.");
    }

    /// <summary>Lists a suite's runs.</summary>
    public Task<IReadOnlyList<TestRun>> ListRunsAsync(string id, CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("test-suites", id, "runs"), AgentOvenJsonContext.Default.IReadOnlyListTestRun, cancellationToken);

    /// <summary>Gets a run.</summary>
    public Task<TestRun> GetRunAsync(string id, string runId, CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("test-suites", id, "runs", runId), AgentOvenJsonContext.Default.TestRun, cancellationToken);

    /// <summary>Polls a run until it completes or fails.</summary>
    public async Task<TestRun> WaitForRunAsync(string id, string runId, TimeSpan? pollInterval = null, CancellationToken cancellationToken = default)
    {
        while (true)
        {
            var run = await GetRunAsync(id, runId, cancellationToken).ConfigureAwait(false);
            if (run.IsFinished)
            {
                return run;
            }

            await Task.Delay(pollInterval ?? TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Cancels a run.</summary>
    public Task CancelRunAsync(string runId, CancellationToken cancellationToken = default) =>
        _http.SendAsync(HttpMethod.Post, ApiPath.Of("test-runs", runId, "cancel"), content: null, cancellationToken);
}

/// <summary>World schemas (ontologies) for scenario worlds. Schemas cannot be deleted.</summary>
public sealed class WorldSchemasClient
{
    private readonly AgentOvenHttp _http;

    internal WorldSchemasClient(AgentOvenHttp http) => _http = http;

    /// <summary>Lists schemas.</summary>
    public Task<IReadOnlyList<WorldSchema>> ListAsync(CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("world-schemas"), AgentOvenJsonContext.Default.IReadOnlyListWorldSchema, cancellationToken);

    /// <summary>Gets a schema.</summary>
    public Task<WorldSchema> GetAsync(string id, CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("world-schemas", id), AgentOvenJsonContext.Default.WorldSchema, cancellationToken);

    /// <summary>Creates a schema; returns it with any validation issues.</summary>
    public Task<WorldSchemaSaveResult> CreateAsync(WorldSchema schema, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(schema);
        return _http.PostAsync(ApiPath.Of("world-schemas"), schema, AgentOvenJsonContext.Default.WorldSchema,
            AgentOvenJsonContext.Default.WorldSchemaSaveResult, cancellationToken);
    }

    /// <summary>Saves a schema (even when invalid); returns it with any validation issues.</summary>
    public Task<WorldSchemaSaveResult> SaveAsync(WorldSchema schema, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(schema);
        return _http.PutAsync(ApiPath.Of("world-schemas", schema.Id), schema, AgentOvenJsonContext.Default.WorldSchema,
            AgentOvenJsonContext.Default.WorldSchemaSaveResult, cancellationToken);
    }

    /// <summary>Validates a schema without saving it.</summary>
    public async Task<IReadOnlyList<ProofIssue>> ValidateAsync(WorldSchema schema, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(schema);
        var result = await _http.PostAsync(ApiPath.Of("world-schemas", schema.Id, "validate"), schema, AgentOvenJsonContext.Default.WorldSchema,
            AgentOvenJsonContext.Default.WorldSchemaSaveResult, cancellationToken).ConfigureAwait(false);
        return result.Issues;
    }
}

/// <summary>Evaluation scenarios, scenario runs and their graded episodes.</summary>
public sealed class ScenariosClient
{
    private readonly AgentOvenHttp _http;

    internal ScenariosClient(AgentOvenHttp http) => _http = http;

    /// <summary>Lists scenarios (with their validation issues).</summary>
    public Task<IReadOnlyList<Scenario>> ListAsync(CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("scenarios"), AgentOvenJsonContext.Default.IReadOnlyListScenario, cancellationToken);

    /// <summary>Gets a scenario.</summary>
    public Task<Scenario> GetAsync(string id, CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("scenarios", id), AgentOvenJsonContext.Default.Scenario, cancellationToken);

    /// <summary>Creates a scenario.</summary>
    public Task<Scenario> CreateAsync(Scenario scenario, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        return _http.PostAsync(ApiPath.Of("scenarios"), scenario, AgentOvenJsonContext.Default.Scenario, AgentOvenJsonContext.Default.Scenario,
            cancellationToken);
    }

    /// <summary>Updates a scenario.</summary>
    public Task<Scenario> UpdateAsync(Scenario scenario, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        return _http.PutAsync(ApiPath.Of("scenarios", scenario.Id), scenario, AgentOvenJsonContext.Default.Scenario,
            AgentOvenJsonContext.Default.Scenario, cancellationToken);
    }

    /// <summary>Deletes a scenario.</summary>
    public Task DeleteAsync(string id, CancellationToken cancellationToken = default) =>
        _http.DeleteAsync(ApiPath.Of("scenarios", id), cancellationToken);

    /// <summary>Validates a scenario without saving it.</summary>
    public async Task<IReadOnlyList<ProofIssue>> ValidateAsync(Scenario scenario, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var result = await _http.PostAsync(ApiPath.Of("scenarios", scenario.Id, "validate"), scenario, AgentOvenJsonContext.Default.Scenario,
            AgentOvenJsonContext.Default.WorldSchemaSaveResult, cancellationToken).ConfigureAwait(false);
        return result.Issues;
    }

    /// <summary>Searches scenarios by text, agent or tools.</summary>
    public Task<IReadOnlyList<ScenarioMatch>> FindAsync(
        string? query = null, string? agent = null, IEnumerable<string>? tools = null, int? limit = null, CancellationToken cancellationToken = default)
    {
        var path = ApiPath.WithQuery(ApiPath.Of("scenarios", "find"), ("q", query), ("agent", agent),
            ("limit", limit?.ToString(CultureInfo.InvariantCulture)));
        foreach (var tool in tools ?? [])
        {
            path += (path.Contains('?', StringComparison.Ordinal) ? "&" : "?") + "tool=" + Uri.EscapeDataString(tool);
        }

        return _http.GetAsync(path, AgentOvenJsonContext.Default.IReadOnlyListScenarioMatch, cancellationToken);
    }

    /// <summary>Starts a run. Omit <see cref="ScenarioRunRequest.ScenarioIds"/> to let the agent's scenario ingredients decide.</summary>
    public Task<ScenarioRun> RunAsync(ScenarioRunRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _http.PostAsync(ApiPath.Of("scenario-runs"), request, AgentOvenJsonContext.Default.ScenarioRunRequest,
            AgentOvenJsonContext.Default.ScenarioRun, cancellationToken);
    }

    /// <summary>Lists runs.</summary>
    public Task<IReadOnlyList<ScenarioRun>> ListRunsAsync(CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("scenario-runs"), AgentOvenJsonContext.Default.IReadOnlyListScenarioRun, cancellationToken);

    /// <summary>Gets a run.</summary>
    public Task<ScenarioRun> GetRunAsync(string runId, CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("scenario-runs", runId), AgentOvenJsonContext.Default.ScenarioRun, cancellationToken);

    /// <summary>Polls a run until it finishes.</summary>
    public async Task<ScenarioRun> WaitForRunAsync(string runId, TimeSpan? pollInterval = null, CancellationToken cancellationToken = default)
    {
        while (true)
        {
            var run = await GetRunAsync(runId, cancellationToken).ConfigureAwait(false);
            if (run.IsFinished)
            {
                return run;
            }

            await Task.Delay(pollInterval ?? TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Lists a run's graded episodes, each with its <see cref="Verdict"/>.</summary>
    public Task<IReadOnlyList<EpisodeRecord>> ListEpisodesAsync(string runId, CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("scenario-runs", runId, "episodes"), AgentOvenJsonContext.Default.IReadOnlyListEpisodeRecord, cancellationToken);

    /// <summary>Cancels a run.</summary>
    public Task CancelRunAsync(string runId, CancellationToken cancellationToken = default) =>
        _http.SendAsync(HttpMethod.Post, ApiPath.Of("scenario-runs", runId, "cancel"), content: null, cancellationToken);

    /// <summary>Compares two runs of the same scenarios (paired deltas, significance, regressions).</summary>
    public Task<JsonObject> CompareRunsAsync(string baselineRunId, string candidateRunId, CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("scenario-runs", baselineRunId, "diff", candidateRunId), AgentOvenJsonContext.Default.JsonObject, cancellationToken);
}

/// <summary>Judges for rubric criteria (LLM or System One models).</summary>
public sealed class JudgesClient
{
    private readonly AgentOvenHttp _http;

    internal JudgesClient(AgentOvenHttp http) => _http = http;

    /// <summary>Lists judges.</summary>
    public Task<IReadOnlyList<Judge>> ListAsync(CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("judges"), AgentOvenJsonContext.Default.IReadOnlyListJudge, cancellationToken);

    /// <summary>Gets a judge.</summary>
    public Task<Judge> GetAsync(string name, CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("judges", name), AgentOvenJsonContext.Default.Judge, cancellationToken);

    /// <summary>Creates a judge.</summary>
    public Task<Judge> CreateAsync(Judge judge, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(judge);
        return _http.PostAsync(ApiPath.Of("judges"), judge, AgentOvenJsonContext.Default.Judge, AgentOvenJsonContext.Default.Judge, cancellationToken);
    }

    /// <summary>Updates a judge (it cannot be renamed).</summary>
    public Task<Judge> UpdateAsync(Judge judge, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(judge);
        return _http.PutAsync(ApiPath.Of("judges", judge.Name), judge, AgentOvenJsonContext.Default.Judge, AgentOvenJsonContext.Default.Judge,
            cancellationToken);
    }

    /// <summary>Deletes a judge.</summary>
    public Task DeleteAsync(string name, CancellationToken cancellationToken = default) =>
        _http.DeleteAsync(ApiPath.Of("judges", name), cancellationToken);

    /// <summary>Asks the judge to grade an artifact against an instruction, <paramref name="samples"/> times.</summary>
    public Task<JsonObject> TestAsync(
        string name, string instruction, string artifact, string? reference = null, string? scale = null, int? samples = null,
        CancellationToken cancellationToken = default) =>
        _http.PostAsync(ApiPath.Of("judges", name, "test"),
            AgentOvenClient.Body(("instruction", instruction), ("artifact", artifact), ("reference", reference), ("scale", scale), ("samples", samples)),
            AgentOvenJsonContext.Default.JsonObject, AgentOvenJsonContext.Default.JsonObject, cancellationToken);

    /// <summary>Lists the judge's recent judgments.</summary>
    public Task<JsonArray> ListJudgmentsAsync(string name, int limit = 50, CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.WithQuery(ApiPath.Of("judges", name, "judgments"), ("limit", limit.ToString(CultureInfo.InvariantCulture))),
            AgentOvenJsonContext.Default.JsonArray, cancellationToken);

    /// <summary>Records a human review of a judgment (feeds calibration).</summary>
    public Task ReviewAsync(string name, string judgmentId, double score, string? note = null, CancellationToken cancellationToken = default) =>
        _http.SendAsync(HttpMethod.Post, ApiPath.Of("judges", name, "reviews"),
            AgentOvenHttp.Json(AgentOvenClient.Body(("judgment_id", judgmentId), ("score", score), ("note", note)), AgentOvenJsonContext.Default.JsonObject),
            cancellationToken);
}

/// <summary>Kubernetes workload records (reconciled by the operator in-cluster).</summary>
public sealed class WorkloadsClient
{
    private readonly AgentOvenHttp _http;

    internal WorkloadsClient(AgentOvenHttp http) => _http = http;

    /// <summary>Lists workloads.</summary>
    public Task<IReadOnlyList<Workload>> ListAsync(CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("workloads"), AgentOvenJsonContext.Default.IReadOnlyListWorkload, cancellationToken);

    /// <summary>Gets a workload.</summary>
    public Task<Workload> GetAsync(string id, CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("workloads", id), AgentOvenJsonContext.Default.Workload, cancellationToken);

    /// <summary>Deletes a workload.</summary>
    public Task DeleteAsync(string id, CancellationToken cancellationToken = default) =>
        _http.DeleteAsync(ApiPath.Of("workloads", id), cancellationToken);
}

/// <summary>Kitchen credentials: secrets that tools and skills reference by name. Values are write-only.</summary>
public sealed class CredentialsClient
{
    private readonly AgentOvenHttp _http;

    internal CredentialsClient(AgentOvenHttp http) => _http = http;

    /// <summary>Lists credentials (names only).</summary>
    public Task<IReadOnlyList<Credential>> ListAsync(CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("credentials"), AgentOvenJsonContext.Default.IReadOnlyListCredential, cancellationToken);

    /// <summary>Stores a credential.</summary>
    public Task<Credential> CreateAsync(string name, string value, string? label = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrEmpty(value);
        return _http.PostAsync(ApiPath.Of("credentials"), AgentOvenClient.Body(("name", name), ("value", value), ("label", label)),
            AgentOvenJsonContext.Default.JsonObject, AgentOvenJsonContext.Default.Credential, cancellationToken);
    }

    /// <summary>Replaces a credential's value.</summary>
    public Task<Credential> RotateAsync(string name, string value, string? label = null, CancellationToken cancellationToken = default) =>
        _http.PutAsync(ApiPath.Of("credentials", name), AgentOvenClient.Body(("value", value), ("label", label)),
            AgentOvenJsonContext.Default.JsonObject, AgentOvenJsonContext.Default.Credential, cancellationToken);

    /// <summary>Deletes a credential.</summary>
    public Task DeleteAsync(string name, CancellationToken cancellationToken = default) =>
        _http.DeleteAsync(ApiPath.Of("credentials", name), cancellationToken);
}

/// <summary>Kitchen membership and per-kitchen settings (Pro).</summary>
public static class KitchenMembersExtensions
{
    /// <summary>Lists a kitchen's members (Pro).</summary>
    public static Task<IReadOnlyList<KitchenMember>> ListMembersAsync(this KitchensClient kitchens, string kitchenId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(kitchens);
        return kitchens.Http.GetAsync(ApiPath.Of("kitchens", kitchenId, "members"), AgentOvenJsonContext.Default.IReadOnlyListKitchenMember, cancellationToken);
    }

    /// <summary>Adds a user to a kitchen with a role (Pro).</summary>
    public static Task AddMemberAsync(this KitchensClient kitchens, string kitchenId, string userId, string role, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(kitchens);
        return kitchens.Http.SendAsync(HttpMethod.Post, ApiPath.Of("kitchens", kitchenId, "members"),
            AgentOvenHttp.Json(AgentOvenClient.Body(("user_id", userId), ("role", role)), AgentOvenJsonContext.Default.JsonObject), cancellationToken);
    }

    /// <summary>Changes a member's role (Pro).</summary>
    public static Task UpdateMemberAsync(this KitchensClient kitchens, string kitchenId, string userId, string role, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(kitchens);
        return kitchens.Http.SendAsync(HttpMethod.Put, ApiPath.Of("kitchens", kitchenId, "members", userId),
            AgentOvenHttp.Json(AgentOvenClient.Body(("role", role)), AgentOvenJsonContext.Default.JsonObject), cancellationToken);
    }

    /// <summary>Removes a member (Pro).</summary>
    public static Task RemoveMemberAsync(this KitchensClient kitchens, string kitchenId, string userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(kitchens);
        return kitchens.Http.DeleteAsync(ApiPath.Of("kitchens", kitchenId, "members", userId), cancellationToken);
    }

    /// <summary>Gets a kitchen's settings by id or name (Pro).</summary>
    public static Task<KitchenSettings> GetSettingsAsync(this KitchensClient kitchens, string kitchen, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(kitchens);
        return kitchens.Http.GetAsync(ApiPath.Of("kitchens", kitchen, "settings"), AgentOvenJsonContext.Default.KitchenSettings, cancellationToken);
    }
}

/// <summary>Agent import from an <c>agents.md</c> file (Pro).</summary>
public static class AgentImportExtensions
{
    /// <summary>Creates agents, providers and tools described in an <c>agents.md</c> document.</summary>
    public static async Task<AgentImportResult> ImportAsync(
        this AgentsClient agents, string agentsMarkdown, bool dryRun = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agents);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentsMarkdown);
        var content = new StringContent(agentsMarkdown, System.Text.Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("text/markdown");
        return await agents.Http.SendAsync(HttpMethod.Post,
            ApiPath.WithQuery(ApiPath.Of("agents", "import"), ("dry_run", dryRun ? "true" : null)), content,
            AgentOvenJsonContext.Default.AgentImportResult, cancellationToken).ConfigureAwait(false);
    }
}
