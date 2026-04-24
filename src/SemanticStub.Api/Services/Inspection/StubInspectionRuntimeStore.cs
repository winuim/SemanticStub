using SemanticStub.Api.Inspection;

namespace SemanticStub.Api.Services;

/// <summary>
/// Stores process-wide runtime inspection state such as metrics, recent requests, and the latest match explanation.
/// </summary>
internal sealed class StubInspectionRuntimeStore
{
    private const int MaxRecentRequestCount = 100;
    private const int MaxBodyLengthChars = 4096;

    private static readonly HashSet<string> _sensitiveHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "Cookie",
        "Set-Cookie",
        "Proxy-Authorization",
    };
    private readonly object _lastMatchSyncRoot = new();
    private readonly object _metricsSyncRoot = new();
    private readonly Dictionary<int, long> _statusCodeCounts = [];
    private readonly Dictionary<string, long> _routeRequestCounts = new(StringComparer.Ordinal);
    private readonly Queue<RecentRequestInfo> _recentRequests = [];
    private MatchExplanationInfo? _lastMatchExplanation;
    private long _totalRequestCount;
    private long _matchedRequestCount;
    private long _unmatchedRequestCount;
    private long _fallbackResponseCount;
    private long _semanticMatchCount;
    private double _totalLatencyMilliseconds;

    public MatchExplanationInfo? GetLastMatchExplanation()
    {
        lock (_lastMatchSyncRoot)
        {
            return _lastMatchExplanation;
        }
    }

    public void RecordLastMatchExplanation(MatchExplanationInfo explanation)
    {
        ArgumentNullException.ThrowIfNull(explanation);

        lock (_lastMatchSyncRoot)
        {
            _lastMatchExplanation = explanation;
        }
    }

    public RuntimeMetricsSummaryInfo GetRuntimeMetrics()
    {
        lock (_metricsSyncRoot)
        {
            return new RuntimeMetricsSummaryInfo
            {
                TotalRequestCount = _totalRequestCount,
                MatchedRequestCount = _matchedRequestCount,
                UnmatchedRequestCount = _unmatchedRequestCount,
                FallbackResponseCount = _fallbackResponseCount,
                SemanticMatchCount = _semanticMatchCount,
                AverageLatencyMilliseconds = _totalRequestCount == 0
                    ? 0
                    : _totalLatencyMilliseconds / _totalRequestCount,
                StatusCodes = _statusCodeCounts
                    .OrderByDescending(entry => entry.Value)
                    .ThenBy(entry => entry.Key)
                    .Select(entry => new RuntimeStatusCodeMetricInfo
                    {
                        StatusCode = entry.Key,
                        RequestCount = entry.Value,
                    })
                    .ToList(),
                TopRoutes = _routeRequestCounts
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

        lock (_metricsSyncRoot)
        {
            _totalRequestCount++;

            if (explanation.Result.Matched)
            {
                _matchedRequestCount++;
            }
            else
            {
                _unmatchedRequestCount++;
            }

            if (string.Equals(explanation.Result.MatchMode, "fallback", StringComparison.Ordinal))
            {
                _fallbackResponseCount++;
            }

            if (string.Equals(explanation.Result.MatchMode, "semantic", StringComparison.Ordinal))
            {
                _semanticMatchCount++;
            }

            _totalLatencyMilliseconds += Math.Max(0, elapsed.TotalMilliseconds);
            _statusCodeCounts[statusCode] = _statusCodeCounts.GetValueOrDefault(statusCode) + 1;

            if (!string.IsNullOrEmpty(explanation.Result.RouteId))
            {
                _routeRequestCounts[explanation.Result.RouteId] = _routeRequestCounts.GetValueOrDefault(explanation.Result.RouteId) + 1;
            }
        }
    }

    public void ResetMetrics()
    {
        lock (_metricsSyncRoot)
        {
            _statusCodeCounts.Clear();
            _routeRequestCounts.Clear();
            _recentRequests.Clear();
            _totalRequestCount = 0;
            _matchedRequestCount = 0;
            _unmatchedRequestCount = 0;
            _fallbackResponseCount = 0;
            _semanticMatchCount = 0;
            _totalLatencyMilliseconds = 0;
        }
    }

    public IReadOnlyList<RecentRequestInfo> GetRecentRequests(int limit)
    {
        lock (_metricsSyncRoot)
        {
            var normalizedLimit = Math.Clamp(limit, 0, MaxRecentRequestCount);

            if (normalizedLimit == 0)
            {
                return [];
            }

            return _recentRequests
                .Reverse()
                .Take(normalizedLimit)
                .ToList();
        }
    }

    public void RecordRecentRequest(
        DateTimeOffset timestamp,
        string method,
        string path,
        MatchExplanationInfo explanation,
        int statusCode,
        TimeSpan elapsed,
        IReadOnlyDictionary<string, string[]>? query = null,
        IReadOnlyDictionary<string, string>? headers = null,
        string? body = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(method);
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(explanation);

        lock (_metricsSyncRoot)
        {
            if (_recentRequests.Count >= MaxRecentRequestCount)
            {
                _recentRequests.Dequeue();
            }

            _recentRequests.Enqueue(new RecentRequestInfo
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
                Query = query,
                Headers = RedactSensitiveHeaders(headers),
                Body = TruncateBody(body),
            });
        }
    }

    private static IReadOnlyDictionary<string, string>? RedactSensitiveHeaders(IReadOnlyDictionary<string, string>? headers)
    {
        if (headers is null || headers.Count == 0)
        {
            return headers;
        }

        Dictionary<string, string>? redacted = null;

        foreach (var (key, value) in headers)
        {
            if (_sensitiveHeaders.Contains(key))
            {
                redacted ??= new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase);
                redacted[key] = "[redacted]";
            }
        }

        return redacted ?? headers;
    }

    private static string? TruncateBody(string? body)
    {
        if (string.IsNullOrEmpty(body) || body.Length <= MaxBodyLengthChars)
        {
            return body;
        }

        return body[..MaxBodyLengthChars] + "...[truncated]";
    }
}
