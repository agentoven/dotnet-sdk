using Microsoft.Extensions.Hosting;

namespace AgentOven.Runtime.Hosting;

/// <summary>Writes the ready signal to stdout once the host has started listening.</summary>
internal sealed class ReadySignalService(IHostApplicationLifetime lifetime, AgentOvenRuntimeOptions options, AgentOvenRuntime runtime) : IHostedService
{
    private CancellationTokenRegistration _registration;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _registration = lifetime.ApplicationStarted.Register(() =>
        {
            Console.Error.WriteLine($"[{runtime.Name}] AgentOven .NET runtime listening on port {runtime.Port} (model {(runtime.ModelName.Length > 0 ? runtime.ModelId : "not set")}, {runtime.Tools.Count} MCP tools)");
            Console.Out.WriteLine(options.ReadySignal);
            Console.Out.Flush();
        });
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _registration.Dispose();
        return Task.CompletedTask;
    }
}
