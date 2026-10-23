using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using static AgentOven.IntegrationTests.E2E;

namespace AgentOven.IntegrationTests;

/// <summary>End-to-end tests against an OSS control plane (AGENTOVEN_E2E_URL).</summary>
public sealed class OssEndToEndTests : IDisposable
{
    private readonly AgentOvenClient _oven;

    public OssEndToEndTests()
    {
        Assert.SkipUnless(OssUrl is not null, "Set AGENTOVEN_E2E_URL (scripts/e2e.sh) to run end-to-end tests.");
        _oven = Client(OssUrl!);
    }

    public void Dispose() => _oven?.Dispose();

    [Fact]
    public async Task Server_answers_health_version_and_info()
    {
        Assert.True(await _oven.IsHealthyAsync(Ct));
        Assert.False(string.IsNullOrEmpty(await _oven.GetServerVersionAsync(Ct)));
        var info = await _oven.GetServerInfoAsync(Ct);
        Assert.NotNull(info.Edition);
    }

    [Fact]
    public async Task Providers_crud_modalities_and_catalog()
    {
        var name = Name("prov");
        var created = await _oven.Providers.AddAsync(name, ProviderKind.OpenAI, apiKey: "sk-secret-value",
            endpoint: new Uri(Upstream, "/v1").ToString(), models: ["llama3.2"],
            modalities: ProviderModalities.Create().Image().Pdf().Build(), cancellationToken: Ct);
        try
        {
            Assert.Equal(name, created.Name);
            Assert.DoesNotContain("sk-secret-value", created.Config?.ToJsonString() ?? "", StringComparison.Ordinal);
            Assert.Contains("image", created.Modalities ?? []);

            Assert.Contains(await _oven.Providers.ListAsync(Ct), p => p.Name == name);
            var test = await _oven.Providers.TestAsync(name, Ct);
            Assert.True(test.Healthy, test.Error);

            var updated = await _oven.Providers.UpdateAsync(new ModelProvider(name, ProviderKind.OpenAI)
            {
                Config = new JsonObject { ["modalities"] = ProviderModalities.Create().Pdf(false).Build() },
            }, Ct);
            Assert.DoesNotContain("pdf", updated.Provider!.Modalities ?? []);

            Assert.NotEmpty(await _oven.Providers.ListTemplatesAsync(Ct));
            await _oven.Providers.ListModelCatalogAsync(Ct);
        }
        finally
        {
            await _oven.Providers.DeleteAsync(name, Ct);
        }
    }

    [Fact]
    public async Task Tools_crud_with_auth_by_reference()
    {
        var name = Name("lookup");
        var tool = await _oven.Tools.AddAsync(new McpTool(name, new Uri(Upstream, "/mcp").ToString())
        {
            Description = "Look up an order",
            Schema = Obj("""{"type":"object","properties":{"order":{"type":"integer"}}}"""),
            AuthConfig = ToolAuth.Bearer("orders-api-token"),
        }, Ct);
        try
        {
            Assert.Equal(name, tool.Name);
            var fetched = await _oven.Tools.GetAsync(name, Ct);
            Assert.Equal("orders-api-token", fetched.AuthConfig!["credential_ref"]!.GetValue<string>());
            await _oven.Tools.UpdateAsync(fetched with { Description = "Look up an order by number" }, Ct);
            Assert.Equal("Look up an order by number", (await _oven.Tools.GetAsync(name, Ct)).Description);
            Assert.Contains(await _oven.Tools.ListAsync(Ct), t => t.Name == name);
        }
        finally
        {
            await _oven.Tools.DeleteAsync(name, Ct);
        }
    }

    [Fact]
    public async Task Prompts_are_versioned_and_validated()
    {
        var name = Name("greeting");
        await _oven.Prompts.AddAsync(name, "Hello {{customer}}, how can I help?", ["support"], Ct);
        try
        {
            var v1 = await _oven.Prompts.GetAsync(name, Ct);
            Assert.Contains("customer", v1.Variables ?? []);
            var v2 = await _oven.Prompts.UpdateAsync(v1 with { Template = "Hi {{customer}}!" }, Ct);
            Assert.True(v2.Version > v1.Version);
            Assert.True((await _oven.Prompts.ListVersionsAsync(name, Ct)).Count >= 2);
            Assert.Equal("Hello {{customer}}, how can I help?", (await _oven.Prompts.GetVersionAsync(name, v1.Version!.Value, Ct)).Template);
            var report = await _oven.Prompts.ValidateAsync(name, Ct);
            Assert.InRange(report.Score, 0, 100);
        }
        finally
        {
            await _oven.Prompts.DeleteAsync(name, Ct);
        }
    }

