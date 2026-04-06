namespace SemanticStub.Api.Inspection;

/// <summary>
/// Describes a virtual request evaluated by the runtime inspection APIs.
/// </summary>
public sealed class MatchRequestInfo
{
    /// <summary>
    /// Gets the HTTP method to evaluate.
    /// </summary>
    public string Method { get; init; } = string.Empty;

    /// <summary>
    /// Gets the absolute request path to evaluate.
    /// </summary>
    public string Path { get; init; } = "/";

    /// <summary>
    /// Gets the optional query parameters keyed by parameter name.
    /// </summary>
    public Dictionary<string, string[]> Query { get; init; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the optional request headers keyed by header name.
    /// </summary>
    public Dictionary<string, string> Headers { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the optional raw request body.
    /// </summary>
    public string? Body { get; init; }

    /// <summary>
    /// Gets whether candidate-level evaluation details should be included.
    /// </summary>
    public bool IncludeCandidates { get; init; }

    /// <summary>
    /// Gets whether semantic candidate scores should be included when semantic matching is attempted.
    /// </summary>
    public bool IncludeSemanticCandidates { get; init; }
}
