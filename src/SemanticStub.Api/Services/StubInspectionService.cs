using Microsoft.Extensions.Options;
using SemanticStub.Api.Inspection;
using SemanticStub.Api.Infrastructure.Yaml;
using SemanticStub.Api.Models;

namespace SemanticStub.Api.Services;

internal sealed class StubInspectionService : IStubInspectionService
{
    private readonly StubDefinitionState state;
    private readonly IStubDefinitionLoader loader;
    private readonly IOptions<StubSettings> settings;
    private readonly IStubService stubService;
    private readonly StubInspectionRuntimeStore runtimeStore = new();
    private readonly StubInspectionScenarioCoordinator scenarioCoordinator;

    public StubInspectionService(
        StubDefinitionState state,
        IStubDefinitionLoader loader,
        IOptions<StubSettings> settings,
        ScenarioService scenarioService,
        IStubService stubService)
    {
        this.state = state;
        this.loader = loader;
        this.settings = settings;
        this.stubService = stubService;
        scenarioCoordinator = new StubInspectionScenarioCoordinator(state, scenarioService);
    }

    /// <inheritdoc/>
    public StubConfigSnapshot GetConfigSnapshot()
    {
        var document = GetCurrentDocument();
        var routes = StubInspectionDocumentProjector.BuildRoutes(document);

        return new StubConfigSnapshot
        {
            SnapshotTimestamp = DateTimeOffset.UtcNow,
            ConfigurationHash = StubInspectionDocumentProjector.ComputeDocumentHash(document),
            DefinitionsDirectoryPath = loader.GetDefinitionsDirectoryPath(),
            RouteCount = routes.Count,
            SemanticMatchingEnabled = settings.Value.SemanticMatching.Enabled,
        };
    }

    /// <inheritdoc/>
    public IReadOnlyList<StubRouteInfo> GetRoutes()
    {
        return StubInspectionDocumentProjector.BuildRoutes(GetCurrentDocument());
    }

    /// <inheritdoc/>
    public StubRouteDetailInfo? GetRoute(string routeId)
    {
        ArgumentException.ThrowIfNullOrEmpty(routeId);

        return StubInspectionDocumentProjector.FindRoute(GetCurrentDocument(), routeId);
    }

    /// <inheritdoc/>
    public IReadOnlyList<ScenarioStateInfo> GetScenarioStates()
    {
        return scenarioCoordinator.GetScenarioStates();
    }

    /// <inheritdoc/>
    public RuntimeMetricsSummaryInfo GetRuntimeMetrics()
    {
        return runtimeStore.GetRuntimeMetrics();
    }

    /// <inheritdoc/>
    public IReadOnlyList<RecentRequestInfo> GetRecentRequests(int limit)
    {
        return runtimeStore.GetRecentRequests(limit);
    }

    /// <inheritdoc/>
    public async Task<MatchSimulationInfo> TestMatchAsync(MatchRequestInfo request)
    {
        var explanation = await stubService.ExplainMatchAsync(request).ConfigureAwait(false);
        return explanation.Result;
    }

    /// <inheritdoc/>
    public Task<MatchExplanationInfo> ExplainMatchAsync(MatchRequestInfo request)
    {
        return stubService.ExplainMatchAsync(request);
    }

    /// <inheritdoc/>
    public MatchExplanationInfo? GetLastMatchExplanation()
    {
        return runtimeStore.GetLastMatchExplanation();
    }

    /// <inheritdoc/>
    public void RecordLastMatchExplanation(MatchExplanationInfo explanation)
    {
        runtimeStore.RecordLastMatchExplanation(explanation);
    }

    /// <inheritdoc/>
    public void RecordRequestMetrics(MatchExplanationInfo explanation, int statusCode, TimeSpan elapsed)
    {
        runtimeStore.RecordRequestMetrics(explanation, statusCode, elapsed);
    }

    /// <inheritdoc/>
    public void RecordRecentRequest(DateTimeOffset timestamp, string method, string path, MatchExplanationInfo explanation, int statusCode, TimeSpan elapsed)
    {
        runtimeStore.RecordRecentRequest(timestamp, method, path, explanation, statusCode, elapsed);
    }

    /// <inheritdoc/>
    public void ResetScenarioStates()
    {
        scenarioCoordinator.ResetScenarioStates();
    }

    /// <inheritdoc/>
    public bool ResetScenarioState(string scenarioName)
    {
        return scenarioCoordinator.ResetScenarioState(scenarioName);
    }

    private StubDocument GetCurrentDocument()
    {
        return state.GetCurrentDocument();
    }
}
