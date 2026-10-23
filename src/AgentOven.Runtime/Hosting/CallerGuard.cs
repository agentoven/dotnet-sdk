using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace AgentOven.Runtime.Hosting;

/// <summary>
/// Accepts only calls that carry the control plane's <c>X-AgentOven-A2A-Token</c>, like the built-in runner.
/// Without a configured token (running outside AgentOven), every call is accepted.
/// </summary>
internal sealed class CallerGuard(string? token, bool enforce)
{
    public const string HeaderName = "X-AgentOven-A2A-Token";

    private readonly byte[]? _expected = token is null ? null : Encoding.UTF8.GetBytes(token);

    public RequestDelegate Wrap(RequestDelegate next) => context =>
    {
        if (_expected is null || !enforce || IsAuthorized(context.Request.Headers[HeaderName].ToString()))
        {
            return next(context);
        }

        return AgentEndpoints.WriteJsonAsync(context, StatusCodes.Status401Unauthorized, w =>
        {
            w.WriteString("error", "authentication_required");
            w.WriteString("message", "This agent only accepts calls from the control plane.");
        });
    };

    internal bool IsAuthorized(string presented) =>
        _expected is null || CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(presented), _expected);
}
