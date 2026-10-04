# 0001 — .NET SDK API surface (proposal)

Status: **Draft, for review.** No code has been written yet. This document
proposes the shape of the public API so we can agree on it before building.

Reference implementations it is modelled on (in `agentoven/agentoven`):

- `sdk/python/agentoven/` — the Python SDK (`client.py`, `_native.pyi`, `runtime/`)
- `crates/agentoven-core/src/` — the Rust core the Python SDK wraps (`AgentBuilder`, `AgentOvenClient`)
- `sdk/typescript/` — the TypeScript SDK

---

## 1. What the Python SDK gives us, and what we keep

The Python SDK has two halves. The .NET SDK mirrors both.

| Python | Purpose | .NET equivalent |
|---|---|---|
| `agentoven` (`AgentOvenClient`, `Agent`, `Ingredient`, `Recipe`, `Step`) | Talk to the control plane: register, bake, cool, recipes, Pro APIs | `AgentOven` package |
| `agentoven.runtime` (`AgentOvenRuntime`, `serve`, adapters) | Run *inside* a baked agent process: read `AGENT_*` env, serve `/invoke`, print `AGENT_READY` | `AgentOven.Runtime` package (ASP.NET Core) |

**Kept as-is (vocabulary and semantics):**

- The kitchen metaphor: `Bake`, `Cool`, `Rewarm`, `Recook`, `Retire`; kitchens; ingredients; recipes.
- `AgentStatus`: `Draft, Baking, Ready, Cooled, Burnt, Retired`.
- `IngredientKind`: `Model, Tool, Prompt, Data, Observability, Embedding, VectorStore, Retriever, Scenario`.
- Ingredient factories: `Ingredient.model(...)`, `Ingredient.tool(...)` → `Ingredient.Model(...)`, `Ingredient.Tool(...)`.
- Client config: `url` (default `http://localhost:8080`), `api_key`, `kitchen` (default `default`), plus env vars `AGENTOVEN_URL`, `AGENTOVEN_API_KEY`, `AGENTOVEN_KITCHEN`.
- Headers: `Authorization: Bearer …`, `X-Kitchen`, `X-Kitchen-Id`.
- `with_kitchen(...)` → `WithKitchen(...)` returning a re-scoped client.
- Error type carrying status code + detail (`AgentOvenAPIError` → `AgentOvenApiException`).
- Runtime contract: `POST /invoke`, `GET /status`, `GET /.well-known/agent-card.json`, `AGENT_READY` on stdout, `AGENTOVEN_PORT`.

**Deliberately changed (because .NET users expect it, or because Python has a wart):**

| Python today | .NET proposal | Why |
|---|---|---|
| Rust core via PyO3 + pure-Python REST for Pro | **Pure managed C#, one `HttpClient` layer** | No native binaries per RID, works with trimming/AOT, one code path instead of two. The Rust core is a thin REST client; there is no performance win to bind it. |
| Sync methods | **Async-only** (`Task<T>`, `CancellationToken` everywhere) | Idiomatic .NET for I/O. No sync-over-async wrappers. |
| ~120 flat methods on one class (`create_guardrail`, `list_schedules`, …) | **Grouped resource clients**: `client.Agents`, `client.Guardrails`, `client.Schedules`, … | Discoverable in IntelliSense; matches Azure SDK / OpenAI .NET / Stripe .NET. |
| Many methods return `str` or `dict[str, Any]` | **Typed records** for every response, with `[JsonExtensionData]` for forward-compat | Compile-time safety; new server fields never break old clients. |
| `list_agents()` silently swallows errors and falls back | Errors always surface | Silent fallbacks hide outages. |
| `target: Any` (name or `Agent`) | Overloads: `BakeAsync(string name)` and `BakeAsync(Agent agent)` | Static typing. |
| `**kwargs` | Option records (`new BakeOptions { Environment = "prod" }`) | Discoverable, versionable. |

---

## 2. Packages

