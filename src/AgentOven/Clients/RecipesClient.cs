using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using AgentOven.Internal;

namespace AgentOven;

/// <summary>Recipe (workflow) operations. Get one from <see cref="AgentOvenClient.Recipes"/>.</summary>
public sealed class RecipesClient
{
    private readonly AgentOvenHttp _http;

    internal RecipesClient(AgentOvenHttp http) => _http = http;

    /// <summary>Lists recipes.</summary>
    public Task<IReadOnlyList<Recipe>> ListAsync(CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("recipes"), AgentOvenJsonContext.Default.IReadOnlyListRecipe, cancellationToken);

    /// <summary>Gets a recipe.</summary>
    public Task<Recipe> GetAsync(string name, CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("recipes", name), AgentOvenJsonContext.Default.Recipe, cancellationToken);

    /// <summary>Gets a recipe, or <see langword="null"/> when it does not exist.</summary>
    public async Task<Recipe?> FindAsync(string name, CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetAsync(name, cancellationToken).ConfigureAwait(false);
        }
        catch (AgentOvenApiException ex) when (ex.IsNotFound)
        {
            return null;
        }
    }

    /// <summary>Creates a recipe. Creating an existing name replaces it.</summary>
    public Task<Recipe> CreateAsync(Recipe recipe, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        return _http.PostAsync(
            ApiPath.Of("recipes"), recipe, AgentOvenJsonContext.Default.Recipe, AgentOvenJsonContext.Default.Recipe, cancellationToken);
    }

    /// <summary>Updates a recipe's description and (when non-empty) its steps.</summary>
    public Task<Recipe> UpdateAsync(Recipe recipe, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        return _http.PutAsync(
            ApiPath.Of("recipes", recipe.Name), recipe, AgentOvenJsonContext.Default.Recipe, AgentOvenJsonContext.Default.Recipe, cancellationToken);
    }

    /// <summary>Deletes a recipe.</summary>
    public Task DeleteAsync(string name, CancellationToken cancellationToken = default) =>
        _http.DeleteAsync(ApiPath.Of("recipes", name), cancellationToken);

    /// <summary>Starts a run. Returns as soon as it is accepted; use <see cref="WaitForCompletionAsync(RecipeRunStarted, TimeSpan?, bool, CancellationToken)"/> to wait.</summary>
    public Task<RecipeRunStarted> BakeAsync(string name, JsonNode? input = null, string? environment = null, CancellationToken cancellationToken = default) =>
        _http.PostAsync(
            ApiPath.Of("recipes", name, "bake"),
            new RecipeBakeRequest { Input = input, Environment = environment },
            InternalJsonContext.Default.RecipeBakeRequest,
            AgentOvenJsonContext.Default.RecipeRunStarted,
            cancellationToken);

    /// <summary>Starts a run with a typed input, serialized with source-generated metadata (trim/AOT safe).</summary>
    public Task<RecipeRunStarted> BakeAsync<TInput>(
        string name, TInput input, JsonTypeInfo<TInput> inputType, string? environment = null, CancellationToken cancellationToken = default) =>
        BakeAsync(name, JsonSerializer.SerializeToNode(input, inputType), environment, cancellationToken);

    /// <summary>Starts a run with any input object, for example <c>new { contractId = 42 }</c>. Uses reflection.</summary>
    [RequiresUnreferencedCode("Serializes the input with reflection. Use the JsonTypeInfo overload when trimming.")]
    [RequiresDynamicCode("Serializes the input with reflection. Use the JsonTypeInfo overload for native AOT.")]
    public Task<RecipeRunStarted> BakeAsync(string name, object input, string? environment = null, CancellationToken cancellationToken = default) =>
        BakeAsync(name, input as JsonNode ?? JsonSerializer.SerializeToNode(input, ReflectionOptions), environment, cancellationToken);

    /// <summary>Lists the last 50 runs of a recipe.</summary>
    public Task<IReadOnlyList<RecipeRun>> ListRunsAsync(string name, CancellationToken cancellationToken = default) =>
        _http.GetAsync(ApiPath.Of("recipes", name, "runs"), AgentOvenJsonContext.Default.IReadOnlyListRecipeRun, cancellationToken);

    /// <summary>Gets a run, including <see cref="RecipeRun.PendingGates"/> when it is paused.</summary>
    public async Task<RecipeRun> GetRunAsync(string name, string runId, CancellationToken cancellationToken = default)
    {
        var envelope = await _http.GetAsync(ApiPath.Of("recipes", name, "runs", runId), InternalJsonContext.Default.RecipeRunEnvelope, cancellationToken)
            .ConfigureAwait(false);
        var run = envelope.Run ?? throw new AgentOvenException($"The server returned no run for '{runId}'.");
        return run with { PendingGates = envelope.PendingGates ?? [] };
    }

    /// <summary>Cancels a run.</summary>
    public Task CancelRunAsync(string name, string runId, CancellationToken cancellationToken = default) =>
        _http.SendAsync(HttpMethod.Post, ApiPath.Of("recipes", name, "runs", runId, "cancel"), content: null, cancellationToken);

    /// <summary>Approves a human gate of a paused run.</summary>
    public Task ApproveGateAsync(string name, string runId, string stepName, CancellationToken cancellationToken = default) =>
        DecideGateAsync(name, runId, stepName, approved: true, cancellationToken);

    /// <summary>Rejects a human gate of a paused run.</summary>
    public Task RejectGateAsync(string name, string runId, string stepName, CancellationToken cancellationToken = default) =>
        DecideGateAsync(name, runId, stepName, approved: false, cancellationToken);

    /// <summary>
    /// Polls a run until it completes, fails or is canceled, and returns it. When
    /// <paramref name="returnWhenPaused"/> is <see langword="true"/> (default), it also returns when the run
    /// pauses on a human gate, so you can approve it.
    /// </summary>
    public async Task<RecipeRun> WaitForCompletionAsync(
        string name,
        string runId,
        TimeSpan? pollInterval = null,
        bool returnWhenPaused = true,
        CancellationToken cancellationToken = default)
    {
        var delay = pollInterval ?? TimeSpan.FromSeconds(2);
        while (true)
        {
            var run = await GetRunAsync(name, runId, cancellationToken).ConfigureAwait(false);
            if (run.IsFinished || (returnWhenPaused && run.Status == RecipeRunStatus.Paused))
            {
                return run;
            }

            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Polls a run started by <see cref="BakeAsync(string, JsonNode?, string?, CancellationToken)"/>.</summary>
    public Task<RecipeRun> WaitForCompletionAsync(
        RecipeRunStarted run, TimeSpan? pollInterval = null, bool returnWhenPaused = true, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        return WaitForCompletionAsync(run.Recipe, run.RunId, pollInterval, returnWhenPaused, cancellationToken);
    }

    private Task DecideGateAsync(string name, string runId, string stepName, bool approved, CancellationToken cancellationToken) =>
        _http.SendAsync(
            HttpMethod.Post,
            ApiPath.Of("recipes", name, "runs", runId, "gates", stepName, "approve"),
            AgentOvenHttp.Json(new GateDecision(approved), InternalJsonContext.Default.GateDecision),
            cancellationToken);

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Only used by the reflection overload, which is annotated.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Only used by the reflection overload, which is annotated.")]
    private static readonly JsonSerializerOptions ReflectionOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };
}
