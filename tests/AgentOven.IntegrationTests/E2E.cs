using System.Text.Json.Nodes;

namespace AgentOven.IntegrationTests;

/// <summary>Configuration of an end-to-end run, from environment variables set by <c>scripts/e2e.sh</c>.</summary>
internal static class E2E
{
    public static Uri? OssUrl => Url("AGENTOVEN_E2E_URL");

    public static Uri? ProUrl => Url("AGENTOVEN_E2E_PRO_URL");

    public static string ApiKey => Environment.GetEnvironmentVariable("AGENTOVEN_E2E_API_KEY") ?? "e2e-key";

    /// <summary>Base URL of tests/e2e/fake_upstream.py.</summary>
    public static Uri Upstream => Url("AGENTOVEN_E2E_UPSTREAM") ?? new Uri("http://127.0.0.1:19100");

    public static string? EchoAgentDll => File(Environment.GetEnvironmentVariable("AGENTOVEN_E2E_ECHO_AGENT"));

    public static string? ChatAgentDll => File(Environment.GetEnvironmentVariable("AGENTOVEN_E2E_CHAT_AGENT"));

    /// <summary>A short suffix so repeated runs against the same server do not collide.</summary>
    public static string RunId { get; } = Guid.NewGuid().ToString("N")[..6];

    public static string Name(string prefix) => $"{prefix}-{RunId}";

    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static AgentOvenClient Client(Uri url, string? kitchen = null, string? apiKey = null) =>
        new(new AgentOvenClientOptions { Url = url, ApiKey = apiKey ?? ApiKey, Kitchen = kitchen ?? "default" });

    /// <summary>Registers an OpenAI-compatible provider backed by the fake upstream (named so the runtime infers its kind).</summary>
    public static async Task<string> EnsureProviderAsync(AgentOvenClient oven)
    {
        var name = Name("fake-openai");
        if ((await oven.Providers.ListAsync(Ct)).Any(p => p.Name == name))
        {
            return name;
        }

        await oven.Providers.AddAsync(name, ProviderKind.OpenAI, apiKey: "sk-fake", endpoint: new Uri(Upstream, "/v1").ToString(),
            models: ["llama3.2", "gpt-4o"], cancellationToken: Ct);
        return name;
    }

    /// <summary>Registers and bakes an agent, returning its handle once ready.</summary>
    public static async Task<AgentHandle> BakeAsync(AgentOvenClient oven, Agent agent)
    {
        await oven.Agents.RegisterAsync(agent, Ct);
        var handle = oven.Agents[agent.Name];
        await handle.BakeAndWaitAsync(timeout: TimeSpan.FromSeconds(90), cancellationToken: Ct);
        return handle;
    }

    public static async Task SafeAsync(Func<Task> cleanup)
    {
        try
        {
            await cleanup();
        }
        catch (Exception ex) when (ex is AgentOvenException or HttpRequestException)
        {
            // Best-effort cleanup.
        }
    }

    public static JsonObject Obj(string json) => JsonNode.Parse(json)!.AsObject();

    private static Uri? Url(string variable) =>
        Environment.GetEnvironmentVariable(variable) is { Length: > 0 } value ? new Uri(value) : null;

    private static string? File(string? path) => path is { Length: > 0 } && System.IO.File.Exists(path) ? path : null;
}
