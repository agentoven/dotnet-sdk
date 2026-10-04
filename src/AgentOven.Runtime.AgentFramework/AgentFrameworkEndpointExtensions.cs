using AgentOven.Runtime;
using AgentOven.Runtime.AgentFramework;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Builder;

/// <summary>Hosting helpers for Microsoft Agent Framework agents.</summary>
public static class AgentFrameworkEndpointExtensions
{
    /// <summary>
    /// Registers an <see cref="AIAgent"/> built from the injected configuration: the registered <see cref="IChatClient"/>
    /// (see <c>AddAgentOvenChatClient</c>), the agent's MCP tools and its name. Map it with <see cref="MapAgentOvenAIAgent"/>.
    /// </summary>
    public static WebApplicationBuilder AddAgentOvenAIAgent(this WebApplicationBuilder builder, Action<ChatClientAgentOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddSingleton<AIAgent>(sp => sp.GetRequiredService<AgentOvenRuntime>().CreateAIAgent(
            sp.GetRequiredService<IChatClient>(),
            sp.GetRequiredService<AgentTools>(),
            configure));
        return builder;
    }

    /// <summary>
    /// Serves the registered <see cref="AIAgent"/>. The injected system prompt is rendered per request; pass
    /// <paramref name="useInjectedSystemPrompt"/> = <see langword="false"/> if your agent has its own instructions.
    /// </summary>
    public static IEndpointConventionBuilder MapAgentOvenAIAgent(this IEndpointRouteBuilder endpoints, bool useInjectedSystemPrompt = true)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var services = endpoints.ServiceProvider;
        var handler = new AIAgentHandler(
            services.GetRequiredService<AIAgent>(),
            useInjectedSystemPrompt ? services.GetRequiredService<AgentOvenRuntime>() : null);
        return endpoints.MapAgentOven(handler);
    }

    /// <summary>Serves an <see cref="AIAgent"/> you built. It keeps its own instructions.</summary>
    public static IEndpointConventionBuilder MapAgentOven(this IEndpointRouteBuilder endpoints, AIAgent agent)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        return endpoints.MapAgentOven(new AIAgentHandler(agent));
    }
}
