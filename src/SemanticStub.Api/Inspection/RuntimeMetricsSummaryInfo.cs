namespace SemanticStub.Api.Inspection;

/// <summary>
/// Describes aggregate runtime metrics captured for real requests handled by the current process.
/// </summary>
public sealed class RuntimeMetricsSummaryInfo
{
    /// <summary>
    /// Gets the total number of real requests recorded by the runtime metrics collector.
    /// </summary>
    public long TotalRequestCount { get; init; }

    /// <summary>
    /// Gets the number of requests that produced a matched stub response.
    /// </summary>
    public long MatchedRequestCount { get; init; }

    /// <summary>
    /// Gets the number of requests that did not produce a matched stub response.
    /// </summary>
    public long UnmatchedRequestCount { get; init; }

    /// <summary>
    /// Gets the number of requests that fell back to the default route response.
    /// </summary>
    public long FallbackResponseCount { get; init; }

    /// <summary>
    /// Gets the number of requests that used semantic matching to select a response.
    /// </summary>
    public long SemanticMatchCount { get; init; }

    /// <summary>
    /// Gets the average observed end-to-end latency in milliseconds.
    /// </summary>
    public double AverageLatencyMilliseconds { get; init; }

    /// <summary>
    /// Gets the status-code summary ordered by request count descending.
    /// </summary>
    public IReadOnlyList<RuntimeStatusCodeMetricInfo> StatusCodes { get; init; } = [];

    /// <summary>
    /// Gets the route usage summary ordered by request count descending.
    /// </summary>
    public IReadOnlyList<RouteUsageMetricInfo> TopRoutes { get; init; } = [];
}