| NuGet package | Contents | Depends on |
|---|---|---|
| `AgentOven` | `AgentOvenClient`, models, builders, exceptions | `System.Text.Json` (in-box on .NET 8+) |
| `AgentOven.Extensions.DependencyInjection` | `services.AddAgentOven(...)`, `IHttpClientFactory`, `IOptions` binding | `Microsoft.Extensions.Http`, `.Options` |
| `AgentOven.Runtime` | Port of `agentoven.runtime`: env config, `MapAgentOven(...)`, `AGENT_READY` | ASP.NET Core (`FrameworkReference`) |
| `AgentOven.Runtime.AI` | Adapters: `Microsoft.Extensions.AI` `IChatClient`, MCP tools as `AIFunction`s, Microsoft Agent Framework / Semantic Kernel agent → handler | `Microsoft.Extensions.AI` |

Python's LangChain / LangGraph / CrewAI adapters map to the .NET ecosystem's
equivalents: `Microsoft.Extensions.AI`, Microsoft Agent Framework and Semantic Kernel.

Target frameworks: `net8.0;net10.0` (both LTS). `netstandard2.0` for .NET
Framework is possible for the `AgentOven` package later if an enterprise user
needs it — it costs us polyfills, so not in v1.

---

## 3. The client — how fluent it is

Three levels of fluency, each used where it fits:

1. **Builders are fluent** (defining agents and recipes — a chain reads like a spec).
2. **Operations are plain async methods** on resource clients (no chaining across `await`).
3. **Handles** bind a name once, for code that does several things to one agent.

### 3.1 Creating a client

```csharp
using AgentOven;

// Zero-config: AGENTOVEN_URL / AGENTOVEN_API_KEY / AGENTOVEN_KITCHEN, else defaults
var oven = new AgentOvenClient();

// Explicit
var oven = new AgentOvenClient(new AgentOvenClientOptions
{
    Url = new Uri("https://agentoven.example.com"),
    ApiKey = "svc_...",
    Kitchen = "payments",
});

// Re-scope to another kitchen. Immutable: returns a new client sharing the same HttpClient.
var billing = oven.WithKitchen("billing");
```

With DI (ASP.NET Core, workers):

```csharp
builder.Services.AddAgentOven(builder.Configuration.GetSection("AgentOven"));
// or
builder.Services.AddAgentOven(o => { o.Url = new("http://localhost:8080"); o.Kitchen = "payments"; });

public class Summarizer(AgentOvenClient oven) { ... }
```

### 3.2 Defining an agent (fluent builder)

Mirrors Rust's `Agent::builder(...)` and Python's `Agent(..., ingredients=[...])`:

```csharp
var agent = Agent.Create("summarizer")
    .Describe("Summarizes long documents")
    .Version("1.2.0")
    .Framework(AgentFramework.Managed)
    .UseModel("gpt-4o", provider: "azure-openai")
    .WithBackupModel("claude-sonnet-4-5", provider: "anthropic")
    .WithSystemPrompt("You are a concise summarizer.")
    .WithMaxTurns(8)
    .AddIngredient(Ingredient.Tool("doc-reader", protocol: "mcp"))
    .AddIngredient(Ingredient.Data("contracts", provider: "pgvector"))
    .AddGuardrail(Guardrail.Pre("pii-detector"))
    .Tag("legal")
    .Build();
```

`Build()` returns an immutable `Agent` record, so the same definition can be
copied with `with { Version = "1.3.0" }`.

The Python-style "one-shot" form is also available for people porting code:

```csharp
var agent = new Agent("summarizer")
{
    ModelProvider = "azure-openai",
    ModelName = "gpt-4o",
    Ingredients = [Ingredient.Model("gpt-4o", provider: "azure-openai"),
                   Ingredient.Tool("doc-reader", protocol: "mcp")],
};
```

An **external** (A2A-proxied) agent:

```csharp
var ext = Agent.Create("fraud-checker")
    .External(a2aEndpoint: "https://fraud.internal/a2a")
    .Build();
```

### 3.3 Operating on agents (resource client)

