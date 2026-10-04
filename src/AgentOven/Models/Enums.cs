// Server-owned value sets. Keep in sync with control-plane/pkg/models/models.go in agentoven/agentoven.

using System.Diagnostics;
using System.Text.Json.Serialization;
using AgentOven.Serialization;

namespace AgentOven;

/// <summary>Lifecycle state of an agent.</summary>
[JsonConverter(typeof(ExtensibleEnumConverter<AgentStatus>))]
[DebuggerDisplay("{Value}")]
public readonly record struct AgentStatus(string Value) : IExtensibleEnum<AgentStatus>
{
    /// <summary><c>draft</c></summary>
    public static AgentStatus Draft { get; } = new("draft");

    /// <summary><c>baking</c></summary>
    public static AgentStatus Baking { get; } = new("baking");

    /// <summary><c>ready</c></summary>
    public static AgentStatus Ready { get; } = new("ready");

    /// <summary><c>cooled</c></summary>
    public static AgentStatus Cooled { get; } = new("cooled");

    /// <summary><c>burnt</c></summary>
    public static AgentStatus Burnt { get; } = new("burnt");

    /// <summary><c>retired</c></summary>
    public static AgentStatus Retired { get; } = new("retired");

    /// <inheritdoc />
    public static AgentStatus Create(string value) => new(value);

    /// <summary>Converts a wire value.</summary>
    public static implicit operator AgentStatus(string value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;

    /// <summary>Case-insensitive comparison with another value.</summary>
    public bool Equals(AgentStatus other) => string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value ?? string.Empty);
}

/// <summary>Who runs the agentic loop.</summary>
[JsonConverter(typeof(ExtensibleEnumConverter<AgentMode>))]
[DebuggerDisplay("{Value}")]
public readonly record struct AgentMode(string Value) : IExtensibleEnum<AgentMode>
{
    /// <summary><c>managed</c></summary>
    public static AgentMode Managed { get; } = new("managed");

    /// <summary><c>external</c></summary>
    public static AgentMode External { get; } = new("external");

    /// <inheritdoc />
    public static AgentMode Create(string value) => new(value);

    /// <summary>Converts a wire value.</summary>
    public static implicit operator AgentMode(string value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;

    /// <summary>Case-insensitive comparison with another value.</summary>
    public bool Equals(AgentMode other) => string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value ?? string.Empty);
}

/// <summary>Which framework runs the LLM loop of a managed agent. Anything other than <c>agentoven</c> means your own process (started from <c>entrypoint</c>) owns the loop; .NET agents built on AgentOven.Runtime use <see cref="Custom"/>.</summary>
[JsonConverter(typeof(ExtensibleEnumConverter<AgentRuntime>))]
[DebuggerDisplay("{Value}")]
public readonly record struct AgentRuntime(string Value) : IExtensibleEnum<AgentRuntime>
{
    /// <summary><c>agentoven</c></summary>
    public static AgentRuntime AgentOven { get; } = new("agentoven");

    /// <summary><c>langchain</c></summary>
    public static AgentRuntime LangChain { get; } = new("langchain");

    /// <summary><c>langgraph</c></summary>
    public static AgentRuntime LangGraph { get; } = new("langgraph");

    /// <summary><c>crewai</c></summary>
    public static AgentRuntime CrewAI { get; } = new("crewai");

    /// <summary><c>custom</c></summary>
    public static AgentRuntime Custom { get; } = new("custom");

    /// <inheritdoc />
    public static AgentRuntime Create(string value) => new(value);

    /// <summary>Converts a wire value.</summary>
    public static implicit operator AgentRuntime(string value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;

    /// <summary>Case-insensitive comparison with another value.</summary>
    public bool Equals(AgentRuntime other) => string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value ?? string.Empty);
}

