using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentOven.Tests;

public class ClientTests
{
    private static (AgentOvenClient Client, StubHttpHandler Stub) Create(string? apiKey = "key-1", string kitchen = "payments")
    {
        var stub = new StubHttpHandler();
        var client = new AgentOvenClient(
            new AgentOvenClientOptions { Url = new Uri("http://oven.test:8080"), ApiKey = apiKey, Kitchen = kitchen },
            new HttpClient(stub));
        return (client, stub);
    }

    [Fact]
    public async Task Sends_auth_and_kitchen_headers()
    {
        var (client, stub) = Create();
        stub.RespondJson("[]");

        await client.Agents.ListAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("http://oven.test:8080/api/v1/agents", stub.Last.Uri.ToString());
        Assert.Equal("Bearer key-1", stub.Last.Headers["Authorization"]);
        Assert.Equal("payments", stub.Last.Headers["X-Kitchen"]);
        Assert.StartsWith("agentoven-dotnet/", stub.Last.Headers["User-Agent"]);
    }

    [Fact]
    public async Task WithKitchen_rescopes_without_changing_the_original()
    {
        var (client, stub) = Create();
        stub.RespondJson("[]").RespondJson("[]");

        var billing = client.WithKitchen("billing");
        await billing.Recipes.ListAsync(TestContext.Current.CancellationToken);
        await client.Recipes.ListAsync(TestContext.Current.CancellationToken);

        Assert.Equal("billing", stub.Requests[0].Headers["X-Kitchen"]);
        Assert.Equal("payments", stub.Requests[1].Headers["X-Kitchen"]);
        Assert.Equal("billing", billing.Kitchen);
    }

