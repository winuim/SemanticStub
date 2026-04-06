namespace SemanticStub.Api.Inspection;

/// <summary>
/// Describes the semantic fallback evaluation for an inspected request.
/// </summary>
public sealed class SemanticMatchInfo
{
    /// <summary>
    /// Gets whether semantic matching was attempted.
    /// </summary>
    public bool Attempted { get; init; }

    /// <summary>
    /// Gets the configured similarity threshold when semantic matching was attempted.
    /// </summary>
    public double? Threshold { get; init; }

    /// <summary>
    /// Gets the configured ambiguity margin when semantic matching was attempted.
    /// </summary>
    public double? RequiredMargin { get; init; }

    /// <summary>
    /// Gets the selected score when semantic matching produced a candidate.
    /// </summary>
    public double? SelectedScore { get; init; }

    /// <summary>
    /// Gets the second-best score when available.
    /// </summary>
    public double? SecondBestScore { get; init; }

    /// <summary>
    /// Gets the score gap between the selected candidate and the second-best candidate when available.
    /// </summary>
    public double? MarginToSecondBest { get; init; }

    /// <summary>
    /// Gets the semantic candidates and their scores when requested.
    /// </summary>
    public IReadOnlyList<SemanticCandidateInfo> Candidates { get; init; } = [];
}
