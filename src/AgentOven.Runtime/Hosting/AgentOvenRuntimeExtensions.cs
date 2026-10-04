using System.Diagnostics.CodeAnalysis;
using AgentOven.Runtime;
using AgentOven.Runtime.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.AspNetCore.Builder;

/// <summary>Wires an ASP.NET Core app up as an AgentOven agent process.</summary>
public static class AgentOvenRuntimeExtensions
{
    /// <summary>
    /// Registers <see cref="AgentOvenRuntime"/> (read from the environment), <see cref="AgentTools"/> and the
    /// <c>AGENT_READY</c> signal, and listens on the port the control plane assigned.
    /// </summary>
    public static WebApplicationBuilder AddAgentOvenRuntime(this WebApplicationBuilder builder, Action<AgentOvenRuntimeOptions>? configure = null) =>
        builder.AddAgentOvenRuntime(new AgentOvenRuntime(), configure);

    /// <summary>Same as <see cref="AddAgentOvenRuntime(WebApplicationBuilder, Action{AgentOvenRuntimeOptions}?)"/> with an explicit runtime.</summary>
    public static WebApplicationBuilder AddAgentOvenRuntime(
        this WebApplicationBuilder builder, AgentOvenRuntime runtime, Action<AgentOvenRuntimeOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(runtime);
        var options = new AgentOvenRuntimeOptions();
        configure?.Invoke(options);

        builder.Services.TryAddSingleton(runtime);
        builder.Services.TryAddSingleton(options);
        builder.Services.AddHttpClient(nameof(AgentTools));
        builder.Services.TryAddSingleton(sp => new AgentTools(
            sp.GetRequiredService<AgentOvenRuntime>(),
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(AgentTools))));
        builder.Services.AddHostedService<ReadySignalService>();

        if (options.BindPort && (runtime.IsPortAssigned || !HasConfiguredUrls(builder)))
        {
            builder.WebHost.UseUrls($"http://0.0.0.0:{runtime.Port}");
        }

        return builder;
    }

    /// <summary>
    /// Maps the agent endpoints (<c>/invoke</c>, <c>/invoke/stream</c>, <c>/health</c>, <c>/status</c>, the agent card,
    /// and A2A on <c>/</c> and <c>/a2a</c>) to a handler resolved from DI per request.
    /// </summary>
    /// <returns>A builder for the invoke and A2A endpoints (health and the card stay anonymous).</returns>
    public static IEndpointConventionBuilder MapAgentOven<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(
        this IEndpointRouteBuilder endpoints)
        where THandler : class, IAgentHandler =>
        endpoints.MapAgentOven(context => ActivatorUtilities.GetServiceOrCreateInstance<THandler>(context.RequestServices));

    /// <summary>Maps the agent endpoints to a handler instance.</summary>
    public static IEndpointConventionBuilder MapAgentOven(this IEndpointRouteBuilder endpoints, IAgentHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return endpoints.MapAgentOven(_ => handler);
    }

    /// <summary>Maps the agent endpoints to a function from message to answer.</summary>
    /// <example><code>app.MapAgentOven(async (message, ct) => $"You said: {message}");</code></example>
    public static IEndpointConventionBuilder MapAgentOven(this IEndpointRouteBuilder endpoints, Func<string, CancellationToken, Task<string>> handler) =>
        endpoints.MapAgentOven(AgentHandler.From(handler));

    /// <summary>
    /// Maps the agent endpoints to a function that gets the full request and the request's services
    /// (resolve an <c>IChatClient</c>, <see cref="AgentTools"/>, … from them).
    /// </summary>
    public static IEndpointConventionBuilder MapAgentOven(
        this IEndpointRouteBuilder endpoints, Func<AgentRequest, IServiceProvider, CancellationToken, Task<AgentReply>> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return endpoints.MapAgentOven(context => new ScopedDelegateHandler(handler, context.RequestServices));
    }

    private static CompositeConventionBuilder MapAgentOven(this IEndpointRouteBuilder endpoints, Func<HttpContext, IAgentHandler> resolveHandler)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        if (endpoints.ServiceProvider.GetService<AgentOvenRuntime>() is null)
        {
            throw new InvalidOperationException("Call builder.AddAgentOvenRuntime() before app.MapAgentOven().");
        }

        var agent = new AgentEndpoints(resolveHandler);
        var a2a = new A2AEndpoint(resolveHandler);

        endpoints.MapGet("/health", AgentEndpoints.HealthAsync);
        endpoints.MapGet("/status", AgentEndpoints.HealthAsync);
        endpoints.MapGet("/.well-known/agent-card.json", AgentEndpoints.AgentCardAsync);
        endpoints.MapGet("/agent-card", AgentEndpoints.AgentCardAsync);

        return new CompositeConventionBuilder(
        [
            endpoints.MapPost("/invoke", agent.InvokeAsync),
            endpoints.MapPost("/invoke/stream", agent.StreamAsync),
            endpoints.MapPost("/a2a", a2a.HandleAsync),
            endpoints.MapPost("/", a2a.HandleAsync),
        ]);
    }

    private static bool HasConfiguredUrls(WebApplicationBuilder builder) =>
        !string.IsNullOrEmpty(builder.Configuration["urls"])
        || !string.IsNullOrEmpty(builder.Configuration["http_ports"])
        || !string.IsNullOrEmpty(builder.Configuration["https_ports"]);

    private sealed class ScopedDelegateHandler(
        Func<AgentRequest, IServiceProvider, CancellationToken, Task<AgentReply>> handler, IServiceProvider services) : IAgentHandler
    {
        public Task<AgentReply> InvokeAsync(AgentRequest request, CancellationToken cancellationToken) => handler(request, services, cancellationToken);
    }

    private sealed class CompositeConventionBuilder(IReadOnlyList<IEndpointConventionBuilder> builders) : IEndpointConventionBuilder
    {
        public void Add(Action<EndpointBuilder> convention)
        {
            foreach (var builder in builders)
            {
                builder.Add(convention);
            }
        }

        public void Finally(Action<EndpointBuilder> finallyConvention)
        {
            foreach (var builder in builders)
            {
                builder.Finally(finallyConvention);
            }
        }
    }
}
