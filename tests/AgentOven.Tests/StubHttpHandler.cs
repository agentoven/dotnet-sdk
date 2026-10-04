using System.Net;
using System.Text;

namespace AgentOven.Tests;

/// <summary>Records requests and answers them from a queue of canned responses or a routing function.</summary>
public sealed class StubHttpHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

    public List<RecordedRequest> Requests { get; } = [];

    public Func<HttpRequestMessage, HttpResponseMessage>? Fallback { get; set; }

    public StubHttpHandler Respond(HttpStatusCode status, string body, string contentType = "application/json")
    {
        _responses.Enqueue(_ => new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, contentType) });
        return this;
    }

    public StubHttpHandler RespondJson(string body) => Respond(HttpStatusCode.OK, body);

    public StubHttpHandler RespondSse(string body) => Respond(HttpStatusCode.OK, body, "text/event-stream");

    public RecordedRequest Last => Requests[^1];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new RecordedRequest(
            request.Method,
            request.RequestUri!,
            request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase),
            body));

        if (_responses.TryDequeue(out var respond))
        {
            return respond(request);
        }

        return Fallback?.Invoke(request) ?? new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("""{"error":"no stub"}""", Encoding.UTF8, "application/json"),
        };
    }
}

public sealed record RecordedRequest(HttpMethod Method, Uri Uri, IReadOnlyDictionary<string, string> Headers, string? Body)
{
    public string PathAndQuery => Uri.PathAndQuery;

    public System.Text.Json.JsonElement Json => System.Text.Json.JsonDocument.Parse(Body ?? "null").RootElement;
}
