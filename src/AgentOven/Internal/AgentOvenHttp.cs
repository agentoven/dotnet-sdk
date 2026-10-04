using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace AgentOven.Internal;

/// <summary>
/// The single HTTP layer every resource client goes through: URL building, auth and kitchen
/// headers, JSON (source-generated, so trim/AOT safe), error mapping and SSE.
/// Immutable; <see cref="WithKitchen"/> returns a copy sharing the same <see cref="HttpClient"/>.
/// </summary>
internal sealed class AgentOvenHttp
{
    private readonly HttpClient _http;
    private readonly Uri _baseUri;

    public AgentOvenHttp(HttpClient http, AgentOvenClientOptions resolved)
    {
        _http = http;
        var url = resolved.Url!.ToString();
        _baseUri = new Uri(url.EndsWith('/') ? url : url + "/", UriKind.Absolute);
        ApiKey = resolved.ApiKey;
        ServiceToken = resolved.ServiceToken;
        Kitchen = resolved.Kitchen!;
    }

    private AgentOvenHttp(AgentOvenHttp other, string kitchen)
    {
        _http = other._http;
        _baseUri = other._baseUri;
        ApiKey = other.ApiKey;
        ServiceToken = other.ServiceToken;
        Kitchen = kitchen;
    }

    public Uri BaseUri => _baseUri;

    public string Kitchen { get; }

    public string? ApiKey { get; }

    public string? ServiceToken { get; }

    public AgentOvenHttp WithKitchen(string kitchen) => new(this, kitchen);

    // ── JSON requests ────────────────────────────────────────────────────

    public Task<TResponse> GetAsync<TResponse>(string path, JsonTypeInfo<TResponse> responseType, CancellationToken ct) =>
        SendAsync(HttpMethod.Get, path, content: null, responseType, ct);

    public Task<TResponse> DeleteAsync<TResponse>(string path, JsonTypeInfo<TResponse> responseType, CancellationToken ct) =>
        SendAsync(HttpMethod.Delete, path, content: null, responseType, ct);

    public Task DeleteAsync(string path, CancellationToken ct) =>
        SendAsync(HttpMethod.Delete, path, content: null, ct);

    public Task<TResponse> PostAsync<TRequest, TResponse>(
        string path, TRequest body, JsonTypeInfo<TRequest> requestType, JsonTypeInfo<TResponse> responseType, CancellationToken ct) =>
        SendAsync(HttpMethod.Post, path, Json(body, requestType), responseType, ct);

    public Task<TResponse> PostAsync<TResponse>(string path, JsonTypeInfo<TResponse> responseType, CancellationToken ct) =>
        SendAsync(HttpMethod.Post, path, EmptyObject(), responseType, ct);

    public Task<TResponse> PutAsync<TRequest, TResponse>(
        string path, TRequest body, JsonTypeInfo<TRequest> requestType, JsonTypeInfo<TResponse> responseType, CancellationToken ct) =>
        SendAsync(HttpMethod.Put, path, Json(body, requestType), responseType, ct);

    public async Task<TResponse> SendAsync<TResponse>(
        HttpMethod method, string path, HttpContent? content, JsonTypeInfo<TResponse> responseType, CancellationToken ct)
    {
        using var request = CreateRequest(method, path, content);
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response, ct).ConfigureAwait(false);

        var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        if (bytes.Length == 0)
        {
            // Some endpoints answer 200/204 with no body; deserialize "{}" so records get their defaults.
            bytes = "{}"u8.ToArray();
        }

        return JsonSerializer.Deserialize(bytes, responseType)
            ?? throw new AgentOvenException($"The server returned an empty JSON body for {method} /{path}.");
    }

    public async Task SendAsync(HttpMethod method, string path, HttpContent? content, CancellationToken ct)
    {
        using var request = CreateRequest(method, path, content);
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response, ct).ConfigureAwait(false);
    }

    // ── Streaming ────────────────────────────────────────────────────────

    public async IAsyncEnumerable<ServerSentEvent> StreamAsync(
        HttpMethod method, string path, HttpContent? content, [EnumeratorCancellation] CancellationToken ct)
    {
        using var request = CreateRequest(method, path, content);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response, ct).ConfigureAwait(false);

        var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            await foreach (var evt in ServerSentEvents.ReadAsync(stream, ct).ConfigureAwait(false))
            {
                yield return evt;
            }
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    public static HttpContent Json<T>(T body, JsonTypeInfo<T> typeInfo)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(body, typeInfo);
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        return content;
    }

    private static ByteArrayContent EmptyObject()
    {
        var content = new ByteArrayContent("{}"u8.ToArray());
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        return content;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, HttpContent? content)
    {
        var request = new HttpRequestMessage(method, new Uri(_baseUri, path)) { Content = content };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("X-Kitchen", Kitchen);
        request.Headers.TryAddWithoutValidation("X-Kitchen-Id", Kitchen);
        if (ApiKey is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
        }

        if (ServiceToken is not null)
        {
            request.Headers.TryAddWithoutValidation("X-Service-Token", ServiceToken);
        }

        request.Headers.UserAgent.Add(UserAgent);
        return request;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string? body = null;
        try
        {
            body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            // Body unreadable; report the status code alone.
        }

        var requestId = response.Headers.TryGetValues("X-Request-Id", out var values) ? values.FirstOrDefault() : null;
        throw AgentOvenApiException.From(response.StatusCode, body, requestId);
    }

    private static readonly ProductInfoHeaderValue UserAgent = new(
        "agentoven-dotnet",
        typeof(AgentOvenHttp).Assembly.GetName().Version?.ToString(3) ?? "0.0.0");
}

/// <summary>Builds escaped relative paths and query strings.</summary>
internal static class ApiPath
{
    public static string Of(params string[] segments)
    {
        var sb = new StringBuilder("api/v1");
        foreach (var segment in segments)
        {
            sb.Append('/').Append(Uri.EscapeDataString(segment));
        }

        return sb.ToString();
    }

    public static string WithQuery(string path, params (string Key, string? Value)[] query)
    {
        var sb = new StringBuilder(path);
        var first = true;
        foreach (var (key, value) in query)
        {
            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            sb.Append(first ? '?' : '&').Append(Uri.EscapeDataString(key)).Append('=').Append(Uri.EscapeDataString(value));
            first = false;
        }

        return sb.ToString();
    }
}
