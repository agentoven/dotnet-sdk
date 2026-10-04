using AgentOven.Runtime;
using AgentOven.Runtime.SemanticKernel;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using SKAgent = Microsoft.SemanticKernel.Agents.Agent;

namespace Microsoft.AspNetCore.Builder;

/// <summary>Hosting helpers for Semantic Kernel agents.</summary>
public static class SemanticKernelEndpointExtensions
{
    /// <summary>
    /// Registers a <see cref="Kernel"/> built from the injected configuration: the registered <see cref="IChatClient"/>
    /// (see <c>AddAgentOvenChatClient</c>) and the agent's MCP tools. Map it with <see cref="MapAgentOvenKernel"/>.
    /// </summary>
    public static WebApplicationBuilder AddAgentOvenKernel(this WebApplicationBuilder builder, Action<Kernel>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddSingleton(sp =>
        {
            var kernel = sp.GetRequiredService<AgentOvenRuntime>().CreateKernel(sp.GetRequiredService<IChatClient>(), sp.GetRequiredService<AgentTools>());
            configure?.Invoke(kernel);
            return kernel;
        });
        return builder;
    }

    /// <summary>Serves the registered <see cref="Kernel"/> with automatic function calling.</summary>
    public static IEndpointConventionBuilder MapAgentOvenKernel(this IEndpointRouteBuilder endpoints, PromptExecutionSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var services = endpoints.ServiceProvider;
        return endpoints.MapAgentOven(new KernelAgentHandler(
            services.GetRequiredService<Kernel>(), services.GetRequiredService<AgentOvenRuntime>(), settings));
    }

    /// <summary>Serves a Semantic Kernel <see cref="SKAgent"/> (for example a <see cref="ChatCompletionAgent"/>).</summary>
    public static IEndpointConventionBuilder MapAgentOven(this IEndpointRouteBuilder endpoints, SKAgent agent)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        return endpoints.MapAgentOven(new SemanticKernelAgentHandler(agent));
    }
}