/// <summary>Free-form framework label shown in the dashboard.</summary>
[JsonConverter(typeof(ExtensibleEnumConverter<AgentFramework>))]
[DebuggerDisplay("{Value}")]
public readonly record struct AgentFramework(string Value) : IExtensibleEnum<AgentFramework>
{
    /// <summary><c>custom</c></summary>
    public static AgentFramework Custom { get; } = new("custom");

    /// <summary><c>managed</c></summary>
    public static AgentFramework Managed { get; } = new("managed");

    /// <summary><c>langchain</c></summary>
    public static AgentFramework LangChain { get; } = new("langchain");

    /// <summary><c>langgraph</c></summary>
    public static AgentFramework LangGraph { get; } = new("langgraph");

    /// <summary><c>crewai</c></summary>
    public static AgentFramework CrewAI { get; } = new("crewai");

    /// <summary><c>openai</c></summary>
    public static AgentFramework OpenAI { get; } = new("openai");

    /// <summary><c>autogen</c></summary>
    public static AgentFramework AutoGen { get; } = new("autogen");

    /// <summary><c>semantic-kernel</c></summary>
    public static AgentFramework SemanticKernel { get; } = new("semantic-kernel");

    /// <summary><c>agent-framework</c></summary>
    public static AgentFramework MicrosoftAgentFramework { get; } = new("agent-framework");

    /// <inheritdoc />
    public static AgentFramework Create(string value) => new(value);

    /// <summary>Converts a wire value.</summary>
    public static implicit operator AgentFramework(string value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;

    /// <summary>Case-insensitive comparison with another value.</summary>
    public bool Equals(AgentFramework other) => string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value ?? string.Empty);
}

/// <summary>Where the control plane runs an agent process.</summary>
[JsonConverter(typeof(ExtensibleEnumConverter<ExecutionMode>))]
[DebuggerDisplay("{Value}")]
public readonly record struct ExecutionMode(string Value) : IExtensibleEnum<ExecutionMode>
{
    /// <summary><c>local</c></summary>
    public static ExecutionMode Local { get; } = new("local");

    /// <summary><c>docker</c></summary>
    public static ExecutionMode Docker { get; } = new("docker");

    /// <summary><c>k8s</c></summary>
    public static ExecutionMode Kubernetes { get; } = new("k8s");

    /// <inheritdoc />
    public static ExecutionMode Create(string value) => new(value);

    /// <summary>Converts a wire value.</summary>
    public static implicit operator ExecutionMode(string value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;

    /// <summary>Case-insensitive comparison with another value.</summary>
    public bool Equals(ExecutionMode other) => string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value ?? string.Empty);
}

/// <summary>Whether the agent runs an autonomous reasoning loop.</summary>
[JsonConverter(typeof(ExtensibleEnumConverter<AgentBehavior>))]
[DebuggerDisplay("{Value}")]
public readonly record struct AgentBehavior(string Value) : IExtensibleEnum<AgentBehavior>
{
    /// <summary><c>reactive</c></summary>
    public static AgentBehavior Reactive { get; } = new("reactive");

    /// <summary><c>agentic</c></summary>
    public static AgentBehavior Agentic { get; } = new("agentic");

    /// <inheritdoc />
    public static AgentBehavior Create(string value) => new(value);

    /// <summary>Converts a wire value.</summary>
    public static implicit operator AgentBehavior(string value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;

    /// <summary>Case-insensitive comparison with another value.</summary>
    public bool Equals(AgentBehavior other) => string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value ?? string.Empty);
}

/// <summary>Reasoning strategy for agentic agents.</summary>
[JsonConverter(typeof(ExtensibleEnumConverter<ReasoningStrategy>))]
[DebuggerDisplay("{Value}")]
public readonly record struct ReasoningStrategy(string Value) : IExtensibleEnum<ReasoningStrategy>
{
    /// <summary><c>react</c></summary>
    public static ReasoningStrategy React { get; } = new("react");

    /// <summary><c>plan-and-execute</c></summary>
    public static ReasoningStrategy PlanAndExecute { get; } = new("plan-and-execute");

    /// <summary><c>reflexion</c></summary>
    public static ReasoningStrategy Reflexion { get; } = new("reflexion");

    /// <inheritdoc />
    public static ReasoningStrategy Create(string value) => new(value);

    /// <summary>Converts a wire value.</summary>
    public static implicit operator ReasoningStrategy(string value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;

    /// <summary>Case-insensitive comparison with another value.</summary>
    public bool Equals(ReasoningStrategy other) => string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value ?? string.Empty);
}

