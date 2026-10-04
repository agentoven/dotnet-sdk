using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AgentOven.Runtime;

/// <summary>
/// Calls the agent's resolved MCP tools and delegates to other agents, the same way the control
/// plane's built-in runner does. Results are strings meant for the model: failures come back as
/// <c>"Tool error: …"</c> text instead of exceptions, so the model can react to them.
/// </summary>
public sealed class AgentTools
{
    /// <summary>Name of the built-in delegation tool.</summary>
    public const string DelegateToolName = "agentoven_delegate";

    private static readonly TimeSpan ToolTimeout = TimeSpan.FromSeconds(30);
    private readonly AgentOvenRuntime _runtime;
    private readonly HttpClient _http;
    private readonly Lazy<AgentOvenClient?> _controlPlane;

    /// <summary>Creates a tool caller.</summary>
    public AgentTools(AgentOvenRuntime runtime, HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(httpClient);
        _runtime = runtime;
        _http = httpClient;
        _controlPlane = new Lazy<AgentOvenClient?>(() => runtime.ControlPlaneUrl is null ? null : runtime.CreateControlPlaneClient(httpClient));
    }

    /// <summary>The agent's MCP tools.</summary>
    public IReadOnlyList<ResolvedTool> Tools => _runtime.Tools;

    /// <summary>
    /// Whether the delegation tool should be offered. Like the built-in runner, it is offered to orchestrator
    /// agents (no MCP tools of their own) when the control plane URL is known.
    /// </summary>
    public bool OffersDelegation => _runtime.Tools.Count == 0 && _runtime.ControlPlaneUrl is not null;

    /// <summary>JSON Schema of the delegation tool's arguments.</summary>
    public static JsonObject DelegateToolSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["agent"] = new JsonObject { ["type"] = "string", ["description"] = "Name of the target agent to delegate to" },
            ["message"] = new JsonObject { ["type"] = "string", ["description"] = "The task or message to send to the target agent" },
        },
        ["required"] = new JsonArray("agent", "message"),
    };

    /// <summary>Description of the delegation tool.</summary>
    public const string DelegateToolDescription =
        "Delegate a task to another specialist agent in this kitchen. " +
        "Use when the request requires a capability owned by a different agent.";

    /// <summary>Calls a tool by name: an MCP tool, or <see cref="DelegateToolName"/>.</summary>
    public async Task<string> CallAsync(string name, JsonObject? arguments, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        arguments ??= [];
        if (name == DelegateToolName)
        {
            return await DelegateAsync(
                arguments["agent"]?.GetValue<string>() ?? "",
                arguments["message"]?.GetValue<string>() ?? "",
                cancellationToken).ConfigureAwait(false);
        }

        var tool = _runtime.Tools.FirstOrDefault(t => t.Name == name);
        if (tool?.Endpoint is not { Length: > 0 } endpoint)
        {
            return $"Error: tool '{name}' has no configured endpoint";
        }

        return await CallMcpAsync(endpoint, name, arguments, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Asks another agent in this kitchen, through the control plane.</summary>
    public async Task<string> DelegateAsync(string agentName, string message, CancellationToken cancellationToken = default)
    {
        if (_controlPlane.Value is not { } client)
        {
            return "Delegation failed: AGENTOVEN_CONTROL_PLANE_URL is not configured";
        }

        if (string.IsNullOrWhiteSpace(agentName))
        {
            return "Delegation failed: target agent name is required";
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return "Delegation failed: message is required";
        }

        try
        {
            var result = await client.Agents.InvokeAsync(agentName, message, cancellationToken: cancellationToken).ConfigureAwait(false);
            return result.Response;
        }
        catch (AgentOvenApiException ex)
        {
            return $"Delegation error ({(int)ex.StatusCode}): {ex.Error ?? ex.Detail}";
        }
        catch (HttpRequestException ex)
        {
            return $"Delegation error: {ex.Message}";
        }
    }

    private async Task<string> CallMcpAsync(string endpoint, string name, JsonObject arguments, CancellationToken cancellationToken)
    {
        // Primary: MCP JSON-RPC 2.0 tools/call.
        var rpc = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = Guid.NewGuid().ToString(),
            ["method"] = "tools/call",
            ["params"] = new JsonObject { ["name"] = name, ["arguments"] = arguments.DeepClone() },
        };

        try
        {
            using var doc = await PostAsync(endpoint.TrimEnd('/'), rpc, cancellationToken).ConfigureAwait(false);
            var root = doc.RootElement;
            if (root.TryGetProperty("error", out var error) && error.ValueKind != JsonValueKind.Null)
            {
                var text = error.ValueKind == JsonValueKind.Object && error.TryGetProperty("message", out var m) ? m.GetString() : error.GetRawText();
                return $"Tool error: {text}";
            }

            var result = root.TryGetProperty("result", out var r) ? r : root;
            return ExtractText(result);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            // Fall through to the REST fallback.
        }

        // Fallback: REST POST {endpoint}/call.
        try
        {
            var body = new JsonObject { ["name"] = name, ["arguments"] = arguments.DeepClone() };
            using var doc = await PostAsync(endpoint.TrimEnd('/') + "/call", body, cancellationToken).ConfigureAwait(false);
            return ExtractText(doc.RootElement);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return $"Tool error: {ex.Message}";
        }
    }

    private async Task<JsonDocument> PostAsync(string url, JsonObject body, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(ToolTimeout);
        using var content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var response = await _http.SendAsync(request, cts.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var stream = await response.Content.ReadAsStreamAsync(cts.Token).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            return await JsonDocument.ParseAsync(stream, cancellationToken: cts.Token).ConfigureAwait(false);
        }
    }

    /// <summary>Joins the <c>text</c> parts of an MCP result's <c>content</c>, or returns the raw JSON.</summary>
    internal static string ExtractText(JsonElement result)
    {
        if (result.ValueKind == JsonValueKind.Object
            && result.TryGetProperty("content", out var content)
            && content.ValueKind == JsonValueKind.Array
            && content.GetArrayLength() > 0)
        {
            var parts = content.EnumerateArray()
                .Where(p => p.ValueKind == JsonValueKind.Object
                    && p.TryGetProperty("type", out var type) && type.ValueEquals("text")
                    && p.TryGetProperty("text", out _))
                .Select(p => p.GetProperty("text").GetString() ?? "")
                .ToList();
            if (parts.Count > 0)
            {
                return string.Join('\n', parts);
            }
        }

        return result.GetRawText();
    }
}
