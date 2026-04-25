namespace SemanticStub.Api.Inspection;

/// <summary>
/// Describes a recorded request in a structured, replay-ready form.
/// Contains only the fields needed to reproduce the request; runtime metadata such as
/// timestamps, match results, and status codes are intentionally excluded.
/// </summary>
public sealed class ReplayReadyRequestInfo
{
    /// <summary>
    /// Gets the HTTP method, normalised to upper case.
    /// </summary>
    public required string Method { get; init; }

    /// <summary>
    /// Gets the absolute request path.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Gets the query parameters captured from the original request, when present.
    /// Multi-value parameters are preserved as arrays.
    /// </summary>
    public IReadOnlyDictionary<string, string[]>? Query { get; init; }

    /// <summary>
    /// Gets the request headers, with transport-only headers removed.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }

    /// <summary>
    /// Gets the request body captured from the original request, when present.
    /// </summary>
    public string? Body { get; init; }
}