    [Fact]
    public async Task Escapes_names_in_paths_and_queries()
    {
        var (client, stub) = Create();
        stub.RespondJson("""{"name":"a b/c","status":"ready"}""").RespondJson("[]");

        await client.Agents.GetAsync("a b/c", TestContext.Current.CancellationToken);
        await client.Agents.ListAsync(tag: "team:legal&ops", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("/api/v1/agents/a%20b%2Fc", stub.Requests[0].Uri.AbsolutePath);
        Assert.Equal("?tag=team%3Alegal%26ops", stub.Requests[1].Uri.Query);
    }

    [Fact]
    public async Task Maps_error_bodies_to_AgentOvenApiException()
    {
        var (client, stub) = Create();
        stub.Respond(HttpStatusCode.NotFound, """{"error":"agent 'x' not found"}""");
        stub.Respond(HttpStatusCode.Unauthorized, """{"error":"authentication_required","message":"no credentials"}""");
        var ct = TestContext.Current.CancellationToken;

        var notFound = await Assert.ThrowsAsync<AgentOvenApiException>(() => client.Agents.GetAsync("x", ct));
        Assert.True(notFound.IsNotFound);
        Assert.Equal("agent 'x' not found", notFound.Error);
        Assert.Contains("404", notFound.Message);

        var unauthorized = await Assert.ThrowsAsync<AgentOvenApiException>(() => client.Agents.InvokeAsync("x", "hi", cancellationToken: ct));
        Assert.True(unauthorized.IsUnauthorized);
        Assert.Equal("authentication_required", unauthorized.Error);
        Assert.Equal("no credentials", unauthorized.ServerMessage);
    }

    [Fact]
    public async Task FindAsync_returns_null_on_404()
    {
        var (client, stub) = Create();
        stub.Respond(HttpStatusCode.NotFound, """{"error":"nope"}""");

        Assert.Null(await client.Agents.FindAsync("ghost", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Register_refetches_when_the_server_echoes_an_empty_version()
    {
        var (client, stub) = Create();
        stub.Respond(HttpStatusCode.Created, """{"name":"summarizer","version":"","status":"draft"}""");
        stub.RespondJson("""{"name":"summarizer","version":"0.1.0","status":"draft"}""");

        var agent = await client.Agents.RegisterAsync(Agent.Create("summarizer").UseModel("gpt-4o", "openai"), TestContext.Current.CancellationToken);

        Assert.Equal("0.1.0", agent.Version);
        Assert.Equal(HttpMethod.Post, stub.Requests[0].Method);
        Assert.Equal(HttpMethod.Get, stub.Requests[1].Method);
        Assert.Equal("gpt-4o", stub.Requests[0].Json.GetProperty("model_name").GetString());
    }

    [Fact]
    public async Task Bake_posts_options_and_reads_the_bake_response()
    {
        var (client, stub) = Create();
        stub.Respond(HttpStatusCode.Accepted, """
            {"name":"summarizer","version":"0.1.0","environment":"","status":"baking","agent_card":"/agents/summarizer/a2a/.well-known/agent-card.json"}
            """);

        var result = await client.Agents["summarizer"].BakeAsync(new BakeOptions { Environment = "prod" }, TestContext.Current.CancellationToken);

        Assert.Equal("/api/v1/agents/summarizer/bake", stub.Last.Uri.AbsolutePath);
        Assert.Equal("""{"environment":"prod"}""", stub.Last.Body);
        Assert.Equal(AgentStatus.Baking, result.Status);
        Assert.EndsWith("agent-card.json", result.AgentCard);
    }

    [Fact]
    public async Task WaitUntilReady_polls_until_ready_or_throws_when_burnt()
    {
        var (client, stub) = Create();
        stub.RespondJson("""{"name":"a","status":"baking"}""").RespondJson("""{"name":"a","status":"ready"}""");
        var ct = TestContext.Current.CancellationToken;

        var ready = await client.Agents.WaitUntilReadyAsync("a", pollInterval: TimeSpan.FromMilliseconds(1), cancellationToken: ct);
        Assert.Equal(AgentStatus.Ready, ready.Status);

        stub.RespondJson("""{"name":"a","status":"burnt","tags":{"error":"provider 'openai' not found"}}""");
        var burnt = await Assert.ThrowsAsync<AgentBurntException>(() => client.Agents.WaitUntilReadyAsync("a", cancellationToken: ct));
        Assert.Equal("provider 'openai' not found", burnt.Agent.BurntReason);
    }

    [Fact]
    public async Task Invoke_sends_message_and_variables_and_reads_the_trace()
    {
        var (client, stub) = Create();
        stub.RespondJson("""
            {"agent":"support","response":"Refund issued.","trace_id":"t-1","turns":2,
             "usage":{"input_tokens":10,"output_tokens":5,"total_tokens":15,"estimated_cost_usd":0.001},
             "latency_ms":420,
             "execution_trace":{"trace_id":"t-1","agent_name":"support","kitchen":"payments","total_ms":420,
               "turns":[{"number":1,"tool_calls":[{"id":"c1","name":"refund","arguments":{"amount":50}}],
                         "tool_results":[{"tool_call_id":"c1","name":"refund","content":"ok","is_error":false}],"latency_ms":300,
                         "usage":{"input_tokens":5,"output_tokens":2,"total_tokens":7}}],
               "usage":{"input_tokens":10,"output_tokens":5,"total_tokens":15}}}
            """);

        var result = await client.Agents.InvokeAsync(
            "support", "Refund order 42", new InvokeOptions { Variables = new Dictionary<string, string> { ["tier"] = "gold" } },
            TestContext.Current.CancellationToken);

        Assert.Equal("""{"message":"Refund order 42","variables":{"tier":"gold"}}""", stub.Last.Body);
        Assert.Equal("Refund issued.", result.Response);
        Assert.Equal(15, result.Usage!.TotalTokens);
        var call = Assert.Single(result.ExecutionTrace!.Turns![0].ToolCalls!);
        Assert.Equal(50, call.Arguments!["amount"]!.GetValue<int>());
    }

    [Fact]
    public async Task InvokeStreaming_parses_events_and_stops_at_done()
    {
        var (client, stub) = Create();
        stub.RespondSse("""
            data: {"type":"token","content":"Hel"}

            data: {"type":"tool_call","name":"lookup","args":{"id":7}}

            data: {"type":"tool_result","name":"lookup","result":"found"}

            : keep-alive

            data: {"type":"token","content":"lo"}

            data: {"type":"done","usage":{"input_tokens":3,"output_tokens":2,"total_tokens":5}}

            data: {"type":"token","content":"ignored"}


            """);

        var events = new List<AgentStreamEvent>();
        await foreach (var e in client.Agents["a"].InvokeStreamingAsync("hi", cancellationToken: TestContext.Current.CancellationToken))
        {
            events.Add(e);
        }

        Assert.Equal("/api/v1/agents/a/invoke/stream", stub.Last.Uri.AbsolutePath);
        Assert.Equal(["token", "tool_call", "tool_result", "token", "done"], events.Select(e => e.Type));
        Assert.Equal("Hello", string.Concat(events.Select(e => e.Text)));
        Assert.Equal(7, ((ToolCallStreamEvent)events[1]).Arguments!["id"]!.GetValue<int>());
        Assert.Equal(5, ((DoneStreamEvent)events[^1]).Usage!.TotalTokens);
    }

    [Fact]
    public async Task Sessions_send_content_and_dispose_deletes()
    {
        var (client, stub) = Create();
        stub.Respond(HttpStatusCode.Created, """{"id":"s-1","agent_name":"support","status":"active","turn_count":0,"total_tokens":0,"total_cost_usd":0}""");
        stub.RespondJson("""{"session_id":"s-1","turn_number":1,"content":"Hi there","usage":{"input_tokens":1,"output_tokens":2,"total_tokens":3},"status":"active"}""");
        stub.Respond(HttpStatusCode.NoContent, "");
        var ct = TestContext.Current.CancellationToken;

        await using (var chat = await client.Agents["support"].Sessions.StartAsync(cancellationToken: ct))
        {
            var reply = await chat.SendAsync("Hello", cancellationToken: ct);
            Assert.Equal("Hi there", reply.Content);
            Assert.Equal("s-1", chat.Id);
        }

        Assert.Equal("/api/v1/agents/support/sessions/s-1/messages", stub.Requests[1].Uri.AbsolutePath);
        Assert.Equal("""{"content":"Hello"}""", stub.Requests[1].Body);
        Assert.Equal(HttpMethod.Delete, stub.Requests[2].Method);
        Assert.Equal("/api/v1/agents/support/sessions/s-1", stub.Requests[2].Uri.AbsolutePath);
    }

    [Fact]
    public async Task Providers_add_puts_the_key_in_config_and_always_sends_is_default()
    {
        var (client, stub) = Create();
        stub.Respond(HttpStatusCode.Created, """{"name":"openai","kind":"openai","models":null,"is_default":false}""");

        var provider = await client.Providers.AddAsync("openai", ProviderKind.OpenAI, apiKey: "sk-1", models: ["gpt-4o"],
            cancellationToken: TestContext.Current.CancellationToken);

        var body = stub.Last.Json;
        Assert.Equal("/api/v1/models/providers", stub.Last.Uri.AbsolutePath);
        Assert.Equal("sk-1", body.GetProperty("config").GetProperty("api_key").GetString());
        Assert.False(body.GetProperty("is_default").GetBoolean());
        Assert.Equal("openai", body.GetProperty("kind").GetString());
        Assert.Null(provider.Models);
    }

    [Fact]
    public async Task Recipe_runs_unwrap_the_envelope_and_pending_gates()
    {
        var (client, stub) = Create();
        stub.Respond(HttpStatusCode.Accepted, """{"recipe":"review","status":"running","run_id":"r-1","poll":"/api/v1/recipes/review/runs/r-1"}""");
        stub.RespondJson("""{"run":{"id":"r-1","status":"running","started_at":"2026-10-04T10:00:00.123456789Z"}}""");
        stub.RespondJson("""{"run":{"id":"r-1","status":"paused","step_results":[{"step_name":"approve","step_kind":"human_gate","status":"running","gate_status":"waiting","duration_ms":0}]},"pending_gates":["approve"]}""");
        var ct = TestContext.Current.CancellationToken;

        var started = await client.Recipes.BakeAsync("review", new { contractId = 42 }, cancellationToken: ct);
        Assert.Equal("""{"input":{"contract_id":42}}""", stub.Requests[0].Body);

        var run = await client.Recipes.WaitForCompletionAsync(started, pollInterval: TimeSpan.FromMilliseconds(1), cancellationToken: ct);

        Assert.Equal(RecipeRunStatus.Paused, run.Status);
        Assert.Equal(["approve"], run.PendingGates);
        Assert.Equal(GateStatus.Waiting, run.StepResults![0].GateStatus);
        Assert.False(run.IsFinished);
    }

    [Fact]
    public async Task DI_registration_binds_configuration()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AgentOven:Url"] = "https://oven.example.com",
            ["AgentOven:Kitchen"] = "payments",
            ["AgentOven:Timeout"] = "00:00:30",
        }).Build();
        var services = new ServiceCollection();
        services.AddAgentOven(config.GetSection("AgentOven"));
        await using var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<AgentOvenClient>();

        Assert.Equal("https://oven.example.com/", client.Url.ToString());
        Assert.Equal("payments", client.Kitchen);
        Assert.Same(client, provider.GetRequiredService<AgentOvenClient>());
    }
}
