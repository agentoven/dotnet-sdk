using AgentOven;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers <see cref="AgentOvenClient"/> in a service collection.</summary>
public static class AgentOvenServiceCollectionExtensions
{
    /// <summary>The named <see cref="HttpClient"/> the client uses; add handlers to it with <c>AddHttpClient(HttpClientName)</c>.</summary>
    public const string HttpClientName = "AgentOven";

    /// <summary>
    /// Registers a singleton <see cref="AgentOvenClient"/>. Unset options fall back to the
    /// <c>AGENTOVEN_URL</c>, <c>AGENTOVEN_API_KEY</c> and <c>AGENTOVEN_KITCHEN</c> environment variables.
    /// </summary>
    /// <returns>The <see cref="IHttpClientBuilder"/> of the client's HTTP pipeline, to add resilience or logging handlers.</returns>
    public static IHttpClientBuilder AddAgentOven(this IServiceCollection services, Action<AgentOvenClientOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        var options = services.AddOptions<AgentOvenClientOptions>();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        return AddClient(services);
    }

    /// <summary>
    /// Registers a singleton <see cref="AgentOvenClient"/> bound to a configuration section with
    /// <c>Url</c>, <c>ApiKey</c>, <c>ServiceToken</c>, <c>Kitchen</c> and <c>Timeout</c> keys.
    /// </summary>
    /// <example>
    /// <code>
    /// // appsettings.json: { "AgentOven": { "Url": "https://oven.example.com", "Kitchen": "payments" } }
    /// builder.Services.AddAgentOven(builder.Configuration.GetSection("AgentOven"));
    /// </code>
    /// </example>
    public static IHttpClientBuilder AddAgentOven(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddOptions<AgentOvenClientOptions>().Configure(options => Bind(configuration, options));
        return AddClient(services);
    }

    private static IHttpClientBuilder AddClient(IServiceCollection services)
    {
        var builder = services.AddHttpClient(HttpClientName);
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AgentOvenClientOptions>>().Value;
            var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName);
            http.Timeout = options.Timeout;
            return new AgentOvenClient(options, http);
        });
        return builder;
    }

    // Manual binding keeps the package trim/AOT safe (ConfigurationBinder uses reflection).
    private static void Bind(IConfiguration configuration, AgentOvenClientOptions options)
    {
        if (configuration["Url"] is { Length: > 0 } url)
        {
            options.Url = new Uri(url, UriKind.Absolute);
        }

        options.ApiKey = configuration["ApiKey"] ?? options.ApiKey;
        options.ServiceToken = configuration["ServiceToken"] ?? options.ServiceToken;
        options.Kitchen = configuration["Kitchen"] ?? options.Kitchen;
        if (configuration["Timeout"] is { Length: > 0 } timeout)
        {
            options.Timeout = TimeSpan.Parse(timeout, System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
