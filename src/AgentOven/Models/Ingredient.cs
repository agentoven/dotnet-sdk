using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace AgentOven;

/// <summary>
/// Something an agent is made from: a model, a tool, a prompt, a data source, …
/// Use the factory methods (<see cref="Model"/>, <see cref="Tool"/>, <see cref="Prompt"/>, …),
/// which fill in the config keys the control plane's resolver reads.
/// </summary>
public sealed record Ingredient
{
    /// <summary>Creates an ingredient.</summary>
    public Ingredient(string name, IngredientKind kind)
    {
        Name = name;
        Kind = kind;
    }

    /// <summary>Server-assigned identifier.</summary>
    public string? Id { get; init; }

    /// <summary>Ingredient name. For tools and prompts this is also the catalog name to resolve.</summary>
    public string Name { get; init; }

    /// <summary>Kind.</summary>
    public IngredientKind Kind { get; init; }

    /// <summary>Kind-specific configuration.</summary>
    public JsonObject? Config { get; init; }

    /// <summary>When <see langword="true"/> (default), baking fails if this ingredient cannot be resolved.</summary>
    public bool Required { get; init; } = true;

    /// <summary>Fields this SDK version does not model.</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; set; }

    /// <summary>An LLM. <paramref name="model"/> is the model name, <paramref name="provider"/> the registered provider name.</summary>
    public static Ingredient Model(string model, string? provider = null, string? apiKey = null, string? name = null) =>
        new(name ?? model, IngredientKind.Model) { Config = Build(("model", model), ("provider", provider), ("api_key", apiKey)) };

    /// <summary>
    /// A tool from the MCP tool catalog. <paramref name="name"/> is the catalog name unless
    /// <paramref name="toolName"/> is given.
    /// </summary>
    public static Ingredient Tool(string name, string? toolName = null, bool required = true) =>
        new(name, IngredientKind.Tool) { Config = Build(("tool_name", toolName)), Required = required };

    /// <summary>An inline prompt.</summary>
    public static Ingredient Prompt(string name, string text) =>
        new(name, IngredientKind.Prompt) { Config = Build(("text", text)) };

    /// <summary>A prompt from the prompt store, optionally pinned to a version.</summary>
    public static Ingredient PromptRef(string promptName, int? version = null, string? name = null) =>
        new(name ?? promptName, IngredientKind.Prompt)
        {
            Config = Build(("prompt_name", promptName), ("version", version)),
        };

    /// <summary>A data source.</summary>
    public static Ingredient Data(string name, string uri) =>
        new(name, IngredientKind.Data) { Config = Build(("uri", uri)) };

    /// <summary>An embedding model (RAG).</summary>
    public static Ingredient Embedding(
        string name, string? provider = null, string? model = null, int? dimensions = null, int? batchSize = null, string? distanceMetric = null) =>
        new(name, IngredientKind.Embedding)
        {
            Config = Build(("provider", provider), ("model", model), ("dimensions", dimensions), ("batch_size", batchSize), ("distance_metric", distanceMetric)),
        };

    /// <summary>A vector store backend (RAG).</summary>
    public static Ingredient VectorStore(string name, string backend, string? index = null, string? @namespace = null) =>
        new(name, IngredientKind.VectorStore) { Config = Build(("backend", backend), ("index", index), ("namespace", @namespace)) };

    /// <summary>A retriever that joins an embedding and a vector store (RAG).</summary>
    public static Ingredient Retriever(
        string name, string embeddingRef, string vectorStoreRef, int? topK = null, double? scoreThreshold = null, bool? hybridSearch = null, string? strategy = null) =>
        new(name, IngredientKind.Retriever)
        {
            Config = Build(
                ("embedding_ref", embeddingRef), ("vectorstore_ref", vectorStoreRef), ("top_k", topK),
                ("score_threshold", scoreThreshold), ("hybrid_search", hybridSearch), ("strategy", strategy)),
        };

    /// <summary>An evaluation scenario the agent is run against.</summary>
    public static Ingredient Scenario(string name, string? scenario = null, int? rollouts = null, double? minPassRate = null) =>
        new(name, IngredientKind.Scenario)
        {
            Config = Build(("scenario", scenario), ("rollouts", rollouts), ("min_pass_rate", minPassRate)),
        };

    /// <summary>An observability integration.</summary>
    public static Ingredient Observability(string name, JsonObject? config = null) =>
        new(name, IngredientKind.Observability) { Config = config };

    internal static JsonObject? Build(params (string Key, object? Value)[] entries)
    {
        JsonObject? config = null;
        foreach (var (key, value) in entries)
        {
            JsonNode? node = value switch
            {
                null => null,
                string s => JsonValue.Create(s),
                int i => JsonValue.Create(i),
                double d => JsonValue.Create(d),
                bool b => JsonValue.Create(b),
                _ => throw new ArgumentException($"Unsupported config value type {value.GetType()}.", nameof(entries)),
            };
            if (node is null)
            {
                continue;
            }

            (config ??= [])[key] = node;
        }

        return config;
    }
}

/// <summary>An input/output validation rule applied when an agent is invoked.</summary>
public sealed record Guardrail
{
    /// <summary>Creates a guardrail. It is enabled unless you set <see cref="Enabled"/> to <see langword="false"/>.</summary>
    public Guardrail(GuardrailKind kind, GuardrailStage stage)
    {
        Kind = kind;
        Stage = stage;
    }

    /// <summary>A guardrail on the user's input.</summary>
    public static Guardrail Input(GuardrailKind kind, JsonObject? config = null) => new(kind, GuardrailStage.Input) { Config = config };

    /// <summary>A guardrail on the model's output.</summary>
    public static Guardrail Output(GuardrailKind kind, JsonObject? config = null) => new(kind, GuardrailStage.Output) { Config = config };

    /// <summary>A guardrail on both input and output.</summary>
    public static Guardrail Both(GuardrailKind kind, JsonObject? config = null) => new(kind, GuardrailStage.Both) { Config = config };

    /// <summary>Server-assigned identifier.</summary>
    public string? Id { get; init; }

    /// <summary>Display name.</summary>
    public string? Name { get; init; }

    /// <summary>Guardrail kind.</summary>
    public GuardrailKind Kind { get; init; }

    /// <summary>When it runs.</summary>
    public GuardrailStage Stage { get; init; }

    /// <summary>Kind-specific configuration.</summary>
    public JsonObject? Config { get; init; }

    /// <summary>
    /// Whether the guardrail runs. Defaults to <see langword="true"/>; the server treats a missing value as
    /// <see langword="false"/>, so this is always sent.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public bool Enabled { get; init; } = true;

    /// <summary>Whether an environment may override it.</summary>
    public bool? Overridable { get; init; }

    /// <summary>Evaluation order.</summary>
    public int? Priority { get; init; }

    /// <summary>Creation time.</summary>
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>Fields this SDK version does not model.</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; set; }
}
