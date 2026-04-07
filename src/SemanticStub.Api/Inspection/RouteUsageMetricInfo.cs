namespace SemanticStub.Api.Inspection;

/// <summary>
/// Describes how often a resolved runtime route has been exercised by real requests.
/// </summary>
public sealed class RouteUsageMetricInfo
{
    /// <summary>
    /// Gets the stable route identifier.
    /// </summary>
    public required string RouteId { get; init; }

    /// <summary>
    /// Gets the number of requests observed for the route.
    /// </summary>
    public long RequestCount { get; init; }
}
