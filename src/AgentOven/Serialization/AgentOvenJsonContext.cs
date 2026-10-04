using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace AgentOven;

/// <summary>
/// Source-generated JSON metadata for every AgentOven model (snake_case, nulls omitted).
/// Use it to serialize models yourself without reflection, for example
/// <c>JsonSerializer.Serialize(agent, AgentOvenJsonContext.Default.Agent)</c>.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(Agent))]
[JsonSerializable(typeof(IReadOnlyList<Agent>))]
[JsonSerializable(typeof(AgentCard))]
[JsonSerializable(typeof(AgentConfig))]
[JsonSerializable(typeof(AgentStatusResult))]
[JsonSerializable(typeof(AgentTestResult))]
[JsonSerializable(typeof(BakeOptions))]
[JsonSerializable(typeof(BakeResult))]
[JsonSerializable(typeof(RecookOptions))]
[JsonSerializable(typeof(InvokeResult))]
[JsonSerializable(typeof(ProcessInfo))]
[JsonSerializable(typeof(LogEntry))]
[JsonSerializable(typeof(IReadOnlyList<LogEntry>))]
[JsonSerializable(typeof(TokenUsage))]
[JsonSerializable(typeof(ResolvedIngredients))]
[JsonSerializable(typeof(IReadOnlyList<ResolvedTool>))]
[JsonSerializable(typeof(IReadOnlyList<ResolvedDataSource>))]
[JsonSerializable(typeof(Recipe))]
[JsonSerializable(typeof(IReadOnlyList<Recipe>))]
[JsonSerializable(typeof(RecipeRun))]
[JsonSerializable(typeof(IReadOnlyList<RecipeRun>))]
[JsonSerializable(typeof(RecipeRunStarted))]
[JsonSerializable(typeof(ModelProvider))]
[JsonSerializable(typeof(IReadOnlyList<ModelProvider>))]
[JsonSerializable(typeof(ProviderUpdateResult))]
[JsonSerializable(typeof(ProviderTestResult))]
[JsonSerializable(typeof(ProviderDiscoveryResult))]
[JsonSerializable(typeof(IReadOnlyList<ProviderTemplate>))]
[JsonSerializable(typeof(Session))]
[JsonSerializable(typeof(IReadOnlyList<Session>))]
[JsonSerializable(typeof(SessionOptions))]
[JsonSerializable(typeof(SessionReply))]
[JsonSerializable(typeof(Kitchen))]
[JsonSerializable(typeof(IReadOnlyList<Kitchen>))]
[JsonSerializable(typeof(ServerInfo))]
[JsonSerializable(typeof(JsonObject))]
[JsonSerializable(typeof(JsonElement))]
public sealed partial class AgentOvenJsonContext : JsonSerializerContext;
