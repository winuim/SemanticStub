namespace SemanticStub.Api.Inspection;

/// <summary>
/// Describes the current runtime state for a configured scenario.
/// </summary>
public sealed class ScenarioStateInfo
{
    /// <summary>Gets the scenario name defined in YAML.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the scenario's current state value.</summary>
    public required string CurrentState { get; init; }

    /// <summary>Gets the UTC timestamp when the current state was last established, when available.</summary>
    public DateTimeOffset? LastUpdatedTimestamp { get; init; }
}
