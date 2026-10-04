using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;

namespace AgentOven.Runtime.AI;

/// <summary>Maps Microsoft.Extensions.AI content to AgentOven stream events and usage.</summary>
internal sealed class AIContentMapping
{
    private readonly Dictionary<string, string> _calls = [];

    /// <summary>Usage accumulated from <see cref="UsageContent"/> seen so far.</summary>
    public TokenUsage Usage { get; private set; } = new();

    /// <summary>Converts one update's contents; remembers call ids so results carry the tool name.</summary>
    public IEnumerable<AgentStreamEvent> Map(IEnumerable<AIContent> contents)
    {
        foreach (var content in contents)
        {
            switch (content)
            {
                case TextContent text when !string.IsNullOrEmpty(text.Text):
                    yield return new TokenStreamEvent(text.Text);
                    break;
                case FunctionCallContent call:
                    _calls[call.CallId] = call.Name;
                    yield return new ToolCallStreamEvent(call.Name, ToJsonObject(call.Arguments));
                    break;
                case FunctionResultContent result:
                    yield return new ToolResultStreamEvent(_calls.TryGetValue(result.CallId, out var name) ? name : result.CallId, ResultText(result));
                    break;
                case UsageContent usage:
                    Usage += ToUsage(usage.Details) ?? new TokenUsage();
                    break;
            }
        }
    }

    internal static TokenUsage? ToUsage(UsageDetails? usage) => usage is null
        ? null
        : new TokenUsage
        {
            InputTokens = usage.InputTokenCount ?? 0,
            OutputTokens = usage.OutputTokenCount ?? 0,
            TotalTokens = usage.TotalTokenCount ?? (usage.InputTokenCount ?? 0) + (usage.OutputTokenCount ?? 0),
        };

    internal static JsonObject? ToJsonObject(IDictionary<string, object?>? arguments)
    {
        if (arguments is null)
        {
            return null;
        }

        var obj = new JsonObject();
        foreach (var (key, value) in arguments)
        {
            obj[key] = value switch
            {
                null => null,
                JsonElement e => JsonNode.Parse(e.GetRawText()),
                JsonNode n => n.DeepClone(),
                string s => JsonValue.Create(s),
                bool b => JsonValue.Create(b),
                int i => JsonValue.Create(i),
                long l => JsonValue.Create(l),
                double d => JsonValue.Create(d),
                _ => JsonValue.Create(value.ToString()),
            };
        }

        return obj;
    }

    internal static string ResultText(FunctionResultContent result) => result.Result switch
    {
        null => result.Exception?.Message ?? "",
        string s => s,
        JsonElement e => e.ValueKind == JsonValueKind.String ? e.GetString() ?? "" : e.GetRawText(),
        var other => other.ToString() ?? "",
    };
}
