namespace AgentOven;

/// <summary>
/// Connection settings for <see cref="AgentOvenClient"/>.
/// </summary>
/// <remarks>
/// Every property falls back to an environment variable when left unset, using the
/// same names as the CLI and the Python and Rust SDKs:
/// <c>AGENTOVEN_URL</c>, <c>AGENTOVEN_API_KEY</c> and <c>AGENTOVEN_KITCHEN</c>.
/// </remarks>
public sealed class AgentOvenClientOptions
{
    /// <summary>The URL used when neither <see cref="Url"/> nor <c>AGENTOVEN_URL</c> is set.</summary>
    public static readonly Uri DefaultUrl = new("http://localhost:8080");

    /// <summary>The kitchen used when neither <see cref="Kitchen"/> nor <c>AGENTOVEN_KITCHEN</c> is set.</summary>
    public const string DefaultKitchen = "default";

    /// <summary>Control plane base URL. Default: <c>http://localhost:8080</c>.</summary>
    public Uri? Url { get; set; }

    /// <summary>Bearer token: an API key or a service-account token.</summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Service token sent as <c>X-Service-Token</c>. The control plane injects one into
    /// baked agent processes as <c>CONTROL_PLANE_TOKEN</c> so agents can call back
    /// (for example, to delegate to another agent).
    /// </summary>
    public string? ServiceToken { get; set; }

    /// <summary>Active kitchen (workspace) slug. Default: <c>default</c>.</summary>
    public string? Kitchen { get; set; }

    /// <summary>
    /// Timeout for non-streaming requests when the client creates its own <see cref="HttpClient"/>.
    /// Default: 100 seconds. Ignored when an <see cref="HttpClient"/> is supplied.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);

    /// <summary>
    /// Resolves unset properties from the environment, then from defaults, and returns a new instance.
    /// </summary>
    internal AgentOvenClientOptions Resolve()
    {
        var url = Url;
        if (url is null && Environment.GetEnvironmentVariable("AGENTOVEN_URL") is { Length: > 0 } envUrl)
        {
            url = new Uri(envUrl, UriKind.Absolute);
        }

        return new AgentOvenClientOptions
        {
            Url = url ?? DefaultUrl,
            ApiKey = NullIfEmpty(ApiKey) ?? NullIfEmpty(Environment.GetEnvironmentVariable("AGENTOVEN_API_KEY")),
            ServiceToken = NullIfEmpty(ServiceToken),
            Kitchen = NullIfEmpty(Kitchen) ?? NullIfEmpty(Environment.GetEnvironmentVariable("AGENTOVEN_KITCHEN")) ?? DefaultKitchen,
            Timeout = Timeout,
        };
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