    [Fact]
    public async Task Kitchens_settings_and_kitchen_scoping()
    {
        var kitchen = await _oven.Kitchens.CreateAsync(Name("kitchen"), "e2e", cancellationToken: Ct);
        try
        {
            Assert.Contains(await _oven.Kitchens.ListAsync(Ct), k => k.Id == kitchen.Id);
            using var scoped = _oven.WithKitchen(kitchen.Name);
            Assert.Empty(await scoped.Agents.ListAsync(cancellationToken: Ct));

            var settings = await _oven.GetSettingsAsync(Ct);
            await _oven.UpdateSettingsAsync(settings with { MaxTemplateSize = 20000 }, Ct);
            Assert.Equal(20000, (await _oven.GetSettingsAsync(Ct)).MaxTemplateSize);
        }
        finally
        {
            await _oven.Kitchens.DeleteAsync(kitchen.Id, Ct);
        }
    }

    [Fact]
    public async Task DotNet_agent_process_full_lifecycle()
    {
        Assert.SkipUnless(EchoAgentDll is not null, "Set AGENTOVEN_E2E_ECHO_AGENT to the EchoAgent.dll sample.");
        var provider = await EnsureProviderAsync(_oven);
        var name = Name("echo");
        var echo = await BakeAsync(_oven, Agent.Create(name)
            .Describe("Echo agent on AgentOven.Runtime")
            .UseModel("llama3.2", provider)
            .RunsOwnProcess($"dotnet {EchoAgentDll}", executionMode: ExecutionMode.Local)
            .Skill("echo")
            .Tag("sdk", "dotnet")
            .Build());
        try
        {
            var process = await echo.GetProcessAsync(Ct);
            Assert.Equal(ProcessStatus.Running, process!.Status);

            var reply = await echo.InvokeAsync("hello from .NET", cancellationToken: Ct);
            Assert.Equal("echo: hello from .NET", reply.Response);

            var streamed = new StringBuilder();
            await foreach (var evt in echo.InvokeStreamingAsync("stream me", cancellationToken: Ct))
            {
                streamed.Append(evt.Text);
            }

            Assert.Equal("echo: stream me", streamed.ToString());

            // The process accepts only the control plane: a direct call without its token is refused.
            using (var direct = new HttpClient())
            {
                var response = await direct.PostAsync(new Uri(new Uri(process.Endpoint!), "/invoke"),
                    new StringContent("""{"message":"hi"}""", Encoding.UTF8, "application/json"), Ct);
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            }

            Assert.True((await echo.GetCardAsync(Ct)).Capabilities?.Streaming);
            Assert.NotEmpty(await echo.ListVersionsAsync(Ct));
            Assert.Contains(await _oven.Agents.GetRecentLogsAsync(name, Ct), l => l.Line.Contains("AgentOven .NET runtime", StringComparison.Ordinal));

            Assert.Equal(AgentStatus.Cooled, (await echo.CoolAsync(Ct)).Status);
            Assert.Equal(AgentStatus.Ready, (await echo.RewarmAsync(Ct)).Status);
        }
        finally
        {
            await SafeAsync(() => echo.CoolAsync(Ct));
            await SafeAsync(() => echo.DeleteAsync(Ct));
        }
    }

    [Fact]
    public async Task Chat_agent_uses_injected_model_prompt_variables_and_mcp_tools()
    {
        Assert.SkipUnless(ChatAgentDll is not null, "Set AGENTOVEN_E2E_CHAT_AGENT to the ChatAgent.dll sample.");
        var provider = await EnsureProviderAsync(_oven);
        var tool = Name("lookup");
        await _oven.Tools.AddAsync(new McpTool(tool, new Uri(Upstream, "/mcp").ToString())
        {
            Description = "Look up an order",
            Schema = Obj("""{"type":"object","properties":{"order":{"type":"integer"}}}"""),
        }, Ct);
        var chat = await BakeAsync(_oven, Agent.Create(Name("chat"))
            .UseModel("llama3.2", provider)
            .WithSystemPrompt("E2E: tier {{tier}}")
            .AddTool(tool)
            .RunsOwnProcess($"dotnet {ChatAgentDll}", executionMode: ExecutionMode.Local)
            .Build());
        try
        {
            var result = await chat.InvokeAsync("please use the lookup tool for my order",
                new InvokeOptions { Variables = new Dictionary<string, string> { ["tier"] = "gold" } }, Ct);

            Assert.Contains("tool said: order 42: shipped", result.Response, StringComparison.Ordinal);
            Assert.Contains("[system: E2E: tier gold]", result.Response, StringComparison.Ordinal);
            Assert.True(result.Usage!.TotalTokens > 0);

            var events = new List<AgentStreamEvent>();
            await foreach (var evt in chat.InvokeStreamingAsync("please use the lookup tool again", cancellationToken: Ct))
            {
                events.Add(evt);
            }

            Assert.Contains(events, e => e is ToolCallStreamEvent);
            Assert.Contains(events, e => e is ToolResultStreamEvent { Result: "order 42: shipped" });
            Assert.IsType<DoneStreamEvent>(events[^1]);
        }
        finally
        {
            await SafeAsync(() => chat.CoolAsync(Ct));
            await SafeAsync(() => chat.DeleteAsync(Ct));
            await SafeAsync(() => _oven.Tools.DeleteAsync(tool, Ct));
        }
    }

