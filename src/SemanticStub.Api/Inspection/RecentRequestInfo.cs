namespace SemanticStub.Api.Inspection;

/// <summary>
/// Describes one recently handled real request observed by the runtime.
/// </summary>
public sealed class RecentRequestInfo
{
    /// <summary>
    /// Gets the UTC timestamp when the request observation was recorded.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Gets the HTTP method used by the request.
    /// </summary>
    public required string Method { get; init; }

    /// <summary>
    /// Gets the absolute request path handled by the runtime.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Gets the resolved route identifier when the request matched a configured route.
    /// </summary>
    public string? RouteId { get; init; }

    /// <summary>
    /// Gets the final HTTP status code returned to the caller.
    /// </summary>
    public int StatusCode { get; init; }

    /// <summary>
    /// Gets the observed end-to-end elapsed time in milliseconds.
    /// </summary>
    public double ElapsedMilliseconds { get; init; }

    /// <summary>
    /// Gets the match mode when a response was selected.
    /// </summary>
    public string? MatchMode { get; init; }

    /// <summary>
    /// Gets the failure reason when the request did not produce a matched response.
    /// </summary>
    public string? FailureReason { get; init; }

    /// <summary>
    /// Gets the query parameters captured from the original request, when available.
    /// </summary>
    public IReadOnlyDictionary<string, string[]>? Query { get; init; }

    /// <summary>
    /// Gets the request headers captured from the original request, when available.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }

    /// <summary>
    /// Gets the request body captured from the original request, when present.
    /// </summary>
    public string? Body { get; init; }
}
