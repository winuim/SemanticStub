using SemanticStub.Api.Inspection;

namespace SemanticStub.Api.Services;

internal sealed class StubInspectionRuntimeStore
{
    private const int MaxRecentRequestCount = 100;
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

    public MatchExplanationInfo? GetLastMatchExplanation()
    {
        lock (lastMatchSyncRoot)
        {
            return lastMatchExplanation;
        }
    }

    public void RecordLastMatchExplanation(MatchExplanationInfo explanation)
    {
        ArgumentNullException.ThrowIfNull(explanation);

        lock (lastMatchSyncRoot)
        {
            lastMatchExplanation = explanation;
        }
    }

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

    public void ResetMetrics()
    {
        lock (metricsSyncRoot)
        {
            statusCodeCounts.Clear();
            routeRequestCounts.Clear();
            recentRequests.Clear();
            totalRequestCount = 0;
            matchedRequestCount = 0;
            unmatchedRequestCount = 0;
            fallbackResponseCount = 0;
            semanticMatchCount = 0;
            totalLatencyMilliseconds = 0;
        }
    }

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
}
