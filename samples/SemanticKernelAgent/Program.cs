// A Semantic Kernel agent served by AgentOven: the kernel gets the injected model as its chat
// completion service and the agent's MCP tools as the "agentoven" plugin; add your own plugins too.
using System.ComponentModel;
using Microsoft.SemanticKernel;

var builder = WebApplication.CreateBuilder(args);
builder.AddAgentOvenRuntime();
builder.AddAgentOvenChatClient();
builder.AddAgentOvenKernel(kernel => kernel.Plugins.AddFromType<Clock>());

var app = builder.Build();
app.MapAgentOvenKernel();
app.Run();

internal sealed class Clock
{
    [KernelFunction, Description("Returns the current UTC time.")]
    public static string UtcNow() => DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
}
