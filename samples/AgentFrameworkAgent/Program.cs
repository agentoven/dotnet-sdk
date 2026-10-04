// A Microsoft Agent Framework agent served by AgentOven. AddAgentOvenAIAgent builds a ChatClientAgent
// from the injected model and MCP tools; bring your own AIAgent with app.MapAgentOven(myAgent) instead.
var builder = WebApplication.CreateBuilder(args);
builder.AddAgentOvenRuntime();
builder.AddAgentOvenChatClient();
builder.AddAgentOvenAIAgent();

var app = builder.Build();
app.MapAgentOvenAIAgent();
app.Run();
