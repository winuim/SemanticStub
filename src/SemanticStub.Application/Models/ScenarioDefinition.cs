using YamlDotNet.Serialization;

namespace SemanticStub.Application.Models;

/// <summary>
/// Describes the scenario state required for a response to be eligible and the optional next state to persist after that response is selected.
/// </summary>
public sealed class ScenarioDefinition
{
    [YamlMember(Alias = "name", ApplyNamingConventions = false)]
    public string Name { get; init; } = string.Empty;

    [YamlMember(Alias = "state", ApplyNamingConventions = false)]
    public string State { get; init; } = string.Empty;

    [YamlMember(Alias = "next", ApplyNamingConventions = false)]
    public string? Next { get; init; }
}
