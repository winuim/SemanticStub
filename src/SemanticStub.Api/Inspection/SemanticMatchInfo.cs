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
    /// Gets the machine-readable semantic selection outcome.
    /// </summary>
    public string SelectionStatus { get; init; } = "notAttempted";

    /// <summary>
    /// Gets the machine-readable reason why no semantic candidate was selected.
    /// </summary>
    public string? NonSelectionReason { get; init; }

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
    /// Gets the second-best above-threshold score when available.
    /// </summary>
    public double? SecondBestScore { get; init; }

    /// <summary>
    /// Gets the score gap between the selected candidate and the second-best above-threshold candidate when available.
    /// </summary>
    public double? MarginToSecondBest { get; init; }

    /// <summary>
    /// Gets the highest-scoring candidate index when semantic scores were calculated.
    /// </summary>
    public int? BestCandidateIndex { get; init; }

    /// <summary>
    /// Gets the highest semantic score when semantic scores were calculated.
    /// </summary>
    public double? BestScore { get; init; }

    /// <summary>
    /// Gets the second-highest scoring above-threshold candidate index when available.
    /// </summary>
    public int? SecondBestCandidateIndex { get; init; }

    /// <summary>
    /// Gets the semantic candidates and their scores when requested.
    /// </summary>
    public IReadOnlyList<SemanticCandidateInfo> Candidates { get; init; } = [];
}
