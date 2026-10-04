using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentOven.Runtime.Hosting;

/// <summary>Request handlers for the agent process HTTP contract.</summary>
internal sealed partial class AgentEndpoints
{
    private static readonly long StartTimestamp = Stopwatch.GetTimestamp();
    private readonly Func<HttpContext, IAgentHandler> _resolveHandler;

    public AgentEndpoints(Func<HttpContext, IAgentHandler> resolveHandler) => _resolveHandler = resolveHandler;

    // GET /health, /status
    public static Task HealthAsync(HttpContext context)
    {
        var runtime = context.RequestServices.GetRequiredService<AgentOvenRuntime>();
        return WriteJsonAsync(context, StatusCodes.Status200OK, w =>
        {
            w.WriteString("status", "healthy");
            w.WriteString("agent", runtime.Name);
            w.WriteString("kitchen", runtime.Kitchen);
            w.WriteString("model", runtime.ModelId);
            w.WriteString("runtime", "dotnet");
            w.WriteStartArray("tools");
            foreach (var name in ToolNames(context, runtime))
            {
                w.WriteStringValue(name);
            }

            w.WriteEndArray();
            w.WriteNumber("pid", Environment.ProcessId);
            w.WriteNumber("uptime_seconds", Math.Round(Stopwatch.GetElapsedTime(StartTimestamp).TotalSeconds, 1));
        });
    }

