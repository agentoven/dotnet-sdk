using System.Net;
using System.Text.Json;
using AgentOven.Internal;

namespace AgentOven;

/// <summary>The kitchen's MCP tool catalog. Get one from <see cref="AgentOvenClient.Tools"/>.</summary>
public sealed class ToolsClient
{
    private readonly AgentOvenHttp _http;

    internal ToolsClient(AgentOvenHttp http) => _http = http;

    /// <summary>Lists tools. Auth config comes back redacted.</summary>
    public Task<IReadOnlyList<McpTool>> ListAsync(CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("tools"), AgentOvenJsonContext.Default.IReadOnlyListMcpTool, cancellationToken);

    /// <summary>Gets a tool.</summary>
    public Task<McpTool> GetAsync(string name, CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("tools", name), AgentOvenJsonContext.Default.McpTool, cancellationToken);

    /// <summary>Registers a tool (transports <c>http</c>, <c>sse</c> or <c>stdio</c>).</summary>
    public Task<McpTool> AddAsync(McpTool tool, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tool);
        return _http.PostAsync(ApiPath.Of("tools"), tool, AgentOvenJsonContext.Default.McpTool, AgentOvenJsonContext.Default.McpTool, cancellationToken);
    }

    /// <summary>Registers several tools at once.</summary>
    public Task<BulkToolResult> AddRangeAsync(IReadOnlyList<McpTool> tools, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tools);
        return _http.PostAsync(ApiPath.Of("tools", "bulk"), tools, AgentOvenJsonContext.Default.IReadOnlyListMcpTool,
            AgentOvenJsonContext.Default.BulkToolResult, cancellationToken);
    }

    /// <summary>Updates a tool.</summary>
    public Task<McpTool> UpdateAsync(McpTool tool, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tool);
        return _http.PutAsync(ApiPath.Of("tools", tool.Name), tool, AgentOvenJsonContext.Default.McpTool, AgentOvenJsonContext.Default.McpTool, cancellationToken);
    }

    /// <summary>Deletes a tool.</summary>
    public Task DeleteAsync(string name, CancellationToken cancellationToken = default) =>
        _http.DeleteAsync(ApiPath.Of("tools", name), cancellationToken);
}

/// <summary>The kitchen's versioned prompt store. Get one from <see cref="AgentOvenClient.Prompts"/>.</summary>
public sealed class PromptsClient
{
    private readonly AgentOvenHttp _http;

    internal PromptsClient(AgentOvenHttp http) => _http = http;

    /// <summary>Lists prompts (latest versions).</summary>
    public Task<IReadOnlyList<Prompt>> ListAsync(CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("prompts"), AgentOvenJsonContext.Default.IReadOnlyListPrompt, cancellationToken);

    /// <summary>Gets the latest version of a prompt.</summary>
    public Task<Prompt> GetAsync(string name, CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("prompts", name), AgentOvenJsonContext.Default.Prompt, cancellationToken);

    /// <summary>Gets one version of a prompt.</summary>
    public Task<Prompt> GetVersionAsync(string name, int version, CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("prompts", name, "versions", version.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            AgentOvenJsonContext.Default.Prompt, cancellationToken);

    /// <summary>Lists every version of a prompt.</summary>
    public Task<IReadOnlyList<Prompt>> ListVersionsAsync(string name, CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("prompts", name, "versions"), AgentOvenJsonContext.Default.IReadOnlyListPrompt, cancellationToken);

    /// <summary>Adds a prompt. Reference it from an agent with <see cref="Ingredient.PromptRef"/>.</summary>
    public Task<Prompt> AddAsync(Prompt prompt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        return _http.PostAsync(ApiPath.Of("prompts"), prompt, AgentOvenJsonContext.Default.Prompt, AgentOvenJsonContext.Default.Prompt, cancellationToken);
    }

    /// <summary>Adds a prompt from its parts.</summary>
    public Task<Prompt> AddAsync(string name, string template, IEnumerable<string>? tags = null, CancellationToken cancellationToken = default) =>
        AddAsync(new Prompt(name, template) { Tags = tags?.ToList() }, cancellationToken);

    /// <summary>Saves a new version of a prompt.</summary>
    public Task<Prompt> UpdateAsync(Prompt prompt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        return _http.PutAsync(ApiPath.Of("prompts", prompt.Name), prompt, AgentOvenJsonContext.Default.Prompt, AgentOvenJsonContext.Default.Prompt, cancellationToken);
    }

    /// <summary>Deletes a prompt and its versions.</summary>
    public Task DeleteAsync(string name, CancellationToken cancellationToken = default) =>
        _http.DeleteAsync(ApiPath.Of("prompts", name), cancellationToken);

    /// <summary>Runs the security and quality review on the latest version.</summary>
    public async Task<PromptValidationReport> ValidateAsync(string name, CancellationToken cancellationToken = default)
    {
        var envelope = await _http.PostAsync(ApiPath.Of("prompts", name, "validate"), InternalJsonContext.Default.PromptValidationEnvelope, cancellationToken)
            .ConfigureAwait(false);
        return envelope.Report ?? throw new AgentOvenException($"The server returned no validation report for '{name}'.");
    }
}

/// <summary>
/// The kitchen's skill registry. A skill must pass a provider-based intent review before agents can use it; a
/// registration can come back accepted, waiting for human review, or rejected. Get one from <see cref="AgentOvenClient.Skills"/>.
/// </summary>
public sealed class SkillsClient
{
    private const int UploadChunkSize = 1024 * 1024;

