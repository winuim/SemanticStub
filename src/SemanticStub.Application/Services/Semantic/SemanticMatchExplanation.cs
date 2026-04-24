using SemanticStub.Application.Models;

namespace SemanticStub.Application.Services.Semantic;

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
    /// Gets the highest-scoring candidate when semantic scores were calculated.
    /// </summary>
    public QueryMatchDefinition? BestCandidate { get; init; }

    /// <summary>
    /// Gets the highest semantic score when semantic scores were calculated.
    /// </summary>
    public double? BestScore { get; init; }

    /// <summary>
    /// Gets the second-highest scoring candidate that satisfied the configured threshold when available.
    /// </summary>
    public QueryMatchDefinition? SecondBestCandidate { get; init; }

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
    /// Gets the second-best above-threshold score when available.
    /// </summary>
    public double? SecondBestScore { get; init; }

    /// <summary>
    /// Gets the score gap between the selected and second-best above-threshold candidates when available.
    /// </summary>
    public double? MarginToSecondBest { get; init; }

    /// <summary>
    /// Gets the machine-readable semantic selection outcome.
    /// </summary>
    public string SelectionStatus { get; init; } = SemanticSelectionStatus.NotAttempted;

    /// <summary>
    /// Gets the machine-readable reason why no semantic candidate was selected.
    /// </summary>
    public string? NonSelectionReason { get; init; }

    /// <summary>
    /// Gets the per-candidate semantic scores when requested.
    /// </summary>
    public IReadOnlyList<SemanticCandidateScore> CandidateScores { get; init; } = [];
}

/// <summary>
/// Defines machine-readable semantic selection outcomes.
/// </summary>
public static class SemanticSelectionStatus
{
    /// <summary>Semantic matching was not attempted.</summary>
    public const string NotAttempted = "notAttempted";

    /// <summary>A semantic candidate was selected.</summary>
    public const string Selected = "selected";

    /// <summary>No candidate reached the configured similarity threshold.</summary>
    public const string BelowThreshold = "belowThreshold";

    /// <summary>The top candidates were too close to select safely.</summary>
    public const string Ambiguous = "ambiguous";

    /// <summary>Semantic matching was attempted, but no score could be produced.</summary>
    public const string Unavailable = "unavailable";
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
