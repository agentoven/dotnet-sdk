using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace AgentOven.Runtime;

/// <summary>
/// One-line hosting, the counterpart of the Python SDK's <c>serve(handler)</c>.
/// For DI, middleware or extra endpoints, use <c>AddAgentOvenRuntime</c> and <c>MapAgentOven</c> on your own
/// <see cref="WebApplication"/> instead.
/// </summary>
/// <example>
/// <code>
/// await AgentOvenServer.RunAsync(async (message, ct) => $"You said: {message}");
/// </code>
/// </example>
public static class AgentOvenServer
{
    /// <summary>Serves <paramref name="handler"/> until the process is stopped.</summary>
    public static async Task RunAsync(IAgentHandler handler, string[]? args = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var builder = WebApplication.CreateSlimBuilder(args ?? []);
        builder.AddAgentOvenRuntime();
        await using var app = builder.Build();
        app.MapAgentOven(handler);
        await app.StartAsync(cancellationToken).ConfigureAwait(false);
        await app.WaitForShutdownAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Serves a function from message to answer until the process is stopped.</summary>
    public static Task RunAsync(Func<string, CancellationToken, Task<string>> handler, string[]? args = null, CancellationToken cancellationToken = default) =>
        RunAsync(AgentHandler.From(handler), args, cancellationToken);
}
