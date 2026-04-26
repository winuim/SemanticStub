namespace SemanticStub.Api.Inspection;

/// <summary>
/// Describes one actionable suggestion for improving an existing stub definition.
/// </summary>
public sealed class MatchImprovementSuggestionInfo
{
    /// <summary>
    /// Gets the suggestion kind. One of <c>SemanticFallbackUsed</c>, <c>NoConditionsOnRoute</c>,
    /// <c>NearMissCandidate</c>, or <c>NoMatchFound</c>.
    /// </summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>
    /// Gets a human-readable description of the detected issue.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Gets a YAML-oriented hint describing what to add or change in the stub definition.
    /// </summary>
    public string YamlHint { get; init; } = string.Empty;

    /// <summary>
    /// Gets the candidate index within the operation's <c>x-match</c> list when the suggestion
    /// is tied to a specific candidate. <see langword="null"/> for route-level suggestions.
    /// </summary>
    public int? CandidateIndex { get; init; }

    /// <summary>
    /// Gets the matching dimension that the suggestion targets, such as <c>query</c>,
    /// <c>header</c>, or <c>body</c>. <see langword="null"/> when not dimension-specific.
    /// </summary>
    public string? Dimension { get; init; }
}