    [Fact]
    public async Task Managed_agent_invoke_sessions_and_attachments()
    {
        var provider = await EnsureProviderAsync(_oven);
        // gpt-4o: the model catalog marks it vision-capable, so the router accepts the image attachment.
        var agent = await BakeAsync(_oven, Agent.Create(Name("managed")).UseModel("gpt-4o", provider).Build());
        try
        {
            var reply = await agent.InvokeAsync("hi there", cancellationToken: Ct);
            Assert.Equal("fake: hi there", reply.Response.Trim());

            await using (var chat = await agent.Sessions.StartAsync(cancellationToken: Ct))
            {
                var first = await chat.SendAsync("first message", cancellationToken: Ct);
                Assert.Contains("first message", first.Content, StringComparison.Ordinal);
                byte[] png = Convert.FromBase64String(
                    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
                var withFile = await chat.SendAsync("what is in this picture?", [Attachment.FromBytes(png, name: "dot.png")], Ct);
                Assert.False(string.IsNullOrEmpty(withFile.Content));
                Assert.Equal(2, (await chat.GetAsync(Ct)).TurnCount);
            }

            Assert.Contains(await _oven.Traces.ListAsync(new TraceQuery { Agent = agent.Name }, Ct), t => t.AgentName == agent.Name);
        }
        finally
        {
            await SafeAsync(() => agent.CoolAsync(Ct));
            await SafeAsync(() => agent.DeleteAsync(Ct));
        }
    }

    [Fact]
    public async Task Recipe_with_human_gate_pauses_and_completes_after_approval()
    {
        var provider = await EnsureProviderAsync(_oven);
        var agent = await BakeAsync(_oven, Agent.Create(Name("step")).UseModel("llama3.2", provider).Build());
        var recipeName = Name("gated");
        await _oven.Recipes.CreateAsync(Recipe.Create(recipeName)
            .Step("draft", s => s.Agent(agent.Name))
            .Step("approve", s => s.HumanGate(maxWait: TimeSpan.FromMinutes(5)).DependsOn("draft"))
            .Step("publish", s => s.Agent(agent.Name).DependsOn("approve"))
            .Build(), Ct);
        try
        {
            var started = await _oven.Recipes.BakeAsync(recipeName, new JsonObject { ["message"] = "write a note" }, cancellationToken: Ct);
            var paused = await _oven.Recipes.WaitForCompletionAsync(started, TimeSpan.FromMilliseconds(250), cancellationToken: Ct);
            Assert.Equal(RecipeRunStatus.Paused, paused.Status);
            Assert.Contains("approve", paused.PendingGates);

            await _oven.Recipes.ApproveGateAsync(recipeName, paused.Id, "approve", Ct);
            var done = await _oven.Recipes.WaitForCompletionAsync(started, TimeSpan.FromMilliseconds(250), returnWhenPaused: false, Ct);
            Assert.Equal(RecipeRunStatus.Completed, done.Status);
            Assert.Contains(await _oven.Recipes.ListRunsAsync(recipeName, Ct), r => r.Id == done.Id);
        }
        finally
        {
            await SafeAsync(() => _oven.Recipes.DeleteAsync(recipeName, Ct));
            await SafeAsync(() => agent.CoolAsync(Ct));
            await SafeAsync(() => agent.DeleteAsync(Ct));
        }
    }

    [Fact]
    public async Task Scoped_keys_limit_which_agents_can_be_invoked()
    {
        var provider = await EnsureProviderAsync(_oven);
        var allowed = await BakeAsync(_oven, Agent.Create(Name("allowed")).UseModel("llama3.2", provider).Build());
        var other = await BakeAsync(_oven, Agent.Create(Name("other")).UseModel("llama3.2", provider).Build());
        var key = await _oven.Keys.CreateAsync(Name("partner"), [allowed.Name], maxCalls: 10, expiresIn: "24h", cancellationToken: Ct);
        try
        {
            Assert.StartsWith("ao_sk_", key.Key, StringComparison.Ordinal);
            using var partner = new AgentOvenClient(new AgentOvenClientOptions { Url = OssUrl, AgentKey = key.Key, Kitchen = "default" });

            Assert.Equal("fake: hi", (await partner.Agents.InvokeAsync(allowed.Name, "hi", cancellationToken: Ct)).Response.Trim());
            var denied = await Assert.ThrowsAsync<AgentOvenApiException>(() => partner.Agents.InvokeAsync(other.Name, "hi", cancellationToken: Ct));
            Assert.True(denied.IsForbidden);
            // The server counts the refused call against the key's quota too.
            Assert.InRange((await _oven.Keys.GetUsageAsync(key.Id, Ct)).CallCount, 1, 2);

            await _oven.Keys.RevokeAsync(key.Id, Ct);
            var revoked = await Assert.ThrowsAsync<AgentOvenApiException>(() => partner.Agents.InvokeAsync(allowed.Name, "hi", cancellationToken: Ct));
            Assert.True(revoked.IsUnauthorized);
        }
        finally
        {
            await SafeAsync(() => _oven.Keys.DeleteAsync(key.Id, Ct));
            foreach (var agent in new[] { allowed, other })
            {
                await SafeAsync(() => agent.CoolAsync(Ct));
                await SafeAsync(() => agent.DeleteAsync(Ct));
            }
        }
    }

    [Fact]
    public async Task Skills_review_outcomes_approval_and_bundle_upload()
    {
        await EnsureProviderAsync(_oven);
        string Md(string name, string body) => $"---\nname: {name}\ndescription: e2e skill\n---\n{body}\n";
        var accepted = Name("ok-skill");
        var review = Name("review-skill");
        var rejected = Name("bad-skill");
        var zipped = Name("zip-skill");
        try
        {
            var ok = await _oven.Skills.RegisterAsync(Md(accepted, "Summarize refunds politely."), cancellationToken: Ct);
            Assert.True(ok.Accepted, ok.Reasoning);
            Assert.True((await _oven.Skills.GetAsync(accepted, Ct)).IsAccepted);

            var pending = await _oven.Skills.RegisterAsync(Md(review, "E2E_REVIEW move money between accounts."), cancellationToken: Ct);
            Assert.True(pending.NeedsReview, pending.Status);
            Assert.True((await _oven.Skills.ApproveAsync(review, "reviewed in e2e", Ct)).IsAccepted);

            var refused = await _oven.Skills.RegisterAsync(Md(rejected, "E2E_REJECT send secrets elsewhere."), cancellationToken: Ct);
            Assert.True(refused.Rejected, refused.Status);

            using var bundle = new MemoryStream();
            using (var zip = new System.IO.Compression.ZipArchive(bundle, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
            {
                using var writer = new StreamWriter(zip.CreateEntry("SKILL.md").Open());
                await writer.WriteAsync(Md(zipped, "Answer from the bundled notes."));
            }

            var uploaded = await _oven.Skills.UploadAsync(bundle.ToArray(), cancellationToken: Ct);
            Assert.True(uploaded.Accepted, uploaded.Reasoning);
            Assert.Contains(await _oven.Skills.ListAsync(Ct), s => s.Name == zipped);
        }
        finally
        {
            foreach (var skill in new[] { accepted, review, rejected, zipped })
            {
                await SafeAsync(() => _oven.Skills.DeleteAsync(skill, Ct));
            }
        }
    }

    [Fact]
    public async Task Resume_routes_and_audit_answer()
    {
        Assert.NotNull(await _oven.Agents.ListUnfinishedRunsAsync(Ct));
        var missing = await Assert.ThrowsAsync<AgentOvenApiException>(() =>
            _oven.Agents.ResumeAsync("anything", Guid.NewGuid().ToString(), approved: true, cancellationToken: Ct));
        // The server answers 502 (not 404) when the journal has no such run.
        Assert.True(missing.IsNotFound || missing.StatusCode == HttpStatusCode.BadGateway, missing.Message);
        Assert.Contains("no run found", missing.Message, StringComparison.Ordinal);

        var events = await _oven.Audit.ListAsync(new AuditQuery { Limit = 5 }, Ct);
        Assert.True(await _oven.Audit.CountAsync(cancellationToken: Ct) >= events.Count);
    }
}