    // GET /.well-known/agent-card.json, /agent-card
    public static Task AgentCardAsync(HttpContext context)
    {
        var runtime = context.RequestServices.GetRequiredService<AgentOvenRuntime>();
        var options = context.RequestServices.GetRequiredService<AgentOvenRuntimeOptions>();
        var tools = ToolNames(context, runtime);
        return WriteJsonAsync(context, StatusCodes.Status200OK, w =>
        {
            w.WriteString("name", runtime.Name);
            w.WriteString("description", runtime.Description ?? options.Description ?? $"{runtime.Name} (AgentOven .NET runtime)");
            w.WriteString("url", $"http://localhost:{runtime.Port}");
            w.WriteString("version", options.Version);
            w.WriteStartObject("capabilities");
            w.WriteBoolean("streaming", true);
            w.WriteBoolean("pushNotifications", false);
            w.WriteBoolean("toolCalling", tools.Count > 0);
            w.WriteEndObject();
            w.WriteStartArray("skills");
            foreach (var skill in runtime.Skills)
            {
                w.WriteStartObject();
                w.WriteString("id", skill);
                w.WriteString("name", skill);
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteStartArray("tools");
            foreach (var name in tools)
            {
                w.WriteStringValue(name);
            }

            w.WriteEndArray();
            w.WriteStartArray("defaultInputModes");
            w.WriteStringValue("text");
            w.WriteEndArray();
            w.WriteStartArray("defaultOutputModes");
            w.WriteStringValue("text");
            w.WriteEndArray();
        });
    }

    // POST /invoke
    public async Task InvokeAsync(HttpContext context)
    {
        var request = await ReadRequestAsync(context).ConfigureAwait(false);
        if (request is null)
        {
            return;
        }

        try
        {
            var reply = await _resolveHandler(context).InvokeAsync(request, context.RequestAborted).ConfigureAwait(false);
            var usage = reply.Usage ?? new TokenUsage();
            await WriteJsonAsync(context, StatusCodes.Status200OK, w =>
            {
                w.WriteString("response", reply.Text);
                w.WriteStartObject("usage");
                w.WriteNumber("input_tokens", usage.InputTokens);
                w.WriteNumber("output_tokens", usage.OutputTokens);
                w.WriteNumber("total_tokens", usage.TotalTokens);
                w.WriteEndObject();
                w.WriteString("trace_id", request.TraceId);
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Caller went away.
        }
        catch (Exception ex)
        {
            LogInvokeFailed(Logger(context), ex, request.TraceId);
            await WriteErrorAsync(context, StatusCodes.Status500InternalServerError, ex.Message).ConfigureAwait(false);
        }
    }

    // POST /invoke/stream
    public async Task StreamAsync(HttpContext context)
    {
        var request = await ReadRequestAsync(context).ConfigureAwait(false);
        if (request is null)
        {
            return;
        }

        var response = context.Response;
        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";
        response.Headers["X-Accel-Buffering"] = "no";
        context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>()?.DisableBuffering();
        await response.Body.FlushAsync(context.RequestAborted).ConfigureAwait(false);

        var ct = context.RequestAborted;
        var sawDone = false;
        try
        {
            await foreach (var evt in _resolveHandler(context).StreamAsync(request, ct).ConfigureAwait(false))
            {
                if (sawDone)
                {
                    break;
                }

                await WriteEventAsync(response, evt, ct).ConfigureAwait(false);
                sawDone = evt is DoneStreamEvent;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            LogStreamFailed(Logger(context), ex, request.TraceId);
            await WriteEventAsync(response, new ErrorStreamEvent(ex.Message), ct).ConfigureAwait(false);
        }

        if (!sawDone)
        {
            await WriteEventAsync(response, new DoneStreamEvent(null), ct).ConfigureAwait(false);
        }
    }

    internal static async Task WriteEventAsync(HttpResponse response, AgentStreamEvent evt, CancellationToken ct)
    {
        var payload = "data: " + evt.ToJson() + "\n\n";
        await response.Body.WriteAsync(Encoding.UTF8.GetBytes(payload), ct).ConfigureAwait(false);
        await response.Body.FlushAsync(ct).ConfigureAwait(false);
    }

    private static async Task<AgentRequest?> ReadRequestAsync(HttpContext context)
    {
        JsonDocument doc;
        try
        {
            doc = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            await WriteErrorAsync(context, StatusCodes.Status400BadRequest, "invalid JSON").ConfigureAwait(false);
            return null;
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                await WriteErrorAsync(context, StatusCodes.Status400BadRequest, "invalid JSON").ConfigureAwait(false);
                return null;
            }

            var request = AgentRequest.Parse(doc.RootElement);
            if (request.Message.Length == 0)
            {
                await WriteErrorAsync(context, StatusCodes.Status400BadRequest, "missing 'message' field").ConfigureAwait(false);
                return null;
            }

            return request;
        }
    }

    internal static IReadOnlyList<string> ToolNames(HttpContext context, AgentOvenRuntime runtime)
    {
        var names = runtime.Tools.Select(t => t.Name).ToList();
        if (names.Count == 0 && context.RequestServices.GetService<AgentTools>() is { OffersDelegation: true })
        {
            names.Add(AgentTools.DelegateToolName);
        }

        return names;
    }

    internal static Task WriteErrorAsync(HttpContext context, int statusCode, string message) =>
        WriteJsonAsync(context, statusCode, w => w.WriteString("error", message));

    internal static async Task WriteJsonAsync(HttpContext context, int statusCode, Action<Utf8JsonWriter> writeBody)
    {
        using var buffer = new MemoryStream();
        await using (var writer = new Utf8JsonWriter(buffer, AgentStreamEvent.WriterOptions))
        {
            writer.WriteStartObject();
            writeBody(writer);
            writer.WriteEndObject();
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        context.Response.ContentLength = buffer.Length;
        await context.Response.Body.WriteAsync(buffer.GetBuffer().AsMemory(0, (int)buffer.Length), context.RequestAborted).ConfigureAwait(false);
    }

    private static ILogger Logger(HttpContext context) =>
        context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("AgentOven.Runtime");

    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Agent invocation failed (trace {TraceId})")]
    private static partial void LogInvokeFailed(ILogger logger, Exception exception, string traceId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Agent stream failed (trace {TraceId})")]
    private static partial void LogStreamFailed(ILogger logger, Exception exception, string traceId);
}
