using Microsoft.Extensions.Options;
using SemanticStub.Api.Inspection;
using SemanticStub.Application.Infrastructure.Yaml;
using SemanticStub.Infrastructure.Yaml;
using SemanticStub.Api.Models;
using SemanticStub.Application.Models;

namespace SemanticStub.Api.Services;

internal sealed class StubInspectionService : IStubInspectionService
{
    private readonly StubDefinitionState _state;
    private readonly IStubDefinitionLoader _loader;
    private readonly IOptions<StubSettings> _settings;
    private readonly IStubService _stubService;
    private readonly StubInspectionRuntimeStore _runtimeStore;
    private readonly StubInspectionScenarioCoordinator _scenarioCoordinator;

    public StubInspectionService(
        StubDefinitionState state,
        IStubDefinitionLoader loader,
        IOptions<StubSettings> settings,
        IStubService stubService,
        StubInspectionRuntimeStore runtimeStore,
        StubInspectionScenarioCoordinator scenarioCoordinator)
    {
        _state = state;
        _loader = loader;
        _settings = settings;
        _stubService = stubService;
        _runtimeStore = runtimeStore;
        _scenarioCoordinator = scenarioCoordinator;
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
            DefinitionsDirectoryPath = _loader.GetDefinitionsDirectoryPath(),
            RouteCount = routes.Count,
            SemanticMatchingEnabled = _settings.Value.SemanticMatching.Enabled,
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
        return _scenarioCoordinator.GetScenarioStates();
    }

    /// <inheritdoc/>
    public RuntimeMetricsSummaryInfo GetRuntimeMetrics()
    {
        return _runtimeStore.GetRuntimeMetrics();
    }

    /// <inheritdoc/>
    public IReadOnlyList<RecentRequestInfo> GetRecentRequests(int limit)
    {
        return _runtimeStore.GetRecentRequests(limit);
    }

    /// <inheritdoc/>
    public async Task<MatchSimulationInfo> TestMatchAsync(MatchRequestInfo request, CancellationToken cancellationToken = default)
    {
        var explanation = await _stubService.ExplainMatchAsync(request, cancellationToken);
        return explanation.Result;
    }

    /// <inheritdoc/>
    public Task<MatchExplanationInfo> ExplainMatchAsync(MatchRequestInfo request, CancellationToken cancellationToken = default)
    {
        return _stubService.ExplainMatchAsync(request, cancellationToken);
    }

    /// <inheritdoc/>
    public MatchExplanationInfo? GetLastMatchExplanation()
    {
        return _runtimeStore.GetLastMatchExplanation();
    }

    /// <inheritdoc/>
    public void RecordLastMatchExplanation(MatchExplanationInfo explanation)
    {
        _runtimeStore.RecordLastMatchExplanation(explanation);
    }

    /// <inheritdoc/>
    public void RecordRequestMetrics(MatchExplanationInfo explanation, int statusCode, TimeSpan elapsed)
    {
        _runtimeStore.RecordRequestMetrics(explanation, statusCode, elapsed);
    }

    /// <inheritdoc/>
    public void RecordRecentRequest(DateTimeOffset timestamp, string method, string path, MatchExplanationInfo explanation, int statusCode, TimeSpan elapsed)
    {
        _runtimeStore.RecordRecentRequest(timestamp, method, path, explanation, statusCode, elapsed);
    }

    /// <inheritdoc/>
    public void ResetRuntimeMetrics()
    {
        _runtimeStore.ResetMetrics();
    }

    /// <inheritdoc/>
    public void ResetScenarioStates()
    {
        _scenarioCoordinator.ResetScenarioStates();
    }

    /// <inheritdoc/>
    public bool ResetScenarioState(string scenarioName)
    {
        return _scenarioCoordinator.ResetScenarioState(scenarioName);
    }

    private StubDocument GetCurrentDocument()
    {
        return _state.GetCurrentDocument();
    }
}
