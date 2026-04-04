namespace SemanticStub.Api.Inspection;

/// <summary>
/// Describes a single route (path + HTTP method) registered in the active stub configuration.
/// </summary>
public sealed class StubRouteInfo
{
    /// <summary>
    /// Gets the stable identifier for this route.
    /// Equal to the operation's <c>operationId</c> when present; otherwise formatted as
    /// <c>METHOD:/path</c> (e.g. <c>GET:/users</c>).
    /// </summary>
    public required string RouteId { get; init; }

    /// <summary>Gets the HTTP method in upper-case (e.g. <c>GET</c>, <c>POST</c>).</summary>
    public required string Method { get; init; }

    /// <summary>Gets the normalized path pattern as defined in the stub YAML (e.g. <c>/users/{id}</c>).</summary>
    public required string PathPattern { get; init; }

    /// <summary>Gets whether any conditional match on this route uses semantic (embedding-based) matching.</summary>
    public required bool UsesSemanticMatching { get; init; }

    /// <summary>Gets whether any response on this route participates in a scenario state machine.</summary>
    public required bool UsesScenario { get; init; }

    /// <summary>Gets the number of named response definitions on this route's operation.</summary>
    public required int ResponseCount { get; init; }
}
