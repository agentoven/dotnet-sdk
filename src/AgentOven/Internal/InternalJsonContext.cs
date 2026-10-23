using System.Text.Json.Serialization;

namespace AgentOven.Internal;

/// <summary>Source-generated JSON metadata for internal request and wrapper bodies.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(InvokeRequest))]
[JsonSerializable(typeof(TestRequest))]
[JsonSerializable(typeof(RecipeBakeRequest))]
[JsonSerializable(typeof(RecipeRunEnvelope))]
[JsonSerializable(typeof(GateDecision))]
[JsonSerializable(typeof(SessionMessageRequest))]
[JsonSerializable(typeof(KitchenCreateRequest))]
[JsonSerializable(typeof(ServerHealth))]
[JsonSerializable(typeof(ResumeRequest))]
[JsonSerializable(typeof(UnfinishedRunsEnvelope))]
[JsonSerializable(typeof(SkillsEnvelope))]
[JsonSerializable(typeof(SkillRegisterRequest))]
[JsonSerializable(typeof(SkillDecision))]
[JsonSerializable(typeof(UploadBegin))]
[JsonSerializable(typeof(UploadState))]
[JsonSerializable(typeof(ModelCatalogEnvelope))]
[JsonSerializable(typeof(PromptValidationEnvelope))]
[JsonSerializable(typeof(CountEnvelope))]
[JsonSerializable(typeof(ScopedKeyCreateRequest))]
[JsonSerializable(typeof(RealtimeTokenRequest))]
[JsonSerializable(typeof(RagIngestRequest))]
internal sealed partial class InternalJsonContext : JsonSerializerContext;
