namespace SemanticStub.Application.Services;

/// <summary>
/// Describes why a single dimension of an <c>x-match</c> candidate failed to match.
/// </summary>
public sealed class MatchDimensionMismatch
{
    /// <summary>
    /// Gets the matching dimension that failed. One of <c>query</c>, <c>header</c>, or <c>body</c>.
    /// </summary>
    public string Dimension { get; init; } = string.Empty;

    /// <summary>
    /// Gets the parameter name, header name, or JSON body path that did not satisfy the configured constraint.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// Gets the expected value or pattern declared in the stub definition.
    /// </summary>
    public string? Expected { get; init; }

    /// <summary>
    /// Gets the actual value received in the request, or <see langword="null"/> when the key was absent.
    /// </summary>
    public string? Actual { get; init; }

    /// <summary>
    /// Gets the failure kind. One of <c>missing</c> (key absent from request) or <c>unequal</c> (key present but value did not match).
    /// </summary>
    public string Kind { get; init; } = string.Empty;
}
