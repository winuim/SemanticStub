namespace SemanticStub.Api.Inspection;

/// <summary>
/// Describes the effective runtime details for one active route.
/// </summary>
public sealed class StubRouteDetailInfo
{
    /// <summary>
    /// Gets the stable identifier for this route.
    /// Equal to the operation's <c>operationId</c> when present; otherwise formatted as
    /// <c>METHOD:/path</c>.
    /// </summary>
    public required string RouteId { get; init; }

    /// <summary>Gets the HTTP method in upper-case (e.g. <c>GET</c>, <c>POST</c>).</summary>
    public required string Method { get; init; }

    /// <summary>Gets the normalized path pattern as defined in the active configuration.</summary>
    public required string PathPattern { get; init; }

    /// <summary>Gets whether any conditional match on this route uses semantic matching.</summary>
    public required bool UsesSemanticMatching { get; init; }

    /// <summary>Gets whether any response on this route participates in a scenario state machine.</summary>
    public required bool UsesScenario { get; init; }

    /// <summary>Gets the number of top-level response definitions on this route.</summary>
    public required int ResponseCount { get; init; }

    /// <summary>Gets whether this route defines any conditional <c>x-match</c> candidates.</summary>
    public required bool HasConditionalMatches { get; init; }

    /// <summary>Gets the top-level responses defined on this route.</summary>
    public IReadOnlyList<StubRouteResponseInfo> Responses { get; init; } = [];

    /// <summary>Gets normalized metadata for each conditional <c>x-match</c> candidate.</summary>
    public IReadOnlyList<StubRouteConditionInfo> ConditionalMatches { get; init; } = [];
}