```csharp
await oven.Agents.RegisterAsync(agent);
Agent       a    = await oven.Agents.GetAsync("summarizer");
IReadOnlyList<Agent> all = await oven.Agents.ListAsync();

await oven.Agents.BakeAsync("summarizer", new BakeOptions { Environment = "prod" });
await oven.Agents.CoolAsync("summarizer");
await oven.Agents.RewarmAsync("summarizer");
await oven.Agents.RetireAsync("summarizer");
await oven.Agents.DeleteAsync("summarizer");

InvokeResult r = await oven.Agents.InvokeAsync("summarizer", "Summarize this contract: ...");
Console.WriteLine(r.Response);          // also r.Trace, r.Usage, r.LatencyMs
```

Every method takes an optional `CancellationToken` as its last parameter.

Streaming uses the control plane's SSE endpoints (`POST /agents/{name}/invoke/stream`,
`GET /agents/{name}/logs`) and surfaces them as `IAsyncEnumerable<T>`. Neither
the Python nor the TypeScript SDK exposes these yet:

```csharp
await foreach (var chunk in oven.Agents.InvokeStreamingAsync("summarizer", "Summarize ..."))
    Console.Write(chunk.Text);

await foreach (var line in oven.Agents.StreamLogsAsync("summarizer", ct))
    Console.WriteLine(line);
```

### 3.4 Handles (bind a name once)

```csharp
var summarizer = oven.Agents["summarizer"];      // no network call

await summarizer.BakeAsync();
var reply = await summarizer.InvokeAsync("hello");

// Sessions — multi-turn
await using var chat = await summarizer.Sessions.StartAsync();
var r1 = await chat.SendAsync("What does clause 4 mean?");
var r2 = await chat.SendAsync("And clause 5?");
// DisposeAsync deletes the session (opt out with StartAsync(keep: true))
```

### 3.5 Recipes (workflows)

Fluent, because a recipe is a DAG spec:

```csharp
var recipe = Recipe.Create("contract-review")
    .Step("extract",   s => s.Agent("extractor"))
    .Step("summarize", s => s.Agent("summarizer").DependsOn("extract").Timeout(TimeSpan.FromMinutes(2)))
    .Step("risk",      s => s.Agent("risk-scorer").DependsOn("extract").Parallel())
    .Step("approve",   s => s.HumanGate().DependsOn("summarize", "risk").Notify("legal@corp.com"))
    .Build();

await oven.Recipes.CreateAsync(recipe);
RecipeRun run = await oven.Recipes.BakeAsync("contract-review", input: new { contractId = 42 });

// Poll to completion
run = await oven.Recipes.WaitForCompletionAsync(run, pollInterval: TimeSpan.FromSeconds(2));
```

`Timeout(TimeSpan)` is serialized to the server's string form (`"2m"`), so
callers never build duration strings by hand.

### 3.6 Pro APIs

Same client, grouped. They throw `AgentOvenApiException` (402/404) against an
OSS server; `await oven.GetServerInfoAsync()` tells you the edition up front.

```csharp
await oven.Guardrails.CreateAsync(new GuardrailPolicy
{
    Name = "content-safety",
    Kind = "llamaguard",
    Stage = GuardrailStage.Both,
    Config = new() { ["endpoint"] = "http://ollama:11434", ["model"] = "llama-guard3:1b" },
});

await oven.Schedules.CreateAsync("daily-report", cron: "0 8 * * MON-FRI", timeZone: "Europe/London");

await oven.Environments.PromoteAsync("summarizer", from: "staging", to: "prod");

var suite  = await oven.TestSuites.CreateAsync(TestSuite.For("classifier")
    .Case("refund case", input: "Refund $50", expect: "refund")
    .ScenarioCase("picks its own scenario", scenarioId: "refund-flow", minPassRate: 1)
    .Build());
var testRun = await oven.TestSuites.RunAsync(suite.Id);
```

Full grouping (one property per area the Python client covers):
`Agents, Recipes, Providers, Tools, Prompts, Kitchens, Users, Guardrails,
Environments, Deployments, Sessions, ServiceAccounts, Schedules, TestSuites,
WorldSchemas, Scenarios, Workloads, ScopedKeys, Credentials, Audit, Traces, Rag`.

