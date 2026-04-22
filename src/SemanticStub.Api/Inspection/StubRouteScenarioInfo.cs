namespace SemanticStub.Api.Inspection;

/// <summary>
/// Describes scenario metadata exposed through route inspection.
/// </summary>
public sealed class StubRouteScenarioInfo
{
    /// <summary>Gets the scenario name defined in YAML.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the required current scenario state.</summary>
    public required string State { get; init; }

    /// <summary>Gets whether the response advances the scenario to a different state after it matches.</summary>
    public bool AdvancesScenarioState { get; init; }

    /// <summary>Gets the next scenario state persisted after a match, when configured.</summary>
    public string? Next { get; init; }
}