/// <summary>Kind of an ingredient.</summary>
[JsonConverter(typeof(ExtensibleEnumConverter<IngredientKind>))]
[DebuggerDisplay("{Value}")]
public readonly record struct IngredientKind(string Value) : IExtensibleEnum<IngredientKind>
{
    /// <summary><c>model</c></summary>
    public static IngredientKind Model { get; } = new("model");

    /// <summary><c>tool</c></summary>
    public static IngredientKind Tool { get; } = new("tool");

    /// <summary><c>prompt</c></summary>
    public static IngredientKind Prompt { get; } = new("prompt");

    /// <summary><c>data</c></summary>
    public static IngredientKind Data { get; } = new("data");

    /// <summary><c>observability</c></summary>
    public static IngredientKind Observability { get; } = new("observability");

    /// <summary><c>embedding</c></summary>
    public static IngredientKind Embedding { get; } = new("embedding");

    /// <summary><c>vectorstore</c></summary>
    public static IngredientKind VectorStore { get; } = new("vectorstore");

    /// <summary><c>retriever</c></summary>
    public static IngredientKind Retriever { get; } = new("retriever");

    /// <summary><c>scenario</c></summary>
    public static IngredientKind Scenario { get; } = new("scenario");

    /// <inheritdoc />
    public static IngredientKind Create(string value) => new(value);

    /// <summary>Converts a wire value.</summary>
    public static implicit operator IngredientKind(string value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;

    /// <summary>Case-insensitive comparison with another value.</summary>
    public bool Equals(IngredientKind other) => string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value ?? string.Empty);
}

/// <summary>Built-in guardrail kinds.</summary>
[JsonConverter(typeof(ExtensibleEnumConverter<GuardrailKind>))]
[DebuggerDisplay("{Value}")]
public readonly record struct GuardrailKind(string Value) : IExtensibleEnum<GuardrailKind>
{
    /// <summary><c>content_filter</c></summary>
    public static GuardrailKind ContentFilter { get; } = new("content_filter");

    /// <summary><c>pii_detection</c></summary>
    public static GuardrailKind PiiDetection { get; } = new("pii_detection");

    /// <summary><c>topic_restriction</c></summary>
    public static GuardrailKind TopicRestriction { get; } = new("topic_restriction");

    /// <summary><c>max_length</c></summary>
    public static GuardrailKind MaxLength { get; } = new("max_length");

    /// <summary><c>regex_filter</c></summary>
    public static GuardrailKind RegexFilter { get; } = new("regex_filter");

    /// <summary><c>prompt_injection</c></summary>
    public static GuardrailKind PromptInjection { get; } = new("prompt_injection");

    /// <summary><c>llamaguard</c></summary>
    public static GuardrailKind LlamaGuard { get; } = new("llamaguard");

    /// <summary><c>custom</c></summary>
    public static GuardrailKind Custom { get; } = new("custom");

    /// <inheritdoc />
    public static GuardrailKind Create(string value) => new(value);

    /// <summary>Converts a wire value.</summary>
    public static implicit operator GuardrailKind(string value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;

    /// <summary>Case-insensitive comparison with another value.</summary>
    public bool Equals(GuardrailKind other) => string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value ?? string.Empty);
}

/// <summary>When a guardrail runs.</summary>
[JsonConverter(typeof(ExtensibleEnumConverter<GuardrailStage>))]
[DebuggerDisplay("{Value}")]
public readonly record struct GuardrailStage(string Value) : IExtensibleEnum<GuardrailStage>
{
    /// <summary><c>input</c></summary>
    public static GuardrailStage Input { get; } = new("input");

    /// <summary><c>output</c></summary>
    public static GuardrailStage Output { get; } = new("output");

    /// <summary><c>both</c></summary>
    public static GuardrailStage Both { get; } = new("both");

    /// <inheritdoc />
    public static GuardrailStage Create(string value) => new(value);

    /// <summary>Converts a wire value.</summary>
    public static implicit operator GuardrailStage(string value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;

    /// <summary>Case-insensitive comparison with another value.</summary>
    public bool Equals(GuardrailStage other) => string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value ?? string.Empty);
}

