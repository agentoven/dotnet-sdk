using System.Runtime.CompilerServices;
using System.Text.Json;
using AgentOven.Runtime.AgentFramework;
using AgentOven.Runtime.AI;
using AgentOven.Runtime.SemanticKernel;
using AgentOven.Tests;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using AIChatMessage = Microsoft.Extensions.AI.ChatMessage;
using FunctionCallContent = Microsoft.Extensions.AI.FunctionCallContent;
using FunctionResultContent = Microsoft.Extensions.AI.FunctionResultContent;

namespace AgentOven.Runtime.Tests;

/// <summary>
/// A scripted model: on the first call it asks for the first tool it is offered, on the second it answers with the
/// tool result it was given.
/// </summary>
internal sealed class ScriptedChatClient : IChatClient
{
    public List<List<AIChatMessage>> Calls { get; } = [];

    public ChatOptions? LastOptions { get; private set; }

    public Task<ChatResponse> GetResponseAsync(IEnumerable<AIChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var list = messages.ToList();
        Calls.Add(list);
        LastOptions = options;
        var toolResult = list.SelectMany(m => m.Contents).OfType<FunctionResultContent>().LastOrDefault();
        var usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5, TotalTokenCount = 15 };
        if (toolResult is null && options?.Tools is { Count: > 0 })
        {
            // Call the tool by the name it was advertised under (Semantic Kernel prefixes the plugin name).
            var call = new FunctionCallContent("call-1", options.Tools[0].Name, new Dictionary<string, object?> { ["order"] = 42 });
            return Task.FromResult(new ChatResponse(new AIChatMessage(ChatRole.Assistant, new List<AIContent> { call })) { Usage = usage });
        }

        var text = toolResult is null ? "no tools" : $"Order status: {toolResult.Result}";
        return Task.FromResult(new ChatResponse(new AIChatMessage(ChatRole.Assistant, text)) { Usage = usage });
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<AIChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken);
        // ToChatResponseUpdates carries the usage as a UsageContent update, like real streaming providers.
        foreach (var update in response.ToChatResponseUpdates())
        {
            yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose()
    {
    }
}

public class AdapterTests
{
    private static (ScriptedChatClient Model, AgentOvenRuntime Runtime, AgentTools Tools, StubHttpHandler Mcp) Setup()
    {
        var mcp = new StubHttpHandler
        {
            Fallback = _ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("""{"jsonrpc":"2.0","id":"1","result":{"content":[{"type":"text","text":"shipped"}]}}"""),
            },
        };
        var runtime = Env.Runtime(Env.WithTools());
        return (new ScriptedChatClient(), runtime, new AgentTools(runtime, new HttpClient(mcp)), mcp);
    }

    [Fact]
    public void Tools_become_AIFunctions_with_schema_and_description()
    {
        var (_, _, tools, _) = Setup();

        var function = Assert.IsType<AgentToolFunction>(Assert.Single(tools.AsAITools()));

        Assert.Equal("lookup", function.Name);
        Assert.Equal("Look up an order", function.Description);
        Assert.False(function.JsonSchema.TryGetProperty("description", out _));
        Assert.Equal("integer", function.JsonSchema.GetProperty("properties").GetProperty("order").GetProperty("type").GetString());
    }

    [Fact]
    public async Task ChatClient_handler_runs_the_tool_loop()
    {
        var (model, runtime, tools, mcp) = Setup();
        var handler = new ChatClientAgentHandler(model, runtime, tools);

        var reply = await handler.InvokeAsync(new AgentRequest("Where is order 42?"), TestContext.Current.CancellationToken);

        Assert.Equal("Order status: shipped", reply.Text);
        Assert.Equal(30, reply.Usage!.TotalTokens);
        Assert.Equal(ChatRole.System, model.Calls[0][0].Role);
        Assert.Equal("You are support.", model.Calls[0][0].Text);
        Assert.Equal(42, JsonDocument.Parse(mcp.Last.Body!).RootElement.GetProperty("params").GetProperty("arguments").GetProperty("order").GetInt32());
    }

    [Fact]
    public async Task ChatClient_handler_streams_tool_activity()
    {
        var (model, runtime, tools, _) = Setup();
        var handler = new ChatClientAgentHandler(model, runtime, tools);

        var events = new List<AgentStreamEvent>();
        await foreach (var e in handler.StreamAsync(new AgentRequest("Where is order 42?"), TestContext.Current.CancellationToken))
        {
            events.Add(e);
        }

        Assert.Contains(events, e => e is ToolCallStreamEvent { Name: "lookup" });
        Assert.Contains(events, e => e is ToolResultStreamEvent { Name: "lookup", Result: "shipped" });
        Assert.Equal("Order status: shipped", string.Concat(events.Select(e => e.Text)));
        var done = Assert.IsType<DoneStreamEvent>(events[^1]);
        Assert.Equal(30, done.Usage!.TotalTokens);
    }

    [Fact]
    public async Task Agent_Framework_handler_serves_a_ChatClientAgent()
    {
        var (model, runtime, tools, _) = Setup();
        var agent = runtime.CreateAIAgent(model, tools);
        var handler = new AIAgentHandler(agent, runtime);

        var reply = await handler.InvokeAsync(
            new AgentRequest("Where is order 42?") { Variables = new Dictionary<string, string>() },
            TestContext.Current.CancellationToken);

        Assert.Equal("support", agent.Name);
        Assert.Equal("Order status: shipped", reply.Text);
        Assert.Equal("You are support.", model.Calls[0].First(m => m.Role == ChatRole.System).Text);
    }

    [Fact]
    public async Task Semantic_Kernel_handler_calls_kernel_functions()
    {
        var (model, runtime, tools, _) = Setup();
        var kernel = runtime.CreateKernel(model, tools);

        Assert.Contains(kernel.Plugins, p => p.Name == AgentOvenKernelExtensions.PluginName && p.Contains("lookup"));

        var reply = await new KernelAgentHandler(kernel, runtime).InvokeAsync(new AgentRequest("Where is order 42?"), TestContext.Current.CancellationToken);

        Assert.Equal("Order status: shipped", reply.Text);
    }

    [Fact]
    public void Chat_client_factory_maps_providers()
    {
        Assert.NotNull(AgentOvenChatClients.Create("openai", "gpt-4o", apiKey: "sk"));
        Assert.NotNull(AgentOvenChatClients.Create("azure-openai", "gpt-4o", apiKey: "k", endpoint: "https://x.openai.azure.com"));
        Assert.NotNull(AgentOvenChatClients.Create("anthropic", "claude-sonnet-4-5", apiKey: "k"));
        Assert.NotNull(AgentOvenChatClients.Create("ollama", "llama3.2"));
        Assert.NotNull(AgentOvenChatClients.Create("litellm", "any", apiKey: "k", endpoint: "http://litellm:4000"));
        Assert.Throws<NotSupportedException>(() => AgentOvenChatClients.Create("mystery", "m", apiKey: "k"));
        Assert.Throws<InvalidOperationException>(() => AgentOvenChatClients.Create("azure-openai", "m", apiKey: "k"));
    }
}