    private static readonly HttpStatusCode[] ReviewStatuses = [HttpStatusCode.Accepted, HttpStatusCode.UnprocessableEntity];
    private readonly AgentOvenHttp _http;

    internal SkillsClient(AgentOvenHttp http) => _http = http;

    /// <summary>Lists skills.</summary>
    public async Task<IReadOnlyList<Skill>> ListAsync(CancellationToken cancellationToken = default) =>
        (await _http.GetAsync(ApiPath.Of("skills"), InternalJsonContext.Default.SkillsEnvelope, cancellationToken).ConfigureAwait(false)).Skills ?? [];

    /// <summary>Gets a skill.</summary>
    public Task<Skill> GetAsync(string name, CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("skills", name), AgentOvenJsonContext.Default.Skill, cancellationToken);

    /// <summary>Deletes a skill and the tools it registered.</summary>
    public Task DeleteAsync(string name, CancellationToken cancellationToken = default) =>
        _http.DeleteAsync(ApiPath.Of("skills", name), cancellationToken);

    /// <summary>
    /// Registers a skill from its <c>SKILL.md</c> text and optional extra files (relative path → content).
    /// </summary>
    public Task<SkillRegistration> RegisterAsync(
        string skillMarkdown,
        IReadOnlyDictionary<string, byte[]>? files = null,
        IReadOnlyDictionary<string, string>? credentials = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillMarkdown);
        return RegisterCoreAsync(new SkillRegisterRequest("inline")
        {
            SkillMd = skillMarkdown,
            Files = files?.ToDictionary(f => f.Key, f => Convert.ToBase64String(f.Value)),
            Credentials = credentials,
        }, cancellationToken);
    }

    /// <summary>Registers a skill from a directory on the control plane's host.</summary>
    public Task<SkillRegistration> RegisterFromPathAsync(
        string path, IReadOnlyDictionary<string, string>? credentials = null, CancellationToken cancellationToken = default) =>
        RegisterCoreAsync(new SkillRegisterRequest("path") { Path = path, Credentials = credentials }, cancellationToken);

