namespace SemanticStub.Api.Inspection;

/// <summary>
/// Wraps the match improvement suggestions produced for a single request evaluation.
/// </summary>
public sealed class MatchImprovementReportInfo
{
    /// <summary>
    /// Gets the explanation that the suggestions were derived from.
    /// </summary>
    public required MatchExplanationInfo Explanation { get; init; }

    /// <summary>
    /// Gets the ordered list of improvement suggestions. Empty when no issues were detected.
    /// </summary>
    public IReadOnlyList<MatchImprovementSuggestionInfo> Suggestions { get; init; } = [];
}
