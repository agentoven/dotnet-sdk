using System.Net;
using System.Text.Json.Nodes;

namespace AgentOven.Tests;

public class ParityTests
{
    private static (AgentOvenClient Client, StubHttpHandler Stub) Create(string? apiKey = "key-1", string? agentKey = null)
    {
        var stub = new StubHttpHandler();
        var client = new AgentOvenClient(
            new AgentOvenClientOptions { Url = new Uri("http://oven.test"), ApiKey = apiKey, AgentKey = agentKey, Kitchen = "payments" },
            new HttpClient(stub));
        return (client, stub);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Scoped_keys_are_sent_as_X_Agent_Key()
    {
        var (viaApiKey, stub1) = Create(apiKey: "ao_sk_abc123");
        var (viaOption, stub2) = Create(apiKey: null, agentKey: "ao_sk_xyz");
        stub1.RespondJson("[]");
        stub2.RespondJson("[]");

        await viaApiKey.Agents.ListAsync(cancellationToken: Ct);
        await viaOption.Agents.ListAsync(cancellationToken: Ct);

        Assert.Equal("ao_sk_abc123", stub1.Last.Headers["X-Agent-Key"]);
        Assert.False(stub1.Last.Headers.ContainsKey("Authorization"));
        Assert.Equal("ao_sk_xyz", stub2.Last.Headers["X-Agent-Key"]);
    }

    [Fact]
    public async Task Null_lists_become_empty()
    {
        var (client, stub) = Create();
        stub.RespondJson("null");

        Assert.Empty(await client.Keys.ListAsync(Ct));
    }

    [Fact]
    public async Task Invoke_sends_session_and_voice_fields()
    {
        var (client, stub) = Create();
        stub.RespondJson("""{"agent":"a","response":"hi","session_id":"s-1","transcript":"hello","audio":{"data":"AAEC","mime_type":"audio/mpeg"}}""");

        var result = await client.Agents.InvokeAsync("a", "", new InvokeOptions
        {
            SessionId = "s-1",
            Audio = AudioClip.FromBytes([0x52, 0x49, 0x46, 0x46], "audio/wav"),
            VoiceOutput = true,
            Voice = "nova",
        }, Ct);

        var body = stub.Last.Json;
        Assert.False(body.TryGetProperty("message", out _));
        Assert.Equal("s-1", body.GetProperty("session_id").GetString());
        Assert.Equal("audio/wav", body.GetProperty("audio").GetProperty("mime_type").GetString());
        Assert.True(body.GetProperty("voice_output").GetBoolean());
        Assert.Equal("nova", body.GetProperty("voice").GetString());
        Assert.Equal("hello", result.Transcript);
        Assert.Equal([0, 1, 2], result.Audio!.ToBytes());
    }

    [Fact]
    public async Task Resume_sends_a_decision_or_no_body()
    {
        var (client, stub) = Create();
        stub.RespondJson("""{"agent":"a","trace_id":"t-1","status":"paused","pending":{"turn":2,"tool_calls":[],"gated_names":["refund"]}}""");
        stub.RespondJson("""{"agent":"a","trace_id":"t-1","status":"completed","response":"done"}""");
        stub.RespondJson("""{"runs":[{"trace_id":"t-1","agent_name":"a","status":"paused","turns_logged":2}]}""");

        var paused = await client.Agents["a"].ResumeAsync("t-1", approved: false, note: "too large", Ct);
        var completed = await client.Agents.ResumeAsync("a", "t-1", approved: null, cancellationToken: Ct);
        var unfinished = await client.Agents.ListUnfinishedRunsAsync(Ct);

        Assert.Equal("/api/v1/agents/a/runs/t-1/resume", stub.Requests[0].Uri.AbsolutePath);
        Assert.Equal("""{"approved":false,"note":"too large"}""", stub.Requests[0].Body);
        Assert.True(paused.IsPaused);
        Assert.Equal(["refund"], paused.Pending!.GatedNames);
        Assert.Null(stub.Requests[1].Body);
        Assert.Equal("done", completed.Response);
        Assert.Equal("/api/v1/agents/runs/unfinished", stub.Requests[2].Uri.AbsolutePath);
        Assert.Equal("t-1", Assert.Single(unfinished).TraceId);
    }

    [Fact]
    public async Task Session_attachments_go_out_as_content_parts()
    {
        var (client, stub) = Create();
        stub.RespondJson("""{"session_id":"s","turn_number":1,"content":"a cat","status":"active"}""");
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A];

        await client.Agents.Sessions.SendAsync("vision", "s", "What is this?",
            [Attachment.FromBytes(png), Attachment.FromUrl(new Uri("https://x.test/a.pdf"), "application/pdf")], cancellationToken: Ct);

        var parts = stub.Last.Json.GetProperty("content_parts");
        Assert.Equal("image", parts[0].GetProperty("type").GetString());
        Assert.Equal("image/png", parts[0].GetProperty("media").GetProperty("mime_type").GetString());
        Assert.Equal(Convert.ToBase64String(png), parts[0].GetProperty("media").GetProperty("data").GetString());
        Assert.Equal("file", parts[1].GetProperty("type").GetString());
        Assert.Equal("https://x.test/a.pdf", parts[1].GetProperty("media").GetProperty("url").GetString());
    }

