namespace SemanticStub.Api.Inspection;

/// <summary>
/// Describes the result of a dry-run request match evaluation.
/// </summary>
public sealed class MatchSimulationInfo
{
    /// <summary>
    /// Gets whether a match was found.
    /// </summary>
    public bool Matched { get; init; }

    /// <summary>
    /// Gets the stable match result name.
    /// </summary>
    public string MatchResult { get; init; } = string.Empty;

    /// <summary>
    /// Gets the resolved route identifier when a path and method matched.
    /// </summary>
    public string? RouteId { get; init; }

    /// <summary>
    /// Gets the resolved HTTP method when a path and method matched.
    /// </summary>
    public string? Method { get; init; }

    /// <summary>
    /// Gets the resolved path pattern when a path and method matched.
    /// </summary>
    public string? PathPattern { get; init; }

    /// <summary>
    /// Gets the selected response identifier when a response was selected.
    /// </summary>
    public string? SelectedResponseId { get; init; }

    /// <summary>
    /// Gets the selected response status code when a response was selected.
    /// </summary>
    public int? SelectedResponseStatusCode { get; init; }

    /// <summary>
    /// Gets the YAML response source that produced the selected response when available.
    /// Expected values are <c>responses</c> and <c>x-match</c>.
    /// </summary>
    public string? SelectedResponseSource { get; init; }

    /// <summary>
    /// Gets the selected conditional candidate index when the response came from <c>x-match</c>.
    /// </summary>
    public int? SelectedResponseCandidateIndex { get; init; }

    /// <summary>
    /// Gets the match mode when a response was selected.
    /// </summary>
    public string? MatchMode { get; init; }

    /// <summary>
    /// Gets the candidate-level evaluation details when requested.
    /// </summary>
    public IReadOnlyList<MatchCandidateInfo> Candidates { get; init; } = [];
}
