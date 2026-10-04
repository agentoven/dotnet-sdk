// The smallest AgentOven agent process. Register it with
//   Agent.Create("echo").RunsOwnProcess("dotnet EchoAgent.dll")
// and the control plane starts it at bake time with AGENT_PORT, AGENT_NAME, … set.
using AgentOven.Runtime;

await AgentOvenServer.RunAsync(async (message, ct) =>
{
    await Task.Yield();
    return $"echo: {message}";
}, args);
