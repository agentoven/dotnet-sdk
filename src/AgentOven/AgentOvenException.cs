using System.Net;
using System.Text.Json;

namespace AgentOven;

/// <summary>Base type for errors raised by the AgentOven SDK.</summary>
public class AgentOvenException : Exception
{
    /// <summary>Creates an exception with a message.</summary>
    public AgentOvenException(string message) : base(message) { }

    /// <summary>Creates an exception with a message and an inner exception.</summary>
    public AgentOvenException(string message, Exception? innerException) : base(message, innerException) { }
}

/// <summary>
/// The control plane answered with a non-success status code.
/// </summary>
/// <remarks>
/// The server writes <c>{"error": "..."}</c> for most failures. Auth failures add a machine code:
/// <c>{"error": "authentication_failed", "message": "..."}</c>; ingredient resolution failures add
/// <c>details</c>; guardrail blocks add <c>guardrails</c>. All of it is in <see cref="Detail"/>.
/// </remarks>
public sealed class AgentOvenApiException : AgentOvenException
{
    /// <summary>Creates an API exception.</summary>
    public AgentOvenApiException(HttpStatusCode statusCode, string? error, string? serverMessage, string? detail, string? requestId = null)
        : base(BuildMessage(statusCode, error, serverMessage, detail))
    {
        StatusCode = statusCode;
        Error = error;
        ServerMessage = serverMessage;
        Detail = detail;
        RequestId = requestId;
    }

    /// <summary>HTTP status code.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>The <c>error</c> field of the response: a message, or a code such as <c>authentication_failed</c>.</summary>
    public string? Error { get; }

    /// <summary>The <c>message</c> (or <c>details</c>) field of the response, when present.</summary>
    public string? ServerMessage { get; }

    /// <summary>The raw response body.</summary>
    public string? Detail { get; }

    /// <summary>The <c>X-Request-Id</c> response header, when present.</summary>
    public string? RequestId { get; }

    /// <summary>404 Not Found.</summary>
    public bool IsNotFound => StatusCode == HttpStatusCode.NotFound;

    /// <summary>401 Unauthorized. The control plane requires an identity for invoke even when auth is otherwise optional.</summary>
    public bool IsUnauthorized => StatusCode == HttpStatusCode.Unauthorized;

    /// <summary>403 Forbidden: a scoped key without access, a kitchen mismatch, or a guardrail block.</summary>
    public bool IsForbidden => StatusCode == HttpStatusCode.Forbidden;

    /// <summary>409 Conflict, for example a session on an agent that is not ready.</summary>
    public bool IsConflict => StatusCode == HttpStatusCode.Conflict;

    /// <summary>A guardrail blocked the input or output.</summary>
    public bool IsGuardrailBlock =>
        StatusCode == HttpStatusCode.Forbidden && Error is not null && Error.Contains("blocked by guardrails", StringComparison.OrdinalIgnoreCase);

    internal static AgentOvenApiException From(HttpStatusCode statusCode, string? body, string? requestId)
    {
        string? error = null;
        string? message = null;
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    error = GetString(doc.RootElement, "error");
                    message = GetString(doc.RootElement, "message") ?? GetString(doc.RootElement, "details");
                }
            }
            catch (JsonException)
            {
                error = body.Length <= 500 ? body.Trim() : null;
            }
        }

        return new AgentOvenApiException(statusCode, error, message, body, requestId);
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string BuildMessage(HttpStatusCode statusCode, string? error, string? serverMessage, string? detail)
    {
        var text = (error, serverMessage) switch
        {
            (not null, not null) => $"{error}: {serverMessage}",
            (not null, null) => error,
            (null, not null) => serverMessage,
            _ => string.IsNullOrWhiteSpace(detail) ? statusCode.ToString() : detail,
        };
        return $"AgentOven API error {(int)statusCode}: {text}";
    }
}
