namespace SemanticStub.Api.Inspection;

/// <summary>
/// Describes the number of requests that produced a specific HTTP status code.
/// </summary>
public sealed class RuntimeStatusCodeMetricInfo
{
    /// <summary>
    /// Gets the HTTP status code.
    /// </summary>
    public int StatusCode { get; init; }

    /// <summary>
    /// Gets the number of requests that produced the status code.
    /// </summary>
    public long RequestCount { get; init; }
}
