using System.Text.Json;

namespace AgentOven.Tests;

public class ModelTests
{
    [Fact]
    public void Agent_builder_produces_the_server_wire_format()
    {
        var agent = Agent.Create("summarizer")
            .Describe("Summarizes documents")
            .UseModel("gpt-4o", provider: "azure-openai")
            .WithBackupModel("claude-sonnet-4-5", provider: "anthropic")
            .WithSystemPrompt("Be concise.")
            .AddTool("doc-reader")
            .AddGuardrail(Guardrail.Input(GuardrailKind.PiiDetection))
            .Tag("team", "legal")
            .RunsOwnProcess("dotnet Summarizer.dll")
            .Build();

        var json = JsonSerializer.SerializeToElement(agent, AgentOvenJsonContext.Default.Agent);

        Assert.Equal("summarizer", json.GetProperty("name").GetString());
        Assert.Equal("azure-openai", json.GetProperty("model_provider").GetString());
        Assert.Equal("claude-sonnet-4-5", json.GetProperty("backup_model").GetString());
        Assert.Equal("custom", json.GetProperty("runtime").GetString());
        Assert.Equal("dotnet Summarizer.dll", json.GetProperty("entrypoint").GetString());
        Assert.Equal("legal", json.GetProperty("tags").GetProperty("team").GetString());
        Assert.False(json.TryGetProperty("id", out _), "server-assigned fields are omitted");
        Assert.False(json.TryGetProperty("system_prompt", out _), "the server has no system_prompt field");

        var ingredients = json.GetProperty("ingredients").EnumerateArray().ToList();
        Assert.Equal("prompt", ingredients[0].GetProperty("kind").GetString());
        Assert.Equal("Be concise.", ingredients[0].GetProperty("config").GetProperty("text").GetString());
        Assert.Equal("tool", ingredients[1].GetProperty("kind").GetString());
        Assert.True(ingredients[1].GetProperty("required").GetBoolean());

        var guardrail = json.GetProperty("guardrails")[0];
        Assert.Equal("pii_detection", guardrail.GetProperty("kind").GetString());
        Assert.Equal("input", guardrail.GetProperty("stage").GetString());
        Assert.True(guardrail.GetProperty("enabled").GetBoolean(), "guardrails must be sent enabled");
    }

    [Fact]
    public void WithSystemPrompt_replaces_rather_than_duplicates()
    {
        var agent = Agent.Create("a").WithSystemPrompt("one").WithSystemPrompt("two").Build();

        var prompt = Assert.Single(agent.Ingredients!);
        Assert.Equal("two", prompt.Config!["text"]!.GetValue<string>());
    }

    [Fact]
    public void Agent_reads_a_real_server_payload()
    {
        const string payload = """
            {"id":"9f1","name":"support","description":"","framework":"langgraph","mode":"managed","status":"cooked",
             "kitchen":"payments","version":"1.2.0","max_turns":10,"tags":{"error":"boom"},
             "ingredients":[{"id":"auto-model","name":"gpt-4o","kind":"model","config":{"provider":"openai","model":"gpt-4o"},"required":true}],
             "guardrails":[{"id":"g1","kind":"content_filter","stage":"both","enabled":true,"created_at":"0001-01-01T00:00:00Z"}],
             "resolved_config":{"tools":[{"name":"lookup","endpoint":"http://mcp/lookup","transport":"http","baked_at":"2026-10-04T10:00:00.123456789Z"}]},
             "process":{"agent_name":"support","kitchen":"payments","mode":"docker","status":"running","port":9100,"endpoint":"http://localhost:9100","started_at":"2026-10-04T10:00:00Z"},
             "created_at":"2026-10-04T10:00:00.987654321+02:00","updated_at":"2026-10-04T10:00:00Z","created_epoch":1791100800,
             "brand_new_field":{"x":1}}
            """;

        var agent = JsonSerializer.Deserialize(payload, AgentOvenJsonContext.Default.Agent)!;

        Assert.Equal("support", agent.Name);
        Assert.Equal(new AgentStatus("cooked"), agent.Status);
        Assert.Equal("boom", agent.BurntReason);
        Assert.Equal(IngredientKind.Model, agent.Ingredients![0].Kind);
        Assert.Equal(GuardrailStage.Both, agent.Guardrails![0].Stage);
        Assert.Equal("lookup", agent.ResolvedConfig!.Tools![0].Name);
        Assert.Equal(ExecutionMode.Docker, agent.Process!.Mode);
        Assert.Equal(9100, agent.Process.Port);
        Assert.Equal(1791100800, agent.CreatedEpoch);
        Assert.True(agent.AdditionalProperties!.ContainsKey("brand_new_field"));
    }

