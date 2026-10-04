using System.Globalization;
using System.Text.Json;

namespace AgentOven.Runtime;

/// <summary>
/// The configuration the AgentOven control plane injects into an agent process at bake time,
/// read from environment variables.
/// </summary>
/// <remarks>
/// Variable names follow the control plane's process manager (<c>internal/process/manager.go</c>),
/// with the names the Python SDK reads accepted as fallbacks:
/// <list type="table">
/// <item><term><c>AGENT_NAME</c>, <c>AGENT_KITCHEN</c></term><description>identity</description></item>
/// <item><term><c>AGENT_PORT</c> (or <c>AGENTOVEN_PORT</c>)</term><description>port to listen on, default 9000</description></item>
/// <item><term><c>AGENT_MODEL_PROVIDER</c>, <c>AGENT_MODEL_NAME</c>, <c>AGENT_API_KEY</c>, <c>AGENT_API_ENDPOINT</c></term><description>the resolved model</description></item>
/// <item><term><c>AGENT_SYSTEM_PROMPT</c>, <c>AGENT_PROMPT_TEMPLATE</c>, <c>AGENT_DESCRIPTION</c></term><description>instructions, first set wins</description></item>
/// <item><term><c>AGENT_TOOLS_JSON</c>, <c>AGENT_DATA_SOURCES_JSON</c></term><description>resolved MCP tools and data sources</description></item>
/// <item><term><c>AGENT_MAX_TURNS</c>, <c>AGENT_SKILLS</c>, <c>AGENT_RUNTIME</c></term><description>loop limit, skills, runtime label</description></item>
/// <item><term><c>AGENTOVEN_CONTROL_PLANE_URL</c>, <c>CONTROL_PLANE_TOKEN</c></term><description>for calling back (delegation)</description></item>
/// </list>
/// </remarks>
public sealed class AgentOvenRuntime
{
    /// <summary>Port used when no port variable is set (the control plane's container port).</summary>
    public const int DefaultPort = 9000;

    /// <summary>Loop limit used when <c>AGENT_MAX_TURNS</c> is not set.</summary>
    public const int DefaultMaxTurns = 10;

    /// <summary>Reads the current process environment.</summary>
    public AgentOvenRuntime()
        : this(Environment.GetEnvironmentVariable)
    {
    }

