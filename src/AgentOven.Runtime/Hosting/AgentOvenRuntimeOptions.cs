namespace AgentOven.Runtime;

/// <summary>Options for <c>AddAgentOvenRuntime</c>.</summary>
public sealed class AgentOvenRuntimeOptions
{
    /// <summary>
    /// Listen on <see cref="AgentOvenRuntime.Port"/>. When the control plane assigned a port (<c>AGENT_PORT</c>),
    /// it always wins; otherwise the port is used only if no URLs are configured. Default: <see langword="true"/>.
    /// </summary>
    public bool BindPort { get; set; } = true;

    /// <summary>Line written to stdout once the server listens. The process manager waits for it. Default: <c>AGENT_READY</c>.</summary>
    public string ReadySignal { get; set; } = "AGENT_READY";

    /// <summary>Version on the agent card. Default: <c>1.0.0</c>.</summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>Description on the agent card when the control plane did not inject one.</summary>
    public string? Description { get; set; }
}
