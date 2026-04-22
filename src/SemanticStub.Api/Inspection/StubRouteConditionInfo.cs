namespace SemanticStub.Api.Inspection;

/// <summary>
/// Describes normalized metadata for one conditional <c>x-match</c> candidate.
/// </summary>
public sealed class StubRouteConditionInfo
{
    /// <summary>Gets the candidate index within the route's <c>x-match</c> list.</summary>
    public int CandidateIndex { get; init; }

    /// <summary>Gets whether the candidate defines exact query constraints.</summary>
    public bool HasExactQuery { get; init; }

    /// <summary>Gets the exact query parameter names constrained by the candidate.</summary>
    public IReadOnlyList<string> ExactQueryKeys { get; init; } = [];

    /// <summary>Gets whether the candidate defines regex query constraints.</summary>
    public bool HasRegexQuery { get; init; }

    /// <summary>Gets the regex query parameter names constrained by the candidate.</summary>
    public IReadOnlyList<string> RegexQueryKeys { get; init; } = [];

    /// <summary>Gets the header names constrained by the candidate.</summary>
    public IReadOnlyList<string> HeaderKeys { get; init; } = [];

    /// <summary>Gets whether the candidate defines a body constraint.</summary>
    public bool HasBody { get; init; }

    /// <summary>Gets whether the candidate uses semantic matching.</summary>
    public bool UsesSemanticMatching { get; init; }

    /// <summary>Gets the response status code selected by the candidate.</summary>
    public int ResponseStatusCode { get; init; }

    /// <summary>Gets the configured response delay in milliseconds when present.</summary>
    public int? DelayMilliseconds { get; init; }

    /// <summary>Gets the configured response media types in stable order.</summary>
    public IReadOnlyList<string> MediaTypes { get; init; } = [];

    /// <summary>Gets whether the candidate response participates in a scenario state machine.</summary>
    public bool UsesScenario { get; init; }

    /// <summary>Gets the configured scenario metadata when present.</summary>
    public StubRouteScenarioInfo? Scenario { get; init; }
}
