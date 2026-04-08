using Microsoft.Extensions.Options;
using SemanticStub.Api.Inspection;
using SemanticStub.Api.Infrastructure.Yaml;

namespace SemanticStub.Api.Services;

internal sealed class StubInspectionService : IStubInspectionService
{
    private const int MaxRecentRequestCount = 100;
    private readonly StubDefinitionState state;
    private readonly IStubDefinitionLoader loader;
    private readonly IOptions<StubSettings> settings;
    private readonly ScenarioService scenarioService;
    private readonly IStubService stubService;
    private readonly object lastMatchSyncRoot = new();
    private readonly object metricsSyncRoot = new();
    private readonly Dictionary<int, long> statusCodeCounts = [];
    private readonly Dictionary<string, long> routeRequestCounts = new(StringComparer.Ordinal);
    private readonly Queue<RecentRequestInfo> recentRequests = [];
    private MatchExplanationInfo? lastMatchExplanation;
    private long totalRequestCount;
    private long matchedRequestCount;
    private long unmatchedRequestCount;
    private long fallbackResponseCount;
    private long semanticMatchCount;
    private double totalLatencyMilliseconds;

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
        lock (metricsSyncRoot)
        {
            return new RuntimeMetricsSummaryInfo
            {
                TotalRequestCount = totalRequestCount,
                MatchedRequestCount = matchedRequestCount,
                UnmatchedRequestCount = unmatchedRequestCount,
                FallbackResponseCount = fallbackResponseCount,
                SemanticMatchCount = semanticMatchCount,
                AverageLatencyMilliseconds = totalRequestCount == 0
                    ? 0
                    : totalLatencyMilliseconds / totalRequestCount,
                StatusCodes = statusCodeCounts
                    .OrderByDescending(entry => entry.Value)
                    .ThenBy(entry => entry.Key)
                    .Select(entry => new RuntimeStatusCodeMetricInfo
                    {
                        StatusCode = entry.Key,
                        RequestCount = entry.Value,
                    })
                    .ToList(),
                TopRoutes = routeRequestCounts
                    .OrderByDescending(entry => entry.Value)
                    .ThenBy(entry => entry.Key, StringComparer.Ordinal)
                    .Select(entry => new RouteUsageMetricInfo
                    {
                        RouteId = entry.Key,
                        RequestCount = entry.Value,
                    })
                    .ToList(),
            };
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<RecentRequestInfo> GetRecentRequests(int limit)
    {
        lock (metricsSyncRoot)
        {
            var normalizedLimit = Math.Clamp(limit, 0, MaxRecentRequestCount);

            if (normalizedLimit == 0)
            {
                return [];
            }

            return recentRequests
                .Reverse()
                .Take(normalizedLimit)
                .ToList();
        }
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
        lock (lastMatchSyncRoot)
        {
            return lastMatchExplanation;
        }
    }

    /// <inheritdoc/>
    public void RecordLastMatchExplanation(MatchExplanationInfo explanation)
    {
        lock (lastMatchSyncRoot)
        {
            lastMatchExplanation = explanation;
        }
    }

    /// <inheritdoc/>
    public void RecordRequestMetrics(MatchExplanationInfo explanation, int statusCode, TimeSpan elapsed)
    {
        ArgumentNullException.ThrowIfNull(explanation);

        lock (metricsSyncRoot)
        {
            totalRequestCount++;

            if (explanation.Result.Matched)
            {
                matchedRequestCount++;
            }
            else
            {
                unmatchedRequestCount++;
            }

            if (string.Equals(explanation.Result.MatchMode, "fallback", StringComparison.Ordinal))
            {
                fallbackResponseCount++;
            }

            if (string.Equals(explanation.Result.MatchMode, "semantic", StringComparison.Ordinal))
            {
                semanticMatchCount++;
            }

            totalLatencyMilliseconds += Math.Max(0, elapsed.TotalMilliseconds);

            statusCodeCounts[statusCode] = statusCodeCounts.GetValueOrDefault(statusCode) + 1;

            if (!string.IsNullOrEmpty(explanation.Result.RouteId))
            {
                routeRequestCounts[explanation.Result.RouteId] = routeRequestCounts.GetValueOrDefault(explanation.Result.RouteId) + 1;
            }
        }
    }

    /// <inheritdoc/>
    public void RecordRecentRequest(DateTimeOffset timestamp, string method, string path, MatchExplanationInfo explanation, int statusCode, TimeSpan elapsed)
    {
        ArgumentException.ThrowIfNullOrEmpty(method);
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(explanation);

        lock (metricsSyncRoot)
        {
            if (recentRequests.Count >= MaxRecentRequestCount)
            {
                recentRequests.Dequeue();
            }

            recentRequests.Enqueue(new RecentRequestInfo
            {
                Timestamp = timestamp,
                Method = method,
                Path = path,
                RouteId = explanation.Result.RouteId,
                StatusCode = statusCode,
                ElapsedMilliseconds = Math.Max(0, elapsed.TotalMilliseconds),
                MatchMode = explanation.Result.MatchMode,
                FailureReason = explanation.Result.Matched || string.IsNullOrWhiteSpace(explanation.SelectionReason)
                    ? null
                    : explanation.SelectionReason,
            });
        }
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