    [Fact]
    public async Task Skill_registration_maps_review_outcomes()
    {
        var (client, stub) = Create();
        stub.Respond(HttpStatusCode.Created, """{"id":"1","name":"refunds","status":"accepted","verification_reasoning":"ok"}""");
        stub.Respond(HttpStatusCode.Accepted, """{"status":"needs_review","reasoning":"calls a payments API","note":"approve to activate"}""");
        stub.Respond(HttpStatusCode.UnprocessableEntity, """{"status":"rejected","reasoning":"exfiltrates data"}""");
        const string md = "---\nname: refunds\ndescription: x\n---\nDo refunds.";

        var accepted = await client.Skills.RegisterAsync(md, cancellationToken: Ct);
        var review = await client.Skills.RegisterAsync(md, cancellationToken: Ct);
        var rejected = await client.Skills.RegisterAsync(md, cancellationToken: Ct);

        Assert.True(accepted.Accepted);
        Assert.Equal("refunds", accepted.Skill!.Name);
        Assert.True(review.NeedsReview);
        Assert.Equal("calls a payments API", review.Reasoning);
        Assert.True(rejected.Rejected);
        Assert.Equal("inline", stub.Requests[0].Json.GetProperty("source").GetString());
    }

    [Fact]
    public async Task Skill_upload_uses_begin_chunk_commit_register()
    {
        var (client, stub) = Create();
        stub.RespondJson("""{"upload_id":"u-1"}""").RespondJson("""{"received":3}""").RespondJson("""{"upload_id":"u-1","status":"committed"}""");
        stub.Respond(HttpStatusCode.Created, """{"name":"zip-skill","status":"accepted"}""");

        var result = await client.Skills.UploadAsync(new byte[] { 1, 2, 3 }, cancellationToken: Ct);

        Assert.Equal(["/api/v1/skills/upload/begin", "/api/v1/skills/upload/chunk", "/api/v1/skills/upload/commit", "/api/v1/skills/register"],
            stub.Requests.Select(r => r.Uri.AbsolutePath));
        Assert.Equal("""{"size":3}""", stub.Requests[0].Body);
        Assert.Equal("?upload_id=u-1", stub.Requests[1].Uri.Query);
        Assert.Equal("upload", stub.Requests[3].Json.GetProperty("source").GetString());
        Assert.True(result.Accepted);
    }

    [Fact]
    public void Builder_sets_schema_approvals_subagents_and_skills()
    {
        var agent = Agent.Create("refunds")
            .WithOutputSchema(new JsonObject { ["type"] = "object" })
            .RequireApprovalFor("issue_refund", "issue_refund")
            .DelegatesTo("billing")
            .AddSkill("payments")
            .Build();

        Assert.Equal(["issue_refund"], agent.ApprovalTools);
        Assert.Equal(["billing"], agent.Subagents);
        Assert.Equal(IngredientKind.Skill, Assert.Single(agent.Ingredients!).Kind);
        Assert.Equal("object", agent.OutputSchema!["type"]!.GetValue<string>());
    }

    [Fact]
    public void Provider_modalities_builder_writes_the_server_schema()
    {
        var modalities = ProviderModalities.Create().Image().Pdf().Audio(stt: "whisper-1", tts: "tts-1").Clear("video").Build();

        Assert.True(modalities["image"]!["enabled"]!.GetValue<bool>());
        Assert.Equal("whisper-1", modalities["audio"]!["stt_model"]!.GetValue<string>());
        Assert.True(modalities.ContainsKey("video"));
        Assert.Null(modalities["video"]);
    }

    // ── Pro: where the Python/TS clients disagree with the server ─────────

    [Fact]
    public async Task Guardrail_toggle_is_PATCH_and_exemptions_are_removed_by_id()
    {
        var (client, stub) = Create();
        stub.RespondJson("""{"id":"g1","name":"pii","kind":"pii_detection","enabled":false}""");
        stub.Respond(HttpStatusCode.NoContent, "");

        var toggled = await client.Guardrails.ToggleAsync("g1", Ct);
        await client.Guardrails.RemoveExemptionAsync("g1", "ex-9", Ct);

        Assert.Equal(HttpMethod.Patch, stub.Requests[0].Method);
        Assert.Null(stub.Requests[0].Body);
        Assert.False(toggled.Enabled);
        Assert.Equal("/api/v1/guardrails/g1/exceptions/ex-9", stub.Requests[1].Uri.AbsolutePath);
    }

