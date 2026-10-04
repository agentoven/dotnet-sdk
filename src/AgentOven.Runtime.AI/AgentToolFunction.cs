using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;

namespace AgentOven.Runtime.AI;

/// <summary>
/// One of the agent's tools as an <see cref="AIFunction"/>: the model sees the MCP tool's name, description and
/// JSON Schema, and calls are routed through <see cref="AgentTools"/>.
/// </summary>
public sealed class AgentToolFunction : AIFunction
{
    private static readonly JsonElement EmptySchema = JsonDocument.Parse("""{"type":"object","properties":{}}""").RootElement.Clone();
    private readonly AgentTools _tools;

    internal AgentToolFunction(AgentTools tools, string name, string description, JsonElement schema)
    {
        _tools = tools;
        Name = name;
        Description = description;
        JsonSchema = schema;
    }

    /// <inheritdoc />
    public override string Name { get; }

    /// <inheritdoc />
    public override string Description { get; }

    /// <inheritdoc />
    public override JsonElement JsonSchema { get; }

    /// <inheritdoc />
    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var args = new JsonObject();
        foreach (var (key, value) in arguments)
        {
            args[key] = value switch
            {
                null => null,
                JsonElement element => JsonNode.Parse(element.GetRawText()),
                JsonNode node => node.DeepClone(),
                string s => JsonValue.Create(s),
                bool b => JsonValue.Create(b),
                int i => JsonValue.Create(i),
                long l => JsonValue.Create(l),
                double d => JsonValue.Create(d),
                decimal m => JsonValue.Create(m),
                _ => JsonValue.Create(value.ToString()),
            };
        }

        return await _tools.CallAsync(Name, args, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Wraps a resolved MCP tool.</summary>
    internal static AgentToolFunction FromResolved(AgentTools tools, ResolvedTool tool)
    {
        // Like the built-in runner: a top-level "description" in the schema is the tool description.
        var schema = tool.Schema?.DeepClone().AsObject() ?? [];
        var description = schema["description"]?.GetValue<string>() ?? tool.Name;
        schema.Remove("description");
        var element = schema.Count == 0 ? EmptySchema : JsonDocument.Parse(schema.ToJsonString()).RootElement.Clone();
        return new AgentToolFunction(tools, tool.Name, description, element);
    }

    internal static AgentToolFunction Delegate(AgentTools tools) => new(
        tools,
        AgentTools.DelegateToolName,
        AgentTools.DelegateToolDescription,
        JsonDocument.Parse(AgentTools.DelegateToolSchema.ToJsonString()).RootElement.Clone());
}

/// <summary><see cref="AgentTools"/> as Microsoft.Extensions.AI tools.</summary>
public static class AgentToolsExtensions
{
    /// <summary>
    /// The agent's MCP tools as <see cref="AIFunction"/>s, plus <c>agentoven_delegate</c> for orchestrator agents
    /// (no MCP tools, control plane reachable), matching the built-in runner.
    /// </summary>
    public static IList<AITool> AsAITools(this AgentTools tools, bool includeDelegation = true)
    {
        ArgumentNullException.ThrowIfNull(tools);
        var list = new List<AITool>(tools.Tools.Select(t => AgentToolFunction.FromResolved(tools, t)));
        if (includeDelegation && tools.OffersDelegation)
        {
            list.Add(AgentToolFunction.Delegate(tools));
        }

        return list;
    }
}
