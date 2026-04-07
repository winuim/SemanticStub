namespace SemanticStub.Api.Inspection;

/// <summary>
/// Describes the semantic score assigned to one semantic candidate.
/// </summary>
public sealed class SemanticCandidateInfo
{
    /// <summary>
    /// Gets the candidate index within the operation's <c>x-match</c> list.
    /// </summary>
    public int CandidateIndex { get; init; }

    /// <summary>
    /// Gets whether the candidate was eligible for semantic evaluation.
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
