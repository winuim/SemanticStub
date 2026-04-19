using SemanticStub.Application.Models;

namespace SemanticStub.Application.Services;

/// <summary>
/// Describes how one <c>x-match</c> candidate evaluated against request data.
/// </summary>
public sealed class QueryMatchCandidateEvaluation
{
    /// <summary>
    /// Gets the candidate definition that was evaluated.
    /// </summary>
    public QueryMatchDefinition Candidate { get; init; } = new();

    /// <summary>
    /// Gets whether query constraints matched.
    /// </summary>
    public bool QueryMatched { get; init; }

    /// <summary>
    /// Gets whether header constraints matched.
    /// </summary>
    public bool HeaderMatched { get; init; }

    /// <summary>
    /// Gets whether body constraints matched.
    /// </summary>
    public bool BodyMatched { get; init; }

    /// <summary>
    /// Gets whether all deterministic dimensions matched.
    /// </summary>
    public bool Matched => QueryMatched && HeaderMatched && BodyMatched;
}