/// <summary>Kind of a recipe step.</summary>
[JsonConverter(typeof(ExtensibleEnumConverter<StepKind>))]
[DebuggerDisplay("{Value}")]
public readonly record struct StepKind(string Value) : IExtensibleEnum<StepKind>
{
    /// <summary><c>agent</c></summary>
    public static StepKind Agent { get; } = new("agent");

    /// <summary><c>human_gate</c></summary>
    public static StepKind HumanGate { get; } = new("human_gate");

    /// <summary><c>evaluator</c></summary>
    public static StepKind Evaluator { get; } = new("evaluator");

    /// <summary><c>condition</c></summary>
    public static StepKind Condition { get; } = new("condition");

    /// <summary><c>fan_out</c></summary>
    public static StepKind FanOut { get; } = new("fan_out");

    /// <summary><c>fan_in</c></summary>
    public static StepKind FanIn { get; } = new("fan_in");

    /// <summary><c>rag</c></summary>
    public static StepKind Rag { get; } = new("rag");

    /// <summary><c>router</c></summary>
    public static StepKind Router { get; } = new("router");

    /// <summary><c>map</c></summary>
    public static StepKind Map { get; } = new("map");

    /// <summary><c>sub_recipe</c></summary>
    public static StepKind SubRecipe { get; } = new("sub_recipe");

    /// <inheritdoc />
    public static StepKind Create(string value) => new(value);

    /// <summary>Converts a wire value.</summary>
    public static implicit operator StepKind(string value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;

    /// <summary>Case-insensitive comparison with another value.</summary>
    public bool Equals(StepKind other) => string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value ?? string.Empty);
}

/// <summary>State of a recipe run.</summary>
[JsonConverter(typeof(ExtensibleEnumConverter<RecipeRunStatus>))]
[DebuggerDisplay("{Value}")]
public readonly record struct RecipeRunStatus(string Value) : IExtensibleEnum<RecipeRunStatus>
{
    /// <summary><c>submitted</c></summary>
    public static RecipeRunStatus Submitted { get; } = new("submitted");

    /// <summary><c>running</c></summary>
    public static RecipeRunStatus Running { get; } = new("running");

    /// <summary><c>paused</c></summary>
    public static RecipeRunStatus Paused { get; } = new("paused");

    /// <summary><c>completed</c></summary>
    public static RecipeRunStatus Completed { get; } = new("completed");

    /// <summary><c>failed</c></summary>
    public static RecipeRunStatus Failed { get; } = new("failed");

    /// <summary><c>canceled</c></summary>
    public static RecipeRunStatus Canceled { get; } = new("canceled");

    /// <inheritdoc />
    public static RecipeRunStatus Create(string value) => new(value);

    /// <summary>Converts a wire value.</summary>
    public static implicit operator RecipeRunStatus(string value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;

    /// <summary>Case-insensitive comparison with another value.</summary>
    public bool Equals(RecipeRunStatus other) => string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value ?? string.Empty);
}

/// <summary>State of one step in a recipe run.</summary>
[JsonConverter(typeof(ExtensibleEnumConverter<StepStatus>))]
[DebuggerDisplay("{Value}")]
public readonly record struct StepStatus(string Value) : IExtensibleEnum<StepStatus>
{
    /// <summary><c>running</c></summary>
    public static StepStatus Running { get; } = new("running");

    /// <summary><c>completed</c></summary>
    public static StepStatus Completed { get; } = new("completed");

    /// <summary><c>failed</c></summary>
    public static StepStatus Failed { get; } = new("failed");

    /// <summary><c>skipped</c></summary>
    public static StepStatus Skipped { get; } = new("skipped");

    /// <summary><c>canceled</c></summary>
    public static StepStatus Canceled { get; } = new("canceled");

    /// <inheritdoc />
    public static StepStatus Create(string value) => new(value);

    /// <summary>Converts a wire value.</summary>
    public static implicit operator StepStatus(string value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;

    /// <summary>Case-insensitive comparison with another value.</summary>
    public bool Equals(StepStatus other) => string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value ?? string.Empty);
}

/// <summary>State of a human gate.</summary>
[JsonConverter(typeof(ExtensibleEnumConverter<GateStatus>))]
[DebuggerDisplay("{Value}")]
public readonly record struct GateStatus(string Value) : IExtensibleEnum<GateStatus>
{
    /// <summary><c>waiting</c></summary>
    public static GateStatus Waiting { get; } = new("waiting");

    /// <summary><c>approved</c></summary>
    public static GateStatus Approved { get; } = new("approved");

    /// <summary><c>rejected</c></summary>
    public static GateStatus Rejected { get; } = new("rejected");

    /// <summary><c>timed_out</c></summary>
    public static GateStatus TimedOut { get; } = new("timed_out");

    /// <inheritdoc />
    public static GateStatus Create(string value) => new(value);

    /// <summary>Converts a wire value.</summary>
    public static implicit operator GateStatus(string value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;

    /// <summary>Case-insensitive comparison with another value.</summary>
    public bool Equals(GateStatus other) => string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value ?? string.Empty);
}

