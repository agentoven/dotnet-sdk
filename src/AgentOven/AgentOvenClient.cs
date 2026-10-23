using AgentOven.Internal;

namespace AgentOven;

/// <summary>
/// Client for the AgentOven control plane. Thread-safe; create one and reuse it.
/// </summary>
/// <example>
/// <code>
/// var oven = new AgentOvenClient();   // AGENTOVEN_URL / AGENTOVEN_API_KEY / AGENTOVEN_KITCHEN, else localhost
///
/// await oven.Agents.RegisterAsync(Agent.Create("summarizer").UseModel("gpt-4o", "openai").Build());
/// await oven.Agents["summarizer"].BakeAndWaitAsync();
/// var result = await oven.Agents["summarizer"].InvokeAsync("Summarize: ...");
/// </code>
/// </example>
public sealed partial class AgentOvenClient : IDisposable
{
    private readonly AgentOvenHttp _http;
    private readonly HttpClient? _ownedHttpClient;

    /// <summary>Creates a client from <c>AGENTOVEN_*</c> environment variables, falling back to <c>http://localhost:8080</c>.</summary>
    public AgentOvenClient()
        : this(new AgentOvenClientOptions())
    {
    }

    /// <summary>Creates a client for a URL.</summary>
    public AgentOvenClient(Uri url, string? apiKey = null, string? kitchen = null)
        : this(new AgentOvenClientOptions { Url = url, ApiKey = apiKey, Kitchen = kitchen })
    {
    }

    /// <summary>
    /// Creates a client. When <paramref name="httpClient"/> is <see langword="null"/>, the client creates and owns one.
    /// Pass your own (for example from <c>IHttpClientFactory</c>) to control handlers, retries and lifetime.
    /// </summary>
    public AgentOvenClient(AgentOvenClientOptions options, HttpClient? httpClient = null)
        : this(Create(options, httpClient))
    {
    }

    private AgentOvenClient((AgentOvenHttp Http, HttpClient? Owned) parts)
        : this(parts.Http)
    {
        _ownedHttpClient = parts.Owned;
    }

    private AgentOvenClient(AgentOvenHttp http)
    {
        _http = http;
        Agents = new AgentsClient(http);
        Recipes = new RecipesClient(http);
        Providers = new ProvidersClient(http);
        Kitchens = new KitchensClient(http);
        Tools = new ToolsClient(http);
        Prompts = new PromptsClient(http);
        Skills = new SkillsClient(http);
        Rag = new RagClient(http);
        Traces = new TracesClient(http);
        Audit = new AuditClient(http);
        Keys = new KeysClient(http);
        Approvals = new ApprovalsClient(http);
        InitializePro(http);
    }

    /// <summary>Control plane base URL.</summary>
    public Uri Url => _http.BaseUri;

    /// <summary>Active kitchen.</summary>
    public string Kitchen => _http.Kitchen;

    /// <summary>Agents: register, bake, invoke, sessions, …</summary>
    public AgentsClient Agents { get; }

    /// <summary>Recipes (workflows) and their runs.</summary>
    public RecipesClient Recipes { get; }

    /// <summary>Model providers.</summary>
    public ProvidersClient Providers { get; }

    /// <summary>Kitchens (workspaces).</summary>
    public KitchensClient Kitchens { get; }

    /// <summary>The MCP tool catalog.</summary>
    public ToolsClient Tools { get; }

    /// <summary>The versioned prompt store.</summary>
    public PromptsClient Prompts { get; }

    /// <summary>The skill registry.</summary>
    public SkillsClient Skills { get; }

    /// <summary>The knowledge base.</summary>
    public RagClient Rag { get; }

    /// <summary>Recorded invocations and spans.</summary>
    public TracesClient Traces { get; }

    /// <summary>The audit log.</summary>
    public AuditClient Audit { get; }

    /// <summary>Scoped API keys.</summary>
    public KeysClient Keys { get; }

    /// <summary>Approvals for recipes' human gates.</summary>
    public ApprovalsClient Approvals { get; }

    /// <summary>Gets the kitchen's settings.</summary>
    public Task<KitchenSettings> GetSettingsAsync(CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("settings"), AgentOvenJsonContext.Default.KitchenSettings, cancellationToken);

    /// <summary>Updates the kitchen's settings.</summary>
    public Task<KitchenSettings> UpdateSettingsAsync(KitchenSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return _http.PutAsync(ApiPath.Of("settings"), settings, AgentOvenJsonContext.Default.KitchenSettings,
            AgentOvenJsonContext.Default.KitchenSettings, cancellationToken);
    }

    /// <summary>
    /// Returns a client bound to another kitchen. It shares this client's connection pool, so it is cheap;
    /// it stays usable only while this client is not disposed.
    /// </summary>
    public AgentOvenClient WithKitchen(string kitchen)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kitchen);
        return new AgentOvenClient(_http.WithKitchen(kitchen));
    }

    /// <summary>Gets the server's edition, plan, features and limits.</summary>
    public Task<ServerInfo> GetServerInfoAsync(CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("info"), AgentOvenJsonContext.Default.ServerInfo, cancellationToken);

    /// <summary>Gets the server version (no auth needed).</summary>
    public async Task<string?> GetServerVersionAsync(CancellationToken cancellationToken = default) =>
        (await _http.GetAsync("version", InternalJsonContext.Default.ServerHealth, cancellationToken).ConfigureAwait(false)).Version;

    /// <summary>Returns <see langword="true"/> when the control plane answers its health check.</summary>
    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var health = await _http.GetAsync("health", InternalJsonContext.Default.ServerHealth, cancellationToken).ConfigureAwait(false);
            return string.Equals(health.Status, "healthy", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is HttpRequestException or AgentOvenException or System.Text.Json.JsonException)
        {
            return false;
        }
    }

    /// <summary>Disposes the <see cref="HttpClient"/> if this client created it.</summary>
    public void Dispose() => _ownedHttpClient?.Dispose();

    private static (AgentOvenHttp, HttpClient?) Create(AgentOvenClientOptions options, HttpClient? httpClient)
    {
        ArgumentNullException.ThrowIfNull(options);
        var resolved = options.Resolve();
        HttpClient? owned = null;
        if (httpClient is null)
        {
            owned = new HttpClient(new SocketsHttpHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.All,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            })
            {
                Timeout = resolved.Timeout,
            };
        }

        return (new AgentOvenHttp(httpClient ?? owned!, resolved), owned);
    }
}