    /// <summary>Reads configuration through <paramref name="getVariable"/> (useful in tests).</summary>
    public AgentOvenRuntime(Func<string, string?> getVariable)
    {
        ArgumentNullException.ThrowIfNull(getVariable);
        string? Get(string name) => getVariable(name) is { Length: > 0 } value ? value : null;

        Name = Get("AGENT_NAME") ?? "unnamed-agent";
        Kitchen = Get("AGENT_KITCHEN") ?? AgentOvenClientOptions.DefaultKitchen;
        RuntimeLabel = Get("AGENT_RUNTIME") ?? "custom";
        Description = Get("AGENT_DESCRIPTION");
        SystemPrompt = Get("AGENT_SYSTEM_PROMPT") ?? Get("AGENT_PROMPT_TEMPLATE") ?? Get("AGENT_DESCRIPTION") ?? "";
        PromptTemplate = Get("AGENT_PROMPT_TEMPLATE");

        var portText = Get("AGENT_PORT") ?? Get("AGENTOVEN_PORT");
        IsPortAssigned = portText is not null;
        Port = int.TryParse(portText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port) ? port : DefaultPort;

        ModelProvider = Get("AGENT_MODEL_PROVIDER") ?? "";
        ModelKind = Get("AGENT_MODEL_KIND") ?? InferModelKind(ModelProvider);
        ModelName = Get("AGENT_MODEL_NAME") ?? "";
        ModelApiKey = Get("AGENT_API_KEY");
        ModelEndpoint = Get("AGENT_API_ENDPOINT");
        Temperature = double.TryParse(Get("AGENT_MODEL_TEMPERATURE"), NumberStyles.Float, CultureInfo.InvariantCulture, out var t) ? t : null;
        MaxTokens = int.TryParse(Get("AGENT_MAX_TOKENS"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var mt) ? mt : null;
        MaxTurns = int.TryParse(Get("AGENT_MAX_TURNS"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var turns) && turns > 0
            ? turns
            : DefaultMaxTurns;
        Skills = (Get("AGENT_SKILLS") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Tools = ParseList(Get("AGENT_TOOLS_JSON"), AgentOvenJsonContext.Default.IReadOnlyListResolvedTool, "AGENT_TOOLS_JSON");
        DataSources = ParseList(Get("AGENT_DATA_SOURCES_JSON"), AgentOvenJsonContext.Default.IReadOnlyListResolvedDataSource, "AGENT_DATA_SOURCES_JSON");

        ControlPlaneUrl = Uri.TryCreate(Get("AGENTOVEN_CONTROL_PLANE_URL"), UriKind.Absolute, out var cp) ? cp : null;
        ControlPlaneToken = Get("CONTROL_PLANE_TOKEN");
        ControlPlaneApiKey = Get("AGENTOVEN_API_KEY");
        HasProToken = Get("AGENTOVEN_PRO_TOKEN") is not null;
    }

    /// <summary>Agent name.</summary>
    public string Name { get; }

    /// <summary>Kitchen.</summary>
    public string Kitchen { get; }

    /// <summary>Runtime label from <c>AGENT_RUNTIME</c> (for example <c>custom</c>).</summary>
    public string RuntimeLabel { get; }

    /// <summary>The agent's description. When the agent has a prompt ingredient, the control plane puts the prompt here.</summary>
    public string? Description { get; }

    /// <summary>System prompt, with <c>{{variable}}</c> placeholders. Render it with <see cref="RenderSystemPrompt"/>.</summary>
    public string SystemPrompt { get; }

    /// <summary>Raw prompt template, when the agent has a prompt ingredient.</summary>
    public string? PromptTemplate { get; }

    /// <summary>Port to listen on.</summary>
    public int Port { get; }

    /// <summary><see langword="true"/> when the control plane assigned the port (the process is running under AgentOven).</summary>
    public bool IsPortAssigned { get; }

    /// <summary>
    /// Name of the provider as registered with the control plane (for example <c>prod-openai</c>).
    /// Note: the control plane injects the provider's <em>name</em> here, not its kind; see <see cref="ModelKind"/>.
    /// </summary>
    public string ModelProvider { get; }

    /// <summary>
    /// Provider kind (openai, azure-openai, anthropic, ollama, …): <c>AGENT_MODEL_KIND</c> when set, otherwise inferred
    /// from <see cref="ModelProvider"/> (an exact kind, or a name containing one, such as <c>team-anthropic</c>).
    /// <see langword="null"/> when it cannot be inferred.
    /// </summary>
    public string? ModelKind { get; }

    /// <summary>Model name.</summary>
    public string ModelName { get; }

    /// <summary>Provider API key.</summary>
    public string? ModelApiKey { get; }

    /// <summary>Provider endpoint override.</summary>
    public string? ModelEndpoint { get; }

    /// <summary>Sampling temperature, if configured.</summary>
    public double? Temperature { get; }

    /// <summary>Output token limit, if configured.</summary>
    public int? MaxTokens { get; }

    /// <summary>Agentic-loop limit (default 10).</summary>
    public int MaxTurns { get; }

    /// <summary>Skills advertised on the agent card.</summary>
    public IReadOnlyList<string> Skills { get; }

    /// <summary>MCP tools resolved at bake time.</summary>
    public IReadOnlyList<ResolvedTool> Tools { get; }

    /// <summary>Data sources resolved at bake time.</summary>
    public IReadOnlyList<ResolvedDataSource> DataSources { get; }

    /// <summary>Control plane URL, for callbacks.</summary>
    public Uri? ControlPlaneUrl { get; }

    /// <summary>Service token (<c>X-Service-Token</c>) for callbacks.</summary>
    public string? ControlPlaneToken { get; }

    /// <summary>API key for callbacks, if one was provided instead of a service token.</summary>
    public string? ControlPlaneApiKey { get; }

    /// <summary>Whether the process runs under AgentOven Pro.</summary>
    public bool HasProToken { get; }

    /// <summary><c>provider/model</c>, as shown on the status endpoint.</summary>
    public string ModelId => $"{ModelProvider}/{ModelName}";

    /// <summary>Replaces <c>{{name}}</c> placeholders in <see cref="SystemPrompt"/>.</summary>
    public string RenderSystemPrompt(IReadOnlyDictionary<string, string>? variables)
    {
        var text = SystemPrompt;
        if (variables is null)
        {
            return text;
        }

        foreach (var (key, value) in variables)
        {
            text = text.Replace("{{" + key + "}}", value, StringComparison.Ordinal);
        }

        return text;
    }

    /// <summary>
    /// Creates a client for the control plane that authenticates as this agent (service token) in its kitchen.
    /// Throws when the control plane URL was not injected.
    /// </summary>
    public AgentOvenClient CreateControlPlaneClient(HttpClient? httpClient = null)
    {
        var url = ControlPlaneUrl ?? throw new InvalidOperationException(
            "AGENTOVEN_CONTROL_PLANE_URL is not set; the control plane injects it when it starts the agent.");
        return new AgentOvenClient(
            new AgentOvenClientOptions { Url = url, Kitchen = Kitchen, ServiceToken = ControlPlaneToken, ApiKey = ControlPlaneApiKey },
            httpClient);
    }

    // Ordered: "azure" before "openai" so "azure-openai-prod" is Azure.
    private static readonly (string Token, string Kind)[] KindHints =
    [
        ("azure", "azure-openai"), ("anthropic", "anthropic"), ("claude", "anthropic"), ("ollama", "ollama"), ("groq", "groq"),
        ("openrouter", "openrouter"), ("gemini", "gemini"), ("litellm", "litellm"), ("openai", "openai"),
    ];

    internal static string? InferModelKind(string provider)
    {
        if (provider.Length == 0)
        {
            return null;
        }

        foreach (var (token, kind) in KindHints)
        {
            if (provider.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return kind;
            }
        }

        return null;
    }

    private static IReadOnlyList<T> ParseList<T>(string? json, System.Text.Json.Serialization.Metadata.JsonTypeInfo<IReadOnlyList<T>> type, string variable)
    {
        if (json is null)
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize(json, type) ?? [];
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"[agentoven] WARNING: could not parse {variable}: {ex.Message}");
            return [];
        }
    }
}
