# AgentOven .NET SDK

.NET SDK for [AgentOven](https://agentoven.dev), the enterprise agent control plane. It is the .NET counterpart
of the [Python SDK](https://github.com/agentoven/agentoven/tree/main/sdk/python) and has two halves:

- **Client**: drive the control plane from any .NET app. Register agents, bake them, invoke them, run recipes,
  and manage providers, sessions and kitchens.
- **Runtime**: write the agent itself in .NET. The control plane starts your process at bake time, and the
  runtime serves the endpoints it expects. Adapters cover Microsoft.Extensions.AI, Microsoft Agent Framework
  and Semantic Kernel.

Targets **.NET 10, 9 and 8**. Fully async, nullable-annotated, and trim/AOT-safe for the client and the runtime.

| Package | What it is |
|---|---|
| `AgentOven` | `AgentOvenClient`, models, fluent builders. No dependencies. |
| `AgentOven.Extensions.DependencyInjection` | `services.AddAgentOven(...)` with `IHttpClientFactory` and configuration binding |
| `AgentOven.Runtime` | Host a .NET agent process: `AddAgentOvenRuntime()` + `MapAgentOven(...)` |
| `AgentOven.Runtime.AI` | `IChatClient` for the injected model, MCP tools as `AIFunction`s, a ready-made tool-calling handler |
| `AgentOven.Runtime.AgentFramework` | Serve a Microsoft Agent Framework `AIAgent` |
| `AgentOven.Runtime.SemanticKernel` | Serve a Semantic Kernel `Kernel` or `Agent` |

## Client

```csharp
using AgentOven;

using var oven = new AgentOvenClient();   // AGENTOVEN_URL / AGENTOVEN_API_KEY / AGENTOVEN_KITCHEN, else http://localhost:8080

await oven.Providers.AddAsync("openai", ProviderKind.OpenAI, apiKey: key, models: ["gpt-4o"]);

var agent = Agent.Create("summarizer")
    .Describe("Summarizes long documents")
    .UseModel("gpt-4o", provider: "openai")
    .WithBackupModel("claude-sonnet-4-5", provider: "anthropic")
    .WithSystemPrompt("You are a concise summarizer.")
    .AddTool("doc-reader")
    .AddGuardrail(Guardrail.Input(GuardrailKind.PiiDetection))
    .Build();

await oven.Agents.RegisterAsync(agent);

var summarizer = oven.Agents["summarizer"];        // a handle; no network call
await summarizer.BakeAndWaitAsync();               // throws AgentBurntException with the reason if baking fails

InvokeResult result = await summarizer.InvokeAsync("Summarize: ...");
Console.WriteLine($"{result.Response} ({result.Usage?.TotalTokens} tokens, trace {result.TraceId})");

await using var chat = await summarizer.Sessions.StartAsync();   // deleted on dispose unless keep: true
var reply = await chat.SendAsync("And the second clause?");
```

The client works at three levels of fluency:

1. **Builders** for definitions: `Agent.Create(...)`, `Recipe.Create(...)`.
2. **Resource clients** for operations: `oven.Agents`, `oven.Recipes`, `oven.Providers`, `oven.Kitchens`.
3. **Handles** for one agent: `oven.Agents["name"]`.

Every call takes a `CancellationToken`.

### Recipes

```csharp
var recipe = Recipe.Create("contract-review")
    .Step("extract",   s => s.Agent("extractor"))
    .Step("summarize", s => s.Agent("summarizer").DependsOn("extract").Timeout(TimeSpan.FromMinutes(2)).Retry(2))
    .Step("risk",      s => s.Agent("risk-scorer").DependsOn("extract"))           // runs in parallel with summarize
    .Step("approve",   s => s.HumanGate(approvers: ["legal@corp.com"]).DependsOn("summarize", "risk"))
    .Build();                                                                       // validates dependencies

await oven.Recipes.CreateAsync(recipe);
var started = await oven.Recipes.BakeAsync("contract-review", new { contractId = 42 });
var run = await oven.Recipes.WaitForCompletionAsync(started);                       // also returns when paused on a gate
if (run.Status == RecipeRunStatus.Paused)
    await oven.Recipes.ApproveGateAsync("contract-review", run.Id, run.PendingGates[0]);
```

### Kitchens, errors, streaming

```csharp
var billing = oven.WithKitchen("billing");          // same connection pool, different X-Kitchen

try { await oven.Agents.GetAsync("nope"); }
catch (AgentOvenApiException ex) when (ex.IsNotFound) { /* ex.StatusCode, ex.Error, ex.Detail, ex.RequestId */ }

await foreach (var evt in summarizer.InvokeStreamingAsync("Summarize ..."))   // token, tool_call, tool_result, error, done
    Console.Write(evt.Text);

await foreach (var line in summarizer.StreamLogsAsync(ct))                    // the agent process's stdout/stderr
    Console.WriteLine(line);
```

### Dependency injection

```csharp
// appsettings.json: { "AgentOven": { "Url": "https://oven.example.com", "ApiKey": "...", "Kitchen": "payments" } }
builder.Services.AddAgentOven(builder.Configuration.GetSection("AgentOven"))
    .AddStandardResilienceHandler();               // optional: Microsoft.Extensions.Http.Resilience

public sealed class Summaries(AgentOvenClient oven) { ... }
```

## Runtime: agents written in .NET

Register the agent with an entrypoint. At bake time, the control plane starts that process with `AGENT_PORT`,
`AGENT_NAME`, the resolved model, the MCP tools and other settings in its environment:

```csharp
await oven.Agents.RegisterAsync(Agent.Create("support")
    .UseModel("gpt-4o", provider: "openai")        // required: the control plane needs a model ingredient to bake
    .WithSystemPrompt("You help {{customer}} with orders.")
    .AddTool("order-lookup")
    .RunsOwnProcess("dotnet Support.dll")
    .Build());
```

The process can be anything from a one-liner to a full framework agent:

```csharp
// Smallest possible agent
await AgentOvenServer.RunAsync(async (message, ct) => $"echo: {message}");
```

```csharp
// Tool-calling agent on Microsoft.Extensions.AI: the injected model, MCP tools, a max-turns loop, streaming
var builder = WebApplication.CreateBuilder(args);
builder.AddAgentOvenRuntime();
builder.AddAgentOvenChatClient();                  // OpenAI, Azure OpenAI, Anthropic, Ollama, Groq, OpenRouter, Gemini, …
var app = builder.Build();
app.MapAgentOven<ChatClientAgentHandler>();
app.Run();
```

```csharp
// Microsoft Agent Framework
builder.AddAgentOvenRuntime();
builder.AddAgentOvenChatClient();
builder.AddAgentOvenAIAgent();                     // a ChatClientAgent with the agent's name and MCP tools
app.MapAgentOvenAIAgent();                         // or app.MapAgentOven(myOwnAIAgent)
```

```csharp
// Semantic Kernel
builder.AddAgentOvenRuntime();
builder.AddAgentOvenChatClient();
builder.AddAgentOvenKernel(k => k.Plugins.AddFromType<MyPlugin>());   // MCP tools arrive as the "agentoven" plugin
app.MapAgentOvenKernel();                          // or app.MapAgentOven(myChatCompletionAgent)
```

```csharp
// Your own handler, with DI
public sealed class SupportAgent(IChatClient chat, AgentOvenRuntime runtime, AgentTools tools) : IAgentHandler
{
    public async Task<AgentReply> InvokeAsync(AgentRequest request, CancellationToken ct) { ... }
    // Optional: override StreamAsync to stream tokens; by default the answer is sent as one token.
}
app.MapAgentOven<SupportAgent>();
```

The runtime implements the same process contract as the control plane's built-in runner:

| Endpoint | |
|---|---|
| `GET /health`, `GET /status` | health check used by the local, Docker and Kubernetes executors |
| `GET /.well-known/agent-card.json`, `GET /agent-card` | A2A agent card |
| `POST /invoke` | `{message, variables, trace_id, max_turns}` → `{response, usage}` |
| `POST /invoke/stream` | SSE `data: {"type": "token" \| "tool_call" \| "tool_result" \| "error" \| "done", …}` |
| `POST /`, `POST /a2a` | A2A JSON-RPC `tasks/send`, `tasks/get`, `tasks/cancel` |
| stdout `AGENT_READY` | printed once Kestrel is listening on `AGENT_PORT` |

MCP tools are called the same way the built-in runner calls them: JSON-RPC `tools/call`, falling back to REST
`/call`. Orchestrator agents, meaning those with no tools of their own, also get `agentoven_delegate`, which calls
other agents in the kitchen with the injected service token.

## Building

```bash
dotnet build
dotnet test                         # xunit v3 on Microsoft.Testing.Platform (see global.json)
dotnet pack -c Release -o artifacts
```

`samples/` has a client quick start and four agent processes: echo, Microsoft.Extensions.AI, Agent Framework and
Semantic Kernel. The API design and the reasoning behind it are in
[docs/design/0001-api-surface.md](docs/design/0001-api-surface.md).

## License

Apache-2.0. "AgentOven" is a trademark of Techdwarfs Digital Solutions LLP.