### 3.7 Errors

```csharp
try { await oven.Agents.GetAsync("nope"); }
catch (AgentOvenApiException ex) when (ex.IsNotFound)
{
    // ex.StatusCode, ex.Detail (raw JSON or text), ex.RequestId
}
```

`AgentOvenException` is the base; `AgentOvenApiException` is non-2xx.
Transport failures stay as `HttpRequestException` (not wrapped), the .NET norm.

For "might not exist" lookups there is a non-throwing variant:
`Agent? a = await oven.Agents.FindAsync("summarizer");`

---

## 4. The runtime — running a .NET agent under AgentOven

Port of `agentoven.runtime`. Python uses FastAPI + uvicorn; .NET uses ASP.NET
Core minimal APIs.

```csharp
using AgentOven.Runtime;

var builder = WebApplication.CreateBuilder(args);
builder.AddAgentOvenRuntime();          // reads AGENT_* env, binds AGENTOVEN_PORT

var app = builder.Build();
app.MapAgentOven(async (string message, AgentOvenRuntime rt, CancellationToken ct) =>
{
    return $"[{rt.Name}] you said: {message}";
});
app.Run();                              // prints AGENT_READY once Kestrel is listening
```

Or a class-based handler (equivalent of Python's `InvokeHandler` protocol):

```csharp
public sealed class MyAgent : IAgentHandler
{
    public Task<AgentReply> RunAsync(AgentRequest request, CancellationToken ct) => ...;
}
app.MapAgentOven<MyAgent>();
```

`AgentOvenRuntime` exposes the same properties as Python's: `Name, Kitchen,
Runtime, Port, ModelProvider, ModelName, Temperature, MaxTokens,
SystemPrompt, PromptTemplate, Tools, DataSources, HasProToken`.

**Adapters** (`AgentOven.Runtime.AI`) — the .NET counterpart of
`build_langchain_llm()` / `build_mcp_tools()` / `LangGraphAdapter`:

```csharp
builder.AddAgentOvenRuntime()
       .AddChatClient();                 // IChatClient for AGENT_MODEL_PROVIDER/NAME (openai, azure-openai, anthropic, ollama)

app.MapAgentOven(async (string message, IChatClient chat, AgentOvenRuntime rt) =>
{
    var tools = rt.BuildMcpTools();      // IList<AIFunction>, calls the MCP gateway with the agent's API key
    var resp  = await chat.GetResponseAsync(
        [new(ChatRole.System, rt.SystemPrompt), new(ChatRole.User, message)],
        new ChatOptions { Tools = [.. tools] });
    return resp.Text;
});

// Or hand over a Microsoft Agent Framework / Semantic Kernel agent wholesale
app.MapAgentOven(AgentAdapter.From(myAgent));
```

Unlike Python, `/invoke` returns real token usage when the handler reports it
(`AgentReply.Usage`) instead of always `0`.

---

## 5. Implementation semantics

- **HTTP:** one internal `AgentOvenHttp` (wraps `HttpClient`) that every resource client uses. Base URL trailing slash normalised. Default timeout 100 s, configurable. Retries are *not* built in — users add `Microsoft.Extensions.Http.Resilience` via DI; we document the recipe.
- **Serialization:** `System.Text.Json` with a source-generated `JsonSerializerContext` (trim/AOT-safe), `snake_case` naming to match the server, `JsonStringEnumConverter` with a fallback `Unknown` member (mirrors Rust's `#[serde(other)]`).
- **Forward compatibility:** every model has `[JsonExtensionData] IDictionary<string, JsonElement>? Extra`.
- **Free-form config** (`Ingredient.Config`, guardrail config): `JsonObject` so callers can pass anonymous objects or dictionaries.
- **Immutability:** models are `record`s with `init` properties; collections exposed as `IReadOnlyList<T>`. Builders are the only mutable types.
- **Thread safety:** `AgentOvenClient` is thread-safe and meant to be a singleton. `WithKitchen` is cheap.
- **Nullability:** `<Nullable>enable</Nullable>` across the board; optional server fields are `T?`.
- **Escaping:** path segments are URL-escaped (Python currently interpolates names raw into paths and query strings).
- **Versioning:** SDK version tracks the platform (`0.8.x` today) so `AgentOven 0.8.8` ↔ `agentoven 0.8.8` ↔ `@agentoven/sdk 0.8.8`.
- **Testing:** unit tests against a stub `HttpMessageHandler`; integration tests against the control plane in `docker-compose` (CI job).

### Repository layout

```
src/
  AgentOven/                                   # core client + models
  AgentOven.Extensions.DependencyInjection/
  AgentOven.Runtime/
  AgentOven.Runtime.AI/
tests/
  AgentOven.Tests/
  AgentOven.Runtime.Tests/
  AgentOven.IntegrationTests/
samples/
  QuickStart/
  RecipeWorkflow/
  ManagedAgentRuntime/
AgentOven.sln
Directory.Build.props                          # TFMs, nullable, analyzers, package metadata
```

---

## 6. Namespace: `AgentOven` vs `Techdwarfs`

**Recommendation: root namespace and package ID `AgentOven`; put Techdwarfs in the package metadata, not the namespace.**

```xml
<PackageId>AgentOven</PackageId>
<RootNamespace>AgentOven</RootNamespace>
<Authors>Techdwarfs Digital Solutions LLP</Authors>
<Company>Techdwarfs Digital Solutions LLP</Company>
<Copyright>Copyright 2026 AgentOven Contributors</Copyright>
```

Reasoning:

- **Consistency with the other SDKs.** Users write `from agentoven import …` and `import … from '@agentoven/sdk'`. The .NET equivalent is `using AgentOven;`. `using Techdwarfs.AgentOven;` would be the only SDK that names the company.
- **The brand is the product.** The GitHub org, domain (`agentoven.dev`), PyPI and npm names are all `agentoven`. .NET libraries from single-product companies use the product name (`Serilog`, `Polly`, `Dapper`, `MediatR`, `Npgsql`). `Company.Product` (`Microsoft.*`, `Amazon.*`, `Google.Cloud.*`) is a convention for companies shipping many unrelated products.
- **Namespaces are forever.** Renaming one is a breaking change for every user. If AgentOven is ever spun out, donated to a foundation, or the company is renamed, a `Techdwarfs.*` namespace becomes wrong overnight. `AgentOven` stays right.
- **Open-source signal.** The repo is Apache-2.0 and copyrighted "AgentOven Contributors". A company prefix makes it read as a vendor-only SDK.
- **Ownership still goes to Techdwarfs where it matters:** publish from a `Techdwarfs` nuget.org organisation account and reserve the `AgentOven.*` package ID prefix there (NuGet prefix reservation gives the verified checkmark and blocks squatters). That is what actually protects the trademark — not the namespace.

When `Techdwarfs` *would* be the right root: if Techdwarfs plans to ship several
unrelated .NET libraries and wants them under one umbrella. Even then it would be
`Techdwarfs.AgentOven` (never bare `Techdwarfs` with AgentOven types directly in it),
and every type name above stays the same — only the `using` line changes.

---

## 7. Open questions for review

1. **Namespace** — `AgentOven` (recommended) or `Techdwarfs.AgentOven`?
2. **Fluency level** — are builders + resource clients + handles the right mix, or do you want the flat Python-style surface (`oven.BakeAsync("x")` on the root client) as well? (We can add flat shortcuts for the top 6 agent operations without much cost.)
3. **.NET Framework** — is `netstandard2.0` needed in v1?
4. **Runtime adapters** — which frameworks first: `Microsoft.Extensions.AI` only, or also Microsoft Agent Framework / Semantic Kernel?
5. **Scope of v1** — proposal: core agent/recipe/provider/session/kitchen operations + runtime in v1; the rest of the Pro surface in v1.1.