    [Fact]
    public async Task Schedules_use_cron_expr()
    {
        var (client, stub) = Create();
        stub.Respond(HttpStatusCode.Created, """{"id":"s1","recipe_name":"daily","cron_expr":"0 8 * * 1-5","enabled":true}""");

        var schedule = await client.Schedules.CreateAsync("daily", "0 8 * * 1-5", cancellationToken: Ct);

        Assert.Equal("""{"recipe_name":"daily","cron_expr":"0 8 * * 1-5","enabled":true}""", stub.Last.Body);
        Assert.Equal("0 8 * * 1-5", schedule.CronExpr);
    }

    [Fact]
    public async Task Service_account_tokens_are_nested_and_promotions_accept_202()
    {
        var (client, stub) = Create();
        stub.Respond(HttpStatusCode.Created, """{"service_account":{"id":"sa1","name":"ci","role":"baker"},"token":"ao_sa_secret","warning":"store it"}""");
        stub.Respond(HttpStatusCode.Accepted, """{"agent_name":"a","from_env":"dev","to_env":"prod","status":"pending_approval","approval_id":"ap1"}""");

        var created = await client.ServiceAccounts.CreateAsync("ci", cancellationToken: Ct);
        var promotion = await client.Environments.PromoteAsync(new PromotionRequest("a", "dev", "prod"), Ct);

        Assert.Equal("ao_sa_secret", created.Token);
        Assert.Equal("ci", created.ServiceAccount!.Name);
        Assert.Equal("pending_approval", promotion.Status);
        Assert.Equal("ap1", promotion.ApprovalId);
    }

    [Fact]
    public async Task Environments_require_name_and_slug_and_traceability_reads_matrix()
    {
        var (client, stub) = Create();
        stub.Respond(HttpStatusCode.Created, """{"id":"e1","name":"Production","slug":"prod","is_production":true,"approval_required":true}""");
        stub.RespondJson("""{"agents":["a"],"environments":["prod"],"matrix":{"a":{"prod":{"id":"d1","version":"1.0.0","status":"active"}}}}""");

        await client.Environments.CreateAsync(new DeploymentEnvironment("Production", "prod") { IsProduction = true, ApprovalRequired = true }, Ct);
        var matrix = await client.Environments.GetTraceabilityMatrixAsync(Ct);

        Assert.Equal("prod", stub.Requests[0].Json.GetProperty("slug").GetString());
        Assert.Equal("?kitchen=payments", stub.Requests[1].Uri.Query);
        Assert.Equal("1.0.0", matrix.Matrix["a"]["prod"].Version);
    }

    [Fact]
    public async Task Agent_environment_routes_send_the_kitchen_uuid()
    {
        var (client, stub) = Create();
        stub.RespondJson("""[{"id":"other","name":"default"},{"id":"6f1c2a9e-0d4b-4c43-9a57-3f0e8b2d1c77","name":"payments"}]""");
        stub.RespondJson("""{"agent_name":"a","env_slug":"dev","status":"ready"}""");
        stub.RespondJson("""[]""");

        await client.Environments.BakeAgentAsync("a", "dev", cancellationToken: Ct);
        await client.Environments.ListAgentEnvironmentsAsync("a", Ct);

        Assert.Equal("/api/v1/kitchens", stub.Requests[0].Uri.AbsolutePath);
        Assert.Equal("6f1c2a9e-0d4b-4c43-9a57-3f0e8b2d1c77", stub.Requests[1].Headers["X-Kitchen"]);
        Assert.Equal(HttpMethod.Put, stub.Requests[1].Method);
        Assert.Equal("6f1c2a9e-0d4b-4c43-9a57-3f0e8b2d1c77", stub.Requests[2].Headers["X-Kitchen"]);   // resolved once
        Assert.Equal(3, stub.Requests.Count);
    }

    [Fact]
    public void Test_suite_builder_validates_cases()
    {
        TestSuite suite = TestSuite.For("classifier").Case("refund", "Refund $50", expect: "refund").ScenarioCase("auto", "refund-flow", 0.9);

        Assert.Equal(2, suite.Cases.Count);
        Assert.Throws<InvalidOperationException>(() => TestSuite.For("x").Build());
        Assert.Throws<ArgumentOutOfRangeException>(() => TestSuite.For("x").ScenarioCase("bad", "s", 1.5));
    }
}
