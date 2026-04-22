namespace SemanticStub.Api.Inspection;

/// <summary>
/// Describes one top-level response configured for a route.
/// </summary>
public sealed class StubRouteResponseInfo
{
    /// <summary>Gets the stable response identifier from the OpenAPI <c>responses</c> map key.</summary>
    public required string ResponseId { get; init; }

    /// <summary>Gets the configured response delay in milliseconds when present.</summary>
    public int? DelayMilliseconds { get; init; }

    /// <summary>Gets the configured response file name when <c>x-response-file</c> is used.</summary>
    public string? ResponseFile { get; init; }

    /// <summary>Gets the configured response media types in stable order.</summary>
    public IReadOnlyList<string> MediaTypes { get; init; } = [];

    /// <summary>Gets whether the response participates in a scenario state machine.</summary>
    public bool UsesScenario { get; init; }

    /// <summary>Gets the configured scenario metadata when present.</summary>
    public StubRouteScenarioInfo? Scenario { get; init; }
}
