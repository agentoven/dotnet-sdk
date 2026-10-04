using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgentOven.Tests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;

namespace AgentOven.Runtime.Tests;

public class RuntimeConfigTests
{
    [Fact]
    public void Reads_the_variables_the_control_plane_injects()
    {
        var runtime = Env.Runtime(new()
        {
            ["AGENT_NAME"] = "support",
            ["AGENT_KITCHEN"] = "payments",
            ["AGENT_PORT"] = "9123",
            ["AGENTOVEN_PORT"] = "1111",
            ["AGENT_MODEL_PROVIDER"] = "anthropic",
            ["AGENT_MODEL_NAME"] = "claude-sonnet-4-5",
            ["AGENT_API_KEY"] = "sk-ant",
            ["AGENT_DESCRIPTION"] = "You help {{customer}}.",
            ["AGENT_MAX_TURNS"] = "4",
            ["AGENT_SKILLS"] = "refunds, orders",
            ["AGENT_TOOLS_JSON"] = """[{"name":"lookup","endpoint":"http://mcp.test/lookup","transport":"http","schema":{"type":"object"}}]""",
            ["AGENTOVEN_CONTROL_PLANE_URL"] = "http://cp.test:8080",
            ["CONTROL_PLANE_TOKEN"] = "svc-token",
        });

        Assert.Equal("support", runtime.Name);
        Assert.Equal(9123, runtime.Port);
        Assert.True(runtime.IsPortAssigned);
        Assert.Equal("anthropic/claude-sonnet-4-5", runtime.ModelId);
        Assert.Equal(4, runtime.MaxTurns);
        Assert.Equal(["refunds", "orders"], runtime.Skills);
        Assert.Equal("lookup", Assert.Single(runtime.Tools).Name);
        Assert.Equal("You help Ada.", runtime.RenderSystemPrompt(new Dictionary<string, string> { ["customer"] = "Ada" }));

        using var client = runtime.CreateControlPlaneClient(new HttpClient());
        Assert.Equal("payments", client.Kitchen);
    }

    [Theory]
    [InlineData("openai", "openai")]
    [InlineData("prod-azure-openai", "azure-openai")]
    [InlineData("team-anthropic", "anthropic")]
    [InlineData("e2e-ollama", "ollama")]
    [InlineData("my-models", null)]
    public void Infers_the_provider_kind_from_its_name(string provider, string? kind)
    {
        Assert.Equal(kind, Env.Runtime(new() { ["AGENT_MODEL_PROVIDER"] = provider }).ModelKind);
        Assert.Equal("gemini", Env.Runtime(new() { ["AGENT_MODEL_PROVIDER"] = provider, ["AGENT_MODEL_KIND"] = "gemini" }).ModelKind);
    }

    [Fact]
    public void Defaults_without_environment()
    {
        var runtime = Env.Runtime([]);

        Assert.Equal(AgentOvenRuntime.DefaultPort, runtime.Port);
        Assert.False(runtime.IsPortAssigned);
        Assert.Equal(AgentOvenRuntime.DefaultMaxTurns, runtime.MaxTurns);
        Assert.Empty(runtime.Tools);
        Assert.Equal("", runtime.SystemPrompt);
    }

    [Fact]
    public void Ignores_malformed_tool_json()
    {
        var runtime = Env.Runtime(new() { ["AGENT_TOOLS_JSON"] = "not json" });

        Assert.Empty(runtime.Tools);
    }
}

public class EndpointTests
{
    [Fact]
    public async Task Health_status_and_agent_card()
    {
        await using var app = await TestApp.StartAsync(AgentHandler.From((m, _) => Task.FromResult(m)));
        var client = app.GetTestClient();
        var ct = TestContext.Current.CancellationToken;

        var health = await client.GetFromJsonElementAsync("/health", ct);
        Assert.Equal("healthy", health.GetProperty("status").GetString());
        Assert.Equal("support", health.GetProperty("agent").GetString());
        Assert.Equal("dotnet", health.GetProperty("runtime").GetString());
        Assert.Equal("lookup", health.GetProperty("tools")[0].GetString());

        var status = await client.GetAsync("/status", ct);
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);

