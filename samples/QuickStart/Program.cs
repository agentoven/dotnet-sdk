// Drives a control plane end to end: provider → agent → bake → invoke → session → recipe.
// Run `agentoven serve` (or docker compose up) first; set AGENTOVEN_URL / AGENTOVEN_API_KEY if needed.
using AgentOven;

using var oven = new AgentOvenClient();
Console.WriteLine($"Control plane {oven.Url} (kitchen '{oven.Kitchen}') healthy: {await oven.IsHealthyAsync()}");

// 1. A model provider the agent can use.
await oven.Providers.AddAsync("openai", ProviderKind.OpenAI,
    apiKey: Environment.GetEnvironmentVariable("OPENAI_API_KEY"), models: ["gpt-4o-mini"]);

// 2. Define and register an agent.
var agent = Agent.Create("summarizer")
    .Describe("Summarizes text in three bullet points")
    .UseModel("gpt-4o-mini", provider: "openai")
    .WithSystemPrompt("Summarize the user's text in exactly three bullet points.")
    .AddGuardrail(Guardrail.Input(GuardrailKind.PromptInjection))
    .Tag("team", "docs")
    .Build();
await oven.Agents.RegisterAsync(agent);

// 3. Bake it and wait until it is ready.
var summarizer = oven.Agents["summarizer"];
await summarizer.BakeAndWaitAsync();

// 4. One-shot invoke.
var result = await summarizer.InvokeAsync("AgentOven is an enterprise control plane for AI agents ...");
Console.WriteLine($"{result.Response}\n({result.Usage?.TotalTokens} tokens, {result.LatencyMs} ms, trace {result.TraceId})");

// 5. A multi-turn session (deleted when disposed).
await using (var chat = await summarizer.Sessions.StartAsync())
{
    Console.WriteLine(await chat.SendAsync("Summarize: the quick brown fox jumps over the lazy dog."));
    Console.WriteLine(await chat.SendAsync("Now make it shorter."));
}

// 6. A recipe: two steps in sequence, then wait for the run.
await oven.Recipes.CreateAsync(Recipe.Create("summarize-twice")
    .Step("first", s => s.Agent("summarizer"))
    .Step("second", s => s.Agent("summarizer").DependsOn("first").Timeout(TimeSpan.FromMinutes(1)))
    .Build());
var started = await oven.Recipes.BakeAsync("summarize-twice", new System.Text.Json.Nodes.JsonObject { ["text"] = "..." });
var run = await oven.Recipes.WaitForCompletionAsync(started);
Console.WriteLine($"Recipe run {run.Id}: {run.Status}");

// 7. Stop the agent.
await summarizer.CoolAsync();
