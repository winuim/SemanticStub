namespace SemanticStub.Api.Inspection;

/// <summary>
/// Describes how one conditional candidate was evaluated for an inspected request.
/// </summary>
public sealed class MatchCandidateInfo
{
    /// <summary>
    /// Gets the candidate index within the operation's <c>x-match</c> list.
    /// </summary>
    public int CandidateIndex { get; init; }

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
    /// Gets whether the candidate is eligible for the current scenario state.
    /// </summary>
    public bool ScenarioMatched { get; init; }

    /// <summary>
    /// Gets whether the candidate defines a usable response.
    /// </summary>
    public bool ResponseConfigured { get; init; }

    /// <summary>
    /// Gets whether every match dimension and scenario check passed.
    /// </summary>
    public bool Matched { get; init; }

    /// <summary>
    /// Gets the selected response identifier for the candidate when available.
    /// </summary>
    public string? ResponseId { get; init; }

    /// <summary>
    /// Gets the selected response status code for the candidate when available.
    /// </summary>
    public int? ResponseStatusCode { get; init; }

    /// <summary>
    /// Gets the per-key mismatch details for failed dimensions.
    /// Covers query, header, scenario, and response-configuration failures.
    /// Empty when the candidate matched or when no individual key failures were collected.
    /// </summary>
    public IReadOnlyList<MatchDimensionMismatchInfo> MismatchReasons { get; init; } = [];
}
