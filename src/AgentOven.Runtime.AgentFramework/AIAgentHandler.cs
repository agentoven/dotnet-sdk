using System.Runtime.CompilerServices;
using AgentOven.Runtime.AI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using AIChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace AgentOven.Runtime.AgentFramework;

/// <summary>
/// Serves a Microsoft Agent Framework <see cref="AIAgent"/> as an AgentOven agent. Each request runs in a
/// fresh session; tokens, tool calls, tool results and usage are streamed back to the control plane.
/// </summary>
public sealed class AIAgentHandler : IAgentHandler
{
    private readonly AIAgent _agent;
    private readonly AgentOvenRuntime? _runtime;

    /// <summary>
    /// Wraps <paramref name="agent"/>. When <paramref name="runtime"/> is given, the agent's injected system prompt
    /// (rendered with the request's variables) is sent as a system message; leave it <see langword="null"/> when
    /// the agent carries its own instructions.
    /// </summary>
    public AIAgentHandler(AIAgent agent, AgentOvenRuntime? runtime = null)
    {
        ArgumentNullException.ThrowIfNull(agent);
        _agent = agent;
        _runtime = runtime;
    }

    /// <inheritdoc />
    public async Task<AgentReply> InvokeAsync(AgentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var response = await _agent.RunAsync(BuildMessages(request), session: null, options: null, cancellationToken).ConfigureAwait(false);
        return new AgentReply(response.Text) { Usage = AIContentMapping.ToUsage(response.Usage) };
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<AgentStreamEvent> StreamAsync(AgentRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var mapper = new AIContentMapping();
        await foreach (var update in _agent.RunStreamingAsync(BuildMessages(request), session: null, options: null, cancellationToken).ConfigureAwait(false))
        {
            foreach (var evt in mapper.Map(update.Contents))
            {
                yield return evt;
            }
        }

        yield return new DoneStreamEvent(mapper.Usage);
    }

    private List<AIChatMessage> BuildMessages(AgentRequest request)
    {
        var messages = new List<AIChatMessage>();
        if (_runtime?.RenderSystemPrompt(request.Variables) is { Length: > 0 } system)
        {
            messages.Add(new AIChatMessage(ChatRole.System, system));
        }

        messages.Add(new AIChatMessage(ChatRole.User, request.Message));
        return messages;
    }
}

/// <summary>Builds Agent Framework agents from the configuration the control plane injects.</summary>
public static class AgentOvenAgentFrameworkExtensions
{
    /// <summary>
    /// Creates a <see cref="ChatClientAgent"/> named after this agent, with its MCP tools (and delegation, for
    /// orchestrators). Instructions are left to <see cref="AIAgentHandler"/>, which renders the system prompt
    /// per request so <c>{{variables}}</c> work.
    /// </summary>
    public static ChatClientAgent CreateAIAgent(this AgentOvenRuntime runtime, IChatClient chatClient, AgentTools tools, Action<ChatClientAgentOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentNullException.ThrowIfNull(tools);
        var options = new ChatClientAgentOptions
        {
            Name = runtime.Name,
            Description = runtime.Description,
            ChatOptions = new ChatOptions
            {
                Tools = tools.AsAITools(),
                Temperature = runtime.Temperature is { } t ? (float)t : null,
                MaxOutputTokens = runtime.MaxTokens,
            },
        };
        configure?.Invoke(options);
        return new ChatClientAgent(chatClient, options);
    }
}
