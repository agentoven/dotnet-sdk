using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace AgentOven.Runtime.Hosting;

/// <summary>
/// A2A JSON-RPC 2.0 (<c>POST /</c> and <c>POST /a2a</c>): <c>tasks/send</c>, <c>tasks/get</c>, <c>tasks/cancel</c>,
/// with the same task shape as the control plane's built-in runner. Tasks run synchronously and are kept in memory.
/// </summary>
internal sealed class A2AEndpoint
{
    private const int MaxTasks = 1000;
    private readonly Func<HttpContext, IAgentHandler> _resolveHandler;
    private readonly ConcurrentDictionary<string, A2ATask> _tasks = new();
    private readonly ConcurrentQueue<string> _order = new();

    public A2AEndpoint(Func<HttpContext, IAgentHandler> resolveHandler) => _resolveHandler = resolveHandler;

    public async Task HandleAsync(HttpContext context)
    {
        JsonDocument doc;
        try
        {
            doc = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            await AgentEndpoints.WriteJsonAsync(context, StatusCodes.Status200OK, w => WriteError(w, null, -32700, "Parse error")).ConfigureAwait(false);
            return;
        }

        using (doc)
        {
            var root = doc.RootElement;
            var id = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("id", out var idElement) ? idElement.Clone() : (JsonElement?)null;
            var method = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("method", out var m) ? m.GetString() ?? "" : "";
            var parameters = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("params", out var p) && p.ValueKind == JsonValueKind.Object
                ? p
                : default;

            Action<Utf8JsonWriter> body = method switch
            {
                "tasks/send" => await SendAsync(context, id, parameters).ConfigureAwait(false),
                "tasks/get" => Get(id, parameters),
                "tasks/cancel" => Cancel(id, parameters),
                _ => w => WriteError(w, id, -32601, $"Method '{method}' not found"),
            };
            await AgentEndpoints.WriteJsonAsync(context, StatusCodes.Status200OK, body).ConfigureAwait(false);
        }
    }

    private async Task<Action<Utf8JsonWriter>> SendAsync(HttpContext context, JsonElement? id, JsonElement parameters)
    {
        var taskId = GetString(parameters, "id") ?? Guid.NewGuid().ToString();
        var text = "";
        if (parameters.ValueKind == JsonValueKind.Object
            && parameters.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.Object
            && message.TryGetProperty("parts", out var parts) && parts.ValueKind == JsonValueKind.Array)
        {
            text = string.Concat(parts.EnumerateArray()
                .Where(part => part.ValueKind == JsonValueKind.Object && part.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String)
                .Select(part => part.GetProperty("text").GetString()));
        }

        if (text.Length == 0)
        {
            return w => WriteError(w, id, -32602, "No text content in message");
        }

        A2ATask task;
        try
        {
            var reply = await _resolveHandler(context)
                .InvokeAsync(new AgentRequest(text) { TraceId = taskId }, context.RequestAborted)
                .ConfigureAwait(false);
            task = new A2ATask(taskId, "completed", reply.Text, Error: null);
        }
        catch (Exception ex) when (!context.RequestAborted.IsCancellationRequested)
        {
            task = new A2ATask(taskId, "failed", Response: null, ex.Message);
        }

        Store(task);
        return w => WriteResult(w, id, task.WriteTo);
    }

    private Action<Utf8JsonWriter> Get(JsonElement? id, JsonElement parameters)
    {
        var taskId = GetString(parameters, "id") ?? "";
        return _tasks.TryGetValue(taskId, out var task)
            ? w => WriteResult(w, id, task.WriteTo)
            : w => WriteError(w, id, -32602, $"Task '{taskId}' not found");
    }

    private Action<Utf8JsonWriter> Cancel(JsonElement? id, JsonElement parameters)
    {
        var taskId = GetString(parameters, "id") ?? "";
        if (_tasks.TryGetValue(taskId, out var task))
        {
            _tasks[taskId] = task with { State = "canceled" };
        }

        return w => WriteResult(w, id, r =>
        {
            r.WriteString("id", taskId);
            r.WriteStartObject("status");
            r.WriteString("state", "canceled");
            r.WriteEndObject();
        });
    }

    private void Store(A2ATask task)
    {
        _tasks[task.Id] = task;
        _order.Enqueue(task.Id);
        while (_order.Count > MaxTasks && _order.TryDequeue(out var oldest))
        {
            _tasks.TryRemove(oldest, out _);
        }
    }

    private static string? GetString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static void WriteEnvelope(Utf8JsonWriter w, JsonElement? id)
    {
        w.WriteString("jsonrpc", "2.0");
        w.WritePropertyName("id");
        if (id is { } value)
        {
            value.WriteTo(w);
        }
        else
        {
            w.WriteNullValue();
        }
    }

    private static void WriteResult(Utf8JsonWriter w, JsonElement? id, Action<Utf8JsonWriter> writeResult)
    {
        WriteEnvelope(w, id);
        w.WriteStartObject("result");
        writeResult(w);
        w.WriteEndObject();
    }

    private static void WriteError(Utf8JsonWriter w, JsonElement? id, int code, string message)
    {
        WriteEnvelope(w, id);
        w.WriteStartObject("error");
        w.WriteNumber("code", code);
        w.WriteString("message", message);
        w.WriteEndObject();
    }

    private sealed record A2ATask(string Id, string State, string? Response, string? Error)
    {
        public void WriteTo(Utf8JsonWriter w)
        {
            w.WriteString("id", Id);
            w.WriteStartObject("status");
            w.WriteString("state", State);
            if (Error is not null)
            {
                w.WriteStartObject("message");
                w.WriteString("role", "agent");
                WriteParts(w, Error);
                w.WriteEndObject();
            }

            w.WriteEndObject();
            w.WriteStartArray("artifacts");
            if (Response is not null)
            {
                w.WriteStartObject();
                WriteParts(w, Response);
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteStartArray("history");
            if (Response is not null)
            {
                w.WriteStartObject();
                w.WriteString("role", "agent");
                WriteParts(w, Response);
                w.WriteEndObject();
            }

            w.WriteEndArray();
        }

        private static void WriteParts(Utf8JsonWriter w, string text)
        {
            w.WriteStartArray("parts");
            w.WriteStartObject();
            w.WriteString("type", "text");
            w.WriteString("text", text);
            w.WriteEndObject();
            w.WriteEndArray();
        }
    }
}
