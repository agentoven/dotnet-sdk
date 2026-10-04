using System.Text.Json.Nodes;
using AgentOven.Internal;

namespace AgentOven;

/// <summary>Model provider operations. Get one from <see cref="AgentOvenClient.Providers"/>.</summary>
public sealed class ProvidersClient
{
    private readonly AgentOvenHttp _http;

    internal ProvidersClient(AgentOvenHttp http) => _http = http;

    /// <summary>Lists providers. API keys come back masked.</summary>
    public Task<IReadOnlyList<ModelProvider>> ListAsync(CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("models", "providers"), AgentOvenJsonContext.Default.IReadOnlyListModelProvider, cancellationToken);

    /// <summary>Gets a provider.</summary>
    public Task<ModelProvider> GetAsync(string name, CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("models", "providers", name), AgentOvenJsonContext.Default.ModelProvider, cancellationToken);

    /// <summary>Registers a provider.</summary>
    public Task<ModelProvider> AddAsync(ModelProvider provider, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return _http.PostAsync(
            ApiPath.Of("models", "providers"),
            provider,
            AgentOvenJsonContext.Default.ModelProvider,
            AgentOvenJsonContext.Default.ModelProvider,
            cancellationToken);
    }

    /// <summary>
    /// Registers a provider from its parts, like the Python SDK's <c>register_provider</c>:
    /// <c>await oven.Providers.AddAsync("openai", ProviderKind.OpenAI, apiKey: key, models: ["gpt-4o"]);</c>
    /// </summary>
    public Task<ModelProvider> AddAsync(
        string name,
        ProviderKind kind,
        string? apiKey = null,
        string? endpoint = null,
        IEnumerable<string>? models = null,
        bool isDefault = false,
        CancellationToken cancellationToken = default)
    {
        JsonObject? config = apiKey is null ? null : new JsonObject { ["api_key"] = apiKey };
        return AddAsync(
            new ModelProvider(name, kind) { Endpoint = endpoint, Models = models?.ToList(), Config = config, IsDefault = isDefault },
            cancellationToken);
    }

    /// <summary>
    /// Updates a provider. <see cref="ModelProvider.IsDefault"/> is always applied; config keys are merged.
    /// Ready agents using the provider are marked burnt and must be re-baked.
    /// </summary>
    public Task<ProviderUpdateResult> UpdateAsync(ModelProvider provider, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return _http.PutAsync(
            ApiPath.Of("models", "providers", provider.Name),
            provider,
            AgentOvenJsonContext.Default.ModelProvider,
            AgentOvenJsonContext.Default.ProviderUpdateResult,
            cancellationToken);
    }

    /// <summary>Deletes a provider.</summary>
    public Task DeleteAsync(string name, CancellationToken cancellationToken = default) =>
        _http.DeleteAsync(ApiPath.Of("models", "providers", name), cancellationToken);

    /// <summary>Checks that the provider answers. An unhealthy provider is a result, not an exception.</summary>
    public Task<ProviderTestResult> TestAsync(string name, CancellationToken cancellationToken = default) =>
        _http.PostAsync(ApiPath.Of("models", "providers", name, "test"), AgentOvenJsonContext.Default.ProviderTestResult, cancellationToken);

    /// <summary>Asks the provider which models it offers.</summary>
    public Task<ProviderDiscoveryResult> DiscoverModelsAsync(string name, CancellationToken cancellationToken = default) =>
        _http.PostAsync(
            ApiPath.Of("models", "providers", name, "discover"), AgentOvenJsonContext.Default.ProviderDiscoveryResult, cancellationToken);

    /// <summary>Lists provider templates (kinds, default endpoints and models).</summary>
    public Task<IReadOnlyList<ProviderTemplate>> ListTemplatesAsync(CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("models", "providers", "templates"), AgentOvenJsonContext.Default.IReadOnlyListProviderTemplate, cancellationToken);
}

/// <summary>Kitchen (workspace) operations. Get one from <see cref="AgentOvenClient.Kitchens"/>.</summary>
public sealed class KitchensClient
{
    private readonly AgentOvenHttp _http;

    internal KitchensClient(AgentOvenHttp http) => _http = http;

    /// <summary>Lists kitchens.</summary>
    public Task<IReadOnlyList<Kitchen>> ListAsync(CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("kitchens"), AgentOvenJsonContext.Default.IReadOnlyListKitchen, cancellationToken);

    /// <summary>Gets a kitchen by id (not name).</summary>
    public Task<Kitchen> GetAsync(string kitchenId, CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("kitchens", kitchenId), AgentOvenJsonContext.Default.Kitchen, cancellationToken);

    /// <summary>Creates a kitchen.</summary>
    public Task<Kitchen> CreateAsync(
        string name, string? description = null, IReadOnlyDictionary<string, string>? tags = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _http.PostAsync(
            ApiPath.Of("kitchens"),
            new KitchenCreateRequest(name) { Description = description, Tags = tags },
            InternalJsonContext.Default.KitchenCreateRequest,
            AgentOvenJsonContext.Default.Kitchen,
            cancellationToken);
    }

    /// <summary>Deletes a kitchen by id. The <c>default</c> kitchen cannot be deleted.</summary>
    public Task DeleteAsync(string kitchenId, CancellationToken cancellationToken = default) =>
        _http.DeleteAsync(ApiPath.Of("kitchens", kitchenId), cancellationToken);
}
