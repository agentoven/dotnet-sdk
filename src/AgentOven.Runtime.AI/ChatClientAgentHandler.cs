using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using AIChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace AgentOven.Runtime.AI;

/// <summary>
/// A complete tool-calling agent on top of any <see cref="IChatClient"/>: renders the system prompt with the
/// request's variables, offers the agent's MCP tools, runs the function-calling loop up to the agent's
/// max turns, and streams tokens, tool calls and tool results.
/// </summary>
/// <example>
/// <code>
/// var builder = WebApplication.CreateBuilder(args);
/// builder.AddAgentOvenRuntime().AddAgentOvenChatClient();
/// var app = builder.Build();
/// app.MapAgentOven&lt;ChatClientAgentHandler&gt;();
/// app.Run();
/// </code>
/// </example>
public class ChatClientAgentHandler : IAgentHandler
{
    private readonly IChatClient _chatClient;
    private readonly AgentOvenRuntime _runtime;
    private readonly IList<AITool> _tools;

    /// <summary>Creates the handler. <paramref name="chatClient"/> should not already perform function invocation.</summary>
    public ChatClientAgentHandler(IChatClient chatClient, AgentOvenRuntime runtime, AgentTools tools)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(tools);
        _chatClient = chatClient;
        _runtime = runtime;
        _tools = tools.AsAITools();
    }

    /// <inheritdoc />
    public async Task<AgentReply> InvokeAsync(AgentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var client = CreateLoop(request);
        var response = await client.GetResponseAsync(BuildMessages(request), BuildOptions(), cancellationToken).ConfigureAwait(false);
        return new AgentReply(response.Text) { Usage = AIContentMapping.ToUsage(response.Usage) };
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<AgentStreamEvent> StreamAsync(AgentRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var client = CreateLoop(request);
        var mapper = new AIContentMapping();
        await foreach (var update in client.GetStreamingResponseAsync(BuildMessages(request), BuildOptions(), cancellationToken).ConfigureAwait(false))
        {
            foreach (var evt in mapper.Map(update.Contents))
            {
                yield return evt;
            }
        }

        yield return new DoneStreamEvent(mapper.Usage);
    }

    /// <summary>The system message and the user's message. Override to add history or retrieved context.</summary>
    protected virtual IList<AIChatMessage> BuildMessages(AgentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var messages = new List<AIChatMessage>();
        var system = _runtime.RenderSystemPrompt(request.Variables);
        if (!string.IsNullOrWhiteSpace(system))
        {
            messages.Add(new AIChatMessage(ChatRole.System, system));
        }

        messages.Add(new AIChatMessage(ChatRole.User, request.Message));
        return messages;
    }

    /// <summary>Chat options: the agent's tools, temperature and token limit. Override to customise.</summary>
    protected virtual ChatOptions BuildOptions()
    {
        var options = new ChatOptions
        {
            Temperature = _runtime.Temperature is { } t ? (float)t : null,
            MaxOutputTokens = _runtime.MaxTokens,
        };
        if (_tools.Count > 0)
        {
            options.Tools = _tools;
        }

        return options;
    }

    // Not disposed: disposing a delegating client disposes the inner (shared) client too.
    private FunctionInvokingChatClient CreateLoop(AgentRequest request) =>
        new(_chatClient)
        {
            MaximumIterationsPerRequest = request.MaxTurns ?? _runtime.MaxTurns,
            IncludeDetailedErrors = true,
        };
}
