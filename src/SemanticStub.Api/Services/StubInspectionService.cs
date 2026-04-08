using Microsoft.Extensions.Options;
using SemanticStub.Api.Inspection;
using SemanticStub.Api.Infrastructure.Yaml;

namespace SemanticStub.Api.Services;

internal sealed class StubInspectionService : IStubInspectionService
{
    private readonly StubDefinitionState state;
    private readonly IStubDefinitionLoader loader;
    private readonly IOptions<StubSettings> settings;
    private readonly ScenarioService scenarioService;
    private readonly IStubService stubService;
    private readonly StubInspectionRuntimeStore runtimeStore = new();

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
        this.scenarioService = scenarioService;
        this.stubService = stubService;
    }

    /// <inheritdoc/>
    public StubConfigSnapshot GetConfigSnapshot()
    {
        var document = state.GetCurrentDocument();
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
        var document = state.GetCurrentDocument();
        return StubInspectionDocumentProjector.BuildRoutes(document);
    }

    /// <inheritdoc/>
    public StubRouteDetailInfo? GetRoute(string routeId)
    {
        ArgumentException.ThrowIfNullOrEmpty(routeId);

        var document = state.GetCurrentDocument();
        return StubInspectionDocumentProjector.FindRoute(document, routeId);
    }

    /// <inheritdoc/>
    public IReadOnlyList<ScenarioStateInfo> GetScenarioStates()
    {
        return scenarioService.ExecuteLocked(() =>
        {
            var document = state.GetCurrentDocument();
            var scenarioNames = StubInspectionDocumentProjector.GetScenarioNames(document);

            return scenarioNames
                .Select(name =>
                {
                    var snapshot = scenarioService.GetSnapshotWithinLock(name);
                    return new ScenarioStateInfo
                    {
                        Name = name,
                        CurrentState = snapshot.State,
                        LastUpdatedTimestamp = snapshot.LastUpdatedTimestamp,
                    };
                })
                .ToList();
        });
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
        return (await stubService.ExplainMatchAsync(request).ConfigureAwait(false)).Result;
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
        scenarioService.ExecuteLocked(() =>
        {
            var document = state.GetCurrentDocument();
            scenarioService.ResetScenariosWithinLock(StubInspectionDocumentProjector.GetScenarioNames(document), DateTimeOffset.UtcNow);
            return 0;
        });
    }

    /// <inheritdoc/>
    public bool ResetScenarioState(string scenarioName)
    {
        return scenarioService.ExecuteLocked(() =>
        {
            var document = state.GetCurrentDocument();
            var scenarioNames = StubInspectionDocumentProjector.GetScenarioNames(document);

            if (!scenarioNames.Contains(scenarioName, StringComparer.Ordinal))
            {
                return false;
            }

            scenarioService.ResetScenarioWithinLock(scenarioName, DateTimeOffset.UtcNow);
            return true;
        });
    }
}
