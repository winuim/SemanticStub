namespace SemanticStub.Api.Inspection;

/// <summary>
/// Describes why a single dimension of a deterministic <c>x-match</c> candidate failed.
/// </summary>
public sealed class MatchDimensionMismatchInfo
{
    /// <summary>
    /// Gets the matching dimension that failed. One of <c>query</c>, <c>header</c>, <c>scenario</c>, or <c>response</c>.
    /// </summary>
    public string Dimension { get; init; } = string.Empty;

    /// <summary>
    /// Gets the parameter or header name, or the scenario name for scenario dimension failures.
    /// <see langword="null"/> for <c>response</c> dimension entries.
    /// </summary>
    public string? Key { get; init; }

    /// <summary>
    /// Gets the expected value or pattern declared in the stub definition.
    /// <see langword="null"/> for <c>response</c> dimension entries.
    /// </summary>
    public string? Expected { get; init; }

    /// <summary>
    /// Gets the actual value received in the request or the current scenario state.
    /// <see langword="null"/> when the key was absent or the dimension is <c>response</c>.
    /// </summary>
    public string? Actual { get; init; }

    /// <summary>
    /// Gets the failure kind. One of <c>missing</c>, <c>unequal</c>, or <c>notConfigured</c>.
    /// </summary>
    public string Kind { get; init; } = string.Empty;
}
