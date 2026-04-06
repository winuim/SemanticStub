using SemanticStub.Api.Models;

namespace SemanticStub.Api.Services;

/// <summary>
/// Describes the result of a semantic fallback evaluation.
/// </summary>
public sealed class SemanticMatchExplanation
{
    /// <summary>
    /// Gets whether semantic matching was attempted.
    /// </summary>
    public bool Attempted { get; init; }

    /// <summary>
    /// Gets the best acceptable candidate when one was selected.
    /// </summary>
    public QueryMatchDefinition? SelectedCandidate { get; init; }

    /// <summary>
    /// Gets the selected candidate score when one was selected.
    /// </summary>
    public double? SelectedScore { get; init; }

    /// <summary>
    /// Gets the configured similarity threshold when semantic matching was attempted.
    /// </summary>
    public double? Threshold { get; init; }

    /// <summary>
    /// Gets the configured ambiguity margin when semantic matching was attempted.
    /// </summary>
    public double? RequiredMargin { get; init; }

    /// <summary>
    /// Gets the second-best score when available.
    /// </summary>
    public double? SecondBestScore { get; init; }

    /// <summary>
    /// Gets the score gap between the selected and second-best candidates when available.
    /// </summary>
    public double? MarginToSecondBest { get; init; }

    /// <summary>
    /// Gets the per-candidate semantic scores when requested.
    /// </summary>
    public IReadOnlyList<SemanticCandidateScore> CandidateScores { get; init; } = [];
}

/// <summary>
/// Describes the semantic score assigned to one candidate.
/// </summary>
public sealed class SemanticCandidateScore
{
    /// <summary>
    /// Gets the candidate definition that was evaluated.
    /// </summary>
    public QueryMatchDefinition Candidate { get; init; } = new();

    /// <summary>
    /// Gets whether the candidate was eligible for semantic scoring.
    /// </summary>
    public bool Eligible { get; init; }

    /// <summary>
    /// Gets the cosine similarity score when one was calculated.
    /// </summary>
    public double? Score { get; init; }

    /// <summary>
    /// Gets whether the score satisfied the configured threshold.
    /// </summary>
    public bool AboveThreshold { get; init; }
}
