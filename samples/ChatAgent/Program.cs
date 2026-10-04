// A tool-calling agent on Microsoft.Extensions.AI. The control plane injects the model
// (AGENT_MODEL_PROVIDER/NAME, AGENT_API_KEY) and the MCP tools (AGENT_TOOLS_JSON);
// ChatClientAgentHandler runs the loop and streams tokens and tool calls.
using AgentOven.Runtime.AI;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);
builder.AddAgentOvenRuntime();
builder.AddAgentOvenChatClient().UseLogging();

var app = builder.Build();
app.MapAgentOven<ChatClientAgentHandler>();
app.Run();