        var card = await client.GetFromJsonElementAsync("/.well-known/agent-card.json", ct);
        Assert.Equal("support", card.GetProperty("name").GetString());
        Assert.True(card.GetProperty("capabilities").GetProperty("streaming").GetBoolean());
        Assert.True(card.GetProperty("capabilities").GetProperty("toolCalling").GetBoolean());
        Assert.Equal("refunds", card.GetProperty("skills")[0].GetProperty("id").GetString());
    }

    [Fact]
    public async Task Invoke_returns_the_process_contract_shape()
    {
        AgentRequest? seen = null;
        await using var app = await TestApp.StartAsync(AgentHandler.From((request, _) =>
        {
            seen = request;
            return Task.FromResult(new AgentReply($"echo: {request.Message}")
            {
                Usage = new TokenUsage { InputTokens = 3, OutputTokens = 4, TotalTokens = 7 },
            });
        }));
        var ct = TestContext.Current.CancellationToken;

        var response = await app.GetTestClient().PostAsync("/invoke", Json("""
            {"message":"hi","trace_id":"t-9","variables":{"tier":"gold"},"provider_config":{"name":"p","tls_skip_verify":true}}
            """), ct);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)).RootElement;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("echo: hi", body.GetProperty("response").GetString());
        Assert.Equal(7, body.GetProperty("usage").GetProperty("total_tokens").GetInt32());
        Assert.Equal("t-9", seen!.TraceId);
        Assert.Equal("gold", seen.Variables["tier"]);
        Assert.True(seen.ProviderConfig!.TlsSkipVerify);
    }

    [Fact]
    public async Task Invoke_validates_input_and_reports_handler_errors()
    {
        await using var app = await TestApp.StartAsync(AgentHandler.From((_, _) => throw new InvalidOperationException("model down")));
        var client = app.GetTestClient();
        var ct = TestContext.Current.CancellationToken;

        var missing = await client.PostAsync("/invoke", Json("{}"), ct);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal("missing 'message' field", JsonDocument.Parse(await missing.Content.ReadAsStringAsync(ct)).RootElement.GetProperty("error").GetString());

        var invalid = await client.PostAsync("/invoke", Json("{nope"), ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var failed = await client.PostAsync("/invoke", Json("""{"message":"hi"}"""), ct);
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        Assert.Contains("model down", await failed.Content.ReadAsStringAsync(ct));
    }

    [Fact]
    public async Task Stream_falls_back_to_invoke_and_always_ends_with_done()
    {
        await using var app = await TestApp.StartAsync(AgentHandler.From((m, _) => Task.FromResult($"echo: {m}")));
        var ct = TestContext.Current.CancellationToken;

        var response = await app.GetTestClient().PostAsync("/invoke/stream", Json("""{"message":"hi"}"""), ct);
        var events = ParseSse(await response.Content.ReadAsStringAsync(ct));

        Assert.Equal("text/event-stream", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal(["token", "done"], events.Select(e => e.Type));
        Assert.Equal("echo: hi", events[0].Text);
    }

    [Fact]
    public async Task Stream_reports_errors_then_done()
    {
        await using var app = await TestApp.StartAsync(new ThrowingStreamHandler());
        var ct = TestContext.Current.CancellationToken;

        var response = await app.GetTestClient().PostAsync("/invoke/stream", Json("""{"message":"hi"}"""), ct);
        var events = ParseSse(await response.Content.ReadAsStringAsync(ct));

        Assert.Equal(["token", "error", "done"], events.Select(e => e.Type));
        Assert.Equal("stream broke", ((ErrorStreamEvent)events[1]).Message);
    }

    [Fact]
    public async Task A2A_send_get_and_unknown_method()
    {
        await using var app = await TestApp.StartAsync(AgentHandler.From((m, _) => Task.FromResult($"echo: {m}")));
        var client = app.GetTestClient();
        var ct = TestContext.Current.CancellationToken;

        var send = await client.PostAsync("/a2a", Json("""
            {"jsonrpc":"2.0","id":1,"method":"tasks/send","params":{"id":"task-1","message":{"role":"user","parts":[{"type":"text","text":"hi"}]}}}
            """), ct);
        var sent = JsonDocument.Parse(await send.Content.ReadAsStringAsync(ct)).RootElement;
        Assert.Equal(1, sent.GetProperty("id").GetInt32());
        Assert.Equal("completed", sent.GetProperty("result").GetProperty("status").GetProperty("state").GetString());
        Assert.Equal("echo: hi", sent.GetProperty("result").GetProperty("artifacts")[0].GetProperty("parts")[0].GetProperty("text").GetString());

        var get = await client.PostAsync("/", Json("""{"jsonrpc":"2.0","id":"g","method":"tasks/get","params":{"id":"task-1"}}"""), ct);
        var got = JsonDocument.Parse(await get.Content.ReadAsStringAsync(ct)).RootElement;
        Assert.Equal("task-1", got.GetProperty("result").GetProperty("id").GetString());

        var unknown = await client.PostAsync("/a2a", Json("""{"jsonrpc":"2.0","id":2,"method":"tasks/explode"}"""), ct);
        var error = JsonDocument.Parse(await unknown.Content.ReadAsStringAsync(ct)).RootElement;
        Assert.Equal(-32601, error.GetProperty("error").GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task Class_handlers_are_resolved_with_dependency_injection()
    {
        await using var app = await TestApp.StartAsync(map: a => a.MapAgentOven<GreetingHandler>());
        var ct = TestContext.Current.CancellationToken;

        var response = await app.GetTestClient().PostAsync("/invoke", Json("""{"message":"Ada"}"""), ct);

        Assert.Contains("Hello Ada from support", await response.Content.ReadAsStringAsync(ct));
    }

    internal static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    internal static List<AgentStreamEvent> ParseSse(string body) =>
        body.Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(chunk => chunk.Trim())
            .Where(chunk => chunk.StartsWith("data: ", StringComparison.Ordinal))
            .Select(chunk => AgentStreamEvent.Parse(chunk["data: ".Length..]))
            .ToList();

    private sealed class ThrowingStreamHandler : IAgentHandler
    {
        public Task<AgentReply> InvokeAsync(AgentRequest request, CancellationToken cancellationToken) => Task.FromResult(new AgentReply("x"));

        public async IAsyncEnumerable<AgentStreamEvent> StreamAsync(AgentRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return new TokenStreamEvent("partial");
            await Task.Yield();
            throw new InvalidOperationException("stream broke");
        }
    }

    public sealed class GreetingHandler(AgentOvenRuntime runtime) : IAgentHandler
    {
        public Task<AgentReply> InvokeAsync(AgentRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new AgentReply($"Hello {request.Message} from {runtime.Name}"));
    }
}

public class AgentToolsTests
{
    [Fact]
    public async Task Calls_MCP_tools_over_JSON_RPC()
    {
        var stub = new StubHttpHandler().RespondJson("""{"jsonrpc":"2.0","id":"1","result":{"content":[{"type":"text","text":"order 42: shipped"}]}}""");
        var tools = new AgentTools(Env.Runtime(Env.WithTools()), new HttpClient(stub));

        var result = await tools.CallAsync("lookup", new JsonObject { ["order"] = 42 }, TestContext.Current.CancellationToken);

        Assert.Equal("order 42: shipped", result);
        Assert.Equal("http://mcp.test/lookup", stub.Last.Uri.ToString());
        var rpc = stub.Last.Json;
        Assert.Equal("tools/call", rpc.GetProperty("method").GetString());
        Assert.Equal(42, rpc.GetProperty("params").GetProperty("arguments").GetProperty("order").GetInt32());
    }

    [Fact]
    public async Task Falls_back_to_REST_call_and_reports_errors_as_text()
    {
        var stub = new StubHttpHandler()
            .Respond(HttpStatusCode.InternalServerError, "boom")
            .RespondJson("""{"content":[{"type":"text","text":"via rest"}]}""");
        var tools = new AgentTools(Env.Runtime(Env.WithTools()), new HttpClient(stub));
        var ct = TestContext.Current.CancellationToken;

        Assert.Equal("via rest", await tools.CallAsync("lookup", null, ct));
        Assert.Equal("http://mcp.test/lookup/call", stub.Last.Uri.ToString());

        stub.Respond(HttpStatusCode.OK, """{"jsonrpc":"2.0","id":"1","error":{"code":-1,"message":"not allowed"}}""");
        Assert.Equal("Tool error: not allowed", await tools.CallAsync("lookup", null, ct));
        Assert.StartsWith("Error: tool 'nope'", await tools.CallAsync("nope", null, ct));
    }

    [Fact]
    public async Task Delegation_calls_the_control_plane_as_this_agent()
    {
        var stub = new StubHttpHandler().RespondJson("""{"agent":"billing","response":"refunded","trace_id":"t"}""");
        var runtime = Env.Runtime(new()
        {
            ["AGENT_KITCHEN"] = "payments",
            ["AGENTOVEN_CONTROL_PLANE_URL"] = "http://cp.test:8080",
            ["CONTROL_PLANE_TOKEN"] = "svc-token",
        });
        var tools = new AgentTools(runtime, new HttpClient(stub));

        Assert.True(tools.OffersDelegation);
        var result = await tools.CallAsync(
            AgentTools.DelegateToolName, new JsonObject { ["agent"] = "billing", ["message"] = "refund 42" }, TestContext.Current.CancellationToken);

        Assert.Equal("refunded", result);
        Assert.Equal("http://cp.test:8080/api/v1/agents/billing/invoke", stub.Last.Uri.ToString());
        Assert.Equal("svc-token", stub.Last.Headers["X-Service-Token"]);
        Assert.Equal("payments", stub.Last.Headers["X-Kitchen"]);
    }
}

internal static class Env
{
    public static AgentOvenRuntime Runtime(Dictionary<string, string> variables) =>
        new(name => variables.TryGetValue(name, out var value) ? value : null);

    public static Dictionary<string, string> WithTools() => new()
    {
        ["AGENT_NAME"] = "support",
        ["AGENT_SKILLS"] = "refunds",
        ["AGENT_DESCRIPTION"] = "You are support.",
        ["AGENT_TOOLS_JSON"] = """[{"name":"lookup","endpoint":"http://mcp.test/lookup","transport":"http","schema":{"description":"Look up an order","type":"object","properties":{"order":{"type":"integer"}}}}]""",
    };
}

internal static class TestApp
{
    public static async Task<WebApplication> StartAsync(
        IAgentHandler? handler = null,
        Action<WebApplication>? map = null,
        Action<WebApplicationBuilder>? configure = null,
        Dictionary<string, string>? env = null)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.AddAgentOvenRuntime(Env.Runtime(env ?? Env.WithTools()), o => o.BindPort = false);
        configure?.Invoke(builder);
        var app = builder.Build();
        if (map is not null)
        {
            map(app);
        }
        else
        {
            app.MapAgentOven(handler!);
        }

        await app.StartAsync();
        return app;
    }

    public static async Task<JsonElement> GetFromJsonElementAsync(this HttpClient client, string path, CancellationToken ct)
    {
        var response = await client.GetAsync(path, ct);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)).RootElement;
    }
}
