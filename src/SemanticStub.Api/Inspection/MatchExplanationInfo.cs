namespace SemanticStub.Api.Inspection;

/// <summary>
/// Describes why an inspected request matched or did not match.
/// </summary>
public sealed class MatchExplanationInfo
{
    /// <summary>
    /// Gets the simulated match result.
    /// </summary>
    public MatchSimulationInfo Result { get; init; } = new();

    /// <summary>
    /// Gets whether the request path matched any configured route.
    /// </summary>
    public bool PathMatched { get; init; }

    /// <summary>
    /// Gets whether the request method matched the resolved path.
    /// </summary>
    public bool MethodMatched { get; init; }

    /// <summary>
    /// Gets the deterministic candidate evaluations.
    /// </summary>
    public IReadOnlyList<MatchCandidateInfo> DeterministicCandidates { get; init; } = [];

    /// <summary>
    /// Gets the semantic fallback evaluation details when relevant.
    /// </summary>
    public SemanticMatchInfo? SemanticEvaluation { get; init; }

    /// <summary>
    /// Gets a concise explanation of the selected outcome.
    /// </summary>
    public string SelectionReason { get; init; } = string.Empty;
}