    [Fact]
    public void Extensible_enums_round_trip_unknown_values_and_compare_case_insensitively()
    {
        var status = JsonSerializer.Deserialize<AgentStatus>("\"Smoking\"", AgentOvenJsonContext.Default.Options);

        Assert.Equal("Smoking", status.Value);
        Assert.Equal("\"Smoking\"", JsonSerializer.Serialize(status, AgentOvenJsonContext.Default.Options));
        Assert.Equal(AgentStatus.Ready, new AgentStatus("READY"));
        Assert.Equal(AgentStatus.Ready.GetHashCode(), new AgentStatus("READY").GetHashCode());
    }

    [Fact]
    public void Recipe_builder_writes_snake_case_kinds_and_seconds()
    {
        var recipe = Recipe.Create("contract-review")
            .Step("extract", s => s.Agent("extractor").Retry(2))
            .Step("summarize", s => s.Agent("summarizer").DependsOn("extract").Timeout(TimeSpan.FromMinutes(2)))
            .Step("approve", s => s.HumanGate(approvers: ["legal@corp.com"], maxWait: TimeSpan.FromHours(1)).DependsOn("summarize"))
            .Step("route", s => s.Condition().When("output.risk > 0.8", "approve").Otherwise("extract"))
            .Build();

        var json = JsonSerializer.SerializeToElement(recipe, AgentOvenJsonContext.Default.Recipe);
        var steps = json.GetProperty("steps");

        Assert.Equal("extractor", steps[0].GetProperty("agent_ref").GetString());
        Assert.Equal(2, steps[0].GetProperty("max_retries").GetInt32());
        Assert.Equal(120, steps[1].GetProperty("timeout_secs").GetInt32());
        Assert.Equal("human_gate", steps[2].GetProperty("kind").GetString());
        Assert.Equal(60, steps[2].GetProperty("config").GetProperty("max_wait_minutes").GetInt32());
        Assert.Equal("legal@corp.com", steps[2].GetProperty("approver_emails")[0].GetString());
        Assert.Equal("approve", steps[3].GetProperty("branches")[0].GetProperty("next_step").GetString());
        Assert.Equal("extract", steps[3].GetProperty("default_next").GetString());
    }

    [Fact]
    public void Recipe_builder_rejects_unknown_dependencies_and_duplicate_steps()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Recipe.Create("r").Step("a", s => s.Agent("x").DependsOn("missing")).Build());
        Assert.Throws<ArgumentException>(() =>
            Recipe.Create("r").Step("a", s => s.Agent("x")).Step("a", s => s.Agent("y")));
    }

    [Fact]
    public void Stream_events_round_trip()
    {
        AgentStreamEvent[] events =
        [
            new TokenStreamEvent("hi"),
            new ToolCallStreamEvent("lookup", new() { ["id"] = 1 }),
            new ToolResultStreamEvent("lookup", "ok"),
            new ErrorStreamEvent("bad"),
            new DoneStreamEvent(new TokenUsage { InputTokens = 1, OutputTokens = 2, TotalTokens = 3 }),
        ];

        foreach (var evt in events)
        {
            var parsed = AgentStreamEvent.Parse(evt.ToJson());
            Assert.Equal(evt.Type, parsed.Type);
            Assert.Equal(evt.ToJson(), parsed.ToJson());
        }

        Assert.IsType<UnknownStreamEvent>(AgentStreamEvent.Parse("""{"type":"thinking","content":"..."}"""));
    }

    [Fact]
    public void Options_fall_back_to_defaults()
    {
        var resolved = new AgentOvenClientOptions { Url = new Uri("http://x") }.Resolve();

        Assert.Equal("http://x/", resolved.Url!.ToString());
        Assert.NotNull(resolved.Kitchen);
    }
}
