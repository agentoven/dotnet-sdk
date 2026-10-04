using System.Runtime.CompilerServices;
using System.Text;
using AgentOven.Runtime.AI;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.ChatCompletion;
using SKAgent = Microsoft.SemanticKernel.Agents.Agent;

namespace AgentOven.Runtime.SemanticKernel;

/// <summary>
/// Serves a Semantic Kernel <see cref="Kernel"/> as an AgentOven agent: the kernel's chat completion service
/// answers with automatic function calling over the kernel's plugins (add the agent's MCP tools with
/// <see cref="AgentOvenKernelExtensions.AddAgentOvenTools"/>).
/// </summary>
public sealed class KernelAgentHandler : IAgentHandler
{
    private readonly Kernel _kernel;
    private readonly AgentOvenRuntime _runtime;
    private readonly PromptExecutionSettings _settings;

    /// <summary>Creates the handler. <paramref name="settings"/> defaults to automatic function calling.</summary>
    public KernelAgentHandler(Kernel kernel, AgentOvenRuntime runtime, PromptExecutionSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(kernel);
        ArgumentNullException.ThrowIfNull(runtime);
        _kernel = kernel;
        _runtime = runtime;
        _settings = settings ?? new PromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() };
    }

    /// <inheritdoc />
    public async Task<AgentReply> InvokeAsync(AgentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var chat = _kernel.GetRequiredService<IChatCompletionService>();
        var result = await chat.GetChatMessageContentAsync(BuildHistory(request), _settings, _kernel, cancellationToken).ConfigureAwait(false);
        return new AgentReply(result.Content ?? "") { Usage = SemanticKernelUsage.From(result.Metadata) };
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<AgentStreamEvent> StreamAsync(AgentRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var chat = _kernel.GetRequiredService<IChatCompletionService>();
        TokenUsage? usage = null;
        await foreach (var chunk in chat.GetStreamingChatMessageContentsAsync(BuildHistory(request), _settings, _kernel, cancellationToken)
                           .ConfigureAwait(false))
        {
            if (!string.IsNullOrEmpty(chunk.Content))
            {
                yield return new TokenStreamEvent(chunk.Content);
            }

            usage = SemanticKernelUsage.From(chunk.Metadata) ?? usage;
        }

        yield return new DoneStreamEvent(usage);
    }

    private ChatHistory BuildHistory(AgentRequest request)
    {
        var history = new ChatHistory();
        if (_runtime.RenderSystemPrompt(request.Variables) is { Length: > 0 } system)
        {
            history.AddSystemMessage(system);
        }

        history.AddUserMessage(request.Message);
        return history;
    }
}

/// <summary>
/// Serves any Semantic Kernel <see cref="SKAgent"/> (for example a <see cref="ChatCompletionAgent"/>) as an AgentOven agent.
/// Each request runs on a fresh thread.
/// </summary>
public sealed class SemanticKernelAgentHandler : IAgentHandler
{
    private readonly SKAgent _agent;

    /// <summary>Wraps <paramref name="agent"/>.</summary>
    public SemanticKernelAgentHandler(SKAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        _agent = agent;
    }

    /// <inheritdoc />
    public async Task<AgentReply> InvokeAsync(AgentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var text = new StringBuilder();
        TokenUsage? usage = null;
        await foreach (var item in _agent.InvokeAsync(Messages(request), thread: null, options: null, cancellationToken).ConfigureAwait(false))
        {
            if (!string.IsNullOrEmpty(item.Message.Content))
            {
                if (text.Length > 0)
                {
                    text.Append('\n');
                }

                text.Append(item.Message.Content);
            }

            usage = SemanticKernelUsage.From(item.Message.Metadata) ?? usage;
        }

        return new AgentReply(text.ToString()) { Usage = usage };
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<AgentStreamEvent> StreamAsync(AgentRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        TokenUsage? usage = null;
        await foreach (var item in _agent.InvokeStreamingAsync(Messages(request), thread: null, options: null, cancellationToken).ConfigureAwait(false))
        {
            if (!string.IsNullOrEmpty(item.Message.Content))
            {
                yield return new TokenStreamEvent(item.Message.Content);
            }

            usage = SemanticKernelUsage.From(item.Message.Metadata) ?? usage;
        }

        yield return new DoneStreamEvent(usage);
    }

    private static List<ChatMessageContent> Messages(AgentRequest request) => [new(AuthorRole.User, request.Message)];
}

/// <summary>Semantic Kernel helpers for AgentOven agents.</summary>
public static class AgentOvenKernelExtensions
{
    /// <summary>Plugin name the agent's MCP tools are registered under.</summary>
    public const string PluginName = "agentoven";

    /// <summary>Adds the agent's MCP tools (and delegation, for orchestrators) as a kernel plugin.</summary>
    public static IKernelBuilderPlugins AddAgentOvenTools(this IKernelBuilderPlugins plugins, AgentTools tools, string pluginName = PluginName)
    {
        ArgumentNullException.ThrowIfNull(plugins);
        ArgumentNullException.ThrowIfNull(tools);
        var functions = tools.AsAITools().OfType<AIFunction>().Select(f => f.AsKernelFunction()).ToList();
        if (functions.Count > 0)
        {
            plugins.AddFromFunctions(pluginName, functions);
        }

        return plugins;
    }

    /// <summary>
    /// Builds a kernel for this agent: the given chat client (wrapped with Semantic Kernel's function invocation, so
    /// kernel functions and filters run) as its chat completion service, and the agent's MCP tools as a plugin.
    /// Pass a chat client that does not already invoke functions itself.
    /// </summary>
    public static Kernel CreateKernel(this AgentOvenRuntime runtime, IChatClient chatClient, AgentTools tools)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(chatClient);
        var builder = Kernel.CreateBuilder();
        var invoking = chatClient.AsBuilder().UseKernelFunctionInvocation().Build();
        builder.Services.AddSingleton(invoking.AsChatCompletionService());
        builder.Plugins.AddAgentOvenTools(tools);
        return builder.Build();
    }
}

internal static class SemanticKernelUsage
{
    public static TokenUsage? From(IReadOnlyDictionary<string, object?>? metadata) =>
        metadata is not null && metadata.TryGetValue("Usage", out var value) && value is UsageDetails details
            ? AIContentMapping.ToUsage(details)
            : null;
}