/// <summary>State of a chat session.</summary>
[JsonConverter(typeof(ExtensibleEnumConverter<SessionStatus>))]
[DebuggerDisplay("{Value}")]
public readonly record struct SessionStatus(string Value) : IExtensibleEnum<SessionStatus>
{
    /// <summary><c>active</c></summary>
    public static SessionStatus Active { get; } = new("active");

    /// <summary><c>paused</c></summary>
    public static SessionStatus Paused { get; } = new("paused");

    /// <summary><c>completed</c></summary>
    public static SessionStatus Completed { get; } = new("completed");

    /// <summary><c>expired</c></summary>
    public static SessionStatus Expired { get; } = new("expired");

    /// <inheritdoc />
    public static SessionStatus Create(string value) => new(value);

    /// <summary>Converts a wire value.</summary>
    public static implicit operator SessionStatus(string value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;

    /// <summary>Case-insensitive comparison with another value.</summary>
    public bool Equals(SessionStatus other) => string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value ?? string.Empty);
}

/// <summary>State of an agent process.</summary>
[JsonConverter(typeof(ExtensibleEnumConverter<ProcessStatus>))]
[DebuggerDisplay("{Value}")]
public readonly record struct ProcessStatus(string Value) : IExtensibleEnum<ProcessStatus>
{
    /// <summary><c>starting</c></summary>
    public static ProcessStatus Starting { get; } = new("starting");

    /// <summary><c>running</c></summary>
    public static ProcessStatus Running { get; } = new("running");

    /// <summary><c>stopped</c></summary>
    public static ProcessStatus Stopped { get; } = new("stopped");

    /// <summary><c>failed</c></summary>
    public static ProcessStatus Failed { get; } = new("failed");

    /// <inheritdoc />
    public static ProcessStatus Create(string value) => new(value);

    /// <summary>Converts a wire value.</summary>
    public static implicit operator ProcessStatus(string value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;

    /// <summary>Case-insensitive comparison with another value.</summary>
    public bool Equals(ProcessStatus other) => string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value ?? string.Empty);
}

/// <summary>Model provider kinds.</summary>
[JsonConverter(typeof(ExtensibleEnumConverter<ProviderKind>))]
[DebuggerDisplay("{Value}")]
public readonly record struct ProviderKind(string Value) : IExtensibleEnum<ProviderKind>
{
    /// <summary><c>openai</c></summary>
    public static ProviderKind OpenAI { get; } = new("openai");

    /// <summary><c>azure-openai</c></summary>
    public static ProviderKind AzureOpenAI { get; } = new("azure-openai");

    /// <summary><c>anthropic</c></summary>
    public static ProviderKind Anthropic { get; } = new("anthropic");

    /// <summary><c>gemini</c></summary>
    public static ProviderKind Gemini { get; } = new("gemini");

    /// <summary><c>ollama</c></summary>
    public static ProviderKind Ollama { get; } = new("ollama");

    /// <summary><c>litellm</c></summary>
    public static ProviderKind LiteLLM { get; } = new("litellm");

    /// <summary><c>openrouter</c></summary>
    public static ProviderKind OpenRouter { get; } = new("openrouter");

    /// <summary><c>groq</c></summary>
    public static ProviderKind Groq { get; } = new("groq");

    /// <inheritdoc />
    public static ProviderKind Create(string value) => new(value);

    /// <summary>Converts a wire value.</summary>
    public static implicit operator ProviderKind(string value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;

    /// <summary>Case-insensitive comparison with another value.</summary>
    public bool Equals(ProviderKind other) => string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value ?? string.Empty);
}

/// <summary>AgentOven edition / plan.</summary>
[JsonConverter(typeof(ExtensibleEnumConverter<Plan>))]
[DebuggerDisplay("{Value}")]
public readonly record struct Plan(string Value) : IExtensibleEnum<Plan>
{
    /// <summary><c>community</c></summary>
    public static Plan Community { get; } = new("community");

    /// <summary><c>pro</c></summary>
    public static Plan Pro { get; } = new("pro");

    /// <summary><c>enterprise</c></summary>
    public static Plan Enterprise { get; } = new("enterprise");

    /// <inheritdoc />
    public static Plan Create(string value) => new(value);

    /// <summary>Converts a wire value.</summary>
    public static implicit operator Plan(string value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;

    /// <summary>Case-insensitive comparison with another value.</summary>
    public bool Equals(Plan other) => string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value ?? string.Empty);
}
