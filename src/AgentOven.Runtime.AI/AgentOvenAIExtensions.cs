using AgentOven.Runtime;
using AgentOven.Runtime.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Builder;

/// <summary>Registers AgentOven's Microsoft.Extensions.AI integration.</summary>
public static class AgentOvenAIExtensions
{
    /// <summary>
    /// Registers an <see cref="IChatClient"/> for the model the control plane resolved for this agent
    /// (see <see cref="AgentOvenChatClients"/>). Call after <c>AddAgentOvenRuntime</c>. Add middleware
    /// (logging, OpenTelemetry, caching) through the returned <see cref="ChatClientBuilder"/>.
    /// Function invocation is not added here: <see cref="ChatClientAgentHandler"/> runs the tool loop itself.
    /// </summary>
    public static ChatClientBuilder AddAgentOvenChatClient(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.Services.AddChatClient(sp => sp.GetRequiredService<AgentOvenRuntime>().CreateChatClient());
    }

    /// <summary>
    /// Registers an <see cref="IChatClient"/> built by <paramref name="factory"/> instead, for providers or
    /// configuration the control plane does not describe.
    /// </summary>
    public static ChatClientBuilder AddAgentOvenChatClient(this WebApplicationBuilder builder, Func<AgentOvenRuntime, IChatClient> factory)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(factory);
        return builder.Services.AddChatClient(sp => factory(sp.GetRequiredService<AgentOvenRuntime>()));
    }
}