    /// <summary>Registers a skill from a git repository (https).</summary>
    public Task<SkillRegistration> RegisterFromGitAsync(
        Uri gitUrl, string? gitRef = null, string? path = null, IReadOnlyDictionary<string, string>? credentials = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gitUrl);
        return RegisterCoreAsync(
            new SkillRegisterRequest("git") { GitUrl = gitUrl.ToString(), GitRef = gitRef, Path = path, Credentials = credentials },
            cancellationToken);
    }

    /// <summary>
    /// Uploads a skill bundle (a ZIP with <c>SKILL.md</c> at its root; up to 8 MiB, 256 files) in chunks, then registers it.
    /// </summary>
    public async Task<SkillRegistration> UploadAsync(
        ReadOnlyMemory<byte> zip, IReadOnlyDictionary<string, string>? credentials = null, CancellationToken cancellationToken = default)
    {
        if (zip.IsEmpty)
        {
            throw new ArgumentException("The bundle is empty.", nameof(zip));
        }

        var begun = await _http.PostAsync(
            ApiPath.Of("skills", "upload", "begin"), new UploadBegin(zip.Length), InternalJsonContext.Default.UploadBegin,
            InternalJsonContext.Default.UploadState, cancellationToken).ConfigureAwait(false);
        var uploadId = begun.UploadId ?? throw new AgentOvenException("The server did not return an upload id.");

        for (var offset = 0; offset < zip.Length; offset += UploadChunkSize)
        {
            var chunk = zip.Slice(offset, Math.Min(UploadChunkSize, zip.Length - offset));
            await _http.SendAsync(HttpMethod.Post, ApiPath.WithQuery(ApiPath.Of("skills", "upload", "chunk"), ("upload_id", uploadId)),
                AgentOvenHttp.Bytes(chunk), InternalJsonContext.Default.UploadState, cancellationToken).ConfigureAwait(false);
        }

        await _http.SendAsync(HttpMethod.Post, ApiPath.WithQuery(ApiPath.Of("skills", "upload", "commit"), ("upload_id", uploadId)),
            content: null, InternalJsonContext.Default.UploadState, cancellationToken).ConfigureAwait(false);

        return await RegisterCoreAsync(new SkillRegisterRequest("upload") { UploadId = uploadId, Credentials = credentials }, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Uploads a skill bundle from a ZIP file.</summary>
    public async Task<SkillRegistration> UploadFileAsync(
        string zipPath, IReadOnlyDictionary<string, string>? credentials = null, CancellationToken cancellationToken = default) =>
        await UploadAsync(await File.ReadAllBytesAsync(zipPath, cancellationToken).ConfigureAwait(false), credentials, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>Imports skills and MCP servers from a plugin repository. Set <see cref="SkillImportRequest.DryRun"/> to plan only.</summary>
    public Task<SkillImportResult> ImportAsync(SkillImportRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _http.PostAsync(ApiPath.Of("skills", "import"), request, AgentOvenJsonContext.Default.SkillImportRequest,
            AgentOvenJsonContext.Default.SkillImportResult, cancellationToken);
    }

    /// <summary>Re-fetches a git or path skill and reviews it again.</summary>
    public async Task<SkillRegistration> RefreshAsync(string name, CancellationToken cancellationToken = default)
    {
        var (status, body) = await _http.SendExpectingAsync(HttpMethod.Patch, ApiPath.Of("skills", name, "refresh"), content: null,
            ReviewStatuses, cancellationToken).ConfigureAwait(false);
        return ToRegistration(status, body);
    }

    /// <summary>Approves a skill waiting for review.</summary>
    public Task<Skill> ApproveAsync(string name, string? note = null, CancellationToken cancellationToken = default) =>
        DecideAsync(name, "approve", note, cancellationToken);

    /// <summary>Rejects a skill waiting for review.</summary>
    public Task<Skill> RejectAsync(string name, string? note = null, CancellationToken cancellationToken = default) =>
        DecideAsync(name, "reject", note, cancellationToken);

    private Task<Skill> DecideAsync(string name, string decision, string? note, CancellationToken cancellationToken) =>
        _http.PostAsync(ApiPath.Of("skills", name, decision), new SkillDecision { Note = note }, InternalJsonContext.Default.SkillDecision,
            AgentOvenJsonContext.Default.Skill, cancellationToken);

    private async Task<SkillRegistration> RegisterCoreAsync(SkillRegisterRequest request, CancellationToken cancellationToken)
    {
        var (status, body) = await _http.SendExpectingAsync(
            HttpMethod.Post, ApiPath.Of("skills", "register"), AgentOvenHttp.Json(request, InternalJsonContext.Default.SkillRegisterRequest),
            ReviewStatuses, cancellationToken).ConfigureAwait(false);
        return ToRegistration(status, body);
    }

    // 201/200 → the accepted Skill; 202 → {status:"needs_review", reasoning, note}; 422 → {status:"rejected", reasoning}.
    internal static SkillRegistration ToRegistration(HttpStatusCode status, byte[] body)
    {
        if (status is HttpStatusCode.Accepted or HttpStatusCode.UnprocessableEntity)
        {
            var verdict = JsonSerializer.Deserialize(body, AgentOvenJsonContext.Default.SkillRegistration) ?? new SkillRegistration();
            return verdict with { Status = verdict.Status.Length > 0 ? verdict.Status : status == HttpStatusCode.Accepted ? "needs_review" : "rejected" };
        }

        var skill = JsonSerializer.Deserialize(body, AgentOvenJsonContext.Default.Skill);
        return new SkillRegistration
        {
            Status = skill?.Status is { Length: > 0 } s ? s : "accepted",
            Skill = skill,
            Reasoning = skill?.VerificationReasoning,
        };
    }
}

/// <summary>The kitchen's knowledge base (RAG). Get one from <see cref="AgentOvenClient.Rag"/>.</summary>
public sealed class RagClient
{
    private readonly AgentOvenHttp _http;

    internal RagClient(AgentOvenHttp http) => _http = http;

    /// <summary>Answers a question from the ingested documents.</summary>
    public Task<RagAnswer> QueryAsync(RagQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return _http.PostAsync(ApiPath.Of("rag", "query"), query with { Kitchen = query.Kitchen ?? _http.Kitchen },
            AgentOvenJsonContext.Default.RagQuery, AgentOvenJsonContext.Default.RagAnswer, cancellationToken);
    }

    /// <summary>Answers a question.</summary>
    public Task<RagAnswer> QueryAsync(string question, int? topK = null, string? @namespace = null, CancellationToken cancellationToken = default) =>
        QueryAsync(new RagQuery(question) { TopK = topK, Namespace = @namespace }, cancellationToken);

    /// <summary>Chunks, embeds and stores documents.</summary>
    public Task<RagIngestResult> IngestAsync(IReadOnlyList<RagDocument> documents, RagIngestOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documents);
        var request = new RagIngestRequest(_http.Kitchen, documents)
        {
            ChunkSize = options?.ChunkSize,
            ChunkOverlap = options?.ChunkOverlap,
            EmbeddingProvider = options?.EmbeddingProvider,
            Namespace = options?.Namespace,
        };
        return _http.PostAsync(ApiPath.Of("rag", "ingest"), request, InternalJsonContext.Default.RagIngestRequest,
            AgentOvenJsonContext.Default.RagIngestResult, cancellationToken);
    }
}

/// <summary>Recorded invocations and their spans. Get one from <see cref="AgentOvenClient.Traces"/>.</summary>
public sealed class TracesClient
{
    private readonly AgentOvenHttp _http;

    internal TracesClient(AgentOvenHttp http) => _http = http;

    /// <summary>Lists traces, newest first.</summary>
    public Task<IReadOnlyList<Trace>> ListAsync(TraceQuery? query = null, CancellationToken cancellationToken = default)
    {
        query ??= new TraceQuery();
        return _http.GetAsync(
            ApiPath.WithQuery(ApiPath.Of("traces"),
                ("limit", query.Limit.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                ("agent", query.Agent), ("recipe", query.Recipe), ("status", query.Status)),
            AgentOvenJsonContext.Default.IReadOnlyListTrace, cancellationToken);
    }

    /// <summary>Gets a trace.</summary>
    public Task<Trace> GetAsync(string traceId, CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("traces", traceId), AgentOvenJsonContext.Default.Trace, cancellationToken);

    /// <summary>Gets a trace's spans.</summary>
    public Task<IReadOnlyList<Span>> GetSpansAsync(string traceId, CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("traces", traceId, "spans"), AgentOvenJsonContext.Default.IReadOnlyListSpan, cancellationToken);
}

/// <summary>The audit log. Get one from <see cref="AgentOvenClient.Audit"/>.</summary>
public sealed class AuditClient
{
    private readonly AgentOvenHttp _http;

    internal AuditClient(AgentOvenHttp http) => _http = http;

    /// <summary>Lists audit events, newest first.</summary>
    public Task<IReadOnlyList<AuditEvent>> ListAsync(AuditQuery? query = null, CancellationToken cancellationToken = default) =>
        _http.GetAsync(Path("audit", query ?? new AuditQuery()), AgentOvenJsonContext.Default.IReadOnlyListAuditEvent, cancellationToken);

    /// <summary>Counts audit events matching the filters.</summary>
    public async Task<long> CountAsync(AuditQuery? query = null, CancellationToken cancellationToken = default) =>
        (await _http.GetAsync(Path("audit/count", query ?? new AuditQuery()), InternalJsonContext.Default.CountEnvelope, cancellationToken)
            .ConfigureAwait(false)).Count;

    private static string Path(string relative, AuditQuery q) => ApiPath.WithQuery(
        "api/v1/" + relative,
        ("limit", q.Limit.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        ("offset", q.Offset?.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        ("action", q.Action), ("agent", q.Agent), ("environment", q.Environment), ("user_id", q.UserId), ("resource", q.Resource));
}

/// <summary>Scoped API keys: keys limited to a kitchen, some agents and a quota. Get one from <see cref="AgentOvenClient.Keys"/>.</summary>
public sealed class KeysClient
{
    private readonly AgentOvenHttp _http;

    internal KeysClient(AgentOvenHttp http) => _http = http;

    /// <summary>Lists keys (without their secrets).</summary>
    public Task<IReadOnlyList<ScopedKey>> ListAsync(CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("keys"), AgentOvenJsonContext.Default.IReadOnlyListScopedKey, cancellationToken);

    /// <summary>Gets a key.</summary>
    public Task<ScopedKey> GetAsync(string id, CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("keys", id), AgentOvenJsonContext.Default.ScopedKey, cancellationToken);

    /// <summary>
    /// Creates a key. <see cref="ScopedKey.Key"/> is returned only now. Use it with
    /// <see cref="AgentOvenClientOptions.AgentKey"/>.
    /// </summary>
    /// <param name="label">Label.</param>
    /// <param name="agentNames">Agents it may invoke; <c>*</c> for all.</param>
    /// <param name="maxCalls">Call quota; 0 for unlimited.</param>
    /// <param name="expiresIn">Lifetime, e.g. <c>24h</c> or <c>7d</c>; empty for never.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public Task<ScopedKey> CreateAsync(
        string label, IEnumerable<string> agentNames, int maxCalls = 0, string expiresIn = "", CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(agentNames);
        return _http.PostAsync(ApiPath.Of("keys"), new ScopedKeyCreateRequest(label, agentNames.ToList()) { MaxCalls = maxCalls, ExpiresIn = expiresIn },
            InternalJsonContext.Default.ScopedKeyCreateRequest, AgentOvenJsonContext.Default.ScopedKey, cancellationToken);
    }

    /// <summary>Gets a key's usage.</summary>
    public Task<ScopedKeyUsage> GetUsageAsync(string id, CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("keys", id, "usage"), AgentOvenJsonContext.Default.ScopedKeyUsage, cancellationToken);

    /// <summary>Revokes a key; it stops working immediately.</summary>
    public Task RevokeAsync(string id, CancellationToken cancellationToken = default) =>
        _http.SendAsync(HttpMethod.Post, ApiPath.Of("keys", id, "revoke"), content: null, cancellationToken);

    /// <summary>Deletes a key.</summary>
    public Task DeleteAsync(string id, CancellationToken cancellationToken = default) =>
        _http.DeleteAsync(ApiPath.Of("keys", id), cancellationToken);
}

/// <summary>Approval requests for recipes' human gates. Get one from <see cref="AgentOvenClient.Approvals"/>.</summary>
public sealed class ApprovalsClient
{
    private readonly AgentOvenHttp _http;

    internal ApprovalsClient(AgentOvenHttp http) => _http = http;

    /// <summary>Lists approvals, by default those still pending.</summary>
    public Task<IReadOnlyList<ApprovalRecord>> ListAsync(string? status = "pending", CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.WithQuery(ApiPath.Of("approvals"), ("status", status)), AgentOvenJsonContext.Default.IReadOnlyListApprovalRecord,
            cancellationToken);

    /// <summary>Gets an approval by its gate key (<c>runId:stepName</c>).</summary>
    public Task<ApprovalRecord> GetAsync(string gateKey, CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("approvals", gateKey), AgentOvenJsonContext.Default.ApprovalRecord, cancellationToken);

    /// <summary>Approves or rejects a gate, with an audit trail (approver, channel, comments).</summary>
    public Task DecideAsync(string runId, string stepName, GateApproval decision, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(decision);
        return _http.SendAsync(HttpMethod.Post, ApiPath.Of("approvals", runId, stepName),
            AgentOvenHttp.Json(decision, AgentOvenJsonContext.Default.GateApproval), cancellationToken);
    }
}
