using System.Runtime.CompilerServices;
using System.Text;

namespace AgentOven.Internal;

/// <summary>One server-sent event: the <c>event:</c> name (or <c>message</c>) and the joined <c>data:</c> lines.</summary>
internal readonly record struct ServerSentEvent(string EventType, string Data);

/// <summary>Minimal <c>text/event-stream</c> reader (WHATWG event stream format).</summary>
internal static class ServerSentEvents
{
    public static async IAsyncEnumerable<ServerSentEvent> ReadAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        string eventType = "message";
        var data = new StringBuilder();
        var hasData = false;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                if (hasData)
                {
                    yield return new ServerSentEvent(eventType, data.ToString());
                }

                yield break;
            }

            if (line.Length == 0)
            {
                if (hasData)
                {
                    yield return new ServerSentEvent(eventType, data.ToString());
                }

                eventType = "message";
                data.Clear();
                hasData = false;
                continue;
            }

            if (line[0] == ':')
            {
                continue; // comment / keep-alive
            }

            var colon = line.IndexOf(':');
            var field = colon < 0 ? line : line[..colon];
            var value = colon < 0 ? string.Empty : line[(colon + 1)..];
            if (value.StartsWith(' '))
            {
                value = value[1..];
            }

            switch (field)
            {
                case "event":
                    eventType = value;
                    break;
                case "data":
                    if (hasData)
                    {
                        data.Append('\n');
                    }

                    data.Append(value);
                    hasData = true;
                    break;
            }
        }
    }
}
